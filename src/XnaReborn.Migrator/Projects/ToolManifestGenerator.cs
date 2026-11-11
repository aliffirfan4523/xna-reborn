using System.Text.Json;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Projects;

/// <summary>Writes <c>.config/dotnet-tools.json</c>, which pins <c>dotnet-mgcb</c> (the MonoGame content builder) and <c>dotnet-mgfxc</c> (the effect compiler) to one version.</summary>
public sealed class ToolManifestGenerator
{
    /// <summary>Creates the tool manifest.</summary>
    /// <param name="dotnetMgcbVersion">Version of dotnet-mgcb and dotnet-mgfxc, equal to the content builder package version.</param>
    /// <returns>A <see cref="ChangeKind.Created"/> change for <c>.config/dotnet-tools.json</c>.</returns>
    /// <exception cref="ArgumentException">The version is empty.</exception>
    public FileChange Generate(string dotnetMgcbVersion)
    {
        if (string.IsNullOrWhiteSpace(dotnetMgcbVersion))
        {
            throw new ArgumentException("The dotnet-mgcb version must not be empty.", nameof(dotnetMgcbVersion));
        }

        var version = JsonSerializer.Serialize(dotnetMgcbVersion);
        var text = string.Join(
            "\n",
            "{",
            "  \"version\": 1,",
            "  \"isRoot\": true,",
            "  \"tools\": {",
            "    \"dotnet-mgcb\": {",
            $"      \"version\": {version},",
            "      \"commands\": [",
            "        \"mgcb\"",
            "      ],",
            "      \"rollForward\": false",
            "    },",
            "    \"dotnet-mgfxc\": {",
            $"      \"version\": {version},",
            "      \"commands\": [",
            "        \"mgfxc\"",
            "      ],",
            "      \"rollForward\": false",
            "    }",
            "  }",
            "}") + "\n";
        return new FileChange(
            ".config/dotnet-tools.json",
            ChangeKind.Created,
            null,
            text,
            "The MonoGame content builder task fails with a message about a missing tool manifest otherwise.");
    }
}
