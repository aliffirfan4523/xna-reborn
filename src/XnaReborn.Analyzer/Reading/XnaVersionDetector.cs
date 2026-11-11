using System.Text.RegularExpressions;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The detected XNA version and, when signals disagree, a diagnostic that says so.</summary>
/// <param name="Version">The chosen version, or <see cref="XnaVersion.Unknown"/> when there is no signal.</param>
/// <param name="Conflict">Diagnostic ANA002 when signals disagree, otherwise null.</param>
public sealed record XnaVersionResult(XnaVersion Version, Diagnostic? Conflict);

/// <summary>
/// Detects the XNA version from three signals, in priority order: the XnaFrameworkVersion property,
/// the XNA Game Studio targets import path, and the versions in XNA Framework references.
/// </summary>
public static partial class XnaVersionDetector
{
    [GeneratedRegex(@"[\\/]v(?<v>\d+\.\d+)[\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex ImportVersion();

    [GeneratedRegex(@"Version\s*=\s*(?<v>\d+\.\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceVersion();

    /// <summary>The project type GUID of an XNA Game Studio Express 1.0 game project.</summary>
    internal const string ExpressProjectTypeGuid = "{9F340DF3-2AED-4330-AC16-78AC2D9B4738}";

    /// <summary>Tells whether an import path is an XNA targets file ("XNA Game Studio" and "Game Studio Express" folders, ending in .targets).</summary>
    /// <param name="importPath">The Project attribute of an Import.</param>
    internal static bool IsGameStudioTargets(string importPath) =>
        importPath.Contains("Game Studio", StringComparison.OrdinalIgnoreCase)
        && importPath.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);

    /// <summary>Detects the version of one project.</summary>
    /// <param name="properties">Project properties by name (first value wins).</param>
    /// <param name="items">Project items, including Import and Reference.</param>
    /// <param name="projectPath">Relative project path, used as the diagnostic file.</param>
    /// <returns>The chosen version and an optional conflict diagnostic.</returns>
    public static XnaVersionResult Detect(
        IReadOnlyDictionary<string, string> properties,
        IReadOnlyList<ProjectItem> items,
        string projectPath)
    {
        var signals = new List<(string Source, XnaVersion Version)>();

        if (properties.TryGetValue("XnaFrameworkVersion", out var property) && Parse(property.TrimStart('v', 'V')) is { } fromProperty)
        {
            signals.Add(("XnaFrameworkVersion", fromProperty));
        }

        foreach (var import in items.Where(i => i.ItemType == "Import" && IsGameStudioTargets(i.Include)))
        {
            var match = ImportVersion().Match(import.Include);
            if (match.Success && Parse(match.Groups["v"].Value) is { } fromImport)
            {
                signals.Add(("the targets import", fromImport));
            }
        }

        var referenceVersions = items
            .Where(i => i.ItemType == "Reference" && i.Include.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase))
            .Select(i => ReferenceVersion().Match(i.Include))
            .Where(m => m.Success)
            .Select(m => Parse(m.Groups["v"].Value))
            .OfType<XnaVersion>()
            .Distinct();
        foreach (var version in referenceVersions)
        {
            signals.Add(("the framework references", version));
        }

        // Lowest priority: the Express 1.0 project type GUID. It only decides when nothing else does, so it never causes ANA002.
        if (signals.Count == 0
            && properties.TryGetValue("ProjectTypeGuids", out var guids)
            && guids.Contains(ExpressProjectTypeGuid, StringComparison.OrdinalIgnoreCase))
        {
            return new XnaVersionResult(XnaVersion.V10, null);
        }

        if (signals.Count == 0)
        {
            return new XnaVersionResult(XnaVersion.Unknown, null);
        }

        var chosen = signals[0].Version;
        if (signals.All(s => s.Version == chosen))
        {
            return new XnaVersionResult(chosen, null);
        }

        var details = string.Join(", ", signals.Select(s => $"{s.Source} says {Format(s.Version)}"));
        var conflict = new Diagnostic("ANA002", $"XNA version signals disagree: {details}. Using {Format(chosen)}.", projectPath);
        return new XnaVersionResult(chosen, conflict);
    }

    private static XnaVersion? Parse(string text) => text.Trim() switch
    {
        "1.0" => XnaVersion.V10,
        "2.0" => XnaVersion.V20,
        "3.0" => XnaVersion.V30,
        "3.1" => XnaVersion.V31,
        "4.0" => XnaVersion.V40,
        _ => null,
    };

    private static string Format(XnaVersion version) => version switch
    {
        XnaVersion.V10 => "1.0",
        XnaVersion.V20 => "2.0",
        XnaVersion.V30 => "3.0",
        XnaVersion.V31 => "3.1",
        XnaVersion.V40 => "4.0",
        _ => "Unknown",
    };
}
