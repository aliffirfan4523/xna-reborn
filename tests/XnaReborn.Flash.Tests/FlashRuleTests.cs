using System.Collections.Concurrent;
using System.Security.Cryptography;
using VerifyXunit;
using XnaReborn.Flash.Analyzer;
using XnaReborn.Flash.Analyzer.Rules;

namespace XnaReborn.Flash.Tests;

public class FlashRuleTests
{
    private static readonly ConcurrentDictionary<string, FlashAnalysisReport> _reports = new();

    /// <summary>Every rule in the catalog and the fixture where it must fire. The negative fixture is flx-plain-as3 unless listed in <see cref="NegativeOverrides"/>.</summary>
    private static readonly Dictionary<string, string> _positives = new()
    {
        ["FL001"] = "flx-hello",
        ["FL002"] = "flx-flixel3",
        ["FL003"] = "flx-swf-only",
        ["FL004"] = "flx-fla-only",
        ["FL005"] = "flx-swc",
        ["FL010"] = "flx-embeds",
        ["FL011"] = "flx-embeds",
        ["FL012"] = "flx-embeds",
        ["FL013"] = "flx-embeds",
        ["FL014"] = "flx-embeds",
        ["FL015"] = "flx-missing-embed",
        ["FL020"] = "flx-gaps",
        ["FL021"] = "flx-gaps",
        ["FL022"] = "flx-gaps",
        ["FL023"] = "flx-gaps",
        ["FL024"] = "flx-gaps",
        ["FL025"] = "flx-gaps",
        ["FL026"] = "flx-gaps",
        ["FL027"] = "flx-gaps",
        ["FL028"] = "flx-gaps",
        ["FL029"] = "flx-gaps",
        ["FL030"] = "flx-gaps",
        ["FL031"] = "flx-gaps",
        ["FL040"] = "flx-gaps",
        ["FL041"] = "flx-gaps",
        ["FL042"] = "flx-gaps",
        ["FL043"] = "flx-gaps",
        ["FL044"] = "flx-gaps",
        ["FL045"] = "flx-gaps",
        ["FL046"] = "flx-gaps",
        ["FL047"] = "flx-gaps",
        ["FL050"] = "flx-gaps",
        ["FL051"] = "flx-gaps",
        ["FL052"] = "flx-gaps",
        ["FL053"] = "flx-gaps",
        ["FL060"] = "flx-two-entries",
    };

    private static readonly Dictionary<string, string> _negativeOverrides = new()
    {
        ["FL002"] = "flx-bundled-flixel",
        ["FL060"] = "flx-hello",
    };

    public static IEnumerable<object[]> RuleRows() => _positives.Keys.Order(StringComparer.Ordinal).Select(k => new object[] { k });

    private static FlashAnalysisReport Report(string fixture) =>
        _reports.GetOrAdd(fixture, f => FlashProjectAnalyzer.AnalyzeAsync(FlashFixtures.Of(f)).GetAwaiter().GetResult());

