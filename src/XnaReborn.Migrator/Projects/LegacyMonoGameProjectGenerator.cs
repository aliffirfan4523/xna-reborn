using System.Text;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Output;

namespace XnaReborn.Migrator.Projects;

/// <summary>An SDK-style project generated from an old MonoGame project.</summary>
/// <param name="Change">The created .csproj, at the old project's relative path.</param>
/// <param name="Notes">One line per thing that was left out and needs a person, or is worth knowing.</param>
/// <param name="KeptHintPaths">HintPath values (project-relative, '\' separators) of the references that were kept, so the migrator can copy the files.</param>
public sealed record GeneratedProject(FileChange Change, IReadOnlyList<string> Notes, IReadOnlyList<string> KeptHintPaths);

/// <summary>
/// Builds the SDK-style .csproj of a migrated old MonoGame project (installer era, <c>packages.config</c> era).
/// Pure: returns text and never touches disk. Everything the installer layout needs is dropped:
/// the installer imports, the MonoGame and OpenTK references, <c>PlatformTarget</c> and the .NET Framework target.
/// </summary>
public sealed class LegacyMonoGameProjectGenerator
{
    private static readonly HashSet<string> _droppedConstants = new(StringComparer.OrdinalIgnoreCase) { "DEBUG", "TRACE" };

