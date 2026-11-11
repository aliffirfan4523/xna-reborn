using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Migrator.Source;

/// <summary>Builds the registry of every source rewriter the migrator ships.</summary>
public static class DefaultRewriters
{
    /// <summary>XNA009 (GamerServices) and XNA012 (Storage): markers that are enabled only when the shims are off (project-migrator 12.3).</summary>
    public static readonly IReadOnlyList<string> ShimFallbackIds = ["XNA009", "XNA012"];

    /// <summary>Creates a registry with ENTRYPOINT, DEVICEINPUT, XNA015, XNA022, XNA024, XNA039, XNA040, XNA053, XNA056, XNA059, XNA062, XNA063, XNA102, XNA103, the Visual Basic rewriters ENTRYPOINT, XNA022, XNA024, XNA039, XNA040, MG028 to MG032 and the SharpDX Toolkit rules SDX005 to SDX063 that have a rewriter or a marker.</summary>
    /// <returns>A new registry.</returns>
    public static RewriterRegistry Create()
    {
        var registry = new RewriterRegistry();
        registry.Register(new EntryPointSourceRewriter());
        registry.Register(new PlatformDirectiveRewriter());
        registry.Register(new GamePadCallRewriter());
        registry.Register(new XactAliasRewriter());
        registry.Register(new DeviceInstallRewriter());
        registry.Register(DeviceManualMarkerRewriter.MediaLibrary());
        registry.Register(DeviceManualMarkerRewriter.Choosers());
        registry.Register(DeviceManualMarkerRewriter.ThirdParty());
        registry.Register(new SpriteBatchBeginRewriter());
        registry.Register(new RenderStateRewriter());
        registry.Register(new EffectPassRewriter(EffectPassRewriter.PassRuleId));
        registry.Register(new EffectPassRewriter(EffectPassRewriter.EffectRuleId));
        registry.Register(new ColorTransparentBlackRewriter());
        registry.Register(new VisualBasicEntryPointSourceRewriter());
        registry.Register(new VisualBasicSpriteBatchBeginRewriter());
        foreach (var marker in VisualBasicMarkerRewriter.CreateAll())
        {
            registry.Register(marker);
        }

        registry.Register(LoadGraphicsContentRewriter.ForLoad());
        registry.Register(LoadGraphicsContentRewriter.ForUnload());
        registry.Register(new NamespaceMarkerRewriter(ShimFallbackIds[0], ["Microsoft.Xna.Framework.GamerServices"], "GamerServices has no MonoGame equivalent and the shims are off. Remove the calls or turn the shims on."));
        registry.Register(new NamespaceMarkerRewriter(ShimFallbackIds[1], ["Microsoft.Xna.Framework.Storage"], "Storage has no MonoGame equivalent and the shims are off. Save with System.IO or turn the shims on."));
        registry.Register(new NamespaceMarkerRewriter("MG028", ["Microsoft.Xna.Framework.Net"], "MonoGame 3.8 has no Microsoft.Xna.Framework.Net. Replace the network code or remove the feature."));
        registry.Register(new NamespaceMarkerRewriter("MG029", ["Microsoft.Xna.Framework.GamerServices"], "MonoGame 3.8 has no Microsoft.Xna.Framework.GamerServices. Replace the code or remove the feature."));
        registry.Register(new NamespaceMarkerRewriter("MG030", ["OpenTK"], "OpenTK is not part of MonoGame 3.8.6.1. Port the OpenGL code by hand or remove it."));
        registry.Register(new NamespaceMarkerRewriter("MG031", ["Windows.UI.Xaml", "Windows.ApplicationModel"], "This is a UWP API. It does not exist in the desktop project. Replace it by hand or remove it."));
        foreach (var ruleId in ToolkitNamespaceRewriter.RuleIds())
        {
            registry.Register(new ToolkitNamespaceRewriter(ruleId));
        }

        registry.Register(new ToolkitInputRewriter());
        registry.Register(ToolkitMathRewriter.ForMatrix());
        registry.Register(ToolkitMathRewriter.ForMathUtil());
        foreach (var ruleId in ToolkitGraphicsRewriter.RuleIds)
        {
            registry.Register(new ToolkitGraphicsRewriter(ruleId));
        }

        foreach (var marker in ToolkitMarkerRewriter.CreateAll())
        {
            registry.Register(marker);
        }

        return registry;
    }
}
