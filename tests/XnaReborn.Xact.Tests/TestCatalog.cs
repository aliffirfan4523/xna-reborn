namespace XnaReborn.Compat.Xact;

/// <summary>The catalog the generated <c>XactCatalog.g.cs</c> would provide. Hand-written here so the shim sources compile on their own.</summary>
internal static partial class XactCatalog
{
    internal const string ContentRoot = "Content";

    internal static readonly XactEngineData[] Engines = [new("Sounds", [], [], [])];

    internal static readonly XactWaveBankData[] WaveBanks = [new("Wave Bank", "WB", false, [])];

    internal static readonly XactSoundBankData[] SoundBanks = [new("Sound Bank", "Sound Bank", [], [new XactCueData("Hit", 0, false, [], [])])];
}
