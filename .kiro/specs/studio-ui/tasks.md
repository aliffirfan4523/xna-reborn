# Tasks: Studio UI

- [x] 1. App shell
  - [x] 1.1 Create Avalonia app with Fluent theme and DI container
  - [x] 1.2 Implement `MainViewModel`, workflow state, and stepper
  - [x] 1.3 Implement `IDialogService` and `RecentProjectsStore`
  - _Requirements: R1, R6, R8_

- [x] 2. Open screen
  - [x] 2.1 Browse dialog and drag and drop
  - [x] 2.2 Recent projects list
  - [x] 2.3 Friendly error for "no project found"
  - _Requirements: R1_

- [x] 3. Report screen
  - [x] 3.1 `ScoreGauge` and `SeverityBadge` controls
  - [x] 3.2 Grouped findings list with detail pane and snippet
  - [x] 3.3 Filters, search, and export
  - [x] 3.4 ViewModel tests
  - _Requirements: R2_

- [x] 4. Checkpoint: analyze demo
  - Open a fixture project and show the report on screen
  - Covered by `CheckpointTests` (real analyzer and catalog, `xna31-legacy`, headless)

- [x] 5. Migration setup
  - [x] 5.1 Output folder, target selection, shims toggle
  - [x] 5.2 Plan checklist with exclusions
  - [x] 5.3 Output folder validation messages
  - _Requirements: R3_

- [x] 6. Diff view
  - [x] 6.1 `DiffViewer` control with AvaloniaEdit and DiffPlex
  - [x] 6.2 Changed files list and marker highlighting
  - [x] 6.3 Dry-run preview
  - _Requirements: R4_

- [x] 7. Build and run screen
  - [x] 7.1 Environment status and live log
  - [x] 7.2 Grouped diagnostics with hints
  - [x] 7.3 Run and Stop with first-launch confirmation
  - [x] 7.4 Startup failure display
  - _Requirements: R5_

- [x] 8. Checkpoint: minimum winning demo
  - Full flow on the demo project: open, analyze, migrate, build, run
  - Headless test on a copy of `xna31-rewritable` (`CheckpointTests`): build needs `XNAREBORN_INTEGRATION=1`, run needs `XNAREBORN_LAUNCH=1`
  - The owner also ran the full flow by hand on Pirate-Treasure-Hunter on 2026-11-11 and it works
  - _Requirements: R1 to R5_

- [x] 9. Summary and polish
  - [x] 9.1 Before and after summary view
  - [x] 9.2 Cancel buttons and progress for all long operations
  - [x] 9.3 Error banner with copy details
  - [x] 9.4 Accessibility pass (keyboard, names, non-color cues)
  - _Requirements: R6, R7, R8_

- [ ] 10. Final checkpoint
  - [x] ViewModel and headless tests pass (both test projects, integration tests included)
  - [ ] Demo rehearsal recorded (left for the owner)
