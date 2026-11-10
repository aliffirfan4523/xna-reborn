namespace XnaReborn.Runner.Environment;

/// <summary>What the machine offers for building and running a migrated copy.</summary>
/// <param name="DotnetVersion">Version printed by <c>dotnet --version</c>, or null when dotnet was not found.</param>
/// <param name="Sdks">Installed SDK versions from <c>dotnet --list-sdks</c>, sorted.</param>
/// <param name="SdkCompatible">True when an SDK of version 8.0 or later is installed.</param>
/// <param name="Os">Short name of the operating system, such as "Windows 10.0.26200".</param>
/// <param name="TargetSupported">True when the selected MonoGame target can run on this operating system.</param>
/// <param name="Problem">A plain message for the user when something is missing, otherwise null.</param>
public sealed record EnvironmentInfo(
    string? DotnetVersion,
    IReadOnlyList<string> Sdks,
    bool SdkCompatible,
    string Os,
    bool TargetSupported,
    string? Problem);
