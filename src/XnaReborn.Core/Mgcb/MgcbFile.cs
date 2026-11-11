using XnaReborn.Core.Models;

namespace XnaReborn.Core.Mgcb;

/// <summary>A global option in a Content.mgcb file, such as <c>/platform:DesktopGL</c> or <c>/compress</c>.</summary>
/// <param name="Name">Option name without the slash, as written.</param>
/// <param name="Value">Text after the first colon, or null for an option without a value.</param>
/// <param name="Line">1-based line number.</param>
public sealed record MgcbSetting(string Name, string? Value, int Line);

/// <summary>A <c>/reference:</c> line: a pipeline assembly to load.</summary>
/// <param name="Path">The path as written.</param>
/// <param name="Line">1-based line number.</param>
public sealed record MgcbReference(string Path, int Line);

/// <summary>A <c>/processorParam:Name=Value</c> line inside an entry.</summary>
/// <param name="Name">Parameter name.</param>
/// <param name="Value">Parameter value, empty when none.</param>
/// <param name="Line">1-based line number.</param>
public sealed record MgcbParameter(string Name, string Value, int Line);

/// <summary>One content item: a <c>/build:</c> or <c>/copy:</c> line with the importer, processor and parameters set for it.</summary>
/// <param name="Source">Source path, as written (the part before any ';').</param>
/// <param name="Destination">Output path after the ';', or null.</param>
/// <param name="IsCopy">True for <c>/copy:</c>.</param>
/// <param name="Importer">Importer name, or null for the default importer.</param>
/// <param name="Processor">Processor name, or null for the default processor.</param>
/// <param name="Parameters">Processor parameters in file order.</param>
/// <param name="Line">1-based line of the <c>#begin</c> line, or of the build line when the entry has no <c>#begin</c>.</param>
public sealed record MgcbEntry(
    string Source,
    string? Destination,
    bool IsCopy,
    string? Importer,
    string? Processor,
    IReadOnlyList<MgcbParameter> Parameters,
    int Line);

/// <summary>A parsed Content.mgcb file. Every part keeps the line it came from.</summary>
/// <param name="GlobalSettings">Options outside any entry, except references, in file order.</param>
/// <param name="References">Pipeline assembly references in file order.</param>
/// <param name="Entries">Content items in file order.</param>
/// <param name="Diagnostics">Lines that could not be read (code ANA010). Parsing continues past them.</param>
public sealed record MgcbFile(
    IReadOnlyList<MgcbSetting> GlobalSettings,
    IReadOnlyList<MgcbReference> References,
    IReadOnlyList<MgcbEntry> Entries,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    /// <summary>Returns the first global setting with this name (ignoring case), or null.</summary>
    /// <param name="name">Option name without the slash.</param>
    public MgcbSetting? Setting(string name) =>
        GlobalSettings.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
