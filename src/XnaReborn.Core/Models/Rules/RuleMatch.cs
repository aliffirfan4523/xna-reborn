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
/// <param name="AlsoNames">More type names that the same rule matches (JSON "alsoNames"), or null.</param>
public sealed record TypeMatch(
    string Name,
    IReadOnlyList<string> Namespaces,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AlsoNames = null) : RuleMatch;

/// <summary>A member access or call, matched by syntax only.</summary>
/// <param name="Name">The member name, or null when <paramref name="Names"/> is used. Exactly one of the two is set.</param>
/// <param name="Namespaces">Namespaces of which one must be imported by the file.</param>
/// <param name="Receiver">Identifier that must appear left of the dot, or null for any.</param>
/// <param name="Kind">Whether to match invocations, accesses, or both.</param>
/// <param name="Arguments">Allowed argument count range, or null for any.</param>
/// <param name="Names">Several member names that the same rule matches, or null. Used for rename families.</param>
public sealed record MemberMatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name,
    IReadOnlyList<string> Namespaces,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Receiver = null,
    MemberMatchKind Kind = MemberMatchKind.Any,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ArgumentRange? Arguments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Names = null) : RuleMatch
{
    /// <summary>Every member name the rule matches: <see cref="Name"/> or <see cref="Names"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AllNames => Names ?? (Name is null ? [] : [Name]);
}

/// <summary>A method declaration, matched by syntax only. Used for API members that a game overrides, such as the XNA 1.0 LoadGraphicsContent.</summary>
/// <param name="Name">The method name, compared ordinally for C# and ignoring case for Visual Basic.</param>
/// <param name="Namespaces">Namespaces of which one must be imported by the file.</param>
/// <param name="Overrides">When true, the declaration must carry the override modifier (Overrides in Visual Basic).</param>
public sealed record MethodDeclarationMatch(
    string Name,
    IReadOnlyList<string> Namespaces,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Overrides = false) : RuleMatch;

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
/// <param name="Metadata">Optional filter on one attribute or child element of the item, or null for no filter.</param>
public sealed record ProjectItemMatch(
    string ItemType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Include = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MetadataFilter? Metadata = null) : RuleMatch;

/// <summary>A filter on one metadata value of a project item, such as the Version of a PackageReference. Every filter that is set must pass. An item without the named metadata passes none of them.</summary>
/// <param name="Name">Attribute or child element name, compared ignoring case.</param>
/// <param name="EqualsValue">Required exact value (JSON "equals"), or null.</param>
/// <param name="NotEquals">Value the metadata must differ from, or null.</param>
/// <param name="MatchesPattern">.NET regular expression the value must match (JSON "matches"), or null.</param>
public sealed record MetadataFilter(
    string Name,
    [property: JsonPropertyName("equals"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EqualsValue = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NotEquals = null,
    [property: JsonPropertyName("matches"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MatchesPattern = null);

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

/// <summary>A project whose Compile items mostly link to files of other projects, such as a Game Studio platform copy.</summary>
/// <param name="MinLinkedRatio">Smallest share of Compile items that must be links, from 0 to 1.</param>
public sealed record ProjectLinksMatch(double MinLinkedRatio = 0.5) : RuleMatch;

/// <summary>The text of files that match a glob. Comments are blanked first for .fx and .fxh files.</summary>
/// <param name="Glob">The glob pattern, for example **/*.fx.</param>
/// <param name="Regex">.NET regular expression that must match somewhere in the file text.</param>
/// <param name="MaxBytes">Files larger than this are skipped. Defaults to 1 MiB.</param>
public sealed record FileContentMatch(string Glob, string Regex, int MaxBytes = FileContentMatch.DefaultMaxBytes) : RuleMatch
{
    /// <summary>The default size limit in bytes (1 MiB).</summary>
    public const int DefaultMaxBytes = 1_048_576;
}

/// <summary>A conditional compilation symbol named in an <c>#if</c> or <c>#elif</c> directive, including inactive branches.</summary>
/// <param name="Names">Symbols to look for. Whole identifiers are compared, so WINDOWS_PHONE_7 does not match WINDOWS.</param>
public sealed record PreprocessorSymbolMatch(IReadOnlyList<string> Names) : RuleMatch;

/// <summary>A setting, reference, or entry in a Content.mgcb file.</summary>
/// <param name="Setting">One of platform, profile, config, compress, reference, or entry.</param>
/// <param name="EqualsValue">Required exact value (JSON "equals"), or null. Not used with entry.</param>
/// <param name="MatchesPattern">.NET regular expression the value must match (JSON "matches"), or null. Not used with entry.</param>
/// <param name="Importer">With entry: importer name filter, or null. A trailing * is a wildcard.</param>
/// <param name="Processor">With entry: processor name filter, or null. A trailing * is a wildcard.</param>
/// <param name="NonStandardPair">With entry: when true, the entry must use an importer or processor that is not a stock pair.</param>
public sealed record MgcbSettingMatch(
    string Setting,
    [property: JsonPropertyName("equals"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EqualsValue = null,
    [property: JsonPropertyName("matches"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MatchesPattern = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Importer = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Processor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool NonStandardPair = false) : RuleMatch;

/// <summary>An ActionScript 3 import of a package, used by Flash catalogs.</summary>
/// <param name="Pattern">Package pattern such as flash.display.*. A trailing .* matches the package and the packages below it.</param>
public sealed record As3ImportMatch(string Pattern) : RuleMatch;

/// <summary>A named ActionScript 3 language construct, used by Flash catalogs.</summary>
/// <param name="Construct">The construct name, for example with, e4x, arguments, closure or regex.</param>
public sealed record As3SyntaxMatch(string Construct) : RuleMatch;

/// <summary>An ActionScript 3 metadata tag, used by Flash catalogs.</summary>
/// <param name="Tag">The tag name such as Embed or SWF.</param>
/// <param name="EmbedKind">With Embed: image, audio, data, font, symbol or missing. Null for any.</param>
public sealed record As3MetadataMatch(
    string Tag,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmbedKind = null) : RuleMatch;

/// <summary>A member or identifier used in ActionScript 3 code, used by Flash catalogs. Exactly one of <paramref name="Names"/> or <paramref name="Qualified"/> is set.</summary>
/// <param name="Names">Member or identifier names that match anywhere, or null.</param>
/// <param name="Qualified">A qualified member such as FlxG.stage, or null.</param>
public sealed record As3ApiMatch(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Names = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Qualified = null) : RuleMatch;
