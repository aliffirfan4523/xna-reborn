using System.Text;
using VerifyXunit;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Content;
using XnaReborn.Migrator.Effects;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Safety;

namespace XnaReborn.Tests.Effects;

/// <summary>Scanner, profile rewriter, sampler rewriter, markers and include collector on the xna40-effects fixture.</summary>
[Trait("Category", "Effects")]
public sealed class EffectRewriterTests
{
    private static readonly string _folder = Path.Combine(FixturePaths.Of("xna40-effects"), "Content");

    private static string Fx(string name) => File.ReadAllText(Path.Combine(_folder, name + ".fx"));

    // ---------- scanner ----------

    [Fact]
    public void Scanner_finds_techniques_passes_and_compiles()
    {
        var doc = FxScanner.Scan(Fx("Sm2"));

        Assert.Equal(1, doc.TechniqueCount);
        Assert.Equal(1, doc.PassCount);
        Assert.Equal(["vs_2_0", "ps_2_0"], doc.Compiles.Select(c => c.Model));
        Assert.Equal(["MainVS", "MainPS"], doc.Compiles.Select(c => c.Function));
        Assert.All(doc.Compiles, c => Assert.False(c.HasArguments));
    }

    [Fact]
    public void Scanner_reports_several_passes()
    {
        const string text = "technique T { pass A { PixelShader = compile ps_2_0 F(); } pass B { PixelShader = compile ps_3_0 G(); } }";

        var doc = FxScanner.Scan(text);

        Assert.Equal(1, doc.TechniqueCount);
        Assert.Equal(2, doc.PassCount);
        Assert.Equal(["ps_2_0", "ps_3_0"], doc.Compiles.Select(c => c.Model));
    }

    [Fact]
    public void Scanner_ignores_constructs_inside_comments()
    {
        const string text = "// technique T { pass P { PixelShader = compile ps_2_0 F(); } }\n/* VPOS compile vs_2_0 */\nfloat x;\n";

        var doc = FxScanner.Scan(text);

        Assert.Empty(doc.Compiles);
        Assert.Empty(doc.Vpos);
        Assert.Equal(0, doc.TechniqueCount);
    }

    [Fact]
    public void Scanner_tracks_conditional_depth_of_defines()
    {
        var doc = FxScanner.Scan(Fx("Portable"));

        Assert.Equal([1, 1, 1, 1], doc.Defines.Select(d => d.ConditionalDepth));
        Assert.Equal(0, FxScanner.Scan(Fx("Template")).Defines[0].ConditionalDepth);
    }

    [Fact]
    public void Scanner_reads_sampler_forms_and_states()
    {
        const string text = "texture T;\nsampler2D A = sampler_state { Texture = <T>; MinFilter = Linear; };\nsampler B : register(s1) = sampler_state { Texture = (T); AddressU = Wrap; };\nSamplerState C { Filter = MIN_MAG_MIP_LINEAR; };\n";

        var doc = FxScanner.Scan(text);

        Assert.Equal(["Texture", "MinFilter", "Texture", "AddressU", "Filter"], doc.SamplerStates.Select(s => s.Key));
        Assert.Equal(["<T>", "Linear", "(T)", "Wrap", "MIN_MAG_MIP_LINEAR"], doc.SamplerStates.Select(s => s.Value));
        Assert.Empty(doc.Annotations);
    }

    [Fact]
    public void Scanner_reads_globals_defaults_and_usage()
    {
        var doc = FxScanner.Scan(Fx("Defaults"));

        Assert.Equal(["Color", "Unused", "AlsoUnused"], doc.Globals.Select(g => g.Name));
        Assert.Equal([true, false, false], doc.Globals.Select(g => g.HasDefault));
        Assert.Equal([true, false, false], doc.Globals.Select(g => g.IsUsed));
    }

    [Fact]
    public void Scanner_finds_include_with_line()
    {
        var doc = FxScanner.Scan(Fx("WithInclude"));

        var include = Assert.Single(doc.Includes);
        Assert.Equal("Common.fxh", include.Path);
        Assert.Equal(1, include.Line);
    }

    [Fact]
    public void Scanner_finds_nothing_in_a_file_without_techniques()
    {
        var doc = FxScanner.Scan(File.ReadAllText(Path.Combine(_folder, "Common.fxh")));

        Assert.Equal(0, doc.TechniqueCount);
        Assert.Empty(doc.Compiles);
    }

    // ---------- profile rewriter ----------

