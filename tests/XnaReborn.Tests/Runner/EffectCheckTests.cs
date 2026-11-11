using XnaReborn.Migrator.Effects;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Effects;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Processes;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Runner;

/// <summary>The mgfxc output parser, the profile escalator, the effect build check and the effect hints.</summary>
[Trait("Category", "Runner")]
public sealed class EffectCheckTests : IDisposable
{
    private const string _limitMessage = "error X5608: Compiled shader code uses too many arithmetic instruction slots (72). Max. allowed by the target (ps_4_0_level_9_1) is 64.";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xr-fxcheck-" + Guid.NewGuid().ToString("N"));

    public EffectCheckTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "xnareborn-manifest.json"), """{"version":1,"completed":true}""");
        File.WriteAllText(Path.Combine(_root, "Game.csproj"), "<Project />");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Sm2Rewritten() =>
        FxProfileRewriter.Rewrite(File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-effects"), "Content", "Sm2.fx"))).Text;

    private string WriteEffect(string name, string text)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static BuildDiagnostic Error(string code, string message) =>
        new(BuildDiagnosticSeverity.Error, code, message, null, null, null, null, null);

    // ---------- parser (real outputs recorded under Runner/Samples) ----------

    [Fact]
    public void Parser_reads_a_parse_error_once_and_ignores_the_summary_line()
    {
        var found = MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-parse-error"));

        var d = Assert.Single(found);
        Assert.Equal("MGFX0001", d.Code);
        Assert.Equal(BuildDiagnosticSeverity.Error, d.Severity);
        Assert.Equal("C:/work/obj/AlphaTest.fx", d.File);
        Assert.Equal(13, d.Line);
        Assert.Equal(9, d.Column);
        Assert.Equal("Unexpected token 'A' found. Expected CloseBracket", d.Message);
        Assert.Equal(d.File, d.AssetPath);
    }

    [Fact]
    public void Parser_reads_profile_errors_with_and_without_a_model_name()
    {
        var named = Assert.Single(MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-profile-error"), "C:/work/obj/Sm2.fx"));
        var empty = Assert.Single(MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-profile-error-pixel")));

        Assert.Equal("MGFX0002", named.Code);
        Assert.Contains("'vs_2_0'", named.Message, StringComparison.Ordinal);
        Assert.Equal("C:/work/obj/Sm2.fx", named.AssetPath);
        Assert.Contains("Invalid profile ''", empty.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_reads_hlsl_errors_and_warnings_with_location()
    {
        var found = MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-hlsl-error"));

        Assert.Equal(["X4576", "X3206"], found.Select(d => d.Code));
        Assert.Equal([BuildDiagnosticSeverity.Error, BuildDiagnosticSeverity.Warning], found.Select(d => d.Severity));
        Assert.Equal((12, 19), (found[0].Line, found[0].Column));
        Assert.Equal((20, 5), (found[1].Line, found[1].Column));
    }

    [Fact]
    public void Parser_finds_nothing_in_a_success_line() =>
        Assert.Empty(MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-success")));

    // ---------- escalator ----------

    [Fact]
    public void Escalator_raises_only_the_directx_branch_one_level_at_a_time()
    {
        var text = Sm2Rewritten();

        var first = ProfileEscalator.Escalate(text);
        var second = ProfileEscalator.Escalate(first!.Text);
        var third = ProfileEscalator.Escalate(second!.Text);

        Assert.Equal("_4_0_level_9_3", first.Level);
        Assert.False(first.NeedsHiDef);
        Assert.Contains("#define PS_SHADERMODEL ps_4_0_level_9_3", first.Text, StringComparison.Ordinal);
        Assert.Contains("#define PS_SHADERMODEL ps_2_0", first.Text, StringComparison.Ordinal);
        Assert.Equal("_4_0", second.Level);
        Assert.True(second.NeedsHiDef);
        Assert.Contains("#define VS_SHADERMODEL vs_4_0\n", second.Text, StringComparison.Ordinal);
        Assert.Null(third);
    }

    [Fact]
    public void Escalator_does_nothing_without_the_macro_block()
    {
        Assert.Null(ProfileEscalator.Escalate("technique T { pass P { PixelShader = compile ps_4_0_level_9_1 F(); } }"));
        Assert.Null(ProfileEscalator.Escalate(File.ReadAllText(Path.Combine(FixturePaths.Of("xna40-effects"), "Content", "Sm2.fx"))));
    }

    [Fact]
    public void Escalator_keeps_crlf()
    {
        var result = ProfileEscalator.Escalate(Sm2Rewritten().Replace("\n", "\r\n"));

        Assert.NotNull(result);
        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty), StringComparison.Ordinal);
    }

    // ---------- build check ----------

    [Fact]
    public async Task Passing_effect_is_reported_with_the_right_command()
    {
        var path = WriteEffect("a.fx", Sm2Rewritten());
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok(RunnerTestPaths.ReadSample("mgfxc-success")));

        var results = await new EffectBuildCheck(isWindows: true).RunAsync(_root, EffectCompilerProfile.OpenGL, [path], runner, CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal(EffectCheckStatus.Passed, result.Status);
        Assert.Equal(0, result.Escalations);
        var request = Assert.Single(runner.Requests);
        Assert.Equal("dotnet", request.FileName);
        Assert.Equal(["tool", "run", "mgfxc", "--", path, Path.Combine(_root, "obj", "xnareborn-fx", "a.mgfxo"), "/Profile:OpenGL"], request.Arguments);
        Assert.Equal(_root, request.WorkingDirectory);
        Assert.True(Directory.Exists(Path.Combine(_root, "obj", "xnareborn-fx")));
    }

    [Fact]
    public async Task Failing_effect_keeps_its_diagnostics_and_gets_a_hint()
    {
        var path = WriteEffect("a.fx", "x");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, RunnerTestPaths.ReadSample("mgfxc-parse-error")));

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Failed, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("H304", new HintMatcher().Match(diagnostic)?.Id);
    }

    [Fact]
    public async Task Limit_error_raises_the_profile_twice_and_warns_about_HiDef()
    {
        var path = WriteEffect("a.fx", Sm2Rewritten());
        var runner = new FakeProcessRunner(request =>
        {
            var text = File.ReadAllText(request.Arguments[4]);
            return text.Contains("_level_9_", StringComparison.Ordinal)
                ? FakeProcessRunner.Exit(1, "probe/a.fx(3,1): " + _limitMessage + "\nFailed to compile 'probe/a.fx'!")
                : FakeProcessRunner.Ok("Compiled.");
        });

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.DirectX11, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Passed, result.Status);
        Assert.Equal(2, result.Escalations);
        Assert.Equal(3, runner.Requests.Count);
        var warning = Assert.Single(result.Diagnostics);
        Assert.Equal("MGFX0003", warning.Code);
        Assert.Equal(BuildDiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("#define PS_SHADERMODEL ps_4_0\n", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Limit_error_without_the_macro_block_is_not_escalated()
    {
        var path = WriteEffect("a.fx", "technique T { pass P { PixelShader = compile ps_4_0_level_9_1 F(); } }");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, "probe/a.fx(3,1): " + _limitMessage));

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.DirectX11, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Failed, result.Status);
        Assert.Equal(0, result.Escalations);
        Assert.Single(runner.Requests);
        Assert.Equal("H303", new HintMatcher().Match(result.Diagnostics[0])?.Id);
    }

    [Fact]
    public async Task Limit_error_on_OpenGL_is_not_escalated()
    {
        var path = WriteEffect("a.fx", Sm2Rewritten());
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, "probe/a.fx(3,1): " + _limitMessage));

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Failed, result.Status);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task Missing_tool_skips_this_and_the_remaining_effects()
    {
        var a = WriteEffect("a.fx", "x");
        var b = WriteEffect("b.fx", "x");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exit(1, "Cannot find a tool in the manifest file that has a command named 'mgfxc'. Run \"dotnet tool restore\"."));

        var results = await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [a, b], runner, CancellationToken.None);

        Assert.Equal([EffectCheckStatus.Skipped, EffectCheckStatus.Skipped], results.Select(r => r.Status));
        Assert.Equal([a, b], results.Select(r => r.Path));
        Assert.Contains("tool", results[1].SkipReason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task Start_failure_is_a_skip_not_a_crash()
    {
        var path = WriteEffect("a.fx", "x");
        var runner = new FakeProcessRunner(_ => new ProcessResult(null, false, false, string.Empty, string.Empty, TimeSpan.Zero, "dotnet was not found"));

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Skipped, result.Status);
        Assert.Equal("dotnet was not found", result.SkipReason);
    }

    [Fact]
    public async Task No_effects_means_no_results_and_no_process()
    {
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var results = await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [], runner, CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task Folder_that_is_not_a_migrated_copy_is_refused()
    {
        var other = Path.Combine(_root, "plain");
        Directory.CreateDirectory(other);
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = Assert.Single(await new EffectBuildCheck(true).RunAsync(other, EffectCompilerProfile.OpenGL, [Path.Combine(other, "a.fx")], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Skipped, result.Status);
        Assert.Contains("migrated copy", result.SkipReason, StringComparison.Ordinal);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task DirectX_is_skipped_when_the_system_is_not_windows()
    {
        var path = WriteEffect("a.fx", "x");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var result = Assert.Single(await new EffectBuildCheck(isWindows: false).RunAsync(_root, EffectCompilerProfile.DirectX11, [path], runner, CancellationToken.None));

        Assert.Equal(EffectCheckStatus.Skipped, result.Status);
        Assert.Contains("Windows", result.SkipReason, StringComparison.Ordinal);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task Results_come_back_in_input_order()
    {
        var paths = new[] { WriteEffect("z.fx", "x"), WriteEffect("a.fx", "x"), WriteEffect("m.fx", "x") };
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Ok());

        var results = await new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, paths, runner, CancellationToken.None);

        Assert.Equal(paths, results.Select(r => r.Path));
    }

    [Fact]
    public async Task Cancelled_token_stops_the_check()
    {
        var path = WriteEffect("a.fx", "x");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new EffectBuildCheck(true).RunAsync(_root, EffectCompilerProfile.OpenGL, [path], new FakeProcessRunner(_ => FakeProcessRunner.Ok()), cts.Token));
    }

    // ---------- hints ----------

    public static readonly TheoryData<string, string, string, string, string> HintCases = new()
    {
        { "H301", "MGFX0002", "Invalid profile ''. Pixel shader 'MainPS' must be SM 4.0 level 9.1 or higher!", "MGFX0002", "Invalid profile 'ps_4_0'. Pixel shader 'P' must be SM 3.0 or lower!" },
        { "H302", "MGFX0002", "Invalid profile 'ps_4_0'. Pixel shader 'P' must be SM 3.0 or lower!", "MGFX0002", "Invalid profile 'x'. Pixel shader 'P' must be SM 4.0 level 9.1 or higher!" },
        { "H303", "X5608", "Compiled shader code uses too many arithmetic instruction slots (72). Max. allowed is 64.", "X5608", "syntax error: unexpected token" },
        { "H304", "MGFX0001", "Unexpected token 'A' found. Expected CloseBracket", "MGFX0003", "The shader needed profile _4_0." },
        { "H305", "X4576", "Input signature mismatch", "X4577", "Input signature mismatch" },
        { "H307", "X4502", "invalid ps_2_0 input semantic 'VPOS'", "X4502", "invalid ps_2_0 input semantic 'COLOR3'" },
        { "H308", "X4505", "anything", "X4506", "anything" },
        { "H309", "X5426", "anything", "X5427", "anything" },
        { "H306", "MGCB", "Could not run ffprobe: no such file", "MGCB", "Could not find file hero.png" },
    };

    [Theory]
    [MemberData(nameof(HintCases))]
    public void Each_effect_hint_matches_its_text_and_not_a_near_miss(string id, string code, string message, string missCode, string missMessage)
    {
        var matcher = new HintMatcher();

        Assert.Equal(id, matcher.Match(Error(code, message))?.Id);
        Assert.NotEqual(id, matcher.Match(Error(missCode, missMessage))?.Id);
    }

    [Fact]
    public void Empty_profile_name_message_matches_the_too_low_hint()
    {
        var parsed = Assert.Single(MgfxcOutputParser.Parse(RunnerTestPaths.ReadSample("mgfxc-profile-error-pixel")));

        Assert.Equal("H301", new HintMatcher().Match(parsed)?.Id);
    }

    [Fact]
    public void Effect_hints_are_unique_and_have_text()
    {
        var entries = HintCatalog.Default.Entries.Where(e => e.Hint.Id.StartsWith("H30", StringComparison.Ordinal)).ToList();

        Assert.Equal(9, entries.Count);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Hint.Advice)));
    }
}
