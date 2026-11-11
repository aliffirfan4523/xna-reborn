namespace XnaReborn.Migrator.Projects;

/// <summary>Rule IDs, task prefixes and texts of the SharpDX Toolkit migration that more than one class needs.</summary>
internal static class ToolkitRules
{
    /// <summary>The rule for Windows Store and Windows Phone project types.</summary>
    internal const string StoreAppRuleId = "SDX004";

    /// <summary>The start of the <c>migratorTask</c> of the content rules.</summary>
    internal const string ContentTaskPrefix = "sharpdx-toolkit:9";

    /// <summary>Why a Windows Store or Windows Phone project is not migrated.</summary>
    internal const string StoreAppReason = "This is a Windows Store or Windows Phone project. MonoGame desktop cannot host it, so it was not migrated and no copy of it was made.";
}
