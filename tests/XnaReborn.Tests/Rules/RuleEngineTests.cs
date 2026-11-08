using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Rules;

/// <summary>Project-level rule evaluation by <see cref="RuleEngine"/>.</summary>
[Trait("Category", "Rules")]
public sealed class RuleEngineTests
{
    private static readonly IReadOnlyDictionary<string, string> _noMetadata = new Dictionary<string, string>();

    private static Rule MakeRule(string id, RuleMatch match, IReadOnlyList<XnaVersion>? versions = null) =>
        new(id, "Title " + id, Severity.Warning, Classification.Manual, null, versions, match, "why", "how", null);

    private static RuleEngine Engine(params Rule[] rules) =>
        new(new RuleCatalog(1, "test", null, rules), new ReadOnlyFileSystem());

    private static ProjectReadResult Project(
        XnaVersion version = XnaVersion.V40,
        string[]? references = null,
        Dictionary<string, string>? properties = null,
        ProjectItem[]? items = null,
        ContentItem[]? content = null)
    {
        var info = new ProjectInfo(
            "Game.csproj", "Game", true, version, XnaPlatform.Windows, null, null,
            references ?? [], [], [], [], content ?? []);
        return new ProjectReadResult(info, properties ?? [], items ?? [], []);
    }

    private static ContentItem Content(string path, string? importer = null, string? processor = null) =>
        new(path, null, importer, processor, new Dictionary<string, string>());

    private static async Task<ProjectReadResult> ReadFixture(string fixture, string projectFile)
    {
        var root = FixturePaths.Of(fixture);
        return await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(Path.Combine(root, projectFile), root, CancellationToken.None);
    }

    private static IReadOnlyList<Finding> RunOnFixture(RuleMatch match, ProjectReadResult project, string fixture) =>
        Engine(MakeRule("XNA900", match)).EvaluateProject(project, FixturePaths.Of(fixture), "p.csproj", CancellationToken.None);

    [Fact]
    public void Reference_rule_fires_and_does_not_fire()
    {
        var engine = Engine(MakeRule("XNA900", new ReferenceMatch("Microsoft.Xna.Framework.GamerServices")));

        var hit = engine.EvaluateProject(Project(references: ["System", "Microsoft.Xna.Framework.GamerServices"]), "x", "Game.csproj", CancellationToken.None);
        var miss = engine.EvaluateProject(Project(references: ["System"]), "x", "Game.csproj", CancellationToken.None);

        var finding = Assert.Single(hit);
        Assert.Equal("XNA900", finding.RuleId);
        Assert.Equal("Title XNA900", finding.Title);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(Classification.Manual, finding.Classification);
        Assert.Equal("Game.csproj", finding.Project);
        Assert.Null(finding.Location);
        Assert.Equal("Microsoft.Xna.Framework.GamerServices", finding.Snippet);
        Assert.Empty(miss);
    }

    [Fact]
    public async Task Reference_rule_on_fixtures()
    {
        var match = new ReferenceMatch("Microsoft.Xna.Framework.GamerServices");
        var gs = await ReadFixture("xna40-gamerservices", "Xna40GamerServices.csproj");
        var basic = await ReadFixture("xna40-basic", "Xna40Basic.csproj");

        Assert.Single(RunOnFixture(match, gs, "xna40-gamerservices"));
        Assert.Empty(RunOnFixture(match, basic, "xna40-basic"));
    }

    [Fact]
    public void Reference_wildcard_and_case_insensitivity()
    {
        var engine = Engine(MakeRule("XNA900", new ReferenceMatch("microsoft.xna.framework.*")));

        var hit = engine.EvaluateProject(Project(references: ["Microsoft.Xna.Framework.Game"]), "x", "p", CancellationToken.None);
        var miss = engine.EvaluateProject(Project(references: ["Other.Lib"]), "x", "p", CancellationToken.None);

        Assert.Single(hit);
        Assert.Empty(miss);
    }

