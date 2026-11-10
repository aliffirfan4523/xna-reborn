using System.Text;

namespace XnaReborn.Migrator.Content;

/// <summary>
/// Writes a <c>Content.mgcb</c> file for the MonoGame content builder (<c>dotnet mgcb</c>). The output is deterministic:
/// the same document always gives the same text, with LF line endings, no BOM and one trailing newline.
/// </summary>
/// <remarks>
/// <para>
/// Format facts below were checked against the real tool, <c>dotnet-mgcb</c> 3.8.6.1 (net8.0, MonoGame.Framework.Content.Pipeline),
/// by the integration tests in <c>MgcbContentBuilderIntegrationTests</c> and by hand-written probe files. The tool's response-file parser is
/// <see href="https://github.com/MonoGame/MonoGame/blob/v3.8.4/Tools/MonoGame.Content.Builder/CommandLineParser.cs">CommandLineParser.cs</see>
/// and the options are in
/// <see href="https://github.com/MonoGame/MonoGame/blob/v3.8.4/Tools/MonoGame.Content.Builder/BuildContent.cs">BuildContent.cs</see>.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Confirmed.</b> <c>dotnet mgcb /@:Content.mgcb</c> reads the file one line at a time. Empty lines and lines that start with
/// <c>#</c> (the <c>#begin</c> markers and the section headings) are ignored. Relative paths resolve against the folder that holds the
/// .mgcb file. LF and CRLF endings, and a file without a BOM, are accepted.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> Output lands in <c>bin/DesktopGL/</c> (or <c>bin/Windows/</c>) because <c>/outputDir:bin/$(Platform)</c> expands
/// <c>$(Platform)</c> to the value of <c>/platform:</c>. The source path keeps its folders, so <c>Textures/hero.png</c> becomes
/// <c>bin/DesktopGL/Textures/hero.xnb</c>.
/// </description></item>
/// <item><description>
/// <b>Corrected.</b> <c>/platform:</c> accepts <c>DesktopGL</c> and <c>Windows</c>. <c>WindowsDX</c> is not a platform value (the tool prints
/// its usage text), so <see cref="MonoGameTarget.WindowsDX"/> is written as <c>Windows</c>.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> <c>/profile:</c> takes <c>Reach</c> or <c>HiDef</c>, <c>/compress:</c> takes <c>True</c> or <c>False</c>, and an empty
/// <c>/config:</c> is valid. The settings reach the output: in the XNB header ("XNB", platform letter, version, flags) the platform
/// letter is <c>d</c> for DesktopGL and <c>w</c> for Windows, and the flag byte is 0x01 for HiDef and 0x00 for Reach, plus 0x40 (LZ4)
/// when <c>/compress:True</c> is set.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> <c>/build:src;dst</c> writes the output under the destination name. The tool replaces the last extension of the
/// destination with <c>.xnb</c>, and adds <c>.xnb</c> when there is none: <c>hero.png;Textures/big.png</c> and <c>hero.png;Textures/big</c>
/// both give <c>Textures/big.xnb</c>. A destination whose name contains dots keeps them (<c>hero.v2.png</c> gives <c>hero.v2.xnb</c>).
/// That is why <see cref="MgcbEntry.DestinationPath"/> carries the source extension: without it a name like <c>hero.v2</c> would lose
/// its last part.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> Paths with spaces are written as they are, for example <c>/build:Sounds/Tropical paradise.wav</c>. The parser takes
/// the whole line as one argument, so there is no quoting. Quote characters are not removed: <c>/build:"Textures/my hero.png"</c> looks for a
/// file whose name contains the quotes and fails with "source file does not exist". The writer therefore never adds quotes.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> <c>/copy:src</c> copies the file unchanged to the same relative place in the output folder, keeping its extension
/// (<c>bin/DesktopGL/readme.txt</c>). <c>/copy:src;dst</c> copies it to <c>dst</c>. The writer uses only the first form.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> <c>/processor:</c> clears all <c>/processorParam:</c> values set before it, so params must come after the processor
/// line of their own entry. The importer is not reset between entries, so the writer writes <c>/importer:</c> and <c>/processor:</c>
/// for every entry. An empty value (<c>/importer:</c> and <c>/processor:</c> with nothing after the colon) is accepted and means
/// "choose by extension"; the writer writes the lines anyway so an earlier entry cannot leak its importer into this one.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> Unknown parameter names are ignored without an error. A known parameter with a bad value fails the build
/// (<c>ColorKeyColor=zzz</c> gives "Could not convert from string(zzz) to Color"). The build then exits with code 1.
/// </description></item>
/// <item><description>
/// <b>Corrected (limit).</b> <c>/processorParam:</c> splits the text after the colon on both <c>=</c> and <c>:</c> and ignores the line unless it
/// gets exactly two parts, without any message. So a value that contains <c>=</c> or <c>:</c> (for example the color form
/// <c>{R:0 G:0 B:0 A:0}</c>, or <c>Defines=A=1</c>) cannot be written. <see cref="FindProblems(MgcbEntry)"/> reports it and
/// <see cref="Write"/> refuses it. Values with commas or spaces such as <c>255,0,255,255</c> are fine.
/// </description></item>
/// <item><description>
/// <b>Corrected (limit).</b> <c>/build:</c> and <c>/copy:</c> split on <c>;</c>, so a path that contains <c>;</c> cannot be written
/// (the tool builds <c>Textures/x</c> from <c>Textures/x;y.png</c>). The check applies to source and destination paths.
/// </description></item>
/// <item><description>
/// <b>Corrected (limit).</b> The parser treats any line that ends with <c>.mgcb</c> as a nested response file, whatever comes before it,
/// and crashes when no such file exists. A <c>/build:</c> or <c>/copy:</c> line whose last path ends in <c>.mgcb</c> cannot be written.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> Two <c>/copy:</c> lines for the same source keep only the later one. Two <c>/build:</c> lines for the same source
/// with different destinations both build, while a later line without a destination replaces an earlier one for the same source.
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> A <c>/reference:</c> path may contain spaces. A relative path resolves against the .mgcb folder. A missing
/// assembly only prints a warning ("Failed to load assembly") and the build goes on; the failure shows up later as "Failed to create
/// processor".
/// </description></item>
/// <item><description>
/// <b>Confirmed.</b> Failed items are counted in the final line ("Build 0 succeeded, 1 failed.") and the process exit code is 1.
/// </description></item>
/// </list>
/// </remarks>
public static class MgcbWriter
{
    private const string _globalHeading = "#----------------------------- Global Properties ----------------------------#";
    private const string _referencesHeading = "#-------------------------------- References --------------------------------#";
    private const string _contentHeading = "#---------------------------------- Content ---------------------------------#";
    private const string _responseFileSuffix = ".mgcb";

