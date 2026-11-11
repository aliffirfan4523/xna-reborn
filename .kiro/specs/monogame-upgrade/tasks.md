# Tasks: MonoGame Upgrade

Each numbered task is sized for one worker on its own branch (`analyzer/...`, `migrator/...`, `runner/...`, `app/...`). Workers do not edit specs, packages, project files, or the XNA rule catalog unless the task says so. Every rule task adds a positive and a negative fixture test per rule (`Category=Rules`). Rules in `rules/monogame-upgrade.json` that are AutoFixable link to the migrator task in the form `monogame-upgrade:N.N`.

- [x] 1. Confirm the open research items (docs only)
  - [ ] 1.1 Read the 3.8.6.1 `MonoGame.Framework.WindowsDX` nuspec and API reference: SharpDX dependency version, whether legacy `Microsoft.Xna.Framework.Storage` types still exist
  - [ ] 1.2 Confirm the `MonoGamePlatform` values for Windows 8, Windows Phone, OUYA, PSM and Vita, the Xamarin.iOS project GUIDs, and the mgcb platform names, from the 3.7.1 and 3.8.6.1 sources and from the sample projects listed in the design
  - [ ] 1.3 Confirm the first version that used `MonoGame.Common.props` and the last version that shipped WindowsGL
  - [ ] 1.4 Update the "to confirm" items in `design.md` and the rule table. Change a rule only in this spec at this step; the catalog is written in tasks 7 to 9
  - _Requirements: R2, R4_

- [x] 2. Fixtures
  - [ ] 2.1 Write fixtures A to I from the design under `tests/fixtures/`, each with a `README.md` that says it is original, synthetic, and under the repository license. Fixture F holds a text placeholder named `Protobuild.exe` that is never executed
  - [ ] 2.2 Add the fixture paths to the test helper
  - [ ] 2.3 Test that every fixture folder is listed in a table in `tests/fixtures/README.md` with the rules it is positive for
  - _Requirements: R11, R12_

- [x] 3. Core models, mgcb parser, schema
  - [ ] 3.1 Add `ProjectFamily`, `MonoGameBand`, `MonoGameVersionInfo`, `MonoGamePlatform`, and the new `ProjectInfo` fields with defaults. Existing tests must pass unchanged
  - [ ] 3.2 Add `MgcbFile` and `MgcbParser` (text in, model out, line numbers). Tests: globals, references, entries with parameters, comments, CRLF and LF, a malformed line (kept as a Diagnostic, not an exception)
  - [ ] 3.3 Update `RuleSchema.json`: id pattern `^(XNA|MG)[0-9]{3}$`, optional `appliesTo`, `matchMgcbSetting`, optional `metadata` on `matchProjectItem`. Add `RuleMatch` records for the new type. Existing catalog tests must pass unchanged
  - [ ] 3.4 Catalog loader: reject duplicate ids across catalogs loaded together
  - _Requirements: R3, R4_

- [x] 4. Detection
  - [ ] 4.1 `MonoGameProjectDetector` and `DiscoveredProject.Family`; `DiscoveryResult.MonoGameProjects`
  - [ ] 4.2 `MonoGameVersionDetector` with the signal table, confidence, and Diagnostic `ANA009` on conflict; platform detection from `HintPath` folder, `MonoGamePlatform`, and project type GUID
  - [ ] 4.3 `PackagesConfigReader`; `ProjectReader` follows `.projitems` imports
  - [ ] 4.4 New `NoProjectReason` wording; Protobuild-only folder message; `ANA008` for an XNA plus MonoGame mix
  - [ ] 4.5 Tests per fixture: family, version, platform; signals alone and in conflict; negative: `not-xna`, `xna40-basic` stay as before
  - _Requirements: R1, R2, R3_

- [x] 5. Reading and engine
  - [ ] 5.1 `ProjectReader` stores parsed `Content.mgcb` files in `ProjectReadResult` (through `ReadOnlyFileSystem`)
  - [ ] 5.2 `RuleEngine` runs a list of catalogs and filters by `appliesTo`; `ProjectAnalyzer.AnalyzeAsync` gets an overload that takes a list of catalogs
  - [ ] 5.3 Tests: an XNA rule never fires on a MonoGame project and the reverse; the old overload still works; the report JSON keeps existing fields
  - _Requirements: R4, R5_

- [x] 6. Matchers
  - [ ] 6.1 `projectItem.metadata` (`name`, `equals`, `notEquals`, `matches`). Test that the literal pin in MG008 equals `monoGame` in `Projects/packages.json`
  - [ ] 6.2 `MgcbSettingMatcher` for `platform`, `profile`, `config`, `compress`, `reference`, `entry` (with `importer`, `processor`, `nonStandardPair`)
  - [ ] 6.3 Positive and negative tests for both
  - _Requirements: R4_

- [x] 7. Catalog part A: project and package rules (MG001 to MG010)
  - [ ] 7.1 Create `rules/monogame-upgrade.json` with `appliesTo: "monogame"` and a `scoring` section; add MG001 to MG006 and MG010. This task adds the file; it does not touch `rules/xna-to-monogame.json`
  - [ ] 7.2 Add MG007 to MG009
  - [ ] 7.3 A positive and a negative fixture test per rule, using the Pos column of the design and `mg38-current` as the default negative
  - [ ] 7.4 Update snapshot reports and review the diff
  - _Requirements: R4, R5, R12_

