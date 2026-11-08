using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Safety;

/// <summary>Task 9.2: the analyzer source must not contain write APIs.</summary>
[Trait("Category", "Safety")]
public class WriteApiScanTests
{
    private static string AnalyzerSourceRoot =>
        Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "src", "XnaReborn.Analyzer"));

    [Fact]
    public void Analyzer_source_contains_no_write_or_network_apis()
    {
        var files = Directory.EnumerateFiles(AnalyzerSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToList();
        Assert.True(files.Count > 20, "The scan should see the analyzer source files.");

        var violations = files.SelectMany(f => WriteApiScanner.FindViolations(File.ReadAllText(f), Path.GetFileName(f))).ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Only_ReadOnlyFileSystem_touches_File_Directory_and_FileStream()
    {
        var withIo = Directory.EnumerateFiles(AnalyzerSourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("FileStream(", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["ReadOnlyFileSystem.cs"], withIo);
    }

    [Theory]
    [InlineData("class C { void M() { File.WriteAllText(\"a\", \"b\"); } }")]
    [InlineData("class C { void M() { File.Delete(\"a\"); } }")]
    [InlineData("class C { void M() { System.IO.File.Copy(\"a\", \"b\"); } }")]
    [InlineData("class C { void M() { Directory.CreateDirectory(\"a\"); } }")]
    [InlineData("class C { void M() { Directory.Delete(\"a\", true); } }")]
    [InlineData("class C { void M() { var s = new FileStream(\"a\", FileMode.Create); } }")]
    [InlineData("class C { void M() { var s = new FileStream(\"a\", FileMode.Open, FileAccess.Write); } }")]
    [InlineData("class C { void M() { var s = new FileStream(\"a\", FileMode.Open, FileAccess.Read); } }")]
    [InlineData("class C { void M() { var w = new StreamWriter(\"a\"); } }")]
    [InlineData("class C { void M() { var i = new FileInfo(\"a\"); } }")]
    [InlineData("class C { void M() { var p = Path.GetTempFileName(); } }")]
    [InlineData("class C { void M() { Process.Start(\"cmd\"); } }")]
    [InlineData("class C { void M() { var c = new HttpClient(); } }")]
    [InlineData("class C { void M() { var o = FileOptions.DeleteOnClose; } }")]
    [InlineData("class C { void M() { var e = File.Exists(\"a\"); } }")]
    public void Scanner_flags_write_apis_outside_the_read_only_file_system(string source)
    {
        Assert.NotEmpty(WriteApiScanner.FindViolations(source, "Other.cs"));
    }

    [Theory]
    [InlineData("class C { bool M() => File.Exists(\"a\"); }")]
    [InlineData("class C { bool M() => Directory.Exists(\"a\"); }")]
    [InlineData("class C { object M() => Directory.EnumerateFiles(\"a\"); }")]
    [InlineData("class C { object M() => new FileStream(\"a\", FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }")]
    public void Scanner_accepts_read_only_use_inside_the_read_only_file_system(string source)
    {
        Assert.Empty(WriteApiScanner.FindViolations(source, "ReadOnlyFileSystem.cs"));
    }

    [Theory]
    [InlineData("class C { void M() { File.WriteAllText(\"a\", \"b\"); } }")]
    [InlineData("class C { void M() { File.Delete(\"a\"); } }")]
    [InlineData("class C { void M() { Directory.CreateDirectory(\"a\"); } }")]
    [InlineData("class C { void M() { var s = new FileStream(\"a\", FileMode.Create); } }")]
    public void Scanner_flags_write_apis_even_inside_the_read_only_file_system(string source)
    {
        Assert.NotEmpty(WriteApiScanner.FindViolations(source, "ReadOnlyFileSystem.cs"));
    }

    [Fact]
    public void Scanner_ignores_comments_strings_and_plain_properties()
    {
        const string source = """
            class C
            {
                // File.Delete("a");
                string Text = "File.WriteAllText";
                string File { get; set; } = "";
                int M(C other) => other.File.Length;
            }
            """;

        Assert.Empty(WriteApiScanner.FindViolations(source, "Other.cs"));
    }
}
