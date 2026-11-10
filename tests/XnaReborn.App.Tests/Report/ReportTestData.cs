using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Tests.Report;

/// <summary>Reports for the report page tests: a small hand-built one and a real one from the analyzer.</summary>
public static class ReportTestData
{
    private static readonly Lazy<Task<AnalysisReport>> _legacy = new(
        () => new AnalysisService().AnalyzeAsync(RepoPaths.Fixture("xna31-legacy"), null, CancellationToken.None));

    /// <summary>Six findings in all three classifications. Rule XNA099 has no entry in the triggered rules.</summary>
    public static AnalysisReport Small(string? noProjectReason = null, IReadOnlyList<Diagnostic>? diagnostics = null)
    {
        Finding[] findings =
        [
            new("XNA001", "Build targets import", Severity.Error, Classification.AutoFixable, "Game.csproj", null, "$(MSBuildExtensionsPath)\\targets"),
            new("XNA022", "SpriteBlendMode", Severity.Error, Classification.AutoFixable, "Game.csproj", new SourceLocation("Game1.cs", 44, 31), "spriteBatch.Begin(SpriteBlendMode.AlphaBlend);"),
            new("XNA004", "Old .NET Framework target", Severity.Warning, Classification.AutoFixable, "Game.csproj", null, null),
            new("XNA027", "ResolveTexture2D", Severity.Error, Classification.Manual, "Game.csproj", new SourceLocation("Game1.cs", 14, 17), "private ResolveTexture2D snapshot;"),
            new("XNA099", "Unknown helper", Severity.Info, Classification.Manual, "Game.csproj", new SourceLocation("Level.cs", 3, 1), "Helper.Run();"),
            new("XNA050", "Xbox Live", Severity.Blocker, Classification.Unsupported, "Game.csproj", new SourceLocation("Net.cs", 9, 5), "Gamer.SignIn();"),
        ];
        TriggeredRule[] rules =
        [
            new("XNA001", "Build targets import", "The old targets file does not exist any more.", "Use an SDK-style project.", "project-migrator:4.1"),
            new("XNA022", "SpriteBlendMode", "SpriteBlendMode was replaced by BlendState.", "Pass BlendState.AlphaBlend.", "source-rewriters:3"),
            new("XNA004", "Old .NET Framework target", "The target is .NET 3.5.", "Target net8.0.", null),
            new("XNA027", "ResolveTexture2D", "ResolveTexture2D is gone.", "Render to a RenderTarget2D.", null),
            new("XNA050", "Xbox Live", "Xbox Live services were shut down.", "Remove the sign-in code.", null),
        ];
        return new AnalysisReport(
            1,
            "test-catalog",
            [],
            noProjectReason is null ? findings : [],
            noProjectReason is null ? rules : [],
            Enum.GetValues<Severity>().ToDictionary(s => s, s => noProjectReason is null ? findings.Count(f => f.Severity == s) : 0),
            Enum.GetValues<Classification>().ToDictionary(c => c, c => noProjectReason is null ? findings.Count(f => f.Classification == c) : 0),
            noProjectReason is null ? 42 : 0,
            noProjectReason is null ? 77 : 0,
            diagnostics ?? [],
            noProjectReason);
    }

    /// <summary>The real analysis of <c>tests/fixtures/xna31-legacy</c>, computed once.</summary>
    public static Task<AnalysisReport> RealLegacyAsync() => _legacy.Value;
}
