using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Reporting;

/// <summary>
/// A test-only catalog that exercises every severity, classification, and match type against the fixtures.
/// These are not production rules; the real catalog is rules/xna-to-monogame.json.
/// </summary>
public static class SampleCatalog
{
    private static readonly string[] _graphics = ["Microsoft.Xna.Framework.Graphics"];
    private static readonly string[] _gamerServices = ["Microsoft.Xna.Framework.GamerServices"];

    /// <summary>Builds the sample catalog with the default scoring weights.</summary>
    public static RuleCatalog Create() => new(1, "xna-test-sample", ScoringOptions.Default,
    [
        Rule("XNA901", "GamerServices reference", Severity.Blocker, Classification.Unsupported, RuleCategory.Reference, new ReferenceMatch("Microsoft.Xna.Framework.GamerServices"), "Xbox Live has no modern equivalent.", "Remove the dependency.", null),
        Rule("XNA902", "Storage reference", Severity.Error, Classification.Unsupported, RuleCategory.Reference, new ReferenceMatch("Microsoft.Xna.Framework.Storage"), "StorageDevice depends on Xbox storage.", "Use plain file APIs.", null),
        Rule("XNA903", "XNA Game Studio targets import", Severity.Warning, Classification.AutoFixable, RuleCategory.Project, new ProjectItemMatch("Import", "*XNA Game Studio*"), "The project imports the XNA build targets.", "The migrator writes an SDK-style project.", "project-migrator:4.1"),
        Rule("XNA904", "XNA 3.1 project", Severity.Warning, Classification.AutoFixable, RuleCategory.Project, new ProjectPropertyMatch("XnaFrameworkVersion", "v3.1"), "XNA 3.1 APIs differ from 4.0.", "The migrator applies 3.1 to 4.0 rewrites.", "project-migrator:6.2"),
        Rule("XNA905", "SpriteBlendMode", Severity.Error, Classification.Manual, RuleCategory.Api, new TypeMatch("SpriteBlendMode", _graphics), "SpriteBlendMode was replaced by BlendState.", "Use BlendState.AlphaBlend or Additive.", null),
        Rule("XNA906", "ResolveBackBuffer", Severity.Error, Classification.Manual, RuleCategory.Api, new MemberMatch("ResolveBackBuffer", _graphics, "GraphicsDevice", MemberMatchKind.Invocation, null), "ResolveBackBuffer was removed.", "Render to a RenderTarget2D instead.", null),
        Rule("XNA907", "Custom content processor", Severity.Warning, Classification.Manual, RuleCategory.Content, new ContentItemMatch(null, null, "Outline*"), "Custom processors need a pipeline assembly.", "Port the processor to MonoGame.", null),
        Rule("XNA908", "XACT audio project", Severity.Error, Classification.Unsupported, RuleCategory.Content, new FilePatternMatch("**/*.xap"), "XACT is not supported by MonoGame.", "Export the sounds as plain wav files.", null),
        Rule("XNA909", "GamerServices namespace", Severity.Blocker, Classification.Unsupported, RuleCategory.Api, new NamespaceMatch("Microsoft.Xna.Framework.GamerServices", true), "Code uses GamerServices.", "Remove the calls.", null),
        Rule("XNA910", "Sprite font", Severity.Info, Classification.Manual, RuleCategory.Content, new ContentItemMatch(".spritefont", null, null), "Fonts must exist on the build machine.", "Check the font family.", null),
    ]);

    private static Rule Rule(string id, string title, Severity severity, Classification classification, RuleCategory category, RuleMatch match, string explanation, string resolution, string? migratorTask) =>
        new(id, title, severity, classification, category, null, match, explanation, resolution, migratorTask);
}
