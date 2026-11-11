using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Content;

/// <summary>
/// Converts the content of one SharpDX Toolkit game project (the items with a Toolkit build action) into a <c>Content.mgcb</c> and the files
/// it builds. Fonts are converted from <c>TkFont</c> to <c>.spritefont</c>. Textures, models and <c>.wav</c> sounds are built with the stock
/// MonoGame importers. Effects are copied and flagged (SDX013). Compiled <c>.tkb</c> files with no source are copied and flagged (SDX015).
/// Reads only through <see cref="IReadOnlyFileSystem"/> and writes nothing.
/// </summary>
/// <remarks>
/// Every asset is placed in the content folder under its asset name plus its extension, so <c>Content.Load&lt;T&gt;("Balls")</c> keeps
/// working whatever the folder of the source file was. Formats accepted by the MonoGame 3.8.6.1 builder are listed in
/// <c>docs</c> of the importer map notes (see the design notes of the sharpdx-toolkit spec).
/// </remarks>
public sealed class ToolkitContentConverter
{
    private const string _mgcbFileName = "Content.mgcb";
    private const string _contentFolder = "Content";
    private const string _externalFolder = "_external";
    private const string _conflictFolder = "_conflict";
    private const string _effectNote = "A Toolkit effect (SDX013). The Toolkit effect syntax is not the syntax of the MonoGame effect compiler. The file was copied unchanged and is not built; rewrite it in MonoGame effect syntax and add it to Content.mgcb.";
    private const string _prebuiltNote = "A compiled .tkb file with no source (SDX015). MonoGame cannot read it and it cannot be rebuilt. The file was copied unchanged; add the original asset to the project.";

    private static readonly string[] _textureExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tga", ".dds"];

    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly XnaOnlyFonts _fonts;

    /// <summary>Creates a converter.</summary>
    /// <param name="fileSystem">Read-only file system for the source.</param>
    /// <param name="fonts">The XNA-only font list. Defaults to <see cref="XnaOnlyFonts.Default"/>.</param>
    public ToolkitContentConverter(IReadOnlyFileSystem fileSystem, XnaOnlyFonts? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
        _fonts = fonts ?? XnaOnlyFonts.Default;
    }

    /// <summary>Tells whether a project has Toolkit content: an item with a Toolkit build action, or a compiled <c>.tkb</c> file item.</summary>
    /// <param name="project">The project as read by the analyzer.</param>
    public static bool HasContent(ProjectReadResult project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return ToolkitContentReader.Read(project).Count > 0 || PrebuiltItems(project).Count > 0;
    }

    /// <summary>Converts the content of one Toolkit game project.</summary>
    /// <param name="project">The game project as read by the analyzer.</param>
    /// <param name="root">Absolute discovery root. The project path in <paramref name="project"/> is relative to it.</param>
    /// <param name="gameDirRelative">The game project folder relative to the output root, with '/' separators. Empty for the root.</param>
    /// <param name="options">What the user chose.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The changes, the report, the manual notes and the path of <c>Content.mgcb</c>, or no path when the project has no content.</returns>
    public Task<ContentConversionResult> ConvertAsync(
        ProjectReadResult project,
        string root,
        string gameDirRelative,
        ContentOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(gameDirRelative);
        ArgumentNullException.ThrowIfNull(options);
        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<ContentConversionResult>(cancellationToken)
            : Task.FromResult(Convert(project, root, gameDirRelative, options, cancellationToken));
    }

