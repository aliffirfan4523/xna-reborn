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

    private readonly IReadOnlyList<Rule> _rules;
    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates an engine for a catalog.</summary>
    /// <param name="catalog">The rule catalog.</param>
    /// <param name="fileSystem">Read-only file system used by file pattern rules.</param>
    public RuleEngine(RuleCatalog catalog, IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _rules = catalog.Rules;
        _fileSystem = fileSystem;
        SourceRules = _rules.Where(r => r.Match is NamespaceMatch or TypeMatch or MemberMatch or PreprocessorSymbolMatch).ToList();
    }

    /// <summary>Rules whose match is a namespace, type, member, or preprocessor symbol. The source scanner uses these.</summary>
    public IReadOnlyList<Rule> SourceRules { get; }

    /// <summary>Evaluates the project-level rules for one project. Gives at most one finding per rule, in catalog order.</summary>
    /// <param name="project">The project as read by <see cref="ProjectReader"/>.</param>
    /// <param name="projectDirectory">Absolute directory of the project, the base for file patterns.</param>
    /// <param name="projectRelativePath">Project path relative to the analysis root, stored in each finding.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The findings, one per matching rule.</returns>
    public IReadOnlyList<Finding> EvaluateProject(ProjectReadResult project, string projectDirectory, string projectRelativePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        var findings = new List<Finding>();
        foreach (var rule in _rules)
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
        ProjectLinksMatch m => ProjectLinksMatcher.Match(m, project),
        FileContentMatch m => FileContentMatcher.Match(m, _fileSystem, projectDirectory, token),
        _ => [],
    };

    private static string Snippet(IReadOnlyList<string> values)
    {
        var text = string.Join(", ", values.Take(_maxSnippetValues));
        return values.Count > _maxSnippetValues ? text + " +" + (values.Count - _maxSnippetValues) + " more" : text;
    }
}
