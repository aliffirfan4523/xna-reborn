using System.Text.RegularExpressions;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The detected MonoGame version and platform of one project, and a conflict diagnostic when signals disagree.</summary>
/// <param name="Version">The chosen version or band. <see cref="MonoGameVersionInfo.Unknown"/> when there is no signal.</param>
/// <param name="Platform">The platform, or <see cref="MonoGamePlatform.Unknown"/>.</param>
/// <param name="Conflict">Diagnostic ANA009 when version signals disagree, otherwise null.</param>
public sealed record MonoGameDetection(MonoGameVersionInfo Version, MonoGamePlatform Platform, Diagnostic? Conflict);

/// <summary>
/// Detects the MonoGame version and platform of a project from signals in its project file and packages.config.
/// It never runs MSBuild. An exact version beats a band; with no signal the result is Unknown and nothing is guessed.
/// </summary>
public static partial class MonoGameVersionDetector
{
    /// <summary>Diagnostic code used when two version signals disagree.</summary>
    public const string ConflictCode = "ANA009";

    private const string _ownerPrefix = "MonoGame.Framework";

    private static readonly Version _maxVersion = new(int.MaxValue, int.MaxValue);

    // Most specific first. A bounded range beats an open one.
    private static readonly MonoGameBand[] _bandPriority =
        [MonoGameBand.V380Era, MonoGameBand.V33To381, MonoGameBand.V32To36, MonoGameBand.V35Plus];

