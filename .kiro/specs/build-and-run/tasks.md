# Tasks: Build and Run

- [x] 1. Setup
  - [x] 1.1 Create `XnaReborn.Runner` project (references Core only, so the migrator can reference the Runner later without a cycle), plus `tests/XnaReborn.TestGame`, a tiny stand-in game for launcher tests
  - [x] 1.2 Define `EnvironmentInfo`, `Diagnostic` (named `BuildDiagnostic` to avoid a clash with the analyzer's `Diagnostic`), `FixHint`, `DiagnosticGroup`, `BuildResult`, `RunSession`, plus `IProcessRunner` with `ProcessRequest`, `ProcessResult` and `OutputLine`
  - _Requirements: R8_

- [x] 2. Safety guard
  - [x] 2.1 Implement `MigratedCopyGuard` (manifest marker check)
  - [x] 2.2 Tests: migrated copy accepted, original XNA folder refused
  - _Requirements: R2, R7_

- [x] 3. Environment detection
  - [x] 3.1 Implement `DotnetLocator` and version comparison
  - [x] 3.2 Report OS and target support
  - [x] 3.3 Tests with a fake `dotnet`
  - _Requirements: R1_

- [x] 4. Process runner
  - [x] 4.1 Implement `ProcessRunner` with streaming output via `Channel<string>`
  - [x] 4.2 Implement timeout and process-tree cancellation
  - [x] 4.3 Tests: success, failure, timeout, cancel
  - _Requirements: R2_

- [x] 5. Build and parse
  - [x] 5.1 Implement `BuildRunner`
  - [x] 5.2 Implement `MsBuildOutputParser` and `MgcbOutputParser`
  - [x] 5.3 Implement `DiagnosticGrouper` with de-duplication
  - [x] 5.4 Collect real output samples and add snapshot tests
  - _Requirements: R2, R3_

- [x] 6. Hint catalog
  - [x] 6.1 Define `hints.json` schema and `HintCatalog` loader
  - [x] 6.2 Implement `HintMatcher` with matching order
  - [x] 6.3 Write the first 10 hints linked to analyzer rules
  - [x] 6.4 Matching tests, including "no known fix"
  - _Requirements: R4_

- [x] 7. Checkpoint: build loop
  - Build the migrated simplest fixture end to end and show grouped diagnostics for a deliberately broken one (`ProjectRunnerIntegrationTests`: `xna31-rewritable` builds; `xna31-legacy` fails with CS0246 `ResolveTexture2D` grouped under hint H011 / rule XNA027)
  - _Requirements: R2, R3, R4_

- [x] 8. Launching
  - [x] 8.1 Implement `GameLauncher` with output capture and working directory
  - [x] 8.2 Implement early-crash detection and stop control
  - [x] 8.3 Implement the first-launch confirmation callback
  - [x] 8.4 Tests with a tiny test executable
  - _Requirements: R5, R7_

- [x] 9. Runtime failure detection
  - [x] 9.1 Implement `RuntimeFailureDetector`
  - [x] 9.2 Add runtime hints to the catalog
  - [x] 9.3 Tests with sample outputs
  - _Requirements: R6_

- [x] 10. Pipeline and integration
  - [x] 10.1 Implement `ProjectRunner.BuildAndRunAsync` with progress events
  - [x] 10.2 Write results into the migration manifest
  - [x] 10.3 Slow integration test with a real build
  - _Requirements: R8_

- [ ] 11. Final checkpoint
  - [x] Demo rehearsal checklist written (`docs/demo-rehearsal.md`); the recording itself is not done
  - Full test suite, `dotnet format --verify-no-changes`, demo rehearsal: failing project to running game (the launch test needs `XNAREBORN_INTEGRATION=1` and `XNAREBORN_LAUNCH=1` and opens a game window, so it runs only when the owner asks)

- [x] 12. One-file publish
  - [x] 12.1 `ProjectPublisher`: `dotnet publish -c Release -r <rid> --self-contained true -p:PublishSingleFile=true` into `<migrated copy>-publish/<rid>`, next to the copy and never inside the original
  - [x] 12.2 Guards: completed migrated copy only, exact RID match (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`), non-Windows RIDs need a DesktopGL copy, an output folder with content waits for confirmation (safety rule 3)
  - [x] 12.3 Output lines streamed, cancel and 15 minute timeout stop the process tree, a folder the run created is removed when the publish fails or is cancelled
  - [x] 12.4 Unit tests with a fake process runner; integration tests (`XNAREBORN_INTEGRATION=1`) publish a migrated fixture for `win-x64`, `linux-x64`, `osx-x64` and `osx-arm64` and check the files exist
  - Shortcut: native libraries stay next to the program (not self-extracted), because MonoGame looks for them there
  - Not checked from Windows: starting the Linux or macOS program, a macOS `.app` bundle, signing; the Windows single file was not launched either
  - _Requirements: R2, R7_
