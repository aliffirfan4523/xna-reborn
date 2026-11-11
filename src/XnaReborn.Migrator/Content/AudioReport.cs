using XnaReborn.Core.Models.Xact;
using XnaReborn.Migrator.Audio;

namespace XnaReborn.Migrator.Content;

/// <summary>What became of the XACT audio in one content conversion (audio-and-shaders R7.1). Counts are over all converted <c>.xap</c> files.</summary>
/// <param name="WaveBanks">Wave banks read from the converted projects.</param>
/// <param name="SoundBanks">Sound banks read from the converted projects.</param>
/// <param name="SoundEffects">Wave files built as <c>SoundEffect</c> assets.</param>
/// <param name="Songs">Wave files built as <c>Song</c> assets.</param>
/// <param name="CuesSupported">Cues whose sounds use only features the stand-in plays.</param>
/// <param name="CuesWithUnsupported">Cues with at least one XACT feature that is not converted (DSP, reverb, filters, and so on).</param>
/// <param name="MissingWaves">Wave files that are missing on disk or cannot be built; their cues are silent.</param>
/// <param name="Notes">Plain sentences about the audio, sorted and distinct.</param>
public sealed record AudioReport(
    int WaveBanks,
    int SoundBanks,
    int SoundEffects,
    int Songs,
    int CuesSupported,
    int CuesWithUnsupported,
    int MissingWaves,
    IReadOnlyList<string> Notes)
{
    /// <summary>Builds the report for the converted projects.</summary>
    /// <param name="projects">The converted projects with their plans.</param>
    /// <param name="notes">Notes to include.</param>
    /// <returns>The report.</returns>
    public static AudioReport Create(IReadOnlyList<XactProjectPlan> projects, IEnumerable<string> notes)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(notes);
        var cuesSupported = 0;
        var cuesUnsupported = 0;
        foreach (var project in projects)
        {
            var bySound = XactCatalogWriter.UnsupportedBySound(project.Project);
            foreach (var cue in project.Project.SoundBanks.SelectMany(b => b.Cues))
            {
                if (cue.Entries.Any(e => bySound.ContainsKey(e.SoundName)))
                {
                    cuesUnsupported++;
                }
                else
                {
                    cuesSupported++;
                }
            }
        }

        var assets = projects.SelectMany(p => p.Plan.Assets).ToList();
        return new AudioReport(
            projects.Sum(p => p.Project.WaveBanks.Count),
            projects.Sum(p => p.Project.SoundBanks.Count),
            assets.Count(a => a.Exists && a.Kind == XactWaveKind.SoundEffect),
            assets.Count(a => a.Exists && a.Kind == XactWaveKind.Song),
            cuesSupported,
            cuesUnsupported,
            assets.Count(a => !a.Exists),
            notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }
}
