# Tasks: Content Conversion

- [ ] 1. Fixtures
  - [ ] 1.1 Add content fixtures: standard (texture, font, wav, mp3), custom processor, XACT, name overrides, nested content project, xnb-only. Done: texture, font, custom processor, XACT, `.fx`, name override and nested content project (`xna40-basic`, `xna40-custom-content`, `multi-project-sln`). Open: wav and mp3 items, and an xnb-only project. Integration tests write real PNG and WAV bytes at test time so the repo stays text-only.
  - [x] 1.2 Confirm licenses and add attribution (all fixtures are original and synthetic)
  - _Requirements: R1 to R8_

- [x] 2. Reading
  - [x] 2.1 Extend `ContentProjectReader` for nested projects, root directory, profile, and pipeline references
  - [x] 2.2 Reader tests
  - _Requirements: R1_

- [x] 3. Asset names
  - [x] 3.1 Implement `AssetNameResolver`
  - [x] 3.2 Conflict detection and tests
  - _Requirements: R2_

- [x] 4. Mapping
  - [x] 4.1 Define `rules/importer-map.json` (shared with the analyzer, see `project-analyzer` task 11.2) and its loader in Core
  - [x] 4.2 Implement `ImporterMap` and `ProcessorParamMapper` (`FontDescriptionProcessor.DefaultCharacter` and `EffectProcessor.DefaultEffect` are dropped: MonoGame's processors do not have them)
  - [x] 4.3 Classify items: Converted, ManualWork, Missing, Conflict, Prebuilt (`AssetClassifier`)
  - [x] 4.4 Mapping tests
  - Open points for task 5: `ConvertedAsset` has no output path, so `MgcbWriter` derives the `source;destination` line from `AssetName`; when no processor is set, `.mp3` and `.wma` are inferred as `SongProcessor` only
  - _Requirements: R3, R7_

- [x] 5. MGCB writer
  - [x] 5.1 Implement `MgcbWriter` with global properties and platform/profile mapping (WindowsDX is written as `/platform:Windows`)
  - [x] 5.2 Support `source;destination` for name overrides
  - [x] 5.3 Verify the format against the installed MonoGame content builder version (dotnet-mgcb 3.8.6.1; facts in `MgcbWriter` remarks; `FindProblems` refuses what the tool would misread)
  - [x] 5.4 Snapshot and determinism tests
  - _Requirements: R3, R10_

- [x] 6. Asset copying
  - [x] 6.1 Implement `AssetCopier` preserving relative paths
  - [x] 6.2 Report missing files
  - _Requirements: R4_

- [x] 7. Checkpoint: content builds
  - Convert the demo project's content and run the content build successfully: `xna40-basic` with a real PNG (integration test), and Pirate-Treasure-Hunter by hand with font substitution on (all 21 assets built, 0 warnings, 0 errors)
  - _Requirements: R3, R4, R10_

- [x] 8. Fonts
  - [x] 8.1 Create `xna-only-fonts.json`, confirming its list (14 families of the XNA Redistributable Font Pack; rule XNA013 uses the same list)
  - [x] 8.2 Implement `FontChecker` and finding `XNA013`
  - [x] 8.3 Implement optional font substitution on the migrated copy only (`MigrationOptions.SubstituteFonts`; without it an XNA-only font is left out of `Content.mgcb` as manual work so the rest still builds)
  - [x] 8.4 Tests
  - _Requirements: R5_

- [x] 9. Audio, effects, and prebuilt
  - [x] 9.1 Flag XACT files as manual work and copy unchanged
  - [x] 9.2 Include `.fx` files with a shader-review flag
  - [x] 9.3 Detect and report prebuilt-only `.xnb` files (copied with `/copy:`)
  - _Requirements: R6, R7, R8_

- [ ] 10. Report and integration
  - [x] 10.1 Implement `ContentReport` and counts by type
  - [x] 10.2 Add the report to the migration manifest and expose it to the UI
  - [ ] 10.3 Attach content build errors with asset paths through the runner
  - _Requirements: R9, R10_

- [ ] 11. Final checkpoint
  - Full test suite, `dotnet format --verify-no-changes`, review of generated `.mgcb` files for all fixtures
