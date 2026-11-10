using XnaReborn.Runner.Safety;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
[Trait("Category", "Safety")]
public sealed class MigratedCopyGuardTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xnareborn-guard-" + Guid.NewGuid().ToString("N"));

    public MigratedCopyGuardTests() => Directory.CreateDirectory(Path.Combine(_dir, "Game"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Fact]
    public void AcceptsCompletedCopyWithOneProject()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        var v = MigratedCopyGuard.Check(_dir);
        Assert.True(v.Allowed);
        Assert.Equal(Path.Combine(_dir, "Game", "Game.csproj"), v.ProjectOrSolutionPath);
    }

    [Fact]
    public void PrefersSingleSolution()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "Game.sln"), string.Empty);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(_dir, "Other.csproj"), "<Project/>");
        var v = MigratedCopyGuard.Check(_dir);
        Assert.True(v.Allowed);
        Assert.Equal(Path.Combine(_dir, "Game.sln"), v.ProjectOrSolutionPath);
    }

    [Fact]
    public void IgnoresBinAndObj()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        Directory.CreateDirectory(Path.Combine(_dir, "Game", "obj"));
        File.WriteAllText(Path.Combine(_dir, "Game", "obj", "Old.csproj"), "<Project/>");
        Assert.True(MigratedCopyGuard.Check(_dir).Allowed);
    }

    [Fact]
    public void RefusesMissingManifest()
    {
        File.WriteAllText(Path.Combine(_dir, "Game.csproj"), "<Project/>");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesIncompleteManifest()
    {
        Manifest(1, false);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesIncompleteMarkerFile()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(_dir, "INCOMPLETE.txt"), "x");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesInvalidJson()
    {
        File.WriteAllText(Path.Combine(_dir, "xnareborn-manifest.json"), "{ not json");
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesWrongVersion()
    {
        Manifest(2, true);
        File.WriteAllText(Path.Combine(_dir, "Game", "Game.csproj"), "<Project/>");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesNoProject()
    {
        Manifest(1, true);
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesTwoSolutions()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "A.sln"), string.Empty);
        File.WriteAllText(Path.Combine(_dir, "B.sln"), string.Empty);
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesTwoProjectsWithoutSolution()
    {
        Manifest(1, true);
        File.WriteAllText(Path.Combine(_dir, "A.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(_dir, "Game", "B.csproj"), "<Project/>");
        Refused(MigratedCopyGuard.Check(_dir));
    }

    [Fact]
    public void RefusesMissingFolder() => Refused(MigratedCopyGuard.Check(Path.Combine(_dir, "nope")));

    [Fact]
    public void RefusesOriginalFixtureFolder_xna40_basic()
    {
        var v = MigratedCopyGuard.Check(FixturePaths.Of("xna40-basic"));
        Refused(v);
        Assert.Contains("xnareborn-manifest.json", v.Message, StringComparison.Ordinal);
    }

    private static void Refused(GuardVerdict v)
    {
        Assert.False(v.Allowed);
        Assert.Null(v.ProjectOrSolutionPath);
        Assert.False(string.IsNullOrWhiteSpace(v.Message));
    }

    private void Manifest(int version, bool completed)
    {
        var done = completed ? "true" : "false";
        File.WriteAllText(
            Path.Combine(_dir, "xnareborn-manifest.json"),
            $"{{\n  \"version\": {version},\n  \"migrator\": \"1.0.0\",\n  \"catalogName\": \"xna-to-monogame\",\n  \"completed\": {done},\n  \"changes\": [],\n  \"build\": null,\n  \"content\": null\n}}\n");
    }
}
