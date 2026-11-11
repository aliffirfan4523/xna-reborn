using System.Text;
using System.Xml.Linq;
using XnaReborn.Analyzer.Discovery;
using XnaReborn.Analyzer.IO;
using XnaReborn.Analyzer.Reading;
using XnaReborn.Core.Content;
using XnaReborn.Core.Models;
using XnaReborn.Migrator.Content;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Planning;
using XnaReborn.Migrator.Projects;
using XnaReborn.Migrator.Shims;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Upgrade;

/// <summary>
/// The monogame-upgrade flow of <see cref="ProjectMigrator"/>: turns old MonoGame projects (installer era, <c>packages.config</c> era,
/// or SDK-style on an older 3.8) into a copy on the pinned MonoGame 3.8.6.1 and .NET 8. The source is only read, and a real run writes
/// into a staging folder that is moved into place only when everything succeeded.
/// </summary>
internal sealed class LegacyMonoGameMigrator
{
    private const string _stagePlan = "Planning";
    private const string _stageProjects = "Generating projects";
    private const string _stageSource = "Rewriting source";
    private const string _stageContent = "Collecting content";
    private const string _stageWrite = "Writing files";
    private const string _stageDone = "Done";

    // Manual and Unsupported source rules that only get a marker above the using (no plan item, so enabled by their finding).
    private static readonly string[] _markerRuleIds = ["MG028", "MG029", "MG030", "MG031"];

    // MG029 (GamerServices) gets no marker when the shims are on: XnaReborn.Compat has the stand-ins (monogame-upgrade task 13.2).
    private const string _gamerServicesRuleId = "MG029";

    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly IReadOnlyFileSystem _fileSystem;
    private readonly RewriterRegistry _rewriters;

    internal LegacyMonoGameMigrator(IReadOnlyFileSystem fileSystem, RewriterRegistry rewriters)
    {
        _fileSystem = fileSystem;
        _rewriters = rewriters;
    }

    /// <summary>Chooses the desktop target for an old platform: DirectX Windows becomes WindowsDX, everything else DesktopGL.</summary>
    /// <param name="platform">The platform the analyzer detected.</param>
    internal static MonoGameTarget ChooseTarget(MonoGamePlatform platform) =>
        platform == MonoGamePlatform.WindowsDX ? MonoGameTarget.WindowsDX : MonoGameTarget.DesktopGL;

