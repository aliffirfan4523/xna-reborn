using System.Globalization;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Xact;

namespace XnaReborn.Analyzer.Xact;

/// <summary>The result of reading one .xap file.</summary>
/// <param name="Project">The project, or null when the text could not be read as blocks.</param>
/// <param name="Diagnostics">Problems found while reading: ANA070 for unbalanced braces, ANA071 for a malformed line.</param>
public sealed record XapReadResult(XapProject? Project, IReadOnlyList<Diagnostic> Diagnostics);

/// <summary>
/// Reads an XACT project (<c>.xap</c>) through <see cref="IReadOnlyFileSystem"/>. The format is plain text:
/// a name line, <c>{</c>, <c>Key = Value;</c> lines and child blocks, <c>}</c>.
/// Keys the reader does not know are ignored, except those named in <c>rules/xact-features.json</c>, which are listed as unsupported.
/// </summary>
public static class XapReader
{
    // shortcut: the format is read as a generic block tree with a table-driven mapper, not from a grammar; upgrade if a real .xap needs more than name-line blocks.
    private static readonly char[] _separators = ['\\', '/'];

    /// <summary>Reads and maps a .xap file.</summary>
    /// <param name="path">Absolute path of the file.</param>
    /// <param name="fileSystem">Read-only file system.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static Task<XapReadResult> ReadAsync(string path, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(fileSystem);
        cancellationToken.ThrowIfCancellationRequested();
        string text;
        try
        {
            text = fileSystem.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(new XapReadResult(null, [new Diagnostic("ANA071", "The .xap file could not be read: " + ex.Message, path)]));
        }

