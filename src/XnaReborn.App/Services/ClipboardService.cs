using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace XnaReborn.App.Services;

/// <summary>Real <see cref="IClipboardService"/>: uses the clipboard of the main window.</summary>
public sealed class ClipboardService : IClipboardService
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the service.</summary>
    /// <param name="topLevel">Returns the window whose clipboard is used, or null before it exists.</param>
    public ClipboardService(Func<TopLevel?> topLevel)
    {
        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task SetTextAsync(string text)
    {
        var clipboard = _topLevel()?.Clipboard ?? throw new InvalidOperationException("There is no window to reach the clipboard.");
        await clipboard.SetTextAsync(text);
    }
}
