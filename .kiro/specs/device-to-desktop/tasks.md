# Tasks: Device Projects to Desktop

Each numbered task is sized for one worker, on its own branch and PR. Every task ends with `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` passing. Fixtures are written from scratch (R9.2). Each rule has a positive and a negative fixture test. Rule IDs are provisional (see design, "Rule changes"): task 2 settles them.

Order: 1 → 2 → 3 → 4 (analyzer, can overlap after 2) → 5 → 6 → 7 and 8 (Compat library, can run side by side after 5) → 9 → 10 → 11 → 12.

- [x] 1. Fixtures
  - [x] 1.1 Add `tests/fixtures/device/wp7-xna`: XNA 4.0 Windows Phone game project (`XnaPlatform` Windows Phone, `XnaProfile` Reach, references `Microsoft.Phone` and `Microsoft.Devices.Sensors`), with a `Game1` that uses `TouchPanel` gestures, `Accelerometer` with `ReadingChanged`, `GamePad.GetState` with `Buttons.Back`, `PhoneApplicationService.Current.State`, `Guide.IsTrialMode`, a 480x800 back buffer, and `#if WINDOWS_PHONE` / `#else` blocks. Original content, one generated texture
  - [x] 1.2 Add `xbox360-copy-solution`: a Windows project plus an `Xbox 360 Copy of ...` project whose `Compile` items are links (`<Link>`), with `#if WINDOWS || XBOX` around `Main`
  - [x] 1.3 Add `zune-31`: XNA 3.1 Zune project (`XnaPlatform` Zune, `ZUNE` define) with game pad input and a `MediaLibrary` use
  - [x] 1.4 Add `wp-silverlight` (a phone project that sets `SilverlightApplication`) from the Microsoft documentation, and note in its README that the shape is unconfirmed against a real project
  - [x] 1.5 Add negatives: `windows-plain` (Windows XNA 4.0 project with `#if WINDOWS`, a user class named `GamePad` in its own namespace, and a type called `Accelerometer` without the Devices.Sensors using)
  - [x] 1.6 Fixture README with sources (Microsoft docs links from the design), and a statement that all code is original
  - _Requirements: R9, R10_

- [x] 2. Analyzer: platform detection and platform copies
  - [x] 2.1 Add `DevicePlatform` to Core and the new `ProjectInfo` fields with defaults (`Platform`, `XnaProfile`, `PlatformInferred`, `LinkedSourceProjects`); existing snapshots must not change
  - [x] 2.2 Implement `PlatformDetector` (R1.1 to R1.5) and tests for each spelling, the inferred Phone and Zune cases, and Windows/Unknown
  - [x] 2.3 Implement `ProjectLinkReader` and the `projectLinks` match type (schema, matcher, docs in the analyzer design table)
  - [x] 2.4 Edit XNA007 (Warning, AutoFixable, new resolution, `migratorTask` link to task 5.1); add XNA051 (Silverlight) and the platform copy rule; reserve final IDs by taking the first free range in the catalog and updating `design.md` and `requirements.md` in the same PR
  - [x] 2.5 Positive and negative fixture tests per rule (`wp7-xna`, `xbox360-copy-solution`, `wp-silverlight` positive; `windows-plain` negative), snapshot update with the diff reviewed
  - [x] 2.6 Read-only test: file hashes unchanged after analyzing the new fixtures
  - _Requirements: R1, R2, R3.1, R9_

- [x] 3. Analyzer: preprocessor symbols
  - [x] 3.1 Implement the `preprocessorSymbol` match type: read `#if` and `#elif` directive trivia, including inactive branches; compare whole identifiers in the condition expression
  - [x] 3.2 Add the XNA053 rule (`names`: XBOX, XBOX360, ZUNE, WINDOWS_PHONE, WINDOWS), `migratorTask` link to task 6.1
  - [x] 3.3 Tests: positive with the symbol only inside an inactive branch; negative with `WINDOWS_PHONE_7`, a symbol in a comment, and a string that looks like a directive
  - _Requirements: R4.1, R9_

- [x] 4. Analyzer: device API rules
  - [x] 4.1 Add XNA054 to XNA058 (marketplace members, marketplace types, Zune media library, phone application service, launchers) with their existing match types
  - [x] 4.2 Add XNA059 to XNA063 (choosers, accelerometer, TouchPanel, GamePad.GetState, third-party device assemblies). Confirm the `member` and `reference` match fields in the schema before writing the entries
  - [x] 4.3 Positive and negative fixture test for each rule; the negative for the type rules is "same name, no using"; the negative for XNA062 is the user's own `GamePad` class
  - [x] 4.4 Scoring check: XNA009 and XNA054 on one file count once each; XNA007 counts once per project
  - [x] 4.5 Snapshot update
  - _Requirements: R2.4, R5, R9_

- [x] 5. Migrator: device project selection
  - [x] 5.1 `MigrationPlanner`: turn an included XNA007 finding into plan items for the device conversion (project generation as DesktopGL, device library, directive folding); choose the platform symbol set (design table)
  - [x] 5.2 Skip platform copies: plan item "skip copy" per copy, chosen-project order Windows, Windows Phone, Xbox 360, Zune, user can flip; `SolutionGenerator` writes only migrated projects
  - [x] 5.3 Manifest: Skipped entries with reason and duplicated project (R3.3, R3.4)
  - [x] 5.4 Tests: Windows + copy, device-only solution, link cycle, one-way link ratio edge, excluded skip item; source hash unchanged
  - _Requirements: R2, R3, R8_