        return Task.FromResult(Read(path, text, cancellationToken));
    }

    /// <summary>Maps .xap text. Handles a byte order mark, CRLF and tabs.</summary>
    /// <param name="path">Path stored in the project and in diagnostics.</param>
    /// <param name="text">The file text.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static XapReadResult Read(string path, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        var header = new XapBlock(string.Empty, 0);
        var diagnostics = new List<Diagnostic>();
        if (!Parse(path, text, header, diagnostics, cancellationToken))
        {
            return new XapReadResult(null, diagnostics);
        }

        return new XapReadResult(Map(path, header), diagnostics);
    }

    private static bool Parse(string path, string text, XapBlock root, List<Diagnostic> diagnostics, CancellationToken token)
    {
        var lines = text.TrimStart('\uFEFF').Split('\n');
        var stack = new Stack<XapBlock>();
        stack.Push(root);
        var seenBlock = false;
        for (var i = 0; i < lines.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0)
            {
                continue;
            }

            if (line == "}")
            {
                if (stack.Count == 1)
                {
                    diagnostics.Add(new Diagnostic("ANA070", $"Unbalanced braces: '}}' on line {number} closes nothing.", path));
                    return false;
                }

                stack.Pop();
                continue;
            }

            if (line == "{")
            {
                diagnostics.Add(new Diagnostic("ANA071", $"Line {number}: '{{' without a block name.", path));
                return false;
            }

            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                if (stack.Count == 1 && seenBlock)
                {
                    diagnostics.Add(new Diagnostic("ANA071", $"Line {number}: property outside a block.", path));
                    return false;
                }

                stack.Peek().Properties.Add(new XapProperty(line[..equals].Trim(), line[(equals + 1)..].Trim().TrimEnd(';').Trim(), number));
                continue;
            }

            // A block: "Name" followed by "{" on the next non-empty line, or "Name {" on one line.
            string name;
            if (line.EndsWith('{'))
            {
                name = line[..^1].Trim();
            }
            else
            {
                name = line;
                var next = NextNonEmpty(lines, i + 1);
                if (next < 0 || lines[next].Trim() != "{")
                {
                    diagnostics.Add(new Diagnostic("ANA071", $"Line {number}: expected '{{' after '{name}'.", path));
                    return false;
                }

                i = next;
            }

            var block = new XapBlock(name, number);
            stack.Peek().Children.Add(block);
            stack.Push(block);
            seenBlock = true;
        }

        if (stack.Count > 1)
        {
            diagnostics.Add(new Diagnostic("ANA070", $"Unbalanced braces: block '{stack.Peek().Name}' from line {stack.Peek().Line} is never closed.", path));
            return false;
        }

        return true;
    }

    private static int NextNonEmpty(string[] lines, int from)
    {
        for (var i = from; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length > 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static XapProject Map(string path, XapBlock root)
    {
        var settings = root.Block("Global Settings");
        return new XapProject(
            path,
            root.Get("Signature"),
            Int(root.Get("Version")),
            Int(root.Get("Content Version")),
            Stem(settings?.Get("Windows File")),
            settings?.Blocks("Category").Select(MapCategory).ToList() ?? [],
            settings?.Blocks("Variable").Select(MapVariable).ToList() ?? [],
            settings?.Blocks("RPC").Select(MapRpc).ToList() ?? [],
            root.Blocks("Wave Bank").Select(MapWaveBank).ToList(),
            root.Blocks("Sound Bank").Select(MapSoundBank).ToList(),
            FindUnsupported(root));
    }

    private static XapCategory MapCategory(XapBlock b)
    {
        var limit = b.Block("Instance Limit");
        var fade = limit?.Block("Crossfade");
        return new XapCategory(
            b.Get("Name") ?? string.Empty,
            Flag(b.Get("Public")),
            Flag(b.Get("Background Music")),
            Int(b.Get("Volume")) ?? 0,
            Int(limit?.Get("Max Instances")) ?? 255,
            Int(limit?.Get("Behavior")) ?? 0,
            Int(fade?.Get("Fade In")) ?? 0,
            Int(fade?.Get("Fade Out")) ?? 0,
            Int(fade?.Get("Crossfade Type")) ?? 0,
            b.Blocks("Category Entry").Select(e => e.Get("Name") ?? string.Empty).ToList());
    }

    private static XapVariable MapVariable(XapBlock b) => new(
        b.Get("Name") ?? string.Empty,
        Flag(b.Get("Global")),
        Flag(b.Get("Public")),
        Flag(b.Get("Read Only")),
        Flag(b.Get("Internal")),
        Float(b.Get("Initial Value")),
        Float(b.Get("Min")),
        Float(b.Get("Max")));

    private static XapRpc MapRpc(XapBlock b) => new(
        b.Get("Name") ?? string.Empty,
        b.Blocks("RPC Curve").Select(c => new XapRpcCurve(
            c.Get("Name") ?? string.Empty,
            Int(c.Get("Property")) ?? 0,
            Flag(c.Get("Sound")),
            c.Block("Variable Entry")?.Get("Name") ?? string.Empty,
            c.Blocks("RPC Point").Select(p => new XapRpcPoint(Float(p.Get("X")), Float(p.Get("Y")), Int(p.Get("Curve")) ?? 0)).ToList())).ToList());

    private static XapWaveBank MapWaveBank(XapBlock b) => new(
        b.Get("Name") ?? string.Empty,
        Stem(b.Get("Windows File")),
        Flag(b.Get("Streaming")),
        b.Blocks("Wave").Select(MapWave).ToList());

    private static XapWave MapWave(XapBlock b)
    {
        var cache = b.Block("Cache");
        return new XapWave(
            b.Get("Name") ?? string.Empty,
            b.Get("File") ?? string.Empty,
            Int(cache?.Get("Channels")) ?? 0,
            Int(cache?.Get("Sampling Rate")) ?? 0,
            Long(cache?.Get("Play Region Length")),
            Long(cache?.Get("Loop Region Offset")),
            Long(cache?.Get("Loop Region Length")),
            b.Get("Compression Preset"));
    }

    private static XapSoundBank MapSoundBank(XapBlock b) => new(
        b.Get("Name") ?? string.Empty,
        Stem(b.Get("Windows File")),
        b.Blocks("Sound").Select(MapSound).ToList(),
        b.Blocks("Cue").Select(MapCue).ToList());

    private static XapSound MapSound(XapBlock b) => new(
        b.Get("Name") ?? string.Empty,
        Int(b.Get("Volume")) ?? 0,
        Int(b.Get("Pitch")) ?? 0,
        Int(b.Get("Priority")) ?? 0,
        b.Blocks("Category Entry").Select(e => e.Get("Name") ?? string.Empty).ToList(),
        b.Blocks("RPC Entry").Select(e => e.Get("RPC Name") ?? e.Get("Name") ?? string.Empty).ToList(),
        b.Blocks("Track").Select(MapTrack).ToList());

    private static XapTrack MapTrack(XapBlock b) => new(
        Int(b.Get("Volume")) ?? 0,
        b.Blocks("Play Wave Event").Select(e =>
        {
            var header = e.Block("Event Header");
            return new XapPlayWaveEvent(
                Int(e.Get("Loop Count")) ?? 0,
                Flag(e.Get("Break Loop")),
                Int(header?.Get("Timestamp")) ?? 0,
                Flag(header?.Get("Relative")),
                e.Blocks("Wave Entry").Select(w => new XapWaveEntry(w.Get("Bank Name") ?? string.Empty, w.Get("Entry Name") ?? string.Empty, Int(w.Get("Weight")) ?? 0)).ToList());
        }).ToList());

    private static XapCue MapCue(XapBlock b)
    {
        var variation = b.Block("Variation");
        return new XapCue(
            b.Get("Name") ?? string.Empty,
            Int(variation?.Get("Variation Type")) ?? 0,
            Int(variation?.Get("Variation Table Type")) ?? 0,
            Flag(variation?.Get("New Variation on Loop")),
            b.Blocks("Sound Entry").Select(e => new XapSoundEntry(e.Get("Name") ?? string.Empty, Int(e.Get("Weight Min")) ?? 0, Int(e.Get("Weight Max")) ?? 255)).ToList());
    }

    private static List<XapUnsupported> FindUnsupported(XapBlock root)
    {
        var terms = XactFeatures.Default.ForGroup("unsupported").ToList();
        var found = new List<XapUnsupported>();
        Walk(root, string.Empty, terms, found);
        return found;
    }

    private static void Walk(XapBlock block, string parentPath, List<XactFeature> terms, List<XapUnsupported> found)
    {
        foreach (var property in block.Properties)
        {
            if (Match(property.Key, terms) is { } term)
            {
                found.Add(new XapUnsupported(property.Line, Join(parentPath, block, property.Key), term.Meaning));
            }
        }

        foreach (var child in block.Children)
        {
            if (Match(child.Name, terms) is { } term)
            {
                found.Add(new XapUnsupported(child.Line, Join(parentPath, block, child.Name), term.Meaning));
            }
            else
            {
                Walk(child, Join(parentPath, block, null), terms, found);
            }
        }
    }

    private static XactFeature? Match(string name, List<XactFeature> terms) =>
        terms.FirstOrDefault(t => name.Contains(t.Key, StringComparison.OrdinalIgnoreCase));

    private static string Join(string parentPath, XapBlock block, string? leaf)
    {
        var self = block.Name.Length == 0 ? string.Empty : block.Get("Name") is { } n ? $"{block.Name}[{n}]" : block.Name;
        var path = parentPath.Length == 0 ? self : self.Length == 0 ? parentPath : parentPath + "/" + self;
        return leaf is null ? path : path.Length == 0 ? leaf : path + "/" + leaf;
    }

    private static string? Stem(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        var name = file.Trim().Split(_separators)[^1];
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static bool Flag(string? value) => value is "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static int? Int(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static long Long(string? value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static float Float(string? value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0f;
}