    private static readonly HashSet<string> _frameworkAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "mscorlib", "System", "Microsoft.CSharp", "WindowsBase",
    };

    private static readonly string[] _replacedPrefixes = ["MonoGame.Framework", "OpenTK", "Tao.Sdl", "NVorbis", "SharpDX"];

    private readonly PackageSelector _selector;

    /// <summary>Creates a generator.</summary>
    /// <param name="selector">Package selector, or null for the pinned defaults.</param>
    public LegacyMonoGameProjectGenerator(PackageSelector? selector = null)
    {
        _selector = selector ?? new PackageSelector();
    }

    /// <summary>Generates the SDK-style project for one old MonoGame project.</summary>
    /// <param name="source">The project as read by the analyzer.</param>
    /// <param name="target">The MonoGame platform to target.</param>
    /// <param name="contentReferences">Paths of the <c>Content.mgcb</c> files relative to the project folder. Empty when the project has no content.</param>
    /// <param name="extraPackages">Entries mapped from <c>packages.config</c>.</param>
    /// <param name="skippedCopiedItems"><see cref="CopiedItem.Include"/> values of copy-to-output items to leave out, for example because the file does not exist.</param>
    /// <returns>The project, with notes and the kept HintPath values.</returns>
    public GeneratedProject Generate(
        ProjectReadResult source,
        MonoGameTarget target,
        IReadOnlyList<string> contentReferences,
        IReadOnlyList<PackageEntry> extraPackages,
        IReadOnlySet<string>? skippedCopiedItems = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(contentReferences);
        ArgumentNullException.ThrowIfNull(extraPackages);
        var info = source.Info;
        var packages = _selector.Select(target);
        var notes = new List<string>();

        var sb = new StringBuilder();
        sb.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n");
        sb.Append("  <PropertyGroup>\n");
        AppendProperty(sb, "OutputType", OutputType(source.Properties));
        AppendProperty(sb, "TargetFramework", packages.TargetFramework);
        AppendProperty(sb, "AssemblyName", info.AssemblyName);
        AppendProperty(sb, "RootNamespace", source.Properties.TryGetValue("RootNamespace", out var ns) && !string.IsNullOrWhiteSpace(ns) ? ns.Trim() : info.AssemblyName);
        if (source.Properties.TryGetValue("ApplicationIcon", out var icon) && !string.IsNullOrWhiteSpace(icon))
        {
            AppendProperty(sb, "ApplicationIcon", icon.Trim());
        }

        if (info.CompileFiles.Any(IsAssemblyInfo))
        {
            AppendProperty(sb, "GenerateAssemblyInfo", "false");
        }

        var constants = string.Join(';', info.DefineConstants.Select(c => c.Trim()).Where(c => c.Length > 0 && !_droppedConstants.Contains(c)).Distinct(StringComparer.Ordinal));
        if (constants.Length > 0)
        {
            AppendProperty(sb, "DefineConstants", constants);
        }

        AppendProperty(sb, "Nullable", "disable");
        AppendProperty(sb, "ImplicitUsings", "disable");
        sb.Append("  </PropertyGroup>\n");

        sb.Append("\n  <ItemGroup>\n");
        AppendPackage(sb, packages.FrameworkPackage, packages.FrameworkVersion, privateAssets: false);
        if (contentReferences.Count > 0)
        {
            AppendPackage(sb, packages.ContentBuilderPackage, packages.ContentBuilderVersion, privateAssets: false);
        }

        foreach (var package in CarriedPackages(source, extraPackages))
        {
            AppendPackage(sb, package.Id, package.Version, package.DevelopmentDependency);
        }

        sb.Append("  </ItemGroup>\n");

        if (contentReferences.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var reference in contentReferences)
            {
                sb.Append("    <MonoGameContentReference Include=\"").Append(Escape(reference.Trim().Replace('/', '\\'))).Append("\" />\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

        var linked = info.CompileFiles
            .Select(c => c.Trim().Replace('/', '\\'))
            .Where(c => c.StartsWith("..\\", StringComparison.Ordinal) && c.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (linked.Count > 0)
        {
            // Files outside the project folder (shared projects) are not found by the SDK's default globbing.
            sb.Append("\n  <ItemGroup>\n");
            foreach (var path in linked)
            {
                sb.Append("    <Compile Include=\"").Append(Escape(path)).Append("\" Link=\"").Append(Escape(path[(path.LastIndexOf('\\') + 1)..])).Append("\" />\n");
            }

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

        var kept = KeptReferences(source, notes);
        if (kept.Count > 0)
        {
            sb.Append("\n  <ItemGroup>\n");
            foreach (var (name, hint) in kept)
            {
                sb.Append("    <Reference Include=\"").Append(Escape(name)).Append("\">\n");
                sb.Append("      <HintPath>").Append(Escape(hint)).Append("</HintPath>\n");
                sb.Append("    </Reference>\n");
            }

            sb.Append("  </ItemGroup>\n");
        }

        var copied = SdkCsprojGenerator.CopiedItems(source)
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
        return new GeneratedProject(
            new FileChange(info.Path, ChangeKind.Created, null, sb.ToString(), null),
            notes,
            kept.Select(k => k.Hint).ToList());
    }

    // PackageReference items an old-style project already had, plus the packages.config entries. Same id wins once.
    private static IEnumerable<PackageEntry> CarriedPackages(ProjectReadResult source, IReadOnlyList<PackageEntry> extra)
    {
        var own = source.Items
            .Where(i => i.ItemType == "PackageReference" && !IsReplaced(i.Include) && i.Metadata.TryGetValue("Version", out var v) && !string.IsNullOrWhiteSpace(v))
            .Select(i => new PackageEntry(i.Include.Trim(), i.Metadata["Version"].Trim(), null));
        return own.Concat(extra).DistinctBy(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static List<(string Name, string Hint)> KeptReferences(ProjectReadResult source, List<string> notes)
    {
        var kept = new List<(string Name, string Hint)>();
        foreach (var item in source.Items.Where(i => i.ItemType == "Reference"))
        {
            var name = SimpleName(item.Include);
            if (_frameworkAssemblies.Contains(name)
                || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase)
                || IsReplaced(name))
            {
                if (name.StartsWith("MonoGame.Framework.Net", StringComparison.OrdinalIgnoreCase))
                {
                    notes.Add($"{source.Info.Path}: reference '{name}' was dropped: it was removed in MonoGame 3.8, so network code needs manual work.");
                }

                continue;
            }

            var hint = item.Metadata.TryGetValue("HintPath", out var h) && !string.IsNullOrWhiteSpace(h) ? h.Trim().Replace('/', '\\') : null;
            if (hint is null)
            {
                notes.Add($"{source.Info.Path}: reference '{name}' has no HintPath and was dropped; add the package or file by hand if the code uses it.");
            }
            else if (IsInstallOrPackagesPath(hint))
            {
                notes.Add($"{source.Info.Path}: reference '{name}' pointed into the MonoGame install folder or a packages folder and was dropped; add a package for it by hand if the code uses it.");
            }
            else
            {
                kept.Add((name, hint));
            }
        }

        return kept;
    }

    private static bool IsReplaced(string name) =>
        _replacedPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || name.StartsWith("MonoGame.Content.Builder", StringComparison.OrdinalIgnoreCase);

    private static bool IsInstallOrPackagesPath(string hint) =>
        hint.Contains("MonoGame\\v3.0", StringComparison.OrdinalIgnoreCase)
        || hint.Contains("$(MonoGameInstallDirectory)", StringComparison.OrdinalIgnoreCase)
        || hint.Contains("Program Files", StringComparison.OrdinalIgnoreCase)
        || hint.Contains("packages\\", StringComparison.OrdinalIgnoreCase)
        || hint.StartsWith("$(", StringComparison.Ordinal);

    private static string OutputType(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("OutputType", out var value))
        {
            var text = value.Trim();
            foreach (var known in new[] { "Library", "Exe" })
            {
                if (string.Equals(text, known, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
        }

        return "WinExe";
    }

    private static bool IsAssemblyInfo(string path) =>
        string.Equals(path.Replace('\\', '/'), "Properties/AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase);

    private static string SimpleName(string include)
    {
        var comma = include.IndexOf(',', StringComparison.Ordinal);
        return (comma < 0 ? include : include[..comma]).Trim();
    }

    private static void AppendProperty(StringBuilder sb, string name, string value) =>
        sb.Append("    <").Append(name).Append('>').Append(Escape(value)).Append("</").Append(name).Append(">\n");

    private static void AppendPackage(StringBuilder sb, string id, string version, bool privateAssets)
    {
        sb.Append("    <PackageReference Include=\"").Append(Escape(id)).Append("\" Version=\"").Append(Escape(version)).Append('"');
        if (privateAssets)
        {
            sb.Append(" PrivateAssets=\"all\"");
        }

        sb.Append(" />\n");
    }

    private static string Escape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
