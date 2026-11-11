using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Controls;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App;

/// <summary>The Avalonia application. Builds the service container and shows the main window.</summary>
public sealed partial class App : Application
{
    private ServiceProvider? _services;

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // No desktop lifetime means a headless test host, which builds its own services and windows.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = AppServices.Build(() => desktop.MainWindow);
            AppearanceApplier.Apply(_services.GetRequiredService<ISettingsService>().Current);
            desktop.MainWindow = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
