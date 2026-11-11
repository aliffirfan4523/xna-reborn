using System.Reflection;
using System.Security;
using System.Text;
using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer;
using XnaReborn.Flash.Analyzer.Reading;
using XnaReborn.Flash.Analyzer.Rules;
using XnaReborn.Migrator;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Projects;

namespace XnaReborn.Flash.Converter;

/// <summary>Progress of a Flash migration.</summary>
/// <param name="Stage">Stage name.</param>
/// <param name="Completed">Units finished.</param>
/// <param name="Total">Units in the stage.</param>
/// <param name="Current">Item being worked on, or null.</param>
public sealed record FlashMigrationProgress(string Stage, int Completed, int Total, string? Current);

/// <summary>The result of a Flash migration.</summary>
/// <param name="OutputFolder">The migrated copy.</param>
/// <param name="ProjectFile">Relative path of the game project file.</param>
/// <param name="Manifest">What was written.</param>
/// <param name="Markers">Every marker left in converted code.</param>
/// <param name="Notes">Embeds and files that could not be converted.</param>
/// <param name="NotConverted">AS3 files that did not convert, with the reason.</param>
public sealed record FlashMigrationResult(
    string OutputFolder,
    string ProjectFile,
    Manifest Manifest,
    IReadOnlyList<ConversionMarker> Markers,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> NotConverted);

/// <summary>Creates a MonoGame copy of a Flash (AS3 + Flixel 2.x) source project. The original folder is only read.</summary>
public static class FlashMigrator
{
    /// <summary>The AS3 behaviors the conversion only approximates. They go into the manifest, the migration notes and the summary screen.</summary>
    public static IReadOnlyList<string> KnownDifferences { get; } =
    [
        "Number becomes double, and a Number field without a value starts as NaN, as in Flash.",
        "Integer division is converted so the result keeps its fraction, as AS3 does.",
        "Text uses a built-in pixel font and draws in capitals, so glyph shapes and spacing differ from Flash.",
        "Sounds are rebuilt through the MonoGame content pipeline, so decoding, looping and timing can differ.",
    ];

