using System.Text.RegularExpressions;
using XnaReborn.Core.Content;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Effects;

/// <summary>
/// Adds a one-line comment above each construct the MonoGame effect compiler rejects and that cannot be fixed safely by text edits:
/// unsupported pass states, annotations, arguments in <c>compile</c>, Direct3D 10 shader calls, unknown sampler states and <c>VPOS</c>.
/// Also reports parameter defaults and unused parameters as notes. A marker is never inserted twice.
/// </summary>
public static partial class FxMarkers
{
    private const string _prefix = "// XNA Reborn [";

    [GeneratedRegex(@"^[ \t]*", RegexOptions.CultureInvariant)]
    private static partial Regex Indent();

    /// <summary>Marks one effect.</summary>
    /// <param name="text">The effect text.</param>
    /// <param name="profiles">The profile data. Defaults to <see cref="EffectProfiles.Default"/>.</param>
    public static FxRewriteResult Mark(string text, EffectProfiles? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        profiles ??= EffectProfiles.Default;
        var document = FxScanner.Scan(text);
        var spots = new List<(int LineStart, int Line, string RuleId, string Message)>();
        var notes = new List<RewriteNote>();

        foreach (var state in document.PassStates.Where(s => !profiles.PassStates.Contains(s.Key)))
        {
            spots.Add((state.LineStart, state.Line, FxRuleIds.PassState, PassStateMessage(state.Key)));
        }

        foreach (var call in document.SetShaderCalls)
        {
            spots.Add((call.LineStart, call.Line, FxRuleIds.Syntax, $"{call.Text} is Direct3D 10 syntax and is not supported by the MonoGame effect compiler. Use 'VertexShader = compile <model> <function>();' or 'PixelShader = compile <model> <function>();'."));
        }

        foreach (var annotation in document.Annotations)
        {
            spots.Add((annotation.LineStart, annotation.Line, FxRuleIds.Syntax, "Annotations (< ... >) are not supported by the MonoGame effect compiler. Remove the annotation block."));
        }

        foreach (var compile in document.Compiles.Where(c => c.HasArguments))
        {
            spots.Add((compile.LineStart, compile.Line, FxRuleIds.Syntax, $"Arguments in a compile statement are not supported by the MonoGame effect compiler. Remove the arguments from the call to {compile.Function} and move them into the function."));
        }

        foreach (var state in FxSamplerRewriter.UnknownStates(document, profiles))
        {
            spots.Add((state.LineStart, state.Line, FxRuleIds.Sampler, $"Sampler state '{state.Key}' is not supported by the MonoGame effect compiler. Remove it or set the state in C#."));
        }

        foreach (var spot in document.Vpos)
        {
            spots.Add((spot.LineStart, spot.Line, FxRuleIds.Vpos, "VPOS is not available. Use SV_Position as a pixel shader input on DirectX and compute the screen position from the vertex shader output on OpenGL."));
        }

        foreach (var global in document.Globals)
        {
            if (!global.IsUsed)
            {
                notes.Add(new RewriteNote(FxRuleIds.Parameters, $"Line {global.Line}: parameter '{global.Name}' is never used. The MonoGame effect compiler removes unused parameters, so setting it from code will fail.", false));
            }
            else if (global.HasDefault)
            {
                notes.Add(new RewriteNote(FxRuleIds.Parameters, $"Line {global.Line}: parameter '{global.Name}' has a default value, which OpenGL targets ignore. Set it from code.", false));
            }
        }

        var edits = new List<FxEdit>();
        foreach (var group in spots.OrderBy(s => s.LineStart).ThenBy(s => s.RuleId, StringComparer.Ordinal).GroupBy(s => s.LineStart))
        {
            var first = group.First();
            var indent = Indent().Match(LineAt(text, first.LineStart)).Value;
            var insert = new System.Text.StringBuilder();
            foreach (var (_, line, ruleId, message) in group.DistinctBy(g => (g.RuleId, g.Message)))
            {
                var comment = $"{_prefix}{ruleId}]: {message}";
                if (HasMarkerAbove(text, first.LineStart, comment))
                {
                    continue;
                }

                insert.Append(indent).Append(comment).Append(document.NewLine);
                notes.Add(new RewriteNote(ruleId, $"Line {line}: {message}", true));
            }

            if (insert.Length > 0)
            {
                edits.Add(new FxEdit(first.LineStart, 0, insert.ToString()));
            }
        }

        return edits.Count == 0 && notes.Count == 0 ? FxRewriteResult.Unchanged(text) : new FxRewriteResult(FxEdits.Apply(text, edits), notes);
    }

    private static string PassStateMessage(string state) =>
        state.StartsWith("Alpha", StringComparison.OrdinalIgnoreCase) && !state.Equals("AlphaBlendEnable", StringComparison.OrdinalIgnoreCase)
            ? $"{state} is not supported by the MonoGame effect compiler. Set alpha testing in C# (AlphaTestEffect) or discard in the pixel shader."
            : $"{state} is not supported by the MonoGame effect compiler. Set it in C# with the matching render state object.";

    private static string LineAt(string text, int lineStart)
    {
        var end = text.IndexOf('\n', lineStart);
        return end < 0 ? text[lineStart..] : text[lineStart..end];
    }

    // True when the contiguous marker comments just above the line already contain this exact comment.
    private static bool HasMarkerAbove(string text, int lineStart, string comment)
    {
        var position = lineStart;
        while (position > 0)
        {
            var previousEnd = position - 1;
            var previousStart = FxScanner.LineStart(text, previousEnd);
            var previous = text[previousStart..previousEnd].Trim();
            if (!previous.StartsWith(_prefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (previous == comment)
            {
                return true;
            }

            position = previousStart;
        }

        return false;
    }
}
