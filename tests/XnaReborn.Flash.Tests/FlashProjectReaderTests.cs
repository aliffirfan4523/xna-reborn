using System.Security.Cryptography;
using System.Text.Json;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer.Detection;
using XnaReborn.Flash.Analyzer.Reading;

namespace XnaReborn.Flash.Tests;

public class FlashProjectReaderTests
{
    /// <summary>A file system that fails the test if anyone opens a compiled Flash file.</summary>
    private sealed class NoSwfFileSystem : IReadOnlyFileSystem
    {
        private readonly ReadOnlyFileSystem _inner = new();

        public bool FileExists(string path) => _inner.FileExists(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) => _inner.EnumerateFiles(directory, searchPattern);

        public IReadOnlyList<string> EnumerateDirectories(string directory) => _inner.EnumerateDirectories(directory);

        public Stream OpenRead(string path) => Guard(path) ? _inner.OpenRead(path) : throw new InvalidOperationException("Opened " + path);

        public string ReadAllText(string path) => Guard(path) ? _inner.ReadAllText(path) : throw new InvalidOperationException("Read " + path);

        private static bool Guard(string path) =>
            !path.EndsWith(".swf", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".swc", StringComparison.OrdinalIgnoreCase)
            && !path.EndsWith(".fla", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(FlashFixtures.ExpectedRows), MemberType = typeof(FlashFixtures))]
    public void Reading_a_fixture_matches_its_expected_file(string name)
    {
        var folder = FlashFixtures.Of(name);
        var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "expected.json"))).RootElement;

        var project = FlashProjectReader.Read(new NoSwfFileSystem(), folder);

        Assert.Equal(expected.GetProperty("kind").GetString(), project.Detection.Kind.ToString());
        Assert.Equal(StringOrNull(expected, "projectFile"), project.ProjectFile);
        Assert.Equal(expected.GetProperty("sourceRoots").EnumerateArray().Select(e => e.GetString()), project.SourceRoots);
        Assert.Equal(expected.GetProperty("sourceFiles").EnumerateArray().Select(e => e.GetString()), project.Files.Select(f => f.Path));
        Assert.Equal(expected.GetProperty("entryClasses").EnumerateArray().Select(e => e.GetString()), project.EntryClasses.Select(e => e.Name));
        Assert.Equal(expected.GetProperty("usesFlixel").GetBoolean(), project.UsesFlixel);
        Assert.Equal(expected.GetProperty("bundlesFlixel").GetBoolean(), project.BundlesFlixel);
        Assert.Equal(StringOrNull(expected, "flixelVersion"), project.FlixelVersion);
        Assert.Equal(expected.GetProperty("diagnostics").GetInt32(), project.Diagnostics.Count);
    }

    [Fact]
    public void Swf_only_and_fla_only_folders_are_flash_without_sources()
    {
        var swf = FlashProjectDetector.Detect(new NoSwfFileSystem(), FlashFixtures.Of("flx-swf-only"));
        Assert.Equal(ProjectKind.Flash, swf.Kind);
        Assert.True(swf.HasSwf);
        Assert.Empty(swf.SourceFiles);

        var fla = FlashProjectDetector.Detect(new NoSwfFileSystem(), FlashFixtures.Of("flx-fla-only"));
        Assert.True(fla.HasFla);
        Assert.Equal(ProjectKind.Flash, fla.Kind);
    }

    [Fact]
    public void A_csharp_only_folder_is_not_flash()
    {
        var d = FlashProjectDetector.Detect(new ReadOnlyFileSystem(), FlashFixtures.Of("flx-not-flash"));
        Assert.Equal(ProjectKind.Xna, d.Kind);
        Assert.False(d.IsConflict);
    }

    [Fact]
    public void A_folder_with_both_kinds_is_a_conflict()
    {
        var temp = Path.Combine(Path.GetTempPath(), "xnareborn-flash-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            File.WriteAllText(Path.Combine(temp, "Game.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(temp, "Main.as"), "package {}");
            var d = FlashProjectDetector.Detect(new ReadOnlyFileSystem(), temp);
            Assert.True(d.IsConflict);
            Assert.Equal(ProjectKind.Unknown, d.Kind);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void Reading_changes_no_file_in_any_fixture()
    {
        var before = Hashes();
        foreach (var name in FlashFixtures.Names)
        {
            FlashProjectReader.Read(new ReadOnlyFileSystem(), FlashFixtures.Of(name));
        }

        Assert.Equal(before, Hashes());
    }

    [Fact]
    public void Cancellation_stops_the_read()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => FlashProjectReader.Read(new ReadOnlyFileSystem(), FlashFixtures.Of("flx-hello"), cts.Token));
    }

    private static string? StringOrNull(JsonElement e, string name) =>
        e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();

    private static Dictionary<string, string> Hashes() =>
        Directory.EnumerateFiles(FlashFixtures.Root, "*", SearchOption.AllDirectories)
            .ToDictionary(f => f, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
}