    /// <summary>Converts the project in <paramref name="sourceFolder"/> into <paramref name="outputFolder"/>.</summary>
    /// <param name="sourceFolder">The Flash project folder. It is never changed.</param>
    /// <param name="outputFolder">Where the migrated copy goes. A non-empty folder is refused unless confirmed.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace an existing non-empty output; the old one is moved aside, not deleted.</param>
    /// <param name="progress">Receives progress, or null.</param>
    /// <param name="cancellationToken">Cancels the run; the staging folder keeps its INCOMPLETE marker.</param>
    /// <exception cref="OutputGuardException">The output location was refused.</exception>
    /// <exception cref="InvalidOperationException">The project is not a convertible Flash project (no sources, or not exactly one FlxGame class).</exception>
    public static async Task<FlashMigrationResult> MigrateAsync(
        string sourceFolder,
        string outputFolder,
        bool confirmedNonEmpty = false,
        IProgress<FlashMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fs = new ReadOnlyFileSystem();
        progress?.Report(new FlashMigrationProgress("Reading", 0, 4, null));
        var project = await Task.Run(() => FlashProjectReader.Read(fs, sourceFolder, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (project.Detection.Kind != ProjectKind.Flash)
        {
            throw new InvalidOperationException("The folder is not a Flash project.");
        }

        if (project.Files.All(f => f.IsFlixelSource))
        {
            throw new InvalidOperationException("The project has no ActionScript source files to convert.");
        }

        if (project.EntryClasses.Count != 1)
        {
            throw new InvalidOperationException($"The converter needs exactly one class that extends FlxGame, but found {project.EntryClasses.Count}.");
        }

        var catalog = FlashRuleCatalog.LoadBuiltIn();
        var findings = FlashRuleEngine.Run(project, catalog, fs, cancellationToken);
        var assets = FlashAssetPlanner.Plan(project, fs);
        var entry = project.EntryClasses[0];
        var entryFile = project.Files.First(f => f.Path == entry.File);
        var gameName = SafeName(entry.Name);

        var changes = new List<FileChange>();
        var markers = new List<ConversionMarker>();
        var sites = new List<ManifestMarkerSite>();
        var notConverted = new List<string>();
        var emitter = new CSharpEmitter(project, assets.Bindings, findings);

        progress?.Report(new FlashMigrationProgress("Converting", 1, 4, null));
        var sources = project.Files.Where(f => !f.IsFlixelSource).ToList();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sources.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = sources[i];
            progress?.Report(new FlashMigrationProgress("Converting", i, sources.Count, file.Path));
            if (file.Unit.Diagnostics.Count > 0 && file.Unit.Members.Count == 0)
            {
                notConverted.Add($"{file.Path}: syntax errors, no declaration could be read.");
                continue;
            }

            var emitted = emitter.Emit(file);
            markers.AddRange(emitted.Markers);
            var target = UniquePath("Game/" + RelativeToRoot(project, file.Path)[..^3] + ".cs", used);
            sites.AddRange(emitted.Markers.Select(m => new ManifestMarkerSite(m.RuleId, m.File, m.Line, target)));
            changes.Add(new FileChange(target, ChangeKind.Created, null, emitted.Text, "Converted from " + file.Path + "."));
        }

        progress?.Report(new FlashMigrationProgress("Project", 2, 4, null));
        var entryNamespace = entryFile.Unit.PackageName;
        var entryType = entryNamespace.Length > 0 ? entryNamespace + "." + entry.Name : entry.Name;
        var selection = new PackageSelector().Select(MonoGameTarget.DesktopGL);
        changes.Add(new FileChange("Program.cs", ChangeKind.Created, null, ProgramText(entryType), "Starts the converted game."));
        changes.Add(new FileChange(gameName + ".csproj", ChangeKind.Created, null, CsprojText(selection, assets.Mgcb is not null), "The game project."));
        changes.AddRange(assets.Changes);
        changes.AddRange(FlixelRuntimeFiles());
        changes.AddRange(new BuildIsolationGenerator().Generate());
        if (assets.Mgcb is not null)
        {
            changes.Add(new ToolManifestGenerator().Generate(PackageVersions.Default.DotnetMgcb));
        }

        var ruleIds = findings.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var markerCounts = markers
            .GroupBy(m => m.RuleId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        using var session = new OutputGuard().Begin(sourceFolder, outputFolder, confirmedNonEmpty);
        var manifestChanges = changes.ToList();
        var manifest = ManifestBuilder.Build(catalog.Name, true, manifestChanges, new Dictionary<string, IReadOnlyList<string>>(), null, "flash-0.1", rulesFired: null) with
        {
            SourceKind = "Flash",
            Markers = markerCounts,
            MarkerSites = [.. sites.OrderBy(m => m.RuleId, StringComparer.Ordinal).ThenBy(m => m.File, StringComparer.Ordinal).ThenBy(m => m.Line)],
            Approximations = KnownDifferences,
            RulesFired = ruleIds,
        };
        changes.Add(new FileChange(ProjectMigrator.ManifestJsonName, ChangeKind.Created, null, ManifestJsonWriter.Write(manifest), "Record of this migration."));
        changes.Add(new FileChange("MIGRATION-NOTES.md", ChangeKind.Created, null, NotesText(project, markerCounts, sites, assets.Notes, notConverted), "What needs a person."));

        progress?.Report(new FlashMigrationProgress("Writing", 3, 4, null));
        await new OutputWriter().WriteAsync(session, sourceFolder, changes, cancellationToken).ConfigureAwait(false);
        session.Commit();
        progress?.Report(new FlashMigrationProgress("Done", 4, 4, null));
        return new FlashMigrationResult(session.OutputFolder, gameName + ".csproj", manifest, markers, assets.Notes, notConverted);
    }

    private static string RelativeToRoot(FlashProject project, string path)
    {
        foreach (var root in project.SourceRoots.Where(r => r != "."))
        {
            var prefix = root.TrimEnd('/') + "/";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return path[prefix.Length..];
            }
        }

        return path;
    }

    private static string UniquePath(string path, HashSet<string> used)
    {
        var p = path;
        var n = 2;
        while (!used.Add(p))
        {
            p = path[..^3] + n++ + ".cs";
        }

        return p;
    }

    private static string SafeName(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_');
        }

        return sb.Length == 0 ? "Game" : sb.ToString();
    }

    private static string ProgramText(string entryType) =>
        "// Starts the converted game.\nusing var game = new " + entryType + "();\ngame.Run();\n";

