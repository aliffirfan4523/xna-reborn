using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests;

/// <summary>
/// Writes a PNG of every screen in light and dark. Does nothing unless <c>XNAREBORN_SCREENSHOTS</c> names an output folder.
/// <c>XNAREBORN_SCREENSHOT_PREFIX</c> (default "after") starts each file name. Used to refresh docs/ui/.
/// </summary>
public sealed class ScreenshotTests
{
    [AvaloniaFact]
    public async Task Capture_every_screen_in_light_and_dark()
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
            foreach (var step in Enum.GetValues<WorkflowStep>())
            {
                using var t = TestServices.Create();
                var window = new MainWindow { DataContext = t.Main, Width = 1180, Height = 760 };
                window.Show();
                await t.OpenPageAsync(step);
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"{prefix}-{name}-{(int)step + 1}-{step}.png"), new PngBitmapEncoderOptions());
                window.Close();
            }

            // Settings is not a workflow step, so it has its own capture. The file number follows the six steps.
            using var settings = TestServices.Create();
            var settingsWindow = new MainWindow { DataContext = settings.Main, Width = 1180, Height = 1760 };
            settingsWindow.Show();
            await settings.Main.OpenSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            settingsWindow.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"{prefix}-{name}-7-Settings.png"), new PngBitmapEncoderOptions());
            settingsWindow.Close();
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
    }
}
