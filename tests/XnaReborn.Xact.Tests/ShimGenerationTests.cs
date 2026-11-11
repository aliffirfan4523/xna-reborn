using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer.Xact;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Audio;
using XnaReborn.Migrator.Shims;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Xact.Tests;

/// <summary>The shim as the migrator writes it: embedded files, the generator, and a build of the output against MonoGame.</summary>
public sealed class ShimGenerationTests
{
    private static readonly string[] _expectedFiles =
    [
        "AudioCategory.cs", "AudioEngine.cs", "Cue.cs", "SoundBank.cs", "WaveBank.cs", "XactData.cs", "XactRuntime.cs",
    ];

    private static string FixtureXap()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "fixtures", "xna40-xact", "Content", "Sounds.xap");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The xna40-xact fixture was not found above " + AppContext.BaseDirectory);
    }

    private static string FixtureCatalog()
    {
        var path = FixtureXap();
        var project = XapReader.Read(path, File.ReadAllText(path)).Project!;
        var plan = XactPlanner.Plan(project, string.Empty, _ => true);
        return XactCatalogWriter.Write([(project, plan)], "Content").Text;
    }

    [Fact]
    public void The_embedded_files_are_the_shim_sources_in_name_order_with_lf_line_endings()
    {
        Assert.Equal(_expectedFiles, XactShimSources.Files.Select(f => f.Name));
        Assert.All(XactShimSources.Files, f =>
        {
            Assert.DoesNotContain("\r", f.Text, StringComparison.Ordinal);
            Assert.Contains("namespace XnaReborn.Compat.Xact", f.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void The_generator_adds_the_xact_files_and_the_catalog_only_when_asked()
    {
        var generator = new ShimProjectGenerator();

        var without = generator.Generate(MonoGameTarget.DesktopGL);
        var with = generator.Generate(MonoGameTarget.DesktopGL, xactCatalog: "// catalog\n");

        Assert.DoesNotContain(without, c => c.RelativePath.Contains("/Xact/", StringComparison.Ordinal));
        Assert.Equal(
            _expectedFiles.Select(f => "XnaReborn.Compat/Xact/" + f).Append("XnaReborn.Compat/Xact/XactCatalog.g.cs"),
            with.Where(c => c.RelativePath.Contains("/Xact/", StringComparison.Ordinal)).Select(c => c.RelativePath));
        Assert.Equal("// catalog\n", with.Single(c => c.RelativePath.EndsWith("XactCatalog.g.cs", StringComparison.Ordinal)).NewText);
        Assert.Equal(without.Select(c => c.RelativePath), with.Take(without.Count).Select(c => c.RelativePath));
    }

    [Fact]
    public void The_generated_files_are_the_same_every_time()
    {
        var generator = new ShimProjectGenerator();

        var first = generator.Generate(MonoGameTarget.DesktopGL, xactCatalog: FixtureCatalog());
        var second = generator.Generate(MonoGameTarget.DesktopGL, xactCatalog: FixtureCatalog());

        Assert.Equal(first.Select(c => (c.RelativePath, c.NewText)), second.Select(c => (c.RelativePath, c.NewText)));
    }

    [Fact]
    public void The_generated_shim_and_the_fixture_catalog_build_against_MonoGame_without_warnings()
    {
        var files = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, xactCatalog: FixtureCatalog())
            .Where(c => c.RelativePath.StartsWith("XnaReborn.Compat/Xact/", StringComparison.Ordinal))
            .Select(c => CSharpSyntaxTree.ParseText(c.NewText!, new CSharpParseOptions(LanguageVersion.CSharp12), path: c.RelativePath));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) != "XnaReborn.Xact.Tests.dll") // it holds its own copy of the shim sources
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "XnaReborn.Compat.Check",
            files,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var problems = compilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToList();

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_fixture_game_with_aliases_next_to_the_audio_using_builds_against_the_stand_in()
    {
        // Risk 8 of the design: an alias must win over the type that `using Microsoft.Xna.Framework.Audio;` imports.
        var game = File.ReadAllText(Path.Combine(Path.GetDirectoryName(FixtureXap())!, "..", "Game1.cs"));
        var rewritten = new XactAliasRewriter().Rewrite(new SourceRewriteContext("Game1.cs", game, ["WINDOWS"])).Text;
        Assert.Contains("using Microsoft.Xna.Framework.Audio;", rewritten, StringComparison.Ordinal);
        Assert.Contains("using Cue = XnaReborn.Compat.Xact.Cue;", rewritten, StringComparison.Ordinal);
        var shim = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, xactCatalog: FixtureCatalog())
            .Where(c => c.RelativePath.StartsWith("XnaReborn.Compat/Xact/", StringComparison.Ordinal))
            .Select(c => c.NewText!)
            .Append(rewritten);

        var errors = Compile(shim).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();
        var original = Compile(shim.SkipLast(1).Append(game)).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToList();

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        Assert.True(original.Count == 0, "The unrewritten game must also build, as a control: " + string.Join(Environment.NewLine, original));
    }

    [Fact]
    public void Without_the_alias_the_audio_using_and_the_stand_in_namespace_make_the_names_ambiguous()
    {
        const string Probe = "using Microsoft.Xna.Framework.Audio;\nusing XnaReborn.Compat.Xact;\nclass P { Cue c; }\n";
        var shim = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL, xactCatalog: FixtureCatalog())
            .Where(c => c.RelativePath.StartsWith("XnaReborn.Compat/Xact/", StringComparison.Ordinal))
            .Select(c => c.NewText!);

        var plain = Compile(shim.Append(Probe)).Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.Id).ToList();
        var aliased = Compile(shim.Append(Probe.Replace("class P", "using Cue = XnaReborn.Compat.Xact.Cue;\nclass P", StringComparison.Ordinal))).Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        Assert.Contains("CS0104", plain);
        Assert.Empty(aliased);
    }

    private static IReadOnlyList<Diagnostic> Compile(IEnumerable<string> sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) != "XnaReborn.Xact.Tests.dll")
            .Select(p => MetadataReference.CreateFromFile(p));
        return CSharpCompilation.Create(
                "Check",
                sources.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.CSharp12))),
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .GetDiagnostics();
    }

    [Fact]
    public void The_catalog_data_types_match_the_contract_the_catalog_writer_compiles_against()
    {
        // The contract text lists the constructor parameter types in order; the shim records must take the same.
        var contract = CSharpSyntaxTree.ParseText(XactCatalogContract.Source).GetRoot();
        var contractCtors = contract.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax>()
            .ToDictionary(c => c.Identifier.ValueText, c => string.Join(",", c.ParameterList.Parameters.Select(p => p.Type!.ToString())));
        var shim = typeof(Compat.Xact.XactCatalog).Assembly;

        foreach (var (name, parameters) in contractCtors)
        {
            var type = shim.GetType("XnaReborn.Compat.Xact." + name, throwOnError: true)!;
            var actual = type.GetConstructors().Single(c => c.GetParameters().Length == parameters.Split(',').Length)
                .GetParameters().Select(p => Alias(p.ParameterType));
            Assert.Equal(parameters, string.Join(",", actual));
        }
    }

    private static string Alias(Type type) =>
        type == typeof(string) ? "string"
        : type == typeof(float) ? "float"
        : type == typeof(bool) ? "bool"
        : type == typeof(int) ? "int"
        : type.IsArray ? Alias(type.GetElementType()!) + "[]"
        : type.Name;
}
