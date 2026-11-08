using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>The kind of member usage a <see cref="MemberMatch"/> applies to.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MemberMatchKind
{
    /// <summary>Either an invocation or a member access.</summary>
    Any,

    /// <summary>Only invocations (method calls).</summary>
    Invocation,

    /// <summary>Only member accesses that are not invocations.</summary>
    Access,
}
