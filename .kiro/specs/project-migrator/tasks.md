# Tasks: Project Migrator

- [x] 1. Setup
  - [x] 1.1 Create `XnaReborn.Migrator` project and test project references
  - [x] 1.2 Define `MigrationOptions`, `PlanItem`, `FileChange`, `MigrationResult`, `Manifest`
  - _Requirements: R2, R8_

- [ ] 2. Output safety
  - [ ] 2.1 Implement `OutputGuard` (inside-source check, non-empty check, temp-then-move)
  - [ ] 2.2 Tests for each guard case, including failure midway
  - _Requirements: R1_

- [ ] 3. Planning
  - [ ] 3.1 Implement `MigrationPlanner` from analyzer findings
  - [ ] 3.2 Support exclusions and dry-run
  - [ ] 3.3 Tests
  - _Requirements: R2_

- [ ] 4. Project file generation
  - [ ] 4.1 Implement `SdkCsprojGenerator` and `PackageSelector` (DesktopGL and WindowsDX). The generator writes a fresh SDK-style project: it drops the XNA targets import, XNA-only properties and project type GUIDs, `NoStdLib`, x86-only settings and ClickOnce settings, and it sets `GenerateAssemblyInfo=false` when a kept `Properties/AssemblyInfo.cs` exists (rules XNA001 to XNA004, XNA045 to XNA047)
  - [ ] 4.2 Implement `SolutionGenerator`
  - [ ] 4.3 Pin package versions in one config file and confirm against current MonoGame releases
  - [ ] 4.4 Snapshot tests per fixture
  - _Requirements: R3_

- [ ] 5. Entry point
  - [ ] 5.1 Implement `EntryPointRewriter`
  - [ ] 5.2 Tests including already-modern entry points
  - _Requirements: R4_

- [ ] 6. Source rewriting framework
  - [ ] 6.1 Implement `RewriterRegistry` and the `ISourceRewriter` contract
  - [ ] 6.2 Implement `MarkerInserter` for unresolved cases
  - [ ] 6.3 Implement `DiffBuilder` with DiffPlex
  - [ ] 6.4 Preserve trivia test on untouched code
  - _Requirements: R5, R8_

- [ ] 7. Content conversion
  - [ ] 7.1 Implement `ImporterMap` and `MgcbGenerator`
  - [ ] 7.2 Implement `AssetCopier`
  - [ ] 7.3 Flag custom processors, XACT, `.fx`, and prebuilt-only `.xnb`
  - [ ] 7.4 Snapshot tests
  - _Requirements: R6_

- [ ] 8. Manifest and writing
  - [ ] 8.1 Implement materialization of `FileChange` records
  - [ ] 8.2 Implement JSON and Markdown manifest writers
  - _Requirements: R8_

- [ ] 9. Checkpoint: minimum winning demo
  - Migrate the simplest fixture end to end and run `dotnet build` on the result
  - Verify the source folder hash is unchanged
  - _Requirements: R1, R3, R4, R6_

- [ ] 10. XNA 3.1 to 4.0 rewriters
  - [ ] 10.1 `SpriteBatchBeginRewriter`
  - [ ] 10.2 `RenderStateRewriter`
  - [ ] 10.3 `EffectPassRewriter`
  - [ ] 10.4 Before/after fixtures including ambiguous cases
  - _Requirements: R5_

- [ ] 11. Determinism and verification
  - [ ] 11.1 Byte-identical output test across two runs
  - [ ] 11.2 Integrate the Runner for optional post-migration build
  - [ ] 11.3 Progress events and cancellation
  - _Requirements: R9, R10_

- [ ] 12. Dead API shims (stretch)
  - [ ] 12.1 Generate `XnaReborn.Compat` with storage and profile stand-ins
  - [ ] 12.2 Rewrite references and record shim use in the manifest
  - [ ] 12.3 Fallback markers when shims are disabled
  - _Requirements: R7_

- [ ] 13. Final checkpoint
  - Full test suite, `dotnet format --verify-no-changes`, review of all manifests and diffs
