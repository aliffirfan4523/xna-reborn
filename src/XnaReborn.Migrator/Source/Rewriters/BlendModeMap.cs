namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>Maps the members of the XNA 3.x <c>SpriteBlendMode</c> to the XNA 4.0 and MonoGame <c>BlendState</c> members. Shared by the C# and Visual Basic rewriters.</summary>
public static class BlendModeMap
{
    /// <summary>Looks up the <c>BlendState</c> member for a <c>SpriteBlendMode</c> member.</summary>
    /// <param name="spriteBlendModeMember">The member name, for example <c>AlphaBlend</c>.</param>
    /// <param name="blendState">The <c>BlendState</c> member, when the member is mapped.</param>
    /// <returns>True for <c>AlphaBlend</c>, <c>Additive</c> and <c>None</c>.</returns>
    public static bool TryGet(string spriteBlendModeMember, out string blendState)
    {
        ArgumentNullException.ThrowIfNull(spriteBlendModeMember);
        blendState = spriteBlendModeMember switch
        {
            "AlphaBlend" => "AlphaBlend",
            "Additive" => "Additive",
            "None" => "Opaque",
            _ => string.Empty,
        };
        return blendState.Length > 0;
    }
}
