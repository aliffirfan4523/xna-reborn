using System.Text.Json.Serialization;

namespace XnaReborn.Migrator.Output;

/// <summary>What a migration does to one file of the output.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChangeKind
{
    /// <summary>A new file that did not exist in the source.</summary>
    Created,

    /// <summary>A source file that was rewritten.</summary>
    Modified,

    /// <summary>A source file copied byte for byte.</summary>
    Copied,

    /// <summary>A file left out of the output on purpose.</summary>
    Skipped,

    /// <summary>A file copied unchanged that a person must handle.</summary>
    ManualWork,
}
