using System.Text.Json;

namespace XnaReborn.Core.Models.Rules;

/// <summary>Shared JSON settings for reading and writing rule catalogs.</summary>
public static class RuleCatalogJson
{
    /// <summary>
    /// Web defaults (camelCase, case-insensitive). Match polymorphism is handled by
    /// <see cref="RuleMatchJsonConverter"/>, so no .NET 9 metadata option is needed.
    /// Unknown properties such as "$schema" are ignored.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
