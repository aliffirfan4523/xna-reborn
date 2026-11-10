namespace XnaReborn.App.Controls;

/// <summary>
/// A request to scroll a <see cref="DiffViewer"/> to a line of the new text. It is a class on purpose: each request is a
/// new object, so asking for the same line twice still changes the bound property and scrolls again.
/// </summary>
public sealed class DiffViewerReveal
{
    /// <summary>Creates a request.</summary>
    /// <param name="newLine">1-based line number in the new text.</param>
    public DiffViewerReveal(int newLine)
    {
        NewLine = newLine;
    }

    /// <summary>1-based line number in the new text.</summary>
    public int NewLine { get; }
}
