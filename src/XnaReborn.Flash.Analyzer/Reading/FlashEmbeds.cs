using XnaReborn.Analyzer.IO;
using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Analyzer.Reading;

/// <summary>What an <c>[Embed]</c> tag brings in.</summary>
public enum FlashEmbedKind
{
    /// <summary>A png, jpg or gif picture.</summary>
    Image,

    /// <summary>An mp3, wav or ogg sound.</summary>
    Audio,

    /// <summary>Any other file, such as a CSV tile map or XML.</summary>
    Data,

    /// <summary>A font file or a system font.</summary>
    Font,

    /// <summary>A symbol from a SWF file.</summary>
    Symbol,
}

/// <summary>One <c>[Embed]</c> tag on a field.</summary>
/// <param name="File">Relative path of the .as file that holds the tag.</param>
/// <param name="Span">Where the tag is.</param>
/// <param name="FieldName">The field the tag decorates.</param>
/// <param name="Source">The source attribute as written, or null.</param>
/// <param name="ResolvedPath">Path relative to the project root with forward slashes, or null when the path is absolute or leaves the project.</param>
/// <param name="Kind">What the embed is.</param>
/// <param name="Exists">True when the resolved file exists. Always true for symbol embeds and system fonts.</param>
/// <param name="MimeType">The mimeType attribute, or null.</param>
public sealed record FlashEmbed(
    string File,
    SourceSpan Span,
    string FieldName,
    string? Source,
    string? ResolvedPath,
    FlashEmbedKind Kind,
    bool Exists,
    string? MimeType);

/// <summary>Finds and classifies the <c>[Embed]</c> tags of a project.</summary>
public static class FlashEmbeds
{
    private static readonly HashSet<string> _image = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif" };
    private static readonly HashSet<string> _audio = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".ogg" };
    private static readonly HashSet<string> _font = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };

    /// <summary>Lists every embed in project code (bundled Flixel sources are skipped).</summary>
    /// <param name="project">The project.</param>
    /// <param name="fileSystem">Used to check that files exist. Nothing is read.</param>
    public static IReadOnlyList<FlashEmbed> Find(FlashProject project, IReadOnlyFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(fileSystem);
        var list = new List<FlashEmbed>();
        foreach (var file in project.Files.Where(f => !f.IsFlixelSource))
        {
            foreach (var node in As3Walker.DescendantsAndSelf(file.Unit))
            {
                if (node is As3Field field)
                {
                    foreach (var meta in field.Metadata.Where(m => m.Name == "Embed"))
                    {
                        list.Add(Classify(project, fileSystem, file.Path, field.Name, meta));
                    }
                }
            }
        }

        return list;
    }

    private static FlashEmbed Classify(FlashProject project, IReadOnlyFileSystem fs, string file, string field, As3Metadata meta)
    {
        var source = meta.Get("source");
        var mime = meta.Get("mimeType");
        var symbol = meta.Get("symbol");
        var ext = source is null ? string.Empty : Path.GetExtension(source);
        if (symbol is not null || string.Equals(ext, ".swf", StringComparison.OrdinalIgnoreCase))
        {
            return new FlashEmbed(file, meta.Span, field, source, null, FlashEmbedKind.Symbol, true, mime);
        }

        if (source is null)
        {
            var systemFont = meta.Get("systemFont") is not null;
            return new FlashEmbed(file, meta.Span, field, null, null, systemFont ? FlashEmbedKind.Font : FlashEmbedKind.Data, systemFont, mime);
        }

        var kind = _image.Contains(ext) ? FlashEmbedKind.Image
            : _audio.Contains(ext) ? FlashEmbedKind.Audio
            : _font.Contains(ext) || meta.Get("fontName") is not null || meta.Get("fontFamily") is not null ? FlashEmbedKind.Font
            : FlashEmbedKind.Data;

        var resolved = Resolve(project, file, source);
        var exists = resolved is not null && fs.FileExists(Path.Combine(project.Root, resolved));
        return new FlashEmbed(file, meta.Span, field, source, resolved, kind, exists, mime);
    }

    /// <summary>
    /// Resolves an embed path. A leading slash means "relative to a source root"; other paths are relative to the .as file.
    /// Returns null when the path is absolute (drive or UNC) or leaves the project folder.
    /// </summary>
    /// <param name="project">The project.</param>
    /// <param name="declaringFile">Relative path of the .as file.</param>
    /// <param name="source">The source attribute.</param>
    public static string? Resolve(FlashProject project, string declaringFile, string source)
    {
        ArgumentNullException.ThrowIfNull(project);
        var s = source.Replace('\\', '/');
        if (s.Length == 0 || s.StartsWith("//", StringComparison.Ordinal) || (s.Length > 1 && s[1] == ':'))
        {
            return null;
        }

        string baseDir;
        if (s.StartsWith('/'))
        {
            s = s.TrimStart('/');
            // shortcut: only the first source root is searched for "/path" embeds; add the others if a project needs it.
            baseDir = Path.Combine(project.Root, project.SourceRoots[0]);
        }
        else
        {
            baseDir = Path.GetDirectoryName(Path.Combine(project.Root, declaringFile)) ?? project.Root;
        }

        var full = Path.GetFullPath(Path.Combine(baseDir, s));
        var rootFull = Path.GetFullPath(project.Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetRelativePath(rootFull, full).Replace('\\', '/');
    }
}
