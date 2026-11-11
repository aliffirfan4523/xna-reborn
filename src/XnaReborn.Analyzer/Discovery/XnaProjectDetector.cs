using System.Xml.Linq;

using XnaReborn.Analyzer.Reading;

namespace XnaReborn.Analyzer.Discovery;

/// <summary>Decides from project XML whether a project uses XNA.</summary>
internal static class XnaProjectDetector
{
    /// <summary>Returns true when the document imports XNA Game Studio, sets XnaFrameworkVersion, or references the XNA Framework.</summary>
    /// <param name="document">Parsed .csproj document.</param>
    internal static bool IsXna(XDocument document)
    {
        foreach (var element in document.Descendants())
        {
            var name = element.Name.LocalName;
            if (name.Equals("XnaFrameworkVersion", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (name.Equals("Import", StringComparison.OrdinalIgnoreCase)
                && ((string?)element.Attribute("Project")) is { } import
                && (import.Contains("Microsoft.Xna.GameStudio", StringComparison.OrdinalIgnoreCase) || IsXnaTargets(import)))
            {
                return true;
            }

            if (name.Equals("ProjectTypeGuids", StringComparison.OrdinalIgnoreCase)
                && element.Value.Contains(XnaVersionDetector.ExpressProjectTypeGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (name.Equals("Reference", StringComparison.OrdinalIgnoreCase)
                && ((string?)element.Attribute("Include"))?.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }

    // Express 1.0 and 2.0 name their targets Microsoft.Xna.Common.targets, Microsoft.Xna.ContentPipeline.targets and similar.
    private static bool IsXnaTargets(string import) =>
        import.Contains("Microsoft.Xna.", StringComparison.OrdinalIgnoreCase) && import.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
}