    [GeneratedRegex(@"^MonoGame\.Framework[^,]*,\s*Version\s*=\s*(?<v>\d+(?:\.\d+){1,3})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceVersion();

    [GeneratedRegex(@"MonoGame\.Framework(?:\.[A-Za-z][A-Za-z0-9]*)?\.(?<v>\d+\.\d+\.\d+(?:\.\d+)?)(?=[\\/])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackagePathVersion();

    [GeneratedRegex(@"MonoGame\.Framework\.(?<p>[A-Za-z][A-Za-z0-9]*)\.\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackagePathPlatform();

    [GeneratedRegex(@"Assemblies\\(?<p>[A-Za-z0-9]+)\\", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssembliesFolder();

    [GeneratedRegex(@"^\d+(?:\.\d+){1,3}", RegexOptions.CultureInvariant)]
    private static partial Regex NumericPrefix();

    /// <summary>Detects version and platform.</summary>
    /// <param name="properties">Project properties by name (first value wins).</param>
    /// <param name="items">Project items, including Reference, PackageReference and Import.</param>
    /// <param name="packages">Entries of the project's packages.config, or empty.</param>
    /// <param name="projectPath">Relative project path, used as the diagnostic file.</param>
    /// <returns>The version, platform and an optional conflict diagnostic.</returns>
    public static MonoGameDetection Detect(
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<ProjectItem> items,
        IReadOnlyList<PackageEntry> packages,
        string projectPath)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(packages);
        var (version, conflict) = DetectVersion(properties, items, packages, projectPath);
        return new MonoGameDetection(version, DetectPlatform(properties, items, packages), conflict);
    }

    private static (MonoGameVersionInfo Version, Diagnostic? Conflict) DetectVersion(
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<ProjectItem> items,
        IReadOnlyList<PackageEntry> packages,
        string projectPath)
    {
        var exact = new List<(string Source, string Text, Version Value)>();
        var bands = new List<(string Source, MonoGameBand Band, Version Min, Version Max)>();

        foreach (var item in items)
        {
            if (item.ItemType == "Reference")
            {
                var fromInclude = ReferenceVersion().Match(item.Include);
                AddExact(exact, "the MonoGame.Framework reference", fromInclude.Success ? fromInclude.Groups["v"].Value : null);
            }

            if (item.ItemType == "PackageReference" && item.Include.StartsWith(_ownerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddExact(exact, "a PackageReference", item.Metadata.GetValueOrDefault("Version"));
            }

            if (item.Metadata.TryGetValue("HintPath", out var hint))
            {
                var path = hint.Replace('/', '\\');
                var fromPath = PackagePathVersion().Match(path);
                AddExact(exact, "a package path", fromPath.Success ? fromPath.Groups["v"].Value : null);
                AddFolderBand(bands, path);
            }

            if (item.ItemType == "Import" && item.Include.Contains("MonoGame.Common.props", StringComparison.OrdinalIgnoreCase))
            {
                bands.Add(("the MonoGame.Common.props import", MonoGameBand.V35Plus, new Version(3, 5), _maxVersion));
            }
        }

        foreach (var package in packages.Where(p => p.Id.StartsWith(_ownerPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            AddExact(exact, "packages.config", package.Version);
        }

        if (properties.TryGetValue("ProjectTypeGuids", out var guids) && guids.Contains("A5A43C5B-DE2A-4C0C-9213-0A381AF9435A", StringComparison.OrdinalIgnoreCase))
        {
            bands.Add(("the UWP project type", MonoGameBand.V33To381, new Version(3, 3), new Version(3, 8, 2)));
        }

        if (items.Any(i => i.ItemType == "PackageReference" && i.Include.StartsWith(_ownerPrefix, StringComparison.OrdinalIgnoreCase))
            && properties.TryGetValue("TargetFramework", out var framework)
            && (framework.Equals("netcoreapp3.1", StringComparison.OrdinalIgnoreCase) || framework.Equals("net5.0", StringComparison.OrdinalIgnoreCase)))
        {
            bands.Add(($"the {framework} target", MonoGameBand.V380Era, new Version(3, 8, 0), new Version(3, 8, 1)));
        }

        if (exact.Count > 0)
        {
            var chosen = exact.OrderByDescending(e => e.Value).ThenBy(e => e.Text, StringComparer.Ordinal).First();
            return (new MonoGameVersionInfo(chosen.Text, BandOf(chosen.Value), Confidence.High), ConflictFor(exact, bands, chosen, projectPath));
        }

        foreach (var band in _bandPriority)
        {
            if (bands.Any(b => b.Band == band))
            {
                return (new MonoGameVersionInfo(null, band, Confidence.Medium), null);
            }
        }

        return (MonoGameVersionInfo.Unknown, null);
    }

    private static void AddFolderBand(List<(string Source, MonoGameBand Band, Version Min, Version Max)> bands, string hintPath)
    {
        var folder = AssembliesFolder().Match(hintPath);
        if (!folder.Success)
        {
            return;
        }

        switch (folder.Groups["p"].Value.ToLowerInvariant())
        {
            case "windowsgl":
                bands.Add(("the Assemblies\\WindowsGL folder", MonoGameBand.V32To36, new Version(3, 2), new Version(3, 7)));
                break;
            case "desktopgl":
                bands.Add(("the Assemblies\\DesktopGL folder", MonoGameBand.V35Plus, new Version(3, 5), _maxVersion));
                break;
            case "windowsuniversal":
                bands.Add(("the Assemblies\\WindowsUniversal folder", MonoGameBand.V33To381, new Version(3, 3), new Version(3, 8, 2)));
                break;
            default:
                break;
        }
    }

    private static void AddExact(List<(string Source, string Text, Version Value)> exact, string source, string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        var prefix = NumericPrefix().Match(trimmed);
        if (prefix.Success && Version.TryParse(prefix.Value, out var value))
        {
            exact.Add((source, trimmed, value));
        }
    }

    private static Diagnostic? ConflictFor(
        List<(string Source, string Text, Version Value)> exact,
        List<(string Source, MonoGameBand Band, Version Min, Version Max)> bands,
        (string Source, string Text, Version Value) chosen,
        string projectPath)
    {
        var differing = exact.Where(e => e.Value != chosen.Value).ToList();
        var outside = bands.Where(b => chosen.Value < b.Min || chosen.Value >= b.Max).ToList();
        if (differing.Count == 0 && outside.Count == 0)
        {
            return null;
        }

        var parts = new List<string> { $"{chosen.Source} says {chosen.Text}" };
        parts.AddRange(differing.Select(e => $"{e.Source} says {e.Text}"));
        parts.AddRange(outside.Select(b => $"{b.Source} implies {Describe(b.Min, b.Max)}"));
        return new Diagnostic(ConflictCode, $"MonoGame version signals disagree: {string.Join(", ", parts)}. Using {chosen.Text}.", projectPath);
    }

    private static string Describe(Version min, Version max) =>
        max == _maxVersion
            ? $"{min} or later"
            : $"{min} up to (not including) {max}";

    private static MonoGameBand BandOf(Version version) => (version.Major, version.Minor) switch
    {
        (3, <= 4) => MonoGameBand.V3Early,
        (3, 5) => MonoGameBand.V35,
        (3, 6) => MonoGameBand.V36,
        (3, 7) => MonoGameBand.V37,
        (3, 8) => MonoGameBand.V38,
        _ => MonoGameBand.Unknown,
    };

    private static MonoGamePlatform DetectPlatform(
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<ProjectItem> items,
        IReadOnlyList<PackageEntry> packages)
    {
        // Project type GUIDs name the platform projects exactly, so they win. Then the property, the assembly folder, the package id.
        if (properties.TryGetValue("ProjectTypeGuids", out var guids) && FromGuids(guids) is { } fromGuid)
        {
            return fromGuid;
        }

        if (properties.TryGetValue("MonoGamePlatform", out var value) && FromPropertyValue(value) is { } fromProperty)
        {
            return fromProperty;
        }

        foreach (var item in items)
        {
            if (item.Metadata.TryGetValue("HintPath", out var hint))
            {
                var path = hint.Replace('/', '\\');
                var folder = AssembliesFolder().Match(path);
                if (folder.Success && FromFolder(folder.Groups["p"].Value) is { } fromFolder)
                {
                    return fromFolder;
                }

                var package = PackagePathPlatform().Match(path);
                if (package.Success && FromPackageSuffix(package.Groups["p"].Value) is { } fromPackagePath)
                {
                    return fromPackagePath;
                }
            }

            if (item.ItemType == "PackageReference" && FromPackageId(item.Include) is { } fromReference)
            {
                return fromReference;
            }
        }

        foreach (var package in packages)
        {
            if (FromPackageId(package.Id) is { } fromPackage)
            {
                return fromPackage;
            }
        }

        return MonoGamePlatform.Unknown;
    }

    private static MonoGamePlatform? FromGuids(string guids)
    {
        if (guids.Contains("EFBA0AD7-5A72-4C68-AF49-83D382785DCF", StringComparison.OrdinalIgnoreCase))
        {
            return MonoGamePlatform.Android;
        }

        if (guids.Contains("FEACFBD2-3405-455C-9665-78FE426C6842", StringComparison.OrdinalIgnoreCase)
            || guids.Contains("6BC8ED88-2882-458C-8E55-DFD12B67127B", StringComparison.OrdinalIgnoreCase))
        {
            return MonoGamePlatform.Ios;
        }

        if (guids.Contains("A5A43C5B-DE2A-4C0C-9213-0A381AF9435A", StringComparison.OrdinalIgnoreCase))
        {
            return MonoGamePlatform.Uwp;
        }

        return guids.Contains("76F1466A-8B6D-4E39-A767-685A06062A39", StringComparison.OrdinalIgnoreCase)
            ? MonoGamePlatform.WindowsPhone
            : null;
    }

    private static MonoGamePlatform? FromPropertyValue(string value) => value.Trim().ToLowerInvariant() switch
    {
        "windows" => MonoGamePlatform.WindowsDX,
        "windowsgl" => MonoGamePlatform.WindowsGL,
        "linux" => MonoGamePlatform.Linux,
        "macosx" => MonoGamePlatform.MacOS,
        "desktopgl" => MonoGamePlatform.DesktopGL,
        "windowsstoreapp" => MonoGamePlatform.Uwp,
        "windowsphone8" => MonoGamePlatform.WindowsPhone,
        "ouya" => MonoGamePlatform.Ouya,
        "playstationmobile" => MonoGamePlatform.PlayStationMobile,
        "psvita" => MonoGamePlatform.PsVita,
        "android" => MonoGamePlatform.Android,
        "ios" => MonoGamePlatform.Ios,
        _ => null,
    };

    private static MonoGamePlatform? FromFolder(string folder) => folder.ToLowerInvariant() switch
    {
        "windows" => MonoGamePlatform.WindowsDX,
        "windowsgl" => MonoGamePlatform.WindowsGL,
        "linux" => MonoGamePlatform.Linux,
        "macos" or "macosx" => MonoGamePlatform.MacOS,
        "desktopgl" => MonoGamePlatform.DesktopGL,
        "windowsuniversal" => MonoGamePlatform.Uwp,
        "windowsphone8" => MonoGamePlatform.WindowsPhone,
        "ouya" => MonoGamePlatform.Ouya,
        "android" => MonoGamePlatform.Android,
        "ios" or "tvos" => MonoGamePlatform.Ios,
        _ => null,
    };

    private static MonoGamePlatform? FromPackageSuffix(string suffix) => suffix.ToLowerInvariant() switch
    {
        "windowsdx" => MonoGamePlatform.WindowsDX,
        "windowsgl" => MonoGamePlatform.WindowsGL,
        "linux" => MonoGamePlatform.Linux,
        "macos" => MonoGamePlatform.MacOS,
        "desktopgl" => MonoGamePlatform.DesktopGL,
        "windowsuniversal" => MonoGamePlatform.Uwp,
        "android" => MonoGamePlatform.Android,
        "ios" => MonoGamePlatform.Ios,
        _ => null,
    };

    private static MonoGamePlatform? FromPackageId(string id) =>
        id.StartsWith(_ownerPrefix + ".", StringComparison.OrdinalIgnoreCase)
            ? FromPackageSuffix(id[(_ownerPrefix.Length + 1)..])
            : null;
}
