namespace XnaReborn.Migrator.Content;

/// <summary>Processor parameters ready for the MGCB file.</summary>
/// <param name="Params">Parameters MonoGame accepts, sorted by name (ordinal). Values are unchanged.</param>
/// <param name="Dropped">One text per parameter left out, with the reason.</param>
public sealed record MappedParams(IReadOnlyList<KeyValuePair<string, string>> Params, IReadOnlyList<string> Dropped);

/// <summary>
/// Maps XNA processor parameters to MonoGame ones. The stock processors keep their parameter names. The names were checked
/// against the MonoGame processor sources:
/// <see href="https://github.com/MonoGame/MonoGame/tree/v3.8.4/MonoGame.Framework.Content.Pipeline/Processors"/>
/// and the parameter list in the
/// <see href="https://docs.monogame.net/articles/getting_to_know/whatis/content_pipeline/CP_StdImpsProcs.html">standard processor docs</see>.
/// </summary>
/// <remarks>
/// MonoGame's TextureProcessor takes ColorKeyColor, ColorKeyEnabled, GenerateMipmaps, PremultiplyAlpha, ResizeToPowerOfTwo,
/// MakeSquare and TextureFormat. FontDescriptionProcessor takes PremultiplyAlpha and TextureFormat. SoundEffectProcessor and
/// SongProcessor take Quality. ModelProcessor takes ColorKeyColor, ColorKeyEnabled, DefaultEffect, GenerateMipmaps,
/// GenerateTangentFrames, PremultiplyTextureAlpha, PremultiplyVertexColors, ResizeTexturesToPowerOfTwo, RotationX/Y/Z,
/// Scale, SwapWindingOrder and TextureFormat. EffectProcessor takes DebugMode and Defines.
/// Those names pass through unchanged, and so does any name not listed here. Only the entries in the table below are dropped,
/// because MonoGame's processor has no such property and the MGCB builder would report an unknown parameter.
/// </remarks>
public static class ProcessorParamMapper
{
    private static readonly Dictionary<(string Processor, string Param), string> _dropped = new(new DropComparer())
    {
        [("FontDescriptionProcessor", "DefaultCharacter")] =
            "MonoGame's FontDescriptionProcessor has no such parameter. Set <DefaultCharacter> inside the .spritefont file instead.",
        [("EffectProcessor", "DefaultEffect")] =
            "MonoGame's EffectProcessor only has DebugMode and Defines. DefaultEffect belongs to ModelProcessor.",
    };

    /// <summary>Maps the parameters of one content item.</summary>
    /// <param name="importer">The XNA importer name, or null. It does not change the result.</param>
    /// <param name="processor">The XNA processor name, or null. Names compare without regard to case.</param>
    /// <param name="xnaParams">Parameters by name, as read from the content project.</param>
    /// <returns>The kept parameters in ordinal order by name, and a reason for each dropped one.</returns>
    public static MappedParams Map(string? importer, string? processor, IReadOnlyDictionary<string, string> xnaParams)
    {
        ArgumentNullException.ThrowIfNull(xnaParams);

        var kept = new List<KeyValuePair<string, string>>();
        var dropped = new List<string>();
        foreach (var pair in xnaParams.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (processor is not null && _dropped.TryGetValue((processor, pair.Key), out var reason))
            {
                dropped.Add($"{pair.Key}: {reason}");
            }
            else
            {
                kept.Add(pair);
            }
        }

        return new MappedParams(kept, dropped);
    }

    private sealed class DropComparer : IEqualityComparer<(string Processor, string Param)>
    {
        public bool Equals((string Processor, string Param) x, (string Processor, string Param) y) =>
            string.Equals(x.Processor, y.Processor, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Param, y.Param, StringComparison.Ordinal);

        public int GetHashCode((string Processor, string Param) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Processor), StringComparer.Ordinal.GetHashCode(obj.Param));
    }
}
