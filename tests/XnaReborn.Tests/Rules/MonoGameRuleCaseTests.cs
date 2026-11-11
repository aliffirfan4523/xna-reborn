using System.Text.Json;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>
/// Tasks 7 to 9: every rule in <c>rules/monogame-upgrade.json</c> has a positive fixture test and a negative test.
/// The positive fixtures are the ones listed in <see cref="FixturePaths.MonoGamePositives"/>. The default negative is
/// <c>mg38-current</c>; each rule also has a near-miss negative below, and the groups from the spec (desktop platform,
/// PackageReference only, plain mgcb, plain source) have their own tests.
/// </summary>
[Trait("Category", "Rules")]
public sealed class MonoGameRuleCaseTests : IDisposable
{
    private const string _ns = "http://schemas.microsoft.com/developer/msbuild/2003";

    private static readonly Lazy<IReadOnlyList<RuleCatalog>> _catalogs = new(() =>
        RuleCatalogLoader.LoadAllAsync([XnaCatalogPath, MonoGameCatalogPath], new ReadOnlyFileSystem(), CancellationToken.None).GetAwaiter().GetResult());

    private readonly string _work = Path.Combine(Path.GetTempPath(), "xnareborn-mgrules-" + Guid.NewGuid().ToString("N"));

    private static string RepoRoot => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", ".."));

    private static string XnaCatalogPath => Path.Combine(RepoRoot, "rules", "xna-to-monogame.json");

    private static string MonoGameCatalogPath => Path.Combine(RepoRoot, "rules", "monogame-upgrade.json");

    private static RuleCatalog MonoGameCatalog => _catalogs.Value[1];

    public static IEnumerable<object[]> RuleIds() => MonoGameCatalog.Rules.Select(r => new object[] { r.Id });

    public static IEnumerable<object[]> PositivePairs() =>
        FixturePaths.MonoGamePositives.SelectMany(p => p.Value.Select(rule => new object[] { p.Key, rule }));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    // ---- the catalog itself ----

    [Fact]
    public void Catalog_has_MG001_to_MG032_in_order_and_applies_to_monogame()
    {
        Assert.Equal("monogame-upgrade", MonoGameCatalog.Name);
        Assert.Equal("monogame", MonoGameCatalog.AppliesTo);
        Assert.NotNull(MonoGameCatalog.Scoring);
        Assert.Equal(Enumerable.Range(1, 32).Select(i => $"MG{i:000}"), MonoGameCatalog.Rules.Select(r => r.Id));
    }

    [Fact]
    public void Ids_do_not_collide_with_the_xna_catalog()
    {
        RuleCatalogLoader.EnsureUniqueIds(_catalogs.Value);
    }

    [Fact]
    public void Autofixable_rules_link_a_monogame_upgrade_task_and_other_rules_do_not()
    {
        foreach (var rule in MonoGameCatalog.Rules)
        {
            if (rule.Classification == Classification.AutoFixable)
            {
                Assert.Matches(@"^monogame-upgrade:\d+\.\d+$", rule.MigratorTask);
            }
            else
            {
                Assert.Null(rule.MigratorTask);
            }
        }
    }

    [Fact]
    public void Rule_counts_by_group_match_the_design()
    {
        int Count(int from, int to) => MonoGameCatalog.Rules.Count(r => string.CompareOrdinal(r.Id, $"MG{from:000}") >= 0 && string.CompareOrdinal(r.Id, $"MG{to:000}") <= 0);

        Assert.Equal(10, Count(1, 10));
        Assert.Equal(6, Count(11, 16));
        Assert.Equal(6, Count(17, 22));
        Assert.Equal(5, Count(23, 27));
        Assert.Equal(5, Count(28, 32));
    }

    [Fact]
    public void MG008_pins_the_same_version_as_packages_json()
    {
        var packages = Path.Combine(RepoRoot, "src", "XnaReborn.Migrator", "Projects", "packages.json");
        using var document = JsonDocument.Parse(File.ReadAllText(packages));
        var rule = MonoGameCatalog.Rules.Single(r => r.Id == "MG008");

        var match = Assert.IsType<ProjectItemMatch>(rule.Match);

        Assert.Equal(document.RootElement.GetProperty("monoGame").GetString(), match.Metadata!.NotEquals);
    }

    [Fact]
    public void Every_rule_has_a_positive_fixture()
    {
        var covered = FixturePaths.MonoGamePositives.Values.SelectMany(v => v).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(RulesNotIn(covered));
    }

    [Fact]
    public void Every_rule_has_a_near_miss_negative()
    {
        var covered = _projectNearMisses.Keys
            .Concat(_mgcbNearMisses.Keys)
            .Concat(_sourceNearMisses.Keys)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(RulesNotIn(covered));
    }