- [x] 6. Migrator: device conditional compilation
  - [x] 6.1 Implement `PlatformDirectiveRewriter`: line scanner, condition parser (`!`, `&&`, `||`, parentheses, `true`, `false`), three-valued evaluation, group folding, markers for unresolved groups
  - [x] 6.2 Add an explicit rewriter order table to `SourceRewriteEngine` (XNA053 before the entry point rewriter) and register the rewriter in `DefaultRewriters`
  - [x] 6.3 Tests: before/after for every symbol set in the design table, `#elif` and `#else`, nested groups, `DEBUG && XBOX`, a parse failure, a file with no directives is byte-identical, `Main` inside `#if WINDOWS || XBOX` ends up compiling, determinism
  - _Requirements: R4, R8.5, R9.3_

- [x] 7. Compat library: device stand-ins
  - [x] 7.1 Add `Device/Accelerometer.cs`, `TiltMapper.cs` and `DeviceControls.cs` as generated text in `ShimSources`; `ShimProjectGenerator` emits them when a device plan item is included. Pure `TiltMapper` tests: resting, each key, two keys, easing, mouse offset, golden values
  - [x] 7.2 Add `PhoneShell.cs` and `PhoneTasks.cs` (`PhoneApplicationService`, launchers that log, choosers that throw `NotSupportedException` with the rule ID)
  - [x] 7.3 Extend the `Guide` stand-in with `IsTrialMode`, `ShowMarketplace`; add marketplace task types and `LicenseInformation`
  - [x] 7.4 (Result: type NOT confirmed, so no Zune adapter and Zune accelerometer use stays a Manual case. Sources name the XNA 3.1 classes Accelerometer, AccelerometerCapabilities and AccelerometerState but give no namespace or members.) Research gate: find the XNA 3.1 Zune HD accelerometer type in the documentation archive and record its shape in this file. If found, add a Zune adapter with a test; if not, leave Zune accelerometer use as a Manual marker and note it here
  - [x] 7.5 (Checked in a net8.0 console project: GetUserStoreForApplication creates a file and FileExists is true, so no wrapper is needed) Verify `IsolatedStorageFile.GetUserStoreForApplication` in a net8.0 test project; if it fails, add a wrapper to the storage stand-in
  - [x] 7.6 Test that the generated library compiles with the same method the existing task 12 shim tests use, and that namespaces match R5.5
  - _Requirements: R5, R6.2 to R6.5, R6.11_

- [x] 8. Compat library: input mapping and rewrites
  - [x] 8.1 `Input/GamePadMapper.cs` (pure) and `CompatGamePad.cs`; `GamePadCallRewriter` for XNA062 with the guard from design; tests: pad connected passes through, keyboard map table, Escape is Back with or without a pad, player two untouched, user `GamePad` class not rewritten, `GetCapabilities` and `SetVibration` untouched
  - [x] 8.2 `Input/DeviceInput.cs` (touch switches, cursor, window, accelerometer ticks) and `DeviceInstallRewriter`; tests: install line added once to the `Game` subclass constructor, no `Game` subclass gives a marker, running twice is idempotent
  - [x] 8.3 (NOT verified in a run: running a migrated game was not allowed for this work. The touch switches are set in the DeviceInput constructor, in Initialize and in the first Update as a safeguard.) Prove the touch switches work from `Initialize` in a short run of a fixture (headless where possible, else a documented manual check); record the result here. If it fails, move the call to the first `Update`
  - _Requirements: R6, R7, R9.4_

- [x] 9. Controls file and manifest
  - [x] 9.1 Generate `DEVICE-CONTROLS.md` from `DeviceControls` constants; snapshot test; make sure a change to a constant changes the file
  - [x] 9.2 Manifest entries for the device library types and manual items (choosers, media library, third-party assemblies)
  - _Requirements: R8_

- [x] 10. End-to-end
  - [x] 10.1 Integration test: migrate `wp7-xna` to a temp folder and run `dotnet build` on it (marked integration); check the migrated `Game1` has no device symbols and no `Microsoft.Phone` reference
  - [x] 10.2 Integration test: migrate `xbox360-copy-solution`; only the Windows project is built
  - [x] 10.3 Determinism: two runs, equal output hash; original fixture hashes unchanged
  - [x] 10.4 Update `README` or docs with the device conversion section (short)
  - _Requirements: R8, R9.5_

- [ ] 11. Candidate game check (read only)
  - [ ] 11.1 For candidates 1 to 4 in `design.md`: shallow clone into a scratch folder outside the repository, run the analyzer, migrate to a scratch output, try `dotnet build`, record score before and after and the first build error in `docs/test-candidates/DEVICE-RESULTS.md`
  - [ ] 11.2 Delete the clones and outputs; do not commit any candidate file. List remaining manual items per game and file new rule tasks for anything that appears in two or more games
  - _Requirements: R10_

- [ ] 12. Checkpoint
  - [ ] 12.1 All rules have positive and negative tests; `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass on master
  - [x] 12.2 Demo: open the Phone fixture, analyze (score), migrate, build, run, show the controls file; record any gap as a new task (Ran headless as Migrated_phone_fixture_launches_and_writes_the_controls_file. Two gaps found and fixed: the Runner refused a copy with the game next to XnaReborn.Compat, and a Windows Phone game marked Library was built as a library so there was nothing to launch.)
  - [ ] 12.3 Review unverified items in the design ("Not verified") and mark each one as confirmed or still open
