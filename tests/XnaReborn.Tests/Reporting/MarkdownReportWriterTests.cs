using VerifyXunit;
using XnaReborn.Analyzer.Reporting;

namespace XnaReborn.Tests.Reporting;

/// <summary>Output checks and snapshots for <see cref="MarkdownReportWriter"/>.</summary>
public class MarkdownReportWriterTests
{
    /// <summary>Output uses LF only and ends with exactly one newline.</summary>
    [Fact]
    public void Output_IsLfWithSingleTrailingNewline()
    {
        foreach (var report in new[] { ReportSamples.Rich(), ReportSamples.NoProject(), ReportSamples.Clean() })
        {
            var text = MarkdownReportWriter.Write(report);
            Assert.DoesNotContain('\r', text);
            Assert.EndsWith("\n", text, StringComparison.Ordinal);
            Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        }
    }

    /// <summary>Sections appear in the documented order.</summary>
    [Fact]
    public void Rich_SectionsAreInOrder()
    {
        var text = MarkdownReportWriter.Write(ReportSamples.Rich());
        var headings = new[]
        {
            "# XNA Reborn compatibility report", "## Summary", "## Projects", "## Findings",
            "### AutoFixable", "### Manual", "### Unsupported", "## Rules", "## Diagnostics",
        };
        var last = -1;
        foreach (var heading in headings)
        {
            var index = text.IndexOf(heading + "\n", StringComparison.Ordinal);
            Assert.True(index > last, heading);
            last = index;
        }

        Assert.DoesNotContain("## No XNA project found", text, StringComparison.Ordinal);
    }

    /// <summary>Summary values, version text, null cells, and pipe escaping.</summary>
    [Fact]
    public void Rich_FormatsSummaryAndProjects()
    {
        var text = MarkdownReportWriter.Write(ReportSamples.Rich());
        Assert.Contains("| Compatibility score | 62 / 100 |", text, StringComparison.Ordinal);
        Assert.Contains("| Projected score after automatic fixes | 80 / 100 |", text, StringComparison.Ordinal);
        Assert.Contains("| Findings | 4 |", text, StringComparison.Ordinal);
        Assert.Contains("Severity: Blocker 1, Error 1, Warning 1, Info 1", text, StringComparison.Ordinal);
        Assert.Contains("Classification: AutoFixable 2, Manual 1, Unsupported 1", text, StringComparison.Ordinal);
        Assert.Contains("| Game/Game.csproj | 3.1 | Windows | HiDef | v3.5 |", text, StringComparison.Ordinal);
        Assert.Contains("| Lib\\|Core/Lib.csproj | Unknown | Xbox360 | - | - |", text, StringComparison.Ordinal);
    }

    /// <summary>Optional lines are omitted and backticks in snippets are replaced.</summary>
    [Fact]
    public void Rich_HandlesOptionalFindingParts()
    {
        var text = MarkdownReportWriter.Write(ReportSamples.Rich());
        Assert.Contains("  - Code: `var s = \"'quoted'\"; // café`", text, StringComparison.Ordinal);
        Assert.Contains("  - Location: `Game1.cs:12:9`", text, StringComparison.Ordinal);
        var legacy = text.Split("- **[", StringSplitOptions.None).First(s => s.StartsWith("Warning] XNA903", StringComparison.Ordinal));
        Assert.DoesNotContain("Location:", legacy, StringComparison.Ordinal);
        Assert.DoesNotContain("Code:", legacy, StringComparison.Ordinal);
        Assert.Contains("- ANA004: Source file has syntax errors. (`Game/Broken.cs`)", text, StringComparison.Ordinal);
        Assert.Contains("- ANA006: Solution lists a missing project.\n", text, StringComparison.Ordinal);
        Assert.Contains("Migrator task: migrator-task-2", text, StringComparison.Ordinal);
    }

    /// <summary>A run without projects shows the reason and no project table or rules.</summary>
    [Fact]
    public void NoProject_ShowsReason()
    {
        var text = MarkdownReportWriter.Write(ReportSamples.NoProject());
        Assert.Contains("## No XNA project found\n\nNo .csproj files found.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Projects", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Rules", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Diagnostics", text, StringComparison.Ordinal);
        Assert.Contains("No findings.", text, StringComparison.Ordinal);
    }

    /// <summary>A clean report says there are no findings and omits rules and diagnostics.</summary>
    [Fact]
    public void Clean_HasNoFindingGroups()
    {
        var text = MarkdownReportWriter.Write(ReportSamples.Clean());
        Assert.EndsWith("## Findings\n\nNo findings.\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("### ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("## Rules", text, StringComparison.Ordinal);
    }

    /// <summary>Snapshot of the rich report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_Rich() => Verifier.Verify(MarkdownReportWriter.Write(ReportSamples.Rich()), "md");

    /// <summary>Snapshot of the no-project report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_NoProject() => Verifier.Verify(MarkdownReportWriter.Write(ReportSamples.NoProject()), "md");

    /// <summary>Snapshot of the clean report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_Clean() => Verifier.Verify(MarkdownReportWriter.Write(ReportSamples.Clean()), "md");
}
