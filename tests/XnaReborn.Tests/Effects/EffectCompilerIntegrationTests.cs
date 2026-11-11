using System.Diagnostics;
using XnaReborn.Migrator.Effects;
using XnaReborn.Migrator.Projects;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Effects;

/// <summary>
/// Runs the real effect compiler (dotnet-mgfxc, the version the migrator pins) on the xna40-effects fixture before and after the rewrite.
/// Needs the network once, to install the tool, so it only runs with XNAREBORN_INTEGRATION=1. DirectX needs Windows.
/// </summary>
[Trait("Category", "Integration")]
public sealed class EffectCompilerIntegrationTests : IDisposable
{
    private static readonly string _folder = Path.Combine(FixturePaths.Of("xna40-effects"), "Content");
    private static readonly string[] _portableAfterRewrite = ["Sm2", "Sm3", "Template", "Portable", "MipBias", "Gaussian", "WithInclude", "Defaults"];

    private readonly string _work = Path.Combine(Path.GetTempPath(), "xr-mgfxc-" + Guid.NewGuid().ToString("N"));

    public EffectCompilerIntegrationTests() => Directory.CreateDirectory(_work);

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private static string ToolPath { get; } = Path.Combine(Path.GetTempPath(), "xr-mgfxc-tool-" + PackageVersions.Default.MonoGame);

    private static async Task<(bool Ok, string Output)> RunAsync(string file, string profile, string output)
    {
        var exe = Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "mgfxc.exe" : "mgfxc");
        if (!File.Exists(exe))
        {
            using var install = Process.Start(new ProcessStartInfo("dotnet", ["tool", "install", "dotnet-mgfxc", "--version", PackageVersions.Default.MonoGame, "--tool-path", ToolPath])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            await install.WaitForExitAsync();
        }

        using var process = Process.Start(new ProcessStartInfo(exe, [file, output, "/Profile:" + profile])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(file)!,
        })!;
        var text = await process.StandardOutput.ReadToEndAsync() + await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode == 0, text);
    }

    private async Task<(bool Ok, string Output)> CompileAsync(string name, string profile, bool rewrite)
    {
        var dir = Path.Combine(_work, rewrite ? "after" : "before");
        Directory.CreateDirectory(dir);
        File.Copy(Path.Combine(_folder, "Common.fxh"), Path.Combine(dir, "Common.fxh"), overwrite: true);
        var text = File.ReadAllText(Path.Combine(_folder, name + ".fx"));
        var file = Path.Combine(dir, name + ".fx");
        File.WriteAllText(file, rewrite ? EffectRewriteStep.Rewrite(text).Text : text);
        return await RunAsync(file, profile, Path.Combine(dir, name + "-" + profile + ".mgfxo"));
    }

    [IntegrationFact]
    public async Task Rewritten_effects_compile_for_OpenGL_and_DirectX()
    {
        var failures = new List<string>();
        foreach (var name in _portableAfterRewrite)
        {
            foreach (var profile in new[] { "OpenGL", "DirectX_11" })
            {
                var (ok, output) = await CompileAsync(name, profile, rewrite: true);
                if (!ok)
                {
                    failures.Add($"{name} {profile}: {output}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [IntegrationFact]
    public async Task Unrewritten_shader_model_2_effect_fails_on_DirectX_but_not_on_OpenGL()
    {
        var gl = await CompileAsync("Sm2", "OpenGL", rewrite: false);
        var dx = await CompileAsync("Sm2", "DirectX_11", rewrite: false);

        Assert.True(gl.Ok, gl.Output);
        Assert.False(dx.Ok);
        Assert.Contains("must be SM 4.0 level 9.1 or higher", dx.Output, StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Effects_with_unsupported_constructs_still_fail_and_carry_a_marker()
    {
        foreach (var name in new[] { "AlphaTest", "Annotated" })
        {
            var rewritten = EffectRewriteStep.Rewrite(File.ReadAllText(Path.Combine(_folder, name + ".fx"))).Text;
            Assert.Contains("// XNA Reborn [NEW-B", rewritten, StringComparison.Ordinal);

            var (ok, _) = await CompileAsync(name, "OpenGL", rewrite: true);
            Assert.False(ok, name);
        }
    }

    [IntegrationFact]
    public async Task Shader_model_2_a_and_2_b_effects_compile_after_the_rewrite()
    {
        const string text = "float4 P() : COLOR0 { return float4(1, 0, 0, 1); }\nfloat4 V(float4 p : POSITION0) : POSITION0 { return p; }\ntechnique T { pass A { VertexShader = compile vs_2_a V(); PixelShader = compile ps_2_b P(); } }\n";
        var file = Path.Combine(_work, "Sm2b.fx");
        File.WriteAllText(file, EffectRewriteStep.Rewrite(text).Text);

        foreach (var profile in new[] { "OpenGL", "DirectX_11" })
        {
            var (ok, output) = await RunAsync(file, profile, Path.Combine(_work, "Sm2b-" + profile + ".mgfxo"));
            Assert.True(ok, profile + ": " + output);
        }
    }
}
