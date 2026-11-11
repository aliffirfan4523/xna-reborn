using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop analyzer tasks 2 and 3: platform detection, platform copies, and preprocessor symbols.</summary>
[Trait("Category", "Analyzer")]
public sealed class DeviceAnalysisTests
{
    private static readonly string[] _deviceRules =
        ["XNA007", "XNA051", "XNA052", "XNA053", "XNA054", "XNA055", "XNA056", "XNA057", "XNA058", "XNA059", "XNA060", "XNA061", "XNA062", "XNA063"];

    private static readonly Lazy<RuleCatalog> _catalog = new(() =>
        RuleCatalogLoader.LoadAsync(CatalogPath, new ReadOnlyFileSystem(), CancellationToken.None).GetAwaiter().GetResult());

    private static string CatalogPath => Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules", "xna-to-monogame.json"));

    // ---------- PlatformDetector (task 2.2) ----------
    [Theory]
    [InlineData("Windows", XnaPlatform.Windows)]
    [InlineData("  windows ", XnaPlatform.Windows)]
    [InlineData("Xbox 360", XnaPlatform.Xbox360)]
    [InlineData("Xbox360", XnaPlatform.Xbox360)]
    [InlineData("XBOX 360", XnaPlatform.Xbox360)]
    [InlineData("Zune", XnaPlatform.Zune)]
    [InlineData(" zune ", XnaPlatform.Zune)]
    [InlineData("Windows Phone", XnaPlatform.WindowsPhone)]
    [InlineData("WindowsPhone", XnaPlatform.WindowsPhone)]
    [InlineData("windows phone", XnaPlatform.WindowsPhone)]
    [InlineData("PlayStation", XnaPlatform.Unknown)]
    public void Stated_platform_accepts_each_spelling(string text, XnaPlatform expected)
    {
        var (platform, inferred) = PlatformDetector.Detect(text, [], [], XnaVersion.V40);

        Assert.Equal(expected, platform);
        Assert.False(inferred);
    }

    [Theory]
    [InlineData("Microsoft.Phone")]
    [InlineData("Microsoft.Devices.Sensors")]
    public void Missing_platform_with_phone_references_is_an_inferred_phone(string reference)
    {
        var (platform, inferred) = PlatformDetector.Detect(null, ["Microsoft.Xna.Framework", reference], [], XnaVersion.V40);

        Assert.Equal(XnaPlatform.WindowsPhone, platform);
        Assert.True(inferred);
    }

    [Fact]
    public void Missing_platform_with_zune_define_in_an_xna3_project_is_an_inferred_zune()
    {
        var (platform, inferred) = PlatformDetector.Detect("  ", ["Microsoft.Xna.Framework"], ["DEBUG", "ZUNE"], XnaVersion.V31);

        Assert.Equal(XnaPlatform.Zune, platform);
        Assert.True(inferred);
    }

    [Fact]
    public void Missing_platform_without_signals_is_unknown_and_not_inferred()
    {
        Assert.Equal((XnaPlatform.Unknown, false), PlatformDetector.Detect(null, ["Microsoft.Xna.Framework"], ["ZUNE"], XnaVersion.V40));
        Assert.Equal((XnaPlatform.Unknown, false), PlatformDetector.Detect(null, [], [], XnaVersion.Unknown));
    }

    [Fact]
    public void A_stated_platform_wins_over_phone_references()
    {
        var (platform, inferred) = PlatformDetector.Detect("Windows", ["Microsoft.Phone"], [], XnaVersion.V40);

        Assert.Equal((XnaPlatform.Windows, false), (platform, inferred));
    }

    // ---------- fixtures: platform, profile, links ----------
    [Fact]
    public async Task Phone_fixture_reads_platform_and_profile_as_stated()
    {
        var report = await AnalyzeAsync("device-wp7-xna");

        var project = Assert.Single(report.Projects);
        Assert.Equal(XnaPlatform.WindowsPhone, project.Platform);
        Assert.Equal("Reach", project.Profile);
        Assert.False(project.PlatformInferred);
        Assert.Null(project.LinkedSourceProjects);
    }

    [Fact]
    public async Task Copy_fixture_links_to_the_windows_project_only()
    {
        var report = await AnalyzeAsync("device-xbox360-copy-solution");

        var windows = Assert.Single(report.Projects, p => p.Platform == XnaPlatform.Windows);
        var copy = Assert.Single(report.Projects, p => p.Platform == XnaPlatform.Xbox360);
        Assert.Null(windows.LinkedSourceProjects);
        Assert.Equal(["Game/Game.csproj"], copy.LinkedSourceProjects);
    }

