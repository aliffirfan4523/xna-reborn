using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XnaReborn.App.Controls;

/// <summary>The meaning of a chip, banner or tile. Each kind has its own color, a default icon and a word, so color is never the only cue.</summary>
public enum StatusKind
{
    /// <summary>No special meaning.</summary>
    Neutral,

    /// <summary>The brand accent: the current thing, or a highlight.</summary>
    Accent,

    /// <summary>Done, passed, good.</summary>
    Success,

    /// <summary>Information.</summary>
    Info,

    /// <summary>Needs attention but not broken.</summary>
    Warning,

    /// <summary>Failed or broken.</summary>
    Error,
}

/// <summary>Maps a <see cref="StatusKind"/> to the style class, default icon and spoken word the shared controls use.</summary>
internal static class StatusKinds
{
    private static readonly string[] _classes = ["neutral", "accent", "success", "info", "warning", "error"];

    /// <summary>The style class used by the control themes in Styles/Controls.axaml, such as "success".</summary>
    public static string ClassOf(StatusKind kind) => _classes[(int)kind];

    /// <summary>The word screen readers hear for the kind.</summary>
    public static string WordOf(StatusKind kind) => kind switch
    {
        StatusKind.Success => "Success",
        StatusKind.Info => "Info",
        StatusKind.Warning => "Warning",
        StatusKind.Error => "Error",
        _ => string.Empty,
    };

    /// <summary>The geometry for an icon key from Styles/Icons.axaml, or null when it is not found.</summary>
    public static Geometry? Icon(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) ? value as Geometry : null;

    /// <summary>The default icon of a kind.</summary>
    public static Geometry? DefaultIcon(StatusKind kind) => Icon(kind switch
    {
        StatusKind.Accent => "IconFlag",
        StatusKind.Success => "IconCheckCircle",
        StatusKind.Warning => "IconWarning",
        StatusKind.Error => "IconError",
        _ => "IconInfo",
    });

    /// <summary>Replaces the kind class on <paramref name="target"/>, leaving its other classes alone.</summary>
    public static void Apply(StyledElement target, StatusKind kind)
    {
        foreach (var name in _classes)
        {
            target.Classes.Set(name, false);
        }

        target.Classes.Set(ClassOf(kind), true);
    }
}
