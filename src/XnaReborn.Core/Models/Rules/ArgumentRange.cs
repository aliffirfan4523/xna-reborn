using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>An inclusive range of argument counts. A missing bound is unbounded.</summary>
/// <param name="Min">The smallest allowed argument count, or null for no lower bound.</param>
/// <param name="Max">The largest allowed argument count, or null for no upper bound.</param>
public sealed record ArgumentRange(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Min = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Max = null);
