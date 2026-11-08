using XnaReborn.Core.Models;

namespace XnaReborn.Tests.Reporting;

/// <summary>Hand-built reports used by the report writer tests.</summary>
public static class ReportSamples
{
    private static readonly string[] _refs = ["Microsoft.Xna.Framework", "Microsoft.Xna.Framework.Game"];
    private static readonly string[] _compile = ["Game1.cs", "Program.cs"];
    private static readonly string[] _defines = ["WINDOWS", "TRACE"];

    /// <summary>A report with several severities and classifications, odd characters, and diagnostics.</summary>
    /// <returns>The rich report.</returns>
    public static AnalysisReport Rich()
    {
        var findings = new List<Finding>
        {
            new("XNA901", "Xbox LIVE services", Severity.Blocker, Classification.Unsupported, "Game/Game.csproj", new SourceLocation("Game1.cs", 12, 9), "Guide.ShowSignIn(1, false);"),
            new("XNA902", "Storage device", Severity.Error, Classification.Manual, "Game/Game.csproj", new SourceLocation("Save/Store.cs", 40, 17), "var s = \"`quoted`\"; // café"),
            new("XNA903", "Legacy project import", Severity.Warning, Classification.AutoFixable, "Game/Game.csproj", null, null),
            new("XNA910", "Sprite font asset", Severity.Info, Classification.AutoFixable, "Lib|Core/Lib.csproj", new SourceLocation("Content/Font.spritefont", 1, 1), null),
        };
        return new AnalysisReport(
            1,
            "sample",
            [
                new ProjectInfo("Game/Game.csproj", "Game", true, XnaVersion.V31, XnaPlatform.Windows, "HiDef", "v3.5", _refs, _compile, _defines, [], []),
                new ProjectInfo("Lib|Core/Lib.csproj", "Lib", true, XnaVersion.Unknown, XnaPlatform.Xbox360, null, null, _refs, _compile, _defines, [], []),
            ],
            findings,
            [
                new TriggeredRule("XNA901", "Xbox LIVE services", "There is no Xbox LIVE in MonoGame.", "Remove the calls.", null),
                new TriggeredRule("XNA903", "Legacy project import", "The project imports XNA targets.", "Switch to an SDK-style project.", "migrator-task-2"),
            ],
            new Dictionary<Severity, int> { [Severity.Info] = 1, [Severity.Warning] = 1, [Severity.Error] = 1, [Severity.Blocker] = 1 },
            new Dictionary<Classification, int> { [Classification.AutoFixable] = 2, [Classification.Manual] = 1, [Classification.Unsupported] = 1 },
            62,
            80,
            [
                new Diagnostic("ANA004", "Source file has syntax errors.", "Game/Broken.cs"),
                new Diagnostic("ANA006", "Solution lists a missing project.", null),
            ],
            null);
    }

    /// <summary>A report for a run that found no XNA project.</summary>
    /// <returns>The empty report.</returns>
    public static AnalysisReport NoProject() => new(
        1,
        "sample",
        [],
        [],
        [],
        new Dictionary<Severity, int> { [Severity.Info] = 0, [Severity.Warning] = 0, [Severity.Error] = 0, [Severity.Blocker] = 0 },
        new Dictionary<Classification, int> { [Classification.AutoFixable] = 0, [Classification.Manual] = 0, [Classification.Unsupported] = 0 },
        100,
        100,
        [],
        "No .csproj files found.");

    /// <summary>A report with one project and zero findings.</summary>
    /// <returns>The clean report.</returns>
    public static AnalysisReport Clean() => new(
        1,
        "sample",
        [new ProjectInfo("Game.csproj", "Game", true, XnaVersion.V40, XnaPlatform.Windows, "Reach", "v4.0", _refs, _compile, _defines, [], [])],
        [],
        [],
        new Dictionary<Severity, int> { [Severity.Info] = 0, [Severity.Warning] = 0, [Severity.Error] = 0, [Severity.Blocker] = 0 },
        new Dictionary<Classification, int> { [Classification.AutoFixable] = 0, [Classification.Manual] = 0, [Classification.Unsupported] = 0 },
        100,
        100,
        [],
        null);
}
