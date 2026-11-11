# Tasks: XACT Audio and .fx Shader Conversion

Each numbered task is sized for one worker, on its own branch cut from `master` (suggested prefixes: `audio/task-N-name`, `shaders/task-N-name`). Every task ends with `dotnet build` (warnings as errors), `dotnet test` and `dotnet format --verify-no-changes` passing. Fixtures are written from scratch. Every rule has a positive and a negative fixture test. Snapshot reports are reviewed, not just accepted. Workers do not edit other specs.

Order: 1 first. Part A is tasks 2 to 10, Part B is tasks 11 to 18; the two parts only meet in tasks 1, 3, 17 and 20. Tasks 11 to 13 can start after task 1, in parallel with Part A.

- [x] 1. Fixtures and confirmations
  - [x] 1.1 Write `tests/fixtures/xna40-xact` (project, `Game1.cs`, `Content/Content.contentproj`, `Content/Sounds.xap`) as described in the design. It has two categories, three variables, one RPC on volume, a normal wave bank (3 waves, 2 pointing to one file), a streaming wave bank, a single-wave cue, a random-variation cue, a looping music cue, a multi-track sound, a cue with a DSP block and a wave with a missing file. Wave bytes are generated at test time.
  - [x] 1.2 Write `tests/fixtures/xna40-xact-compiled` (same calls, placeholder `.xgs/.xwb/.xsb`) and `tests/fixtures/xna40-effects` (the effect files listed in the design, one `.fxh` include). Each with a `README.md` stating the files are original and synthetic.
  - [x] 1.3 Add the three names to `FixturePaths.AllNames` and run the existing fixture integrity tests over them.
  - [x] 1.4 Create `rules/xact-features.json` (data): XACT feature names, units (volume in hundredths of a dB, pitch range), loop count meaning, instance behavior names, variation types, each with a `status` of `confirmed` or `expected, unverified` and a source. Confirm from the archived XACT documentation and from MonoGame's `Tests/Assets/Audio/Tests.xap` (read-only, nothing copied). Where the documentation cannot be reached, mark the item unverified; do not guess silently.
  - [x] 1.5 Tests: fixtures exist and parse as XML or text; the effect fixtures are each under 40 lines; the fixture hash test helper covers the new folders.
  - _Requirements: R17, R16.2_

- [x] 2. XACT models and reader
  - [x] 2.1 Add the `Xap*` records to `XnaReborn.Core/Models/Xact` with XML comments (design: Models).
  - [x] 2.2 Implement `XapReader` in `XnaReborn.Analyzer/Xact` using `IReadOnlyFileSystem`: generic block tree, typed mapper, unsupported list, findings with line numbers, BOM, CRLF, tabs.
  - [x] 2.3 Tests: one test per block type from `xna40-xact`; unknown key goes to the unsupported list without stopping; unbalanced braces give a finding and manual-work result; a hash test proves the fixture files are unchanged after reading. A negative test uses `xna40-basic` (no `.xap`).
  - _Requirements: R1, R16.2_

- [x] 3. `fileContent` match type
  - [x] 3.1 Extend the rule schema and loader with `fileContent` (`glob`, `regex`, optional `maxBytes`, default 1 MiB).
  - [x] 3.2 Implement the matcher in `XnaReborn.Analyzer`: read-only, text only, blank `//` and `/* */` comments for `.fx` and `.fxh`, report the first matching lines.
  - [x] 3.3 Tests: positive (a regex that matches `xna40-effects`), negative (a regex that matches only inside a comment, and a file over `maxBytes`), loader rejects a missing `regex`, hash-unchanged test. Do not add catalog rules in this task.
  - _Requirements: R15.3, R16.2_

