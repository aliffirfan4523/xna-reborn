using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules.Matchers;

/// <summary>Matches <see cref="ProjectPropertyMatch"/> against the project's properties.</summary>
internal static class ProjectPropertyMatcher
{
    /// <summary>Returns <c>Name=Value</c> when the property matches, or an empty list.</summary>
    /// <param name="match">The rule match.</param>
    /// <param name="project">The project.</param>
    public static IReadOnlyList<string> Match(ProjectPropertyMatch match, ProjectReadResult project)
    {
        string? value = null;
        var found = false;
        foreach (var pair in project.Properties)
        {
            if (string.Equals(pair.Key, match.Name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return [];
        }

        var text = value ?? string.Empty;
        if (match.EqualsValue is not null)
        {
            if (!string.Equals(text, match.EqualsValue, StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }
        }
        else if (match.MatchesPattern is not null && !IsRegexMatch(match.MatchesPattern, text))
        {
            return [];
        }

        return [match.Name + "=" + text];
    }

    private static bool IsRegexMatch(string pattern, string text) => TextPattern.IsMatch(pattern, text);
}
