using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Effects;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Content;

/// <summary>What the user chose for content conversion.</summary>
/// <param name="Target">The MonoGame platform. It decides the <c>/platform:</c> value of <c>Content.mgcb</c>.</param>
/// <param name="SubstituteFonts">
/// True to give a font that only XNA Game Studio installed a Windows substitute in the copied <c>.spritefont</c>.
/// False to list such an asset as manual work and leave it out of <c>Content.mgcb</c>.
/// </param>
/// <param name="RewriteEffects">True to rewrite <c>.fx</c> files (shader model macros, sampler fixes, markers) and copy the files they include. False to copy them unchanged.</param>
public sealed record ContentOptions(MonoGameTarget Target, bool SubstituteFonts, bool RewriteEffects = true);

/// <summary>The outcome of converting the content of one game project.</summary>
/// <param name="Changes">
/// The asset copies, substituted fonts and the generated <c>Content.mgcb</c> (a Created change), sorted by path (ordinal).
/// </param>
/// <param name="Report">The assets with their final status, and the counts.</param>
/// <param name="ManualNotes">Plain sentences for the manifest, sorted and distinct.</param>
/// <param name="MgcbRelativePath">Path of <c>Content.mgcb</c> relative to the output root, with '/' separators, or null when the project has no content.</param>
public sealed record ContentConversionResult(
    IReadOnlyList<FileChange> Changes,
    ContentReport Report,
    IReadOnlyList<string> ManualNotes,
    string? MgcbRelativePath);

/// <summary>
/// Converts the content of one game project: reads its content projects, classifies every item, writes <c>Content.mgcb</c>
/// and plans the copy of every source asset. Reads only through <see cref="IReadOnlyFileSystem"/> and writes nothing.
/// </summary>
public sealed class ContentConverter
{
    private const string _mgcbFileName = "Content.mgcb";
    private const string _defaultContentRoot = "Content";
    private const string _externalFolder = "_external";

    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly ImporterMap _map;
    private readonly XnaOnlyFonts _fonts;
    private readonly AssetClassifier _classifier;
    private readonly AssetCopier _copier;

    /// <summary>Creates a converter.</summary>
    /// <param name="fileSystem">Read-only file system for the source.</param>
    /// <param name="map">The importer map. Defaults to <see cref="ImporterMap.Default"/>.</param>
    /// <param name="fonts">The XNA-only font list. Defaults to <see cref="XnaOnlyFonts.Default"/>.</param>
    public ContentConverter(IReadOnlyFileSystem fileSystem, ImporterMap? map = null, XnaOnlyFonts? fonts = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
        _map = map ?? ImporterMap.Default;
        _fonts = fonts ?? XnaOnlyFonts.Default;
        _classifier = new AssetClassifier(fileSystem);
        _copier = new AssetCopier(fileSystem);
    }