- [ ] 4. Planner, conversion mode and compiled-bank fallback
  - [ ] 4.1 Add `XactMode` and `StreamingWavesAsSong` to `MigrationOptions` (defaults per design).
  - [ ] 4.2 Implement `XactPlanner` in `XnaReborn.Migrator/Audio`: asset names, shared files, kinds, missing waves, compression preset notes, findings.
  - [ ] 4.3 Change `ContentConverter` classification as described in the design: convert when a parsable `.xap` exists; otherwise copy and mark manual work. In `Auto` with compiled banks and no `.xap`, add the Warning with the MonoGame limits (PCM and ADPCM only, no streaming). When both exist, copy compiled files and say they are ignored.
  - [ ] 4.4 Tests for each mode and each case in R2 (positive and negative); planner tests for R3 (including the shared file and the missing file).
  - [ ] 4.5 Link catalog rule `NEW-A1` in the rule change table to this task (the catalog edit itself is task 17).
  - _Requirements: R2, R3_

- [ ] 5. Catalog generator
  - [ ] 5.1 Implement `XactCatalogWriter` (design: Catalog) with unit conversion, deterministic ordering and invariant-culture float formatting.
  - [ ] 5.2 Unsupported features: collect them from the reader list and from `xact-features.json`; write them into the catalog per cue and into the report as Warnings (`NEW-A3`) and Info (`NEW-A2`).
  - [ ] 5.3 Tests: Verify snapshot of the `xna40-xact` catalog; determinism (two runs equal); unit conversion table test (0 dB = 1.0, -1200 = 0.251...); negative test where a project without cues writes an empty catalog; a stem used twice reports a conflict.
  - [ ] 5.4 Test that the generated file compiles (compile it with Roslyn together with the shim data types from task 8; until task 8 lands, compile it against a test stub of those types).
  - _Requirements: R4, R16.6_

- [ ] 6. Content entries and audio tool check
  - [ ] 6.1 Add the wave entries to the `Content.mgcb` generation (`WavImporter` with `SoundEffectProcessor` or `SongProcessor`, `Quality=Best`, `source;destination` form). Confirm `MgcbWriter.FindProblems` accepts the pairs; if not, add an allowed-pairs list.
  - [ ] 6.2 Add the audio section to `ContentReport` and the manifest (R7.1, R7.2); expose it as structured data.
  - [ ] 6.3 Runner: detect `ffmpeg` and `ffprobe` on the path when `Content.mgcb` has audio entries; add the Warning and the `audio-no-ffmpeg` hint (R7.3). Fake `IProcessRunner` tests: found, missing, no audio entries (no warning).
  - [ ] 6.4 Slow integration test (skipped without the tools): convert `xna40-xact`, run `dotnet-mgcb`, expect the wave assets to build.
  - _Requirements: R3, R7_

- [ ] 7. Source aliasing
  - [ ] 7.1 Implement `XactAliasRewriter` (rule `XNA015`) with Roslyn, position-based edits, alias placement, qualified names, existing aliases, local type conflict note.
  - [ ] 7.2 Extend `ShimProjectGenerator.Scan` and the project reference logic with the `Microsoft.Xna.Framework.Audio` types (R6.5).
  - [ ] 7.3 Register the rewriter; do nothing in `CopyOnly` mode.
  - [ ] 7.4 Tests: positive (uses `AudioEngine` and `Cue`), negative (uses only `SoundEffect`), alias with `using Microsoft.Xna.Framework.Audio;` still compiles when both the alias and the using are present against MonoGame DesktopGL and WindowsDX packages, idempotence, snapshot, local type declaration.
  - _Requirements: R6_

- [ ] 8. Shim core
  - [ ] 8.1 Add `XactShimSources.cs` and wire it into `ShimProjectGenerator`: data types (`XactEngineData`, `XactWaveBankData`, `XactSoundBankData` and friends), `AudioEngine`, `AudioCategory`, `WaveBank`, `SoundBank`, `Cue` (play, stop, pause, resume, states), lookup by stem, error messages, `EmptyServiceProvider`, private `ContentManager`.
  - [ ] 8.2 Variations (types 0 to 4) with an injectable `Random`; sound, track, category and cue volume; pitch.
  - [ ] 8.3 Seams for tests: `IInstanceFactory` so tests run without an audio device.
  - [ ] 8.4 Tests: lookup by stem (case, folders, missing file), variation order with a seeded random, volume math, state transitions, `FileNotFoundException` text. One test loads a real built `SoundEffect` asset through the empty-service-provider `ContentManager` (skipped without the audio tools) to prove R5.10.
  - _Requirements: R5.1 to R5.6, R5.10, R5.11_

