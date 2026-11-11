// Part of XnaReborn.Compat, the XACT stand-in that XNA Reborn writes into a migrated copy.
// The data types the generated XactCatalog.g.cs constructs. Units are already converted: volumes are linear gains,
// pitch is in the XNA range of -1 to 1. RPC curve points stay in XACT units (hundredths of a decibel or a semitone).
namespace XnaReborn.Compat.Xact;

internal enum XactWaveKind
{
    SoundEffect,
    Song,
}

internal sealed record XactCategoryData(string Name, float Volume, bool IsPublic, bool BackgroundMusic, int MaxInstances, int Behavior, int FadeInMs, int FadeOutMs, string[] Parents);

internal sealed record XactVariableData(string Name, bool IsGlobal, bool IsPublic, bool ReadOnly, float InitialValue, float Min, float Max);

internal sealed record XactRpcPointData(float X, float Y, int CurveShape);

internal sealed record XactRpcCurveData(string Name, int Property, bool IsSound, string Variable, XactRpcPointData[] Points);

internal sealed record XactRpcData(string Name, XactRpcCurveData[] Curves);

internal sealed record XactEngineData(string Stem, XactCategoryData[] Categories, XactVariableData[] Variables, XactRpcData[] Rpcs);

internal sealed record XactWaveData(string Name, string Asset, XactWaveKind Kind, bool Missing, bool HasLoopRegion);

internal sealed record XactWaveBankData(string Stem, string Name, bool Streaming, XactWaveData[] Waves);

internal sealed record XactWaveEntryData(string Bank, string Wave, int Weight);

internal sealed record XactEventData(int LoopCount, int TimestampMs, bool Relative, XactWaveEntryData[] Entries);

internal sealed record XactTrackData(float Volume, XactEventData[] Events);

internal sealed record XactSoundData(string Name, float Volume, float Pitch, int Priority, string[] Categories, string[] Rpcs, XactTrackData[] Tracks);

internal sealed record XactCueEntryData(string Sound, int WeightMin, int WeightMax);

internal sealed record XactCueData(string Name, int VariationType, bool NewVariationOnLoop, XactCueEntryData[] Entries, string[] Unsupported);

internal sealed record XactSoundBankData(string Stem, string Name, XactSoundData[] Sounds, XactCueData[] Cues);
