using XnaReborn.Core.Models;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Runner;
using XnaReborn.Runner.Environment;
using XnaReborn.Runner.Safety;

namespace XnaReborn.App.Tests.Fakes;

/// <summary>Small, valid model objects for tests that do not need real analysis output.</summary>
public static class SampleData
{
    public static AnalysisReport Report(int score = 40, int projectedScore = 90) => new(
        1,
        "test-catalog",
        [],
        [],
        [],
        Enum.GetValues<Severity>().ToDictionary(s => s, _ => 0),
        Enum.GetValues<Classification>().ToDictionary(c => c, _ => 0),
        score,
        projectedScore,
        [],
        null);

    public static MigrationResult Migration(bool completed) => new(
        new MigrationPlan([], []),
        [],
        new Manifest(1, "test", "test-catalog", completed, [], null),
        completed);

    public static EnvironmentInfo Environment() => new("8.0.100", ["8.0.100"], true, "Windows 11", true, null);

    public static RunnerResult Runner() => new(new GuardVerdict(true, "OK", null), Environment(), null, null, null, null, null);
}
