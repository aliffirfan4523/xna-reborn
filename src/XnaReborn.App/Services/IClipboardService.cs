namespace XnaReborn.App.Services;

/// <summary>Puts text on the clipboard.</summary>
public interface IClipboardService
{
    /// <summary>Replaces the clipboard content with <paramref name="text"/>.</summary>
    /// <param name="text">The text to copy.</param>
    Task SetTextAsync(string text);
}
