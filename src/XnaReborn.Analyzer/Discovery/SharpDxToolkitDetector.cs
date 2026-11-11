using System.Xml.Linq;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>Decides from project XML whether a project uses the SharpDX Toolkit. Reads only; never writes.</summary>
internal static class SharpDxToolkitDetector
{
    private const string _toolkitPrefix = "SharpDX.Toolkit";

    private static readonly string[] _buildActions = ["ToolkitFxc", "ToolkitFont", "ToolkitModel", "ToolkitTexture", "ToolkitAudio"];

    /// <summary>
    /// Returns true when any of these holds: a reference named SharpDX.Toolkit*, an import of SharpDX.Toolkit.targets,
    /// an item with a Toolkit build action, or a packages.config next to the project that lists a SharpDX.Toolkit* package.
    /// Plain SharpDX (SharpDX.targets alone, or SharpDX.Direct3D11) is not enough.
    /// </summary>
    /// <param name="document">Parsed .csproj document.</param>
    /// <param name="projectDirectory">Folder of the project, where packages.config is looked for.</param>
    /// <param name="fileSystem">Read-only file system used to read packages.config.</param>
    internal static bool IsSharpDxToolkit(XDocument document, string projectDirectory, IReadOnlyFileSystem fileSystem)
    {
        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if (name.Equals("Reference", StringComparison.OrdinalIgnoreCase)
                && ((string?)element.Attribute("Include"))?.StartsWith(_toolkitPrefix, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (name.Equals("Import", StringComparison.OrdinalIgnoreCase)
                && ((string?)element.Attribute("Project"))?.EndsWith("SharpDX.Toolkit.targets", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (Array.Exists(_buildActions, a => a.Equals(name, StringComparison.OrdinalIgnoreCase)) && element.Attribute("Include") is not null)
            {
                return true;
            }
        }

        return PackagesConfigListsToolkit(projectDirectory, fileSystem);
    }

    /// <summary>Classifies a project: SharpDxToolkit wins over every other family, otherwise the MonoGame detector decides.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    /// <param name="projectDirectory">Folder of the project.</param>
    /// <param name="fileSystem">Read-only file system used to read packages.config.</param>
    /// <param name="packages">Entries of the project's packages.config, or empty.</param>
    internal static ProjectFamily Classify(XDocument document, string projectDirectory, IReadOnlyFileSystem fileSystem, IReadOnlyList<PackageEntry> packages) =>
        IsSharpDxToolkit(document, projectDirectory, fileSystem) ? ProjectFamily.SharpDxToolkit : MonoGameProjectDetector.Classify(document, packages);

    private static bool PackagesConfigListsToolkit(string projectDirectory, IReadOnlyFileSystem fileSystem)
    {
        var path = Path.Combine(projectDirectory, "packages.config");
        try
        {
            if (!fileSystem.FileExists(path))
            {
                return false;
            }

            using var stream = fileSystem.OpenRead(path);
            return XDocument.Load(stream).Descendants()
                .Any(e => e.Name.LocalName.Equals("package", StringComparison.OrdinalIgnoreCase)
                    && ((string?)e.Attribute("id"))?.StartsWith(_toolkitPrefix, StringComparison.OrdinalIgnoreCase) == true);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
