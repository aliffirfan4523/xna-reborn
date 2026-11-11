using System.Text.RegularExpressions;
using System.Xml.Linq;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer.As3;
using XnaReborn.Flash.Analyzer.Detection;

namespace XnaReborn.Flash.Analyzer.Reading;

/// <summary>Reads a Flash project folder: source roots, parsed files, entry class and Flixel version. Read-only.</summary>
public static class FlashProjectReader
{
    private static readonly Regex _major = new(@"LIBRARY_MAJOR_VERSION\s*(?::\s*\w+)?\s*=\s*(\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex _minor = new(@"LIBRARY_MINOR_VERSION\s*(?::\s*\w+)?\s*=\s*(\d+)", RegexOptions.CultureInvariant);

    /// <summary>Reads the project in <paramref name="root"/>. Never throws for bad input; problems become diagnostics.</summary>
    /// <param name="fileSystem">Read-only file access. Only .as and project files are read.</param>
    /// <param name="root">Absolute project folder.</param>
    /// <param name="cancellationToken">Cancels the read at file boundaries.</param>
    public static FlashProject Read(IReadOnlyFileSystem fileSystem, string root, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        var detection = FlashProjectDetector.Detect(fileSystem, root, cancellationToken);
        var diagnostics = new List<Diagnostic>();
        var roots = ReadSourceRoots(fileSystem, root, detection.ProjectFile, diagnostics);

        var files = new List<FlashSourceFile>();
        foreach (var rel in detection.SourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text;
            try
            {
                text = fileSystem.ReadAllText(Path.Combine(root, rel));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic("FLA001", $"Cannot read source file: {ex.Message}", rel));
                continue;
            }

            var unit = As3Parser.Parse(text);
            foreach (var d in unit.Diagnostics)
            {
                diagnostics.Add(new Diagnostic("FLA002", $"{d.Message} (line {d.Span.Line}, column {d.Span.Column})", rel));
            }

            var isFlixel = unit.PackageName == "org.flixel" || unit.PackageName.StartsWith("org.flixel.", StringComparison.Ordinal);
            files.Add(new FlashSourceFile(rel, text, unit, isFlixel));
        }

        var entries = new List<FlashEntryClass>();
        var usesFlixel = false;
        foreach (var f in files)
        {
            usesFlixel |= f.IsFlixelSource || f.Unit.Imports.Any(i => i.Name.StartsWith("org.flixel", StringComparison.Ordinal));
            if (f.IsFlixelSource)
            {
                continue;
            }

            foreach (var t in f.Unit.Members.OfType<As3TypeDeclaration>())
            {
                if (t.Kind == As3TypeKind.Class && t.BaseType is { Name: "FlxGame" or "org.flixel.FlxGame" })
                {
                    var full = f.Unit.PackageName.Length == 0 ? t.Name : f.Unit.PackageName + "." + t.Name;
                    entries.Add(new FlashEntryClass(t.Name, full, f.Path, t.Metadata));
                    usesFlixel = true;
                }
            }
        }

        return new FlashProject(
            root,
            detection.ProjectFile,
            roots,
            files,
            entries,
            usesFlixel,
            files.Any(f => f.IsFlixelSource),
            ProbeVersion(files),
            detection,
            diagnostics);
    }

    private static string? ProbeVersion(IReadOnlyList<FlashSourceFile> files)
    {
        foreach (var f in files.Where(f => f.IsFlixelSource))
        {
            var major = _major.Match(f.Text);
            if (major.Success)
            {
                var minor = _minor.Match(f.Text);
                return minor.Success ? $"{major.Groups[1].Value}.{minor.Groups[1].Value}" : major.Groups[1].Value;
            }
        }

        return null;
    }

    private static List<string> ReadSourceRoots(IReadOnlyFileSystem fileSystem, string root, string? projectFile, List<Diagnostic> diagnostics)
    {
        var result = new List<string>();
        if (projectFile is not null)
        {
            try
            {
                var doc = XDocument.Parse(fileSystem.ReadAllText(Path.Combine(root, projectFile)));
                var found = projectFile.EndsWith(".as3proj", StringComparison.OrdinalIgnoreCase)
                    ? doc.Descendants("class").Select(e => (string?)e.Attribute("path"))
                    : doc.Descendants("compiler").Select(e => (string?)e.Attribute("sourceFolderPath"));
                foreach (var p in found)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                    {
                        result.Add(Normalize(p));
                    }
                }
            }
            catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic("FLA003", $"Cannot read project file: {ex.Message}", projectFile));
            }
        }

        if (result.Count == 0)
        {
            result.Add(".");
        }

        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string Normalize(string path)
    {
        var p = path.Replace('\\', '/').Trim().TrimEnd('/');
        return p.Length == 0 ? "." : p;
    }
}
