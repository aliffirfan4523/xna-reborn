using XnaReborn.App.Tests.Fakes;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.Tests.Diff;

/// <summary>Small file changes of every kind, with and without <c>// XNAREBORN:</c> markers.</summary>
internal static class DiffTestData
{
    public const string Game1Old = "using A;\nclass Game1 {\n    int x;\n}\n";

    public const string Game1New = "using B;\nclass Game1 {\n    // XNAREBORN: XNA022: fix by hand\n    int x;\n}\n";

    public const string ProgramOld = "class Program {\n    void Main() { }\n}\n";

    // Two markers, written with CRLF to check the numbering ignores the CR.
    public const string ProgramNew = "class Program {\r\n    // XNAREBORN: XNA039: first\r\n    void Main() { }\r\n    // XNAREBORN: XNA040: second\r\n}\r\n";

    public static List<FileChange> Changes() =>
    [
        new("Game1.cs", ChangeKind.Modified, Game1Old, Game1New, "Rewrote usings"),
        new("Program.cs", ChangeKind.Modified, ProgramOld, ProgramNew, "Entry point"),
        new("Game.csproj", ChangeKind.Created, null, "<Project />\n", "SDK project"),
        new("Shim.cs", ChangeKind.Created, null, "class Shim { }\n", "Compatibility shim"),
        new("Notes.txt", ChangeKind.ManualWork, null, null, "Needs a person", "C:/src/Notes.txt"),
        new("Content/logo.png", ChangeKind.Copied, null, null, null, "C:/src/Content/logo.png"),
        new("Old.txt", ChangeKind.Skipped, null, null, "Not needed"),
    ];

    public static MigrationResult Result(IReadOnlyList<FileChange> changes, bool completed) => new(
        new MigrationPlan([], []),
        changes,
        new Manifest(1, "test", "test-catalog", completed, [], null),
        completed);

    /// <summary>Puts a dry run (and nothing else) into the session and opens the Diff page.</summary>
    public static async Task<ViewModels.DiffViewModel> OpenDryRunAsync(TestServices t, IReadOnlyList<FileChange>? changes = null)
    {
        t.Session.Report = SampleData.Report();
        t.Session.DryRunResult = Result(changes ?? Changes(), completed: false);
        await t.OpenPageAsync(ViewModels.WorkflowStep.Diff);
        return t.Get<ViewModels.DiffViewModel>();
    }

    /// <summary>Puts a finished real migration into the session and opens the Diff page.</summary>
    public static async Task<ViewModels.DiffViewModel> OpenMigrationAsync(TestServices t, IReadOnlyList<FileChange>? changes = null, bool completed = true)
    {
        t.Session.Report = SampleData.Report();
        t.Session.MigrationResult = Result(changes ?? Changes(), completed);
        await t.OpenPageAsync(ViewModels.WorkflowStep.Diff);
        return t.Get<ViewModels.DiffViewModel>();
    }
}
