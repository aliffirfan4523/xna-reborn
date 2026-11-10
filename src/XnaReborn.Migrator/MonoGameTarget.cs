namespace XnaReborn.Migrator;

/// <summary>Which MonoGame platform package the migrated project uses.</summary>
public enum MonoGameTarget
{
    /// <summary>Cross-platform OpenGL (MonoGame.Framework.DesktopGL).</summary>
    DesktopGL,

    /// <summary>Windows DirectX (MonoGame.Framework.WindowsDX).</summary>
    WindowsDX,
}
