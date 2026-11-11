using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Content;
using XnaReborn.Core.Mgcb;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>One place in a Content.mgcb file where a rule matched.</summary>
/// <param name="Path">Relative path of the mgcb file.</param>
/// <param name="Line">1-based line of the match.</param>
/// <param name="Text">Short description for the finding snippet.</param>
internal sealed record MgcbMatchResult(string Path, int Line, string Text);

/// <summary>Matches <see cref="MgcbSettingMatch"/> against the Content.mgcb files that were read for a project.</summary>
internal static class MgcbSettingMatcher
{
    /// <summary>Returns every match in file order, then line order. Empty when the project has no mgcb file or nothing matches.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<MgcbMatchResult> Match(MgcbSettingMatch match, ProjectReadResult project)
    {
        var results = new List<MgcbMatchResult>();
        foreach (var mgcb in project.MgcbFiles ?? [])
        {
            switch (match.Setting.ToLowerInvariant())
            {
                case "reference":
                    results.AddRange(mgcb.File.References
                        .Where(r => ValuePasses(match, r.Path))
                        .Select(r => new MgcbMatchResult(mgcb.Path, r.Line, "reference=" + r.Path)));
                    break;
                case "entry":
                    results.AddRange(mgcb.File.Entries
                        .Where(e => EntryPasses(match, e))
                        .Select(e => new MgcbMatchResult(mgcb.Path, e.Line, e.Source + EntryNames(e))));
                    break;
                default:
                    // platform, profile, config, compress: the first occurrence in the file decides.
                    if (mgcb.File.Setting(match.Setting) is { } setting && ValuePasses(match, setting.Value ?? string.Empty))
                    {
                        results.Add(new MgcbMatchResult(mgcb.Path, setting.Line, setting.Name + "=" + setting.Value));
                    }

                    break;
            }
        }

        return results;
    }

    private static bool ValuePasses(MgcbSettingMatch match, string value) =>
        (match.EqualsValue is null || string.Equals(value, match.EqualsValue, StringComparison.OrdinalIgnoreCase))
        && (match.MatchesPattern is null || TextPattern.IsMatch(match.MatchesPattern, value));

    private static bool EntryPasses(MgcbSettingMatch match, MgcbEntry entry) =>
        (match.Importer is null || (entry.Importer is not null && Wildcard.IsMatch(match.Importer, entry.Importer)))
        && (match.Processor is null || (entry.Processor is not null && Wildcard.IsMatch(match.Processor, entry.Processor)))
        && (!match.NonStandardPair || ImporterMap.Default.IsCustom(entry.Importer, entry.Processor));

    private static string EntryNames(MgcbEntry entry) =>
        entry.Importer is null && entry.Processor is null
            ? string.Empty
            : $" ({entry.Importer ?? "-"}/{entry.Processor ?? "-"})";
}
