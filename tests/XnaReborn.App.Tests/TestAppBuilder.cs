using Avalonia;
using Avalonia.Headless;
using XnaReborn.App;
using XnaReborn.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace XnaReborn.App.Tests;

/// <summary>Starts the real <see cref="App"/> on Avalonia's headless platform, so views load with the real theme and styles.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
