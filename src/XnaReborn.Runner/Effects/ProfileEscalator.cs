using System.Text.RegularExpressions;
using XnaReborn.Core.Content;

namespace XnaReborn.Runner.Effects;

/// <summary>The result of raising the DirectX profile level of one effect.</summary>
/// <param name="Text">The new text.</param>
/// <param name="Level">The highest level now used, such as <c>_4_0_level_9_3</c>.</param>
/// <param name="NeedsHiDef">True when <paramref name="Level"/> needs the HiDef graphics profile at run time.</param>
public sealed record EscalationResult(string Text, string Level, bool NeedsHiDef);

/// <summary>
/// Raises the DirectX shader profile of an effect whose shader models were set by the migrator's macro block
/// (<c>#if OPENGL ... #else ... #endif</c> after the "XNA Reborn: shader models set per target" comment).
/// Only the <c>#else</c> branch of that block changes.
/// </summary>
public static partial class ProfileEscalator
{
    private const string _marker = "// XNA Reborn: shader models set per target";

    [GeneratedRegex(@"^(?<head>[ \t]*#define[ \t]+\w+[ \t]+(?:vs|ps))(?<level>_4_\d(?:_level_\d_\d)?)(?<tail>[ \t]*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ElseDefine();

    /// <summary>Raises every DirectX define of the block by one level.</summary>
    /// <param name="text">The effect text.</param>
    /// <param name="profiles">The profile data. Defaults to <see cref="EffectProfiles.Default"/>.</param>
    /// <returns>The new text, or null when the block is missing or every define is already at the last level.</returns>
    public static EscalationResult? Escalate(string text, EffectProfiles? profiles = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        profiles ??= EffectProfiles.Default;
        var levels = profiles.EscalationLevels;
        var start = text.IndexOf(_marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var lines = text.Split('\n');
        var offset = 0;
        var index = 0;
        while (index < lines.Length && offset + lines[index].Length < start)
        {
            offset += lines[index].Length + 1;
            index++;
        }

        var inElse = false;
        var changed = false;
        var highest = -1;
        for (var i = index + 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("#else", StringComparison.Ordinal))
            {
                inElse = true;
                continue;
            }

            if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
            {
                break;
            }

            if (!inElse)
            {
                continue;
            }

            var carriage = lines[i].EndsWith('\r') ? "\r" : string.Empty;
            var match = ElseDefine().Match(lines[i].TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var current = levels.ToList().IndexOf(match.Groups["level"].Value);
            if (current < 0)
            {
                continue;
            }

            var next = Math.Min(current + 1, levels.Count - 1);
            highest = Math.Max(highest, next);
            if (next != current)
            {
                lines[i] = match.Groups["head"].Value + levels[next] + match.Groups["tail"].Value + carriage;
                changed = true;
            }
        }

        return changed
            ? new EscalationResult(string.Join('\n', lines), levels[highest], highest >= levels.ToList().IndexOf(profiles.HiDefFrom))
            : null;
    }
}
