namespace XnaReborn.Core.Models;

/// <summary>Works out the source language of a project from its file name.</summary>
public static class ProjectLanguage
{
    /// <summary>Gets the language of a project file: <c>.vbproj</c> is Visual Basic, anything else is C#.</summary>
    /// <param name="projectPath">Path of the project file.</param>
    /// <returns>The language.</returns>
    public static SourceLanguage FromPath(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        return projectPath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ? SourceLanguage.VisualBasic : SourceLanguage.CSharp;
    }
}