    private static List<ToolkitContentItem> PrebuiltItems(ProjectReadResult project)
    {
        var named = ToolkitContentReader.Read(project).Select(i => i.AssetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<ToolkitContentItem>();
        foreach (var item in project.Items)
        {
            if (item.ItemType is not ("Content" or "None" or "EmbeddedResource")
                || !item.Include.Trim().EndsWith(".tkb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = item.Include.Trim().Replace('\\', '/');
            var name = path[..^".tkb".Length];
            if (!named.Contains(name))
            {
                result.Add(new ToolkitContentItem("(prebuilt)", new ContentItem(path, name, null, null, new Dictionary<string, string>()), null));
            }
        }

        return result;
    }

    private static string Join(params string[] parts) => string.Join('/', parts.Where(p => p.Length > 0));

    private static string ToNative(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private static string Collapse(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Replace('\\', '/').Split('/'))
        {
            if (part.Length == 0 || part == ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    private ContentConversionResult Convert(ProjectReadResult project, string root, string gameDirRelative, ContentOptions options, CancellationToken ct)
    {
        var items = ToolkitContentReader.Read(project).Concat(PrebuiltItems(project)).ToList();
        if (items.Count == 0)
        {
            return new ContentConversionResult([], ContentReport.Create([], []), [], null);
        }

        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, ToNative(project.Info.Path)))) ?? root;
        var folder = Join(gameDirRelative.Replace('\\', '/').Trim('/'), _contentFolder);
        var mgcbPath = Join(folder, _mgcbFileName);
        var notes = new List<string>();
        var slots = new List<Slot>();
        var conflicts = items.GroupBy(i => Name(i), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var source = Path.GetFullPath(Path.Combine(projectDirectory, ToNative(item.Item.SourcePath)));
            slots.Add(Classify(item, source, conflicts.Contains(Name(item)), folder, options, notes));
        }

        var entries = new List<MgcbEntry>();
        var changes = new List<FileChange>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mgcbPath };
        foreach (var slot in slots.OrderBy(s => s.OutputPath, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var change = ToChange(slot);
            if (!used.Add(change.RelativePath))
            {
                // Two assets whose names differ only in case would write one file. The second keeps its place in the report as manual work.
                slot.Asset = slot.Asset with { Status = AssetStatus.Conflict, Note = "Another asset has the same name apart from case." };
                continue;
            }

            changes.Add(change);
            if (slot.Asset.Status == AssetStatus.Converted)
            {
                var placed = slot.Asset with { Item = slot.Asset.Item with { SourcePath = slot.OutputPath[(folder.Length + 1)..] } };
                entries.Add(MgcbWriter.ToEntry(placed, ProcessorParamMapper.Map(placed.MonoGameImporter, placed.MonoGameProcessor, placed.Item.ProcessorParams)));
            }
        }

        var document = new MgcbDocument(options.Target, MgcbWriter.MapProfile(null), false, [], entries, []);
        var problems = MgcbWriter.FindProblems(document);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The generated Content.mgcb is not valid: " + string.Join(" ", problems));
        }

        changes.Insert(0, new FileChange(mgcbPath, ChangeKind.Created, null, MgcbWriter.Write(document), "Generated from the Toolkit build actions of the game project. Build it with the MonoGame content builder."));

        AddSummaryNotes(slots, notes);
        var sorted = notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return new ContentConversionResult(
            changes.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ToList(),
            ContentReport.Create(slots.Select(s => s.Asset), sorted),
            sorted,
            mgcbPath);
    }

    // An item without a Link is placed by its project-relative path, and the game's ContentManager adds the root folder (Content) in front of the name.
    // So a path that starts with the usual root folder gives a name without it.
    private static string Name(ToolkitContentItem item)
    {
        var name = Collapse(item.AssetName);
        return name.StartsWith(_contentFolder + "/", StringComparison.OrdinalIgnoreCase) ? name[(_contentFolder.Length + 1)..] : name;
    }

    private Slot Classify(ToolkitContentItem item, string source, bool conflict, string folder, ContentOptions options, List<string> notes)
    {
        var name = Name(item);
        if (name == ".." || name.StartsWith("../", StringComparison.Ordinal))
        {
            name = Join(_externalFolder, name[(name.LastIndexOf('/') + 1)..]);
        }

        var extension = Path.GetExtension(item.Item.SourcePath);
        var include = item.Item.SourcePath;
        ConvertedAsset Asset(string importer, string processor, AssetStatus status, string? note) =>
            new(item.Item, name, importer, processor, status, note);

        if (!_fileSystem.FileExists(source))
        {
            return new Slot(Asset(string.Empty, string.Empty, AssetStatus.Missing, $"Source file not found: {include}"), source, Join(folder, name + extension), null);
        }

        if (conflict)
        {
            return new Slot(Asset(string.Empty, string.Empty, AssetStatus.Conflict, $"Asset name '{name}' is used by more than one item. All of them are kept; rename one so Content.Load is unambiguous."), source, Join(folder, _conflictFolder, include), null);
        }

        var output = Join(folder, name + extension);
        switch (item.BuildAction)
        {
            case "ToolkitFont":
                return ClassifyFont(item, name, source, folder, options, notes, Asset);
            case "ToolkitTexture" when _textureExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase):
                return new Slot(Asset("TextureImporter", "TextureProcessor", AssetStatus.Converted, null), source, output, null);
            case "ToolkitModel" when extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase):
                return new Slot(Asset("FbxImporter", "ModelProcessor", AssetStatus.Converted, null), source, output, null);
            case "ToolkitModel" when extension.Equals(".x", StringComparison.OrdinalIgnoreCase):
                return new Slot(Asset("XImporter", "ModelProcessor", AssetStatus.Converted, null), source, output, null);
            case "ToolkitAudio" when extension.Equals(".wav", StringComparison.OrdinalIgnoreCase):
                return new Slot(Asset("WavImporter", "SoundEffectProcessor", AssetStatus.Converted, null), source, output, null);
            case "ToolkitFxc":
                notes.Add($"{output}: {_effectNote}");
                return new Slot(Asset("EffectImporter", "EffectProcessor", AssetStatus.ManualWork, _effectNote), source, output, null);
            case "(prebuilt)":
                notes.Add($"{output}: {_prebuiltNote}");
                return new Slot(Asset(string.Empty, string.Empty, AssetStatus.Prebuilt, _prebuiltNote), source, output, null);
            default:
                var kind = item.BuildAction["Toolkit".Length..].ToLowerInvariant();
                var note = $"The {extension} format of a Toolkit {kind} is not one the MonoGame builder takes by this migration. The file was copied unchanged and is not built.";
                notes.Add($"{output}: {note}");
                return new Slot(Asset(string.Empty, string.Empty, AssetStatus.ManualWork, note), source, output, null);
        }
    }

