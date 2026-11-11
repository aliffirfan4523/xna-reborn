using XnaReborn.Runner.Diagnostics;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Runner;

/// <summary>Task 5.2 and 5.4: the MSBuild and content builder output parsers, on real samples and on edge cases.</summary>
[Trait("Category", "Runner")]
public sealed class OutputParserTests
{
    private static string SamplesFolder { get; } =
        Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "XnaReborn.Tests", "Runner", "Samples"));

    public static IEnumerable<object[]> SampleNames() =>
    [
        ["build-cs-errors"],
        ["build-mgcb-errors"],
        ["build-mgcb-font-missing"],
        ["build-mgcb-source-missing"],
        ["build-mgcb-xml-error"],
        ["build-restore-error"],
        ["build-vb-errors"],
    ];

    private static string ReadSample(string name) => File.ReadAllText(Path.Combine(SamplesFolder, name + ".txt"));

    [Theory]
    [MemberData(nameof(SampleNames))]
    public Task Real_sample_is_parsed(string name)
    {
        var raw = ReadSample(name);
        return Verifier.Verify(new
        {
            MsBuild = MsBuildOutputParser.Parse(raw).Select(Describe),
            Mgcb = MgcbOutputParser.Parse(raw).Select(Describe),
        }).UseParameters(name);
    }

    // Spells out the severity, which Verify would leave out for the default enum value.
    private static object Describe(BuildDiagnostic d) => new
    {
        Severity = d.Severity.ToString(),
        d.Code,
        d.Message,
        d.File,
        d.Line,
        d.Column,
        d.Project,
        d.AssetPath,
    };

    [Fact]
    public void Samples_were_made_portable()
    {
        foreach (var path in Directory.EnumerateFiles(SamplesFolder, "*.txt"))
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("xr-r3", text);
            Assert.DoesNotContain(@"C:\Users", text);
            Assert.DoesNotContain("\r", text);
        }
    }

    [Fact]
    public void Compiler_error_has_all_parts()
    {
        var result = MsBuildOutputParser.Parse(
            @"C:\x\Game1.cs(42,17): error CS0246: The type or namespace name 'Foo' could not be found [C:\x\Game.csproj]");

        var d = Assert.Single(result);
        Assert.Equal(BuildDiagnosticSeverity.Error, d.Severity);
        Assert.Equal("CS0246", d.Code);
        Assert.Equal("The type or namespace name 'Foo' could not be found", d.Message);
        Assert.Equal(@"C:\x\Game1.cs", d.File);
        Assert.Equal(42, d.Line);
        Assert.Equal(17, d.Column);
        Assert.Equal(@"C:\x\Game.csproj", d.Project);
        Assert.Null(d.AssetPath);
    }

    [Fact]
    public void Warning_is_a_warning()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(
            @"/home/u/x/Game1.cs(7,9): warning CS0168: The variable 'e' is declared but never used [/home/u/x/Game.csproj]"));

        Assert.Equal(BuildDiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("CS0168", d.Code);
        Assert.Equal("/home/u/x/Game1.cs", d.File);
        Assert.Equal("/home/u/x/Game.csproj", d.Project);
    }

    [Fact]
    public void Project_level_error_has_no_line_and_column()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(
            @"C:\x\Game.csproj : error NU1101: Unable to find package Foo. No packages exist with this id in source(s): nuget.org"));

        Assert.Equal("NU1101", d.Code);
        Assert.Equal(@"C:\x\Game.csproj", d.File);
        Assert.Null(d.Line);
        Assert.Null(d.Column);
        Assert.Null(d.Project);
        Assert.EndsWith("nuget.org", d.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tool_name_origin_is_not_a_file()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse("MSBUILD : error MSB1009: Project file does not exist."));

        Assert.Equal("MSB1009", d.Code);
        Assert.Null(d.File);
        Assert.Equal("Project file does not exist.", d.Message);
    }

    [Fact]
    public void Error_without_origin_is_read()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse("error CS5001: Program does not contain a static 'Main' method suitable for an entry point [C:\\x\\Game.csproj]"));

        Assert.Equal("CS5001", d.Code);
        Assert.Null(d.File);
        Assert.Equal(@"C:\x\Game.csproj", d.Project);
    }

    [Fact]
    public void Line_only_position_is_read()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(@"C:\x\Game.csproj(12): error MSB4025: The project file could not be loaded."));

        Assert.Equal(12, d.Line);
        Assert.Null(d.Column);
    }

    [Fact]
    public void End_position_is_ignored()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(@"C:\x\A.cs(3,4,5,6): error CS1002: ; expected [C:\x\G.csproj]"));

        Assert.Equal(3, d.Line);
        Assert.Equal(4, d.Column);
    }

    [Fact]
    public void Minimal_verbosity_prefixes_are_removed()
    {
        var result = MsBuildOutputParser.Parse(
            "  1>C:\\x\\A.cs(1,2): error CS1002: ; expected [C:\\x\\G.csproj]\n12:3>C:\\x\\B.cs(1,2): warning CS0219: unused [C:\\x\\G.csproj]\n   C:\\x\\C.cs(5,6): error CS1003: Syntax error [C:\\x\\G.csproj]");

        Assert.Equal(3, result.Count);
        Assert.Equal([@"C:\x\A.cs", @"C:\x\B.cs", @"C:\x\C.cs"], result.Select(d => d.File));
    }

    [Fact]
    public void Target_framework_suffix_is_not_part_of_the_project()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(@"C:\x\A.cs(1,2): error CS1002: ; expected [C:\x\G.csproj::TargetFramework=net8.0]"));

        Assert.Equal(@"C:\x\G.csproj", d.Project);
        Assert.Equal("; expected", d.Message);
    }

    [Fact]
    public void Brackets_that_are_not_a_project_stay_in_the_message()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(@"C:\x\A.cs(1,2): error CS0103: The name 'a[0]' does not exist [in this context]"));

        Assert.Equal("The name 'a[0]' does not exist [in this context]", d.Message);
        Assert.Null(d.Project);
    }

    [Fact]
    public void Path_with_parentheses_and_spaces_is_read()
    {
        var d = Assert.Single(MsBuildOutputParser.Parse(@"C:\Program Files (x86)\My Game\A.cs(3,4): error CS1002: ; expected [C:\Program Files (x86)\My Game\G.csproj]"));

        Assert.Equal(@"C:\Program Files (x86)\My Game\A.cs", d.File);
        Assert.Equal(3, d.Line);
        Assert.Equal(@"C:\Program Files (x86)\My Game\G.csproj", d.Project);
    }

    [Fact]
    public void Lines_that_are_not_diagnostics_are_ignored()
    {
        var result = MsBuildOutputParser.Parse(
            "Build FAILED.\n    0 Warning(s)\n    1 Error(s)\n\nTime Elapsed 00:00:01.52\n  Determining projects to restore...\nThis is not an error: really.\n");

        Assert.Empty(result);
    }

    [Fact]
    public void Empty_output_gives_no_diagnostics()
    {
        Assert.Empty(MsBuildOutputParser.Parse(string.Empty));
        Assert.Empty(MgcbOutputParser.Parse(string.Empty));
    }

    [Fact]
    public void Null_output_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => MsBuildOutputParser.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => MgcbOutputParser.Parse(null!));
    }

    [Fact]
    public void All_line_endings_are_understood()
    {
        const string One = "A.cs(1,2): error CS1002: x";
        const string Two = "B.cs(1,2): error CS1002: y";

        foreach (var separator in new[] { "\n", "\r\n", "\r" })
        {
            Assert.Equal(2, MsBuildOutputParser.Parse(One + separator + Two + separator).Count);
        }
    }

    [Fact]
    public void Msbuild_errors_printed_twice_are_kept_twice_by_the_parser()
    {
        var raw = ReadSample("build-cs-errors");

        Assert.Equal(2, MsBuildOutputParser.Parse(raw).Count);
    }

    [Fact]
    public void Content_error_is_tagged_with_the_asset_path()
    {
        var raw = ReadSample("build-mgcb-font-missing");

        var d = Assert.Single(MgcbOutputParser.Parse(raw).Where(x => x.Code == "MGCB").DistinctBy(x => x.Message));
        Assert.Equal(BuildDiagnosticSeverity.Error, d.Severity);
        Assert.Equal("<root>/Content/Fonts/Font.spritefont", d.AssetPath);
        Assert.Equal(d.AssetPath, d.File);
        Assert.Contains("FontDescriptionProcessor", d.Message, StringComparison.Ordinal);
        Assert.Contains("System.IO.FileNotFoundException", d.Message, StringComparison.Ordinal);
        Assert.Contains("NoSuchFontXyz", d.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("FontDescriptionProcessor.cs:line", d.Message, StringComparison.Ordinal);
        Assert.EndsWith("Xna40Basic.csproj", d.Project, StringComparison.Ordinal);
    }

    [Fact]
    public void Importer_failure_line_gives_an_asset_diagnostic()
    {
        var d = Assert.Single(MgcbOutputParser.Parse(ReadSample("build-mgcb-errors")), x => x.Code == "MGCB");

        Assert.Equal("<root>/Content/Textures/hero.png", d.AssetPath);
        Assert.Equal("TextureImporter failed to load '<root>/Content/Textures/hero.png': unknown image type", d.Message);
    }

    [Fact]
    public void Position_inside_a_content_file_is_read()
    {
        var d = Assert.Single(MgcbOutputParser.Parse(ReadSample("build-mgcb-xml-error")), x => x.Code == "MGCB");

        Assert.Equal("<root>/Content/Fonts/Font.spritefont", d.AssetPath);
        Assert.Equal(1, d.Line);
        Assert.Equal(41, d.Column);
        Assert.Equal("Could not resolve type 'Graphics:FontDescription'.", d.Message);
    }

    [Fact]
    public void Content_warning_is_a_warning()
    {
        var d = Assert.Single(MgcbOutputParser.Parse("C:/g/Content/Sounds/a.wav : warning : The sample rate is unusual. [C:\\g\\G.csproj]"));

        Assert.Equal(BuildDiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("MGCB", d.Code);
        Assert.Equal("C:/g/Content/Sounds/a.wav", d.AssetPath);
        Assert.Equal("The sample rate is unusual.", d.Message);
    }

    [Fact]
    public void Content_task_failure_line_is_one_diagnostic_even_when_both_parsers_run()
    {
        var raw = ReadSample("build-mgcb-errors");

        var fromMgcb = MgcbOutputParser.Parse(raw).Where(d => d.Code == "MSB3073").ToList();
        var fromMsBuild = MsBuildOutputParser.Parse(raw).Where(d => d.Code == "MSB3073").ToList();

        Assert.Equal(2, fromMgcb.Count);
        Assert.Equal(fromMsBuild, fromMgcb);
        var all = MsBuildOutputParser.Parse(raw).Concat(MgcbOutputParser.Parse(raw));
        Assert.Single(DiagnosticGrouper.Group(all, _ => null), g => g.Items[0].Code == "MSB3073");
        Assert.Single(DiagnosticGrouper.Group(all, _ => null).SelectMany(g => g.Items), d => d.Code == "MSB3073");
        Assert.Contains("exited with code 1", fromMgcb[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Other_msbuild_command_failures_are_not_content_errors()
    {
        var raw = @"C:\x\G.csproj(9,5): error MSB3073: The command ""npm run build"" exited with code 1. [C:\x\G.csproj]";

        Assert.Empty(MgcbOutputParser.Parse(raw));
        Assert.Single(MsBuildOutputParser.Parse(raw));
    }

    [Fact]
    public void Compiler_errors_are_not_content_errors()
    {
        var raw = ReadSample("build-cs-errors");

        Assert.Empty(MgcbOutputParser.Parse(raw));
        Assert.Empty(MgcbOutputParser.Parse("C:\\x\\A.cs(1,2): error CS1002: ; expected [C:\\x\\G.csproj]"));
        Assert.Empty(MgcbOutputParser.Parse("C:\\x\\A.cs : error : something odd"));
    }

    [Fact]
    public void Restore_error_is_not_a_content_error()
    {
        Assert.Empty(MgcbOutputParser.Parse(ReadSample("build-restore-error")));
    }

    [Fact]
    public void Codeless_lines_are_not_msbuild_diagnostics()
    {
        Assert.Empty(MsBuildOutputParser.Parse(ReadSample("build-mgcb-source-missing").Split('\n').First(l => l.Contains(": error : The source", StringComparison.Ordinal))));
    }

    [Fact]
    public void Information_lines_from_the_content_builder_are_ignored()
    {
        Assert.Empty(MgcbOutputParser.Parse("  I: C:/g/Content/a.png\n  I: \tBuilding Font C:\\WINDOWS\\Fonts\\arial.ttf\n  E: C:/g/Content/b.png\n"));
    }
}
