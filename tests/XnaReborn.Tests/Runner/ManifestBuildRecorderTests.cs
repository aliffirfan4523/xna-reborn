using System.Text.Json.Nodes;
using XnaReborn.Migrator.Output;
using XnaReborn.Runner.Build;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;
using XnaReborn.Tests.Migrator;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
[Trait("Category", "Safety")]
public sealed class ManifestBuildRecorderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xr-recorder-" + Guid.NewGuid().ToString("N"));

    public ManifestBuildRecorderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string JsonPath => Path.Combine(_dir, "xnareborn-manifest.json");

    private string MarkdownPath => Path.Combine(_dir, "xnareborn-manifest.md");

    private static Manifest Base(ManifestBuild? build = null) => new(
        1,
        "0.1.0",
        "xna-to-monogame",
        true,
        [
            new ManifestChange("Game.csproj", ChangeKind.Created, ["XNA001"], "SDK project"),
            new ManifestChange("Game1.cs", ChangeKind.Modified, ["XNA022", "XNA024"], null),
        ],
        build);

    private void WriteBase(bool withMarkdown = true)
    {
        var manifest = Base();
        File.WriteAllText(JsonPath, ManifestJsonWriter.Write(manifest));
        if (withMarkdown)
        {
            File.WriteAllText(MarkdownPath, ManifestMarkdownWriter.Write(manifest, []));
        }
    }

    private static BuildDiagnostic Error(string code, string message, string? file = null, int? line = null, string? asset = null) =>
        new(BuildDiagnosticSeverity.Error, code, message, file, line, line is null ? null : 1, null, asset);

    private static BuildResult Failed(params BuildDiagnostic[] diagnostics) => new(
        false,
        TimeSpan.FromSeconds(2),
        [new DiagnosticGroup(null, diagnostics)],
        "raw",
        1,
        false,
        false);

    private static BuildResult Succeeded() => new(true, TimeSpan.FromSeconds(2), [], "ok", 0, false, false);

    [Fact]
    public async Task Failure_WritesTheSameJsonTheMigratorWouldWrite()
    {
        WriteBase();
        var game = Path.Combine(_dir, "Game1.cs");
        var build = Failed(
            Error("CS0246", "The type or namespace name 'ResolveTexture2D' could not be found", game, 14),
            Error("CS0103", "The name 'x' does not exist", game, 3));

        await new ManifestBuildRecorder().RecordAsync(_dir, build, CancellationToken.None);

        var expected = ManifestJsonWriter.Write(Base(new ManifestBuild(
            true,
            false,
            [
                "CS0103: The name 'x' does not exist (Game1.cs:3)",
                "CS0246: The type or namespace name 'ResolveTexture2D' could not be found (Game1.cs:14)",
            ])));
        Assert.Equal(expected, File.ReadAllText(JsonPath));
    }

    [Fact]
    public async Task Success_WritesAnEmptyErrorList()
    {
        WriteBase();

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        Assert.Equal(ManifestJsonWriter.Write(Base(new ManifestBuild(true, true, []))), File.ReadAllText(JsonPath));
    }

    [Fact]
    public async Task OtherProperties_KeepTheirValuesAndOrder()
    {
        WriteBase();
        var before = JsonNode.Parse(File.ReadAllText(JsonPath))!.AsObject();

        await new ManifestBuildRecorder().RecordAsync(_dir, Failed(Error("CS1", "m")), CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(JsonPath))!.AsObject();
        Assert.Equal(before.Select(p => p.Key), after.Select(p => p.Key));
        foreach (var (key, value) in before.Where(p => p.Key != "build"))
        {
            Assert.Equal(value?.ToJsonString(), after[key]?.ToJsonString());
        }

        var build = after["build"]!.AsObject();
        Assert.Equal(["attempted", "succeeded", "errors"], build.Select(p => p.Key));
        Assert.True(build["attempted"]!.GetValue<bool>());
        Assert.False(build["succeeded"]!.GetValue<bool>());
    }

    [Fact]
    public async Task UnknownProperties_AreKept()
    {
        File.WriteAllText(JsonPath, "{\n  \"version\": 1,\n  \"completed\": true,\n  \"future\": { \"a\": [1, 2] },\n  \"build\": null\n}\n");

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(JsonPath))!.AsObject();
        Assert.Equal(["version", "completed", "future", "build"], after.Select(p => p.Key));
        Assert.Equal("[1,2]", after["future"]!["a"]!.ToJsonString());
    }

    [Fact]
    public async Task ExistingBuildObject_IsReplaced()
    {
        File.WriteAllText(JsonPath, ManifestJsonWriter.Write(Base(new ManifestBuild(true, false, ["OLD: error"]))));

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        Assert.Equal(ManifestJsonWriter.Write(Base(new ManifestBuild(true, true, []))), File.ReadAllText(JsonPath));
    }

    [Fact]
    public async Task MissingBuildProperty_IsAppended()
    {
        File.WriteAllText(JsonPath, "{\n  \"version\": 1,\n  \"completed\": true\n}\n");

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        var after = JsonNode.Parse(File.ReadAllText(JsonPath))!.AsObject();
        Assert.Equal(["version", "completed", "build"], after.Select(p => p.Key));
    }

    [Fact]
    public async Task Output_UsesLfAndOneTrailingNewline()
    {
        WriteBase();

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        var text = File.ReadAllText(JsonPath);
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.False(File.ReadAllBytes(JsonPath).AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]));
    }

    [Fact]
    public async Task Errors_AreErrorsOnly_Distinct_AndSorted()
    {
        WriteBase();
        var warning = new BuildDiagnostic(BuildDiagnosticSeverity.Warning, "CS0168", "unused", "A.cs", 1, 1, null, null);
        var build = new BuildResult(
            false,
            TimeSpan.Zero,
            [
                new DiagnosticGroup(null, [Error("MSB2", "b"), warning]),
                new DiagnosticGroup(null, [Error("MSB1", "a"), Error("MSB2", "b")]),
            ],
            string.Empty,
            1,
            false,
            false);

        await new ManifestBuildRecorder().RecordAsync(_dir, build, CancellationToken.None);

        var errors = JsonNode.Parse(File.ReadAllText(JsonPath))!["build"]!["errors"]!.AsArray().Select(n => n!.GetValue<string>());
        Assert.Equal(["MSB1: a", "MSB2: b"], errors);
    }

    [Fact]
    public async Task ContentErrors_UseTheAssetPath_AndRelativePaths()
    {
        WriteBase();
        var asset = Path.Combine(_dir, "Content", "hero.png");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "Other.cs");

        await new ManifestBuildRecorder().RecordAsync(
            _dir,
            Failed(Error("MGCB", "unknown image type", asset: asset), Error("CS1", "oops", outside, 7), Error("NU1101", "no package")),
            CancellationToken.None);

        var errors = JsonNode.Parse(File.ReadAllText(JsonPath))!["build"]!["errors"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.Contains("MGCB: unknown image type (Content/hero.png)", errors);
        Assert.Contains($"CS1: oops ({outside}:7)", errors);
        Assert.Contains("NU1101: no package", errors);
    }

    [Fact]
    public async Task Markdown_AppendsABuildSectionWithErrors()
    {
        WriteBase();
        var before = File.ReadAllText(MarkdownPath);

        await new ManifestBuildRecorder().RecordAsync(_dir, Failed(Error("CS1", "one"), Error("CS2", "two")), CancellationToken.None);

        var after = File.ReadAllText(MarkdownPath);
        Assert.StartsWith(before.TrimEnd('\n') + "\n\n## Build\n\nFailed with 2 error(s).\n\n- CS1: one\n- CS2: two\n", after);
        Assert.EndsWith("- CS2: two\n", after);
        Assert.DoesNotContain('\r', after);
    }

    [Fact]
    public async Task Markdown_SuccessSection()
    {
        WriteBase();

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        Assert.EndsWith("\n\n## Build\n\nSucceeded.\n", File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task Markdown_ReplacesAnExistingBuildSection_AndKeepsTheRest()
    {
        File.WriteAllText(JsonPath, ManifestJsonWriter.Write(Base()));
        File.WriteAllText(
            MarkdownPath,
            "# Manifest\n\n## Summary\n\nx\n\n## Build\n\nFailed with 1 error(s).\n\n- OLD: gone\n\n## Notes\n\nkept\n");

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        Assert.Equal("# Manifest\n\n## Summary\n\nx\n\n## Build\n\nSucceeded.\n\n## Notes\n\nkept\n", File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task Markdown_ReplacesTheBuildSectionAtTheEnd()
    {
        File.WriteAllText(JsonPath, ManifestJsonWriter.Write(Base()));
        File.WriteAllText(MarkdownPath, "# Manifest\n\n## Build\n\nSucceeded.\n");

        await new ManifestBuildRecorder().RecordAsync(_dir, Failed(Error("CS1", "one")), CancellationToken.None);

        Assert.Equal("# Manifest\n\n## Build\n\nFailed with 1 error(s).\n\n- CS1: one\n", File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task Markdown_RecordingTwiceGivesTheSameFile()
    {
        WriteBase();
        var recorder = new ManifestBuildRecorder();

        await recorder.RecordAsync(_dir, Failed(Error("CS1", "one")), CancellationToken.None);
        var json = File.ReadAllText(JsonPath);
        var markdown = File.ReadAllText(MarkdownPath);
        await recorder.RecordAsync(_dir, Failed(Error("CS1", "one")), CancellationToken.None);

        Assert.Equal(json, File.ReadAllText(JsonPath));
        Assert.Equal(markdown, File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task Markdown_CrlfFileKeepsCrlf()
    {
        File.WriteAllText(JsonPath, ManifestJsonWriter.Write(Base()));
        File.WriteAllText(MarkdownPath, "# Manifest\r\n\r\n## Build\r\n\r\nSucceeded.\r\n\r\n## Notes\r\n\r\nkept\r\n");

        await new ManifestBuildRecorder().RecordAsync(_dir, Failed(Error("CS1", "one")), CancellationToken.None);

        Assert.Equal(
            "# Manifest\r\n\r\n## Build\r\n\r\nFailed with 1 error(s).\r\n\r\n- CS1: one\r\n\r\n## Notes\r\n\r\nkept\r\n",
            File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task MissingMarkdown_IsNotCreated()
    {
        WriteBase(withMarkdown: false);

        await new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None);

        Assert.False(File.Exists(MarkdownPath));
    }

    [Fact]
    public async Task OnlyTheTwoManifestFilesChange_AndNoTempFileStaysBehind()
    {
        WriteBase();
        File.WriteAllText(Path.Combine(_dir, "Game1.cs"), "class G {}");
        var before = MigrationTestSupport.Tree(_dir);

        await new ManifestBuildRecorder().RecordAsync(_dir, Failed(Error("CS1", "one")), CancellationToken.None);

        var after = MigrationTestSupport.Tree(_dir);
        Assert.Equal(before.Keys, after.Keys);
        var changed = after.Where(p => before[p.Key] != p.Value).Select(p => p.Key).Order(StringComparer.Ordinal);
        Assert.Equal(["xnareborn-manifest.json", "xnareborn-manifest.md"], changed);
    }

    [Fact]
    public async Task MissingJson_Throws_AndWritesNothing()
    {
        File.WriteAllText(MarkdownPath, "# M\n");

        await Assert.ThrowsAsync<FileNotFoundException>(() => new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None));

        Assert.Equal("# M\n", File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task InvalidJson_Throws_AndLeavesTheFilesAlone()
    {
        File.WriteAllText(JsonPath, "{ not json");
        File.WriteAllText(MarkdownPath, "# M\n");

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None));

        Assert.Equal("{ not json", File.ReadAllText(JsonPath));
        Assert.Equal("# M\n", File.ReadAllText(MarkdownPath));
    }

    [Fact]
    public async Task JsonThatIsNotAnObject_Throws()
    {
        File.WriteAllText(JsonPath, "[1]");

        await Assert.ThrowsAsync<InvalidDataException>(() => new ManifestBuildRecorder().RecordAsync(_dir, Succeeded(), CancellationToken.None));
    }
}
