using XnaReborn.Analyzer;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Rules;
using XnaReborn.Migrator;
using XnaReborn.Tests.Fixtures;
using XnaReborn.Tests.Migrator;
using Xunit.Abstractions;

namespace XnaReborn.Tests.Sdx;

/// <summary>
/// Migrates the synthetic Toolkit fixture and builds the copy with the .NET SDK against MonoGame. The game is only built, never run.
/// Needs NuGet and a Windows font, so it runs only when <c>XNAREBORN_INTEGRATION=1</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SdxIntegrationTests(ITestOutputHelper testOutput) : IDisposable
{
    // A real 1x1 white PNG. The fixture's own Balls.dds is only a text placeholder that the content builder cannot decode.
    private static readonly byte[] _onePixelPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x01, 0x73, 0x52, 0x47, 0x42, 0x00, 0xAE, 0xCE, 0x1C, 0xE9, 0x00, 0x00,
        0x00, 0x04, 0x67, 0x41, 0x4D, 0x41, 0x00, 0x00, 0xB1, 0x8F, 0x0B, 0xFC, 0x61, 0x05, 0x00, 0x00,
        0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01, 0xC7,
        0x6F, 0xA8, 0x64, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x18, 0x57, 0x63, 0xF8, 0xFF,
        0xFF, 0xFF, 0x7F, 0x00, 0x09, 0xFB, 0x03, 0xFD, 0x05, 0x43, 0x45, 0xCA, 0x00, 0x00, 0x00, 0x00,
        0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "xr-sdx-int-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_temp))
        {
            try
            {
                Directory.Delete(_temp, recursive: true);
            }
            catch (IOException)
            {
                // A build server may still hold a file. The folder is in the temp directory and can be removed later.
            }
        }
    }

    [IntegrationTheory]
    [InlineData(MonoGameTarget.WindowsDX)]
    [InlineData(MonoGameTarget.DesktopGL)]
    public async Task Migrated_sdx_basic_builds_against_MonoGame(MonoGameTarget target)
    {
        var source = Path.Combine(_temp, "source");
        MigrationTestSupport.CopyFolder(FixturePaths.Of("sdx-basic"), source);
        var project = Path.Combine(source, "SdxBasic.csproj");
        File.WriteAllText(project, File.ReadAllText(project).Replace("Content\\Balls.dds", "Content\\Balls.png", StringComparison.Ordinal).Replace("<Link>Balls.dds</Link>", "<Link>Balls.png</Link>", StringComparison.Ordinal));
        File.Delete(Path.Combine(source, "Content", "Balls.dds"));
        File.WriteAllBytes(Path.Combine(source, "Content", "Balls.png"), _onePixelPng);

        var rules = Path.GetFullPath(Path.Combine(FixturePaths.Root, "..", "..", "rules"));
        var catalogs = await RuleCatalogLoader.LoadAllAsync([Path.Combine(rules, "xna-to-monogame.json"), Path.Combine(rules, "sharpdx-toolkit-to-monogame.json")], new ReadOnlyFileSystem(), CancellationToken.None);
        var report = await new ProjectAnalyzer().AnalyzeAsync(source, catalogs, null, CancellationToken.None);
        var output = Path.Combine(_temp, "migrated");
        var result = await new ProjectMigrator().MigrateAsync(source, report, new MigrationOptions(output, target, false, false, new HashSet<string>(StringComparer.Ordinal)), false, null, CancellationToken.None);
        Assert.True(result.Completed);

        var (exitCode, text) = await MigrationIntegrationTests.RunBuildAsync(output);
        testOutput.WriteLine(text);

        Assert.True(exitCode == 0, $"dotnet build failed with exit code {exitCode}.{Environment.NewLine}{text}");
    }
}
