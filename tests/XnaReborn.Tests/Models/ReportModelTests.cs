using System.Text.Json;
using XnaReborn.Core.Models;

namespace XnaReborn.Tests.Models;

/// <summary>Equality and JSON behavior of the analysis report models.</summary>
public class ReportModelTests
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static AnalysisReport BuildReport()
    {
        var projects = new List<ProjectInfo>
        {
            new(
                Path: "games/Space/Space.csproj",
                AssemblyName: "Space",
                IsXna: true,
                XnaVersion: XnaVersion.V40,
                Platform: XnaPlatform.Windows,
                Profile: "Reach",
                TargetFramework: "v4.0",
                References: new List<string> { "Microsoft.Xna.Framework", "Microsoft.Xna.Framework.Game" },
                CompileFiles: new List<string> { "Game1.cs", "Ship.cs" },
                DefineConstants: new List<string> { "WINDOWS", "DEBUG" },
                ContentProjects: new List<string> { "games/Space/Content/Content.contentproj" },
                ContentItems: new List<ContentItem>
                {
                    new(
                        SourcePath: "ship.png",
                        NameOverride: "Ship",
                        Importer: "TextureImporter",
                        Processor: "TextureProcessor",
                        ProcessorParams: new Dictionary<string, string> { ["ColorKeyEnabled"] = "false" }),
                }),
        };

        var findings = new List<Finding>
        {
            new(
                RuleId: "XNA001",
                Title: "GamerServices",
                Severity: Severity.Blocker,
                Classification: Classification.Unsupported,
                Project: "games/Space/Space.csproj",
                Location: new SourceLocation("Game1.cs", 12, 5),
                Snippet: "Guide.ShowMessageBox(...);"),
            new(
                RuleId: "XNA002",
                Title: "Project reference",
                Severity: Severity.Info,
                Classification: Classification.AutoFixable,
                Project: "games/Space/Space.csproj",
                Location: null,
                Snippet: null),
        };

        var triggered = new List<TriggeredRule>
        {
            new("XNA001", "GamerServices", "No modern equivalent.", "Remove the dependency.", null),
            new("XNA002", "Project reference", "Can be rewritten.", "Use the migrator.", "M-12"),
        };

        var diagnostics = new List<Diagnostic> { new("ANA003", "Missing file.", "Ship.cs") };

        return new AnalysisReport(
            SchemaVersion: 1,
            CatalogName: "xna-to-monogame",
            Projects: projects,
            Findings: findings,
            TriggeredRules: triggered,
            CountsBySeverity: new Dictionary<Severity, int> { [Severity.Blocker] = 1, [Severity.Info] = 1 },
            CountsByClassification: new Dictionary<Classification, int>
            {
                [Classification.Unsupported] = 1,
                [Classification.AutoFixable] = 1,
            },
            Score: 62,
            ProjectedScore: 80,
            Diagnostics: diagnostics,
            NoProjectReason: null);
    }

    [Fact]
    public void Records_with_equal_values_are_equal()
    {
        var sharedProjects = new List<ProjectInfo>();
        var sharedFindings = new List<Finding>();
        var sharedTriggered = new List<TriggeredRule>();
        var sharedDiagnostics = new List<Diagnostic>();
        var counts = new Dictionary<Severity, int> { [Severity.Error] = 2 };
        var classCounts = new Dictionary<Classification, int> { [Classification.Manual] = 2 };

        var a = new AnalysisReport(1, "c", sharedProjects, sharedFindings, sharedTriggered, counts, classCounts, 90, 95, sharedDiagnostics, null);
        var b = new AnalysisReport(1, "c", sharedProjects, sharedFindings, sharedTriggered, counts, classCounts, 90, 95, sharedDiagnostics, null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var loc1 = new SourceLocation("A.cs", 1, 1);
        Assert.Equal(loc1, new SourceLocation("A.cs", 1, 1));
        Assert.Equal(new Diagnostic("ANA001", "x", null), new Diagnostic("ANA001", "x", null));
        Assert.Equal(new AnalysisProgress("Scan", 1, 2, null), new AnalysisProgress("Scan", 1, 2, null));
        Assert.NotEqual(new AnalysisProgress("Scan", 1, 2, null), new AnalysisProgress("Scan", 2, 2, null));
    }

    [Fact]
    public void Fully_populated_report_round_trips_through_json()
    {
        var original = BuildReport();

        var json = JsonSerializer.Serialize(original, _options);
        var restored = JsonSerializer.Deserialize<AnalysisReport>(json, _options);

        Assert.NotNull(restored);
        Assert.Equal(original.Score, restored.Score);
        Assert.Equal(original.ProjectedScore, restored.ProjectedScore);
        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.CatalogName, restored.CatalogName);
        Assert.Equal(original.CountsBySeverity, restored.CountsBySeverity);
        Assert.Equal(original.CountsByClassification, restored.CountsByClassification);
        Assert.Equal(original.Findings, restored.Findings);
        Assert.Equal(original.TriggeredRules, restored.TriggeredRules);
        Assert.Equal(original.Diagnostics, restored.Diagnostics);
        Assert.Equal(original.Projects.Count, restored.Projects.Count);

        var project = Assert.Single(restored.Projects);
        var expected = Assert.Single(original.Projects);
        Assert.Equal(expected.Path, project.Path);
        Assert.Equal(expected.XnaVersion, project.XnaVersion);
        Assert.Equal(expected.Platform, project.Platform);
        Assert.Equal(expected.References, project.References);
        Assert.Equal(expected.CompileFiles, project.CompileFiles);
        Assert.Equal(expected.DefineConstants, project.DefineConstants);
        Assert.Equal(expected.ContentProjects, project.ContentProjects);
        var item = Assert.Single(project.ContentItems);
        Assert.Equal("false", item.ProcessorParams["ColorKeyEnabled"]);
        Assert.Equal("Ship", item.NameOverride);
    }

    [Fact]
    public void Enums_are_written_as_strings_and_version_as_dotted_text()
    {
        var json = JsonSerializer.Serialize(BuildReport(), _options);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var finding = root.GetProperty("findings")[0];
        Assert.Equal("Blocker", finding.GetProperty("severity").GetString());
        Assert.Equal("Unsupported", finding.GetProperty("classification").GetString());

        var version = root.GetProperty("projects")[0].GetProperty("xnaVersion").GetString();
        Assert.Equal("4.0", version);

        var platform = root.GetProperty("projects")[0].GetProperty("platform").GetString();
        Assert.Equal("Windows", platform);

        var severityKeys = root.GetProperty("countsBySeverity").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("Blocker", severityKeys);
        Assert.Contains("Info", severityKeys);

        var classKeys = root.GetProperty("countsByClassification").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("Unsupported", classKeys);
        Assert.Contains("AutoFixable", classKeys);
    }

    [Fact]
    public void Finding_with_null_location_serializes_and_round_trips()
    {
        var finding = new Finding(
            RuleId: "XNA002",
            Title: "Project reference",
            Severity: Severity.Warning,
            Classification: Classification.Manual,
            Project: "a/A.csproj",
            Location: null,
            Snippet: null);

        var json = JsonSerializer.Serialize(finding, _options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("location").ValueKind);

        var restored = JsonSerializer.Deserialize<Finding>(json, _options);

        Assert.Equal(finding, restored);
        Assert.Null(restored!.Location);
    }
}
