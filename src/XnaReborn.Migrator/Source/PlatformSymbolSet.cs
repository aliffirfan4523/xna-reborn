using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source;

/// <summary>
/// Which conditional compilation platform symbols are true for one migrated project. Every symbol in
/// <see cref="Symbols"/> has a definite value; any other symbol is unknown to the set.
/// </summary>
public sealed class PlatformSymbolSet
{
    /// <summary>The platform symbols a set decides. Other symbols, such as DEBUG, are never decided.</summary>
    public static readonly IReadOnlyList<string> Symbols = ["WINDOWS", "XBOX", "XBOX360", "ZUNE", "WINDOWS_PHONE"];

    private readonly HashSet<string> _true;

    private PlatformSymbolSet(HashSet<string> trueSymbols) => _true = trueSymbols;

    /// <summary>Gets the symbols that are true, in <see cref="Symbols"/> order.</summary>
    public IEnumerable<string> TrueSymbols => Symbols.Where(_true.Contains);

    /// <summary>Picks the set for the platform of the project that is migrated.</summary>
    /// <param name="platform">The platform of the migrated project.</param>
    /// <returns>
    /// The set, or null for <see cref="XnaPlatform.Unknown"/>: with no known platform no symbol can be decided.
    /// A Windows project (or the Windows sibling of a platform copy) makes only WINDOWS true. A device project that
    /// is migrated alone makes only its own symbols true, and WINDOWS false.
    /// </returns>
    public static PlatformSymbolSet? For(XnaPlatform platform) => platform switch
    {
        XnaPlatform.Windows => new PlatformSymbolSet(["WINDOWS"]),
        XnaPlatform.Xbox360 => new PlatformSymbolSet(["XBOX", "XBOX360"]),
        XnaPlatform.Zune => new PlatformSymbolSet(["ZUNE"]),
        XnaPlatform.WindowsPhone => new PlatformSymbolSet(["WINDOWS_PHONE"]),
        _ => null,
    };

    /// <summary>Tells whether a platform symbol is true in this set.</summary>
    /// <param name="symbol">The symbol, compared with case.</param>
    /// <returns>True or false for a platform symbol, or null for any other symbol.</returns>
    public bool? Value(string symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return Symbols.Contains(symbol, StringComparer.Ordinal) ? _true.Contains(symbol) : null;
    }
}
