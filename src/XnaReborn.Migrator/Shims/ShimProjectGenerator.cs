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

    /// <summary>Path of the controls file relative to the output root.</summary>
    public const string ControlsFileName = "DEVICE-CONTROLS.md";

    private const string _shimReason = "Shim: part of the XnaReborn.Compat library, an offline stand-in for APIs MonoGame does not have.";

    private static readonly string[] _storageTypes = ["StorageContainer", "StorageDevice"];

    private static readonly string[] _gamerServicesTypes =
    [
        "Gamer", "GamerServicesComponent", "GamerServicesDispatcher", "Guide", "MessageBoxIcon",
        "SignedInEventArgs", "SignedInGamer", "SignedInGamerCollection", "SignedOutEventArgs",
    ];

    private static readonly (string Namespace, string Pattern, string[] Types)[] _deviceNamespaces =
    [
        ("Microsoft.Devices.Sensors", @"Microsoft\s*\.\s*Devices\s*\.\s*Sensors\b", ["Accelerometer", "AccelerometerReading", "AccelerometerReadingEventArgs", "SensorState"]),
        ("Microsoft.Phone.Marketplace", @"Microsoft\s*\.\s*Phone\s*\.\s*Marketplace\b", ["LicenseInformation"]),
        ("Microsoft.Phone.Shell", @"Microsoft\s*\.\s*Phone\s*\.\s*Shell\b", ["PhoneApplicationService"]),
        (
            "Microsoft.Phone.Tasks",
            @"Microsoft\s*\.\s*Phone\s*\.\s*Tasks\b",
            [
                "CameraCaptureTask", "EmailComposeTask", "MarketplaceDetailTask", "MarketplaceHubTask", "MarketplaceReviewTask",
                "MarketplaceSearchTask", "PhoneCallTask", "PhotoChooserTask", "SaveRingtoneTask", "SmsComposeTask", "WebBrowserTask",
            ]),
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
    /// <param name="includeDevice">True to add the device stand-ins and the input layer (device-to-desktop tasks 7 and 8).</param>
    /// <param name="controls">The keys and tilt numbers of the device part. The shipped values when null.</param>
    /// <param name="xactCatalog">The text of <c>XactCatalog.g.cs</c> to add the XACT stand-in (audio-and-shaders tasks 8 and 9) with that catalog. No XACT files when null.</param>
    /// <param name="includeSharpDx">True to add the SharpDX math stand-ins (<c>RectangleF</c>, <c>Color3</c>, <c>Color4</c>; sharpdx-toolkit task 14).</param>
    /// <returns>
    /// Four <see cref="ChangeKind.Created"/> changes under <c>XnaReborn.Compat/</c>, in a fixed order, followed by the
    /// device files and then the XACT files (each in a fixed order) when they are asked for.
    /// </returns>
    public IReadOnlyList<FileChange> Generate(MonoGameTarget target, bool includeDevice = false, DeviceControlsModel? controls = null, string? xactCatalog = null, bool includeSharpDx = false)
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

        var files = new List<FileChange>
        {
            Create(ProjectPath, project),
            Create(ProjectName + "/CompletedAsyncResult.cs", ShimSources.AsyncResult),
            Create(ProjectName + "/GamerServices.cs", ShimSources.GamerServices),
            Create(ProjectName + "/Storage.cs", ShimSources.Storage),
        };
        if (includeDevice)
        {
            files.AddRange(
            [
                Create(ProjectName + "/Device/DeviceControls.cs", (controls ?? DeviceControlsModel.Default).ToCSharp()),
                Create(ProjectName + "/Device/DeviceRuntime.cs", ShimDeviceSources.Runtime),
                Create(ProjectName + "/Device/Accelerometer.cs", ShimDeviceSources.Accelerometer),
                Create(ProjectName + "/Device/TiltMapper.cs", ShimDeviceSources.TiltMapper),
                Create(ProjectName + "/Device/PhoneShell.cs", ShimDeviceSources.PhoneShell),
                Create(ProjectName + "/Device/PhoneTasks.cs", ShimDeviceSources.PhoneTasks),
                Create(ProjectName + "/Device/Marketplace.cs", ShimDeviceSources.Marketplace),
                Create(ProjectName + "/Input/CompatGamePad.cs", ShimDeviceSources.CompatGamePad),
                Create(ProjectName + "/Input/DeviceInput.cs", ShimDeviceSources.DeviceInput),
                Create(ProjectName + "/Input/GamePadMapper.cs", ShimDeviceSources.GamePadMapper),
            ]);
        }

        if (includeSharpDx)
        {
            files.Add(Create(ProjectName + "/" + ShimSharpDxSources.FileName, ShimSharpDxSources.Source));
        }

        if (xactCatalog is not null)
        {
            files.AddRange(XactShimSources.Files.Select(f => Create(ProjectName + "/Xact/" + f.Name, f.Text)));
            files.Add(Create(ProjectName + "/Xact/XactCatalog.g.cs", xactCatalog));
        }

        return files;
    }

    /// <summary>Creates <c>DEVICE-CONTROLS.md</c>, written from the same table as <c>DeviceControls.cs</c>.</summary>
    /// <param name="controls">The keys and tilt numbers. The shipped values when null.</param>
    /// <returns>A <see cref="ChangeKind.Created"/> change at the output root.</returns>
    public static FileChange GenerateControlsFile(DeviceControlsModel? controls = null) =>
        new(ControlsFileName, ChangeKind.Created, null, (controls ?? DeviceControlsModel.Default).ToMarkdown(), "Device conversion: the keys, mouse actions and tilt values the player needs.");

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

        if (SharpDxPattern().IsMatch(sourceText) && FindTypes(sourceText, ShimSharpDxSources.Types) is { Length: > 0 } sharpDxTypes)
        {
            uses.Add(new ShimUse(ShimSharpDxSources.Namespace, sharpDxTypes));
        }

        foreach (var (ns, pattern, types) in _deviceNamespaces)
        {
            if (Regex.IsMatch(sourceText, pattern, RegexOptions.CultureInvariant))
            {
                uses.Add(new ShimUse(ns, FindTypes(sourceText, types)));
            }
        }

        return uses.OrderBy(u => u.Namespace, StringComparer.Ordinal).ToList();
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

    [GeneratedRegex(@"^\s*using\s+SharpDX\s*;", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex SharpDxPattern();

    [GeneratedRegex(@"Microsoft\s*\.\s*Xna\s*\.\s*Framework\s*\.\s*GamerServices\b", RegexOptions.CultureInvariant)]
    private static partial Regex GamerServicesPattern();
}

/// <summary>One shimmed namespace used by one source file.</summary>
/// <param name="Namespace">The original XNA namespace the file uses.</param>
/// <param name="Types">Shimmed type names the file mentions, sorted.</param>
public sealed record ShimUse(string Namespace, IReadOnlyList<string> Types);
