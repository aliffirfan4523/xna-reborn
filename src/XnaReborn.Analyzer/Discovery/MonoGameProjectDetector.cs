using System.Xml.Linq;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>Decides from project XML whether a project uses MonoGame, and whether it is already a current 3.8.6.1 desktop project. Pure: no disk access.</summary>
internal static class MonoGameProjectDetector
{
    /// <summary>
    /// The MonoGame version the migrator writes. Must equal <c>monoGame</c> in <c>Projects/packages.json</c> of the Migrator
    /// (a test checks it), because the analyzer cannot reference the Migrator.
    /// </summary>
    internal const string CurrentVersion = "3.8.6.1";

    private const string _frameworkPrefix = "MonoGame.Framework";

    /// <summary>Classifies a project: Xna wins over MonoGame, then MonoGameCurrent or MonoGameLegacy, otherwise Other.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    /// <param name="packages">Entries of the project's packages.config, or empty.</param>
    internal static ProjectFamily Classify(XDocument document, IReadOnlyList<PackageEntry> packages)
    {
        if (XnaProjectDetector.IsXna(document))
        {
            return ProjectFamily.Xna;
        }

        if (!IsMonoGame(document, packages))
        {
            return ProjectFamily.Other;
        }

        return IsCurrent(document) ? ProjectFamily.MonoGameCurrent : ProjectFamily.MonoGameLegacy;
    }

    /// <summary>Returns true when the project has any MonoGame signal from R1.2.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    /// <param name="packages">Entries of the project's packages.config, or empty.</param>
    internal static bool IsMonoGame(XDocument document, IReadOnlyList<PackageEntry> packages)
    {
        if (packages.Any(p => p.Id.StartsWith(_frameworkPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            var include = (string?)element.Attribute("Include");
            if (name is "Reference" or "PackageReference"
                && include?.StartsWith(_frameworkPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (name.Equals("Import", StringComparison.OrdinalIgnoreCase)
                && IsInstallerImport((string?)element.Attribute("Project")))
            {
                return true;
            }

            if (name is "MonoGamePlatform" or "MonoGameContentReference")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the project is SDK-style, targets net8.0 or net8.0-windows, and every MonoGame.Framework.* package is at <see cref="CurrentVersion"/>.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    internal static bool IsCurrent(XDocument document)
    {
        if (!IsSdkStyle(document))
        {
            return false;
        }

        var frameworks = document.Descendants()
            .Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(e => e.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        if (frameworks.Count == 0
            || !frameworks.All(f => f.Equals("net8.0", StringComparison.OrdinalIgnoreCase) || f.Equals("net8.0-windows", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var versions = MonoGamePackageReferences(document).ToList();
        return versions.Count > 0 && versions.All(v => string.Equals(v, CurrentVersion, StringComparison.Ordinal));
    }

    /// <summary>Names the XNA and MonoGame signals of a mixed project, for the ANA008 message.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    /// <param name="packages">Entries of the project's packages.config, or empty.</param>
    internal static string DescribeMix(XDocument document, IReadOnlyList<PackageEntry> packages)
    {
        var xna = new SortedSet<string>(StringComparer.Ordinal);
        var monoGame = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            var include = (string?)element.Attribute("Include");
            var simple = include is null ? null : include.Split(',')[0].Trim();
            if (name == "Reference" && simple?.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) == true)
            {
                xna.Add(simple);
            }
            else if (name is "Reference" or "PackageReference" && simple?.StartsWith(_frameworkPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                monoGame.Add(simple);
            }
            else if (name == "Import" && IsInstallerImport((string?)element.Attribute("Project")))
            {
                monoGame.Add(Path.GetFileName(((string)element.Attribute("Project")!).Replace('\\', '/')));
            }
            else if (name is "MonoGamePlatform" or "MonoGameContentReference")
            {
                monoGame.Add(name);
            }
        }

        monoGame.UnionWith(packages.Where(p => p.Id.StartsWith(_frameworkPrefix, StringComparison.OrdinalIgnoreCase)).Select(p => p.Id));
        if (xna.Count == 0)
        {
            xna.Add("XNA Game Studio targets");
        }

        return $"XNA: {string.Join(", ", xna)}; MonoGame: {string.Join(", ", monoGame)}";
    }

    /// <summary>The Version of each <c>PackageReference</c> named MonoGame.Framework.*. A reference without a Version gives an empty string.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    internal static IEnumerable<string> MonoGamePackageReferences(XDocument document) =>
        document.Descendants()
            .Where(e => e.Name.LocalName == "PackageReference"
                && ((string?)e.Attribute("Include"))?.StartsWith(_frameworkPrefix, StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => ((string?)e.Attribute("Version") ?? e.Elements().FirstOrDefault(c => c.Name.LocalName == "Version")?.Value)?.Trim() ?? string.Empty);

    /// <summary>True for an import path inside the old installer folder, such as <c>$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Common.props</c>.</summary>
    /// <param name="project">The Project attribute of an Import.</param>
    internal static bool IsInstallerImport(string? project)
    {
        if (project is null)
        {
            return false;
        }

        var normalized = project.Replace('/', '\\');
        return normalized.Contains(@"MonoGame\v3.0\MonoGame.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the project uses <c>Sdk="Microsoft.NET.Sdk"</c> (as an attribute or an Sdk element).</summary>
    /// <param name="document">Parsed .csproj document.</param>
    internal static bool IsSdkStyle(XDocument document) =>
        document.Root is { } root
        && (((string?)root.Attribute("Sdk"))?.StartsWith("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) == true
            || root.Elements().Any(e => e.Name.LocalName == "Sdk" && ((string?)e.Attribute("Name"))?.StartsWith("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) == true));
}
