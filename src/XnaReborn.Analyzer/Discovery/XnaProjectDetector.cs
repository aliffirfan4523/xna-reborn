using System.Xml.Linq;

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
                && ((string?)element.Attribute("Project"))?.Contains("Microsoft.Xna.GameStudio", StringComparison.OrdinalIgnoreCase) == true)
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
}
