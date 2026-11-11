namespace XnaReborn.Migrator.Projects;

/// <summary>Knows which files of a Visual Basic project belong to the My application model.</summary>
public static class VisualBasicProjectItems
{
    /// <summary>Tells whether a Compile item is a generated designer file in the <c>My Project</c> folder, such as <c>Application.Designer.vb</c>.</summary>
    /// <param name="compileInclude">The Compile item path as written in the project.</param>
    /// <returns>True for a <c>*.Designer.vb</c> file below a <c>My Project</c> folder.</returns>
    public static bool IsMyDesignerFile(string compileInclude)
    {
        ArgumentNullException.ThrowIfNull(compileInclude);
        var path = compileInclude.Replace('\\', '/');
        return path.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase)
            && path.Split('/').SkipLast(1).Any(s => s.Equals("My Project", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tells whether a file is a My application model settings file (<c>.myapp</c> or <c>.settings</c>) in a <c>My Project</c> folder.</summary>
    /// <param name="relativePath">The path relative to the project folder.</param>
    /// <returns>True for a <c>.myapp</c> or <c>.settings</c> file below a <c>My Project</c> folder.</returns>
    public static bool IsMyApplicationFile(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        var path = relativePath.Replace('\\', '/');
        return (path.EndsWith(".myapp", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".settings", StringComparison.OrdinalIgnoreCase))
            && path.Split('/').SkipLast(1).Any(s => s.Equals("My Project", StringComparison.OrdinalIgnoreCase));
    }
}
