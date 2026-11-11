using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Diff;

/// <summary>
/// Writes the Diff screen with sample changes and a marker selected, in light and dark. Does nothing unless
/// <c>XNAREBORN_SCREENSHOTS</c> names an output folder. Overwrites <c>{prefix}-{theme}-4-Diff.png</c> of the generic capture.
/// </summary>
public sealed class DiffScreenshotTests
{
    [AvaloniaFact]
    public async Task Capture_the_diff_screen_with_sample_changes()
    {
        var folder = Environment.GetEnvironmentVariable("XNAREBORN_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var prefix = Environment.GetEnvironmentVariable("XNAREBORN_SCREENSHOT_PREFIX") ?? "after";
        foreach (var (variant, name) in new[] { (ThemeVariant.Light, "light"), (ThemeVariant.Dark, "dark") })
        {
            Application.Current!.RequestedThemeVariant = variant;
            using var t = TestServices.Create();
            var window = new MainWindow { DataContext = t.Main, Width = 1180, Height = 760 };
            window.Show();
            var vm = await DiffTestData.OpenMigrationAsync(t);
            vm.NextMarkerCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"{prefix}-{name}-4-Diff.png"), new PngBitmapEncoderOptions());
            window.Close();
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
    }
}