    [Fact]
    public void ProjectProperty_exists_equals_and_matches()
    {
        var props = new Dictionary<string, string> { ["XnaFrameworkVersion"] = "v3.1" };
        var exists = Engine(MakeRule("A", new ProjectPropertyMatch("xnaframeworkversion")));
        var equals = Engine(MakeRule("B", new ProjectPropertyMatch("XnaFrameworkVersion", "V3.1")));
        var regex = Engine(MakeRule("C", new ProjectPropertyMatch("XnaFrameworkVersion", null, @"^v3\.\d$")));

        Assert.Single(exists.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
        Assert.Single(equals.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
        Assert.Single(regex.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
    }

    [Fact]
    public void ProjectProperty_negative_cases()
    {
        var props = new Dictionary<string, string> { ["XnaFrameworkVersion"] = "v4.0" };
        var missing = Engine(MakeRule("A", new ProjectPropertyMatch("Other")));
        var notEqual = Engine(MakeRule("B", new ProjectPropertyMatch("XnaFrameworkVersion", "v3.1")));
        var noRegex = Engine(MakeRule("C", new ProjectPropertyMatch("XnaFrameworkVersion", null, @"^v3")));
        var badRegex = Engine(MakeRule("D", new ProjectPropertyMatch("XnaFrameworkVersion", null, "(")));

        Assert.Empty(missing.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
        Assert.Empty(notEqual.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
        Assert.Empty(noRegex.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
        Assert.Empty(badRegex.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
    }

    [Fact]
    public void ProjectProperty_regex_timeout_counts_as_no_match()
    {
        var engine = Engine(MakeRule("A", new ProjectPropertyMatch("P", null, @"^(a+)+$")));
        var props = new Dictionary<string, string> { ["P"] = new string('a', 60) + "!" };

        Assert.Empty(engine.EvaluateProject(Project(properties: props), "x", "p", CancellationToken.None));
    }

    [Fact]
    public async Task ProjectProperty_on_fixtures_fires_only_on_xna31()
    {
        var match = new ProjectPropertyMatch("XnaFrameworkVersion", "v3.1");
        var legacy = await ReadFixture("xna31-legacy", "Xna31Legacy.csproj");
        var basic = await ReadFixture("xna40-basic", "Xna40Basic.csproj");

        Assert.Single(RunOnFixture(match, legacy, "xna31-legacy"));
        Assert.Empty(RunOnFixture(match, basic, "xna40-basic"));
    }

    [Fact]
    public void ProjectItem_matches_type_and_include_wildcard()
    {
        var items = new[]
        {
            new ProjectItem("Import", @"$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\x.targets", _noMetadata),
            new ProjectItem("Compile", "Game1.cs", _noMetadata),
        };
        var engine = Engine(MakeRule("A", new ProjectItemMatch("import", "*xna game studio*")));
        var wrongType = Engine(MakeRule("B", new ProjectItemMatch("Reference", "*XNA Game Studio*")));
        var wrongInclude = Engine(MakeRule("C", new ProjectItemMatch("Import", "*Nope*")));
        var typeOnly = Engine(MakeRule("D", new ProjectItemMatch("Compile")));

        Assert.Single(engine.EvaluateProject(Project(items: items), "x", "p", CancellationToken.None));
        Assert.Empty(wrongType.EvaluateProject(Project(items: items), "x", "p", CancellationToken.None));
        Assert.Empty(wrongInclude.EvaluateProject(Project(items: items), "x", "p", CancellationToken.None));
        Assert.Equal("Game1.cs", Assert.Single(typeOnly.EvaluateProject(Project(items: items), "x", "p", CancellationToken.None)).Snippet);
    }

    [Fact]
    public async Task ProjectItem_import_on_fixtures()
    {
        var match = new ProjectItemMatch("Import", "*XNA Game Studio*");
        var legacy = await ReadFixture("xna31-legacy", "Xna31Legacy.csproj");
        var notXna = await ReadFixture("not-xna", "NotXna.csproj");

        Assert.Single(RunOnFixture(match, legacy, "xna31-legacy"));
        Assert.Empty(RunOnFixture(match, notXna, "not-xna"));
    }

    [Fact]
    public async Task FilePattern_on_fixtures()
    {
        var match = new FilePatternMatch("**/*.xap");
        var custom = await ReadFixture("xna40-custom-content", "CustomContent.csproj");
        var basic = await ReadFixture("xna40-basic", "Xna40Basic.csproj");

        var hit = Assert.Single(RunOnFixture(match, custom, "xna40-custom-content"));
        Assert.Equal("Content/Audio/Sounds.xap", hit.Snippet);
        Assert.Empty(RunOnFixture(match, basic, "xna40-basic"));
    }

    [Fact]
    public async Task FilePattern_without_wildcard_checks_existence()
    {
        var custom = await ReadFixture("xna40-custom-content", "CustomContent.csproj");

        Assert.Single(RunOnFixture(new FilePatternMatch("Content/Audio/Sounds.xap"), custom, "xna40-custom-content"));
        Assert.Empty(RunOnFixture(new FilePatternMatch("Content/Audio/Missing.xap"), custom, "xna40-custom-content"));
    }

    [Fact]
    public void FilePattern_skips_bin_obj_git_vs_and_lists_with_forward_slashes()
    {
        var temp = Path.Combine(Path.GetTempPath(), "xnareborn-glob-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var dir in new[] { "Content/Sub", "bin", "obj", ".git", ".vs" })
            {
                Directory.CreateDirectory(Path.Combine(temp, dir));
                File.WriteAllText(Path.Combine(temp, dir, "a.xap"), "x");
            }

            File.WriteAllText(Path.Combine(temp, "Content", "a.xap"), "x");
            var finding = Assert.Single(Engine(MakeRule("A", new FilePatternMatch("**/*.XAP"))).EvaluateProject(Project(), temp, "p", CancellationToken.None));

            Assert.Equal("Content/Sub/a.xap, Content/a.xap", finding.Snippet);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ContentItem_extension_importer_processor_are_anded()
    {
        var content = new[]
        {
            Content(@"Fonts\a.spritefont", "FontDescriptionImporter", "FontDescriptionProcessor"),
            Content(@"Models\b.fbx", "FbxImporter", "OutlineModelProcessor"),
        };
        var project = Project(content: content);

        Assert.Equal(@"Fonts\a.spritefont", Assert.Single(Engine(MakeRule("A", new ContentItemMatch(".SPRITEFONT"))).EvaluateProject(project, "x", "p", CancellationToken.None)).Snippet);
        Assert.Equal(@"Models\b.fbx", Assert.Single(Engine(MakeRule("B", new ContentItemMatch(null, null, "outline*"))).EvaluateProject(project, "x", "p", CancellationToken.None)).Snippet);
        Assert.Single(Engine(MakeRule("C", new ContentItemMatch(".fbx", "Fbx*", "Outline*"))).EvaluateProject(project, "x", "p", CancellationToken.None));
        Assert.Empty(Engine(MakeRule("D", new ContentItemMatch(".spritefont", null, "Outline*"))).EvaluateProject(project, "x", "p", CancellationToken.None));
        Assert.Empty(Engine(MakeRule("E", new ContentItemMatch(".png"))).EvaluateProject(project, "x", "p", CancellationToken.None));
    }

    [Fact]
    public async Task ContentItem_on_fixtures()
    {
        var custom = await ReadFixture("xna40-custom-content", "CustomContent.csproj");
        var basic = await ReadFixture("xna40-basic", "Xna40Basic.csproj");

        Assert.Single(RunOnFixture(new ContentItemMatch(null, null, "Outline*"), custom, "xna40-custom-content"));
        Assert.Empty(RunOnFixture(new ContentItemMatch(null, null, "Outline*"), basic, "xna40-basic"));
        Assert.Single(RunOnFixture(new ContentItemMatch(".spritefont"), basic, "xna40-basic"));
        Assert.Single(RunOnFixture(new ContentItemMatch(".spritefont"), custom, "xna40-custom-content"));
    }

    [Fact]
    public void XnaVersions_filter_limits_rules_and_unknown_never_matches()
    {
        var rule = MakeRule("A", new ReferenceMatch("System"), [XnaVersion.V30, XnaVersion.V31]);
        var engine = Engine(rule);

        Assert.Single(engine.EvaluateProject(Project(XnaVersion.V31, ["System"]), "x", "p", CancellationToken.None));
        Assert.Empty(engine.EvaluateProject(Project(XnaVersion.V40, ["System"]), "x", "p", CancellationToken.None));
        Assert.Empty(engine.EvaluateProject(Project(XnaVersion.Unknown, ["System"]), "x", "p", CancellationToken.None));
    }

    [Fact]
    public void One_finding_per_rule_with_capped_snippet_in_catalog_order()
    {
        var engine = Engine(
            MakeRule("XNA002", new ReferenceMatch("Lib.*")),
            MakeRule("XNA001", new ReferenceMatch("System")));

        var findings = engine.EvaluateProject(Project(references: ["Lib.A", "Lib.B", "Lib.C", "Lib.D", "Lib.E", "System"]), "x", "p", CancellationToken.None);

        Assert.Equal(["XNA002", "XNA001"], findings.Select(f => f.RuleId));
        Assert.Equal("Lib.A, Lib.B, Lib.C +2 more", findings[0].Snippet);
    }

    [Fact]
    public void SourceRules_lists_only_namespace_type_and_member_rules()
    {
        var engine = Engine(
            MakeRule("A", new ReferenceMatch("x")),
            MakeRule("B", new NamespaceMatch("Microsoft.Xna")),
            MakeRule("C", new TypeMatch("Game", ["Microsoft.Xna.Framework"])),
            MakeRule("D", new MemberMatch("Foo", ["Microsoft.Xna.Framework"])));

        Assert.Equal(["B", "C", "D"], engine.SourceRules.Select(r => r.Id));
        Assert.Empty(engine.EvaluateProject(Project(), "x", "p", CancellationToken.None));
    }

    [Theory]
    [InlineData("abc", "ABC", true)]
    [InlineData("a*c", "abbbc", true)]
    [InlineData("a*c", "ab", false)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]
    [InlineData("*", "anything", true)]
    [InlineData("*x*", "aXb", true)]
    [InlineData("a**", "a", true)]
    public void Wildcard_semantics_through_reference_rules(string pattern, string reference, bool expected)
    {
        var engine = Engine(MakeRule("A", new ReferenceMatch(pattern)));

        var findings = engine.EvaluateProject(Project(references: [reference]), "x", "p", CancellationToken.None);

        Assert.Equal(expected, findings.Count == 1);
    }

    [Fact]
    public void Cancellation_is_honored()

    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var engine = Engine(MakeRule("A", new ReferenceMatch("System")));

        Assert.Throws<OperationCanceledException>(() => engine.EvaluateProject(Project(references: ["System"]), "x", "p", cts.Token));
    }

    [Fact]
    public void Catalog_deserialized_from_json_evaluates()
    {
        const string json = """
            { "version": 1, "name": "t", "rules": [ {
              "id": "XNA001", "title": "T", "severity": "warning", "classification": "manual",
              "match": { "type": "reference", "name": "System" },
              "explanation": "e", "resolution": "r" } ] }
            """;
        var catalog = System.Text.Json.JsonSerializer.Deserialize<RuleCatalog>(json, RuleCatalogJson.Options)!;

        var findings = new RuleEngine(catalog, new ReadOnlyFileSystem()).EvaluateProject(Project(references: ["System"]), "x", "p", CancellationToken.None);

        Assert.Single(findings);
    }
}
