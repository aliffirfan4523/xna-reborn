using System.Reflection;
using System.Text;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Migrator.Projects;
using XnaReborn.Migrator.Source;

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
    private const string _stageWrite = "Writing files";
    private const string _stageDone = "Done";

    private const string _contentReason =
        "Content conversion is not wired in yet; this asset was copied unchanged and is not built.";

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
        var xnaProjects = discovery.XnaProjects.ToList();
        if (xnaProjects.Count == 0)
        {
            throw new InvalidOperationException(
                report.NoProjectReason ?? discovery.NoProjectReason ?? "No XNA project was found in the source.");
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
            .Where(r => r.MigratorTask?.StartsWith("content-conversion", StringComparison.Ordinal) == true)
            .Select(r => r.Id)
            .ToHashSet(StringComparer.Ordinal);
        progress?.Report(new MigrationProgress(_stagePlan, 1, 1, null));

        var state = new BuildState();
        var reader = new ProjectReader(_fileSystem);
        var generator = new SdkCsprojGenerator();
        var projects = new List<ProjectWork>();

        // Step 4: project files and the solution.
        for (var i = 0; i < xnaProjects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discovered = xnaProjects[i];
            progress?.Report(new MigrationProgress(_stageProjects, i, xnaProjects.Count, discovered.RelativePath));

            var read = await reader.ReadAsync(discovered.FullPath, root, cancellationToken).ConfigureAwait(false);
            var items = plan.Items.Where(p => string.Equals(p.Id, p.RuleId + ":" + discovered.RelativePath, StringComparison.Ordinal)).ToList();
            var enabled = items.Select(p => p.RuleId).Append(EntryPointSourceRewriter.Id).ToHashSet(StringComparer.Ordinal);
            var projectLevel = items
                .Select(p => p.RuleId)
                .Where(id => !_rewriters.TryGet(id, out _) && !contentRuleIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var withoutContent = read with { Info = read.Info with { ContentProjects = [], ContentItems = [] } };
            var csproj = generator.Generate(withoutContent, options.Target);
            state.Add(csproj, projectLevel);

            projects.Add(new ProjectWork(discovered, read, enabled));
            progress?.Report(new MigrationProgress(_stageProjects, i + 1, xnaProjects.Count, discovered.RelativePath));
        }

        var isSolutionInput = string.Equals(Path.GetExtension(sourcePath), ".sln", StringComparison.OrdinalIgnoreCase);
        if (projects.Count >= 2 || isSolutionInput)
        {
            state.Add(
                new SolutionGenerator().Generate(
                    SolutionName(sourcePath, root, isSolutionInput),
                    projects.Select(p => p.Read.Info.Path).ToList()),
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

        foreach (var project in projects)
        {
            CopyApplicationIcon(project, root, state);
        }

        // Step 6: content placeholder.
        var contentSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contentJobs = projects.SelectMany(p => p.Read.Info.ContentProjects).Where(contentSeen.Add).ToList();
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

    private static string SolutionName(string sourcePath, string root, bool isSolutionInput)
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

    private static string MigratorVersion()
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

    private static string Relative(string root, string full) =>
        Path.GetRelativePath(root, full).Replace('\\', '/');

    private static bool IsOutside(string relative) =>
        relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative);

    private static string ToNative(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static Manifest BuildManifest(
        AnalysisReport report,
        bool completed,
        IReadOnlyList<FileChange> changes,
        BuildState state,
        string version) =>
        ManifestBuilder.Build(report.CatalogName, completed, changes, state.RuleIdsByPath, null, version);

    private static async Task WriteManifestFilesAsync(
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

        var result = engine.Rewrite(file.RelativePath, text, file.Project.Read.Info.DefineConstants, file.Project.Enabled);
        foreach (var note in result.Notes.Where(n => n.NeedsManualWork))
        {
            state.Notes.Add($"{file.RelativePath}: {note.RuleId}: {note.Message}");
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

        state.Notes.Add($"{contentProject}: content conversion is not wired in yet; {copied} file(s) were copied unchanged and are not built.");
    }

    private sealed record ProjectWork(DiscoveredProject Discovered, ProjectReadResult Read, IReadOnlySet<string> Enabled);

    private sealed record SourceFile(ProjectWork Project, string RelativePath, string FullPath);

    private sealed class BuildState
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public List<FileChange> Changes { get; } = [];

        public List<string> Notes { get; } = [];

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
