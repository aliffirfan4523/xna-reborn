using Avalonia.Controls;
using Avalonia.Interactivity;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Views;

/// <summary>The "Build and run" page: environment, live build log, grouped diagnostics, and the game controls.</summary>
public sealed partial class BuildRunView : UserControl
{
    /// <summary>Creates the view.</summary>
    public BuildRunView()
    {
        InitializeComponent();
    }

    private async void OnOpenLink(object? sender, RoutedEventArgs e)
    {
        // The link comes from the runner's own message and must be https. Opening it needs the window's launcher.
        if (DataContext is BuildRunViewModel { EnvironmentLink: { } link }
            && Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            try
            {
                await launcher.LaunchUriAsync(uri);
            }
            catch (Exception)
            {
                // No browser could be started; the address is still shown in the message above.
            }
        }
    }
}
