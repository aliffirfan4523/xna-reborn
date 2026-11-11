using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Migrator.Source;

/// <summary>Builds the registry of every source rewriter the migrator ships.</summary>
public static class DefaultRewriters
{
    /// <summary>Creates a registry with ENTRYPOINT, DEVICEINPUT, XNA022, XNA024, XNA039, XNA040, XNA053, XNA056, XNA059, XNA062, XNA063 and MG028 to MG032.</summary>
    /// <returns>A new registry.</returns>
    public static RewriterRegistry Create()
    {
        var registry = new RewriterRegistry();
        registry.Register(new EntryPointSourceRewriter());
        registry.Register(new PlatformDirectiveRewriter());
        registry.Register(new GamePadCallRewriter());
        registry.Register(new DeviceInstallRewriter());
        registry.Register(DeviceManualMarkerRewriter.MediaLibrary());
        registry.Register(DeviceManualMarkerRewriter.Choosers());
        registry.Register(DeviceManualMarkerRewriter.ThirdParty());
        registry.Register(new SpriteBatchBeginRewriter());
        registry.Register(new RenderStateRewriter());
        registry.Register(new EffectPassRewriter(EffectPassRewriter.PassRuleId));
        registry.Register(new EffectPassRewriter(EffectPassRewriter.EffectRuleId));
        registry.Register(new ColorTransparentBlackRewriter());
        registry.Register(new NamespaceMarkerRewriter("MG028", ["Microsoft.Xna.Framework.Net"], "MonoGame 3.8 has no Microsoft.Xna.Framework.Net. Replace the network code or remove the feature."));
        registry.Register(new NamespaceMarkerRewriter("MG029", ["Microsoft.Xna.Framework.GamerServices"], "MonoGame 3.8 has no Microsoft.Xna.Framework.GamerServices. Replace the code or remove the feature."));
        registry.Register(new NamespaceMarkerRewriter("MG030", ["OpenTK"], "OpenTK is not part of MonoGame 3.8.6.1. Port the OpenGL code by hand or remove it."));
        registry.Register(new NamespaceMarkerRewriter("MG031", ["Windows.UI.Xaml", "Windows.ApplicationModel"], "This is a UWP API. It does not exist in the desktop project. Replace it by hand or remove it."));
        return registry;
    }
}
