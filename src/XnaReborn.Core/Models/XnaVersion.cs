using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>The XNA Game Studio version a project targets. Serialized as "3.0", "3.1", "4.0", or "Unknown".</summary>
[JsonConverter(typeof(XnaVersionJsonConverter))]
public enum XnaVersion
{
    /// <summary>No version signal was found.</summary>
    Unknown,

    /// <summary>XNA 3.0.</summary>
    V30,

    /// <summary>XNA 3.1.</summary>
    V31,

    /// <summary>XNA 4.0.</summary>
    V40,
}
