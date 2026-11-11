using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Effects;

/// <summary>
/// Rule ids used in effect notes and markers: the catalog rules XNA064 to XNA069 (task 17 of the audio-and-shaders spec).
/// </summary>
public static class FxRuleIds
{
    /// <summary>Fixed shader model in a <c>compile</c> statement.</summary>
    public const string Profile = "XNA064";

    /// <summary>Pass state that the MonoGame effect compiler does not accept.</summary>
    public const string PassState = "XNA065";

    /// <summary>Annotation, arguments in <c>compile</c>, or a Direct3D 10 shader call.</summary>
    public const string Syntax = "XNA066";

    /// <summary>A <c>VPOS</c> pixel shader input.</summary>
    public const string Vpos = "XNA067";

    /// <summary>Old sampler state names and values.</summary>
    public const string Sampler = "XNA068";

    /// <summary>Parameter default values and unused parameters.</summary>
    public const string Parameters = "XNA069";
}

/// <summary>The result of rewriting one effect file.</summary>
/// <param name="Text">The new text. Equal to the input when nothing changed.</param>
/// <param name="Notes">What was changed or marked, in a fixed order.</param>
public sealed record FxRewriteResult(string Text, IReadOnlyList<RewriteNote> Notes)
{
    /// <summary>A result for a file that was left alone.</summary>
    /// <param name="text">The unchanged text.</param>
    public static FxRewriteResult Unchanged(string text) => new(text, []);
}

/// <summary>A text edit against the original text of an effect file.</summary>
/// <param name="Start">Offset of the first replaced character.</param>
/// <param name="Length">Number of replaced characters (0 for an insertion).</param>
/// <param name="Replacement">The new text.</param>
internal sealed record FxEdit(int Start, int Length, string Replacement);

/// <summary>Applies position-based edits.</summary>
internal static class FxEdits
{
    /// <summary>Applies the edits from the end of the text to the start, so earlier positions stay valid. Overlapping edits are not allowed.</summary>
    /// <param name="text">The original text.</param>
    /// <param name="edits">The edits, in any order.</param>
    public static string Apply(string text, IEnumerable<FxEdit> edits)
    {
        var result = text;
        var last = int.MaxValue;
        foreach (var edit in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Length))
        {
            if (edit.Start + edit.Length > last)
            {
                throw new InvalidOperationException("Effect edits overlap.");
            }

            result = result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Replacement);
            last = edit.Start;
        }

        return result;
    }
}
