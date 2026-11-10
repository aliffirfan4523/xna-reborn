namespace XnaReborn.Migrator.Content;

/// <summary>One asset that the MonoGame content builder compiles into an .xnb file.</summary>
/// <param name="SourcePath">Source file, relative to the folder of <c>Content.mgcb</c>, with '/' separators.</param>
/// <param name="DestinationPath">
/// Output path relative to the content output folder, with the source extension (mgcb replaces the last extension with
/// <c>.xnb</c>), or null when the output name follows the source path.
/// </param>
/// <param name="Importer">Importer name. Empty lets mgcb choose the importer from the file extension.</param>
/// <param name="Processor">Processor name. Empty lets mgcb choose the importer's default processor.</param>
/// <param name="Params">Processor parameters in the order they are written.</param>
public sealed record MgcbEntry(
    string SourcePath,
    string? DestinationPath,
    string Importer,
    string Processor,
    IReadOnlyList<KeyValuePair<string, string>> Params);

/// <summary>Everything that goes into one <c>Content.mgcb</c> file.</summary>
/// <param name="Target">The MonoGame platform package. It decides the <c>/platform:</c> value.</param>
/// <param name="Profile">Graphics profile, <c>Reach</c> or <c>HiDef</c>. See <see cref="MgcbWriter.MapProfile"/>.</param>
/// <param name="Compress">Value of <c>/compress:</c>.</param>
/// <param name="References">Pipeline assemblies, as paths relative to the folder of <c>Content.mgcb</c>. Written in the given order.</param>
/// <param name="Entries">Assets to build. The writer sorts them by output name.</param>
/// <param name="CopyPaths">Files that bypass the pipeline and are copied as they are. The writer sorts them.</param>
public sealed record MgcbDocument(
    MonoGameTarget Target,
    string Profile,
    bool Compress,
    IReadOnlyList<string> References,
    IReadOnlyList<MgcbEntry> Entries,
    IReadOnlyList<string> CopyPaths);
