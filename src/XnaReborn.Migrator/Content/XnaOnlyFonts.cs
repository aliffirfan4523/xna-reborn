using System.Text.Json;

namespace XnaReborn.Migrator.Content;

/// <summary>One font family that XNA Game Studio 4.0 installed and Windows does not ship.</summary>
/// <param name="Family">The font family name as written in <c>FontName</c>.</param>
/// <param name="Substitute">A font that ships with Windows 10 and 11 and looks close.</param>
/// <param name="Note">Why the substitute was chosen.</param>
public sealed record XnaFontInfo(string Family, string Substitute, string? Note);

/// <summary>The fonts XNA Game Studio 4.0 installed that a clean Windows machine does not have, read from the embedded <c>xna-only-fonts.json</c>.</summary>
public sealed class XnaOnlyFonts
{
    private static readonly string _resourceName = "XnaReborn.Migrator.Content.xna-only-fonts.json";

    private static readonly Lazy<XnaOnlyFonts> _default = new(Load);

    private readonly Dictionary<string, XnaFontInfo> _byFamily;

    private XnaOnlyFonts(int version, IReadOnlyList<string> sources, IReadOnlyList<XnaFontInfo> fonts)
    {
        Version = version;
        Sources = sources;
        Fonts = fonts;
        _byFamily = new Dictionary<string, XnaFontInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var font in fonts)
        {
            _byFamily[font.Family] = font;
        }
    }

    /// <summary>The list shipped with this build.</summary>
    public static XnaOnlyFonts Default => _default.Value;

    /// <summary>Schema version of the data file.</summary>
    public int Version { get; }

    /// <summary>Links the list was confirmed against.</summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>All listed fonts, in file order.</summary>
    public IReadOnlyList<XnaFontInfo> Fonts { get; }

    /// <summary>Parses a font list from JSON text.</summary>
    /// <param name="json">Text in the <c>xna-only-fonts.json</c> format.</param>
    /// <returns>The parsed list.</returns>
    public static XnaOnlyFonts Parse(string json)
    {
        var data = JsonSerializer.Deserialize<FontFile>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("xna-only-fonts.json is empty.");
        return new XnaOnlyFonts(data.Version, data.Sources ?? [], data.Fonts ?? []);
    }

    /// <summary>Looks up a family name, ignoring case and surrounding white space.</summary>
    /// <param name="family">The family name.</param>
    /// <param name="info">The entry when found.</param>
    /// <returns><see langword="true"/> when the family is XNA-only.</returns>
    public bool TryGet(string family, out XnaFontInfo info)
    {
        if (family is not null && _byFamily.TryGetValue(family.Trim(), out var found))
        {
            info = found;
            return true;
        }

        info = null!;
        return false;
    }

    private static XnaOnlyFonts Load()
    {
        using var stream = typeof(XnaOnlyFonts).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException("Embedded resource " + _resourceName + " is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private sealed record FontFile(int Version, List<string>? Sources, List<XnaFontInfo>? Fonts);
}