    internal async Task<MigrationResult> MigrateAsync(
        DiscoveryResult discovery,
        string sourcePath,
        AnalysisReport report,
        MigrationOptions options,
        bool confirmedNonEmpty,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var root = discovery.Root;
        var candidates = discovery.MonoGameProjects.ToList();
        if (!candidates.Any(p => p.Family == ProjectFamily.MonoGameLegacy))
        {
            throw new InvalidOperationException(report.NoProjectReason ?? discovery.NoProjectReason ?? "All MonoGame projects are already current. Nothing to migrate.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var guard = new OutputGuard();
        var verdict = guard.Check(root, options.OutputFolder, confirmedNonEmpty);
        if (!verdict.Allowed)
        {
            throw new OutputGuardException(verdict);
        }

        progress?.Report(new MigrationProgress(_stagePlan, 0, 1, null));
        var plan = new MigrationPlanner().CreatePlan(report, options);
        progress?.Report(new MigrationProgress(_stagePlan, 1, 1, null));

        var state = new State { ShimsEnabled = options.EnableShims };
        var reader = new ProjectReader(_fileSystem);
        var versions = PackageVersions.Default;
        var generator = new LegacyMonoGameProjectGenerator();
        var migrated = new List<Work>();
        var skippedProjects = new List<string>();

        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discovered = candidates[i];
            progress?.Report(new MigrationProgress(_stageProjects, i, candidates.Count, discovered.RelativePath));
            var read = await reader.ReadAsync(discovered.FullPath, root, cancellationToken).ConfigureAwait(false);
            if (discovered.Family == ProjectFamily.MonoGameLegacy && MigrationScope.IsPlatformProject(read.Info.MonoGamePlatform))
            {
                skippedProjects.Add(discovered.RelativePath);
                CopyPlatformProject(discovered, read, root, state, cancellationToken);
                continue;
            }

            var text = _fileSystem.ReadAllText(discovered.FullPath);
            var sdkStyle = IsSdkStyle(text);
            var items = plan.Items.Where(p => string.Equals(p.Id, p.RuleId + ":" + discovered.RelativePath, StringComparison.Ordinal)).ToList();
            var enabled = items.Select(p => p.RuleId).ToHashSet(StringComparer.Ordinal);
            enabled.UnionWith(report.Findings
                .Where(f => string.Equals(f.Project, discovered.RelativePath, StringComparison.Ordinal) && _markerRuleIds.Contains(f.RuleId, StringComparer.Ordinal)
                    && !(options.EnableShims && string.Equals(f.RuleId, _gamerServicesRuleId, StringComparison.Ordinal)))
                .Select(f => f.RuleId));
            if (!sdkStyle)
            {
                // Old templates wrap Main in platform conditionals such as "#if WINDOWS || LINUX".
                enabled.Add(EntryPointSourceRewriter.Id);
            }

            var projectLevel = items.Select(p => p.RuleId).Where(id => !_rewriters.TryGet(id, out _)).Distinct(StringComparer.Ordinal).ToList();
            var work = new Work(discovered, read, enabled, sdkStyle);
            migrated.Add(work);

            if (work.SdkStyle)
            {
                work.Target = text.Contains("MonoGame.Framework.WindowsDX", StringComparison.OrdinalIgnoreCase) ? MonoGameTarget.WindowsDX : MonoGameTarget.DesktopGL;
                var bump = SdkProjectBumper.Bump(text, versions);
                state.Notes.AddRange(bump.Notes.Select(n => $"{discovered.RelativePath}: {n}"));
                state.Add(
                    string.Equals(bump.Text, text, StringComparison.Ordinal)
                        ? new FileChange(discovered.RelativePath, ChangeKind.Copied, null, null, null, discovered.FullPath)
                        : new FileChange(discovered.RelativePath, ChangeKind.Modified, text, bump.Text, null),
                    projectLevel);
            }
            else
            {
                var target = options.LegacyTarget ?? ChooseTarget(read.Info.MonoGamePlatform);
                var mapping = PackagesConfigMapper.Map(read.Packages);
                foreach (var drop in mapping.Dropped)
                {
                    state.Notes.Add($"{discovered.RelativePath}: packages.config: {drop.Id} {drop.Version} was not carried over. {drop.Reason}");
                }

                foreach (var manual in mapping.Manual)
                {
                    state.Notes.Add($"{discovered.RelativePath}: packages.config: {manual.Id} {manual.Version} needs manual work. {manual.Reason}");
                }

                var contentReferences = (read.MgcbFiles ?? []).Select(m => ProjectRelative(work, m.Path)).ToList();
                var generated = generator.Generate(read, target, contentReferences, mapping.Mapped, MissingCopiedItems(work, root));
                state.Add(generated.Change, projectLevel);
                state.Notes.AddRange(generated.Notes);
                work.KeptHintPaths = generated.KeptHintPaths;
                work.Target = target;
                if (read.Packages is { Count: > 0 })
                {
                    var packagesConfig = ProjectDirectory(work) is { Length: > 0 } d ? d + "/packages.config" : "packages.config";
                    state.Add(
                        new FileChange(packagesConfig, ChangeKind.Skipped, null, null, "Replaced by PackageReference items in the migrated project."),
                        ["MG007"]);
                }
            }

            progress?.Report(new MigrationProgress(_stageProjects, i + 1, candidates.Count, discovered.RelativePath));
        }

        if (migrated.Count == 0)
        {
            throw new InvalidOperationException(
                "No desktop project to migrate: every project is for a platform this version does not generate (UWP, Windows Phone, Android, iOS or a console).");
        }

        foreach (var isolation in new BuildIsolationGenerator().Generate())
        {
            state.Add(isolation, []);
        }

        var needsTools = migrated.Any(w => w.SdkStyle ? HasContentBuilder(w) : (w.Read.MgcbFiles?.Count ?? 0) > 0);
        if (needsTools)
        {
            AddToolManifest(migrated, root, versions, state);
        }

        var engine = new SourceRewriteEngine(_rewriters);
        var files = migrated.SelectMany(w => SourceFiles(w, root, state, cancellationToken)).ToList();
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new MigrationProgress(_stageSource, i, files.Count, files[i].RelativePath));
            await RewriteFileAsync(engine, files[i], state, cancellationToken).ConfigureAwait(false);
            progress?.Report(new MigrationProgress(_stageSource, i + 1, files.Count, files[i].RelativePath));
        }