    /// <summary>Writes the text of a <c>Content.mgcb</c> file.</summary>
    /// <param name="document">The document. Entries and copy paths may be in any order; the output is sorted.</param>
    /// <returns>The file text: LF line endings, no BOM, one trailing newline.</returns>
    /// <exception cref="ArgumentException">
    /// The document contains something the tool would read wrongly or ignore. The message lists every problem found by
    /// <see cref="FindProblems(MgcbDocument)"/>.
    /// </exception>
    public static string Write(MgcbDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problems = FindProblems(document);
        if (problems.Count > 0)
        {
            throw new ArgumentException("The document cannot be written as a Content.mgcb file: " + string.Join(" ", problems), nameof(document));
        }

        var lines = new List<string>
        {
            _globalHeading,
            string.Empty,
            "/outputDir:bin/$(Platform)",
            "/intermediateDir:obj/$(Platform)",
            "/platform:" + MapPlatform(document.Target),
            "/config:",
            "/profile:" + CanonicalProfile(document.Profile),
            "/compress:" + (document.Compress ? "True" : "False"),
            string.Empty,
            _referencesHeading,
            string.Empty,
        };

        foreach (var reference in document.References)
        {
            lines.Add("/reference:" + reference);
        }

        lines.Add(string.Empty);
        lines.Add(_contentHeading);

        foreach (var entry in document.Entries.OrderBy(OutputName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(OutputName, StringComparer.Ordinal)
                     .ThenBy(e => e.SourcePath, StringComparer.Ordinal)
                     .ThenBy(e => e.Importer, StringComparer.Ordinal)
                     .ThenBy(e => e.Processor, StringComparer.Ordinal)
                     .ThenBy(e => string.Join('\n', e.Params.Select(p => p.Key + "=" + p.Value)), StringComparer.Ordinal))
        {
            lines.Add(string.Empty);
            lines.Add("#begin " + entry.SourcePath);
            lines.AddRange(EntryLines(entry));
        }

        foreach (var path in document.CopyPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ThenBy(p => p, StringComparer.Ordinal))
        {
            lines.Add(string.Empty);
            lines.Add("#begin " + path);
            lines.Add("/copy:" + path);
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>Builds the entry for one converted asset.</summary>
    /// <param name="asset">The asset. Its <see cref="ConvertedAsset.AssetName"/> has no extension.</param>
    /// <param name="mappedParams">The processor parameters from <see cref="ProcessorParamMapper"/>, kept in their order.</param>
    /// <returns>
    /// An entry whose source path is the item's path with '/' separators and no leading "./". The destination path is the asset name plus
    /// the source extension when that differs from the source path, otherwise null.
    /// </returns>
    public static MgcbEntry ToEntry(ConvertedAsset asset, MappedParams mappedParams)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(mappedParams);

        var source = NormalizePath(asset.Item.SourcePath);
        var destination = NormalizePath(asset.AssetName) + ExtensionOf(source);
        return new MgcbEntry(
            source,
            string.Equals(destination, source, StringComparison.Ordinal) ? null : destination,
            asset.MonoGameImporter,
            asset.MonoGameProcessor,
            mappedParams.Params.ToList());
    }

    /// <summary>Maps an XNA graphics profile to the value of <c>/profile:</c>.</summary>
    /// <param name="xnaProfile">The <c>XnaProfile</c> property of the game project, or null.</param>
    /// <returns><c>Reach</c> or <c>HiDef</c>, ignoring case and surrounding spaces. Anything else, including null, gives <c>HiDef</c>.</returns>
    public static string MapProfile(string? xnaProfile) =>
        xnaProfile?.Trim() is { } trimmed && trimmed.Equals("Reach", StringComparison.OrdinalIgnoreCase) ? "Reach" : "HiDef";

    /// <summary>Lists what is wrong with one entry. An empty list means the tool reads the entry as intended.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>One sentence per problem, in a fixed order. Callers can use it to send an asset to manual work instead of failing the whole file.</returns>
    public static IReadOnlyList<string> FindProblems(MgcbEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var problems = new List<string>();
        var name = $"Entry '{entry.SourcePath}'";

        CheckPath(problems, $"{name}: the source path", entry.SourcePath);
        if (entry.DestinationPath is not null)
        {
            CheckPath(problems, $"{name}: the destination path", entry.DestinationPath);
        }

        CheckSingleLine(problems, $"{name}: the importer", entry.Importer);
        CheckSingleLine(problems, $"{name}: the processor", entry.Processor);
        foreach (var (key, value) in entry.Params)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                problems.Add($"{name}: a processor parameter has no name.");
                continue;
            }

            CheckSingleLine(problems, $"{name}: the processor parameter '{key}'", key + "=" + value);
            if (key.Contains('=') || key.Contains(':') || value.Contains('=') || value.Contains(':'))
            {
                problems.Add($"{name}: the processor parameter '{key}' has '=' or ':' in its name or value, which mgcb silently ignores.");
            }
        }

        AddResponseFileProblems(problems, name, EntryLines(entry));
        return problems;
    }