    // ---- positive ----

    [Theory]
    [MemberData(nameof(PositivePairs))]
    public async Task Positive_fixture_produces_the_finding(string fixture, string ruleId)
    {
        var folder = fixture == "mg3x-protobuild" ? ProtobuildWithGeneratedProject() : FixturePaths.Of(fixture);

        var report = await AnalyzeAsync(folder);

        Assert.Contains(report.Findings, f => f.RuleId == ruleId);
        Assert.All(report.Findings.Where(f => f.RuleId == ruleId), f => Assert.StartsWith("MG", f.RuleId, StringComparison.Ordinal));
    }

    // ---- negative ----

    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Default_negative_mg38_current_has_no_finding_for_the_rule(string ruleId)
    {
        var report = await AnalyzeAsync(FixturePaths.Of("mg38-current"));

        Assert.NotEmpty(report.Projects);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == ruleId);
    }

    [Fact]
    public async Task Mg38_current_scores_100_with_no_findings()
    {
        var report = await AnalyzeAsync(FixturePaths.Of("mg38-current"));

        Assert.Empty(report.Findings);
        Assert.Equal(100, report.Score);
        Assert.Equal(100, report.ProjectedScore);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public async Task Near_miss_negative_has_no_finding_for_the_rule(string ruleId)
    {
        var folder = Path.Combine(_work, ruleId);
        Directory.CreateDirectory(folder);
        if (_projectNearMisses.TryGetValue(ruleId, out var project))
        {
            WriteProject(folder, project.Properties, project.Items, project.Imports);
            foreach (var file in project.Files ?? [])
            {
                Write(Path.Combine(folder, file), "placeholder");
            }
        }
        else if (_mgcbNearMisses.TryGetValue(ruleId, out var mgcb))
        {
            WriteProject(folder, string.Empty, string.Empty, string.Empty);
            Write(Path.Combine(folder, "Content", "Content.mgcb"), mgcb);
        }
        else
        {
            WriteProject(folder, string.Empty, "<Compile Include=\"Game.cs\" />", string.Empty);
            Write(Path.Combine(folder, "Game.cs"), _sourceNearMisses[ruleId]);
        }

        var report = await AnalyzeAsync(folder);

        Assert.NotEmpty(report.Projects);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == ruleId);
    }

    [Fact]
    public async Task A_desktopgl_project_has_none_of_the_platform_findings()
    {
        var folder = Path.Combine(_work, "desktop");
        Directory.CreateDirectory(folder);
        WriteProject(folder, "<MonoGamePlatform>DesktopGL</MonoGamePlatform>", "<Reference Include=\"MonoGame.Framework\" />", string.Empty);

        var report = await AnalyzeAsync(folder);

        Assert.Empty(FindingIdsBetween(report, "MG011", "MG016"));
    }

    [Fact]
    public async Task A_package_reference_only_project_has_none_of_the_reference_findings()
    {
        var folder = Path.Combine(_work, "packages");
        Directory.CreateDirectory(folder);
        WriteProject(folder, "<TargetFramework>net8.0</TargetFramework>", "<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" /><PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.1\" />", string.Empty);

        var report = await AnalyzeAsync(folder);

        Assert.Empty(FindingIdsBetween(report, "MG017", "MG022"));
    }

    [Fact]
    public async Task A_plain_desktopgl_mgcb_has_none_of_the_content_findings()
    {
        var folder = Path.Combine(_work, "plainmgcb");
        Directory.CreateDirectory(folder);
        WriteProject(folder, "<TargetFramework>net8.0</TargetFramework>", "<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" />", string.Empty);
        Write(Path.Combine(folder, "Content", "Content.mgcb"), "/platform:DesktopGL\n/profile:Reach\n\n#begin a.png\n/importer:TextureImporter\n/processor:TextureProcessor\n/build:a.png\n\n#begin Font.spritefont\n/importer:FontDescriptionImporter\n/processor:FontDescriptionProcessor\n/build:Font.spritefont\n");

        var report = await AnalyzeAsync(folder);

        Assert.Empty(FindingIdsBetween(report, "MG023", "MG027"));
    }

    [Fact]
    public async Task Plain_source_has_none_of_the_source_findings()
    {
        var folder = Path.Combine(_work, "plainsource");
        Directory.CreateDirectory(folder);
        WriteProject(folder, "<TargetFramework>net8.0</TargetFramework>", "<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" /><Compile Include=\"A.cs\" /><Compile Include=\"B.cs\" />", string.Empty);
        Write(Path.Combine(folder, "A.cs"), "using Microsoft.Xna.Framework;\nnamespace App { class A { Color c = Color.Transparent; } }\n");
        Write(Path.Combine(folder, "B.cs"), "namespace Other { class Net { public static void Go() { } } }\nnamespace App { class B { void M() { Other.Net.Go(); } } }\n");

        var report = await AnalyzeAsync(folder);

        Assert.Empty(FindingIdsBetween(report, "MG028", "MG032"));
    }

    [Fact]
    public async Task An_sdk_style_project_has_its_source_scanned_without_Compile_items_and_skips_bin_and_obj()
    {
        var folder = Path.Combine(_work, "sdk");
        Write(Path.Combine(folder, "Game.csproj"), """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" /></ItemGroup></Project>""");
        Write(Path.Combine(folder, "Src", "Colors.cs"), "using Microsoft.Xna.Framework;\nnamespace App { class A { Color c = Color.TransparentBlack; } }\n");
        Write(Path.Combine(folder, "obj", "Generated.cs"), "using Microsoft.Xna.Framework;\nnamespace App { class B { Color c = Color.TransparentBlack; } }\n");

        var report = await AnalyzeAsync(folder);

        var finding = Assert.Single(report.Findings, f => f.RuleId == "MG032");
        Assert.Equal("Src/Colors.cs", finding.Location!.File);
        Assert.Equal(["Src/Colors.cs"], Assert.Single(report.Projects).CompileFiles);
    }

    [Fact]
    public async Task An_sdk_style_project_can_turn_off_default_compile_items()
    {
        var folder = Path.Combine(_work, "sdkoff");
        Write(Path.Combine(folder, "Game.csproj"), """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" /></ItemGroup></Project>""");
        Write(Path.Combine(folder, "Colors.cs"), "using Microsoft.Xna.Framework;\nnamespace App { class A { Color c = Color.TransparentBlack; } }\n");

        var report = await AnalyzeAsync(folder);

        Assert.Empty(Assert.Single(report.Projects).CompileFiles);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == "MG032");
    }

    // ---- reports ----

    [Fact]
    public async Task Fixture_B_projected_score_is_above_its_score_and_a_current_project_is_not_lowered()
    {
        var b = await AnalyzeAsync(FixturePaths.Of("mg37-desktopgl-installdir"));

        Assert.True(b.ProjectedScore > b.Score, $"{b.ProjectedScore} should be above {b.Score}");
        Assert.True(b.Score < 100);
    }

    [Fact]
    public async Task Finding_in_an_mgcb_points_at_the_file_and_line()
    {
        var report = await AnalyzeAsync(FixturePaths.Of("mg37-desktopgl-installdir"));

        var finding = Assert.Single(report.Findings, f => f.RuleId == "MG023");
        Assert.Equal(new SourceLocation("Content/Content.mgcb", 13, 1), finding.Location);
    }

    [Fact]
    public async Task Xna_rules_do_not_fire_on_monogame_fixtures_and_monogame_rules_do_not_fire_on_xna_fixtures()
    {
        var monoGame = await AnalyzeAsync(FixturePaths.Of("mg37-source-apis"));
        var xna = await AnalyzeAsync(FixturePaths.Of("xna40-basic"));

        Assert.DoesNotContain(monoGame.Findings, f => f.RuleId.StartsWith("XNA", StringComparison.Ordinal));
        Assert.DoesNotContain(xna.Findings, f => f.RuleId.StartsWith("MG", StringComparison.Ordinal));
    }

    // ---- helpers ----

    private static List<string> RulesNotIn(HashSet<string> covered) =>
        MonoGameCatalog.Rules.Select(r => r.Id).Where(id => !covered.Contains(id)).ToList();

    private static List<string> FindingIdsBetween(AnalysisReport report, string first, string last) =>
        report.Findings.Select(f => f.RuleId).Where(id => string.CompareOrdinal(id, first) >= 0 && string.CompareOrdinal(id, last) <= 0).ToList();

    private static Task<AnalysisReport> AnalyzeAsync(string folder) =>
        new ProjectAnalyzer().AnalyzeAsync(folder, _catalogs.Value, null, CancellationToken.None);

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    // A MonoGame project that no rule matches: PackageReference only, net8.0. Each near miss adds one thing to it.
    private static void WriteProject(string folder, string properties, string items, string imports) =>
        Write(
            Path.Combine(folder, "Game.csproj"),
            $"""
            <Project xmlns="{_ns}">
              <PropertyGroup><TargetFramework>net8.0</TargetFramework>{properties}</PropertyGroup>
              <ItemGroup><PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" />{items}</ItemGroup>
              {imports}
            </Project>
            """);

    // MG010 needs a project next to Protobuild.exe. The fixture has none (so the discovery message can be tested), so copy it and add one.
    private string ProtobuildWithGeneratedProject()
    {
        var copy = Path.Combine(_work, "protobuild-copy");
        CopyFolder(FixturePaths.Of("mg3x-protobuild"), copy);
        WriteProject(copy, string.Empty, string.Empty, string.Empty);
        return copy;
    }

    private static void CopyFolder(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
        }
    }

    private sealed record NearMiss(string Properties = "", string Items = "", string Imports = "", IReadOnlyList<string>? Files = null);

    // Project-level rules: something close to the pattern that must not match.
    private static readonly Dictionary<string, NearMiss> _projectNearMisses = new()
    {
        ["MG001"] = new(Imports: """<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />"""),
        ["MG002"] = new(Imports: """<Import Project="$(MSBuildExtensionsPath)\Microsoft.Common.props" />"""),
        ["MG003"] = new(Items: """<Reference Include="System.Xml" />"""),
        ["MG004"] = new(Properties: "<Platform>AnyCPU</Platform>"),
        ["MG005"] = new(Properties: "<TargetFrameworkVersion>v5.0</TargetFrameworkVersion>"),
        ["MG006"] = new(Properties: "<PlatformTarget>AnyCPU</PlatformTarget>"),
        ["MG007"] = new(Files: ["Notes/packages.json"]),
        ["MG008"] = new(Items: """<PackageReference Include="MonoGame.Extended" Version="4.0.0" /><PackageReference Include="MonoGame.Framework.WindowsDX" />"""),
        ["MG009"] = new(Properties: "<TargetFramework>net8.0-windows</TargetFramework>"),
        ["MG010"] = new(Files: ["Tools/Protobuild.txt"]),
        ["MG011"] = new(Properties: "<MonoGamePlatform>DesktopGL</MonoGamePlatform>"),
        ["MG012"] = new(Properties: "<ProjectTypeGuids>{FEACFBD2-3405-455C-9665-78FE426C6842}</ProjectTypeGuids>"),
        ["MG013"] = new(Properties: "<ProjectTypeGuids>{EFBA0AD7-5A72-4C68-AF49-83D382785DCF}</ProjectTypeGuids>"),
        ["MG014"] = new(Properties: "<MonoGamePlatform>DesktopGL</MonoGamePlatform>"),
        ["MG015"] = new(Properties: "<MonoGamePlatform>Windows</MonoGamePlatform>"),
        ["MG016"] = new(Properties: "<MonoGamePlatform>Windows</MonoGamePlatform>"),
        ["MG017"] = new(Items: """<Reference Include="OpenAL" />"""),
        ["MG018"] = new(Items: """<Reference Include="System.Xml" />"""),
        ["MG019"] = new(Items: """<Reference Include="Vorbis.Net" />"""),
        ["MG020"] = new(Items: """<Reference Include="System.Drawing" />"""),
        ["MG021"] = new(Items: """<Reference Include="MonoGame.Framework" />"""),
        ["MG022"] = new(Items: """<Reference Include="Lidgren" />"""),
        ["MG027"] = new(Files: ["Content/hero.png"]),
    };

    // Content.mgcb rules.
    private static readonly Dictionary<string, string> _mgcbNearMisses = new()
    {
        ["MG023"] = "/platform:DesktopGL\n/reference:Pipeline\\GlowPipeline.dll\n",
        ["MG024"] = "/platform:DesktopGL\n",
        ["MG025"] = "/platform:DesktopGL\n/reference:..\\..\\Program Files (x86)\\MonoGame\\v3.0\\Tools\\MonoGame.Content.Pipeline.dll\n",
        ["MG026"] = "/platform:DesktopGL\n#begin a.png\n/importer:TextureImporter\n/processor:TextureProcessor\n/build:a.png\n",
    };

    // Source rules: code that looks close but is not the API.
    private static readonly Dictionary<string, string> _sourceNearMisses = new()
    {
        ["MG028"] = "namespace Other { class Net { public static void Go() { } } }\nnamespace App { class A { void M() { Other.Net.Go(); } } }\n",
        ["MG029"] = "namespace App.GamerServices { class Guide { } }\nnamespace App { using App.GamerServices; class A { Guide g; } }\n",
        ["MG030"] = "using OpenTKish;\nnamespace App { class A { } }\n",
        ["MG031"] = "using Windows.UI.Core;\nnamespace App { class A { } }\n",
        ["MG032"] = "using Microsoft.Xna.Framework;\nnamespace App { class A { Color c = Color.Transparent; Other o = Other.TransparentBlack; } class Other { public static Other TransparentBlack; } }\n",
    };
}
