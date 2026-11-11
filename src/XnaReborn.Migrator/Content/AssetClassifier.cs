using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Content;

/// <summary>Decides, for one content item, whether MonoGame can build it and under which importer and processor.</summary>
public sealed class AssetClassifier
{
    private static readonly string[] _xactExtensions = [".xap", ".xgs", ".xwb", ".xsb"];

    private static readonly Dictionary<string, (string Importer, string Processor)> _byExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = ("TextureImporter", "TextureProcessor"),
        [".jpg"] = ("TextureImporter", "TextureProcessor"),
        [".jpeg"] = ("TextureImporter", "TextureProcessor"),
        [".bmp"] = ("TextureImporter", "TextureProcessor"),
        [".tga"] = ("TextureImporter", "TextureProcessor"),
        [".dds"] = ("TextureImporter", "TextureProcessor"),
        [".spritefont"] = ("FontDescriptionImporter", "FontDescriptionProcessor"),
        [".wav"] = ("WavImporter", "SoundEffectProcessor"),
        [".mp3"] = ("Mp3Importer", "SongProcessor"),
        [".wma"] = ("WmaImporter", "SongProcessor"),
        [".fbx"] = ("FbxImporter", "ModelProcessor"),
        [".x"] = ("XImporter", "ModelProcessor"),
        [".fx"] = ("EffectImporter", "EffectProcessor"),
        [".xml"] = ("XmlImporter", "PassThroughProcessor"),
    };

    private readonly IReadOnlyFileSystem _fileSystem;

    /// <summary>Creates a classifier.</summary>
    /// <param name="fileSystem">Read-only file system used to check that source files exist.</param>
    public AssetClassifier(IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    /// <summary>Classifies one item. The first rule that matches wins.</summary>
    /// <param name="item">The content item.</param>
    /// <param name="assetName">The item's asset name from <see cref="AssetNameResolver"/>.</param>
    /// <param name="contentProjectFullPath">Absolute path of the .contentproj. <c>item.SourcePath</c> is relative to its folder.</param>
    /// <param name="map">The importer and processor map.</param>
    /// <param name="hasNameConflict">True when another item has the same asset name.</param>
    /// <returns>The classified asset.</returns>
    public ConvertedAsset Classify(ContentItem item, string assetName, string contentProjectFullPath, ImporterMap map, bool hasNameConflict)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(assetName);
        ArgumentNullException.ThrowIfNull(contentProjectFullPath);
        ArgumentNullException.ThrowIfNull(map);

        var importer = item.Importer ?? string.Empty;
        var processor = item.Processor ?? string.Empty;
        var projectDirectory = Path.GetDirectoryName(contentProjectFullPath) ?? string.Empty;
        var sourcePath = Path.Combine(projectDirectory, item.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        var extension = Path.GetExtension(item.SourcePath);

        ConvertedAsset Result(string imp, string proc, AssetStatus status, string? note) =>
            new(item, assetName, imp, proc, status, note);

        if (!_fileSystem.FileExists(sourcePath))
        {
            return Result(importer, processor, AssetStatus.Missing, $"Source file not found: {item.SourcePath}");
        }

        if (hasNameConflict)
        {
            return Result(importer, processor, AssetStatus.Conflict,
                $"Asset name '{assetName}' is used by more than one item. Both items are kept; rename one so Content.Load is unambiguous.");
        }

        if (_xactExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || IsUnsupportedPair(item, map))
        {
            return Result(importer, processor, AssetStatus.ManualWork,
                "XACT audio has no MonoGame pipeline equivalent. Re-export it as plain assets (WAV, MP3 or OGG). The file is copied unchanged.");
        }

        if (item.Importer is not null || item.Processor is not null)
        {
            if (map.IsCustom(item.Importer, item.Processor))
            {
                return Result(importer, processor, AssetStatus.ManualWork,
                    "This item needs a custom pipeline assembly, which has to be ported to MonoGame by hand.");
            }
        }
        else if (extension.Equals(".xnb", StringComparison.OrdinalIgnoreCase) && !HasSiblingSource(sourcePath))
        {
            return Result(importer, processor, AssetStatus.Prebuilt,
                "Prebuilt .xnb without a source file. It is copied as is, cannot be rebuilt, and loading it is not guaranteed.");
        }

        var inferred = _byExtension.TryGetValue(extension, out var pair) ? pair : default;
        var finalImporter = item.Importer ?? inferred.Importer;
        var finalProcessor = item.Processor ?? inferred.Processor;
        if (string.IsNullOrEmpty(finalImporter) || string.IsNullOrEmpty(finalProcessor))
        {
            return Result(finalImporter ?? string.Empty, finalProcessor ?? string.Empty, AssetStatus.ManualWork, "Unknown asset type");
        }

        if (!map.IsStock(finalImporter, finalProcessor))
        {
            return Result(finalImporter, finalProcessor, AssetStatus.ManualWork,
                $"{finalImporter} with {finalProcessor} is not a stock pair that MonoGame can build.");
        }

        // An XNA 1.0 processor that was merged into a current one is built with the current one.
        finalProcessor = map.Resolve(finalImporter, finalProcessor, out var alias);
        var note = alias?.Note ?? (extension.Equals(".fx", StringComparison.OrdinalIgnoreCase) ? "Shader review needed." : null);
        return Result(finalImporter, finalProcessor, AssetStatus.Converted, note);
    }

    private static bool IsUnsupportedPair(ContentItem item, ImporterMap map) =>
        item.Importer is not null
        && item.Processor is not null
        && map.StockUnsupported.Any(p =>
            string.Equals(p.Importer, item.Importer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Processor, item.Processor, StringComparison.OrdinalIgnoreCase));

    private bool HasSiblingSource(string xnbPath)
    {
        var directory = Path.GetDirectoryName(xnbPath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(xnbPath);
        return _fileSystem.EnumerateFiles(directory, "*").Any(f =>
            !Path.GetExtension(f).Equals(".xnb", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileNameWithoutExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase));
    }
}