- [x] 8. Catalog part B: platform and reference rules (MG011 to MG022)
  - [ ] 8.1 Add MG011 to MG016 (platform rules); a negative variant for each: a desktop project whose platform value is `DesktopGL`
  - [ ] 8.2 Add MG017 to MG022 (reference rules); the negative is a project with only `PackageReference` items
  - [ ] 8.3 Update snapshot reports and review the diff
  - _Requirements: R4, R5, R12_

- [x] 9. Catalog part C: content and source rules (MG023 to MG032)
  - [ ] 9.1 Add MG023 to MG027 (content rules); negatives: an mgcb with `/platform:DesktopGL`, no references, and only standard pairs
  - [ ] 9.2 Add MG028 to MG032 (source rules); the negative is a file that imports `Microsoft.Xna.Framework` and uses `Color.Transparent`, and a file with a type named `Net` in another namespace
  - [ ] 9.3 Update snapshot reports and review the diff
  - _Requirements: R4, R5, R12_

- [ ] 10. Checkpoint: analyzer
  - Full test suite, `dotnet build`, `dotnet format --verify-no-changes`
  - Every rule has a positive and a negative test; `mg38-current` scores 100; fixture B projected score is above its score
  - Hash-unchanged test over fixtures A to I; write-API scan passes
  - Review of every report snapshot
  - _Requirements: R1 to R5, R11, R12_

- [ ] 11. Project generation
  - [ ] 11.1 `LegacyMonoGameProjectGenerator` (mode of `SdkCsprojGenerator`): target from platform, compile and content items, filtered `DefineConstants`, project references, packages from `PackageSelector`, everything in R6.4 dropped. Snapshot per fixture A, B, C, G. Links: MG001 to MG006, MG011, MG017 to MG020
  - [ ] 11.2 Bump path for projects already on 3.8.x (fixture G): update the target framework, package versions, and tool manifest. Links: MG008, MG009
  - [ ] 11.3 `PackagesConfigMapper`: non-MonoGame entries to `PackageReference`; MonoGame and SharpDX entries dropped; unmappable entries to manual work; no network. Links: MG007
  - [ ] 11.4 Planner: plan items per project, exclusions, dry run
  - _Requirements: R6, R11_

- [ ] 12. Content upgrade
  - [ ] 12.1 `MgcbUpgrader`: set `/platform` to the target, remove install-folder `/reference` lines, keep everything else byte for byte. Links: MG023, MG024
  - [ ] 12.2 List custom pipeline assembly references and non-standard pairs in the manifest as manual work; copy the DLLs. Related: MG025, MG026
  - [ ] 12.3 Copy and list prebuilt `.xnb` files; do not invent an mgcb when the project has none. Related: MG027
  - [ ] 12.4 Tests: before and after, determinism (two runs, same bytes), asset names unchanged
  - _Requirements: R7_

- [ ] 13. Source upgrade
  - [ ] 13.1 `ColorTransparentBlackRewriter` with before and after fixtures, including a file with no `Microsoft.Xna.Framework` import (no change) and an ambiguous case (marker). Links: MG032
  - [ ] 13.2 (stretch) Reuse `XnaReborn.Compat` for `GamerServices`; add `Net` stand-ins only if the GamerServices pattern fits. Without shims, leave markers. Related: MG021, MG028, MG029
  - [ ] 13.3 Register `EntryPointRewriter` for legacy MonoGame projects; fixture with `Program` inside `#if WINDOWS || LINUX`
  - [ ] 13.4 Marker for Manual source findings (MG030, MG031) through `MarkerInserter`
  - _Requirements: R8_

- [ ] 14. Platform projects and solutions
  - [ ] 14.1 Platform projects (MG012 to MG016) become `Skipped` plan items; files copied unchanged; manifest lists them with the reason
  - [ ] 14.2 Shared project linking from `.projitems` into the migrated desktop project
  - [ ] 14.3 Solution generation lists only projects written as SDK-style; DesktopGL project from shared code only after confirmation
  - [ ] 14.4 Tests on fixture E: platform projects skipped, shared code linked, solution lists one project
  - _Requirements: R9_

- [ ] 15. Build and run
  - [ ] 15.1 Add hints H100 to H105 to `hints.json` with a matching and a non-matching sample diagnostic each
  - [ ] 15.2 Integration test (`XNAREBORN_INTEGRATION=1`): migrate fixtures A and B, run `dotnet build` on the copy; record whether `net8.0-windows` with WindowsDX 3.8.6.1 restores and builds
  - [ ] 15.3 Source folder hash unchanged after migration and build; the output has the manifest marker; the runner refuses the unmigrated fixture
  - _Requirements: R10, R11_

- [ ] 16. Studio UI
  - [ ] 16.1 Project list shows family, version with confidence, and platform
  - [ ] 16.2 Platform projects are shown as "not migrated, see manual work"
  - [ ] 16.3 UI tests with a fake analyzer; no migration logic in `XnaReborn.App`
  - _Requirements: R5, R9_

- [ ] 17. Manual checks on real projects (outside the repository)
  - [ ] 17.1 For each project in the design table, clone to a scratch folder outside the repo, recheck the license in the repository, run analyze, migrate to a new folder, build, and record the before and after score and what failed
  - [ ] 17.2 Find at least one finished 3.x game with a permissive license and add it to the list
  - [ ] 17.3 Record results in `docs/test-candidates/` (summary only; no source or assets copied)
  - _Requirements: R11_

- [ ] 18. Final checkpoint
  - Full test suite, `dotnet build`, `dotnet format --verify-no-changes`
  - Review of all manifests, diffs, and snapshots
  - Demo: BloomFilter or Squid goes from not building to building with minimal manual work
  - Confirm the original folders are unchanged (hash)
  - _Requirements: R1 to R12_
