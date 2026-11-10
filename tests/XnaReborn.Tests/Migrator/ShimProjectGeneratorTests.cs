using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Shims;

namespace XnaReborn.Tests.Migrator;

/// <summary>The shim generator writes a small offline-stand-in library, and the generated code compiles and keeps to its folder.</summary>
[Trait("Category", "Migrator")]
public sealed class ShimProjectGeneratorTests
{
    // Just enough of MonoGame for the shim sources to compile in a test, without a package download.
    private const string _monoGameStub = """
        namespace Microsoft.Xna.Framework
        {
            public enum PlayerIndex { One, Two, Three, Four }
            public class Game { }
            public class GameComponent { public GameComponent(Game game) { } }
        }
        """;

    private static readonly Lazy<Assembly> _compat = new(CompileShims);

    private readonly ShimProjectGenerator _generator = new();

    [Fact]
    public void Generates_four_created_files_marked_as_shim()
    {
        var changes = _generator.Generate(MonoGameTarget.DesktopGL);

        Assert.Equal(
            [
                "XnaReborn.Compat/XnaReborn.Compat.csproj",
                "XnaReborn.Compat/CompletedAsyncResult.cs",
                "XnaReborn.Compat/GamerServices.cs",
                "XnaReborn.Compat/Storage.cs",
            ],
            changes.Select(c => c.RelativePath));
        Assert.All(changes, c =>
        {
            Assert.Equal(ChangeKind.Created, c.Kind);
            Assert.StartsWith("Shim:", c.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain('\r', c.NewText!);
            Assert.EndsWith("\n", c.NewText!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Project_targets_the_chosen_MonoGame_package()
    {
        var gl = _generator.Generate(MonoGameTarget.DesktopGL)[0].NewText!;
        var dx = _generator.Generate(MonoGameTarget.WindowsDX)[0].NewText!;

        Assert.Contains("MonoGame.Framework.DesktopGL", gl, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", gl, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Framework.WindowsDX", dx, StringComparison.Ordinal);
        Assert.Contains("net8.0-windows", dx, StringComparison.Ordinal);
    }

    [Fact]
    public void Types_keep_their_original_XNA_namespaces()
    {
        var storage = _generator.Generate(MonoGameTarget.DesktopGL)[3].NewText!;
        var gamer = _generator.Generate(MonoGameTarget.DesktopGL)[2].NewText!;

        Assert.Contains("namespace Microsoft.Xna.Framework.Storage", storage, StringComparison.Ordinal);
        Assert.Contains("namespace Microsoft.Xna.Framework.GamerServices", gamer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Compat_project_matches_snapshot() =>
        await Verifier.Verify(_generator.Generate(MonoGameTarget.DesktopGL)[0].NewText!, "xml");

    [Fact]
    public async Task Storage_source_matches_snapshot() =>
        await Verifier.Verify(_generator.Generate(MonoGameTarget.DesktopGL)[3].NewText!, "txt");

    [Fact]
    public async Task GamerServices_source_matches_snapshot() =>
        await Verifier.Verify(_generator.Generate(MonoGameTarget.DesktopGL)[2].NewText!, "txt");

    [Fact]
    public void Scan_finds_shimmed_namespaces_and_types()
    {
        var uses = ShimProjectGenerator.Scan("using Microsoft.Xna.Framework.Storage;\nusing Microsoft.Xna.Framework.GamerServices;\nclass A { StorageDevice d; void M() { Guide.ShowSignIn(1, false); } }");

        Assert.Equal(["Microsoft.Xna.Framework.GamerServices", "Microsoft.Xna.Framework.Storage"], uses.Select(u => u.Namespace));
        Assert.Equal(["Guide"], uses[0].Types);
        Assert.Equal(["StorageDevice"], uses[1].Types);
    }

    [Fact]
    public void Scan_finds_nothing_in_plain_xna_code()
    {
        Assert.Empty(ShimProjectGenerator.Scan("using Microsoft.Xna.Framework;\nusing Microsoft.Xna.Framework.Graphics;\nclass A { }"));
    }

    [Fact]
    public void AddToProject_references_the_library_from_a_subfolder_project()
    {
        var csproj = new FileChange("Game/Game.csproj", ChangeKind.Created, null, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n", null);

        var result = ShimProjectGenerator.AddToProject(csproj, "Game", reference: true);

        Assert.Contains("<ProjectReference Include=\"..\\XnaReborn.Compat\\XnaReborn.Compat.csproj\" />", result.NewText, StringComparison.Ordinal);
        Assert.DoesNotContain("Compile Remove", result.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public void AddToProject_keeps_a_root_project_from_compiling_the_library_sources()
    {
        var csproj = new FileChange("Game.csproj", ChangeKind.Created, null, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n", null);

        var result = ShimProjectGenerator.AddToProject(csproj, string.Empty, reference: true);

        Assert.Contains("<Compile Remove=\"XnaReborn.Compat\\**\" />", result.NewText, StringComparison.Ordinal);
        Assert.Contains("<ProjectReference Include=\"XnaReborn.Compat\\XnaReborn.Compat.csproj\" />", result.NewText, StringComparison.Ordinal);
    }

    [Fact]
    public void AddToProject_leaves_an_unrelated_subfolder_project_alone()
    {
        var csproj = new FileChange("Game/Game.csproj", ChangeKind.Created, null, "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>\n", null);

        Assert.Same(csproj, ShimProjectGenerator.AddToProject(csproj, "Game", reference: false));
    }

    [Fact]
    public void Generated_sources_compile_without_warnings()
    {
        Assert.NotNull(_compat.Value);
    }

    [Theory]
    [InlineData("..\\outside.dat")]
    [InlineData("a/../../outside.dat")]
    [InlineData("a/..")]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\win.ini")]
    [InlineData("C:\\windows\\win.ini")]
    [InlineData("C:win.ini")]
    [InlineData("\\\\server\\share\\x")]
    [InlineData("file.txt:stream")]
    [InlineData("")]
    public void Storage_rejects_paths_that_leave_the_container(string path)
    {
        var error = Assert.Throws<ArgumentException>(() => Resolve(path, allowRoot: false));
        Assert.NotNull(error.Message);
    }

    [Theory]
    [InlineData("save.dat")]
    [InlineData("Saves\\slot1.sav")]
    [InlineData("Saves/slot1.sav")]
    [InlineData("./a/./b.txt")]
    public void Storage_keeps_relative_paths_inside_the_container(string path)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xr-container"));

        var full = Resolve(path, allowRoot: false, root);

        Assert.StartsWith(root + Path.DirectorySeparatorChar, full, StringComparison.Ordinal);
    }

    [Fact]
    public void Storage_allows_the_container_folder_itself_only_when_listing()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xr-container"));

        Assert.Equal(root, Resolve(string.Empty, allowRoot: true, root));
        Assert.Throws<ArgumentException>(() => Resolve(".", allowRoot: false, root));
    }

    [Fact]
    public void Offline_profile_and_guide_return_fixed_values_and_never_block()
    {
        var gamer = Type("Microsoft.Xna.Framework.GamerServices.Gamer");
        var signedIn = (System.Collections.IList)gamer.GetProperty("SignedInGamers")!.GetValue(null)!;
        var guide = Type("Microsoft.Xna.Framework.GamerServices.Guide");

        Assert.Single(signedIn);
        Assert.Equal("Player1", signedIn[0]!.GetType().GetProperty("Gamertag")!.GetValue(signedIn[0]));
        Assert.Equal(false, signedIn[0]!.GetType().GetProperty("IsSignedInToLive")!.GetValue(signedIn[0]));
        Assert.Equal(false, guide.GetProperty("IsTrialMode")!.GetValue(null));
        Assert.Equal(false, guide.GetProperty("IsVisible")!.GetValue(null));

        var called = false;
        AsyncCallback callback = _ => called = true;
        var box = guide.GetMethods().First(m => m.Name == "BeginShowMessageBox" && m.GetParameters().Length == 7);
        var result = (IAsyncResult)box.Invoke(null, ["Title", "Text", new[] { "OK" }, 0, Enum.ToObject(Type("Microsoft.Xna.Framework.GamerServices.MessageBoxIcon"), 0), callback, null])!;

        Assert.True(called);
        Assert.True(result.IsCompleted);
        Assert.Null(guide.GetMethod("EndShowMessageBox")!.Invoke(null, [result]));
    }

    [Fact]
    public void Storage_selector_finishes_at_once_with_a_device()
    {
        var device = Type("Microsoft.Xna.Framework.Storage.StorageDevice");
        var called = false;
        AsyncCallback callback = _ => called = true;
        var begin = device.GetMethods().First(m => m.Name == "BeginShowSelector" && m.GetParameters().Length == 2);

        var result = (IAsyncResult)begin.Invoke(null, [callback, "state"])!;

        Assert.True(called);
        Assert.Equal("state", result.AsyncState);
        Assert.IsType(device, device.GetMethod("EndShowSelector")!.Invoke(null, [result]));
    }

    private static string Resolve(string path, bool allowRoot, string? root = null)
    {
        var sandbox = Type("Microsoft.Xna.Framework.Storage.StorageSandbox");
        var resolve = sandbox.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            return (string)resolve.Invoke(null, [root ?? Path.Combine(Path.GetTempPath(), "xr-container"), path, allowRoot])!;
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }
    }

    private static System.Type Type(string name) => _compat.Value.GetType(name, throwOnError: true)!;

    private static Assembly CompileShims()
    {
        var sources = new ShimProjectGenerator().Generate(MonoGameTarget.DesktopGL)
            .Where(c => c.RelativePath.EndsWith(".cs", StringComparison.Ordinal))
            .Select(c => c.NewText!)
            .Append(_monoGameStub)
            .Select(text => CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp12)));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create(
            "XnaReborn.Compat.TestBuild",
            sources,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        var problems = emitted.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).Select(d => d.ToString()).ToList();
        Assert.True(emitted.Success && problems.Count == 0, string.Join(Environment.NewLine, problems));
        return Assembly.Load(stream.ToArray());
    }
}
