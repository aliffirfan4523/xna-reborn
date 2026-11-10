using System.Text;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Projects;

/// <summary>Builds the SDK-style .csproj of a migrated project. Pure: returns text and never touches disk.</summary>
public sealed class SdkCsprojGenerator
{
    private static readonly HashSet<string> _droppedConstants = new(StringComparer.OrdinalIgnoreCase)
    {
        "DEBUG", "TRACE", "XBOX", "XBOX360", "ZUNE", "WINDOWS_PHONE",
    };

    private static readonly HashSet<string> _frameworkAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib", "System", "Microsoft.CSharp", "WindowsBase",
    };

    private readonly PackageSelector _selector;

    /// <summary>Creates a generator.</summary>
    /// <param name="selector">Package selector, or null for the pinned defaults.</param>
    public SdkCsprojGenerator(PackageSelector? selector = null)
    {
        _selector = selector ?? new PackageSelector();
    }

    /// <summary>Generates the SDK-style project for one source project.</summary>
    /// <param name="source">The project as read by the analyzer.</param>
    /// <param name="target">The MonoGame platform to target.</param>
    /// <returns>A Created change at the source project's relative path.</returns>
    public FileChange Generate(ProjectReadResult source, MonoGameTarget target)
    {
        ArgumentNullException.ThrowIfNull(source);
        var info = source.Info;
        var packages = _selector.Select(target);
        var hasContent = info.ContentProjects.Count > 0;

        var sb = new StringBuilder();
        sb.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n");
        sb.Append("  <PropertyGroup>\n");
        AppendProperty(sb, "OutputType", OutputType(source.Properties));
        AppendProperty(sb, "TargetFramework", packages.TargetFramework);
        AppendProperty(sb, "AssemblyName", info.AssemblyName);
        AppendProperty(sb, "RootNamespace", RootNamespace(source));
        if (source.Properties.TryGetValue("ApplicationIcon", out var icon) && !string.IsNullOrWhiteSpace(icon))
        {
            AppendProperty(sb, "ApplicationIcon", icon.Trim());
        }

        if (info.CompileFiles.Any(IsAssemblyInfo))
        {
            AppendProperty(sb, "GenerateAssemblyInfo", "false");
        }

        var constants = DefineConstants(info);
        if (constants.Length > 0)
        {
            AppendProperty(sb, "DefineConstants", constants);
        }

        AppendProperty(sb, "Nullable", "disable");
        AppendProperty(sb, "ImplicitUsings", "disable");
        sb.Append("  </PropertyGroup>\n");

        sb.Append("\n  <ItemGroup>\n");
        AppendPackage(sb, packages.FrameworkPackage, packages.FrameworkVersion);
        if (hasContent)
        {
            AppendPackage(sb, packages.ContentBuilderPackage, packages.ContentBuilderVersion);
        }

        sb.Append("  </ItemGroup>\n");

        if (hasContent)
        {
            sb.Append("\n  <ItemGroup>\n");
            sb.Append("    <MonoGameContentReference Include=\"Content\\Content.mgcb\" />\n");
            sb.Append("  </ItemGroup>\n");
        }

        var projectReferences = source.Items
            .Where(i => i.ItemType == "ProjectReference" && i.Include.Trim().EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Include.Trim().Replace('/', '\\'))
            .ToList();
        if (projectReferences.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var reference in projectReferences)
            {
                sb.Append("    <ProjectReference Include=\"").Append(Escape(reference)).Append("\" />\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

        var references = source.Items
            .Where(i => i.ItemType == "Reference")
            .Select(i => (Name: SimpleName(i.Include), Hint: HintPath(i)))
            .Where(r => r.Hint is not null && !IsDroppedAssembly(r.Name))
            .ToList();
        if (references.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var (name, hint) in references)
            {
                sb.Append("    <Reference Include=\"").Append(Escape(name)).Append("\">\n");
                sb.Append("      <HintPath>").Append(Escape(hint!)).Append("</HintPath>\n");
                sb.Append("    </Reference>\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

        sb.Append("</Project>\n");
        return new FileChange(info.Path, ChangeKind.Created, null, sb.ToString(), null);
    }

    private static string OutputType(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("OutputType", out var value)
            && string.Equals(value.Trim(), "Library", StringComparison.OrdinalIgnoreCase))
        {
            return "Library";
        }

        return "WinExe";
    }

    private static string RootNamespace(ProjectReadResult source) =>
        source.Properties.TryGetValue("RootNamespace", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : source.Info.AssemblyName;

    private static string DefineConstants(ProjectInfo info)
    {
        var kept = info.DefineConstants
            .Select(c => c.Trim())
            .Where(c => c.Length > 0 && !_droppedConstants.Contains(c))
            .Where(c => !string.Equals(c, "WINDOWS", StringComparison.OrdinalIgnoreCase) || info.Platform == XnaPlatform.Windows)
            .Distinct(StringComparer.Ordinal);
        return string.Join(';', kept);
    }

    private static bool IsAssemblyInfo(string path) =>
        string.Equals(path.Replace('\\', '/'), "Properties/AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase);

    private static string SimpleName(string include)
    {
        var comma = include.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? include : include[..comma]).Trim();
    }

    private static string? HintPath(ProjectItem item) =>
        item.Metadata.TryGetValue("HintPath", out var hint) && !string.IsNullOrWhiteSpace(hint)
            ? hint.Trim().Replace('/', '\\')
            : null;

    private static bool IsDroppedAssembly(string name) =>
        _frameworkAssemblies.Contains(name)
        || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase);

    private static void AppendProperty(StringBuilder sb, string name, string value) =>
        sb.Append("    <").Append(name).Append('>').Append(Escape(value)).Append("</").Append(name).Append(">\n");

    private static void AppendPackage(StringBuilder sb, string id, string version) =>
        sb.Append("    <PackageReference Include=\"").Append(Escape(id)).Append("\" Version=\"").Append(Escape(version)).Append("\" />\n");

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
