using XnaReborn.Core.Content;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Effects;

/// <summary>
/// Fixes sampler state names and filter values the MonoGame effect compiler rejects: <c>MipMapLodBias</c> becomes <c>MipLodBias</c>,
/// and the <c>GaussianQuad</c> and <c>PyramidalQuad</c> filters become <c>Linear</c>. Only text inside <c>sampler_state</c> blocks changes.
/// A sampler state that is not on the allow list is left as written and reported.
/// </summary>
public static class FxSamplerRewriter
{
    private static readonly string[] _filterKeys = ["MinFilter", "MagFilter", "MipFilter", "Filter"];
    private static readonly string[] _oldFilters = ["GaussianQuad", "PyramidalQuad"];

    /// <summary>Rewrites the sampler states of one effect.</summary>
    /// <param name="text">The effect text.</param>
    /// <param name="profiles">The profile data. Defaults to <see cref="EffectProfiles.Default"/>.</param>
    public static FxRewriteResult Rewrite(string text, EffectProfiles? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        profiles ??= EffectProfiles.Default;
        var document = FxScanner.Scan(text);
        var edits = new List<FxEdit>();
        var notes = new List<RewriteNote>();
        foreach (var state in document.SamplerStates)
        {
            if (state.Key.Equals("MipMapLodBias", StringComparison.OrdinalIgnoreCase))
            {
                edits.Add(new FxEdit(state.KeyStart, state.Key.Length, "MipLodBias"));
                notes.Add(new RewriteNote(FxRuleIds.Sampler, $"Line {state.Line}: sampler state 'MipMapLodBias' was renamed to 'MipLodBias'.", false));
            }
            else if (_filterKeys.Contains(state.Key, StringComparer.OrdinalIgnoreCase) && _oldFilters.Contains(state.Value, StringComparer.OrdinalIgnoreCase))
            {
                edits.Add(new FxEdit(state.ValueStart, state.ValueLength, "Linear"));
                notes.Add(new RewriteNote(FxRuleIds.Sampler, $"Line {state.Line}: filter '{state.Value}' does not exist in MonoGame, so it was replaced by 'Linear'. Check the look.", false));
            }
        }

        return edits.Count == 0 ? FxRewriteResult.Unchanged(text) : new FxRewriteResult(FxEdits.Apply(text, edits), notes);
    }

    /// <summary>Lists the sampler states that neither the allow list nor <see cref="Rewrite"/> covers.</summary>
    /// <param name="document">The scanned effect.</param>
    /// <param name="profiles">The profile data.</param>
    internal static IEnumerable<FxStatement> UnknownStates(FxDocument document, EffectProfiles profiles) =>
        document.SamplerStates.Where(s =>
            !profiles.SamplerStates.Contains(s.Key) && !s.Key.Equals("MipMapLodBias", StringComparison.OrdinalIgnoreCase));
}