    [Fact]
    public async Task Inferred_platform_is_reported_in_the_project_table()
    {
        var folder = Path.Combine(Path.GetTempPath(), "xnareborn-infer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(
                Path.Combine(folder, "Game.csproj"),
                """
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup><XnaFrameworkVersion>v4.0</XnaFrameworkVersion></PropertyGroup>
                  <ItemGroup>
                    <Reference Include="Microsoft.Xna.Framework, Version=4.0.0.0" />
                    <Reference Include="Microsoft.Phone" />
                  </ItemGroup>
                </Project>
                """);

            var report = await new ProjectAnalyzer().AnalyzeAsync(folder, _catalog.Value, null, CancellationToken.None);

            var project = Assert.Single(report.Projects);
            Assert.Equal(XnaPlatform.WindowsPhone, project.Platform);
            Assert.True(project.PlatformInferred);
            Assert.Contains("WindowsPhone (inferred)", XnaReborn.Analyzer.Reporting.MarkdownReportWriter.Write(report), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Task<AnalysisReport> AnalyzeAsync(string fixture) =>
        new ProjectAnalyzer().AnalyzeAsync(FixturePaths.Of(fixture), _catalog.Value, null, CancellationToken.None);

    // ---------- device rules per fixture (tasks 2.5, 4.3) ----------
    [Theory]
    [InlineData("device-wp7-xna", "XNA007,XNA053,XNA054,XNA057,XNA060,XNA061,XNA062")]
    [InlineData("device-zune-31", "XNA007,XNA053,XNA056,XNA062")]
    [InlineData("device-wp-silverlight", "XNA007,XNA051")]
    [InlineData("device-windows-plain", "XNA053")]
    public async Task Fixture_triggers_exactly_the_expected_device_rules(string fixture, string expected)
    {
        var report = await AnalyzeAsync(fixture);

        var fired = report.Findings.Select(f => f.RuleId).Where(_deviceRules.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Assert.Equal(expected.Split(','), fired);
    }

    [Fact]
    public async Task Copy_rule_and_device_project_rule_fire_on_the_copy_and_not_on_the_windows_project()
    {
        var report = await AnalyzeAsync("device-xbox360-copy-solution");

        var copyOnly = report.Findings.Where(f => f.RuleId is "XNA007" or "XNA052").ToList();
        Assert.Equal(["XNA007", "XNA052"], copyOnly.Select(f => f.RuleId).Order(StringComparer.Ordinal));
        Assert.All(copyOnly, f => Assert.Equal("Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj", f.Project));
        Assert.Equal(2, report.Findings.Count(f => f.RuleId == "XNA053"));
    }

    [Fact]
    public async Task Look_alike_names_in_the_negative_fixture_do_not_fire_the_api_rules()
    {
        var report = await AnalyzeAsync("device-windows-plain");

        Assert.DoesNotContain(report.Findings, f => f.RuleId is "XNA060" or "XNA062");
    }

    // ---------- scoring (task 4.4) ----------
    [Fact]
    public async Task Each_rule_counts_once_even_when_it_fires_many_times_and_alongside_the_gamer_services_rule()
    {
        var report = await AnalyzeAsync("device-wp7-xna");

        Assert.Contains(report.Findings, f => f.RuleId == "XNA009");
        Assert.Contains(report.Findings, f => f.RuleId == "XNA054");
        Assert.True(report.Findings.Count(f => f.RuleId == "XNA057") > 1);

        var scoring = _catalog.Value.Scoring ?? ScoringOptions.Default;
        var penalty = report.Findings
            .GroupBy(f => f.RuleId, StringComparer.Ordinal)
            .Sum(g => scoring.SeverityWeights[g.First().Severity] * scoring.ClassificationFactors[g.First().Classification]);
        Assert.Equal((int)Math.Round(100 - penalty, MidpointRounding.AwayFromZero), report.Score);
    }

    // ---------- preprocessorSymbol (task 3.3) ----------
    private static readonly Rule _symbolRule = new(
        "SYM", "Symbols", Severity.Warning, Classification.Manual, null, null,
        new PreprocessorSymbolMatch(["XBOX", "WINDOWS_PHONE", "WINDOWS"]), "why", "how", null);

    private static IReadOnlyList<SourceMatch> Symbols(string code, string[]? defines = null) =>
        SourceRuleMatcher.Match(
            CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(preprocessorSymbols: defines ?? [])),
            [_symbolRule]);

    [Fact]
    public void Symbol_only_inside_an_inactive_branch_is_found()
    {
        const string code = "class C\n{\n#if DEBUG\n    int a;\n#elif XBOX\n    int b;\n#endif\n}\n";

        var match = Assert.Single(Symbols(code, ["DEBUG"]));

        Assert.Equal(5, match.Line);
        Assert.Equal(7, match.Column);
        Assert.Equal("#elif XBOX", match.Snippet);
    }

    [Fact]
    public void Symbol_inside_a_condition_expression_is_found_once_per_file()
    {
        const string code = "#if WINDOWS || XBOX\nclass A { }\n#endif\n#if !WINDOWS_PHONE\nclass B { }\n#endif\n";

        var match = Assert.Single(Symbols(code));

        Assert.Equal(1, match.Line);
        Assert.Equal(5, match.Column);
    }

    [Fact]
    public void Symbol_in_a_nested_inactive_group_is_found()
    {
        const string code = "#if FALSE_SYMBOL\n#if XBOX\nclass A { }\n#endif\n#endif\n";

        Assert.Single(Symbols(code));
    }

    [Theory]
    [InlineData("#if WINDOWS_PHONE_7\nclass A { }\n#endif\n")]
    [InlineData("#if MYXBOX || XBOX_ONE\nclass A { }\n#endif\n")]
    [InlineData("// #if XBOX\nclass A { }\n")]
    [InlineData("/* #if XBOX */\nclass A { }\n")]
    [InlineData("class A { string s = \"#if XBOX\"; }\n")]
    [InlineData("class A { string s = @\"\n#if XBOX\n\"; }\n")]
    [InlineData("#define XBOX\nclass A { }\n")]
    [InlineData("#if DEBUG\nclass A { }\n#endif\n")]
    public void Look_alike_text_is_not_a_symbol_use(string code)
    {
        Assert.Empty(Symbols(code));
    }
}
