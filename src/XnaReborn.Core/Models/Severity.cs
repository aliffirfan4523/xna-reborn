using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>How serious a finding is. Ordered from least to most serious.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severity
{
    /// <summary>Worth knowing, no action required.</summary>
    Info,

    /// <summary>Probably works, but may behave differently.</summary>
    Warning,

    /// <summary>Will fail to build or run until fixed.</summary>
    Error,

    /// <summary>Stops the migration from producing a working game.</summary>
    Blocker,
}
