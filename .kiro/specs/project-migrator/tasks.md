# Tasks: Project Migrator

- [x] 1. Setup
  - [x] 1.1 Create `XnaReborn.Migrator` project and test project references
  - [x] 1.2 Define `MigrationOptions`, `PlanItem`, `FileChange`, `MigrationResult`, `Manifest`
  - _Requirements: R2, R8_

- [x] 2. Output safety
  - [x] 2.1 Implement `OutputGuard` (inside-source check, non-empty check, temp-then-move)
  - [x] 2.2 Tests for each guard case, including failure midway
  - _Requirements: R1_

- [x] 3. Planning
  - [x] 3.1 Implement `MigrationPlanner` from analyzer findings
  - [x] 3.2 Support exclusions and dry-run (the planner is a pure function with no file access, so a dry run and the UI preview use the same call)
  - [x] 3.3 Tests
  - _Requirements: R2_

- [x] 4. Project file generation
  - [x] 4.1 Implement `SdkCsprojGenerator` and `PackageSelector` (DesktopGL and WindowsDX). The generator writes a fresh SDK-style project: it drops the XNA targets import, XNA-only properties and project type GUIDs, `NoStdLib`, x86-only settings and ClickOnce settings, and it sets `GenerateAssemblyInfo=false` when a kept `Properties/AssemblyInfo.cs` exists (rules XNA001 to XNA004, XNA045 to XNA047)
  - [x] 4.2 Implement `SolutionGenerator` (and `ToolManifestGenerator` for `.config/dotnet-tools.json`, which the content builder needs)
  - [x] 4.3 Pin package versions in one config file and confirm against current MonoGame releases (`Projects/packages.json`, 3.8.6.1 confirmed on nuget.org)
  - [x] 4.4 Snapshot tests per fixture
  - _Requirements: R3_

- [x] 5. Entry point
  - [x] 5.1 Implement `EntryPointRewriter` (unwraps a platform-only `#if` around `Main`; text in, text out)
  - [x] 5.2 Tests including already-modern entry points
  - _Requirements: R4_

- [x] 6. Source rewriting framework
  - [x] 6.1 Implement `RewriterRegistry` and the `ISourceRewriter` contract (text based; see the design), plus `SourceRewriteEngine` and the `EntryPointSourceRewriter` adapter
  - [x] 6.2 Implement `MarkerInserter` for unresolved cases
  - [x] 6.3 Implement `DiffBuilder` with DiffPlex
  - [x] 6.4 Preserve trivia test on untouched code
  - _Requirements: R5, R8_

- [x] 7. Content conversion
  - The plan of record is the `content-conversion` spec (its tasks 1 to 10). The migrator only wires it in; it does not repeat that work. The shared importer map is `rules/importer-map.json`, loaded from Core.
  - Content is converted for a project when its XNA005 or XNA049 plan item is included; when both are excluded, its files are copied unchanged as manual work. `MigrationOptions.SubstituteFonts` (off by default) swaps XNA-only fonts in the copy.
  - [x] 7.1 Call `ContentConverter` from `ProjectMigrator` and turn its output into `FileChange` records
  - [x] 7.2 Add the `ContentReport` to the manifest (`ManifestContent`: counts by status and by type)
  - [x] 7.3 Snapshot test: migrate a fixture with content and check the generated `Content.mgcb`
  - [x] 7.4 Emit `.config/dotnet-tools.json` (pinned `dotnet-mgcb`) when content is converted
  - _Requirements: R6_

- [x] 8. Manifest and writing
  - [x] 8.1 Implement materialization of `FileChange` records (`OutputWriter`)
  - [x] 8.2 Implement JSON and Markdown manifest writers
  - [x] 8.3 (`BuildIsolationGenerator`; a parent `NuGet.config` can still change package sources) Stop the migrated copy from inheriting a parent folder's `Directory.Build.props` and `Directory.Packages.props` (a copy written under a repo that uses central package management failed restore with NU1008)
  - _Requirements: R8_

- [x] 9. Checkpoint: minimum winning demo
  - Migrate the simplest fixture end to end and run `dotnet build` on the result (`MigrationIntegrationTests`, run with `XNAREBORN_INTEGRATION=1`, on `tests/fixtures/xna31-rewritable`)
  - Verify the source folder hash is unchanged
  - Also checked by hand on Pirate-Treasure-Hunter: the original fails with MSB4019, the migrated copy builds with 0 warnings and 0 errors (content not yet built, see task 7)
  - _Requirements: R1, R3, R4, R6_

- [x] 10. XNA 3.1 to 4.0 rewriters
  - [x] 10.1 `SpriteBatchBeginRewriter`
  - [x] 10.2 `RenderStateRewriter`
  - [x] 10.3 `EffectPassRewriter`
  - [x] 10.4 Before/after fixtures including ambiguous cases
  - _Requirements: R5_

- [ ] 11. Determinism and verification
  - [x] 11.1 Byte-identical output test across two runs
  - [ ] 11.2 Integrate the Runner for optional post-migration build
  - [x] 11.3 Progress events and cancellation
  - _Requirements: R9, R10_

- [ ] 12. Dead API shims (stretch)
  - [ ] 12.1 Generate `XnaReborn.Compat` with storage and profile stand-ins
  - [ ] 12.2 Rewrite references and record shim use in the manifest
  - [ ] 12.3 Fallback markers when shims are disabled
  - _Requirements: R7_

- [ ] 13. Final checkpoint
  - Full test suite, `dotnet format --verify-no-changes`, review of all manifests and diffs