    /// <summary>Converts the content of one game project.</summary>
    /// <param name="gameProject">The game project as read by the analyzer.</param>
    /// <param name="root">Absolute discovery root. The content project paths in <paramref name="gameProject"/> are relative to it.</param>
    /// <param name="gameDirRelative">The game project folder relative to the output root, with '/' separators. Empty for the root.</param>
    /// <param name="options">What the user chose.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    /// <returns>The changes, the report, the manual notes and the path of <c>Content.mgcb</c>.</returns>
    public Task<ContentConversionResult> ConvertAsync(
        ProjectReadResult gameProject,
        string root,
        string gameDirRelative,
        ContentOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameProject);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(gameDirRelative);
        ArgumentNullException.ThrowIfNull(options);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ContentConversionResult>(cancellationToken);
        }

        try
        {
            return Task.FromResult(ConvertCore(gameProject, root, gameDirRelative, options, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<ContentConversionResult>(cancellationToken);
        }
    }

    private static string Join(params string[] parts) =>
        string.Join('/', parts.Where(p => p.Length > 0));

    private static string ToNative(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static bool IsOutside(string relative) =>
        relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative);

    private static string Extension(string path) => Path.GetExtension(path);

    /// <summary>Removes "." and ".." segments. Segments that climb above the start are kept so the caller can see them.</summary>
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

    private static string SimpleName(string include)
    {
        var comma = include.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? include : include[..comma]).Trim();
    }

    private ContentConversionResult ConvertCore(
        ProjectReadResult gameProject,
        string root,
        string gameDirRelative,
        ContentOptions options,
        CancellationToken ct)
    {
        var notes = new List<string>();
        var contentProjectPaths = gameProject.Info.ContentProjects
            .Select(p => p.Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (contentProjectPaths.Count == 0)
        {
            return new ContentConversionResult([], ContentReport.Create([], []), [], null);
        }

        // Steps 1 and 2: read every content project.
        var reader = new ContentProjectReader(_fileSystem);
        var projects = new List<ContentProject>();
        foreach (var relative in contentProjectPaths)
        {
            ct.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(Path.Combine(root, ToNative(relative)));
            projects.Add(new ContentProject(relative, full, reader.Read(full, root)));
        }

        // Step 3: one content root for the whole game.
        var good = projects.Where(p => p.Read.Diagnostics.Count == 0).ToList();
        var first = good.Count > 0 ? good[0] : null;
        var contentRoot = first?.Read.ContentRootDirectory ?? _defaultContentRoot;
        foreach (var other in good.Skip(1).Where(p => !string.Equals(p.Read.ContentRootDirectory, contentRoot, StringComparison.Ordinal)))
        {
            notes.Add($"{other.RelativePath}: its ContentRootDirectory is '{other.Read.ContentRootDirectory}', but this game uses '{contentRoot}' from {first!.RelativePath}. All its content was put in one folder.");
        }

        var gameDir = gameDirRelative.Replace('\\', '/').Trim('/');
        var folder = Join(gameDir, contentRoot);
        var mgcbPath = Join(folder, _mgcbFileName);

        // Step 4 and 5: names over all items together, then the classification.
        var slots = new List<Slot>();
        foreach (var bad in projects.Where(p => p.Read.Diagnostics.Count > 0))
        {
            ct.ThrowIfCancellationRequested();
            slots.AddRange(FallbackSlots(bad, notes, ct));
        }

        var items = good.SelectMany(p => p.Read.Items.Select(i => (Project: p, Item: i))).ToList();
        var resolution = AssetNameResolver.Resolve(items.Select(i => i.Item).ToList(), contentRoot);
        var conflictNames = resolution.Conflicts.Select(c => c.AssetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nameChanges = new List<(Slot Slot, string Raw)>();
        for (var i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (project, item) = items[i];
            var rawName = resolution.Names[i].AssetName;
            var name = NormalizeName(rawName);
            var asset = _classifier.Classify(item, name, project.FullPath, _map, conflictNames.Contains(rawName));
            var slot = new Slot(project, asset);
            slots.Add(slot);
            if (Escapes(Collapse(rawName)))
            {
                nameChanges.Add((slot, rawName));
            }
        }

        AssignOutputPaths(slots, folder, mgcbPath, notes);
        foreach (var (slot, raw) in nameChanges)
        {
            notes.Add($"{slot.OutputPath}: the asset name '{raw}' leaves the content folder, so it was changed to '{slot.Asset.AssetName}'.");
        }

        // Step 6: fonts.
        foreach (var slot in slots.Where(s => s.Asset.Status == AssetStatus.Converted
                     && Extension(s.Asset.Item.SourcePath).Equals(".spritefont", StringComparison.OrdinalIgnoreCase)))
        {
            ct.ThrowIfCancellationRequested();
            CheckFont(slot, options, notes);
        }

        // Steps 7 and 8: the MGCB document.
        var entries = new List<MgcbEntry>();
        var copyPaths = new List<string>();
        foreach (var slot in slots)
        {
            ct.ThrowIfCancellationRequested();
            if (slot.Asset.Status == AssetStatus.Converted)
            {
                AddEntry(slot, folder, entries, notes);
            }
            else if (slot.Asset.Status == AssetStatus.Prebuilt)
            {
                AddCopyPath(slot, folder, options, copyPaths);
            }
        }

        foreach (var project in good)
        {
            foreach (var reference in project.Read.PipelineReferences)
            {
                var hint = reference.Metadata.TryGetValue("HintPath", out var h) && !string.IsNullOrWhiteSpace(h)
                    ? $" ({h.Trim().Replace('\\', '/')})"
                    : string.Empty;
                notes.Add($"{project.RelativePath}: the pipeline assembly '{SimpleName(reference.Include)}'{hint} was built against XNA, so Content.mgcb does not reference it. Port it to MonoGame and add a /reference: line by hand.");
            }
        }

        var document = new MgcbDocument(options.Target, MgcbWriter.MapProfile(first?.Read.Profile), false, [], entries, copyPaths);
        var documentProblems = MgcbWriter.FindProblems(document);
        if (documentProblems.Count > 0)
        {
            throw new InvalidOperationException("The generated Content.mgcb is not valid: " + string.Join(" ", documentProblems));
        }

        // Step 9: the copies, planned with the final statuses.
        var changes = new List<FileChange>
        {
            new(mgcbPath, ChangeKind.Created, null, MgcbWriter.Write(document), "Generated from the XNA content project. Build it with the MonoGame content builder."),
        };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mgcbPath };
        var skipped = new List<FileChange>();
        foreach (var slot in slots)
        {
            ct.ThrowIfCancellationRequested();
            var change = _copier.Plan(slot.Project.FullPath, [slot.Asset], folder)[0] with { RelativePath = slot.OutputPath };
            if (slot.FontNewText is not null && slot.Asset.Status == AssetStatus.Converted)
            {
                change = new FileChange(slot.OutputPath, ChangeKind.Modified, slot.FontOriginalText, slot.FontNewText, slot.FontReason);
            }

            if (change.Kind == ChangeKind.Skipped)
            {
                skipped.Add(change);
            }
            else if (used.Add(change.RelativePath))
            {
                changes.Add(change);
            }
        }

        // Effects: rewrite the copied .fx text and copy the files it includes.
        if (options.RewriteEffects)
        {
            foreach (var slot in slots.Where(s => s.Asset.Status == AssetStatus.Converted
                         && Extension(s.Asset.Item.SourcePath).Equals(".fx", StringComparison.OrdinalIgnoreCase)))
            {
                ct.ThrowIfCancellationRequested();
                RewriteEffect(slot, folder, changes, used, notes, ct);
            }
        }

        // A missing source cannot take a path that a real file needs.
        foreach (var change in skipped.OrderBy(c => c.RelativePath, StringComparer.Ordinal))
        {
            if (used.Add(change.RelativePath))
            {
                changes.Add(change);
            }
        }

        var sortedNotes = notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var report = ContentReport.Create(slots.Select(s => s.Asset), sortedNotes);
        return new ContentConversionResult(
            changes.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ToList(),
            report,
            sortedNotes,
            mgcbPath);
    }

    /// <summary>A content project that could not be read: every file below it is kept as manual work so nothing is lost.</summary>
    private IEnumerable<Slot> FallbackSlots(ContentProject project, List<string> notes, CancellationToken ct)
    {
        var message = project.Read.Diagnostics[0].Message;
        var directory = Path.GetDirectoryName(project.FullPath);
        if (directory is null || !_fileSystem.DirectoryExists(directory))
        {
            notes.Add($"{project.RelativePath}: {message} Its folder was not found, so no content was copied.");
            yield break;
        }

        notes.Add($"{project.RelativePath}: {message} Its files were copied unchanged as manual work and are not built.");
        var reason = "The content project could not be read, so this file was copied unchanged and is not built.";
        foreach (var file in ContentFileCollector.Collect(_fileSystem, directory, ct))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            var item = new ContentItem(relative, null, null, null, new Dictionary<string, string>());
            var name = relative[..(relative.Length - Extension(relative).Length)];
            yield return new Slot(project, new ConvertedAsset(item, name, string.Empty, string.Empty, AssetStatus.ManualWork, reason));
        }
    }

    /// <summary>Gives a name that stays inside the content folder. A name that climbs out is moved under <c>_external</c>.</summary>
    private static string NormalizeName(string name)
    {
        var collapsed = Collapse(name);
        if (Escapes(collapsed))
        {
            return Join(_externalFolder, collapsed[(collapsed.LastIndexOf('/') + 1)..]);
        }

        return collapsed.Length == 0 ? name : collapsed;
    }

    private static bool Escapes(string collapsedPath) =>
        collapsedPath == ".." || collapsedPath.StartsWith("../", StringComparison.Ordinal);

    /// <summary>
    /// Works out the final output path of every copy. Two copies may not share a path (case-insensitive); the later ones, ordered by
    /// source path, get <c>-2</c>, <c>-3</c> before the extension. Missing sources take part only as far as they never block a real file.
    /// </summary>
    private void AssignOutputPaths(List<Slot> slots, string folder, string mgcbPath, List<string> notes)
    {
        foreach (var slot in slots)
        {
            var asset = slot.Asset;
            var projectDirectory = Path.GetDirectoryName(slot.Project.FullPath) ?? string.Empty;
            var absolute = Path.GetFullPath(Path.Combine(projectDirectory, ToNative(asset.Item.SourcePath)));
            var relative = Path.GetRelativePath(projectDirectory, absolute).Replace('\\', '/');
            slot.SourceFullPath = absolute;
            slot.OutputPath = IsOutside(relative)
                ? Join(folder, _externalFolder, Path.GetFileName(absolute))
                : Join(folder, relative);
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mgcbPath };
        var real = slots.Where(s => s.Asset.Status != AssetStatus.Missing)
            .OrderBy(s => s.SourceFullPath, StringComparer.Ordinal)
            .ThenBy(s => s.OutputPath, StringComparer.Ordinal)
            .ToList();
        var renamed = new List<Slot>();

        // The first claimant of each path keeps it, so a renamed copy never takes a path another file already has.
        foreach (var slot in real)
        {
            if (!used.Add(slot.OutputPath))
            {
                renamed.Add(slot);
            }
        }

        foreach (var slot in renamed)
        {
            var original = slot.OutputPath;
            var extension = Extension(original);
            var stem = original[..(original.Length - extension.Length)];
            for (var n = 2; ; n++)
            {
                var candidate = $"{stem}-{n}{extension}";
                if (used.Add(candidate))
                {
                    slot.OutputPath = candidate;
                    break;
                }
            }

            notes.Add($"{original}: another file already uses this path, so the copy of '{slot.Asset.Item.SourcePath}' is named {slot.OutputPath}.");
        }
    }

    private void CheckFont(Slot slot, ContentOptions options, List<string> notes)
    {
        string text;
        bool hasBom;
        bool isUtf8;
        try
        {
            (text, hasBom, isUtf8) = ReadText(slot.SourceFullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ToManual(slot, $"The font file could not be read: {e.Message}");
            return;
        }

        var check = FontChecker.Check(text, _fonts);
        if (check.Problem is not null)
        {
            ToManual(slot, check.Problem);
            return;
        }

        if (!check.IsXnaOnly)
        {
            return;
        }

        var family = check.Family!;
        var substitute = check.Substitute!;
        if (!options.SubstituteFonts)
        {
            ToManual(slot, $"Font '{family}' shipped only with XNA Game Studio. Turn on font substitution (suggested: {substitute}) or point FontName to an installed font.");
            return;
        }

        if (!isUtf8)
        {
            ToManual(slot, $"Font '{family}' shipped only with XNA Game Studio, but the file is not UTF-8, so it cannot be rewritten safely. Save it as UTF-8 or point FontName to an installed font (suggested: {substitute}).");
            return;
        }

        try
        {
            slot.FontNewText = FontSubstituter.Substitute(text, substitute);
        }
        catch (InvalidOperationException e)
        {
            ToManual(slot, e.Message);
            return;
        }

        var sentence = $"The font '{family}' shipped only with XNA Game Studio, so the copy uses '{substitute}' instead.";
        slot.FontOriginalText = (hasBom ? "\uFEFF" : string.Empty) + text;
        slot.FontReason = sentence;
        slot.Asset = slot.Asset with { Note = sentence };
        notes.Add($"{slot.OutputPath}: {sentence}");
    }

    private (string Text, bool HasBom, bool IsUtf8) ReadText(string path)
    {
        byte[] bytes;
        using (var stream = _fileSystem.OpenRead(path))
        using (var buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var isUtf16Or32 = bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF));
        using var text = new MemoryStream(bytes);
        using var reader = new StreamReader(text, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        return (reader.ReadToEnd(), hasBom, !isUtf16Or32);
    }

    private void RewriteEffect(Slot slot, string folder, List<FileChange> changes, HashSet<string> used, List<string> notes, CancellationToken ct)
    {
        var index = changes.FindIndex(c => string.Equals(c.RelativePath, slot.OutputPath, StringComparison.Ordinal) && c.Kind == ChangeKind.Copied);
        if (index < 0)
        {
            return;
        }

        string text;
        bool hasBom;
        bool isUtf8;
        try
        {
            (text, hasBom, isUtf8) = ReadText(slot.SourceFullPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            notes.Add($"{slot.OutputPath}: the effect could not be read for rewriting ({e.Message}), so it was copied unchanged.");
            return;
        }

        if (!isUtf8)
        {
            notes.Add($"{slot.OutputPath}: the effect is not UTF-8, so it was copied unchanged. Save it as UTF-8 to have its shader models rewritten.");
            return;
        }

        var result = EffectRewriteStep.Rewrite(text);
        foreach (var note in result.Notes)
        {
            notes.Add($"{slot.OutputPath}: [{note.RuleId}] {note.Message}");
        }

        if (!string.Equals(result.Text, text, StringComparison.Ordinal))
        {
            changes[index] = new FileChange(
                slot.OutputPath,
                ChangeKind.Modified,
                (hasBom ? "\uFEFF" : string.Empty) + text,
                result.Text,
                "Shader models, sampler states and unsupported constructs were adjusted for MonoGame. See the notes for this file.");
        }

        var includes = EffectRewriteStep.CollectIncludes(slot.SourceFullPath, _fileSystem, ct);
        foreach (var missing in includes.Missing)
        {
            notes.Add($"{slot.OutputPath}: {missing}");
        }

        var sourceDirectory = Path.GetDirectoryName(slot.SourceFullPath) ?? string.Empty;
        var outputDirectory = slot.OutputPath.Contains('/', StringComparison.Ordinal) ? slot.OutputPath[..slot.OutputPath.LastIndexOf('/')] : string.Empty;
        foreach (var include in includes.Files)
        {
            var relative = Path.GetRelativePath(sourceDirectory, include).Replace('\\', '/');
            var outputPath = IsOutside(relative)
                ? Join(folder, _externalFolder, Path.GetFileName(include))
                : Join(outputDirectory, relative);
            if (used.Add(outputPath))
            {
                changes.Add(new FileChange(outputPath, ChangeKind.Copied, null, null, $"Included by {Path.GetFileName(slot.SourceFullPath)}.", include));
            }
        }
    }

    private static void ToManual(Slot slot, string note) =>
        slot.Asset = slot.Asset with { Status = AssetStatus.ManualWork, Note = note };

    private static string RelativeToFolder(Slot slot, string folder) => slot.OutputPath[(folder.Length + 1)..];

    private static void AddEntry(Slot slot, string folder, List<MgcbEntry> entries, List<string> notes)
    {
        var asset = slot.Asset;
        var mapped = ProcessorParamMapper.Map(asset.MonoGameImporter, asset.MonoGameProcessor, asset.Item.ProcessorParams);

        // The entry points at the file as it is in the output, which differs from the item path for external or renamed copies.
        var placed = asset with { Item = asset.Item with { SourcePath = RelativeToFolder(slot, folder) } };
        var entry = MgcbWriter.ToEntry(placed, mapped);
        var problems = MgcbWriter.FindProblems(entry);
        if (problems.Count > 0)
        {
            ToManual(slot, string.Join(" ", problems));
            return;
        }

        entries.Add(entry);
        foreach (var dropped in mapped.Dropped)
        {
            notes.Add($"{slot.OutputPath}: processor parameter {dropped}");
        }
    }

    private static void AddCopyPath(Slot slot, string folder, ContentOptions options, List<string> copyPaths)
    {
        var path = RelativeToFolder(slot, folder);
        var probe = new MgcbDocument(options.Target, "HiDef", false, [], [], [path]);
        var problems = MgcbWriter.FindProblems(probe);
        if (problems.Count > 0)
        {
            ToManual(slot, string.Join(" ", problems));
            return;
        }

        copyPaths.Add(path);
    }

    private sealed record ContentProject(string RelativePath, string FullPath, ContentProjectReadResult Read);

    private sealed class Slot(ContentProject project, ConvertedAsset asset)
    {
        public ContentProject Project { get; } = project;

        public ConvertedAsset Asset { get; set; } = asset;

        public string SourceFullPath { get; set; } = string.Empty;

        public string OutputPath { get; set; } = string.Empty;

        public string? FontOriginalText { get; set; }

        public string? FontNewText { get; set; }

        public string? FontReason { get; set; }
    }
}
