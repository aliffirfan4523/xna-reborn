using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The kind of source project found in a folder.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectKind
{
    /// <summary>Nothing recognized, or a folder that holds both kinds of project (see the conflict flag of the detector).</summary>
    Unknown,

    /// <summary>A .NET project or solution. Whether it is XNA is decided by the XNA analyzer.</summary>
    Xna,

    /// <summary>A Flash project: ActionScript sources, a FlashDevelop or Flash Builder project file, or compiled Flash files.</summary>
    Flash,
}