    private Slot ClassifyFont(ToolkitContentItem item, string name, string source, string folder, ContentOptions options, List<string> notes, Func<string, string, AssetStatus, string?, ConvertedAsset> asset)
    {
        var fontOutput = Join(folder, name + ".spritefont");
        var copyOutput = Join(folder, name + Path.GetExtension(item.Item.SourcePath));
        string text;
        try
        {
            using var stream = _fileSystem.OpenRead(source);
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Manual($"The font file could not be read: {e.Message}");
        }

        var converted = TkFontConverter.Convert(text);
        if (converted.SpriteFont is null)
        {
            return Manual(converted.Error!);
        }

        foreach (var warning in converted.Warnings)
        {
            notes.Add($"{fontOutput}: {warning}");
        }

        var spriteFont = converted.SpriteFont;
        var check = FontChecker.Check(spriteFont, _fonts);
        string? note = converted.Warnings.Count == 0 ? null : string.Join(" ", converted.Warnings);
        if (check.IsXnaOnly)
        {
            if (!options.SubstituteFonts)
            {
                return Manual($"Font '{check.Family}' shipped only with XNA Game Studio. Turn on font substitution (suggested: {check.Substitute}) or point FontName to an installed font.");
            }

            spriteFont = FontSubstituter.Substitute(spriteFont, check.Substitute!);
            var sentence = $"The font '{check.Family}' shipped only with XNA Game Studio, so the copy uses '{check.Substitute}' instead.";
            notes.Add($"{fontOutput}: {sentence}");
            note = note is null ? sentence : note + " " + sentence;
        }

        return new Slot(asset("FontDescriptionImporter", "FontDescriptionProcessor", AssetStatus.Converted, note), source, fontOutput, spriteFont);

        Slot Manual(string problem)
        {
            notes.Add($"{copyOutput}: {problem}");
            return new Slot(asset(string.Empty, string.Empty, AssetStatus.ManualWork, problem), source, copyOutput, null);
        }
    }

    private static FileChange ToChange(Slot slot)
    {
        var asset = slot.Asset;
        return asset.Status switch
        {
            AssetStatus.Converted when slot.GeneratedText is not null =>
                new FileChange(slot.OutputPath, ChangeKind.Created, null, slot.GeneratedText, asset.Note ?? "Converted from the Toolkit font file."),
            AssetStatus.Converted or AssetStatus.Prebuilt =>
                new FileChange(slot.OutputPath, ChangeKind.Copied, null, null, asset.Note, slot.SourceFullPath),
            AssetStatus.ManualWork or AssetStatus.Conflict =>
                new FileChange(slot.OutputPath, ChangeKind.ManualWork, null, null, asset.Note, slot.SourceFullPath),
            _ => new FileChange(slot.OutputPath, ChangeKind.Skipped, null, null, asset.Note ?? $"Source file not found: {asset.Item.SourcePath}"),
        };
    }

    private static void AddSummaryNotes(List<Slot> slots, List<string> notes)
    {
        var loadable = slots
            .Where(s => s.Asset.Status is AssetStatus.Converted or AssetStatus.ManualWork or AssetStatus.Prebuilt)
            .Select(s => $"'{s.Asset.AssetName}' (from {s.Asset.Item.SourcePath})")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (loadable.Count > 0)
        {
            notes.Add("Content.Load names: " + string.Join(", ", loadable) + ". The Toolkit added .tkb to a name without an extension. A Load call that still spells the extension, such as Load<Texture2D>(\"Balls.dds\"), has to drop it.");
        }

        if (slots.Any(s => s.Asset.Status == AssetStatus.Converted && s.Asset.MonoGameProcessor == "TextureProcessor"))
        {
            notes.Add("Textures: whether the Toolkit texture path and the MonoGame TextureProcessor premultiply alpha the same way was not checked. If sprite edges look wrong, set PremultiplyAlpha in Content.mgcb.");
        }
    }

    private sealed class Slot(ConvertedAsset asset, string sourceFullPath, string outputPath, string? generatedText)
    {
        public ConvertedAsset Asset { get; set; } = asset;

        public string SourceFullPath { get; } = sourceFullPath;

        public string OutputPath { get; } = outputPath;

        public string? GeneratedText { get; } = generatedText;
    }
}
