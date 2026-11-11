using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using XnaReborn.App.Controls;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Views;

/// <summary>The "Build and run" page: environment, live build log, grouped diagnostics, and the game controls.</summary>
public sealed partial class BuildRunView : UserControl
{
    /// <summary>Creates the view.</summary>
    public BuildRunView()
    {
        InitializeComponent();
        // Below these widths the panels stack: three columns of ~160 px or a 200 px problems column wrap word by word.
        NarrowLayout.Attach(Pipeline, 640, "*,*,*");
        NarrowLayout.Attach(LogAndProblems, 700, "3*,2*");
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

    private async void OnCopyLog(object? sender, RoutedEventArgs e)
    {
        // The text is exactly the lines now shown, so a filter narrows what is copied.
        if (DataContext is not BuildRunViewModel vm)
        {
            return;
        }

        var note = this.FindControl<TextBlock>("CopyNote")!;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard ?? throw new InvalidOperationException("No clipboard.");
            await clipboard.SetTextAsync(string.Join(System.Environment.NewLine, vm.VisibleLogLines.Select(l => l.Text)));
            note.Text = $"Copied {vm.VisibleLogLines.Count} lines.";
        }
        catch (Exception)
        {
            note.Text = "The log could not be copied.";
        }

        note.IsVisible = true;
    }
}
