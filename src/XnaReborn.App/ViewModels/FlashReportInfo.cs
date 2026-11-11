using XnaReborn.App.Services;
using XnaReborn.Core.Models;

namespace XnaReborn.App.ViewModels;

/// <summary>
/// What the report page says about a Flash (AS3 + Flixel 2.x) project: the entry class, the Flixel version and, when the
/// project cannot be converted, why. Read from the shared report only; no analysis or conversion happens here.
/// </summary>
/// <param name="KindText">The project kind in words.</param>
/// <param name="EntryClassText">The entry class, or a short note when none was found.</param>
/// <param name="FlixelText">The Flixel version, or a note that it is not known.</param>
/// <param name="Explanation">Plain words about why the project cannot be converted, or null when it can.</param>
/// <param name="CanConvert">True when migration can start: exactly one entry class was found and the folder has source code.</param>
public sealed record FlashReportInfo(string KindText, string EntryClassText, string FlixelText, string? Explanation, bool CanConvert)
{
    private const string _swfOnlyRule = "FL003";
    private const string _flashProRule = "FL004";

    /// <summary>Reads the Flash facts from a report.</summary>
    /// <param name="report">The report, or null.</param>
    /// <returns>The facts, or null when the report is not a Flash report.</returns>
    public static FlashReportInfo? From(AnalysisReport? report)
    {
        if (report is null || report.CatalogName != FlashServiceAdapter.CatalogName)
        {
            return null;
        }

        var references = report.Projects.FirstOrDefault()?.References ?? [];
        var entry = references.FirstOrDefault(r => r.StartsWith(FlashServiceAdapter.EntryClassPrefix, StringComparison.Ordinal))?[FlashServiceAdapter.EntryClassPrefix.Length..];
        var flixel = references.FirstOrDefault(r => r.StartsWith(FlashServiceAdapter.FlixelPrefix, StringComparison.Ordinal))?[FlashServiceAdapter.FlixelPrefix.Length..];
        var swfOnly = report.Findings.Any(f => f.RuleId == _swfOnlyRule);
        var flashPro = report.Findings.Any(f => f.RuleId == _flashProRule);

        string? explanation = null;
        if (swfOnly)
        {
            explanation = "This folder holds only a compiled .swf file. XNA Reborn works from source code and does not decompile Flash files. Open the ActionScript source project instead.";
        }
        else if (entry is null && flashPro)
        {
            explanation = "This folder holds a Flash Pro document (.fla) and no ActionScript game class. Its timeline art and code cannot be read. Move the code into ActionScript classes, export the art as image files, then open the folder again.";
        }
        else if (entry is null)
        {
            explanation = "The converter needs exactly one class that extends FlxGame, and this folder has none or more than one. The first slice converts Flixel 2.x source projects only.";
        }

        return new FlashReportInfo(
            "Flash project (ActionScript 3 + Flixel 2.x source)",
            entry is null ? "Entry class: not found" : "Entry class: " + entry,
            flixel is null ? "Flixel version: not known (no bundled Flixel source)" : "Flixel version: " + flixel,
            explanation,
            explanation is null);
    }
}