- [ ] 9. Shim advanced
  - [ ] 9.1 Loops (infinite and finite), delayed events, `Update` driven timers.
  - [ ] 9.2 Category instance limits and behaviors, `AudioCategory` operations, global and cue variables, RPC curves on volume and pitch (linear interpolation of the points).
  - [ ] 9.3 `Song` waves through `MediaPlayer` with the limits as notes; `Apply3D`.
  - [ ] 9.4 Unsupported catalog entries never throw (R5.9).
  - [ ] 9.5 Parity test by reflection against the MonoGame assembly (R5.1); members the shim cannot support throw `NotSupportedException` naming `XnaReborn.Compat`.
  - [ ] 9.6 Tests for every behavior above, positive and negative.
  - _Requirements: R5.7 to R5.9, R5.1_

- [ ] 10. Checkpoint: XACT end to end
  - Convert `xna40-xact` into a scratch output folder; the content file builds with `dotnet-mgcb`; the migrated game compiles against DesktopGL and WindowsDX; a test plays the cues through the shim with the real built assets; the report lists notes for the DSP cue, the missing wave and the streaming bank.
  - Review the catalog snapshot and the generated shim diff by eye. Decide the default for `StreamingWavesAsSong` from the results (design risk 2).
  - _Requirements: R2 to R7_

- [ ] 11. Effect scanner and include collector
  - [ ] 11.1 Implement `FxScanner` and `FxDocument` in `XnaReborn.Migrator/Effects` (tokenizer, top-level split, technique and pass reading, sampler forms, conditional depth, parameter usage).
  - [ ] 11.2 Implement `FxIncludeCollector` with cycle protection and missing-include findings.
  - [ ] 11.3 Tests per construct from `xna40-effects`: comments hide matches, `#if` depth, sampler forms listed in R10.3, several passes, missing include (positive) and a file with no techniques (negative).
  - [ ] 11.4 Add the `shortcut:` comment for the not-a-full-parser limit.
  - _Requirements: R8, R12.1, R12.3_

- [ ] 12. Profile rewriter and profile data
  - [ ] 12.1 Create `rules/effect-profiles.json` (profile mapping, escalation ladder, pass state allow list) with sources, and a loader in Core.
  - [ ] 12.2 Implement `FxProfileRewriter` (R9). Confirm the `_2_a`/`_2_b` OpenGL mapping with `mgfxc` on a small fixture; if it fails, change the data, not the code.
  - [ ] 12.3 Tests: SM2 effect, SM3 effect, template-style unconditional `#define`, mixed models (per-model macros), unknown model (marker), already portable (no change), idempotence, rest of file byte identical, snapshot.
  - [ ] 12.4 Slow test (skipped without `mgfxc`): the rewritten SM2 fixtures pass on OpenGL and on DirectX where the machine can run it.
  - _Requirements: R9_

- [ ] 13. Sampler rewriter and markers
  - [ ] 13.1 Implement `FxSamplerRewriter` (R10) and `FxMarkers` (R11) using the scanner and the pass state allow list.
  - [ ] 13.2 Tests: each rule with a positive and a negative fixture effect; a marker is not inserted twice; notes carry the right `NeedsManualWork`; plain sampler forms are untouched (negative); marker text snapshot.
  - [ ] 13.3 Slow test: the sampler-rewritten fixtures compile with `mgfxc`.
  - _Requirements: R10, R11_

