namespace XnaReborn.Runner.Publish;

/// <summary>A platform a migrated copy can be published for, named by its .NET runtime identifier (RID).</summary>
/// <param name="Rid">The runtime identifier passed to <c>dotnet publish -r</c>.</param>
/// <param name="DisplayName">A plain name for a picker.</param>
/// <param name="IsWindows">True for a Windows RID. Windows output is a <c>.exe</c>; the others have no extension.</param>
public sealed record PublishTarget(string Rid, string DisplayName, bool IsWindows)
{
    /// <summary>Windows 64-bit. Works for both DesktopGL and WindowsDX copies.</summary>
    public static PublishTarget WindowsX64 { get; } = new("win-x64", "Windows (64-bit)", true);

    /// <summary>Linux 64-bit. Needs a DesktopGL copy.</summary>
    public static PublishTarget LinuxX64 { get; } = new("linux-x64", "Linux (64-bit)", false);

    /// <summary>macOS on Intel. Needs a DesktopGL copy.</summary>
    public static PublishTarget MacX64 { get; } = new("osx-x64", "macOS (Intel)", false);

    /// <summary>macOS on Apple silicon. Needs a DesktopGL copy.</summary>
    public static PublishTarget MacArm64 { get; } = new("osx-arm64", "macOS (Apple silicon)", false);

    /// <summary>Every supported target, in picker order.</summary>
    public static IReadOnlyList<PublishTarget> All { get; } = [WindowsX64, LinuxX64, MacX64, MacArm64];

    /// <summary>Finds a target by RID.</summary>
    /// <param name="rid">The runtime identifier, such as <c>linux-x64</c>.</param>
    /// <returns>The target, or null for an unknown RID. The match is exact, so a RID can never carry extra arguments.</returns>
    public static PublishTarget? Find(string? rid) => All.FirstOrDefault(t => string.Equals(t.Rid, rid, StringComparison.Ordinal));
}
