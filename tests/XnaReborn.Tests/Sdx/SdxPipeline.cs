using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Migrator.Source;
using XnaReborn.Tests.Device;

namespace XnaReborn.Tests.Sdx;

/// <summary>Runs the default rewriters of the SharpDX Toolkit rules over source text and, when MonoGame is in the NuGet cache, compiles the result.</summary>
internal static class SdxPipeline
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    /// <summary>Every rule ID of the Toolkit catalog that has a rewriter in the default registry.</summary>
    internal static IReadOnlySet<string> SdxRuleIds { get; } =
        DefaultRewriters.Create().RuleIds.Where(id => id.StartsWith("SDX", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);

    /// <summary>Rewrites one file with every SDX rewriter, the way the migrator does for a project where all those rules fired.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="path">The relative file path.</param>
    internal static FileRewriteResult Rewrite(string text, string path = "Game1.cs") =>
        new SourceRewriteEngine(DefaultRewriters.Create()).Rewrite(path, text, _symbols, SdxRuleIds);

    /// <summary>Compiles C# files against the MonoGame assembly of the NuGet cache and returns the errors only.</summary>
    /// <param name="files">File name and text pairs.</param>
    internal static IReadOnlyList<string> CompileErrors(params (string Name, string Text)[] files)
    {
        var path = MonoGameFactAttribute.Path() ?? throw new InvalidOperationException("MonoGame is not in the NuGet cache.");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(path)
            .Select(p => MetadataReference.CreateFromFile(p));
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Text, new CSharpParseOptions(LanguageVersion.CSharp12), f.Name));
        var compilation = CSharpCompilation.Create(
            "SdxMigrated",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
        using var stream = new MemoryStream();
        return compilation.Emit(stream).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
    }
}