    private static string CsprojText(PackageSelection selection, bool hasContent)
    {
        var sb = new StringBuilder();
        sb.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n");
        sb.Append("  <PropertyGroup>\n");
        sb.Append("    <OutputType>Exe</OutputType>\n");
        sb.Append("    <TargetFramework>").Append(SecurityElement.Escape(selection.TargetFramework)).Append("</TargetFramework>\n");
        sb.Append("    <LangVersion>12.0</LangVersion>\n");
        sb.Append("    <Nullable>disable</Nullable>\n");
        sb.Append("    <ImplicitUsings>disable</ImplicitUsings>\n");
        sb.Append("    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>\n");
        sb.Append("    <NoWarn>$(NoWarn);CS0108;CS0114;CS0162;CS0168;CS0169;CS0219;CS0414;CS1998;CS8981</NoWarn>\n");
        sb.Append("    <RollForward>Major</RollForward>\n");
        sb.Append("    <MonoGamePlatform>DesktopGL</MonoGamePlatform>\n");
        sb.Append("  </PropertyGroup>\n\n");
        sb.Append("  <ItemGroup>\n");
        sb.Append("    <Compile Remove=\"XnaReborn.Flixel\\**\" />\n");
        sb.Append("    <PackageReference Include=\"").Append(selection.FrameworkPackage).Append("\" Version=\"").Append(selection.FrameworkVersion).Append("\" />\n");
        if (hasContent)
        {
            sb.Append("    <PackageReference Include=\"").Append(selection.ContentBuilderPackage).Append("\" Version=\"").Append(selection.ContentBuilderVersion).Append("\" />\n");
        }

        sb.Append("    <ProjectReference Include=\"XnaReborn.Flixel\\XnaReborn.Flixel.csproj\" />\n");
        sb.Append("  </ItemGroup>\n");
        if (hasContent)
        {
            sb.Append("\n  <ItemGroup>\n    <MonoGameContentReference Include=\"Content\\Content.mgcb\" />\n  </ItemGroup>\n");
        }

        sb.Append("\n</Project>\n");
        return sb.ToString();
    }

    private static string FlixelCsprojText(PackageSelection selection) =>
        "<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>" + selection.TargetFramework + "</TargetFramework>\n"
        + "    <LangVersion>12.0</LangVersion>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n"
        + "    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>\n    <RootNamespace>org.flixel</RootNamespace>\n  </PropertyGroup>\n\n"
        + "  <ItemGroup>\n    <PackageReference Include=\"" + selection.FrameworkPackage + "\" Version=\"" + selection.FrameworkVersion + "\" />\n  </ItemGroup>\n\n</Project>\n";

    private static IEnumerable<FileChange> FlixelRuntimeFiles()
    {
        var assembly = typeof(FlashMigrator).Assembly;
        const string prefix = "XnaReborn.Flixel/";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var text = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
            yield return new FileChange(name.Replace('\\', '/'), ChangeKind.Created, null, text, "Flixel runtime source.");
        }

        var selection = new PackageSelector().Select(MonoGameTarget.DesktopGL);
        yield return new FileChange("XnaReborn.Flixel/XnaReborn.Flixel.csproj", ChangeKind.Created, null, FlixelCsprojText(selection), "Flixel runtime project.");
        yield return new FileChange(
            "XnaReborn.Flixel/THIRD-PARTY-NOTICES.md",
            ChangeKind.Created,
            null,
            "# Third-party notices\n\nThe Flixel runtime in this folder is a C# implementation of the public Flixel 2.x API, written for XNA Reborn.\n"
            + "Flixel is copyright Adam Atomic and is distributed under the MIT License; this port keeps its class and member names.\n\n"
            + "MIT License\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files, "
            + "to deal in the software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, "
            + "sublicense, and/or sell copies of the software, subject to the inclusion of the original copyright notice and this permission notice. "
            + "The software is provided \"as is\", without warranty of any kind.\n",
            "Notice for the Flixel API.");
    }

    private static string NotesText(FlashProject project, IReadOnlyDictionary<string, int> markers, IReadOnlyList<ManifestMarkerSite> sites, IReadOnlyList<string> notes, IReadOnlyList<string> notConverted)
    {
        var sb = new StringBuilder();
        sb.Append("# Migration notes\n\n");
        sb.Append($"Entry class: {project.EntryClasses[0].Name}\n\n");
        sb.Append("## Markers left in the code\n\n");
        if (markers.Count == 0)
        {
            sb.Append("None.\n");
        }

        foreach (var (rule, count) in markers)
        {
            sb.Append($"- {rule}: {count}\n");
            foreach (var site in sites.Where(m => m.RuleId == rule).GroupBy(m => m.File, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.Append($"  - {site.Key}, line {string.Join(", ", site.Select(m => m.Line).Order())}\n");
            }
        }

        sb.Append("\nSearch the Game folder for `XNAREBORN:` to find each one.\n");
        if (notes.Count > 0 || notConverted.Count > 0)
        {
            sb.Append("\n## Not converted\n\n");
            foreach (var n in notes.Concat(notConverted))
            {
                sb.Append("- ").Append(n).Append('\n');
            }
        }

        sb.Append("\n## Known behavior differences\n\n");
        foreach (var difference in KnownDifferences)
        {
            sb.Append("- ").Append(difference).Append('\n');
        }

        return sb.ToString();
    }
}
