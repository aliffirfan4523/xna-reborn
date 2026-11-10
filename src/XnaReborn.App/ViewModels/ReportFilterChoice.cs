namespace XnaReborn.App.ViewModels;

/// <summary>One entry of a filter drop-down on the report page.</summary>
/// <param name="Label">Text shown in the drop-down.</param>
public abstract record ReportFilterChoice(string Label);

/// <summary>A filter drop-down entry that stands for one enum value, or for "all" when <see cref="Value"/> is null.</summary>
/// <typeparam name="T">The enum being filtered.</typeparam>
/// <param name="Label">Text shown in the drop-down.</param>
/// <param name="Value">The value to keep, or null to keep everything.</param>
public sealed record ReportFilterChoice<T>(string Label, T? Value) : ReportFilterChoice(Label)
    where T : struct, Enum;