    /// <summary>Lists what is wrong with a document: the problems of its entries, its copy paths and references, and its profile.</summary>
    /// <param name="document">The document.</param>
    /// <returns>One sentence per problem. An empty list means <see cref="Write"/> will succeed.</returns>
    public static IReadOnlyList<string> FindProblems(MgcbDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(document.Profile);
        ArgumentNullException.ThrowIfNull(document.References);
        ArgumentNullException.ThrowIfNull(document.Entries);
        ArgumentNullException.ThrowIfNull(document.CopyPaths);

        var problems = new List<string>();
        if (!document.Profile.Trim().Equals("Reach", StringComparison.OrdinalIgnoreCase)
            && !document.Profile.Trim().Equals("HiDef", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"The profile '{document.Profile}' is not Reach or HiDef.");
        }

        if (!Enum.IsDefined(document.Target))
        {
            problems.Add($"The target '{document.Target}' is not a known MonoGame target.");
        }

        foreach (var reference in document.References)
        {
            CheckSingleLine(problems, $"The reference '{reference}'", reference);
            AddResponseFileProblems(problems, $"The reference '{reference}'", ["/reference:" + reference]);
        }

        foreach (var entry in document.Entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(document));
            problems.AddRange(FindProblems(entry));
        }

        foreach (var path in document.CopyPaths)
        {
            CheckPath(problems, $"The copy path '{path}'", path);
            AddResponseFileProblems(problems, $"The copy path '{path}'", ["/copy:" + path]);
        }

