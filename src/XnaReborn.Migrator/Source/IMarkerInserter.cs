namespace XnaReborn.Migrator.Source;

/// <summary>Adds a manual-work marker comment above the code that contains a position. One implementation per source language.</summary>
public interface IMarkerInserter
{
    /// <summary>Adds a marker line above the statement or member that contains <paramref name="position"/>.</summary>
    /// <param name="text">Text of one source file.</param>
    /// <param name="position">Character offset into <paramref name="text"/>.</param>
    /// <param name="ruleId">The rule that needs attention.</param>
    /// <param name="message">What the person has to do. Line breaks become spaces.</param>
    /// <param name="preprocessorSymbols">Symbols defined when parsing. Null means none.</param>
    /// <returns>The new text, or the input text when the marker is already there or the position is inside a comment, a string or inactive code.</returns>
    string InsertMarker(string text, int position, string ruleId, string message, IReadOnlyCollection<string>? preprocessorSymbols = null);
}

/// <summary>The C# marker inserter. It calls <see cref="MarkerInserter"/>.</summary>
public sealed class CSharpMarkerInserter : IMarkerInserter
{
    /// <inheritdoc />
    public string InsertMarker(string text, int position, string ruleId, string message, IReadOnlyCollection<string>? preprocessorSymbols = null) =>
        MarkerInserter.InsertMarker(text, position, ruleId, message, preprocessorSymbols);
}

/// <summary>The text that starts a marker line in each language. Code that looks for markers reads <see cref="All"/>.</summary>
public static class MarkerPrefixes
{
    /// <summary>The C# marker prefix.</summary>
    public const string CSharp = "// XNAREBORN:";

    /// <summary>The Visual Basic marker prefix.</summary>
    public const string VisualBasic = "' XNAREBORN:";

    /// <summary>Every marker prefix, without the trailing space.</summary>
    public static IReadOnlyList<string> All { get; } = [CSharp, VisualBasic];

    /// <summary>Tells whether a line holds a marker of any language.</summary>
    /// <param name="line">One line of text.</param>
    /// <returns>True when the line contains a marker prefix.</returns>
    public static bool ContainsMarker(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return All.Any(prefix => line.Contains(prefix, StringComparison.Ordinal));
    }
}
