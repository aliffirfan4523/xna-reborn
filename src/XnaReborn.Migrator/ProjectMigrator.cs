using System.Reflection;
using System.Text;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Modern;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Migrator.Projects;
using XnaReborn.Migrator.Shims;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Migrator.Upgrade;

namespace XnaReborn.Migrator;

/// <summary>
/// Turns an analyzed XNA project into a migrated MonoGame copy. The source is only read. Nothing is written
/// before the output location is approved, and a real run writes into a staging folder that is moved into
/// place only when everything succeeded.
/// </summary>
public sealed class ProjectMigrator
{
    /// <summary>File name of the JSON manifest in the output folder.</summary>
    public const string ManifestJsonName = "xnareborn-manifest.json";

    /// <summary>File name of the Markdown manifest in the output folder.</summary>
    public const string ManifestMarkdownName = "xnareborn-manifest.md";

    private const string _stageDiscover = "Discovering projects";
    private const string _stagePlan = "Planning";
    private const string _stageProjects = "Generating projects";
    private const string _stageSource = "Rewriting source";
    private const string _stageContent = "Collecting content";
    private const string _stageConvert = "Converting content";
    private const string _stageWrite = "Writing files";
    private const string _stageDone = "Done";

    private const string _contentReason =
        "Content conversion is switched off for this project; this asset was copied unchanged and is not built.";

    private static readonly string[] _contentRuleIds = ["XNA005", "XNA049", "XNA101", "SDX010", "SDX011", "SDX012", "SDX014"];

    // Toolkit effects and compiled .tkb files are Manual or Unsupported, so they are not plan items. Their finding still starts the content conversion.
    private static readonly string[] _toolkitContentFindingIds = ["SDX013", "SDX015"];

    // Plan items that make the device part of the Compat library necessary (device-to-desktop tasks 7 and 8).
    private static readonly string[] _deviceRuleIds = ["XNA007", "XNA054", "XNA055", "XNA057", "XNA058", "XNA060", "XNA061", "XNA062"];

    // Manual device rules that only get a marker in the source. They are not plan items, so they are enabled by their finding.
    private static readonly string[] _markerRuleIds =
        [DeviceManualMarkerRewriter.MediaLibraryId, DeviceManualMarkerRewriter.ChooserId, DeviceManualMarkerRewriter.ThirdPartyId, .. ToolkitMarkerRewriter.CreateAll().Select(r => r.RuleId)];

    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly RewriterRegistry _rewriters;

    /// <summary>Creates a migrator.</summary>
    /// <param name="fileSystem">Read-only view of the source. Defaults to the real file system.</param>
    /// <param name="rewriters">Source rewriters. Defaults to <see cref="DefaultRewriters.Create"/>.</param>
    public ProjectMigrator(IReadOnlyFileSystem? fileSystem = null, RewriterRegistry? rewriters = null)
    {
        _fileSystem = fileSystem ?? new ReadOnlyFileSystem();
        _rewriters = rewriters ?? DefaultRewriters.Create();
    }

