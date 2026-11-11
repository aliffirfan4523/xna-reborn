using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Shims;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Sdx;

/// <summary>SharpDX math stand-ins (sharpdx-toolkit task 14): generated only when asked, referenced only by files that need them, and they compile.</summary>
[Trait("Category", "Migrator")]
public sealed class SharpDxShimTests
{
    private static readonly string _output = Path.Combine(Path.GetTempPath(), "xr-sdx-shim-never-written");

    // Just enough of MonoGame for the stand-ins to compile in a test, without a package download.
    private const string _monoGameStub = """
        namespace Microsoft.Xna.Framework
        {
            public struct Vector2 { public float X, Y; public Vector2(float x, float y) { X = x; Y = y; } }
            public struct Vector3 { public float X, Y, Z; public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; } }
            public struct Vector4 { public float X, Y, Z, W; public Vector4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; } }
            public struct Rectangle { public int X, Y, Width, Height; public Rectangle(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; } }
            public struct Color
            {
                public byte R, G, B, A;
                public Color(Vector4 v) { R = (byte)(v.X * 255); G = (byte)(v.Y * 255); B = (byte)(v.Z * 255); A = (byte)(v.W * 255); }
            }
        }
        """;

    private static MigrationOptions Options(bool shims) =>
        new(_output, MonoGameTarget.DesktopGL, shims, DryRun: true, new HashSet<string>(StringComparer.Ordinal));

    private static async Task<MigrationResult> MigrateAsync(MigrationOptions options)
    {
        var path = FixturePaths.Of("sdx-lowlevel");
        var rules = Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules"));
        var catalogs = await RuleCatalogLoader.LoadAllAsync([Path.Combine(rules, "xna-to-monogame.json"), Path.Combine(rules, "sharpdx-toolkit-to-monogame.json")], new ReadOnlyFileSystem(), CancellationToken.None);
        var report = await new ProjectAnalyzer().AnalyzeAsync(path, catalogs, null, CancellationToken.None);
        return await new ProjectMigrator().MigrateAsync(path, report, options, false, null, CancellationToken.None);
    }

    [Fact]
    public void Generate_adds_the_math_file_only_when_asked()
    {
        var generator = new ShimProjectGenerator();

        Assert.DoesNotContain(generator.Generate(MonoGameTarget.DesktopGL), c => c.RelativePath.EndsWith("SharpDxMath.cs", StringComparison.Ordinal));
        var withMath = generator.Generate(MonoGameTarget.DesktopGL, includeSharpDx: true);
        Assert.Contains(withMath, c => c.RelativePath == "XnaReborn.Compat/SharpDxMath.cs" && c.Kind == ChangeKind.Created);
    }

    [Fact]
    public void Scan_finds_the_math_types_in_a_file_that_imports_SharpDX()
    {
        var uses = ShimProjectGenerator.Scan("using SharpDX;\nclass A { RectangleF r; Color4 c; }\n");

        var use = Assert.Single(uses);
        Assert.Equal("Microsoft.Xna.Framework", use.Namespace);
        Assert.Equal(["Color4", "RectangleF"], use.Types);
    }

    [Theory]
    [InlineData("using SharpDX;\nclass A { Vector3 v; }\n")]
    [InlineData("using SharpDX.Toolkit;\nclass A { RectangleF r; }\n")]
    [InlineData("using Microsoft.Xna.Framework;\nclass A { Color c; }\n")]
    public void Scan_finds_nothing_without_the_import_or_without_the_types(string source)
    {
        Assert.Empty(ShimProjectGenerator.Scan(source));
    }

    [Fact]
    public async Task Shims_on_adds_the_library_and_leaves_no_marker_for_RectangleF_and_Color4()
    {
        var result = await MigrateAsync(Options(true));

        Assert.Contains(result.Changes, c => c.RelativePath == "XnaReborn.Compat/SharpDxMath.cs");
        var game = result.Changes.Single(c => c.RelativePath == "Game1.cs").NewText!;
        Assert.DoesNotContain("XNAREBORN: SDX034", game, StringComparison.Ordinal);
        Assert.DoesNotContain("XNAREBORN: SDX035", game, StringComparison.Ordinal);
        Assert.Contains("RectangleF bounds", game, StringComparison.Ordinal);
        Assert.Contains(result.Manifest.Shims!.Uses, u => u.Namespace == "Microsoft.Xna.Framework" && u.Types.Contains("RectangleF"));
        Assert.Contains("<ProjectReference", result.Changes.Single(c => c.RelativePath.EndsWith("SdxLowLevel.csproj", StringComparison.Ordinal)).NewText!, StringComparison.Ordinal);

        // Rules that have no stand-in still get their marker.
        Assert.Contains("XNAREBORN: SDX048", game, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Shims_off_keeps_the_markers_and_adds_no_library()
    {
        var result = await MigrateAsync(Options(false));

        Assert.DoesNotContain(result.Changes, c => c.RelativePath.StartsWith("XnaReborn.Compat/", StringComparison.Ordinal));
        var game = result.Changes.Single(c => c.RelativePath == "Game1.cs").NewText!;
        Assert.Contains("XNAREBORN: SDX034", game, StringComparison.Ordinal);
        Assert.Contains("XNAREBORN: SDX035", game, StringComparison.Ordinal);
    }

    [Fact]
    public void The_stand_ins_compile_and_behave()
    {
        var sources = new[] { ShimSharpDxSources.Source, _monoGameStub }
            .Select(text => CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp12)));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "XnaReborn.Compat.SharpDxTestBuild",
            sources,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        var problems = emitted.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.True(emitted.Success && problems.Count == 0, string.Join(Environment.NewLine, problems));

        var assembly = System.Reflection.Assembly.Load(stream.ToArray());
        var rect = assembly.GetType("Microsoft.Xna.Framework.RectangleF", throwOnError: true)!;
        var made = Activator.CreateInstance(rect, 1f, 2f, 10f, 20f)!;
        Assert.Equal(11f, rect.GetProperty("Right")!.GetValue(made));
        Assert.Equal(22f, rect.GetProperty("Bottom")!.GetValue(made));
        Assert.True((bool)rect.GetMethod("Contains", [typeof(float), typeof(float)])!.Invoke(made, [5f, 5f])!);
        Assert.False((bool)rect.GetMethod("Contains", [typeof(float), typeof(float)])!.Invoke(made, [11f, 5f])!);
    }
}
