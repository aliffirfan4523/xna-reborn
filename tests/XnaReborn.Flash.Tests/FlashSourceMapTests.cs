using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Flash.Converter;

namespace XnaReborn.Flash.Tests;

/// <summary>Task 15.2: converted C# carries #line directives, so compiler messages point at the .as file and line.</summary>
public sealed class FlashSourceMapTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xnareborn-flashmap-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            Directory.Delete(_temp, recursive: true);
        }
    }

    // A copy of flx-hello whose create() calls a function that does not exist. The new call is on line 12 of src/HelloState.as.
    private async Task<FlashMigrationResult> MigrateBroken()
    {
        var source = Path.Combine(_temp, "source");
        CopyDirectory(FlashFixtures.Of("flx-hello"), source);
        var state = Path.Combine(source, "src", "HelloState.as");
        var text = File.ReadAllText(state);
        File.WriteAllText(state, text.Replace("add(text);", "add(text);\n\t\t\tundefinedCall();", StringComparison.Ordinal));
        return await FlashMigrator.MigrateAsync(source, Path.Combine(_temp, "out"));
    }

    [Fact]
    public async Task Compiler_positions_map_back_to_the_as_file_and_line()
    {
        var result = await MigrateBroken();
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(result.OutputFolder, "Game", "HelloState.cs")), new CSharpParseOptions(LanguageVersion.CSharp12));
        var root = tree.GetRoot();

        Assert.Equal(12, Mapped(tree, root.DescendantNodes().OfType<IdentifierNameSyntax>().First(n => n.Identifier.Text == "undefinedCall")).Line);
        Assert.Equal(11, Mapped(tree, root.DescendantNodes().OfType<IdentifierNameSyntax>().First(n => n.Identifier.Text == "add")).Line);
        Assert.Equal(9, Mapped(tree, root.DescendantNodes().OfType<VariableDeclaratorSyntax>().First(n => n.Identifier.Text == "text")).Line);
        Assert.Equal(7, Mapped(tree, root.DescendantNodes().OfType<MethodDeclarationSyntax>().First()).Line);
        Assert.All(root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single().DescendantNodes().OfType<IdentifierNameSyntax>(), n => Assert.Equal("src/HelloState.as", Mapped(tree, n).Path));
    }

    [Fact]
    public async Task Files_without_errors_still_compile_as_valid_syntax_with_directives()
    {
        var result = await Migrate("flx-platformer");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(result.OutputFolder, "Game"), "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.Contains("#line ", text, StringComparison.Ordinal);
            Assert.DoesNotContain(_temp.Replace('\\', '/'), text, StringComparison.OrdinalIgnoreCase);
            var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp12));
            Assert.DoesNotContain(tree.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        }
    }

    [SkippableIntegrationFact]
    public async Task Build_error_names_the_as_file_and_line()
    {
        var result = await MigrateBroken();
        var psi = new ProcessStartInfo("dotnet", $"build \"{Path.Combine(result.OutputFolder, result.ProjectFile)}\" -nologo -v q --disable-build-servers")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = result.OutputFolder,
        };
        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.NotEqual(0, process.ExitCode);
        // The compiler resolves the #line path against the folder of the .cs file, so it prints Game\src\HelloState.as(12,9).
        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(output, @"src[\\/]HelloState\.as\(12,\d+\): error CS0103"), output);
        Assert.Contains("undefinedCall", output, StringComparison.Ordinal);
    }

    private async Task<FlashMigrationResult> Migrate(string fixture) =>
        await FlashMigrator.MigrateAsync(FlashFixtures.Of(fixture), Path.Combine(_temp, "out-" + fixture));

    private static (string Path, int Line) Mapped(SyntaxTree tree, SyntaxNode node)
    {
        var span = tree.GetMappedLineSpan(node.Span);
        return (span.Path, span.StartLinePosition.Line + 1);
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        }

        foreach (var d in Directory.GetDirectories(from))
        {
            CopyDirectory(d, Path.Combine(to, Path.GetFileName(d)));
        }
    }
}
