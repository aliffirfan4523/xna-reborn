using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>The findings of one classification that pass the current filters.</summary>
/// <param name="Classification">The classification of every item.</param>
/// <param name="Items">The findings, in report order.</param>
public sealed record ReportGroup(Classification Classification, IReadOnlyList<ReportFindingItem> Items)
{
    /// <summary>Name of the classification as shown to the user.</summary>
    public string Label => LabelOf(Classification);

    /// <summary>One line telling who fixes these findings.</summary>
    public string Description => Classification switch
    {
        Classification.AutoFixable => "The migrator fixes these for you.",
        Classification.Manual => "You need to fix these by hand.",
        _ => "No modern equivalent exists.",
    };

    /// <summary>Label with the number of items, such as "Manual (3)".</summary>
    public string Header => $"{Label} ({Items.Count})";

    /// <summary>Name of a classification as shown to the user.</summary>
    /// <param name="classification">The classification.</param>
    public static string LabelOf(Classification classification) => classification switch
    {
        Classification.AutoFixable => "Auto-fixable",
        Classification.Manual => "Manual",
        _ => "Unsupported",
    };
}

/// <summary>One line of the findings list. The list is flat so it can be virtualized; the first row of a group carries the group header.</summary>
/// <param name="Item">The finding on this row.</param>
/// <param name="Group">The group the row opens, or null when the row continues the group above.</param>
public sealed record ReportRow(ReportFindingItem Item, ReportGroup? Group)
{
    /// <summary>True when the row is the first of its group.</summary>
    public bool StartsGroup => Group is not null;

    /// <summary>Header of the group the row opens, or an empty string.</summary>
    public string GroupHeader => Group?.Header ?? string.Empty;

    /// <summary>Description of the group the row opens, or an empty string.</summary>
    public string GroupDescription => Group?.Description ?? string.Empty;
}
