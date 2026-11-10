using System.Reflection;
using System.Text.Json;

namespace XnaReborn.Migrator.Projects;

/// <summary>The pinned MonoGame package versions, read from the embedded <c>packages.json</c>.</summary>
public sealed class PackageVersions
{
    private static readonly string _resourceName = "XnaReborn.Migrator.Projects.packages.json";

    private static readonly Lazy<PackageVersions> _default = new(Load);

    /// <summary>The pinned versions of this build.</summary>
    public static PackageVersions Default => _default.Value;

    /// <summary>Version of the MonoGame.Framework.* packages.</summary>
    public string MonoGame { get; init; } = string.Empty;

    /// <summary>Version of the MonoGame.Content.Builder.Task package.</summary>
    public string ContentBuilder { get; init; } = string.Empty;

    /// <summary>Version of the dotnet-mgcb tool.</summary>
    public string DotnetMgcb { get; init; } = string.Empty;

    /// <summary>Target framework of DesktopGL projects.</summary>
    public string TargetFramework { get; init; } = string.Empty;

    /// <summary>Target framework of WindowsDX projects, as the official MonoGame WindowsDX template writes it.</summary>
    public string WindowsTargetFramework { get; init; } = string.Empty;

    /// <summary>Links the versions and target frameworks were verified against.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Reads the embedded <c>packages.json</c>.</summary>
    /// <returns>The pinned versions.</returns>
    public static PackageVersions Load()
    {
        using var stream = typeof(PackageVersions).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException("Embedded resource " + _resourceName + " is missing.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Deserialize<PackageVersions>(stream, options)
            ?? throw new InvalidOperationException("packages.json is empty.");
    }
}