    [Fact]
    public void Catalog_loads_and_every_rule_has_a_positive_fixture()
    {
        var catalog = FlashRuleCatalog.LoadBuiltIn();
        Assert.Equal("flash-to-monogame", catalog.Name);
        Assert.Equal(catalog.Rules.Select(r => r.Id).Order(StringComparer.Ordinal), _positives.Keys.Order(StringComparer.Ordinal));
        Assert.All(catalog.Rules, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Explanation));
            Assert.False(string.IsNullOrWhiteSpace(r.Resolution));
            Assert.Matches(@"^FL\d{3}$", r.Id);
        });
    }

    [Fact]
    public void Catalog_rejects_duplicate_ids_and_missing_match_types()
    {
        const string dup = "{\"name\":\"x\",\"rules\":[{\"id\":\"FL001\",\"title\":\"t\",\"severity\":\"Info\",\"classification\":\"Manual\",\"match\":{\"type\":\"as3Syntax\"},\"explanation\":\"e\",\"resolution\":\"r\"},{\"id\":\"FL001\",\"title\":\"t\",\"severity\":\"Info\",\"classification\":\"Manual\",\"match\":{\"type\":\"as3Syntax\"},\"explanation\":\"e\",\"resolution\":\"r\"}]}";
        Assert.Throws<InvalidOperationException>(() => FlashRuleCatalog.Parse(dup));
        const string none = "{\"name\":\"x\",\"rules\":[{\"id\":\"FL001\",\"title\":\"t\",\"severity\":\"Info\",\"classification\":\"Manual\",\"match\":{\"type\":\"\"},\"explanation\":\"e\",\"resolution\":\"r\"}]}";
        Assert.Throws<InvalidOperationException>(() => FlashRuleCatalog.Parse(none));
    }

    [Theory]
    [MemberData(nameof(RuleRows))]
    public void Rule_fires_on_its_positive_fixture(string ruleId)
    {
        Assert.Contains(Report(_positives[ruleId]).Findings, f => f.RuleId == ruleId);
    }

    [Theory]
    [MemberData(nameof(RuleRows))]
    public void Rule_stays_quiet_on_its_negative_fixture(string ruleId)
    {
        var negative = _negativeOverrides.GetValueOrDefault(ruleId, "flx-plain-as3");
        Assert.DoesNotContain(Report(negative).Findings, f => f.RuleId == ruleId);
    }

    [Fact]
    public void Findings_point_at_the_right_files()
    {
        var gaps = Report("flx-gaps").Findings;
        Assert.Equal("src/GapWith.as", gaps.First(f => f.RuleId == "FL020").Location?.File);
        Assert.Equal("src/GapNumbers.as", gaps.First(f => f.RuleId == "FL027").Location?.File);
        Assert.Equal("src/GapNumbers.as", gaps.First(f => f.RuleId == "FL028").Location?.File);
        Assert.Equal("src/GapRegex.as", gaps.First(f => f.RuleId == "FL030").Location?.File);
        Assert.Equal("return total / parts;", gaps.First(f => f.RuleId == "FL027").Snippet);
    }

    [Fact]
    public void Platformer_fixture_parses_cleanly_and_shows_the_expected_rules()
    {
        var r = Report("flx-platformer");
        Assert.Empty(r.Diagnostics);
        Assert.Equal("PlatformerGame", r.EntryClass);
        var ids = r.Findings.Select(f => f.RuleId).ToHashSet();
        Assert.Superset(new HashSet<string> { "FL001", "FL010", "FL011", "FL026", "FL027", "FL028" }, ids);
        Assert.DoesNotContain("FL060", ids);
        Assert.Contains(r.Findings, f => f.RuleId == "FL027" && f.Location?.File == "src/MenuState.as");
        Assert.Contains(r.Findings, f => f.RuleId == "FL027" && f.Location?.File == "src/Score.as");
        Assert.DoesNotContain(r.Findings, f => f.RuleId == "FL027" && f.Location?.File == "src/Player.as");
    }

    [Fact]
    public void Score_drops_with_blockers_and_projection_is_higher_when_findings_are_fixable()
    {
        Assert.True(Report("flx-swf-only").Score < Report("flx-hello").Score);
        var p = Report("flx-platformer");
        Assert.True(p.ProjectedScore >= p.Score);
    }

    [Fact]
    public async Task Analysis_changes_no_file_in_any_fixture()
    {
        string[] Before() => [.. Directory.EnumerateFiles(FlashFixtures.Root, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(f => f + "=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))))];
        var before = Before();
        foreach (var name in FlashFixtures.Names)
        {
            await FlashProjectAnalyzer.AnalyzeAsync(FlashFixtures.Of(name));
        }

        Assert.Equal(before, Before());
    }

    [Fact]
    public async Task Reports_are_identical_between_runs()
    {
        var a = FlashProjectAnalyzer.ToJson(await FlashProjectAnalyzer.AnalyzeAsync(FlashFixtures.Of("flx-gaps")));
        var b = FlashProjectAnalyzer.ToJson(await FlashProjectAnalyzer.AnalyzeAsync(FlashFixtures.Of("flx-gaps")));
        Assert.Equal(a, b);
        Assert.DoesNotContain(FlashFixtures.Root, a, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancelled_analysis_throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FlashProjectAnalyzer.AnalyzeAsync(FlashFixtures.Of("flx-hello"), null, null, cts.Token));
    }

    [Theory]
    [InlineData("flx-hello")]
    [InlineData("flx-platformer")]
    [InlineData("flx-gaps")]
    public async Task Report_snapshots(string fixture)
    {
        var report = Report(fixture);
        await Verifier.Verify(FlashProjectAnalyzer.ToJson(report), "json").UseParameters(fixture + "-json");
        await Verifier.Verify(FlashProjectAnalyzer.ToMarkdown(report), "md").UseParameters(fixture + "-md");
    }
}