        for (var i = 0; i < migrated.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new MigrationProgress(_stageContent, i, migrated.Count, migrated[i].Discovered.RelativePath));
            CopyProjectFiles(migrated[i], root, options.ExcludedPlanItemIds, state, cancellationToken);
            progress?.Report(new MigrationProgress(_stageContent, i + 1, migrated.Count, migrated[i].Discovered.RelativePath));
        }

        AddShimLibrary(migrated, state);

        var isSolutionInput = string.Equals(Path.GetExtension(sourcePath), ".sln", StringComparison.OrdinalIgnoreCase);
        if (migrated.Count >= 2 || isSolutionInput)
        {
            state.Add(
                new SolutionGenerator().Generate(
                    ProjectMigrator.SolutionName(sourcePath, root, isSolutionInput),
                    migrated.Select(w => w.Read.Info.Path).Concat(state.ShimUses.Count == 0 ? [] : [ShimProjectGenerator.ProjectPath]).ToList()),
                []);
        }

        AddManualNotes(report, skippedProjects, state);

        cancellationToken.ThrowIfCancellationRequested();
        var changes = state.Changes.OrderBy(c => c.RelativePath, StringComparer.Ordinal).ToList();
        var notes = state.Notes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var version = ProjectMigrator.MigratorVersion();
        var finalPlan = new MigrationPlan(
            plan.Items.Where(p => !IsInSkippedProject(p, skippedProjects)).ToList(),
            plan.ExcludedItems.Where(p => !IsInSkippedProject(p, skippedProjects)).ToList());

        if (options.DryRun)
        {
            var dry = ManifestBuilder.Build(report.CatalogName, completed: false, changes, state.RuleIdsByPath, null, version, shims: state.ShimManifest(changes));
            progress?.Report(new MigrationProgress(_stageDone, 1, 1, null));
            return new MigrationResult(finalPlan, changes, dry, Completed: false);
        }

        using var session = guard.Begin(root, options.OutputFolder, confirmedNonEmpty);
        try
        {
            progress?.Report(new MigrationProgress(_stageWrite, 0, 1, null));
            await new OutputWriter().WriteAsync(session, root, changes, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var manifest = ManifestBuilder.Build(report.CatalogName, completed: true, changes, state.RuleIdsByPath, null, version, shims: state.ShimManifest(changes));
            await ProjectMigrator.WriteManifestFilesAsync(session.StagingFolder, manifest, notes, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            session.Commit();
            progress?.Report(new MigrationProgress(_stageWrite, 1, 1, null));
            progress?.Report(new MigrationProgress(_stageDone, 1, 1, null));
            return new MigrationResult(finalPlan, changes, manifest, Completed: true);
        }
        catch
        {
            await TryWriteIncompleteManifestAsync(session, report, changes, state, notes, version).ConfigureAwait(false);
            throw;
        }
    }

    // Adds XnaReborn.Compat (GamerServices only) when a migrated source file uses it, and references it from the projects that do.
    // shortcut: no Microsoft.Xna.Framework.Net stand-ins, the XNA Net API (sessions, lobbies) does not fit the offline pattern; MG028 keeps its marker. Upgrade if a real game needs a local-only session.
    private void AddShimLibrary(List<Work> migrated, State state)
    {
        if (state.ShimUses.Count == 0)
        {
            return;
        }

        var users = state.ShimUses.Select(u => u.Project).Distinct().ToList();
        var target = users.Select(w => w.Target).Distinct().Count() == 1 ? users[0].Target : MonoGameTarget.DesktopGL;
        if (migrated.Select(w => w.Target).Distinct().Count() > 1)
        {
            state.Notes.Add($"{ShimProjectGenerator.ProjectPath}: the migrated projects use different MonoGame platforms, so the shim library uses {target}. A project on the other platform may need its own copy.");
        }

        foreach (var shim in new ShimProjectGenerator(new PackageSelector()).Generate(target))
        {
            state.Add(shim, []);
        }

        foreach (var work in users)
        {
            var path = work.Read.Info.Path;
            var index = state.Changes.FindIndex(c => string.Equals(c.RelativePath, path, StringComparison.Ordinal));
            if (index < 0)
            {
                continue;
            }

            var change = state.Changes[index];
            if (change.NewText is null)
            {
                // An unchanged SDK-style project was only copied. It needs real text to get the reference.
                var original = _fileSystem.ReadAllText(work.Discovered.FullPath);
                change = new FileChange(path, ChangeKind.Modified, original, original, null);
            }

            state.Changes[index] = ShimProjectGenerator.AddToProject(change, ProjectDirectory(work), reference: true);
            state.ShimReferencedBy.Add(path.Replace('\\', '/'));
        }
    }

    private static bool IsInSkippedProject(PlanItem item, List<string> skipped) =>
        skipped.Any(p => item.Id.EndsWith(":" + p, StringComparison.Ordinal));

    private static async Task TryWriteIncompleteManifestAsync(
        OutputSession session,
        AnalysisReport report,
        IReadOnlyList<FileChange> changes,
        State state,
        IReadOnlyList<string> notes,
        string version)
    {
        try
        {
            if (!session.Committed && Directory.Exists(session.StagingFolder))
            {
                var manifest = ManifestBuilder.Build(report.CatalogName, completed: false, changes, state.RuleIdsByPath, null, version);
                await ProjectMigrator.WriteManifestFilesAsync(session.StagingFolder, manifest, notes, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort. The caller rethrows the original exception, and INCOMPLETE.txt already marks the folder.
        }
    }

    private static bool IsSdkStyle(string projectText)
    {
        try
        {
            return XDocument.Parse(projectText).Root?.Attribute("Sdk") is not null;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    // Manual and Unsupported findings become one note per rule and project, so the manifest tells a person what is left.
    private static void AddManualNotes(AnalysisReport report, List<string> skippedProjects, State state)
    {
        var rules = report.TriggeredRules.GroupBy(r => r.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var group in report.Findings
            .Where(f => f.Classification != Classification.AutoFixable && !skippedProjects.Contains(f.Project, StringComparer.Ordinal))
            .GroupBy(f => (f.RuleId, f.Project)))
        {
            var places = group.Count() > 1 ? $" ({group.Count()} places)" : string.Empty;
            var resolution = rules.TryGetValue(group.Key.RuleId, out var rule) ? " " + rule.Resolution : string.Empty;
            state.Notes.Add($"{group.Key.Project}: {group.Key.RuleId}: {group.First().Title}{places}: needs manual work.{resolution}");
        }
    }

    private void AddToolManifest(List<Work> migrated, string root, PackageVersions versions, State state)
    {
        // An existing manifest keeps its other tools: only the dotnet-mgcb version is changed.
        const string relative = ".config/dotnet-tools.json";
        var full = Path.Combine(root, ".config", "dotnet-tools.json");
        if (_fileSystem.FileExists(full))
        {
            var text = _fileSystem.ReadAllText(full);
            var bumped = SdkProjectBumper.BumpToolManifest(text, versions.DotnetMgcb);
            if (bumped.Contains("\"dotnet-mgcb\"", StringComparison.Ordinal))
            {
                state.Add(
                    string.Equals(bumped, text, StringComparison.Ordinal)
                        ? new FileChange(relative, ChangeKind.Copied, null, null, null, full)
                        : new FileChange(relative, ChangeKind.Modified, text, bumped, "dotnet-mgcb moved to the pinned MonoGame version."),
                    []);
                return;
            }

            state.Notes.Add($"{relative}: the tool manifest has no dotnet-mgcb entry, so it was replaced by one that has; other tools in it were not carried over.");
        }

        state.Add(new ToolManifestGenerator().Generate(versions.DotnetMgcb), []);
    }

    private bool HasContentBuilder(Work work)
    {
        var text = _fileSystem.ReadAllText(work.Discovered.FullPath);
        return text.Contains("MonoGame.Content.Builder.Task", StringComparison.OrdinalIgnoreCase);
    }

    private static string ProjectDirectory(Work work)
    {
        var path = work.Read.Info.Path.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    // A root-relative '/' path as the project file would write it: relative to the project folder.
    private static string ProjectRelative(Work work, string rootRelative)
    {
        var directory = ProjectDirectory(work);
        var from = directory.Length == 0 ? "." : directory;
        return Path.GetRelativePath(from, rootRelative).Replace('/', '\\');
    }

    private IEnumerable<SourceFile> SourceFiles(Work work, string root, State state, CancellationToken cancellationToken)
    {
        var projectDirectory = Path.GetDirectoryName(work.Discovered.FullPath) ?? root;
        IEnumerable<string> fulls;
        if (work.SdkStyle)
        {
            // An SDK-style project compiles every .cs file below its folder, so every file is looked at.
            fulls = ContentFileCollector.Collect(_fileSystem, projectDirectory, cancellationToken)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            fulls = work.Read.Info.CompileFiles
                .Where(c => c.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Select(c => Path.GetFullPath(Path.Combine(projectDirectory, ProjectMigrator.ToNative(c))));
        }

        foreach (var full in fulls)
        {
            var relative = ProjectMigrator.Relative(root, full);
            if (ProjectMigrator.IsOutside(relative))
            {
                state.Notes.Add($"{work.Read.Info.Path}: {relative}: this source file is outside the source folder and was not copied.");
                continue;
            }

            if (state.Claim(relative))
            {
                yield return new SourceFile(work, relative, full);
            }
        }
    }

    private async Task RewriteFileAsync(SourceRewriteEngine engine, SourceFile file, State state, CancellationToken cancellationToken)
    {
        if (!_fileSystem.FileExists(file.FullPath))
        {
            state.Changes.Add(new FileChange(
                file.RelativePath,
                ChangeKind.Skipped,
                null,
                null,
                $"The source file '{file.RelativePath}' is listed in {file.Work.Read.Info.Path} but does not exist."));
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

        if (state.ShimsEnabled)
        {
            // Only GamerServices: MonoGame 3.8 has its own Storage, so the Storage stand-ins would clash.
            foreach (var use in ShimProjectGenerator.Scan(text).Where(u => u.Namespace == ShimProjectGenerator.GamerServicesNamespace))
            {
                state.ShimUses.Add((file.Work, new ManifestShimUse(file.RelativePath, use.Namespace, use.Types)));
            }
        }

        var result = engine.Rewrite(file.RelativePath, text, file.Work.Read.Info.DefineConstants, file.Work.Enabled);
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

    // Everything besides the .cs files and the project file: icon, copy-to-output items, kept references, content, and for an SDK-style project the rest of its folder.
    private void CopyProjectFiles(Work work, string root, IReadOnlySet<string> excluded, State state, CancellationToken cancellationToken)
    {
        var projectPath = work.Read.Info.Path;
        var projectDirectory = Path.GetDirectoryName(work.Discovered.FullPath) ?? root;

        if (work.SdkStyle)
        {
            foreach (var full in ContentFileCollector.Collect(_fileSystem, projectDirectory, cancellationToken))
            {
                var relative = ProjectMigrator.Relative(root, full);
                if (!full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && state.Claim(relative))
                {
                    state.Changes.Add(new FileChange(relative, ChangeKind.Copied, null, null, null, full));
                }
            }

            return;
        }

        CopyIfExists(work, root, state, work.Read.Properties.TryGetValue("ApplicationIcon", out var icon) ? icon : null, ChangeKind.Copied, null);

        foreach (var item in SdkCsprojGenerator.CopiedItems(work.Read))
        {
            var include = item.Include.Replace('\\', '/');
            if (item.Problem == CopiedItemProblem.OutsideRoot)
            {
                state.Notes.Add($"{projectPath}: {include}: this item is copied to the output folder but lies outside the source folder, so it was not copied.");
            }
            else if (item.Problem == CopiedItemProblem.Wildcard)
            {
                state.Notes.Add($"{projectPath}: {include}: wildcard items that are copied to the output folder are not expanded; add the files by hand.");
            }
            else if (item.RootRelativePath.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
            {
                CopyIfExists(work, root, state, item.Include, ChangeKind.ManualWork, "Prebuilt .xnb content cannot be rebuilt for MonoGame and may not load. It is copied to the output folder as is.");
            }
            else
            {
                CopyIfExists(work, root, state, item.Include, ChangeKind.Copied, null);
            }
        }

        foreach (var hint in work.KeptHintPaths)
        {
            CopyIfExists(work, root, state, hint, ChangeKind.Copied, null);
        }

        // Prebuilt .xnb files cannot be rebuilt. They are copied and listed even when nothing asks for them to be copied to the output.
        foreach (var item in work.Read.Items.Where(i => i.ItemType is "Content" or "None" && i.Include.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase) && !i.Include.Contains('*', StringComparison.Ordinal)))
        {
            CopyIfExists(work, root, state, item.Include, ChangeKind.ManualWork, "Prebuilt .xnb content cannot be rebuilt for MonoGame and may not load. It is copied as is.");
        }

        foreach (var mgcb in work.Read.MgcbFiles ?? [])
        {
            CopyContent(work, mgcb, root, excluded, state);
        }
    }

    // Upgrades one Content.mgcb, copies the files its entries name, and lists what needs a person.
    private void CopyContent(Work work, ParsedMgcb mgcb, string root, IReadOnlySet<string> excluded, State state)
    {
        var mgcbFull = Path.GetFullPath(Path.Combine(root, ProjectMigrator.ToNative(mgcb.Path)));
        var mgcbDirectory = Path.GetDirectoryName(mgcbFull) ?? root;
        var kept = new List<string>();
        if (state.Claim(mgcb.Path))
        {
            var (text, hasBom) = ReadTextWithBom(mgcbFull);
            var platform = work.Target == MonoGameTarget.WindowsDX ? "Windows" : "DesktopGL";
            var removeInstall = !excluded.Contains("MG023:" + work.Discovered.RelativePath);
            var upgrade = MgcbUpgrader.Upgrade(text, platform, removeInstall);
            kept.AddRange(upgrade.KeptReferences);
            if (upgrade.Changed)
            {
                var ids = new List<string>();
                if (upgrade.RemovedReferences.Count > 0)
                {
                    ids.Add("MG023");
                }

                if (work.Enabled.Contains("MG024") && !string.Equals(upgrade.OldPlatform, platform, StringComparison.Ordinal))
                {
                    ids.Add("MG024");
                }

                state.Add(
                    new FileChange(mgcb.Path, ChangeKind.Modified, (hasBom ? "\uFEFF" : string.Empty) + text, upgrade.Text, $"Platform set to {platform}; installer-folder references removed: {upgrade.RemovedReferences.Count}."),
                    ids,
                    alreadyClaimed: true);
            }
            else
            {
                state.Changes.Add(new FileChange(mgcb.Path, ChangeKind.Copied, null, null, null, mgcbFull));
            }
        }

        foreach (var entry in mgcb.File.Entries)
        {
            var full = Path.GetFullPath(Path.Combine(mgcbDirectory, ProjectMigrator.ToNative(entry.Source)));
            var relative = ProjectMigrator.Relative(root, full);
            if (ProjectMigrator.IsOutside(relative) || !_fileSystem.FileExists(full))
            {
                state.Notes.Add($"{mgcb.Path}: {entry.Source}: the content file was not found inside the source folder, so it was not copied.");
            }
            else if (state.Claim(relative))
            {
                state.Changes.Add(new FileChange(relative, ChangeKind.Copied, null, null, null, full));
            }
        }

        foreach (var reference in kept)
        {
            var full = Path.GetFullPath(Path.Combine(mgcbDirectory, ProjectMigrator.ToNative(reference)));
            var relative = ProjectMigrator.Relative(root, full);
            if (ProjectMigrator.IsOutside(relative) || !_fileSystem.FileExists(full))
            {
                state.Notes.Add($"{mgcb.Path}: MG025: the pipeline assembly '{reference.Replace('\\', '/')}' was not found inside the source folder, so it was not copied. Build it for MonoGame 3.8.6.1 and add it by hand.");
            }
            else if (state.Claim(relative))
            {
                const string reason = "Custom content pipeline assembly built for an older MonoGame or XNA. Rebuild it against MonoGame 3.8.6.1; the copy is the old binary.";
                state.Changes.Add(new FileChange(relative, ChangeKind.ManualWork, null, null, reason, full));
            }
        }

        var custom = mgcb.File.Entries.Where(e => ImporterMap.Default.IsCustom(e.Importer, e.Processor)).ToList();
        if (custom.Count > 0)
        {
            var pairs = string.Join(", ", custom.Select(e => $"{e.Importer ?? "-"}/{e.Processor ?? "-"}").Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Take(5));
            state.Notes.Add($"{mgcb.Path}: MG026: {custom.Count} entr{(custom.Count == 1 ? "y uses" : "ies use")} a custom importer or processor ({pairs}). They are kept in Content.mgcb and build only with a pipeline assembly for MonoGame 3.8.6.1.");
        }
    }

    private (string Text, bool HasBom) ReadTextWithBom(string full)
    {
        using var stream = _fileSystem.OpenRead(full);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return (new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), hasBom);
    }
    private void CopyIfExists(Work work, string root, State state, string? projectRelative, ChangeKind kind, string? reason)
    {
        if (string.IsNullOrWhiteSpace(projectRelative))
        {
            return;
        }

        var projectDirectory = Path.GetDirectoryName(work.Discovered.FullPath) ?? root;
        var full = Path.GetFullPath(Path.Combine(projectDirectory, ProjectMigrator.ToNative(projectRelative.Trim())));
        var relative = ProjectMigrator.Relative(root, full);
        if (ProjectMigrator.IsOutside(relative) || !_fileSystem.FileExists(full))
        {
            state.Notes.Add($"{work.Read.Info.Path}: {projectRelative.Replace('\\', '/')}: the file was not found inside the source folder, so it was not copied.");
            return;
        }

        if (state.Claim(relative))
        {
            state.Changes.Add(new FileChange(relative, kind, null, null, reason, full));
        }
    }

    private HashSet<string> MissingCopiedItems(Work work, string root)
    {
        var projectDirectory = Path.GetDirectoryName(work.Discovered.FullPath) ?? root;
        return SdkCsprojGenerator.CopiedItems(work.Read)
            .Where(i => i.Problem == CopiedItemProblem.None
                && !_fileSystem.FileExists(Path.GetFullPath(Path.Combine(projectDirectory, ProjectMigrator.ToNative(i.Include)))))
            .Select(i => i.Include)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // A platform project is not migrated. Its files are copied unchanged and listed as manual work with the reason.
    private void CopyPlatformProject(DiscoveredProject discovered, ProjectReadResult read, string root, State state, CancellationToken cancellationToken)
    {
        var reason = $"Platform project ({MigrationScope.Describe(read.Info.MonoGamePlatform)}): this version does not migrate it, so the files are copied unchanged.";
        var projectDirectory = Path.GetDirectoryName(discovered.FullPath) ?? root;
        IEnumerable<string> fulls;
        if (string.Equals(ProjectMigrator.Relative(root, projectDirectory), ".", StringComparison.Ordinal))
        {
            fulls = new[] { discovered.FullPath }.Concat(read.Info.CompileFiles.Select(c => Path.GetFullPath(Path.Combine(projectDirectory, ProjectMigrator.ToNative(c)))));
        }
        else
        {
            fulls = ContentFileCollector.Collect(_fileSystem, projectDirectory, cancellationToken);
        }

        foreach (var full in fulls)
        {
            var relative = ProjectMigrator.Relative(root, full);
            if (!ProjectMigrator.IsOutside(relative) && _fileSystem.FileExists(full) && state.Claim(relative))
            {
                state.Changes.Add(new FileChange(relative, ChangeKind.ManualWork, null, null, reason, full));
            }
        }

        state.Notes.Add($"{discovered.RelativePath}: {MigrationScope.Describe(read.Info.MonoGamePlatform)} project: not migrated, copied unchanged. See manual work.");
    }

    private sealed class Work(DiscoveredProject discovered, ProjectReadResult read, IReadOnlySet<string> enabled, bool sdkStyle)
    {
        public DiscoveredProject Discovered { get; } = discovered;

        public ProjectReadResult Read { get; } = read;

        public IReadOnlySet<string> Enabled { get; } = enabled;

        public bool SdkStyle { get; } = sdkStyle;

        public MonoGameTarget Target { get; set; }

        public IReadOnlyList<string> KeptHintPaths { get; set; } = [];
    }

    private sealed record SourceFile(Work Work, string RelativePath, string FullPath);

    private sealed class State
    {
        private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public List<FileChange> Changes { get; } = [];

        public List<string> Notes { get; } = [];

        public bool ShimsEnabled { get; init; }

        public List<(Work Project, ManifestShimUse Use)> ShimUses { get; } = [];

        public SortedSet<string> ShimReferencedBy { get; } = new(StringComparer.Ordinal);

        public ManifestShims? ShimManifest(IReadOnlyList<FileChange> changes) =>
            ShimUses.Count == 0
                ? null
                : new ManifestShims(
                    ShimProjectGenerator.ProjectPath,
                    changes.Where(c => c.RelativePath.StartsWith(ShimProjectGenerator.ProjectName + "/", StringComparison.Ordinal)).Select(c => c.RelativePath).Order(StringComparer.Ordinal).ToList(),
                    ShimReferencedBy.ToList(),
                    ShimUses.Select(u => u.Use).OrderBy(u => u.File, StringComparer.Ordinal).ThenBy(u => u.Namespace, StringComparer.Ordinal).ToList());

        public Dictionary<string, IReadOnlyList<string>> RuleIdsByPath { get; } = new(StringComparer.Ordinal);

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