    /// <summary>Plans the migration and, unless it is a dry run, writes the migrated copy.</summary>
    /// <param name="sourcePath">The folder, .sln or .csproj that was analyzed.</param>
    /// <param name="report">The analyzer's report for <paramref name="sourcePath"/>.</param>
    /// <param name="options">What the user chose.</param>
    /// <param name="confirmedNonEmpty">True when the user agreed to replace a non-empty output folder.</param>
    /// <param name="progress">Receives progress updates, or null.</param>
    /// <param name="cancellationToken">Cancels the run. Nothing reaches the final output folder after cancellation.</param>
    /// <returns>The plan, every file change, the manifest and whether the output was completed.</returns>
    /// <exception cref="InvalidOperationException">The source has no XNA project.</exception>
    /// <exception cref="OutputGuardException">The output location is not safe.</exception>
    public async Task<MigrationResult> MigrateAsync(
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: discovery.
        progress?.Report(new MigrationProgress(_stageDiscover, 0, 1, null));
        var discovery = await new ProjectDiscovery(_fileSystem).DiscoverAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var xnaProjects = discovery.MigratableProjects.ToList();
        if (xnaProjects.Count == 0 && discovery.MonoGameProjects.Any())
        {
            // An old MonoGame upgrade has its own flow (monogame-upgrade spec). A folder with any XNA project keeps the XNA flow.
            return await new LegacyMonoGameMigrator(_fileSystem, _rewriters)
                .MigrateAsync(discovery, sourcePath, report, options, confirmedNonEmpty, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        if (xnaProjects.Count == 0)
        {
            throw new InvalidOperationException(
                report.NoProjectReason ?? discovery.NoProjectReason ?? "No XNA or SharpDX Toolkit project was found in the source.");
        }

        var root = discovery.Root;
        progress?.Report(new MigrationProgress(_stageDiscover, 1, 1, null));

        // Step 2: guard. Runs for a dry run too, so the user learns about a bad location early.
        cancellationToken.ThrowIfCancellationRequested();
        var guard = new OutputGuard();
        var verdict = guard.Check(root, options.OutputFolder, confirmedNonEmpty);
        if (!verdict.Allowed)
        {
            throw new OutputGuardException(verdict);
        }

        // Step 3: plan.
        progress?.Report(new MigrationProgress(_stagePlan, 0, 1, null));
        var plan = new MigrationPlanner().CreatePlan(report, options);
        var contentRuleIds = report.TriggeredRules
            .Where(r => r.MigratorTask?.StartsWith("content-conversion", StringComparison.Ordinal) == true
                || r.MigratorTask?.StartsWith(ToolkitRules.ContentTaskPrefix, StringComparison.Ordinal) == true)
            .Select(r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        progress?.Report(new MigrationProgress(_stagePlan, 1, 1, null));

        var modern = ModernExtrasChoice.From(options);
        var device = plan.Items.Any(i => _deviceRuleIds.Contains(i.RuleId, StringComparer.Ordinal));
        var state = new BuildState
        {
            Shims = options.EnableShims || device ? new ShimProjectGenerator(new PackageSelector()) : null,
            Device = device,
            Modern = modern.Any ? modern : null,
        };
        var reader = new ProjectReader(_fileSystem);
        var generator = new SdkCsprojGenerator();
        var projects = new List<ProjectWork>();

        // Platform copies (device-to-desktop:5.2): the projects of an included skip item are not migrated and are recorded as Skipped.
        var copyPlan = PlatformCopies.Find(report);
        var skips = copyPlan.Skips
            .Where(s => plan.Items.Any(i => string.Equals(i.Id, PlatformCopies.SkipItemId(s.Project), StringComparison.Ordinal)))
            .ToDictionary(s => s.Project, StringComparer.Ordinal);
        foreach (var skip in skips.Values)
        {
            state.Add(new FileChange(skip.Project, ChangeKind.Skipped, null, null, skip.Reason), [PlatformCopies.CopyRuleId]);
            state.Notes.Add($"{skip.Project}: {PlatformCopies.CopyRuleId}: skipped, {skip.Reason}. Its own files were not copied or merged.");
        }

        state.Notes.AddRange(copyPlan.Notes.Where(n => skips.Values.Any(s => n.StartsWith(s.Kept + " ", StringComparison.Ordinal))));

        // Windows Store and Windows Phone Toolkit projects (SDX004) cannot be hosted by MonoGame desktop. They are refused one by one, with no partial copy.
        var refused = xnaProjects
            .Where(d => d.IsSharpDxToolkit && report.Findings.Any(f => f.RuleId == ToolkitRules.StoreAppRuleId && string.Equals(f.Project, d.RelativePath, StringComparison.Ordinal)))
            .ToDictionary(d => d.RelativePath, StringComparer.Ordinal);
        foreach (var project in refused.Keys)
        {
            state.Add(new FileChange(project, ChangeKind.Skipped, null, null, ToolkitRules.StoreAppReason), [ToolkitRules.StoreAppRuleId]);
            state.Notes.Add($"{project}: {ToolkitRules.StoreAppRuleId}: {ToolkitRules.StoreAppReason}");
        }

        if (xnaProjects.All(d => skips.ContainsKey(d.RelativePath) || refused.ContainsKey(d.RelativePath)) && refused.Count > 0)
        {
            throw new InvalidOperationException($"Every project in the source was refused: {ToolkitRules.StoreAppReason}");
        }

        // Step 4: read the projects, convert their content, then write the project files and the solution.
        foreach (var discovered in xnaProjects.Where(d => !skips.ContainsKey(d.RelativePath) && !refused.ContainsKey(d.RelativePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await reader.ReadAsync(discovered.FullPath, root, cancellationToken).ConfigureAwait(false);
            var items = plan.Items.Where(p => string.Equals(p.Id, p.RuleId + ":" + discovered.RelativePath, StringComparison.Ordinal)).ToList();
            var enabled = items.Select(p => p.RuleId).Append(EntryPointSourceRewriter.Id).ToHashSet(StringComparer.Ordinal);
            if (items.Any(p => p.RuleId == "XNA007"))
            {
                enabled.Add(DeviceInstallRewriter.Id);
            }

            enabled.UnionWith(report.Findings
                .Where(f => string.Equals(f.Project, discovered.RelativePath, StringComparison.Ordinal) && _markerRuleIds.Contains(f.RuleId, StringComparer.Ordinal))
                .Select(f => f.RuleId));
            var projectLevel = items
                .Select(p => p.RuleId)
                .Where(id => !_rewriters.TryGet(id, out _) && !contentRuleIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var contentRules = items
                .Select(p => p.RuleId)
                .Concat(discovered.IsSharpDxToolkit
                    ? report.Findings.Where(f => string.Equals(f.Project, discovered.RelativePath, StringComparison.Ordinal) && _toolkitContentFindingIds.Contains(f.RuleId, StringComparer.Ordinal)).Select(f => f.RuleId)
                    : [])
                .Where(id => _contentRuleIds.Contains(id, StringComparer.Ordinal) || _toolkitContentFindingIds.Contains(id, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            projects.Add(new ProjectWork(discovered, read, enabled, projectLevel, contentRules));
        }

        var converting = projects.Where(p => p.ContentRules.Count > 0
            && (p.Read.Info.ContentProjects.Count > 0 || p.Read.Info.ContentItems.Any(i => i.InProject) || (p.Discovered.IsSharpDxToolkit && ToolkitContentConverter.HasContent(p.Read)))).ToList();
        var converter = new ContentConverter(_fileSystem);
        var toolkitConverter = new ToolkitContentConverter(_fileSystem);
        var contentOptions = new ContentOptions(options.Target, options.SubstituteFonts, options.RewriteEffects);
        for (var i = 0; i < converting.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = converting[i];
            progress?.Report(new MigrationProgress(_stageConvert, i, converting.Count, project.Discovered.RelativePath));
            project.Content = project.Discovered.IsSharpDxToolkit
                ? await toolkitConverter.ConvertAsync(project.Read, root, project.GameDirectory, contentOptions, cancellationToken).ConfigureAwait(false)
                : await converter.ConvertAsync(project.Read, root, project.GameDirectory, contentOptions, cancellationToken).ConfigureAwait(false);
            progress?.Report(new MigrationProgress(_stageConvert, i + 1, converting.Count, project.Discovered.RelativePath));
        }

        for (var i = 0; i < projects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var project = projects[i];
            progress?.Report(new MigrationProgress(_stageProjects, i, projects.Count, project.Discovered.RelativePath));

            var missingItems = MissingCopiedItems(project, root);
            FileChange csproj;
            if (project.Content?.MgcbRelativePath is { } mgcb)
            {
                var prefix = project.GameDirectory.Length == 0 ? string.Empty : project.GameDirectory + "/";
                csproj = generator.Generate(project.Read, options.Target, mgcb.StartsWith(prefix, StringComparison.Ordinal) ? mgcb[prefix.Length..] : mgcb, missingItems);
            }
            else
            {
                var withoutContent = project.Read with { Info = project.Read.Info with { ContentProjects = [], ContentItems = [] } };
                csproj = generator.Generate(withoutContent, options.Target, null, missingItems);
            }

            state.Add(csproj, project.ProjectLevelRules);
            progress?.Report(new MigrationProgress(_stageProjects, i + 1, projects.Count, project.Discovered.RelativePath));
        }

        // Isolation files go first so they win a path clash with a same-named file copied from the source.
        foreach (var isolation in new BuildIsolationGenerator().Generate())
        {
            state.Add(isolation, []);
        }

        // Shim library files go in early so they win a path clash with a same-named file from the source.
        if (state.Shims is not null)
        {
            foreach (var shim in state.Shims.Generate(options.Target, state.Device))
            {
                state.Add(shim, []);
            }

            if (state.Device)
            {
                state.Add(ShimProjectGenerator.GenerateControlsFile(), []);
            }
        }

        var isSolutionInput = string.Equals(Path.GetExtension(sourcePath), ".sln", StringComparison.OrdinalIgnoreCase);
        if (projects.Count >= 2 || isSolutionInput)
        {
            state.Add(
                new SolutionGenerator().Generate(
                    SolutionName(sourcePath, root, isSolutionInput),
                    projects.Select(p => p.Read.Info.Path).Concat(state.Shims is null ? [] : [ShimProjectGenerator.ProjectPath]).ToList()),
                []);
        }

        // Step 5: source files and the application icon.
        var files = projects.SelectMany(p => CompileFiles(p, root, state)).ToList();
        var engine = new SourceRewriteEngine(_rewriters);
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            progress?.Report(new MigrationProgress(_stageSource, i, files.Count, file.RelativePath));
            await RewriteFileAsync(engine, file, state, cancellationToken).ConfigureAwait(false);
            progress?.Report(new MigrationProgress(_stageSource, i + 1, files.Count, file.RelativePath));
        }

        if (state.Device)
        {
            foreach (var project in projects.Where(p => DevicePlatforms.IsDevice(p.Read.Info.Platform) && !state.DeviceInstalled.Contains(p.Read.Info.Path)))
            {
                state.Notes.Add($"{project.Read.Info.Path}: {DeviceInstallRewriter.Id}: no Game class with a constructor was found, so the DeviceInput component was not added. Add {DeviceInstallRewriter.Statement} to your Game constructor, or touch, tilt and the window setup will not work.");
            }
        }

        if (state.Shims is not null)
        {
            AddShimReferences(projects, state);
        }

        if (state.Modern is not null)
        {
            AddModernHelpers(projects, state);
        }

        foreach (var project in projects)
        {
            CopyApplicationIcon(project, root, state);
            CopyOutputItems(project, root, state);
        }

        // Step 6: converted content, then the unconverted copy for projects whose content rules were switched off.
        foreach (var project in projects.Where(p => p.Content is not null))
        {
            AddConvertedContent(project, state);
        }

        if (projects.Any(p => p.Content is not null))
        {
            state.Add(new ToolManifestGenerator().Generate(PackageVersions.Default.DotnetMgcb), []);
            state.Content = SumContent(projects.Where(p => p.Content is not null).Select(p => p.Content!.Report));
        }

        var contentSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentJobs = projects
            .Where(p => p.Content is null)
            .SelectMany(p => p.Read.Info.ContentProjects)
            .Where(contentSeen.Add)
            .ToList();
        for (var i = 0; i < contentJobs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new MigrationProgress(_stageContent, i, contentJobs.Count, contentJobs[i]));
            CopyContentProject(contentJobs[i], root, state, cancellationToken);
            progress?.Report(new MigrationProgress(_stageContent, i + 1, contentJobs.Count, contentJobs[i]));
        }

        // Step 9 (ordering): changes sorted by path.
        cancellationToken.ThrowIfCancellationRequested();
        var changes = state.Changes.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ToList();
        var notes = state.Notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var version = MigratorVersion();

        // Step 7: dry run.
        if (options.DryRun)
        {
            var dryManifest = BuildManifest(report, completed: false, changes, state, version);
            progress?.Report(new MigrationProgress(_stageDone, 1, 1, null));
            return new MigrationResult(plan, changes, dryManifest, Completed: false);
        }

        // Step 8: write into staging, then commit.
        cancellationToken.ThrowIfCancellationRequested();
        using var session = guard.Begin(root, options.OutputFolder, confirmedNonEmpty);
        try
        {
            progress?.Report(new MigrationProgress(_stageWrite, 0, 1, null));
            await new OutputWriter().WriteAsync(session, root, changes, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var manifest = BuildManifest(report, completed: true, changes, state, version);
            await WriteManifestFilesAsync(session.StagingFolder, manifest, notes, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            session.Commit();
            progress?.Report(new MigrationProgress(_stageWrite, 1, 1, null));
            progress?.Report(new MigrationProgress(_stageDone, 1, 1, null));
            return new MigrationResult(plan, changes, manifest, Completed: true);
        }
        catch
        {
            await TryWriteIncompleteManifestAsync(session, report, changes, state, notes, version).ConfigureAwait(false);
            throw;
        }
    }

    internal static string SolutionName(string sourcePath, string root, bool isSolutionInput)
    {
        if (isSolutionInput)
        {
            var name = Path.GetFileNameWithoutExtension(sourcePath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        var folder = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        return string.IsNullOrWhiteSpace(folder) ? "Solution" : folder;
    }

    internal static string MigratorVersion()
    {
        var text = typeof(ProjectMigrator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (text is not null)
        {
            var plus = text.IndexOf('+', StringComparison.Ordinal);
            if (plus >= 0)
            {
                text = text[..plus];
            }
        }

        return string.IsNullOrWhiteSpace(text) ? "0.1.0" : text.Trim();
    }

    internal static string Relative(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    internal static bool IsOutside(string relative) =>
        relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative);

    internal static string ToNative(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static Manifest BuildManifest(
        AnalysisReport report,
        bool completed,
        IReadOnlyList<FileChange> changes,
        BuildState state,
        string version) =>
        ManifestBuilder.Build(
            report.CatalogName,
            completed,
            changes,
            state.RuleIdsByPath,
            null,
            version,
            state.Content,
            state.ShimManifest(changes),
            state.ModernManifest(changes),
            SourceFrameworks(report),
            RulesFired(report));

    // Only a source with a SharpDX Toolkit project names its frameworks and fired rules in the manifest, so XNA manifests do not change.
    private static IReadOnlyList<string>? SourceFrameworks(AnalysisReport report) =>
        report.Projects.Any(p => p.Family == ProjectFamily.SharpDxToolkit)
            ? report.Projects.Select(p => p.Family).Where(f => f is ProjectFamily.SharpDxToolkit or ProjectFamily.Xna).Select(f => f.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
            : null;

    private static IReadOnlyList<string>? RulesFired(AnalysisReport report) =>
        SourceFrameworks(report) is null ? null : report.Findings.Select(f => f.RuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    internal static async Task WriteManifestFilesAsync(
        string staging,
        Manifest manifest,
        IReadOnlyList<string> notes,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(staging, ManifestJsonName), ManifestJsonWriter.Write(manifest), _utf8, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(staging, ManifestMarkdownName), ManifestMarkdownWriter.Write(manifest, notes), _utf8, cancellationToken).ConfigureAwait(false);
    }

    private static async Task TryWriteIncompleteManifestAsync(
        OutputSession session,
        AnalysisReport report,
        IReadOnlyList<FileChange> changes,
        BuildState state,
        IReadOnlyList<string> notes,
        string version)
    {
        try
        {
            if (!session.Committed && Directory.Exists(session.StagingFolder))
            {
                var manifest = BuildManifest(report, completed: false, changes, state, version);
                await WriteManifestFilesAsync(session.StagingFolder, manifest, notes, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort. The caller rethrows the original exception, and INCOMPLETE.txt already marks the folder.
        }
    }

    private IEnumerable<SourceFile> CompileFiles(ProjectWork project, string root, BuildState state)
    {
        var projectDirectory = Path.GetDirectoryName(project.Discovered.FullPath) ?? root;
        foreach (var compile in project.Read.Info.CompileFiles)
        {
            if (!compile.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(projectDirectory, ToNative(compile)));
            var relative = Relative(root, full);
            if (IsOutside(relative))
            {
                state.Notes.Add($"{project.Read.Info.Path}: {compile.Replace('\\', '/')}: this source file is outside the source folder and was not copied.");
                continue;
            }

            if (state.Claim(relative))
            {
                yield return new SourceFile(project, relative, full);
            }
        }
    }

    private async Task RewriteFileAsync(SourceRewriteEngine engine, SourceFile file, BuildState state, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(file.FullPath))
        {
            state.Changes.Add(new FileChange(
                file.RelativePath,
                ChangeKind.Skipped,
                null,
                null,
                $"The source file '{file.RelativePath}' is listed in {file.Project.Read.Info.Path} but does not exist."));
            return;
        }

        byte[] bytes;
        await using (var stream = _fileSystem.OpenRead(file.FullPath))
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        if (state.Shims is not null)
        {
            foreach (var use in ShimProjectGenerator.Scan(_utf8.GetString(bytes)))
            {
                state.ShimUses.Add((file.Project.Read.Info.Path, new ManifestShimUse(file.RelativePath, use.Namespace, use.Types)));
            }
        }

        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string text;
        try
        {
            text = _strictUtf8.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
        }
        catch (DecoderFallbackException)
        {
            state.Changes.Add(new FileChange(file.RelativePath, ChangeKind.Copied, null, null, null, file.FullPath));
            state.Notes.Add($"{file.RelativePath}: this file is not valid UTF-8, so it was copied unchanged and no source rewrite was applied.");
            return;
        }

        var result = engine.Rewrite(file.RelativePath, text, file.Project.Read.Info.DefineConstants, file.Project.Enabled, file.Project.Read.Info.Platform);
        if (state.Modern is not null)
        {
            var hook = ModernExtrasHook.Apply(result.Text, file.Project.Read.Info.DefineConstants);
            if (hook.Note is not null)
            {
                state.Notes.Add($"{file.RelativePath}: {ModernExtrasHook.Id}: {hook.Note}");
            }

            if (hook.Applied)
            {
                result = result with { Text = hook.Text, Changed = true, AppliedRuleIds = [.. result.AppliedRuleIds, ModernExtrasHook.Id] };
                state.ModernHooks[file.Project.Read.Info.Path] = file.RelativePath;
            }
        }

        foreach (var note in result.Notes.Where(n => n.NeedsManualWork))
        {
            state.Notes.Add($"{file.RelativePath}: {note.RuleId}: {note.Message}");
        }

        if (state.Shims is not null)
        {
            var projectPath = file.Project.Read.Info.Path;
            if (result.AppliedRuleIds.Contains(DeviceInstallRewriter.Id, StringComparer.Ordinal))
            {
                state.ShimUses.Add((projectPath, new ManifestShimUse(file.RelativePath, "XnaReborn.Compat.Input", ["DeviceInput"])));
                state.DeviceInstalled.Add(projectPath);
            }

            if (result.AppliedRuleIds.Contains(GamePadCallRewriter.Id, StringComparer.Ordinal))
            {
                state.ShimUses.Add((projectPath, new ManifestShimUse(file.RelativePath, "XnaReborn.Compat.Input", ["CompatGamePad"])));
            }
        }

        foreach (var failure in result.Failures)
        {
            state.Notes.Add($"{file.RelativePath}: {failure.RuleId}: the rewrite failed and was skipped: {failure.Message}");
        }

        if (result.Changed)
        {
            state.Add(
                new FileChange(file.RelativePath, ChangeKind.Modified, (hasBom ? "\uFEFF" : string.Empty) + text, result.Text, null),
                result.AppliedRuleIds,
                alreadyClaimed: true);
        }
        else
        {
            state.Changes.Add(new FileChange(file.RelativePath, ChangeKind.Copied, null, null, null, file.FullPath));
        }
    }

    // Adds the shim library to each game project: always keeps the library out of the project's own compile list,
    // and references it from projects whose source uses a shimmed namespace.
    private static void AddShimReferences(List<ProjectWork> projects, BuildState state)
    {
        foreach (var project in projects)
        {
            var path = project.Read.Info.Path;
            var index = state.Changes.FindIndex(c => string.Equals(c.RelativePath, path, StringComparison.Ordinal));
            if (index < 0)
            {
                continue;
            }

            var uses = state.ShimUses.Any(u => string.Equals(u.Project, path, StringComparison.Ordinal));
            state.Changes[index] = ShimProjectGenerator.AddToProject(state.Changes[index], project.GameDirectory, uses);
            if (uses)
            {
                state.ShimReferencedBy.Add(path.Replace('\\', '/'));
            }
        }
    }

    // Writes the helper next to each game project whose entry point got the hook. Says so when no project did.
    private static void AddModernHelpers(List<ProjectWork> projects, BuildState state)
    {
        foreach (var project in projects)
        {
            if (state.ModernHooks.ContainsKey(project.Read.Info.Path))
            {
                state.Add(ModernExtrasGenerator.Generate(project.GameDirectory, state.Modern!), []);
            }
        }

        if (state.ModernHooks.Count == 0)
        {
            state.Notes.Add($"{ModernExtrasHook.Id}: the window extras are on, but no 'game.Run();' call was found in a Main method, so they do nothing. Add {ModernExtrasGenerator.AttachCall}(game); before Run by hand.");
        }
    }

    private void CopyApplicationIcon(ProjectWork project, string root, BuildState state)
    {
        if (!project.Read.Properties.TryGetValue("ApplicationIcon", out var icon) || string.IsNullOrWhiteSpace(icon))
        {
            return;
        }

        var projectDirectory = Path.GetDirectoryName(project.Discovered.FullPath) ?? root;
        var full = Path.GetFullPath(Path.Combine(projectDirectory, ToNative(icon.Trim())));
        var relative = Relative(root, full);
        if (IsOutside(relative) || !_fileSystem.FileExists(full) || !state.Claim(relative))
        {
            return;
        }

        state.Changes.Add(new FileChange(relative, ChangeKind.Copied, null, null, null, full));
    }

    private HashSet<string> MissingCopiedItems(ProjectWork project, string root) =>
        SdkCsprojGenerator.CopiedItems(project.Read)
            .Where(i => i.Problem == CopiedItemProblem.None && !_fileSystem.FileExists(Path.GetFullPath(Path.Combine(root, ToNative(i.RootRelativePath)))))
            .Select(i => i.Include)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void CopyOutputItems(ProjectWork project, string root, BuildState state)
    {
        var projectPath = project.Read.Info.Path;
        foreach (var item in SdkCsprojGenerator.CopiedItems(project.Read))
        {
            var include = item.Include.Replace('\\', '/');
            if (item.Problem == CopiedItemProblem.OutsideRoot)
            {
                state.Notes.Add($"{projectPath}: {include}: this item is copied to the output folder but lies outside the source folder, so it was not copied.");
                continue;
            }

            if (item.Problem == CopiedItemProblem.Wildcard)
            {
                state.Notes.Add($"{projectPath}: {include}: wildcard items that are copied to the output folder are not expanded; add the files by hand.");
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(root, ToNative(item.RootRelativePath)));
            if (IsOutside(Relative(root, full)) || !_fileSystem.FileExists(full))
            {
                state.Notes.Add($"{projectPath}: {include}: this item is copied to the output folder but the file does not exist, so it was not copied.");
                continue;
            }

            if (!state.Claim(item.RootRelativePath))
            {
                continue;
            }

            if (item.RootRelativePath.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
            {
                const string reason = "Prebuilt .xnb content cannot be rebuilt for MonoGame and may not load. It is copied to the output folder as is.";
                state.Changes.Add(new FileChange(item.RootRelativePath, ChangeKind.ManualWork, null, null, reason, full));
                state.Notes.Add($"{item.RootRelativePath}: {reason}");
            }
            else
            {
                state.Changes.Add(new FileChange(item.RootRelativePath, ChangeKind.Copied, null, null, null, full));
            }
        }
    }
    private void CopyContentProject(string contentProject, string root, BuildState state, CancellationToken cancellationToken)
    {
        var projectFull = Path.GetFullPath(Path.Combine(root, ToNative(contentProject)));
        var directory = Path.GetDirectoryName(projectFull);
        if (directory is null || IsOutside(Relative(root, directory)) || !_fileSystem.DirectoryExists(directory))
        {
            state.Notes.Add($"{contentProject}: the content project folder was not found inside the source folder, so no content was copied.");
            return;
        }

        var copied = 0;
        foreach (var full in ContentFileCollector.Collect(_fileSystem, directory, cancellationToken))
        {
            var relative = Relative(root, full);
            if (!state.Claim(relative))
            {
                continue;
            }

            state.Changes.Add(new FileChange(relative, ChangeKind.ManualWork, null, null, _contentReason, full));
            copied++;
        }

        state.Notes.Add($"{contentProject}: content conversion is switched off for this project; {copied} file(s) were copied unchanged and are not built.");
    }

    private static void AddConvertedContent(ProjectWork project, BuildState state)
    {
        var result = project.Content!;
        foreach (var change in result.Changes)
        {
            if (!state.Claim(change.RelativePath))
            {
                state.Notes.Add($"{change.RelativePath}: another output file already uses this path, so this content file was not copied.");
                continue;
            }

            var ids = change.RelativePath == result.MgcbRelativePath ? project.ContentRules : [];
            state.Add(change, ids, alreadyClaimed: true);
        }

        state.Notes.AddRange(result.ManualNotes);
    }

    private static ManifestContent SumContent(IEnumerable<ContentReport> reports)
    {
        var list = reports.ToList();
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var status in Enum.GetValues<AssetStatus>())
        {
            byStatus[status.ToString()] = list.Sum(r => r.CountsByStatus.GetValueOrDefault(status));
        }

        var byType = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var type in list.SelectMany(r => r.CountsByType.Keys).Distinct(StringComparer.Ordinal))
        {
            byType[type] = list.Sum(r => r.CountsByType.GetValueOrDefault(type));
        }

        return new ManifestContent(byStatus, byType);
    }

    private sealed record ProjectWork(
        DiscoveredProject Discovered,
        ProjectReadResult Read,
        IReadOnlySet<string> Enabled,
        IReadOnlyList<string> ProjectLevelRules,
        IReadOnlyList<string> ContentRules)
    {
        /// <summary>The content conversion result, or null when the project has none or its content rules are switched off.</summary>
        public ContentConversionResult? Content { get; set; }

        /// <summary>The project folder relative to the output root, with '/' separators. Empty for the root.</summary>
        public string GameDirectory
        {
            get
            {
                var path = Read.Info.Path.Replace('\\', '/');
                var slash = path.LastIndexOf('/');
                return slash < 0 ? string.Empty : path[..slash];
            }
        }
    }

    private sealed record SourceFile(ProjectWork Project, string RelativePath, string FullPath);

    private sealed class BuildState
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public List<FileChange> Changes { get; } = [];

        public List<string> Notes { get; } = [];

        public ManifestContent? Content { get; set; }

        /// <summary>The shim generator, or null when shims are off.</summary>
        public ShimProjectGenerator? Shims { get; init; }

        /// <summary>True when the device part of the shim library is generated.</summary>
        public bool Device { get; init; }

        /// <summary>Paths of the projects whose Game constructor got the DeviceInput component.</summary>
        public HashSet<string> DeviceInstalled { get; } = new(StringComparer.Ordinal);

        /// <summary>The window extras that are on, or null when all are off.</summary>
        public ModernExtrasChoice? Modern { get; init; }

        /// <summary>Project path to the source file that got the extras hook.</summary>
        public Dictionary<string, string> ModernHooks { get; } = new(StringComparer.Ordinal);

        public List<(string Project, ManifestShimUse Use)> ShimUses { get; } = [];

        public SortedSet<string> ShimReferencedBy { get; } = new(StringComparer.Ordinal);

        /// <summary>The manifest block for the shims, or null when shims are off.</summary>
        public ManifestShims? ShimManifest(IReadOnlyList<FileChange> changes) =>
            Shims is null
                ? null
                : new ManifestShims(
                    ShimProjectGenerator.ProjectPath,
                    changes.Where(c => c.RelativePath.StartsWith(ShimProjectGenerator.ProjectName + "/", StringComparison.Ordinal)).Select(c => c.RelativePath).Order(StringComparer.Ordinal).ToList(),
                    ShimReferencedBy.ToList(),
                    ShimUses.Select(u => u.Use).OrderBy(u => u.File, StringComparer.Ordinal).ThenBy(u => u.Namespace, StringComparer.Ordinal).ToList());

        /// <summary>The manifest block for the window extras, or null when all are off.</summary>
        public ManifestModern? ModernManifest(IReadOnlyList<FileChange> changes) =>
            Modern is null
                ? null
                : new ManifestModern(
                    Modern.FeatureNames(),
                    changes.Where(c => c.RelativePath.EndsWith(ModernExtrasGenerator.FileName, StringComparison.Ordinal)).Select(c => c.RelativePath).Order(StringComparer.Ordinal).ToList(),
                    ModernHooks.Values.Order(StringComparer.Ordinal).ToList(),
                    ModernExtrasGenerator.Limits(Modern));

        public Dictionary<string, IReadOnlyList<string>> RuleIdsByPath { get; } = new(StringComparer.Ordinal);

        /// <summary>Reserves an output path. Returns false when an earlier change already uses it (case-insensitive).</summary>
        public bool Claim(string relativePath) => _paths.Add(relativePath);

        public void Add(FileChange change, IReadOnlyList<string> ruleIds, bool alreadyClaimed = false)
        {
            if (!alreadyClaimed && !Claim(change.RelativePath))
            {
                return;
            }

            Changes.Add(change);
            if (ruleIds.Count > 0)
            {
                RuleIdsByPath[change.RelativePath] = ruleIds;
            }
        }
    }
}