        return problems;
    }

    private static IEnumerable<string> EntryLines(MgcbEntry entry)
    {
        yield return "/importer:" + entry.Importer;
        yield return "/processor:" + entry.Processor;
        foreach (var (key, value) in entry.Params)
        {
            yield return "/processorParam:" + key + "=" + value;
        }

        yield return entry.DestinationPath is null || string.Equals(entry.DestinationPath, entry.SourcePath, StringComparison.Ordinal)
            ? "/build:" + entry.SourcePath
            : "/build:" + entry.SourcePath + ";" + entry.DestinationPath;
    }

    private static string OutputName(MgcbEntry entry) => entry.DestinationPath ?? entry.SourcePath;

    private static string MapPlatform(MonoGameTarget target) => target switch
    {
        MonoGameTarget.DesktopGL => "DesktopGL",
        MonoGameTarget.WindowsDX => "Windows",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown MonoGame target."),
    };

    private static string CanonicalProfile(string profile) =>
        profile.Trim().Equals("Reach", StringComparison.OrdinalIgnoreCase) ? "Reach" : "HiDef";

    private static string NormalizePath(string path)
    {
        var result = path.Replace('\\', '/');
        while (result.StartsWith("./", StringComparison.Ordinal))
        {
            result = result[2..];
        }

        return result;
    }

    private static string ExtensionOf(string path)
    {
        var fileName = path[(path.LastIndexOf('/') + 1)..];
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[dot..] : string.Empty;
    }

    private static void CheckSingleLine(List<string> problems, string what, string value)
    {
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            problems.Add($"{what} contains a line break.");
        }
    }

    private static void CheckPath(List<string> problems, string what, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            problems.Add($"{what} is empty.");
            return;
        }

        CheckSingleLine(problems, what, value);
        if (value.Contains(';'))
        {
            problems.Add($"{what} contains ';', which mgcb reads as the divider between source and destination.");
        }
    }

    private static void AddResponseFileProblems(List<string> problems, string what, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.EndsWith(_responseFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{what}: the line '{line}' ends with '{_responseFileSuffix}', which mgcb reads as a nested response file.");
            }
        }
    }
}
