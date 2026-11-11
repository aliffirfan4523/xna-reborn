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
    /// <param name="contentReferencePath">
    /// Path of <c>Content.mgcb</c> relative to the project folder, for the <c>MonoGameContentReference</c> item. Used only when the project
    /// has content. Defaults to <c>Content\Content.mgcb</c>. Either slash is accepted and the file gets backslashes.
    /// </param>
    /// <param name="skippedCopiedItems">
    /// <see cref="CopiedItem.Include"/> values of copy-to-output items to leave out, for example because the file does not exist.
    /// </param>
    /// <returns>A Created change at the source project's relative path.</returns>
    public FileChange Generate(
        ProjectReadResult source,
        MonoGameTarget target,
        string? contentReferencePath = null,
        IReadOnlySet<string>? skippedCopiedItems = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var info = source.Info;
        var packages = _selector.Select(target);
        // A Toolkit project keeps its assets in build-action items, not in a content project. It has content when a Content.mgcb was made for it.
        var isToolkit = info.Family == ProjectFamily.SharpDxToolkit;
        var hasContent = info.ContentProjects.Count > 0 || info.ContentItems.Any(i => i.InProject) || (isToolkit && contentReferencePath is not null);
        var isVisualBasic = info.Language == SourceLanguage.VisualBasic;

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

        if (isVisualBasic)
        {
            // The application model (MyType) needs types that SDK-style projects for a game do not have. With Empty, My.* code fails to compile (rule XNA112).
            AppendProperty(sb, "MyType", "Empty");
            foreach (var option in _visualBasicOptions)
            {
                if (source.Properties.TryGetValue(option, out var optionValue) && !string.IsNullOrWhiteSpace(optionValue))
                {
                    AppendProperty(sb, option, optionValue.Trim());
                }
            }

            if (source.Properties.TryGetValue("StartupObject", out var startup) && NamesAType(startup))
            {
                AppendProperty(sb, "StartupObject", startup.Trim());
            }
        }
        else
        {
            AppendProperty(sb, "Nullable", "disable");
            AppendProperty(sb, "ImplicitUsings", "disable");
        }

        sb.Append("  </PropertyGroup>\n");

        var namespaceImports = isVisualBasic ? source.Items.Where(i => i.ItemType == "NamespaceImport").Select(i => i.Include.Trim()).Distinct(StringComparer.Ordinal).ToList() : [];
        if (namespaceImports.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var import in namespaceImports)
            {
                sb.Append("    <Import Include=\"").Append(Escape(import)).Append("\" />\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

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
            var reference = string.IsNullOrWhiteSpace(contentReferencePath)
                ? "Content\\Content.mgcb"
                : contentReferencePath.Trim().Replace('/', '\\');
            sb.Append("    <MonoGameContentReference Include=\"").Append(Escape(reference)).Append("\" />\n");
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
            .Where(r => r.Hint is not null && !IsDroppedAssembly(r.Name) && !(isToolkit && r.Name.StartsWith("SharpDX", StringComparison.OrdinalIgnoreCase)))
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

        var copied = CopiedItems(source)
            .Where(i => i.Problem == CopiedItemProblem.None && skippedCopiedItems?.Contains(i.Include) != true)
            .ToList();
        if (copied.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var item in copied)
            {
                sb.Append("    <None Include=\"").Append(Escape(item.Include)).Append("\" CopyToOutputDirectory=\"").Append(Escape(item.CopyToOutputDirectory)).Append("\" />\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

        sb.Append("</Project>\n");
        return new FileChange(info.Path, ChangeKind.Created, null, sb.ToString(), null);
    }

    /// <summary>
    /// Lists the <c>Content</c> and <c>None</c> items of a project that ask to be copied to the output folder
    /// (<c>CopyToOutputDirectory</c> of <c>Always</c> or <c>PreserveNewest</c>), in document order, without repeats.
    /// </summary>
    /// <param name="source">The project as read by the analyzer.</param>
    /// <returns>One entry per item. Entries with a problem are not emitted into the generated project.</returns>
    public static IReadOnlyList<CopiedItem> CopiedItems(ProjectReadResult source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var projectPath = source.Info.Path.Replace('\\', '/');
        var slash = projectPath.LastIndexOf('/');
        var projectDirectory = slash < 0 ? string.Empty : projectPath[..slash];

        var result = new List<CopiedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source.Items)
        {
            if (item.ItemType is not ("Content" or "None")
                || !item.Metadata.TryGetValue("CopyToOutputDirectory", out var copy)
                || string.IsNullOrWhiteSpace(item.Include))
            {
                continue;
            }

            var mode = copy.Trim();
            if (!string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(mode, "PreserveNewest", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var include = item.Include.Trim().Replace('/', '\\');
            if (!seen.Add(include))
            {
                continue;
            }

            if (include.Contains('*', StringComparison.Ordinal) || include.Contains('?', StringComparison.Ordinal))
            {
                result.Add(new CopiedItem(include, mode, include, CopiedItemProblem.Wildcard));
                continue;
            }

            var rootRelative = Normalize(projectDirectory, include);
            result.Add(rootRelative is null
                ? new CopiedItem(include, mode, include, CopiedItemProblem.OutsideRoot)
                : new CopiedItem(include, mode, rootRelative, CopiedItemProblem.None));
        }

        return result;
    }

    private static string? Normalize(string projectDirectory, string include)
    {
        if (Path.IsPathRooted(include) || include.StartsWith('\\') || (include.Length > 1 && include[1] == ':'))
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var part in (projectDirectory + "/" + include.Replace('\\', '/')).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    private static readonly string[] _visualBasicOptions = ["OptionExplicit", "OptionStrict", "OptionCompare", "OptionInfer"];

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
        string.Equals(path.Replace('\\', '/'), "Properties/AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)
        || string.Equals(path.Replace('\\', '/'), "My Project/AssemblyInfo.vb", StringComparison.OrdinalIgnoreCase);

    // StartupObject is kept only when it names a type or module. "Sub Main" and the My application class are left out.
    private static bool NamesAType(string value)
    {
        var name = value.Trim();
        return name.Length > 0
            && !name.Equals("Sub Main", StringComparison.OrdinalIgnoreCase)
            && !name.EndsWith("My.MyApplication", StringComparison.OrdinalIgnoreCase);
    }

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
        || name.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase)
        || IsDeviceAssembly(name);

    // Windows Phone and ad assemblies have no desktop version (rules XNA060 to XNA063). Their types are stood in for or marked.
    private static bool IsDeviceAssembly(string name) =>
        name.Equals("Microsoft.Phone", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.Phone.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.Devices.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.Advertising", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("AdDuplex", StringComparison.OrdinalIgnoreCase);

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

/// <summary>Why a <see cref="CopiedItem"/> cannot be migrated.</summary>
public enum CopiedItemProblem
{
    /// <summary>No problem. The item is copied and emitted.</summary>
    None,

    /// <summary>The path points outside the source folder.</summary>
    OutsideRoot,

    /// <summary>The path holds a wildcard, which is not expanded.</summary>
    Wildcard,
}

/// <summary>A <c>Content</c> or <c>None</c> project item that is copied to the output folder.</summary>
/// <param name="Include">The path as written in the project, relative to the project folder, with '\' separators.</param>
/// <param name="CopyToOutputDirectory">The original value: <c>Always</c> or <c>PreserveNewest</c>.</param>
/// <param name="RootRelativePath">Path relative to the source root with '/' separators. Equals <paramref name="Include"/> when there is a problem.</param>
/// <param name="Problem">Whether the item can be migrated.</param>
public sealed record CopiedItem(string Include, string CopyToOutputDirectory, string RootRelativePath, CopiedItemProblem Problem);
