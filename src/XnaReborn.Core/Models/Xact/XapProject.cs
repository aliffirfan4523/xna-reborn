namespace XnaReborn.Core.Models.Xact;

/// <summary>An XACT project (<c>.xap</c> file) as read by the analyzer.</summary>
/// <param name="Path">Path of the file as given to the reader.</param>
/// <param name="Signature">The Signature header value, or null.</param>
/// <param name="Version">The Version header value, or null.</param>
/// <param name="ContentVersion">The Content Version header value, or null.</param>
/// <param name="EngineFileStem">File name without folder or extension of the global settings Windows File (the name the game uses for the .xgs), or null.</param>
/// <param name="Categories">Categories in file order.</param>
/// <param name="Variables">Variables in file order.</param>
/// <param name="Rpcs">Runtime parameter controls in file order.</param>
/// <param name="WaveBanks">Wave banks in file order.</param>
/// <param name="SoundBanks">Sound banks in file order.</param>
/// <param name="Unsupported">Keys and blocks that the converter does not carry over.</param>
public sealed record XapProject(
    string Path,
    string? Signature,
    int? Version,
    int? ContentVersion,
    string? EngineFileStem,
    IReadOnlyList<XapCategory> Categories,
    IReadOnlyList<XapVariable> Variables,
    IReadOnlyList<XapRpc> Rpcs,
    IReadOnlyList<XapWaveBank> WaveBanks,
    IReadOnlyList<XapSoundBank> SoundBanks,
    IReadOnlyList<XapUnsupported> Unsupported);

/// <summary>A category: a volume group with an instance limit.</summary>
/// <param name="Name">Category name.</param>
/// <param name="IsPublic">Whether game code can reach it by name.</param>
/// <param name="BackgroundMusic">Whether the category is marked as background music.</param>
/// <param name="VolumeCentiDb">Volume in hundredths of a decibel.</param>
/// <param name="MaxInstances">Instance limit.</param>
/// <param name="Behavior">Instance limit behavior number (see rules/xact-features.json).</param>
/// <param name="FadeInMs">Crossfade fade-in in milliseconds.</param>
/// <param name="FadeOutMs">Crossfade fade-out in milliseconds.</param>
/// <param name="CrossfadeType">Crossfade type number.</param>
/// <param name="Parents">Names of parent categories.</param>
public sealed record XapCategory(
    string Name,
    bool IsPublic,
    bool BackgroundMusic,
    int VolumeCentiDb,
    int MaxInstances,
    int Behavior,
    int FadeInMs,
    int FadeOutMs,
    int CrossfadeType,
    IReadOnlyList<string> Parents);

/// <summary>A variable that curves can read.</summary>
/// <param name="Name">Variable name.</param>
/// <param name="IsGlobal">Whether the value is shared by all cues.</param>
/// <param name="IsPublic">Whether game code can set it.</param>
/// <param name="ReadOnly">Whether game code cannot set it.</param>
/// <param name="IsInternal">Whether the engine owns it.</param>
/// <param name="InitialValue">Value at start.</param>
/// <param name="Min">Smallest value.</param>
/// <param name="Max">Largest value.</param>
public sealed record XapVariable(
    string Name,
    bool IsGlobal,
    bool IsPublic,
    bool ReadOnly,
    bool IsInternal,
    float InitialValue,
    float Min,
    float Max);

/// <summary>One point of a curve.</summary>
/// <param name="X">Variable value.</param>
/// <param name="Y">Property value.</param>
/// <param name="CurveShape">Curve shape number to the next point.</param>
public sealed record XapRpcPoint(float X, float Y, int CurveShape);

/// <summary>A curve that maps a variable to a sound property.</summary>
/// <param name="Name">Curve name.</param>
/// <param name="Property">Property number (0 is volume in the files read).</param>
/// <param name="IsSound">Whether the curve targets a sound (true) or a category.</param>
/// <param name="VariableName">Name of the variable it reads.</param>
/// <param name="Points">Points in file order.</param>
public sealed record XapRpcCurve(string Name, int Property, bool IsSound, string VariableName, IReadOnlyList<XapRpcPoint> Points);

/// <summary>A runtime parameter control: a named group of curves.</summary>
/// <param name="Name">RPC name.</param>
/// <param name="Curves">Curves in file order.</param>
public sealed record XapRpc(string Name, IReadOnlyList<XapRpcCurve> Curves);

