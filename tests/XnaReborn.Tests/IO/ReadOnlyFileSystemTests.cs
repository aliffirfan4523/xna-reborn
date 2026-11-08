using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using XnaReborn.Analyzer.IO;

namespace XnaReborn.Tests.IO;

/// <summary>Verifies that <see cref="ReadOnlyFileSystem"/> reads disk without changing it.</summary>
public sealed class ReadOnlyFileSystemTests : IDisposable
{
    private static readonly string[] _allowedMembers =
        ["FileExists", "DirectoryExists", "EnumerateFiles", "EnumerateDirectories", "OpenRead", "ReadAllText"];

    private readonly ReadOnlyFileSystem _fs = new();
    private readonly string _root;

    public ReadOnlyFileSystemTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rofs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string P(string name) => Path.Combine(_root, name);

    private static string Sha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public void FileExists_and_DirectoryExists_return_true_for_existing_and_false_for_missing_paths()
    {
        File.WriteAllText(P("present.txt"), "x");
        Directory.CreateDirectory(P("sub"));

        Assert.True(_fs.FileExists(P("present.txt")));
        Assert.False(_fs.FileExists(P("missing.txt")));
        Assert.True(_fs.DirectoryExists(P("sub")));
        Assert.False(_fs.DirectoryExists(P("missing")));
    }

    [Fact]
    public void ReadAllText_reads_UTF8_without_BOM()
    {
        File.WriteAllBytes(P("utf8.txt"), Encoding.UTF8.GetBytes("héllo"));

        Assert.Equal("héllo", _fs.ReadAllText(P("utf8.txt")));
    }

    [Fact]
    public void ReadAllText_strips_UTF8_BOM_from_the_text()
    {
        File.WriteAllBytes(P("utf8bom.txt"), [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("héllo")]);

        Assert.Equal("héllo", _fs.ReadAllText(P("utf8bom.txt")));
    }

    [Fact]
    public void ReadAllText_decodes_UTF16_LE_with_BOM_and_strips_the_BOM()
    {
        File.WriteAllBytes(P("utf16.txt"), [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("héllo")]);

        Assert.Equal("héllo", _fs.ReadAllText(P("utf16.txt")));
    }

    [Fact]
    public void EnumerateFiles_returns_only_direct_children_in_ordinal_order()
    {
        File.WriteAllText(P("b.csproj"), "");
        File.WriteAllText(P("a.csproj"), "");
        File.WriteAllText(P("C.csproj"), "");
        Directory.CreateDirectory(P("nested"));
        File.WriteAllText(Path.Combine(P("nested"), "deep.csproj"), "");

        var files = _fs.EnumerateFiles(_root, "*.csproj");

        Assert.Equal(new[] { P("C.csproj"), P("a.csproj"), P("b.csproj") }, files);
    }

    [Fact]
    public void EnumerateFiles_honors_the_search_pattern()
    {
        File.WriteAllText(P("a.cs"), "");
        File.WriteAllText(P("a.csproj"), "");

        var files = _fs.EnumerateFiles(_root, "*.cs");

        Assert.Equal(new[] { P("a.cs") }, files);
    }

    [Fact]
    public void EnumerateDirectories_returns_only_direct_subdirectories_in_ordinal_order()
    {
        Directory.CreateDirectory(P("b"));
        Directory.CreateDirectory(P("a"));
        Directory.CreateDirectory(P("C"));
        Directory.CreateDirectory(Path.Combine(P("b"), "inner"));
        File.WriteAllText(P("file.txt"), "");

        var dirs = _fs.EnumerateDirectories(_root);

        Assert.Equal(new[] { P("C"), P("a"), P("b") }, dirs);
    }

    [Fact]
    public void OpenRead_returns_a_readable_seekable_stream_that_rejects_writes()
    {
        File.WriteAllText(P("data.txt"), "content");

        using var stream = _fs.OpenRead(P("data.txt"));

        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.True(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[] { 1 }, 0, 1));
    }

    [Fact]
    public void OpenRead_and_ReadAllText_succeed_while_another_handle_holds_the_file_open_for_writing()
    {
        File.WriteAllText(P("locked.txt"), "original");
        using var writer = new FileStream(P("locked.txt"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

        Assert.Equal("original", _fs.ReadAllText(P("locked.txt")));
        using var stream = _fs.OpenRead(P("locked.txt"));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Reading_leaves_the_file_hash_and_LastWriteTimeUtc_unchanged()
    {
        File.WriteAllText(P("stable.txt"), "stable content");
        File.SetLastWriteTimeUtc(P("stable.txt"), new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        var hashBefore = Sha256(P("stable.txt"));
        var timeBefore = File.GetLastWriteTimeUtc(P("stable.txt"));

        _ = _fs.ReadAllText(P("stable.txt"));

        Assert.Equal(hashBefore, Sha256(P("stable.txt")));
        Assert.Equal(timeBefore, File.GetLastWriteTimeUtc(P("stable.txt")));

        using (var stream = _fs.OpenRead(P("stable.txt")))
        {
            stream.CopyTo(Stream.Null);
        }

        Assert.Equal(hashBefore, Sha256(P("stable.txt")));
        Assert.Equal(timeBefore, File.GetLastWriteTimeUtc(P("stable.txt")));
    }

    [Fact]
    public void Interface_exposes_only_the_allowed_read_methods()
    {
        var names = typeof(IReadOnlyFileSystem)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(m => m.Name)
            .ToArray();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.Contains(name, _allowedMembers));
    }

    [Fact]
    public void EnumerateFiles_on_a_missing_directory_throws_DirectoryNotFoundException()
    {
        Assert.Throws<DirectoryNotFoundException>(() => _fs.EnumerateFiles(P("does-not-exist"), "*"));
    }
}
