using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer;

namespace XnaReborn.Flash.Tests;

/// <summary>Task 6.6: analyzes local checkouts of the candidate games. Skipped unless XNAREBORN_FLASH_SAMPLES is set. Nothing from the games is copied into the repository.</summary>
public class FlashSampleAnalysisTests
{
    [FlashSamplesTheory]
    [InlineData("Mode")]
    [InlineData("FlxCollisions")]
    [InlineData("Revenge")]
    public async Task Candidate_game_is_analyzed_read_only(string sample)
    {
        var root = Environment.GetEnvironmentVariable("XNAREBORN_FLASH_SAMPLES")!;
        var folder = Directory.EnumerateDirectories(root, sample, SearchOption.AllDirectories).FirstOrDefault();
        Assert.True(folder is not null, $"No folder named {sample} under {root}.");

        var report = await FlashProjectAnalyzer.AnalyzeAsync(folder!);

        Assert.Equal(ProjectKind.Flash, report.Kind);
        Assert.InRange(report.Score, 0, 100);
        Assert.DoesNotContain(root, FlashProjectAnalyzer.ToJson(report), StringComparison.OrdinalIgnoreCase);

        // XNAREBORN_KEEP_OUTPUT=<folder> keeps the markdown report for the task notes.
        var keep = Environment.GetEnvironmentVariable("XNAREBORN_KEEP_OUTPUT");
        if (!string.IsNullOrEmpty(keep))
        {
            Directory.CreateDirectory(keep);
            await File.WriteAllTextAsync(Path.Combine(keep, sample + ".md"), FlashProjectAnalyzer.ToMarkdown(report));
        }
    }
}
