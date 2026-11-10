using XnaReborn.Analyzer.IO;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Content;

/// <summary>Plans how each content source file reaches the output folder. Only checks that files exist; writes nothing.</summary>
public sealed class AssetCopier
{
    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates a copier.</summary>
    /// <param name="fileSystem">Read-only file system used for existence checks.</param>
    public AssetCopier(IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>Plans one change per asset, sorted by output path (ordinal).</summary>
    /// <param name="contentProjectFullPath">Absolute path of the .contentproj. Item source paths are relative to its folder.</param>
    /// <param name="assets">The classified assets.</param>
    /// <param name="outputContentFolder">Content folder relative to the output root, with '/' separators (for example <c>Game/Content</c>).</param>
    /// <returns>
    /// Converted and Prebuilt assets become Copied. ManualWork and Conflict become ManualWork with the asset note as reason.
    /// Missing becomes Skipped. Sources outside the content project folder go to <c>_external/{fileName}</c>.
    /// </returns>
    public IReadOnlyList<FileChange> Plan(string contentProjectFullPath, IReadOnlyList<ConvertedAsset> assets, string outputContentFolder)
    {
        ArgumentNullException.ThrowIfNull(contentProjectFullPath);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(outputContentFolder);

        var projectDirectory = Path.GetFullPath(Path.GetDirectoryName(contentProjectFullPath) ?? string.Empty);
        var folder = outputContentFolder.Replace('\\', '/').Trim('/');
        var changes = new List<FileChange>();

        foreach (var asset in assets)
        {
            var relative = asset.Item.SourcePath.Replace('\\', '/');
            var absolute = Path.GetFullPath(Path.Combine(projectDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
            var external = !IsInside(projectDirectory, absolute);
            var outputPath = external
                ? Join(folder, "_external", Path.GetFileName(absolute))
                : Join(folder, relative);

            string? note = asset.Note;
            if (external)
            {
                var externalNote = $"Source '{asset.Item.SourcePath}' is outside the content project folder; copied to _external/.";
                note = note is null ? externalNote : $"{externalNote} {note}";
            }

            changes.Add(asset.Status switch
            {
                AssetStatus.Converted or AssetStatus.Prebuilt =>
                    new FileChange(outputPath, ChangeKind.Copied, null, null, note, absolute),
                AssetStatus.ManualWork or AssetStatus.Conflict =>
                    new FileChange(outputPath, ChangeKind.ManualWork, null, null, note, absolute),
                _ => new FileChange(outputPath, ChangeKind.Skipped, null, null, note ?? $"Source file not found: {asset.Item.SourcePath}"),
            });
        }

        return changes.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ToList();
    }

    private static string Join(params string[] parts) =>
        string.Join('/', parts.Where(p => p.Length > 0));

    private static bool IsInside(string directory, string path)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
