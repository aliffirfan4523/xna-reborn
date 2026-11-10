using XnaReborn.Runner.Run;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public sealed class GameLocatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xr-locator-" + Guid.NewGuid().ToString("N"));

    public GameLocatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private string Project(string folder, string body, string name = "")
    {
        var dir = Path.Combine(_dir, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, (name.Length == 0 ? folder : name) + ".csproj");
        File.WriteAllText(path, $"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>");
        return path;
    }

    private static string Props(string outputType, string? assembly = null, string framework = "net8.0") =>
        $"<PropertyGroup><OutputType>{outputType}</OutputType><TargetFramework>{framework}</TargetFramework>"
        + (assembly is null ? string.Empty : $"<AssemblyName>{assembly}</AssemblyName>")
        + "</PropertyGroup>";

    private string Built(string projectPath, string file, string framework = "net8.0", string configuration = "Debug")
    {
        var dir = Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", configuration, framework);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, file);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void SingleExe_IsTheGame()
    {
        var project = Project("Game", Props("WinExe"));
        var dll = Built(project, "Game.dll");
        var exe = Built(project, "Game.exe");

        var result = GameLocator.Find(_dir);

        Assert.Null(result.Problem);
        var target = result.Target!;
        Assert.Equal(project, target.ProjectPath);
        Assert.Equal("Game", target.AssemblyName);
        Assert.Equal("net8.0", target.TargetFramework);
        Assert.Equal(OperatingSystem.IsWindows() ? exe : dll, target.ExecutablePath);
    }

    [Fact]
    public void WithoutExe_UsesTheDll()
    {
        var project = Project("Game", Props("Exe"));
        var dll = Built(project, "Game.dll");

        var target = GameLocator.Find(_dir).Target!;

        Assert.Equal(dll, target.ExecutablePath);
    }

    [Fact]
    public void AssemblyNameAndFrameworkComeFromTheProject()
    {
        var project = Project("Game", Props("WinExe", "Hero", "net8.0-windows"));
        var dll = Built(project, "Hero.dll", "net8.0-windows");

        var target = GameLocator.Find(_dir).Target!;

        Assert.Equal("Hero", target.AssemblyName);
        Assert.Equal("net8.0-windows", target.TargetFramework);
        Assert.Equal(dll, target.ExecutablePath);
    }

    [Fact]
    public void FirstOfSeveralFrameworksIsUsed()
    {
        var project = Project("Game", "<PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup>");
        Built(project, "Game.dll", "net9.0");
        var dll = Built(project, "Game.dll", "net8.0");

        Assert.Equal(dll, GameLocator.Find(_dir).Target!.ExecutablePath);
    }

    [Fact]
    public void MissingAssemblyName_UsesTheProjectFileName()
    {
        var project = Project("src", Props("Exe"), "MyGame");
        var dll = Built(project, "MyGame.dll");

        Assert.Equal(dll, GameLocator.Find(_dir).Target!.ExecutablePath);
    }

    [Fact]
    public void LibraryPlusGame_PicksTheGame()
    {
        var library = Project("Lib", Props("Library"));
        Built(library, "Lib.dll");
        var game = Project("Game", Props("WinExe") + "<ItemGroup><ProjectReference Include=\"..\\Lib\\Lib.csproj\" /></ItemGroup>");
        var dll = Built(game, "Game.dll");

        var result = GameLocator.Find(_dir);

        Assert.Equal(game, result.Target!.ProjectPath);
        Assert.Equal(dll, result.Target.ExecutablePath);
    }

    [Fact]
    public void ProjectWithoutOutputType_IsALibrary()
    {
        Project("Lib", "<PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>");

        var result = GameLocator.Find(_dir);

        Assert.Null(result.Target);
        Assert.Contains("OutputType", result.Problem);
    }

    [Fact]
    public void TwoGames_WithNoDifference_IsAProblemNamingBoth()
    {
        var a = Project("A", Props("Exe"));
        var b = Project("B", Props("Exe"));
        Built(a, "A.dll");
        Built(b, "B.dll");

        var result = GameLocator.Find(_dir);

        Assert.Null(result.Target);
        Assert.Contains(a, result.Problem);
        Assert.Contains(b, result.Problem);
    }

    [Fact]
    public void TwoGames_TheOneWithContentReferenceWins()
    {
        var tool = Project("Tool", Props("Exe"));
        var game = Project("Game", Props("WinExe") + "<ItemGroup><MonoGameContentReference Include=\"Content\\Content.mgcb\" /></ItemGroup>");
        Built(tool, "Tool.dll");
        var dll = Built(game, "Game.dll");

        Assert.Equal(dll, GameLocator.Find(_dir).Target!.ExecutablePath);
    }

    [Fact]
    public void TwoGames_TheOneNoOtherProjectReferencesWins()
    {
        var tool = Project("Tool", Props("Exe"));
        var game = Project("Game", Props("WinExe") + "<ItemGroup><ProjectReference Include=\"../Tool/Tool.csproj\" /></ItemGroup>");
        Built(tool, "Tool.dll");
        var dll = Built(game, "Game.dll");

        Assert.Equal(dll, GameLocator.Find(_dir).Target!.ExecutablePath);
    }

    [Fact]
    public void MissingBuildOutput_SaysToBuildFirst()
    {
        Project("Game", Props("WinExe"));

        var result = GameLocator.Find(_dir);

        Assert.Null(result.Target);
        Assert.Contains("Build the project first", result.Problem);
    }

    [Fact]
    public void BinAndObjFoldersAreSkipped()
    {
        var game = Project("Game", Props("Exe"));
        var dll = Built(game, "Game.dll");
        var stray = Path.Combine(Path.GetDirectoryName(game)!, "obj", "Stray");
        Directory.CreateDirectory(stray);
        File.WriteAllText(Path.Combine(stray, "Stray.csproj"), "<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>");

        Assert.Equal(dll, GameLocator.Find(_dir).Target!.ExecutablePath);
    }

    [Fact]
    public void UnreadableProjectIsNamedInTheProblem()
    {
        var broken = Path.Combine(_dir, "Broken.csproj");
        File.WriteAllText(broken, "<Project><oops");

        var result = GameLocator.Find(_dir);

        Assert.Null(result.Target);
        Assert.Contains(broken, result.Problem);
    }

    [Fact]
    public void MissingFolder_IsAProblemNotAnException()
    {
        var result = GameLocator.Find(Path.Combine(_dir, "nope"));

        Assert.Null(result.Target);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public void ConfigurationOption_PicksTheFolder()
    {
        var project = Project("Game", Props("Exe"));
        Built(project, "Game.dll");
        var release = Built(project, "Game.dll", configuration: "Release");

        Assert.Equal(release, GameLocator.Find(_dir, "Release").Target!.ExecutablePath);
    }
}
