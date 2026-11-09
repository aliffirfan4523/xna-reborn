# Tasks: Content Conversion

- [ ] 1. Fixtures
  - [ ] 1.1 Add content fixtures: standard (texture, font, wav, mp3), custom processor, XACT, name overrides, nested content project, xnb-only
  - [ ] 1.2 Confirm licenses and add attribution
  - _Requirements: R1 to R8_

- [ ] 2. Reading
  - [ ] 2.1 Extend `ContentProjectReader` for nested projects, root directory, profile, and pipeline references
  - [ ] 2.2 Reader tests
  - _Requirements: R1_

- [ ] 3. Asset names
  - [ ] 3.1 Implement `AssetNameResolver`
  - [ ] 3.2 Conflict detection and tests
  - _Requirements: R2_

- [ ] 4. Mapping
  - [ ] 4.1 Define `rules/importer-map.json` (shared with the analyzer, see `project-analyzer` task 11.2) and its loader in Core
  - [ ] 4.2 Implement `ImporterMap` and `ProcessorParamMapper`
  - [ ] 4.3 Classify items: Converted, ManualWork, Missing, Conflict, Prebuilt
  - [ ] 4.4 Mapping tests
  - _Requirements: R3, R7_

- [ ] 5. MGCB writer
  - [ ] 5.1 Implement `MgcbWriter` with global properties and platform/profile mapping
  - [ ] 5.2 Support `source;destination` for name overrides
  - [ ] 5.3 Verify the format against the installed MonoGame content builder version
  - [ ] 5.4 Snapshot and determinism tests
  - _Requirements: R3, R10_

- [ ] 6. Asset copying
  - [ ] 6.1 Implement `AssetCopier` preserving relative paths
  - [ ] 6.2 Report missing files
  - _Requirements: R4_

- [ ] 7. Checkpoint: content builds
  - Convert the demo project's content and run the content build successfully
  - _Requirements: R3, R4, R10_

- [ ] 8. Fonts
  - [ ] 8.1 Create `xna-only-fonts.json`, confirming its list
  - [ ] 8.2 Implement `FontChecker` and finding `XNA013`
  - [ ] 8.3 Implement optional font substitution on the migrated copy only
  - [ ] 8.4 Tests
  - _Requirements: R5_

- [ ] 9. Audio, effects, and prebuilt
  - [ ] 9.1 Flag XACT files as manual work and copy unchanged
  - [ ] 9.2 Include `.fx` files with a shader-review flag
  - [ ] 9.3 Detect and report prebuilt-only `.xnb` files
  - _Requirements: R6, R7, R8_

- [ ] 10. Report and integration
  - [ ] 10.1 Implement `ContentReport` and counts by type
  - [ ] 10.2 Add the report to the migration manifest and expose it to the UI
  - [ ] 10.3 Attach content build errors with asset paths through the runner
  - _Requirements: R9, R10_

- [ ] 11. Final checkpoint
  - Full test suite, `dotnet format --verify-no-changes`, review of generated `.mgcb` files for all fixtures
