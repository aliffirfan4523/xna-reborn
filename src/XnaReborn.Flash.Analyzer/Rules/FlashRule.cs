using System.Text.Json;
using System.Text.Json.Serialization;
using XnaReborn.Core.Models;

namespace XnaReborn.Flash.Analyzer.Rules;

/// <summary>What a Flash rule looks for. Which fields apply depends on <see cref="Type"/>.</summary>
/// <param name="Type">One of as3Import, as3Syntax, as3Metadata, as3Api, filePattern, project.</param>
/// <param name="Pattern">as3Import: package pattern such as "flash.display.*" (a trailing ".*" matches the package and below). filePattern: a file name suffix such as ".swc".</param>
/// <param name="Construct">as3Syntax: the construct name (with, e4x, arguments, untyped, dynamicClass, namespace, closure, intDivision, numberDefault, objectBag, regex, reflection).</param>
/// <param name="Tag">as3Metadata: the tag name such as Embed or Frame.</param>
/// <param name="EmbedKind">as3Metadata with Embed: image, audio, data, font, symbol or missing.</param>
/// <param name="Names">as3Api: member or identifier names that match anywhere.</param>
/// <param name="Qualified">as3Api: a qualified member such as "FlxG.stage".</param>
/// <param name="Probe">project: the project-level probe name (flixelNot2, swfWithoutSource, flashPro, swc, entryCount).</param>
public sealed record FlashRuleMatch(
    string Type,
    string? Pattern = null,
    string? Construct = null,
    string? Tag = null,
    string? EmbedKind = null,
    IReadOnlyList<string>? Names = null,
    string? Qualified = null,
    string? Probe = null);

/// <summary>One Flash migration rule.</summary>
/// <param name="Id">Rule id such as FL001.</param>
/// <param name="Title">Short title.</param>
/// <param name="Severity">How serious a match is.</param>
/// <param name="Classification">Whether the converter can fix it.</param>
/// <param name="Match">What to look for.</param>
/// <param name="Explanation">Why it matters.</param>
/// <param name="Resolution">What to do.</param>
/// <param name="MigratorTask">The spec task that implements the fix, or null.</param>
public sealed record FlashRule(
    string Id,
    string Title,
    Severity Severity,
    Classification Classification,
    FlashRuleMatch Match,
    string Explanation,
    string Resolution,
    string? MigratorTask = null);

/// <summary>A loaded rule catalog.</summary>
/// <param name="Name">Catalog name.</param>
/// <param name="Rules">The rules, in file order.</param>
public sealed record FlashRuleCatalog(string Name, IReadOnlyList<FlashRule> Rules)
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Loads the catalog compiled into this assembly (rules/flash-to-monogame.json).</summary>
    public static FlashRuleCatalog LoadBuiltIn()
    {
        using var stream = typeof(FlashRuleCatalog).Assembly.GetManifestResourceStream("XnaReborn.Flash.Analyzer.flash-to-monogame.json")
            ?? throw new InvalidOperationException("The Flash rule catalog is not embedded.");
        return Parse(new StreamReader(stream).ReadToEnd());
    }

    /// <summary>Parses catalog JSON and checks it: ids are unique and every rule has a match type.</summary>
    /// <param name="json">The catalog text.</param>
    public static FlashRuleCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var catalog = JsonSerializer.Deserialize<FlashRuleCatalog>(json, _options)
            ?? throw new InvalidOperationException("The Flash rule catalog is empty.");
        var duplicate = catalog.Rules.GroupBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Duplicate rule id {duplicate.Key}.");
        }

        var bad = catalog.Rules.FirstOrDefault(r => r.Match is null || string.IsNullOrEmpty(r.Match.Type));
        if (bad is not null)
        {
            throw new InvalidOperationException($"Rule {bad.Id} has no match type.");
        }

        return catalog;
    }
}
