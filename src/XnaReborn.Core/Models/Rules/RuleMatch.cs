using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>
/// Describes what a rule looks for. The JSON "type" property selects the derived record.
/// Serialization is handled by <see cref="RuleMatchJsonConverter"/>, which accepts "type" at any position.
/// </summary>
[JsonConverter(typeof(RuleMatchJsonConverter))]
public abstract record RuleMatch;

/// <summary>An assembly reference in a project file.</summary>
/// <param name="Name">Assembly simple name. A trailing * is a wildcard.</param>
public sealed record ReferenceMatch(string Name) : RuleMatch;

/// <summary>A using directive or qualified name that uses a namespace.</summary>
/// <param name="Namespace">The namespace to find.</param>
/// <param name="IncludeChildren">Whether child namespaces also match.</param>
public sealed record NamespaceMatch(string Namespace, bool IncludeChildren = true) : RuleMatch;

/// <summary>A type name used in code, gated by an imported or qualifying namespace.</summary>
/// <param name="Name">The type name.</param>
/// <param name="Namespaces">Namespaces that make the name relevant.</param>
public sealed record TypeMatch(string Name, IReadOnlyList<string> Namespaces) : RuleMatch;

/// <summary>A member access or call, matched by syntax only.</summary>
/// <param name="Name">The member name.</param>
/// <param name="Namespaces">Namespaces of which one must be imported by the file.</param>
/// <param name="Receiver">Identifier that must appear left of the dot, or null for any.</param>
/// <param name="Kind">Whether to match invocations, accesses, or both.</param>
/// <param name="Arguments">Allowed argument count range, or null for any.</param>
public sealed record MemberMatch(
    string Name,
    IReadOnlyList<string> Namespaces,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Receiver = null,
    MemberMatchKind Kind = MemberMatchKind.Any,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ArgumentRange? Arguments = null) : RuleMatch;

/// <summary>An MSBuild property in a project file.</summary>
/// <param name="Name">The property name.</param>
/// <param name="EqualsValue">Required exact value (JSON "equals"), or null.</param>
/// <param name="MatchesPattern">.NET regular expression the value must match (JSON "matches"), or null.</param>
public sealed record ProjectPropertyMatch(
    string Name,
    [property: JsonPropertyName("equals"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EqualsValue = null,
    [property: JsonPropertyName("matches"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MatchesPattern = null) : RuleMatch;

/// <summary>An item in a project file, such as Reference, Compile, NestedContentProject, or Import.</summary>
/// <param name="ItemType">The item type.</param>
/// <param name="Include">Item include value (the Project attribute for Import), or null for any. A * is a wildcard.</param>
public sealed record ProjectItemMatch(
    string ItemType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Include = null) : RuleMatch;

/// <summary>A file under the project folder that matches a glob.</summary>
/// <param name="Glob">The glob pattern, for example **/*.xap.</param>
public sealed record FilePatternMatch(string Glob) : RuleMatch;

/// <summary>An item in a content project. At least one field should be set.</summary>
/// <param name="Extension">File extension including the dot, or null.</param>
/// <param name="Importer">Importer name, or null. A trailing * is a wildcard.</param>
/// <param name="Processor">Processor name, or null. A trailing * is a wildcard.</param>
/// <param name="NonStandardPair">When true, the item must use an importer or processor that is not part of stock XNA.</param>
public sealed record ContentItemMatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Extension = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Importer = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Processor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NonStandardPair = false) : RuleMatch;

/// <summary>A .spritefont file whose FontName is one of the listed font families. Looks in the project folder and in each nested content project folder.</summary>
/// <param name="Families">Font family names, compared case-insensitively with the FontName element.</param>
public sealed record SpriteFontFamilyMatch(IReadOnlyList<string> Families) : RuleMatch;
