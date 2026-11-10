namespace XnaReborn.Migrator.Planning;

/// <summary>One step of the migration plan, shown as a checklist item the user can exclude.</summary>
/// <param name="Id">Stable id of the item within one plan.</param>
/// <param name="RuleId">Analyzer rule the step comes from, such as "XNA022".</param>
/// <param name="Description">Plain-language description of the step.</param>
/// <param name="AffectedFiles">Relative paths of the files the step changes.</param>
public sealed record PlanItem(
    string Id,
    string RuleId,
    string Description,
    IReadOnlyList<string> AffectedFiles);