/// <summary>A wave in a wave bank.</summary>
/// <param name="Name">Wave name.</param>
/// <param name="File">Source file path as written, relative to the .xap folder.</param>
/// <param name="Channels">Channel count.</param>
/// <param name="SamplingRate">Samples per second.</param>
/// <param name="PlayRegionLength">Play region length from the cache block.</param>
/// <param name="LoopRegionOffset">Loop region offset from the cache block.</param>
/// <param name="LoopRegionLength">Loop region length from the cache block (0 means no loop region).</param>
/// <param name="CompressionPreset">Compression preset name, or null.</param>
public sealed record XapWave(
    string Name,
    string File,
    int Channels,
    int SamplingRate,
    long PlayRegionLength,
    long LoopRegionOffset,
    long LoopRegionLength,
    string? CompressionPreset);

/// <summary>A wave bank.</summary>
/// <param name="Name">Bank name.</param>
/// <param name="FileStem">Stem of the Windows File (the name the game uses for the .xwb), or null.</param>
/// <param name="Streaming">Whether the bank is marked as streaming.</param>
/// <param name="Waves">Waves in file order.</param>
public sealed record XapWaveBank(string Name, string? FileStem, bool Streaming, IReadOnlyList<XapWave> Waves);

/// <summary>A wave chosen by a play-wave event.</summary>
/// <param name="BankName">Wave bank name.</param>
/// <param name="EntryName">Wave name.</param>
/// <param name="Weight">Selection weight.</param>
public sealed record XapWaveEntry(string BankName, string EntryName, int Weight);

/// <summary>An event that plays a wave.</summary>
/// <param name="LoopCount">Loop count (255 means infinite).</param>
/// <param name="BreakLoop">Whether the loop can be broken.</param>
/// <param name="TimestampMs">Start time in milliseconds.</param>
/// <param name="Relative">Whether the timestamp is relative to the previous event.</param>
/// <param name="Entries">The waves it can pick.</param>
public sealed record XapPlayWaveEvent(int LoopCount, bool BreakLoop, int TimestampMs, bool Relative, IReadOnlyList<XapWaveEntry> Entries);

/// <summary>A track of a sound.</summary>
/// <param name="VolumeCentiDb">Track volume in hundredths of a decibel.</param>
/// <param name="Events">Play-wave events in file order.</param>
public sealed record XapTrack(int VolumeCentiDb, IReadOnlyList<XapPlayWaveEvent> Events);

/// <summary>A sound: tracks plus volume, pitch, categories and curves.</summary>
/// <param name="Name">Sound name.</param>
/// <param name="VolumeCentiDb">Volume in hundredths of a decibel.</param>
/// <param name="PitchCents">Pitch in hundredths of a semitone.</param>
/// <param name="Priority">Priority number.</param>
/// <param name="Categories">Category names.</param>
/// <param name="RpcNames">Names of the RPCs that apply to the sound.</param>
/// <param name="Tracks">Tracks in file order.</param>
public sealed record XapSound(
    string Name,
    int VolumeCentiDb,
    int PitchCents,
    int Priority,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> RpcNames,
    IReadOnlyList<XapTrack> Tracks);

/// <summary>A sound a cue can pick.</summary>
/// <param name="SoundName">Sound name.</param>
/// <param name="WeightMin">Lowest weight.</param>
/// <param name="WeightMax">Highest weight.</param>
public sealed record XapSoundEntry(string SoundName, int WeightMin, int WeightMax);

/// <summary>A cue: the name game code plays.</summary>
/// <param name="Name">Cue name.</param>
/// <param name="VariationType">Variation type number (see rules/xact-features.json).</param>
/// <param name="VariationTableType">Variation table type number.</param>
/// <param name="NewVariationOnLoop">Whether a new sound is picked on each loop.</param>
/// <param name="Entries">The sounds it can pick.</param>
public sealed record XapCue(string Name, int VariationType, int VariationTableType, bool NewVariationOnLoop, IReadOnlyList<XapSoundEntry> Entries);

/// <summary>A sound bank.</summary>
/// <param name="Name">Bank name.</param>
/// <param name="FileStem">Stem of the Windows File (the name the game uses for the .xsb), or null.</param>
/// <param name="Sounds">Sounds in file order.</param>
/// <param name="Cues">Cues in file order.</param>
public sealed record XapSoundBank(string Name, string? FileStem, IReadOnlyList<XapSound> Sounds, IReadOnlyList<XapCue> Cues);

/// <summary>A key or block that the converter does not carry over.</summary>
/// <param name="Line">1-based line in the .xap file.</param>
/// <param name="Path">Block path to the item, such as <c>Sound Bank/Sound/echo/Reverb Setting</c>.</param>
/// <param name="Reason">Why it is listed.</param>
public sealed record XapUnsupported(int Line, string Path, string Reason);