    [Fact]
    public async Task Sm2_effect_gets_per_target_macros_and_keeps_the_rest()
    {
        var original = Fx("Sm2");

        var result = FxProfileRewriter.Rewrite(original);

        Assert.Contains("compile VS_SHADERMODEL MainVS()", result.Text, StringComparison.Ordinal);
        Assert.Contains("compile PS_SHADERMODEL MainPS()", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define PS_SHADERMODEL ps_4_0_level_9_1", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define VS_SHADERMODEL vs_2_0", result.Text, StringComparison.Ordinal);
        var withoutBlock = result.Text.Replace("VS_SHADERMODEL", "vs_2_0", StringComparison.Ordinal);
        Assert.Contains("float4x4 WorldViewProjection;", withoutBlock, StringComparison.Ordinal);
        Assert.False(result.Notes[0].NeedsManualWork);
        await Verifier.Verify(result.Text, "txt");
    }

    [Fact]
    public void Sm3_effect_uses_level_9_3_for_directx()
    {
        var result = FxProfileRewriter.Rewrite(Fx("Sm3"));

        Assert.Contains("#define VS_SHADERMODEL vs_3_0", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define VS_SHADERMODEL vs_4_0_level_9_3", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define PS_SHADERMODEL ps_4_0_level_9_3", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_style_unconditional_defines_are_replaced_in_place()
    {
        var result = FxProfileRewriter.Rewrite(Fx("Template"));

        Assert.StartsWith("// XNA Reborn: shader models set per target", result.Text, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(result.Text, @"#define VS_SHADERMODEL vs_2_0"));
        Assert.DoesNotContain("#define VS_SHADERMODEL vs_2_0\n#define PS", result.Text, StringComparison.Ordinal);
        Assert.Contains("compile VS_SHADERMODEL MainVS()", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Already_portable_effect_is_unchanged()
    {
        var original = Fx("Portable");

        var result = FxProfileRewriter.Rewrite(original);

        Assert.Equal(original, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Effect_without_compile_statements_is_unchanged()
    {
        var original = Fx("Vpos").Replace("compile ps_3_0 MainPS();", "compile ps_3_0 MainPS();").Replace("PixelShader = compile ps_3_0 MainPS();", string.Empty);

        Assert.Equal(original, FxProfileRewriter.Rewrite(original).Text);
    }

    [Fact]
    public void Mixed_models_get_one_macro_per_model()
    {
        const string text = "technique T { pass A { VertexShader = compile vs_2_0 V(); PixelShader = compile ps_2_0 P(); } pass B { VertexShader = compile vs_3_0 V3(); PixelShader = compile ps_3_0 P3(); } }\n";

        var result = FxProfileRewriter.Rewrite(text);

        Assert.Contains("compile XR_VS_2_0 V()", result.Text, StringComparison.Ordinal);
        Assert.Contains("compile XR_PS_3_0 P3()", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define XR_VS_3_0 vs_4_0_level_9_3", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define XR_PS_2_0 ps_2_0", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shader_model_2_a_and_2_b_map_to_model_3_on_OpenGL_and_level_9_3_on_DirectX()
    {
        const string text = "technique T { pass A { VertexShader = compile vs_2_a V(); PixelShader = compile ps_2_b P(); } }\n";

        var result = FxProfileRewriter.Rewrite(text);

        Assert.Contains("#define VS_SHADERMODEL vs_3_0", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define PS_SHADERMODEL ps_3_0", result.Text, StringComparison.Ordinal);
        Assert.Contains("#define PS_SHADERMODEL ps_4_0_level_9_3", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("vs_2_a V", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_model_is_left_alone_with_a_manual_note()
    {
        const string text = "technique T { pass A { PixelShader = compile ps_4_4 P(); } }\n";

        var result = FxProfileRewriter.Rewrite(text);

        Assert.Equal(text, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
        Assert.Contains("ps_4_4", note.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Sm2")]
    [InlineData("Sm3")]
    [InlineData("Template")]
    [InlineData("WithInclude")]
    public void Profile_rewrite_is_idempotent(string name)
    {
        var once = FxProfileRewriter.Rewrite(Fx(name));
        var twice = FxProfileRewriter.Rewrite(once.Text);

        Assert.NotEqual(Fx(name), once.Text);
        Assert.Equal(once.Text, twice.Text);
    }

    [Fact]
    public void Profile_rewrite_keeps_crlf_line_endings()
    {
        var original = Fx("Sm2").Replace("\n", "\r\n");

        var result = FxProfileRewriter.Rewrite(original);

        Assert.DoesNotContain("\n", result.Text.Replace("\r\n", string.Empty), StringComparison.Ordinal);
        Assert.Contains("#if OPENGL\r\n", result.Text, StringComparison.Ordinal);
    }

    // ---------- sampler rewriter ----------

    [Fact]
    public void Mip_bias_and_old_filters_are_fixed_inside_sampler_state()
    {
        var bias = FxSamplerRewriter.Rewrite(Fx("MipBias"));
        var gaussian = FxSamplerRewriter.Rewrite(Fx("Gaussian"));

        Assert.Contains("MipLodBias = -1;", bias.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("MipMapLodBias", bias.Text, StringComparison.Ordinal);
        Assert.Contains("MinFilter = Linear;", gaussian.Text, StringComparison.Ordinal);
        Assert.Contains("MagFilter = Linear;", gaussian.Text, StringComparison.Ordinal);
        Assert.Equal(2, gaussian.Notes.Count);
    }

    [Fact]
    public void Plain_sampler_forms_are_untouched()
    {
        foreach (var name in new[] { "Sm2", "Sm3", "Portable" })
        {
            Assert.Equal(Fx(name), FxSamplerRewriter.Rewrite(Fx(name)).Text);
        }
    }

    [Fact]
    public void Old_names_outside_sampler_state_are_not_touched()
    {
        const string text = "float MipMapLodBias = 0;\nfloat GaussianQuad;\n";

        Assert.Equal(text, FxSamplerRewriter.Rewrite(text).Text);
    }

    [Fact]
    public void Sampler_rewrite_is_idempotent()
    {
        var once = FxSamplerRewriter.Rewrite(Fx("Gaussian")).Text;

        Assert.Equal(once, FxSamplerRewriter.Rewrite(once).Text);
    }

    // ---------- markers ----------

    [Fact]
    public void Alpha_test_states_get_a_marker_above_each_line()
    {
        var result = FxMarkers.Mark(Fx("AlphaTest"));

        Assert.Equal(3, result.Notes.Count(n => n.RuleId == FxRuleIds.PassState));
        Assert.True(result.Notes.All(n => n.NeedsManualWork));
        Assert.Contains("        // XNA Reborn [NEW-B2]: AlphaTestEnable is not supported", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[NEW-B2]: AlphaBlendEnable", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[NEW-B2]: SrcBlend", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Annotation_compile_arguments_and_set_shader_calls_are_marked()
    {
        var result = FxMarkers.Mark(Fx("Annotated"));

        Assert.Equal(3, result.Notes.Count(n => n.RuleId == FxRuleIds.Syntax));
        Assert.Contains("Annotations (< ... >) are not supported", result.Text, StringComparison.Ordinal);
        Assert.Contains("Arguments in a compile statement", result.Text, StringComparison.Ordinal);
        Assert.Contains("SetPixelShader is Direct3D 10 syntax", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Vpos_is_marked()
    {
        var result = FxMarkers.Mark(Fx("Vpos"));

        var note = Assert.Single(result.Notes);
        Assert.Equal(FxRuleIds.Vpos, note.RuleId);
        Assert.StartsWith("// XNA Reborn [NEW-B4]: VPOS is not available.", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_and_unused_parameters_are_notes_only()
    {
        var result = FxMarkers.Mark(Fx("Defaults"));

        Assert.Equal(Fx("Defaults"), result.Text);
        Assert.Equal(3, result.Notes.Count);
        Assert.True(result.Notes.All(n => n.RuleId == FxRuleIds.Parameters && !n.NeedsManualWork));
        Assert.Contains(result.Notes, n => n.Message.Contains("'Color' has a default value", StringComparison.Ordinal));
        Assert.Contains(result.Notes, n => n.Message.Contains("'Unused' is never used", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_sampler_state_is_marked_and_known_ones_are_not()
    {
        const string text = "sampler2D S = sampler_state\n{\n    Texture = <T>;\n    SRGBTexture = true;\n    MinFilter = Linear;\n};\n";

        var result = FxMarkers.Mark(text);

        var note = Assert.Single(result.Notes);
        Assert.Equal(FxRuleIds.Sampler, note.RuleId);
        Assert.Contains("    // XNA Reborn [NEW-B5]: Sampler state 'SRGBTexture'", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Marker_is_not_inserted_twice()
    {
        var once = FxMarkers.Mark(Fx("AlphaTest")).Text;
        var twice = FxMarkers.Mark(once);

        Assert.Equal(once, twice.Text);
        Assert.DoesNotContain(twice.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void Clean_effects_get_no_markers()
    {
        foreach (var name in new[] { "Sm2", "Sm3", "Portable", "Template", "MipBias", "Gaussian", "WithInclude" })
        {
            Assert.Equal(Fx(name), FxMarkers.Mark(Fx(name)).Text);
        }
    }

    // ---------- whole step ----------

    [Fact]
    public async Task Whole_step_output_for_every_fixture_matches_snapshot()
    {
        var builder = new StringBuilder();
        foreach (var path in Directory.EnumerateFiles(_folder, "*.fx").Order(StringComparer.Ordinal))
        {
            var result = EffectRewriteStep.Rewrite(File.ReadAllText(path));
            builder.Append("=== ").Append(Path.GetFileName(path)).Append(" ===\n").Append(result.Text).Append('\n');
            foreach (var note in result.Notes)
            {
                builder.Append("# ").Append(note.RuleId).Append(note.NeedsManualWork ? " (manual) " : " ").Append(note.Message).Append('\n');
            }
        }

        await Verifier.Verify(builder.ToString(), "txt");
    }

    [Fact]
    public void Whole_step_is_idempotent_for_every_fixture()
    {
        foreach (var path in Directory.EnumerateFiles(_folder, "*.fx"))
        {
            var once = EffectRewriteStep.Rewrite(File.ReadAllText(path)).Text;

            Assert.Equal(once, EffectRewriteStep.Rewrite(once).Text);
        }
    }

    [Fact]
    public void Rewriting_does_not_change_the_fixture_files()
    {
        var before = FolderState.Capture(FixturePaths.Of("xna40-effects"));

        foreach (var path in Directory.EnumerateFiles(_folder, "*.fx"))
        {
            EffectRewriteStep.Rewrite(File.ReadAllText(path));
        }

        EffectRewriteStep.CollectIncludes(Path.Combine(_folder, "WithInclude.fx"), new ReadOnlyFileSystem(), CancellationToken.None);
        Assert.Equal(before, FolderState.Capture(FixturePaths.Of("xna40-effects")));
    }

    // ---------- includes ----------

    [Fact]
    public void Include_collector_finds_the_fxh_next_to_the_effect()
    {
        var found = EffectRewriteStep.CollectIncludes(Path.Combine(_folder, "WithInclude.fx"), new ReadOnlyFileSystem(), CancellationToken.None);

        Assert.Equal([Path.Combine(_folder, "Common.fxh")], found.Files);
        Assert.Empty(found.Missing);
    }

    [Fact]
    public void Effect_without_includes_has_none()
    {
        var found = EffectRewriteStep.CollectIncludes(Path.Combine(_folder, "Sm2.fx"), new ReadOnlyFileSystem(), CancellationToken.None);

        Assert.Empty(found.Files);
        Assert.Empty(found.Missing);
    }

    [Fact]
    public void Include_collector_cuts_cycles_and_reports_missing_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnareborn-fxinc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        try
        {
            File.WriteAllText(Path.Combine(root, "a.fx"), "#include \"sub/b.fxh\"\n#include \"gone.fxh\"\n");
            File.WriteAllText(Path.Combine(root, "sub", "b.fxh"), "#include \"../a.fx\"\n#include \"c.fxh\"\n");
            File.WriteAllText(Path.Combine(root, "sub", "c.fxh"), "float c;\n");

            var found = EffectRewriteStep.CollectIncludes(Path.Combine(root, "a.fx"), new ReadOnlyFileSystem(), CancellationToken.None);

            Assert.Equal([Path.Combine(root, "sub", "b.fxh"), Path.Combine(root, "sub", "c.fxh")], found.Files);
            var missing = Assert.Single(found.Missing);
            Assert.Contains("gone.fxh", missing, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---------- profile data ----------

    [Fact]
    public void Profile_data_maps_every_model_to_a_directx_level_9_profile()
    {
        var profiles = EffectProfiles.Default;

        Assert.All(profiles.Profiles, p => Assert.Contains("_4_0_level_9_", p.DirectX, StringComparison.Ordinal));
        Assert.True(profiles.TryGet("PS_2_0", out var ps20));
        Assert.Equal("ps_4_0_level_9_1", ps20.DirectX);
        Assert.False(profiles.TryGet("ps_4_4", out _));
        Assert.Equal(["_4_0_level_9_1", "_4_0_level_9_3", "_4_0"], profiles.EscalationLevels);
        Assert.Equal("_4_0", profiles.HiDefFrom);
        Assert.Contains("ZEnable", profiles.PassStates);
        Assert.DoesNotContain("AlphaTestEnable", profiles.PassStates);
        Assert.Contains("MipLodBias", profiles.SamplerStates);
    }

    [Theory]
    [InlineData("""{"version":2}""")]
    [InlineData("""{"version":1,"profiles":[],"escalation":{"levels":["a"],"hiDefFrom":"b"},"passStates":[],"samplerStates":[]}""")]
    [InlineData("""{"version":1,"profiles":[{"model":"x"}],"escalation":{"levels":["a"],"hiDefFrom":"a"},"passStates":[],"samplerStates":[]}""")]
    [InlineData("nope")]
    public void Profile_data_parse_rejects_bad_input(string json) =>
        Assert.Throws<FormatException>(() => EffectProfiles.Parse(json));
}
