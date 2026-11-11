using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Migrator.Source;

/// <summary>Builds the registry of every source rewriter the migrator ships.</summary>
public static class DefaultRewriters
{
    /// <summary>Creates a registry with ENTRYPOINT, DEVICEINPUT, XNA022, XNA024, XNA039, XNA040, XNA053, XNA056, XNA059, XNA062 and XNA063.</summary>
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
        return registry;
    }
}
