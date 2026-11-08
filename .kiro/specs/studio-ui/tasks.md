# Tasks: Studio UI

- [ ] 1. App shell
  - [ ] 1.1 Create Avalonia app with Fluent theme and DI container
  - [ ] 1.2 Implement `MainViewModel`, workflow state, and stepper
  - [ ] 1.3 Implement `IDialogService` and `RecentProjectsStore`
  - _Requirements: R1, R6, R8_

- [ ] 2. Open screen
  - [ ] 2.1 Browse dialog and drag and drop
  - [ ] 2.2 Recent projects list
  - [ ] 2.3 Friendly error for "no project found"
  - _Requirements: R1_

- [ ] 3. Report screen
  - [ ] 3.1 `ScoreGauge` and `SeverityBadge` controls
  - [ ] 3.2 Grouped findings list with detail pane and snippet
  - [ ] 3.3 Filters, search, and export
  - [ ] 3.4 ViewModel tests
  - _Requirements: R2_

- [ ] 4. Checkpoint: analyze demo
  - Open a fixture project and show the report on screen

- [ ] 5. Migration setup
  - [ ] 5.1 Output folder, target selection, shims toggle
  - [ ] 5.2 Plan checklist with exclusions
  - [ ] 5.3 Output folder validation messages
  - _Requirements: R3_

- [ ] 6. Diff view
  - [ ] 6.1 `DiffViewer` control with AvaloniaEdit and DiffPlex
  - [ ] 6.2 Changed files list and marker highlighting
  - [ ] 6.3 Dry-run preview
  - _Requirements: R4_

- [ ] 7. Build and run screen
  - [ ] 7.1 Environment status and live log
  - [ ] 7.2 Grouped diagnostics with hints
  - [ ] 7.3 Run and Stop with first-launch confirmation
  - [ ] 7.4 Startup failure display
  - _Requirements: R5_

- [ ] 8. Checkpoint: minimum winning demo
  - Full flow on the demo project: open, analyze, migrate, build, run
  - _Requirements: R1 to R5_

- [ ] 9. Summary and polish
  - [ ] 9.1 Before and after summary view
  - [ ] 9.2 Cancel buttons and progress for all long operations
  - [ ] 9.3 Error banner with copy details
  - [ ] 9.4 Accessibility pass (keyboard, names, non-color cues)
  - _Requirements: R6, R7, R8_

- [ ] 10. Final checkpoint
  - ViewModel and headless tests pass, demo rehearsal recorded
