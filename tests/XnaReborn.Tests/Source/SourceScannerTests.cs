using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Source;

/// <summary>Scanning compile items with <see cref="SourceScanner"/>, using a fake matcher that reports every identifier named Hit.</summary>
public sealed class SourceScannerTests : IDisposable
{
    private const string _projectPath = "App/App.csproj";

    private static readonly Rule _hitRule = new(
        "XNA900",
        "Hit found",
        Severity.Warning,
        Classification.Manual,
        null,
        null,
        new NamespaceMatch("Fake"),
        "Explanation",
        "Resolution",
        null);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "xnareborn-scan-" + Guid.NewGuid().ToString("N"));

    public SourceScannerTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "App"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Findings_carry_relative_location_trimmed_snippet_and_rule_data()
    {
        Write("App/Sub/Game.cs", "class A\n{\n    void M() { Hit(); }\n}\n");

        var result = await ScanAsync(["Sub/Game.cs"]);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("XNA900", finding.RuleId);
        Assert.Equal("Hit found", finding.Title);
        Assert.Equal(Severity.Warning, finding.Severity);
        Assert.Equal(Classification.Manual, finding.Classification);
        Assert.Equal(_projectPath, finding.Project);
        Assert.Equal(new SourceLocation("App/Sub/Game.cs", 3, 16), finding.Location);
        Assert.Equal("void M() { Hit(); }", finding.Snippet);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Define_constants_control_inactive_code()
    {
        Write("App/A.cs", "class A\n{\n#if XBOX\n    int Hit;\n#endif\n}\n");

        var without = await ScanAsync(["A.cs"], defines: []);
        var with = await ScanAsync(["A.cs"], defines: ["XBOX"]);

        Assert.Empty(without.Findings);
        var finding = Assert.Single(with.Findings);
        Assert.Equal(4, finding.Location?.Line);
    }

    [Fact]
    public async Task Missing_compile_item_gives_ANA003_and_other_files_still_scan()
    {
        Write("App/A.cs", "class A { int Hit; }");

        var result = await ScanAsync(["Missing.cs", "A.cs"]);

        Assert.Single(result.Findings);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA003", diagnostic.Code);
        Assert.Equal("App/Missing.cs", diagnostic.File);
        Assert.Equal("Compile item points to a missing file: App/Missing.cs", diagnostic.Message);
    }

    [Fact]
    public async Task Syntax_errors_give_ANA004_and_the_file_is_still_scanned()
    {
        Write("App/Bad.cs", "class A\n{\n    int Hit = ;\n}\n");

        var result = await ScanAsync(["Bad.cs"]);

        Assert.Single(result.Findings);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA004", diagnostic.Code);
        Assert.Equal("App/Bad.cs", diagnostic.File);
        Assert.StartsWith("Source file has syntax errors: ", diagnostic.Message, StringComparison.Ordinal);
        Assert.EndsWith("(line 3)", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_larger_than_the_limit_gives_ANA005_and_is_not_read()
    {
        Write("App/Big.cs", "class A { int Hit; }");
        var fs = new TestFileSystem();

        var result = await ScanAsync(["Big.cs"], maxFileBytes: 5, fileSystem: fs);

        Assert.Empty(result.Findings);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA005", diagnostic.Code);
        Assert.Equal("Source file skipped because it is larger than 5 bytes", diagnostic.Message);
        Assert.Equal(0, fs.BytesRead);
    }

    [Fact]
    public async Task Non_cs_compile_items_are_ignored()
    {
        Write("App/Notes.txt", "Hit");

        var result = await ScanAsync(["Notes.txt", "Missing.txt"]);

        Assert.Empty(result.Findings);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task Duplicate_compile_items_are_scanned_once()
    {
        Write("App/A.cs", "class A { int Hit; }");

        var result = await ScanAsync(["A.cs", "a.CS", "./A.cs"]);

        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task Empty_rule_list_returns_empty_result_and_reads_nothing()
    {
        Write("App/A.cs", "class A { int Hit; }");
        var fs = new TestFileSystem();

        var result = await ScanAsync(["A.cs", "Missing.cs"], rules: [], fileSystem: fs);

        Assert.Empty(result.Findings);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(0, fs.Calls);
    }

    [Fact]
    public async Task Order_is_deterministic_across_many_files()
    {
        var items = new List<string>();
        var expected = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            var name = $"F{i:D2}.cs";
            Write("App/" + name, "class C\n{\n    int Hit; int Hit2; int Hit;\n}\n");
            items.Add(name);
            expected.Add($"App/{name}:3:9");
            expected.Add($"App/{name}:3:28");
        }

        items.Reverse();
        var first = await ScanAsync(items);
        var second = await ScanAsync(items);

        var firstKeys = first.Findings.Select(f => $"{f.Location?.File}:{f.Location?.Line}:{f.Location?.Column}").ToList();
        Assert.Equal(expected, firstKeys);
        Assert.Equal(firstKeys, second.Findings.Select(f => $"{f.Location?.File}:{f.Location?.Line}:{f.Location?.Column}"));
    }

    [Fact]
    public async Task Diagnostics_are_ordered_by_file_then_code()
    {
        Write("App/B.cs", "class B { int Hit = ; }");
        var items = new[] { "Z.cs", "B.cs", "A.cs" };

        var result = await ScanAsync(items);

        Assert.Equal(["App/A.cs", "App/B.cs", "App/Z.cs"], result.Diagnostics.Select(d => d.File));
        Assert.Equal(["ANA003", "ANA004", "ANA003"], result.Diagnostics.Select(d => d.Code));
    }

    [Fact]
    public async Task Progress_reports_every_file_and_reaches_total()
    {
        var items = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            Write($"App/P{i}.cs", "class C { }");
            items.Add($"P{i}.cs");
        }

        var reports = new List<AnalysisProgress>();
        var progress = new ListProgress(reports);
        await ScanAsync(items, progress: progress);

        Assert.Equal(10, reports.Count);
        Assert.All(reports, r =>
        {
            Assert.Equal("Scanning source", r.Stage);
            Assert.Equal(10, r.Total);
            Assert.NotNull(r.Current);
        });
        Assert.Equal(10, reports.Max(r => r.Completed));
        Assert.Equal(Enumerable.Range(1, 10), reports.Select(r => r.Completed).Order());
    }

    [Fact]
    public async Task Cancelled_token_throws_OperationCanceledException()
    {
        Write("App/A.cs", "class A { int Hit; }");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScanAsync(["A.cs"], token: cts.Token));
    }

    [Fact]
    public async Task Byte_order_mark_files_parse()
    {
        File.WriteAllText(Path.Combine(_root, "App", "Bom8.cs"), "class A { int Hit; }", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(Path.Combine(_root, "App", "Bom16.cs"), "class B { int Hit; }", System.Text.Encoding.Unicode);

        var result = await ScanAsync(["Bom8.cs", "Bom16.cs"]);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Findings.Count);
        Assert.All(result.Findings, f => Assert.Equal(15, f.Location?.Column));
        Assert.All(result.Findings, f => Assert.StartsWith("class", f.Snippet, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unreadable_file_gives_ANA001_and_the_scan_continues()
    {
        Write("App/Locked.cs", "class A { int Hit; }");
        Write("App/Open.cs", "class B { int Hit; }");
        var fs = new TestFileSystem { ThrowOn = "Locked.cs" };

        var result = await ScanAsync(["Locked.cs", "Open.cs"], fileSystem: fs);

        var finding = Assert.Single(result.Findings);
        Assert.Equal("App/Open.cs", finding.Location?.File);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("ANA001", diagnostic.Code);
        Assert.Equal("App/Locked.cs", diagnostic.File);
        Assert.StartsWith("Source file could not be read: ", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Scanning_does_not_change_any_file()
    {
        for (var i = 0; i < 5; i++)
        {
            Write($"App/H{i}.cs", $"class C{i}\n{{\n    int Hit;\n}}\n");
        }

        var before = HashAll();
        await ScanAsync(["H0.cs", "H1.cs", "H2.cs", "H3.cs", "H4.cs", "Missing.cs"]);
        var after = HashAll();

        Assert.Equal(before, after);
    }

    private static IReadOnlyList<SourceMatch> FakeMatch(SyntaxTree tree, IReadOnlyList<Rule> rules)
    {
        var text = tree.GetText();
        var matches = new List<SourceMatch>();
        foreach (var token in tree.GetRoot().DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != "Hit")
            {
                continue;
            }

            var position = token.GetLocation().GetLineSpan().StartLinePosition;
            var snippet = text.Lines[position.Line].ToString().Trim();
            foreach (var rule in rules)
            {
                matches.Add(new SourceMatch(rule, position.Line + 1, position.Character + 1, snippet));
            }
        }

        return matches;
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private Dictionary<string, string> HashAll() =>
        Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));

    private Task<SourceScanResult> ScanAsync(
        IEnumerable<string> compileItems,
        IReadOnlyList<string>? defines = null,
        long maxFileBytes = 5 * 1024 * 1024,
        IReadOnlyList<Rule>? rules = null,
        IReadOnlyFileSystem? fileSystem = null,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken token = default)
    {
        var info = new ProjectInfo(
            _projectPath,
            "App",
            true,
            XnaVersion.V40,
            XnaPlatform.Windows,
            null,
            null,
            [],
            compileItems.ToList(),
            defines ?? [],
            [],
            []);
        var project = new ProjectReadResult(info, new Dictionary<string, string>(), [], []);
        var scanner = new SourceScanner(fileSystem ?? new ReadOnlyFileSystem(), maxFileBytes, FakeMatch);
        return scanner.ScanAsync(project, Path.Combine(_root, "App"), _root, _projectPath, rules ?? [_hitRule], progress, token);
    }

    private sealed class ListProgress(List<AnalysisProgress> reports) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value)
        {
            lock (reports)
            {
                reports.Add(value);
            }
        }
    }

    private sealed class TestFileSystem : IReadOnlyFileSystem
    {
        private readonly ReadOnlyFileSystem _inner = new();
        private long _bytesRead;
        private int _calls;

        public string? ThrowOn { get; init; }

        public int Calls => Volatile.Read(ref _calls);

        public long BytesRead => Interlocked.Read(ref _bytesRead);

        public bool FileExists(string path)
        {
            Interlocked.Increment(ref _calls);
            return _inner.FileExists(path);
        }

        public bool DirectoryExists(string path)
        {
            Interlocked.Increment(ref _calls);
            return _inner.DirectoryExists(path);
        }

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern)
        {
            Interlocked.Increment(ref _calls);
            return _inner.EnumerateFiles(directory, searchPattern);
        }

        public IReadOnlyList<string> EnumerateDirectories(string directory)
        {
            Interlocked.Increment(ref _calls);
            return _inner.EnumerateDirectories(directory);
        }

        public Stream OpenRead(string path)
        {
            Interlocked.Increment(ref _calls);
            if (ThrowOn is not null && path.EndsWith(ThrowOn, StringComparison.Ordinal))
            {
                throw new UnauthorizedAccessException("Access denied.");
            }

            return new CountingStream(_inner.OpenRead(path), n => Interlocked.Add(ref _bytesRead, n));
        }

        public string ReadAllText(string path)
        {
            Interlocked.Increment(ref _calls);
            return _inner.ReadAllText(path);
        }
    }

    private sealed class CountingStream(Stream inner, Action<int> onRead) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = inner.Read(buffer, offset, count);
            onRead(n);
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
