# Tasks: Build and Run

- [x] 1. Setup
  - [x] 1.1 Create `XnaReborn.Runner` project (references Core only, so the migrator can reference the Runner later without a cycle), plus `tests/XnaReborn.TestGame`, a tiny stand-in game for launcher tests
  - [x] 1.2 Define `EnvironmentInfo`, `Diagnostic` (named `BuildDiagnostic` to avoid a clash with the analyzer's `Diagnostic`), `FixHint`, `DiagnosticGroup`, `BuildResult`, `RunSession`, plus `IProcessRunner` with `ProcessRequest`, `ProcessResult` and `OutputLine`
  - _Requirements: R8_

- [ ] 2. Safety guard
  - [ ] 2.1 Implement `MigratedCopyGuard` (manifest marker check)
  - [ ] 2.2 Tests: migrated copy accepted, original XNA folder refused
  - _Requirements: R2, R7_

- [ ] 3. Environment detection
  - [ ] 3.1 Implement `DotnetLocator` and version comparison
  - [ ] 3.2 Report OS and target support
  - [ ] 3.3 Tests with a fake `dotnet`
  - _Requirements: R1_

- [ ] 4. Process runner
  - [ ] 4.1 Implement `ProcessRunner` with streaming output via `Channel<string>`
  - [ ] 4.2 Implement timeout and process-tree cancellation
  - [ ] 4.3 Tests: success, failure, timeout, cancel
  - _Requirements: R2_

- [ ] 5. Build and parse
  - [ ] 5.1 Implement `BuildRunner`
  - [ ] 5.2 Implement `MsBuildOutputParser` and `MgcbOutputParser`
  - [ ] 5.3 Implement `DiagnosticGrouper` with de-duplication
  - [ ] 5.4 Collect real output samples and add snapshot tests
  - _Requirements: R2, R3_

- [ ] 6. Hint catalog
  - [ ] 6.1 Define `hints.json` schema and `HintCatalog` loader
  - [ ] 6.2 Implement `HintMatcher` with matching order
  - [ ] 6.3 Write the first 10 hints linked to analyzer rules
  - [ ] 6.4 Matching tests, including "no known fix"
  - _Requirements: R4_

- [ ] 7. Checkpoint: build loop
  - Build the migrated simplest fixture end to end and show grouped diagnostics for a deliberately broken one
  - _Requirements: R2, R3, R4_

- [ ] 8. Launching
  - [ ] 8.1 Implement `GameLauncher` with output capture and working directory
  - [ ] 8.2 Implement early-crash detection and stop control
  - [ ] 8.3 Implement the first-launch confirmation callback
  - [ ] 8.4 Tests with a tiny test executable
  - _Requirements: R5, R7_

- [ ] 9. Runtime failure detection
  - [ ] 9.1 Implement `RuntimeFailureDetector`
  - [ ] 9.2 Add runtime hints to the catalog
  - [ ] 9.3 Tests with sample outputs
  - _Requirements: R6_

- [ ] 10. Pipeline and integration
  - [ ] 10.1 Implement `ProjectRunner.BuildAndRunAsync` with progress events
  - [ ] 10.2 Write results into the migration manifest
  - [ ] 10.3 Slow integration test with a real build
  - _Requirements: R8_

- [ ] 11. Final checkpoint
  - Full test suite, `dotnet format --verify-no-changes`, demo rehearsal: failing project to running game
