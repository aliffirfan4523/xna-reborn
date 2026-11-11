using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>How sure the analyzer is about a detected value.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Confidence
{
    /// <summary>No signal. Nothing was guessed.</summary>
    Unknown,

    /// <summary>Only a weak hint was found.</summary>
    Low,

    /// <summary>A band or platform signal was found, but no exact value.</summary>
    Medium,

    /// <summary>An exact value was read from the project.</summary>
    High,
}
