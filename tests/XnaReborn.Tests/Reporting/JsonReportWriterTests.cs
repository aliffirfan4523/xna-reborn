using System.Text.Json;
using VerifyXunit;
using XnaReborn.Analyzer.Reporting;

namespace XnaReborn.Tests.Reporting;

/// <summary>Output checks and snapshots for <see cref="JsonReportWriter"/>.</summary>
public class JsonReportWriterTests
{
    /// <summary>Output uses LF only and ends with exactly one newline.</summary>
    [Fact]
    public void Output_IsLfWithSingleTrailingNewline()
    {
        var text = JsonReportWriter.Write(ReportSamples.Rich());
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
    }

    /// <summary>Names are camelCase, schemaVersion comes first, enums are strings, versions read "3.1".</summary>
    [Fact]
    public void Output_UsesCamelCaseStringsAndVersionText()
    {
        var text = JsonReportWriter.Write(ReportSamples.Rich());
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal("schemaVersion", root.EnumerateObject().First().Name);
        Assert.Equal("Blocker", root.GetProperty("findings")[0].GetProperty("severity").GetString());
        Assert.Equal("3.1", root.GetProperty("projects")[0].GetProperty("xnaVersion").GetString());
        Assert.Equal("Unknown", root.GetProperty("projects")[1].GetProperty("xnaVersion").GetString());
        Assert.Equal(1, root.GetProperty("countsBySeverity").GetProperty("Blocker").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("noProjectReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("findings")[2].GetProperty("location").ValueKind);
        Assert.Contains("café", text, StringComparison.Ordinal);
        Assert.Contains("`quoted`", text, StringComparison.Ordinal);
    }

    /// <summary>Counts keep the order supplied by the report.</summary>
    [Fact]
    public void Output_KeepsDictionaryOrder()
    {
        var text = JsonReportWriter.Write(ReportSamples.NoProject());
        Assert.True(text.IndexOf("\"Info\"", StringComparison.Ordinal) < text.IndexOf("\"Blocker\"", StringComparison.Ordinal));
        Assert.Contains("\"noProjectReason\": \"No .csproj files found.\"", text, StringComparison.Ordinal);
    }

    /// <summary>The same report gives the same text.</summary>
    [Fact]
    public void Output_IsDeterministic() =>
        Assert.Equal(JsonReportWriter.Write(ReportSamples.Rich()), JsonReportWriter.Write(ReportSamples.Rich()));

    /// <summary>Snapshot of the rich report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_Rich() => Verifier.Verify(JsonReportWriter.Write(ReportSamples.Rich()), "json");

    /// <summary>Snapshot of the no-project report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_NoProject() => Verifier.Verify(JsonReportWriter.Write(ReportSamples.NoProject()), "json");

    /// <summary>Snapshot of the clean report.</summary>
    /// <returns>A task for the snapshot check.</returns>
    [Fact]
    public Task Snapshot_Clean() => Verifier.Verify(JsonReportWriter.Write(ReportSamples.Clean()), "json");
}
