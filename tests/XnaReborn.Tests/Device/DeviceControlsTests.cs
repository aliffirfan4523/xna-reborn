using VerifyXunit;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Shims;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop task 9.1: DEVICE-CONTROLS.md and DeviceControls.cs are written from one table.</summary>
[Trait("Category", "Migrator")]
public sealed class DeviceControlsTests
{
    [Fact]
    public async Task Controls_file_matches_snapshot() =>
        await Verifier.Verify(ShimProjectGenerator.GenerateControlsFile().NewText!, "md");

    [Fact]
    public async Task Controls_source_matches_snapshot() =>
        await Verifier.Verify(DeviceControlsModel.Default.ToCSharp(), "txt");

    [Fact]
    public void The_controls_file_is_created_at_the_output_root_with_a_reason()
    {
        var file = ShimProjectGenerator.GenerateControlsFile();

        Assert.Equal("DEVICE-CONTROLS.md", file.RelativePath);
        Assert.Equal(XnaReborn.Migrator.Output.ChangeKind.Created, file.Kind);
        Assert.False(string.IsNullOrWhiteSpace(file.Reason));
        Assert.DoesNotContain('\r', file.NewText!);
    }

    [Fact]
    public void A_changed_constant_changes_both_the_file_and_the_source()
    {
        var changed = DeviceControlsModel.Default with { TiltFull = 0.5f, UseMouseTilt = true };

        var file = ShimProjectGenerator.GenerateControlsFile(changed).NewText!;
        var source = changed.ToCSharp();

        Assert.NotEqual(ShimProjectGenerator.GenerateControlsFile().NewText, file);
        Assert.Contains("| Full tilt on X and Y | 0.5 g |", file, StringComparison.Ordinal);
        Assert.Contains("| Source | mouse offset from the window centre |", file, StringComparison.Ordinal);
        Assert.Contains("public static readonly float TiltFull = 0.5f;", source, StringComparison.Ordinal);
        Assert.Contains("public static readonly bool UseMouseTilt = true;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_changed_key_binding_changes_both_the_file_and_the_source()
    {
        var bindings = DeviceControlsModel.Default.Bindings
            .Select(b => b.Name == "Back" ? b with { Keys = ["Escape", "Back"] } : b)
            .ToList();
        var changed = DeviceControlsModel.Default with { Bindings = bindings };

        Assert.Contains("| Back button (the phone's hardware Back button) | Escape, Back |", ShimProjectGenerator.GenerateControlsFile(changed).NewText!, StringComparison.Ordinal);
        Assert.Contains("public static readonly Keys[] Back = { Keys.Escape, Keys.Back };", changed.ToCSharp(), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_binding_and_value_of_the_table_is_in_the_file()
    {
        var model = DeviceControlsModel.Default;
        var file = model.ToMarkdown();

        Assert.All(model.Bindings, b => Assert.Contains(b.Description + " | " + string.Join(", ", b.Keys), file, StringComparison.Ordinal));
        Assert.Contains("(0.0, 0.0, -1.0) g", file, StringComparison.Ordinal);
        Assert.Contains("| Easing time constant | 0.12 s |", file, StringComparison.Ordinal);
        Assert.Contains("| Time between readings | 20.0 ms |", file, StringComparison.Ordinal);
        Assert.Contains("OpenWebLinks", file, StringComparison.Ordinal);
    }

    [MonoGameFact]
    public void A_changed_table_still_compiles()
    {
        var changed = DeviceControlsModel.Default with { TiltFull = 0.4f, EaseSeconds = 0.3f, OpenWebLinks = true };

        Assert.Empty(DeviceCompat.CompileProblems(changed));
    }
}
