using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator;

/// <summary>The SDK-style .csproj generator.</summary>
[Trait("Category", "Migrator")]
public class SdkCsprojGeneratorTests
{
    private static readonly SdkCsprojGenerator _generator = new();

    public static IEnumerable<object[]> ProjectRows() =>
    [
        ["xna40-basic", "xna40-basic"],
        ["xna31-legacy", "xna31-legacy"],
        ["xna31-rewritable", "xna31-rewritable"],
        ["xna40-gamerservices", "xna40-gamerservices"],
        ["xna40-custom-content", "xna40-custom-content"],
        ["multi-project-sln", "multi-project-sln/Game/Game.csproj"],
        ["multi-project-sln", "multi-project-sln/Library/Library.csproj"],
    ];

    [Theory]
    [MemberData(nameof(ProjectRows))]
    public async Task Generated_project_for_a_fixture_matches_snapshot(string fixture, string label)
    {
        var root = FixturePaths.Of(fixture);
        var csproj = label.EndsWith(".csproj", StringComparison.Ordinal)
            ? Path.Combine(FixturePaths.Root, label.Replace('/', Path.DirectorySeparatorChar))
            : Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Single();
        var source = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(csproj, root, CancellationToken.None);

        var change = _generator.Generate(source, MonoGameTarget.DesktopGL);

        Assert.Equal(source.Info.Path, change.RelativePath);
        Assert.DoesNotContain(FixturePaths.Root, change.NewText!, StringComparison.OrdinalIgnoreCase);
        await Verifier.Verify(change.NewText!, "csproj").UseParameters(label.Replace('/', '_'));
    }

    [Fact]
    public async Task Windows_target_snapshot_uses_WindowsDX()
    {
        var root = FixturePaths.Of("xna40-custom-content");
        var csproj = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Single();
        var source = await new ProjectReader(new ReadOnlyFileSystem()).ReadAsync(csproj, root, CancellationToken.None);

        var change = _generator.Generate(source, MonoGameTarget.WindowsDX);

        await Verifier.Verify(change.NewText!, "csproj");
    }

    [Fact]
    public void Change_is_a_created_file_at_the_source_path()
    {
        var change = _generator.Generate(Build(path: "Sub/Game.csproj"), MonoGameTarget.DesktopGL);

        Assert.Equal("Sub/Game.csproj", change.RelativePath);
        Assert.Equal(ChangeKind.Created, change.Kind);
        Assert.Null(change.OriginalText);
        Assert.Null(change.Reason);
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var source = Build(items: [Item("Reference", "Foo", ("HintPath", "lib/Foo.dll"))]);

        Assert.Equal(_generator.Generate(source, MonoGameTarget.DesktopGL), _generator.Generate(source, MonoGameTarget.DesktopGL));
    }

