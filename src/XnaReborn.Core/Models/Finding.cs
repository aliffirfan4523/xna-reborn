namespace XnaReborn.Core.Models;

/// <summary>One place where a rule matched a project.</summary>
/// <param name="RuleId">Id of the rule that matched, such as "XNA001".</param>
/// <param name="Title">Short title of the rule.</param>
/// <param name="Severity">How serious the match is.</param>
/// <param name="Classification">Who can fix the match.</param>
/// <param name="Project">Relative path of the .csproj the match belongs to.</param>
/// <param name="Location">Source position, or null for project-level matches.</param>
/// <param name="Snippet">Trimmed source line for context, or null.</param>
public sealed record Finding(
    string RuleId,
    string Title,
    Severity Severity,
    Classification Classification,
    string Project,
    SourceLocation? Location,
    string? Snippet);
