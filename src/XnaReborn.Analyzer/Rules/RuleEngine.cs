using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules.Matchers;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Analyzer.Rules;

/// <summary>Evaluates the project-level rules of a catalog (reference, project property, project item, file pattern, content item, sprite font family).</summary>
public sealed class RuleEngine
{
    private const int _maxSnippetValues = 3;

    private readonly IReadOnlyList<RuleCatalog> _catalogs;
    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates an engine for one catalog.</summary>
    /// <param name="catalog">The rule catalog.</param>
    /// <param name="fileSystem">Read-only file system used by file pattern rules.</param>
    public RuleEngine(RuleCatalog catalog, IReadOnlyFileSystem fileSystem)
        : this(catalog is null ? throw new ArgumentNullException(nameof(catalog)) : [catalog], fileSystem)
    {
    }

    /// <summary>Creates an engine for several catalogs. Each catalog runs only against projects of the family it applies to.</summary>
    /// <param name="catalogs">The rule catalogs, in run order.</param>
    /// <param name="fileSystem">Read-only file system used by file pattern rules.</param>
    public RuleEngine(IReadOnlyList<RuleCatalog> catalogs, IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _catalogs = catalogs;
        _fileSystem = fileSystem;
        SourceRules = catalogs.SelectMany(c => c.Rules).Where(IsSourceRule).ToList();
    }

    /// <summary>Source rules (namespace, type, or member match) of every catalog, regardless of family.</summary>
    public IReadOnlyList<Rule> SourceRules { get; }

    /// <summary>Source rules of the catalogs that apply to a project family. The source scanner uses these.</summary>
    /// <param name="family">The project family.</param>
    public IReadOnlyList<Rule> SourceRulesFor(ProjectFamily family) =>
        _catalogs.Where(c => c.AppliesToFamily(family)).SelectMany(c => c.Rules).Where(IsSourceRule).ToList();

    private static bool IsSourceRule(Rule rule) => rule.Match is NamespaceMatch or TypeMatch or MemberMatch;

    /// <summary>Evaluates the project-level rules of the catalogs that apply to the project's family. Gives at most one finding per rule, in catalog order.</summary>
    /// <param name="project">The project as read by <see cref="ProjectReader"/>.</param>
    /// <param name="projectDirectory">Absolute directory of the project, the base for file patterns.</param>
    /// <param name="projectRelativePath">Project path relative to the analysis root, stored in each finding.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The findings, one per matching rule.</returns>
    public IReadOnlyList<Finding> EvaluateProject(ProjectReadResult project, string projectDirectory, string projectRelativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var findings = new List<Finding>();
        var rules = _catalogs.Where(c => c.AppliesToFamily(project.Info.Family)).SelectMany(c => c.Rules);
        foreach (var rule in rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rule.XnaVersions is { } versions && !versions.Contains(project.Info.XnaVersion))
            {
                continue;
            }

            var matched = Match(rule.Match, project, projectDirectory, cancellationToken);
            if (matched.Count == 0)
            {
                continue;
            }

            findings.Add(new Finding(rule.Id, rule.Title, rule.Severity, rule.Classification, projectRelativePath, null, Snippet(matched)));
        }

        return findings;
    }

    private IReadOnlyList<string> Match(RuleMatch match, ProjectReadResult project, string projectDirectory, CancellationToken token) => match switch
    {
        ReferenceMatch m => ReferenceMatcher.Match(m, project),
        ProjectPropertyMatch m => ProjectPropertyMatcher.Match(m, project),
        ProjectItemMatch m => ProjectItemMatcher.Match(m, project),
        FilePatternMatch m => FilePatternMatcher.Match(m, _fileSystem, projectDirectory, token),
        ContentItemMatch m => ContentItemMatcher.Match(m, project),
        SpriteFontFamilyMatch m => SpriteFontMatcher.Match(m, project, _fileSystem, projectDirectory, token),
        _ => [],
    };

    private static string Snippet(IReadOnlyList<string> values)
    {
        var text = string.Join(", ", values.Take(_maxSnippetValues));
        return values.Count > _maxSnippetValues ? text + " +" + (values.Count - _maxSnippetValues) + " more" : text;
    }
}
