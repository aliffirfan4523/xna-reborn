using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>Who can fix a finding.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Classification
{
    /// <summary>The migrator can fix it.</summary>
    AutoFixable,

    /// <summary>A person must fix it.</summary>
    Manual,

    /// <summary>There is no modern equivalent.</summary>
    Unsupported,
}