- [ ] 14. Migrator integration for effects
  - [ ] 14.1 Add `RewriteEffects` to `MigrationOptions`; add `EffectRewriteStep` called by `ContentConverter` where `.fx` items are copied; copy `#include` files (R12); notes into `ContentReport` and the manifest.
  - [ ] 14.2 Pin `dotnet-mgfxc` 3.8.6.1 in `ToolManifestGenerator`, next to `dotnet-mgcb`.
  - [ ] 14.3 Tests: migrated `xna40-effects` copy has rewritten effects and included files, original folder hashes unchanged, `RewriteEffects = false` copies unchanged, the tool manifest lists both tools.
  - _Requirements: R9, R12, R16_

- [ ] 15. Runner effect check
  - [ ] 15.1 Implement `MgfxcOutputParser` with the patterns from the design; record the real outputs from the research runs as text fixtures under the runner tests (parse errors, HLSL errors, profile errors, summary lines).
  - [ ] 15.2 Implement `EffectBuildCheck` and `ProfileEscalator` (R13), using `IProcessRunner` and `MigratedCopyGuard`.
  - [ ] 15.3 Tests with a fake runner: pass, fail with hint, escalation 9_1 to 9_3 to `_4_0` with the HiDef warning, no escalation without the macro block, skip reasons (tool missing, no `.fx`), results in input order, no network access.
  - [ ] 15.4 Slow test (skipped without the tool): run on the rewritten `xna40-effects` for the current target.
  - _Requirements: R13_

- [ ] 16. Effect hints
  - [ ] 16.1 Add the hints in the design table to `hints.json`.
  - [ ] 16.2 Tests: every hint has one message that matches and one that does not; the empty-profile message matches `fx-sm-too-low`.
  - _Requirements: R14_

- [ ] 17. Catalog rule changes
  - [ ] 17.1 Apply the rule change table in `design.md` to `rules/xna-to-monogame.json`: the three edits (XNA015, XNA016, XNA017) and the new rules (`NEW-A1` to `NEW-B6`), taking the next free `XNAnnn` IDs at the time of the task. Replace the `NEW-` labels in markers, rewriters and tests with the real IDs.
  - [ ] 17.2 Link every AutoFixable rule to its migrator task in `migratorTask` (`audio-and-shaders:N`).
  - [ ] 17.3 For each rule a positive and a negative fixture test; update the snapshot reports and review the diff; the analyzer score on `xna40-xact` and `xna40-effects` is recorded.
  - _Requirements: R15_

- [ ] 18. Checkpoint: shaders end to end
  - Migrate `xna40-effects`; the build check passes for every effect that is expected to pass on the current target and reports hints for the rest; the report lists every marker.
  - A person compares the rendered output of at least three of the candidate effects before and after on WindowsDX and DesktopGL (screenshots go in the PR description, not in the repository). The rewrite is not called visually safe without this.
  - _Requirements: R9 to R14_

- [ ] 19. Candidate runs (manual, outside the repository)
  - [ ] 19.1 Clone `Dandy-Dungeon`, `XNAGameStudio` samples (`Catapult`, `Movipa`, `RobotGame`), `XNAUnleashed` `Chapter7`, `Space-Shooter-Game`, `XNA-Shader-Programming`, `EvoNet`, `XNA-GPUDust` and `Grey-Infection` into a scratch folder outside the repository. Check each license file first.
  - [ ] 19.2 Run the analyzer and migrator with output outside the repository, build, run, and record per project: score before and after, cues played, effects passed, markers added, any false positives. Use the number of false positives to decide whether the scanner needs more than the tokenizer.
  - [ ] 19.3 Write the results into `docs/test-candidates/AUDIO-SHADERS.md` (summary only: no source, no assets) and delete the clones.
  - _Requirements: R16.5_

- [ ] 20. Final checkpoint
  - Full test suite, `dotnet build`, `dotnet format --verify-no-changes`, review of all snapshots, review of generated files for the fixtures, requirement traceability (every R item has a test or a manual checkpoint).
  - Update `content-conversion` R6 and R7 text and `project-migrator` shim notes to point to this spec (done by that spec's owner, listed here so it is not forgotten).
