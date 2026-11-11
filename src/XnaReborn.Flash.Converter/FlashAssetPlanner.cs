using XnaReborn.Analyzer.IO;
using XnaReborn.Flash.Analyzer.Reading;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Flash.Converter;

/// <summary>What the converter does with the <c>[Embed]</c> tags of a project.</summary>
/// <param name="Bindings">How each embed field is initialised in the converted code.</param>
/// <param name="Mgcb">The content project text, or null when there is nothing to build.</param>
/// <param name="Changes">Files to copy into the migrated copy (assets and Content.mgcb).</param>
/// <param name="Notes">Plain sentences about embeds that could not be converted.</param>
public sealed record AssetPlan(
    IReadOnlyDictionary<(string File, string Field), EmbedBinding> Bindings,
    string? Mgcb,
    IReadOnlyList<FileChange> Changes,
    IReadOnlyList<string> Notes);

/// <summary>Plans the content for a Flash project: images and sounds go through the MonoGame content builder, data files are copied.</summary>
public static class FlashAssetPlanner
{
    /// <summary>Folder, inside the migrated copy, that holds the content.</summary>
    public const string ContentFolder = "Content";

    /// <summary>Builds the plan. Embeds that are missing, leave the project, or come from a SWF are left out and noted.</summary>
    /// <param name="project">The project.</param>
    /// <param name="fileSystem">Used to check that files exist.</param>
    public static AssetPlan Plan(FlashProject project, IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileSystem);
        var bindings = new Dictionary<(string File, string Field), EmbedBinding>();
        var entries = new Dictionary<string, MgcbEntry>(StringComparer.Ordinal);
        var copies = new SortedSet<string>(StringComparer.Ordinal);
        var changes = new Dictionary<string, FileChange>(StringComparer.Ordinal);
        var notes = new List<string>();

        foreach (var e in FlashEmbeds.Find(project, fileSystem))
        {
            if (e.Kind == FlashEmbedKind.Symbol)
            {
                notes.Add($"{e.File}: the embed for {e.FieldName} comes from a SWF symbol and is not converted.");
                continue;
            }

            if (e.ResolvedPath is null || !e.Exists)
            {
                notes.Add($"{e.File}: the embed file for {e.FieldName} is missing or outside the project.");
                continue;
            }

            if (e.Kind == FlashEmbedKind.Font)
            {
                notes.Add($"{e.File}: the embedded font for {e.FieldName} is not converted; text uses the built-in font.");
                continue;
            }

            var path = e.ResolvedPath;
            var stem = path[..^Path.GetExtension(path).Length];
            switch (e.Kind)
            {
                case FlashEmbedKind.Image:
                    entries[path] = new MgcbEntry(path, null, "TextureImporter", "TextureProcessor", []);
                    bindings[(e.File, e.FieldName)] = new EmbedBinding(stem, "Texture");
                    break;
                case FlashEmbedKind.Audio:
                    var wav = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
                    entries[path] = new MgcbEntry(path, null, wav ? "WavImporter" : path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "Mp3Importer" : "OggImporter", "SoundEffectProcessor", []);
                    bindings[(e.File, e.FieldName)] = new EmbedBinding(stem, "Sound");
                    break;
                default:
                    copies.Add(path);
                    bindings[(e.File, e.FieldName)] = new EmbedBinding(path, "Data");
                    break;
            }

            changes[ContentFolder + "/" + path] = new FileChange(
                ContentFolder + "/" + path,
                ChangeKind.Copied,
                null,
                null,
                "Embedded asset copied for the content build.",
                Path.GetFullPath(Path.Combine(project.Root, path)));
        }

        string? mgcb = null;
        if (entries.Count > 0 || copies.Count > 0)
        {
            mgcb = MgcbWriter.Write(new MgcbDocument(MonoGameTarget.DesktopGL, "Reach", false, [], [.. entries.Values], [.. copies]));
            changes[ContentFolder + "/Content.mgcb"] = new FileChange(ContentFolder + "/Content.mgcb", ChangeKind.Created, null, mgcb, "Content project for the embedded assets.");
        }

        return new AssetPlan(bindings, mgcb, [.. changes.Values.OrderBy(c => c.RelativePath, StringComparer.Ordinal)], notes);
    }
}
