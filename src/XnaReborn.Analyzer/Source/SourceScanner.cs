using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;
using Diagnostic = XnaReborn.Core.Models.Diagnostic;

namespace XnaReborn.Analyzer.Source;

/// <summary>
/// Parses the C# compile items of a project with Roslyn and applies source rules to them.
/// Reads disk only through <see cref="IReadOnlyFileSystem"/>, so it cannot change the project.
/// </summary>
public sealed class SourceScanner
{
    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly long _maxFileBytes;
    private readonly Func<SyntaxTree, IReadOnlyList<Rule>, IReadOnlyList<SourceMatch>> _matcher;

    /// <summary>Creates a scanner that uses the real rule matcher.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    /// <param name="maxFileBytes">Files larger than this are skipped with an ANA005 diagnostic.</param>
    public SourceScanner(IReadOnlyFileSystem fileSystem, long maxFileBytes = 5 * 1024 * 1024)
        : this(fileSystem, maxFileBytes, SourceRuleMatcher.Match)
    {
    }

    /// <summary>Creates a scanner with a custom matcher. Used by tests.</summary>
    /// <param name="fileSystem">Read-only file system used for all disk access.</param>
    /// <param name="maxFileBytes">Files larger than this are skipped with an ANA005 diagnostic.</param>
    /// <param name="matcher">Finds matches in one syntax tree.</param>
    internal SourceScanner(
        IReadOnlyFileSystem fileSystem,
        long maxFileBytes,
        Func<SyntaxTree, IReadOnlyList<Rule>, IReadOnlyList<SourceMatch>> matcher)
    {
        _fileSystem = fileSystem;
        _maxFileBytes = maxFileBytes;
        _matcher = matcher;
    }

    /// <summary>Scans the C# compile items of one project.</summary>
    /// <param name="project">The project as read by <see cref="ProjectReader"/>.</param>
    /// <param name="projectDirectory">Absolute folder of the project file. Compile items are relative to it.</param>
    /// <param name="root">Absolute discovery root. Locations and diagnostics are relative to it with '/'.</param>
    /// <param name="projectRelativePath">Relative path of the .csproj, stored in each finding.</param>
    /// <param name="sourceRules">Source rules to apply. An empty list returns an empty result without reading files.</param>
    /// <param name="progress">Receives one update after each file, or null.</param>
    /// <param name="cancellationToken">Cancels the scan with <see cref="OperationCanceledException"/>.</param>
    /// <returns>Findings and diagnostics in a deterministic order.</returns>
    public async Task<SourceScanResult> ScanAsync(
        ProjectReadResult project,
        string projectDirectory,
        string root,
        string projectRelativePath,
        IReadOnlyList<Rule> sourceRules,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceRules.Count == 0)
        {
            return new SourceScanResult([], []);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        foreach (var item in project.Info.CompileFiles)
        {
            if (!item.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(projectDirectory, item.Replace('/', Path.DirectorySeparatorChar)));
            if (seen.Add(full))
            {
                files.Add(full);
            }
        }

        var results = new FileScan[files.Count];
        var completed = 0;
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: project.Info.DefineConstants);
        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            parallel,
            (index, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var relative = Relative(root, files[index]);
                results[index] = ScanFile(files[index], relative, options, projectRelativePath, sourceRules);
                var done = Interlocked.Increment(ref completed);
                progress?.Report(new AnalysisProgress("Scanning source", done, files.Count, relative));
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        var ordered = results.OrderBy(r => r.RelativePath, StringComparer.Ordinal).ToList();
        var findings = ordered.SelectMany(r => r.Findings).ToList();
        var diagnostics = ordered
            .SelectMany(r => r.Diagnostics)
            .OrderBy(d => d.File, StringComparer.Ordinal)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ToList();
        return new SourceScanResult(findings, diagnostics);
    }

    private static string Relative(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    private FileScan ScanFile(
        string fullPath,
        string relative,
        CSharpParseOptions options,
        string projectRelativePath,
        IReadOnlyList<Rule> rules)
    {
        var diagnostics = new List<Diagnostic>();
        string text;
        try
        {
            if (!_fileSystem.FileExists(fullPath))
            {
                diagnostics.Add(new Diagnostic("ANA003", $"Compile item points to a missing file: {relative}", relative));
                return new FileScan(relative, [], diagnostics);
            }

            using var stream = _fileSystem.OpenRead(fullPath);
            if (stream.Length > _maxFileBytes)
            {
                diagnostics.Add(new Diagnostic("ANA005", $"Source file skipped because it is larger than {_maxFileBytes} bytes", relative));
                return new FileScan(relative, [], diagnostics);
            }

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new Diagnostic("ANA001", $"Source file could not be read: {ex.Message}", relative));
            return new FileScan(relative, [], diagnostics);
        }

        var tree = CSharpSyntaxTree.ParseText(text, options, relative);
        var firstError = tree.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
        if (firstError is not null)
        {
            var line = firstError.Location.GetLineSpan().StartLinePosition.Line + 1;
            diagnostics.Add(new Diagnostic("ANA004", $"Source file has syntax errors: {firstError.GetMessage()} (line {line})", relative));
        }

        var findings = _matcher(tree, rules)
            .Select((m, i) => (Match: m, Index: i))
            .OrderBy(x => x.Match.Line)
            .ThenBy(x => x.Match.Column)
            .ThenBy(x => x.Index)
            .Select(x => new Finding(
                x.Match.Rule.Id,
                x.Match.Rule.Title,
                x.Match.Rule.Severity,
                x.Match.Rule.Classification,
                projectRelativePath,
                new SourceLocation(relative, x.Match.Line, x.Match.Column),
                x.Match.Snippet))
            .ToList();
        return new FileScan(relative, findings, diagnostics);
    }

    private sealed record FileScan(string RelativePath, IReadOnlyList<Finding> Findings, IReadOnlyList<Diagnostic> Diagnostics);
}
