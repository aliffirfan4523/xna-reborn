namespace XnaReborn.Analyzer.Xact;

/// <summary>One <c>Key = Value;</c> line of a .xap file.</summary>
/// <param name="Key">The text left of the first equals sign, trimmed.</param>
/// <param name="Value">The text right of it, trimmed, without the closing semicolon.</param>
/// <param name="Line">1-based line number.</param>
internal sealed record XapProperty(string Key, string Value, int Line);

/// <summary>A named block of a .xap file with its properties and child blocks, in file order.</summary>
internal sealed class XapBlock
{
    /// <summary>Creates a block.</summary>
    /// <param name="name">The block name, such as <c>Wave Bank</c>.</param>
    /// <param name="line">1-based line of the name.</param>
    public XapBlock(string name, int line)
    {
        Name = name;
        Line = line;
    }

    /// <summary>The block name.</summary>
    public string Name { get; }

    /// <summary>1-based line of the name.</summary>
    public int Line { get; }

    /// <summary>Properties in file order.</summary>
    public List<XapProperty> Properties { get; } = [];

    /// <summary>Child blocks in file order.</summary>
    public List<XapBlock> Children { get; } = [];

    /// <summary>The first property with this key (ignoring case), or null.</summary>
    /// <param name="key">The key.</param>
    public string? Get(string key) =>
        Properties.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>The child blocks with this name (ignoring case).</summary>
    /// <param name="name">The block name.</param>
    public IEnumerable<XapBlock> Blocks(string name) =>
        Children.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The first child block with this name (ignoring case), or null.</summary>
    /// <param name="name">The block name.</param>
    public XapBlock? Block(string name) => Blocks(name).FirstOrDefault();
}
