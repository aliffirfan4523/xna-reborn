using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop tasks 7 to 10: migrating the device fixtures (dry runs, no build).</summary>
[Trait("Category", "Migrator")]
public sealed class DeviceMigrationTests : IDisposable
{
    private const string _install = "Components.Add(new XnaReborn.Compat.Input.DeviceInput(this));";

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-device-" + Guid.NewGuid().ToString("N"));

    public DeviceMigrationTests() => Directory.CreateDirectory(_temp);

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    private async Task<MigrationResult> MigrateAsync(string fixture, params string[] excluded) =>
        await MigrationTestSupport.MigrateAsync(FixturePaths.Of(fixture), MigrationTestSupport.Options(Path.Combine(_temp, "out"), dryRun: true, excluded));

    private static FileChange Change(MigrationResult result, string path) =>
        Assert.Single(result.Changes, c => c.RelativePath == path);

    [Fact]
    public async Task A_phone_project_gets_the_device_library_the_controls_file_and_a_reference()
    {
        var result = await MigrateAsync("device-wp7-xna");

        Assert.All(
            new[] { "XnaReborn.Compat/Device/Accelerometer.cs", "XnaReborn.Compat/Input/DeviceInput.cs", "XnaReborn.Compat/Input/CompatGamePad.cs", "DEVICE-CONTROLS.md" },
            path => Assert.Equal(ChangeKind.Created, Change(result, path).Kind));
        var csproj = Change(result, "Wp7Xna.csproj").NewText!;
        Assert.Contains("<ProjectReference Include=\"XnaReborn.Compat\\XnaReborn.Compat.csproj\" />", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Phone", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Devices", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("WINDOWS_PHONE", csproj, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_phone_game_source_is_folded_redirected_and_given_the_component()
    {
        var result = await MigrateAsync("device-wp7-xna");

        var game = Change(result, "Game1.cs").NewText!;
        Assert.DoesNotContain("#if", game, StringComparison.Ordinal);
        Assert.Contains("graphics.IsFullScreen = true;", game, StringComparison.Ordinal);
        Assert.Contains("XnaReborn.Compat.Input.CompatGamePad.GetState(PlayerIndex.One)", game, StringComparison.Ordinal);
        Assert.Equal(1, game.Split(_install).Length - 1);
        Assert.Equal(["DEVICEINPUT", "XNA053", "XNA062"], result.Manifest.Changes.Single(c => c.Path == "Game1.cs").Rules);
    }

    [Fact]
    public async Task The_manifest_lists_the_device_library_types_that_the_game_uses()
    {
        var result = await MigrateAsync("device-wp7-xna");

        var shims = result.Manifest.Shims;
        Assert.NotNull(shims);
        Assert.Contains("XnaReborn.Compat/Device/Accelerometer.cs", shims.Files);
        Assert.Contains("Wp7Xna.csproj", shims.ReferencedBy);
        var uses = shims.Uses.Where(u => u.File == "Game1.cs").GroupBy(u => u.Namespace).ToDictionary(g => g.Key, g => g.SelectMany(u => u.Types).ToList());
        Assert.Contains("Accelerometer", uses["Microsoft.Devices.Sensors"]);
        Assert.Equal(["PhoneApplicationService"], uses["Microsoft.Phone.Shell"]);
        Assert.Contains("DeviceInput", uses["XnaReborn.Compat.Input"]);
        Assert.Contains("Guide", uses["Microsoft.Xna.Framework.GamerServices"]);
    }

    [Fact]
    public async Task Zune_media_library_use_gets_a_marker_and_the_code_stays()
    {
        var result = await MigrateAsync("device-zune-31");

        var game = Change(result, "Game1.cs").NewText!;
        Assert.Contains("// XNAREBORN: XNA056:", game, StringComparison.Ordinal);
        Assert.Contains("library = new MediaLibrary();", game, StringComparison.Ordinal);
        Assert.Contains("XNA056", result.Manifest.Changes.Single(c => c.Path == "Game1.cs").Rules);
    }

    [Fact]
    public async Task A_windows_project_gets_no_device_library()
    {
        var result = await MigrateAsync("device-windows-plain");

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Changes, c => c.RelativePath == "DEVICE-CONTROLS.md");
        Assert.DoesNotContain("DeviceInput", Change(result, "Game1.cs").NewText ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_copy_solution_migrates_only_the_windows_project_without_the_device_library()
    {
        var result = await MigrateAsync("device-xbox360-copy-solution");

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal));
        Assert.Single(result.Changes, c => c.Kind == ChangeKind.Skipped);
        Assert.Equal(ChangeKind.Created, Change(result, "Game/Game.csproj").Kind);
    }

    [Fact]
    public async Task Migration_is_deterministic()
    {
        var first = await MigrateAsync("device-wp7-xna");
        var second = await MigrateAsync("device-wp7-xna");

        Assert.Equal(first.Changes.Select(c => (c.RelativePath, c.NewText)), second.Changes.Select(c => (c.RelativePath, c.NewText)));
    }
}