    [Fact]
    public void Text_uses_LF_has_one_trailing_newline_no_BOM_and_no_xml_declaration()
    {
        var text = _generator.Generate(Build(), MonoGameTarget.DesktopGL).NewText!;

        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("</Project>\n", text, StringComparison.Ordinal);
        Assert.False(text.EndsWith("\n\n", StringComparison.Ordinal));
        Assert.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk\">", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<?xml", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("WinExe", "WinExe")]
    [InlineData("Exe", "WinExe")]
    [InlineData("Library", "Library")]
    [InlineData("library", "Library")]
    [InlineData(null, "WinExe")]
    public void Output_type_is_mapped(string? original, string expected)
    {
        var properties = original is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["OutputType"] = original };

        var text = Text(Build(properties: properties));

        Assert.Contains($"<OutputType>{expected}</OutputType>", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Windows Phone", null, "WinExe")]
    [InlineData("Xbox 360", "Game", "WinExe")]
    [InlineData("Zune", null, "WinExe")]
    [InlineData("Xbox 360", "Library", "Library")]
    [InlineData("x86", null, "Library")]
    public void A_device_game_marked_library_becomes_an_exe(string xnaPlatform, string? xnaOutputType, string expected)
    {
        var properties = new Dictionary<string, string> { ["OutputType"] = "Library", ["XnaPlatform"] = xnaPlatform };
        if (xnaOutputType is not null)
        {
            properties["XnaOutputType"] = xnaOutputType;
        }

        Assert.Contains($"<OutputType>{expected}</OutputType>", Text(Build(properties: properties)), StringComparison.Ordinal);
    }

    [Fact]
    public void Properties_come_in_the_documented_order()
    {
        var source = Build(
            properties: new Dictionary<string, string> { ["ApplicationIcon"] = "Game.ico", ["RootNamespace"] = "My.Ns" },
            compile: ["Properties\\AssemblyInfo.cs"],
            defines: ["WINDOWS", "FOO"]);

        var text = Text(source);

        var order = new[] { "OutputType", "TargetFramework", "AssemblyName", "RootNamespace", "ApplicationIcon", "GenerateAssemblyInfo", "DefineConstants", "Nullable", "ImplicitUsings" };
        var positions = order.Select(n => text.IndexOf("<" + n + ">", StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
        Assert.Contains("<Nullable>disable</Nullable>", text, StringComparison.Ordinal);
        Assert.Contains("<ImplicitUsings>disable</ImplicitUsings>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Root_namespace_comes_from_the_source_else_the_assembly_name()
    {
        Assert.Contains("<RootNamespace>Other.Ns</RootNamespace>", Text(Build(properties: new Dictionary<string, string> { ["RootNamespace"] = "Other.Ns" })), StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>MyGame</RootNamespace>", Text(Build()), StringComparison.Ordinal);
    }

    [Fact]
    public void Optional_properties_are_left_out_when_not_needed()
    {
        var text = Text(Build(compile: ["Game1.cs"]));

        Assert.DoesNotContain("ApplicationIcon", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateAssemblyInfo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("DefineConstants", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Properties\\AssemblyInfo.cs")]
    [InlineData("properties/assemblyinfo.CS")]
    public void Assembly_info_in_the_compile_list_turns_off_generation(string compileFile)
    {
        Assert.Contains("<GenerateAssemblyInfo>false</GenerateAssemblyInfo>", Text(Build(compile: [compileFile])), StringComparison.Ordinal);
    }

    [Fact]
    public void Define_constants_drop_sdk_and_old_platform_symbols()
    {
        var text = Text(Build(defines: ["DEBUG", "TRACE", "XBOX", "XBOX360", "ZUNE", "WINDOWS_PHONE", "FOO", "BAR"]));

        Assert.Contains("<DefineConstants>FOO;BAR</DefineConstants>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_symbol_is_kept_only_for_a_windows_project()
    {
        Assert.Contains("<DefineConstants>WINDOWS;FOO</DefineConstants>", Text(Build(defines: ["WINDOWS", "FOO"], platform: XnaPlatform.Windows)), StringComparison.Ordinal);
        Assert.Contains("<DefineConstants>FOO</DefineConstants>", Text(Build(defines: ["WINDOWS", "FOO"], platform: XnaPlatform.Xbox360)), StringComparison.Ordinal);
    }

    [Fact]
    public void Define_constants_are_skipped_when_nothing_is_left()
    {
        Assert.DoesNotContain("DefineConstants", Text(Build(defines: ["DEBUG", "TRACE", "XBOX360"])), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_framework_package_is_referenced_without_content()
    {
        var text = Text(Build());

        Assert.Contains("<PackageReference Include=\"MonoGame.Framework.DesktopGL\" Version=\"3.8.6.1\" />", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Content.Builder", text, StringComparison.Ordinal);
        Assert.DoesNotContain("MonoGameContentReference", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_adds_the_builder_package_and_the_mgcb_reference()
    {
        var text = Text(Build(contentProjects: ["Content/Content.contentproj"]));

        Assert.Contains("<PackageReference Include=\"MonoGame.Content.Builder.Task\" Version=\"3.8.6.1\" />", text, StringComparison.Ordinal);
        Assert.Contains("<MonoGameContentReference Include=\"Content\\Content.mgcb\" />", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_content_reference_path_replaces_the_default_and_gets_backslashes()
    {
        var source = Build(contentProjects: ["Content/Content.contentproj"]);

        var text = _generator.Generate(source, MonoGameTarget.DesktopGL, "Data/Art/Content.mgcb").NewText!;

        Assert.Contains("<MonoGameContentReference Include=\"Data\\Art\\Content.mgcb\" />", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Content\\Content.mgcb\"", text, StringComparison.Ordinal);
        Assert.Equal(Text(source), _generator.Generate(source, MonoGameTarget.DesktopGL, null).NewText);
        Assert.Equal(Text(source), _generator.Generate(source, MonoGameTarget.DesktopGL, " ").NewText);
    }

    [Fact]
    public void A_content_reference_path_is_ignored_without_content_and_escaped_with_it()
    {
        var withoutContent = _generator.Generate(Build(), MonoGameTarget.DesktopGL, "Data/Content.mgcb").NewText!;
        var escaped = _generator.Generate(Build(contentProjects: ["C.contentproj"]), MonoGameTarget.DesktopGL, "A&B/Content.mgcb").NewText!;

        Assert.DoesNotContain("MonoGameContentReference", withoutContent, StringComparison.Ordinal);
        Assert.Contains("Include=\"A&amp;B\\Content.mgcb\"", escaped, StringComparison.Ordinal);
    }
    [Fact]
    public void Windows_target_changes_package_and_target_framework()
    {
        var text = _generator.Generate(Build(), MonoGameTarget.WindowsDX).NewText!;

        Assert.Contains("<TargetFramework>net8.0-windows</TargetFramework>", text, StringComparison.Ordinal);
        Assert.Contains("MonoGame.Framework.WindowsDX", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Csproj_project_references_are_kept_with_backslashes_in_order_and_contentproj_is_dropped()
    {
        var source = Build(items:
        [
            Item("ProjectReference", "../B/B.csproj"),
            Item("ProjectReference", "..\\Content\\Content.contentproj"),
            Item("ProjectReference", "..\\A\\A.CSPROJ"),
        ]);

        var text = Text(source);

        Assert.DoesNotContain("contentproj", text, StringComparison.OrdinalIgnoreCase);
        var b = text.IndexOf("..\\B\\B.csproj", StringComparison.Ordinal);
        var a = text.IndexOf("..\\A\\A.CSPROJ", StringComparison.Ordinal);
        Assert.True(b >= 0 && a > b);
    }

    [Fact]
    public void Xna_and_framework_references_are_dropped_even_with_a_hint_path()
    {
        var source = Build(items:
        [
            Item("Reference", "Microsoft.Xna.Framework, Version=4.0.0.0", ("HintPath", "x.dll")),
            Item("Reference", "Microsoft.Xna.Framework.Game", ("HintPath", "x.dll")),
            Item("Reference", "mscorlib", ("HintPath", "x.dll")),
            Item("Reference", "System", ("HintPath", "x.dll")),
            Item("Reference", "System.Core", ("HintPath", "x.dll")),
            Item("Reference", "Microsoft.CSharp", ("HintPath", "x.dll")),
            Item("Reference", "WindowsBase", ("HintPath", "x.dll")),
        ]);

        Assert.DoesNotContain("<Reference ", Text(source), StringComparison.Ordinal);
    }

    [Fact]
    public void Other_references_with_a_hint_path_are_kept_in_order_and_without_one_are_dropped()
    {
        var source = Build(items:
        [
            Item("Reference", "Zeta, Version=1.0.0.0, Culture=neutral", ("HintPath", "libs/Zeta.dll")),
            Item("Reference", "NoHint"),
            Item("Reference", "Alpha", ("HintPath", "..\\libs\\Alpha.dll")),
        ]);

        var text = Text(source);

        Assert.Contains("<Reference Include=\"Zeta\">", text, StringComparison.Ordinal);
        Assert.Contains("<HintPath>libs\\Zeta.dll</HintPath>", text, StringComparison.Ordinal);
        Assert.Contains("<HintPath>..\\libs\\Alpha.dll</HintPath>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NoHint", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Zeta", StringComparison.Ordinal) < text.IndexOf("Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public void Old_project_settings_are_not_carried_over()
    {
        var source = Build(
            properties: new Dictionary<string, string>
            {
                ["XnaFrameworkVersion"] = "v4.0",
                ["PlatformTarget"] = "x86",
                ["ProjectTypeGuids"] = "{6D335F3A-9D43-41b4-9D22-F6F17C4BE596}",
                ["NoStdLib"] = "true",
            },
            items: [Item("Import", "$(MSBuildExtensionsPath)\\Microsoft\\XNA Game Studio\\v4.0\\Microsoft.Xna.GameStudio.targets")]);

        var text = Text(source);

        foreach (var forbidden in new[] { "Xna", "x86", "ProjectTypeGuids", "NoStdLib", "Import", "EnableDefaultCompileItems" })
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var text = Text(Build(items: [Item("Reference", "Foo", ("HintPath", "a&b\\Foo.dll"))]));

        Assert.Contains("<HintPath>a&amp;b\\Foo.dll</HintPath>", text, StringComparison.Ordinal);
    }

    private static string Text(ProjectReadResult source) => _generator.Generate(source, MonoGameTarget.DesktopGL).NewText!;

    private static ProjectItem Item(string type, string include, params (string Key, string Value)[] metadata) =>
        new(type, include, metadata.ToDictionary(m => m.Key, m => m.Value));

    private static ProjectReadResult Build(
        string path = "Game.csproj",
        IReadOnlyDictionary<string, string>? properties = null,
        IReadOnlyList<ProjectItem>? items = null,
        IReadOnlyList<string>? compile = null,
        IReadOnlyList<string>? defines = null,
        IReadOnlyList<string>? contentProjects = null,
        XnaPlatform platform = XnaPlatform.Windows)
    {
        var info = new ProjectInfo(
            path,
            "MyGame",
            true,
            XnaVersion.V40,
            platform,
            null,
            null,
            [],
            compile ?? [],
            defines ?? [],
            contentProjects ?? [],
            []);
        return new ProjectReadResult(
            info,
            new Dictionary<string, string>(properties ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            items ?? [],
            []);
    }
}
