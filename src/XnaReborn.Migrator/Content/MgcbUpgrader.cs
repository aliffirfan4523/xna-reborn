using System.Text.RegularExpressions;

namespace XnaReborn.Migrator.Content;

/// <summary>The result of upgrading the text of one <c>Content.mgcb</c>.</summary>
/// <param name="Text">The new text. Equals the input when nothing needed a change.</param>
/// <param name="Changed">True when <paramref name="Text"/> differs from the input.</param>
/// <param name="OldPlatform">The <c>/platform</c> value the file had, or null when it had none.</param>
/// <param name="RemovedReferences">The install-folder <c>/reference</c> values that were removed, in file order.</param>
/// <param name="KeptReferences">The other <c>/reference</c> values (custom pipeline assemblies), in file order.</param>
public sealed record MgcbUpgrade(
    string Text,
    bool Changed,
    string? OldPlatform,
    IReadOnlyList<string> RemovedReferences,
    IReadOnlyList<string> KeptReferences);

/// <summary>
/// Edits an existing <c>Content.mgcb</c> for MonoGame 3.8.6.1 at the text level: sets <c>/platform</c> to the migrated target and
/// removes <c>/reference</c> lines that point into the MonoGame installer folder. Every other byte stays, including entry order and line endings.
/// </summary>
public static partial class MgcbUpgrader
{
    private const string _platformPrefix = "/platform:";
    private const string _referencePrefix = "/reference:";
    private const string _beginMarker = "#begin";

    [GeneratedRegex(@"MonoGame[\\/]v3\.0[\\/]|\$\(MonoGameInstallDirectory\)|Program Files", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InstallFolderRegex();

    /// <summary>Tells whether a <c>/reference</c> path points into the MonoGame installer folder.</summary>
    /// <param name="path">The reference path as written.</param>
    public static bool IsInstallFolderReference(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return InstallFolderRegex().IsMatch(path);
    }

    /// <summary>Upgrades the text of one mgcb file.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="platform">The mgcb platform name of the migrated target: <c>DesktopGL</c> or <c>Windows</c>.</param>
    /// <param name="removeInstallReferences">False to keep the install-folder references (the user switched that step off).</param>
    /// <returns>The new text and what was found.</returns>
    public static MgcbUpgrade Upgrade(string text, string platform, bool removeInstallReferences = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Split('\n').ToList();
        string? oldPlatform = null;
        var platformIndex = -1;
        var lastSettingIndex = -1;
        var removed = new List<string>();
        var kept = new List<string>();
        var remove = new HashSet<int>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith(_beginMarker, StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith(_platformPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (platformIndex < 0)
                {
                    platformIndex = i;
                    oldPlatform = line[_platformPrefix.Length..].Trim();
                }
            }
            else if (line.StartsWith(_referencePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var path = line[_referencePrefix.Length..].Trim();
                if (removeInstallReferences && IsInstallFolderReference(path))
                {
                    removed.Add(path);
                    remove.Add(i);
                }
                else
                {
                    kept.Add(path);
                }
            }
            else if (line.StartsWith('/'))
            {
                lastSettingIndex = i;
            }
        }

        if (platformIndex >= 0)
        {
            if (!string.Equals(oldPlatform, platform, StringComparison.Ordinal))
            {
                var original = lines[platformIndex];
                var start = original.IndexOf(_platformPrefix, StringComparison.OrdinalIgnoreCase) + _platformPrefix.Length;
                var end = original.TrimEnd('\r', ' ', '\t').Length;
                lines[platformIndex] = original[..start] + platform + original[end..];
            }
        }
        else
        {
            lines.Insert(lastSettingIndex + 1, _platformPrefix + platform + (newLine == "\r\n" ? "\r" : string.Empty));
            remove = [.. remove.Select(r => r > lastSettingIndex ? r + 1 : r)];
        }

        var result = string.Join('\n', lines.Where((_, i) => !remove.Contains(i)));
        return new MgcbUpgrade(result, !string.Equals(result, text, StringComparison.Ordinal), oldPlatform, removed, kept);
    }
}
