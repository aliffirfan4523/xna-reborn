using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>The area of a project a rule is about.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleCategory
{
    /// <summary>Project file settings.</summary>
    Project,

    /// <summary>Assembly references.</summary>
    Reference,

    /// <summary>API usage in source code.</summary>
    Api,

    /// <summary>Content pipeline items.</summary>
    Content,

    /// <summary>Platform-specific files or settings.</summary>
    Platform,
}
