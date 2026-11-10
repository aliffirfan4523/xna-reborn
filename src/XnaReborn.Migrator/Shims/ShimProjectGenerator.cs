using System.Text;
using System.Text.RegularExpressions;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;

namespace XnaReborn.Migrator.Shims;

/// <summary>
/// Writes the <c>XnaReborn.Compat</c> class library: offline stand-ins for the XNA storage and Xbox Live profile APIs that
/// MonoGame does not have. The types keep their original XNA namespaces, so game code compiles without source rewrites.
/// Pure: returns text and never touches disk.
/// </summary>
public sealed partial class ShimProjectGenerator
{
    /// <summary>Folder, project name and assembly name of the shim library.</summary>
    public const string ProjectName = "XnaReborn.Compat";

    /// <summary>Path of the shim project file relative to the output root, with '/' separators.</summary>
    public const string ProjectPath = ProjectName + "/" + ProjectName + ".csproj";

    /// <summary>Original XNA namespace of the storage stand-ins.</summary>
    public const string StorageNamespace = "Microsoft.Xna.Framework.Storage";

    /// <summary>Original XNA namespace of the profile and guide stand-ins.</summary>
    public const string GamerServicesNamespace = "Microsoft.Xna.Framework.GamerServices";

    private const string _shimReason = "Shim: part of the XnaReborn.Compat library, an offline stand-in for APIs MonoGame does not have.";

    private static readonly string[] _storageTypes = ["StorageContainer", "StorageDevice"];

    private static readonly string[] _gamerServicesTypes =
    [
        "Gamer", "GamerServicesComponent", "GamerServicesDispatcher", "Guide", "MessageBoxIcon",
        "SignedInEventArgs", "SignedInGamer", "SignedInGamerCollection", "SignedOutEventArgs",
    ];

    private readonly PackageSelector _selector;

    /// <summary>Creates a generator.</summary>
    /// <param name="selector">Package selector, or null for the pinned defaults.</param>
    public ShimProjectGenerator(PackageSelector? selector = null)
    {
        _selector = selector ?? new PackageSelector();
    }

    /// <summary>Creates the files of the shim library.</summary>
    /// <param name="target">The MonoGame platform of the migrated games. The library uses the same package.</param>
    /// <returns>Four <see cref="ChangeKind.Created"/> changes under <c>XnaReborn.Compat/</c>, in a fixed order.</returns>
    public IReadOnlyList<FileChange> Generate(MonoGameTarget target)
    {
        var packages = _selector.Select(target);
        var project = new StringBuilder()
            .Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n")
            .Append("  <PropertyGroup>\n")
            .Append("    <OutputType>Library</OutputType>\n")
            .Append("    <TargetFramework>").Append(packages.TargetFramework).Append("</TargetFramework>\n")
            .Append("    <AssemblyName>").Append(ProjectName).Append("</AssemblyName>\n")
            .Append("    <RootNamespace>").Append(ProjectName).Append("</RootNamespace>\n")
            .Append("    <Nullable>enable</Nullable>\n")
            .Append("    <ImplicitUsings>disable</ImplicitUsings>\n")
            .Append("  </PropertyGroup>\n\n")
            .Append("  <ItemGroup>\n")
            .Append("    <PackageReference Include=\"").Append(packages.FrameworkPackage).Append("\" Version=\"").Append(packages.FrameworkVersion).Append("\" />\n")
            .Append("  </ItemGroup>\n\n")
            .Append("</Project>\n")
            .ToString();

        return
        [
            Create(ProjectPath, project),
            Create(ProjectName + "/CompletedAsyncResult.cs", ShimSources.AsyncResult),
            Create(ProjectName + "/GamerServices.cs", ShimSources.GamerServices),
            Create(ProjectName + "/Storage.cs", ShimSources.Storage),
        ];
    }

    /// <summary>Finds which shimmed namespaces a source file uses, and which shimmed types it names.</summary>
    /// <param name="sourceText">The text of one C# file.</param>
    /// <returns>One entry per shimmed namespace the file mentions, sorted by namespace. Empty when none.</returns>
    /// <remarks>shortcut: a plain text match, so a namespace named only in a comment also counts; harmless extra reference, upgrade if that ever misleads.</remarks>
    public static IReadOnlyList<ShimUse> Scan(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        var uses = new List<ShimUse>();
        if (GamerServicesPattern().IsMatch(sourceText))
        {
            uses.Add(new ShimUse(GamerServicesNamespace, FindTypes(sourceText, _gamerServicesTypes)));
        }

        if (StoragePattern().IsMatch(sourceText))
        {
            uses.Add(new ShimUse(StorageNamespace, FindTypes(sourceText, _storageTypes)));
        }

        return uses;
    }

    /// <summary>Adds the shim library to a generated game project.</summary>
    /// <param name="project">The generated project file change.</param>
    /// <param name="projectDirectory">Folder of the project relative to the output root, with '/' separators. Empty for the root.</param>
    /// <param name="reference">True to add a project reference to the shim library.</param>
    /// <returns>
    /// The project with the library's source files excluded from its own compile list when the library sits inside the project folder
    /// (SDK projects compile every .cs file below them), plus the reference when asked.
    /// </returns>
    public static FileChange AddToProject(FileChange project, string projectDirectory, bool reference)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var depth = projectDirectory.Length == 0 ? 0 : projectDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
        var insideProject = projectDirectory.Length == 0;
        if (!reference && !insideProject)
        {
            return project;
        }

        var items = new StringBuilder("\n  <ItemGroup>\n");
        if (insideProject)
        {
            items.Append("    <Compile Remove=\"").Append(ProjectName).Append("\\**\" />\n");
        }

        if (reference)
        {
            items.Append("    <ProjectReference Include=\"").Append(string.Concat(Enumerable.Repeat("..\\", depth))).Append(ProjectName).Append('\\').Append(ProjectName).Append(".csproj\" />\n");
        }

        items.Append("  </ItemGroup>\n");
        var text = project.NewText ?? string.Empty;
        var end = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (end < 0)
        {
            return project;
        }

        return project with
        {
            NewText = text[..end] + items.ToString() + text[end..],
            Reason = reference ? "Shim: references XnaReborn.Compat." : project.Reason,
        };
    }

    private static string[] FindTypes(string text, string[] candidates) =>
        candidates.Where(t => Regex.IsMatch(text, @"\b" + t + @"\b", RegexOptions.CultureInvariant)).Order(StringComparer.Ordinal).ToArray();

    private static FileChange Create(string path, string text) =>
        new(path, ChangeKind.Created, null, text.ReplaceLineEndings("\n").TrimEnd('\n') + "\n", _shimReason);

    [GeneratedRegex(@"Microsoft\s*\.\s*Xna\s*\.\s*Framework\s*\.\s*Storage\b", RegexOptions.CultureInvariant)]
    private static partial Regex StoragePattern();

    [GeneratedRegex(@"Microsoft\s*\.\s*Xna\s*\.\s*Framework\s*\.\s*GamerServices\b", RegexOptions.CultureInvariant)]
    private static partial Regex GamerServicesPattern();
}

/// <summary>One shimmed namespace used by one source file.</summary>
/// <param name="Namespace">The original XNA namespace the file uses.</param>
/// <param name="Types">Shimmed type names the file mentions, sorted.</param>
public sealed record ShimUse(string Namespace, IReadOnlyList<string> Types);
