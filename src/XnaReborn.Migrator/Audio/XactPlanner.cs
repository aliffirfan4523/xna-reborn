using System.Text;
using System.Text.RegularExpressions;
using XnaReborn.Core.Models.Xact;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Audio;

/// <summary>What kind of asset a wave becomes.</summary>
public enum XactWaveKind
{
    /// <summary>A <c>SoundEffect</c> (built with <c>SoundEffectProcessor</c>).</summary>
    SoundEffect,

    /// <summary>A <c>Song</c> (built with <c>SongProcessor</c>), used for streaming wave banks.</summary>
    Song,
}

/// <summary>One audio file that the content build turns into an asset.</summary>
/// <param name="AssetName">The asset name, with '/' separators, such as <c>xact/Sounds/Wave Bank/hit</c>.</param>
/// <param name="SourceRelativePath">The wave file relative to the content project folder, with '/' separators.</param>
/// <param name="Kind">Sound effect or song.</param>
/// <param name="Exists">False when the file is not there. The catalog still lists it, marked missing.</param>
public sealed record WaveAsset(string AssetName, string SourceRelativePath, XactWaveKind Kind, bool Exists);

/// <summary>The planned assets of one .xap and the findings that came up while planning.</summary>
/// <param name="EngineStem">The stem the game uses for the engine (<c>Sounds</c> for <c>Win\Sounds.xgs</c>).</param>
/// <param name="Assets">One entry per distinct wave file, in file order.</param>
/// <param name="AssetByWave">Asset name of each wave, keyed <c>bank|wave</c>. Waves that share a file share an asset.</param>
/// <param name="Findings">Notes for the report, in a fixed order.</param>
public sealed record XactPlan(
    string EngineStem,
    IReadOnlyList<WaveAsset> Assets,
    IReadOnlyDictionary<string, string> AssetByWave,
    IReadOnlyList<RewriteNote> Findings);

/// <summary>Plans the content assets for a parsed .xap: names, shared files, kinds, missing files and notes. Pure: it reads nothing itself.</summary>
public static partial class XactPlanner
{
    /// <summary>Rule id of the note about compiled XACT banks that have no .xap and are only copied.</summary>
    public const string CompiledRuleId = "XNA070";

    /// <summary>Rule id of the note that says a streaming wave bank was converted.</summary>
    public const string StreamingRuleId = "XNA071";

    /// <summary>Rule id of the note that lists XACT features that are not converted.</summary>
    public const string UnsupportedRuleId = "XNA072";

    /// <summary>Rule id of the .xap file rule, used for missing waves and presets.</summary>
    public const string XapRuleId = "XNA016";

    [GeneratedRegex(@"[^\p{L}\p{N} _\-.]", RegexOptions.CultureInvariant)]
    private static partial Regex UnsafeCharacters();

    /// <summary>Plans one project.</summary>
    /// <param name="project">The parsed .xap.</param>
    /// <param name="xapDirectoryRelative">Folder of the .xap relative to the content project folder, with '/' separators. Empty when they are the same folder.</param>
    /// <param name="fileExists">Tells whether a file exists, given its path relative to the content project folder.</param>
    /// <param name="streamingWavesAsSong">True to build the waves of streaming banks as songs.</param>
    public static XactPlan Plan(XapProject project, string xapDirectoryRelative, Func<string, bool> fileExists, bool streamingWavesAsSong = true)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(xapDirectoryRelative);
        ArgumentNullException.ThrowIfNull(fileExists);
        var engine = project.EngineFileStem ?? Path.GetFileNameWithoutExtension(project.Path);
        var assets = new List<WaveAsset>();
        var byWave = new Dictionary<string, string>(StringComparer.Ordinal);
        var byFile = new Dictionary<string, WaveAsset>(StringComparer.OrdinalIgnoreCase);
        var findings = new List<RewriteNote>();
        var directory = xapDirectoryRelative.Replace('\\', '/').Trim('/');

        foreach (var bank in project.WaveBanks)
        {
            var kind = bank.Streaming && streamingWavesAsSong ? XactWaveKind.Song : XactWaveKind.SoundEffect;
            if (bank.Streaming)
            {
                findings.Add(streamingWavesAsSong
                    ? new RewriteNote(StreamingRuleId, $"Wave bank '{bank.Name}' is a streaming bank, so its {bank.Waves.Count} wave(s) are built as songs. Only one song plays at a time, through MediaPlayer, and pitch is ignored.", false)
                    : new RewriteNote(StreamingRuleId, $"Wave bank '{bank.Name}' is a streaming bank, but streaming-as-song is off, so its waves are built as sound effects and are loaded fully into memory.", false));
            }

            foreach (var wave in bank.Waves)
            {
                var source = Combine(directory, wave.File);
                if (!byFile.TryGetValue(source, out var asset))
                {
                    asset = new WaveAsset(AssetName(engine, bank.Name, wave.Name), source, kind, fileExists(source));
                    byFile[source] = asset;
                    assets.Add(asset);
                    if (!asset.Exists)
                    {
                        findings.Add(new RewriteNote(XapRuleId, $"Wave '{wave.Name}' in bank '{bank.Name}' points to '{source}', which does not exist. Cues that use it will be silent.", true));
                    }
                }
                else if (asset.Kind != kind)
                {
                    findings.Add(new RewriteNote(XapRuleId, $"Wave '{wave.Name}' in bank '{bank.Name}' shares the file '{source}' with a wave of another kind. It uses the asset '{asset.AssetName}' ({asset.Kind}).", false));
                }

                byWave[bank.Name + "|" + wave.Name] = asset.AssetName;
                if (wave.CompressionPreset is { Length: > 0 } preset)
                {
                    findings.Add(new RewriteNote(XapRuleId, $"Wave '{wave.Name}' uses the compression preset '{preset}'. MonoGame builds the file as PCM or ADPCM instead, so file size and sound may differ.", false));
                }

                if (wave.LoopRegionLength > 0)
                {
                    findings.Add(new RewriteNote(XapRuleId, $"Wave '{wave.Name}' has a loop region. The converted asset loops the whole wave unless the file has its own loop points.", false));
                }
            }
        }

        foreach (var item in project.Unsupported)
        {
            findings.Add(new RewriteNote(UnsupportedRuleId, $"Line {item.Line}: {item.Path}: {item.Reason}", true));
        }

        return new XactPlan(engine, assets, byWave, findings);
    }

    private static string Combine(string directory, string file)
    {
        var path = file.Replace('\\', '/').TrimStart('/');
        var parts = new List<string>();
        foreach (var part in (directory.Length == 0 ? path : directory + "/" + path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(part);
            }
        }

        return string.Join('/', parts);
    }

    private static string AssetName(string engine, string bank, string wave)
    {
        var sb = new StringBuilder("xact");
        foreach (var part in new[] { engine, bank, wave })
        {
            sb.Append('/').Append(UnsafeCharacters().Replace(part, "_"));
        }

        return sb.ToString();
    }
}
