using XnaReborn.Core.Models;

namespace XnaReborn.Analyzer.Reading;

/// <summary>The result of reading one content project.</summary>
/// <param name="Items">Content entries in document order.</param>
/// <param name="Properties">Project properties by name. The first value wins. Keys ignore case.</param>
/// <param name="References">Reference items, including custom pipeline assemblies (HintPath in the metadata).</param>
/// <param name="Diagnostics">Problems found while reading, such as ANA001.</param>
public sealed record ContentProjectReadResult(
    IReadOnlyList<ContentItem> Items,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ProjectItem> References,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    private const string _pipelinePrefix = "Microsoft.Xna.Framework.Content.Pipeline";

    /// <summary>Gets the <c>ContentRootDirectory</c> property, trimmed, with '/' separators and no trailing separator. <c>Content</c> when unset or blank.</summary>
    public string ContentRootDirectory
    {
        get
        {
            if (Properties.TryGetValue("ContentRootDirectory", out var value))
            {
                var normalized = value.Trim().Replace('\\', '/').Trim('/');
                if (normalized.Length > 0)
                {
                    return normalized;
                }
            }

            return "Content";
        }
    }

    /// <summary>Gets the <c>XnaProfile</c> property (for example Reach or HiDef), or null when unset or blank.</summary>
    public string? Profile =>
        Properties.TryGetValue("XnaProfile", out var value) && value.Trim().Length > 0 ? value.Trim() : null;

    /// <summary>Gets the Reference items that are not stock XNA content pipeline assemblies. Their HintPath metadata is kept.</summary>
    public IReadOnlyList<ProjectItem> PipelineReferences =>
        References.Where(r => !SimpleName(r.Include).StartsWith(_pipelinePrefix, StringComparison.OrdinalIgnoreCase)).ToList();

    private static string SimpleName(string include)
    {
        var comma = include.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? include : include[..comma]).Trim();
    }
}
