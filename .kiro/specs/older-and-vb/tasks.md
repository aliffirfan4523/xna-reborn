# Tasks: Older XNA Projects (1.0 / 2.0) and Visual Basic XNA Projects

Rules for every task:

- One worker, one branch, one PR. Branch names: `analyzer/older-N-name`, `migrator/older-N-name`, `runner/older-N-name`, `app/older-N-name`.
- Read `design.md` first. Write only the files the task names. A task that needs a shared type from an earlier task starts after that task is merged.
- Fixtures are written from scratch. Never copy a file from a candidate project.
- Each rule or match type needs a positive and a negative test. No test uses the network.
- Before the PR: `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` all pass. Snapshot diffs are reviewed by hand, not accepted blindly.
- Tasks 1 to 10 are Part A. Tasks 11 to 25 are Part B. Part B can start after task 2 and task 6 are merged, because it reuses the version model and catalog schema.

## Part A: XNA 1.0 and 2.0

- [x] 1. Fixtures for older projects (`tests/fixtures`)
  - [x] 1.1 `xna10-express-basic`: Express 1.0 C# game (type GUID `{9F340DF3-...}`, `v1.0`, Express targets, no `TargetFrameworkVersion`, in-project `Content` items with `XNAUseContentPipeline`, `SpriteTextureProcessor`, `LoadGraphicsContent`/`UnloadGraphicsContent` with `if (loadAllContent)`, `ResourceManagementMode`, `SpriteBlendMode`)
  - [x] 1.2 `xna20-vs2005`: 2.0 game with `NestedContentProject`, `Content.contentproj` (GUID `{96E2B04D-...}`), `.sln` format 9.00, a Windows and an Xbox 360 game project, uses `LoadContent`, `RenderState`, `Effect.Begin`
  - [x] 1.3 `xna10-clean`: Express 1.0 project that uses none of the removed APIs
  - [x] 1.4 One `README.md` per fixture ("Original synthetic code written for this repository") and the list of expected findings
  - _Requirements: R1, R2, R3, R14_

- [x] 2. Version model (`XnaReborn.Core`, `XnaReborn.Analyzer`)
  - [x] 2.1 Add `V10` and `V20` to `XnaVersion` after `V40`. Update `XnaVersionJsonConverter` (read, write, error text) and `RuleSchema.json` `xnaVersion` enum
  - [x] 2.2 Update `XnaVersionDetector.Parse` and `Format`
  - [x] 2.3 Tests: JSON round trip for all five versions, schema accepts "1.0" and "2.0", old reports still load
  - _Requirements: R1_

- [x] 3. Detection signals (`XnaReborn.Analyzer`)
  - [x] 3.1 Change the import filter to `Game Studio` and `.targets`; add the Express GUID fallback (lowest priority, never a conflict)
  - [x] 3.2 `XnaProjectDetector.IsXna`: accept `Microsoft.Xna.*.targets` imports and the Express GUID
  - [x] 3.3 Tests on `xna10-express-basic`, `xna20-vs2005`, `xna10-clean`: version, `IsXna`, no ANA002, weak hints give no conflict; a 3.1 and a 4.0 fixture still detect as before
  - _Requirements: R1, R2_

- [x] 4. `projectItem` metadata match (`Core`, `Analyzer`)
  - [x] 4.1 Add optional `Metadata` to `ProjectItemMatch`; update converter, schema, `ProjectItemMatcher` (case-insensitive value compare, all pairs must match)
  - [x] 4.2 Unit tests: match, missing key, wrong value, no metadata field (old behavior)
  - _Requirements: R3_

- [x] 5. `methodDeclaration` match type (`Core`, `Analyzer`)
  - [x] 5.1 Add `MethodDeclarationMatch`, converter case, schema entry, `SourceRuleIndex` support
  - [x] 5.2 Implement in the current C# `SourceWalker` (task 14 moves it behind the fact sink): declaration name, `override` modifier, namespace gate
  - [x] 5.3 Tests: override found, non-override with `overrides: true` not found, same name without the gate, same name in another class
  - _Requirements: R3_

- [x] 6. Part A catalog (`rules/xna-to-monogame.json`, `rule-cases.json`)
  - [x] 6.1 XNA001 `include` to `*Microsoft.Xna.*.targets`; XNA002 explanation text
  - [x] 6.2 Widen XNA022 to XNA043 to `["1.0","2.0","3.0","3.1"]`
  - [x] 6.3 Add XNA100 to XNA105 as in the design table (XNA102 and XNA103 link to `older-and-vb:9`, XNA101 to `older-and-vb:8`)
  - [x] 6.4 Positive and negative cases for every row of the table, including the 4.0 negative for each widened rule (the coverage test must still pass)
  - [x] 6.5 Update analyzer snapshot reports and review the diff by hand
  - _Requirements: R3_

- [x] 7. In-project content and legacy processors (`Analyzer`, `Core`)
  - [x] 7.1 Add `ContentItem.InProject` (default false); add `InProjectContentReader`; call it from `ProjectReader`
  - [x] 7.2 Add `aliases` to `rules/importer-map.json` and the `ImporterMap` loader (`Resolve`, standard-pair check treats aliases as standard)
  - [x] 7.3 Check the default parameters of the 1.0 texture processors against a real 1.0 candidate or archived docs before writing the note text; if they cannot be confirmed, write the note as "defaults may differ"
  - [x] 7.4 Tests: Express fixture gives content items with importer, processor, name; XNA050 does not fire for the two legacy processors; XNA050 still fires for a real custom processor
  - _Requirements: R2, R4_

- [x] 8. Content conversion and project file for in-project content (`Migrator`)
  - [x] 8.1 `ContentConverter`: use the game project folder as base for `InProject` items; keep everything else
  - [x] 8.2 `SdkCsprojGenerator`: `hasContent` also true for in-project items
  - [x] 8.3 Snapshot tests of `Content.mgcb` and the generated `.csproj` for `xna10-express-basic`; 2.0 nested project case unchanged
  - [x] 8.4 Merge this task after any open `content-conversion` PR (see design risk 2)
  - _Requirements: R4, R5_

- [x] 9. `LoadGraphicsContentRewriter` (`Migrator`)
  - [x] 9.1 Implement for XNA102 and XNA103: rename, drop parameter, add `const bool` first statement
  - [x] 9.2 Markers and no change for: calls by name, odd parameter list, existing `LoadContent`
  - [x] 9.3 Register in `DefaultRewriters`; before and after tests; preservation test (untouched bytes identical)
  - [x] 9.4 Hand check: migrate `xna10-express-basic` and run `dotnet build` on the result (integration test, `XNAREBORN_INTEGRATION=1`)
  - _Requirements: R5_

- [ ] 10. Checkpoint A
  - Analyze all three older fixtures: expected findings match the fixture READMEs; source folder hashes unchanged
  - Migrate `xna10-express-basic` and `xna20-vs2005`; the migrated copies build with 0 errors (warnings recorded)
  - Read-only scan of the candidates [jackpal/Dandy-Dungeon](https://github.com/jackpal/Dandy-Dungeon), [frham20/SharpEngine](https://github.com/frham20/SharpEngine), [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) Movipa, [keithjohnston/infiniminer](https://github.com/keithjohnston/infiniminer): shallow clone into a temporary folder outside the repo, run the analyzer, record version, score and top findings in the PR text, delete the clones
  - Migrate one candidate copy and record the compile errors that remain; add each new gap as an issue note in the PR (no silent scope growth)
  - Full test run and `dotnet format --verify-no-changes`
  - _Requirements: R1 to R5, R14_

## Part B: Visual Basic

- [x] 11. Fixtures for VB (`tests/fixtures`)
  - [x] 11.1 `vb-xna40-basic`: VB 4.0 game and content project reference, project-level `Import` items, `Program.vb` with `#If WINDOWS OrElse XBOX Then` around `Shared Sub Main`, `My Project/AssemblyInfo.vb`, comma separated `DefineConstants`
  - [x] 11.2 `vb-xna31-handmade`: VB 3.1, plain references, `MyType WindowsForms`, `Application.Designer.vb`, `SpriteBlendMode`, a `With device.RenderState` block, `fx.Begin` without parentheses, one `My.Application` call
  - [x] 11.3 `vb-monogame-already` (negative) and `mixed-cs-vb-sln` (a C# game plus a VB library)
  - [x] 11.4 README per fixture with expected findings
  - _Requirements: R6 to R8, R14_

- [x] 12. Shared language types (`Core`, `Analyzer`)
  - [x] 12.1 `SourceLanguage` enum with JSON converter; `ProjectLanguage.FromPath`; `ProjectInfo.Language` as a last parameter with a default
  - [x] 12.2 `Rule.Languages` and `Rule.AutoFixLanguages`; `RuleSchema.json`; loader error for an unknown language
  - [x] 12.3 `RuleEngine` skips rules by project language; `Scorer` and planner use the effective classification
  - [x] 12.4 Report JSON and Markdown show `language`; schema minor increment; old reports still load
  - [x] 12.5 Tests: default is C#; rule with `languages ["vb"]` skipped for a C# project; AutoFixable with `autoFixLanguages ["csharp"]` counts as Manual for VB in score and projected score
  - _Requirements: R6, R8, R13_

- [x] 13. VB discovery and project reading (`Analyzer`)
  - [x] 13.1 `ProjectDiscovery`: `.vbproj` at the four extension checks, folder search, `.sln` filter, messages
  - [x] 13.2 `ProjectXml.ReadItems`: `ItemGroup` `Import` becomes `NamespaceImport`; `ProjectReadResult.ImplicitImports`
  - [x] 13.3 `ProjectReader`: `DefineConstants` split on `;` and `,`; strip `=value`; set `Language`
  - [x] 13.4 Tests on `vb-xna40-basic`, `vb-xna31-handmade`, `vb-monogame-already`, `mixed-cs-vb-sln`: language, IsXna, version, imports, defines, compile files; hash test for all four
  - _Requirements: R6, R7, R14_

- [x] 14. Matcher refactor to a fact sink (`Analyzer`, C# only)
  - [x] 14.1 Add `ISourceFactSink`, `ISourceWalker`, `MemberUse`; move rule logic from `SourceWalker` to `RuleHitCollector`; rename the walker to `CSharpSourceWalker`; carry `MethodDeclaration` from task 5
  - [x] 14.2 `SourceRuleMatcher.Match` takes `implicitImports` and picks the walker by `tree.Options.Language`
  - [x] 14.3 No VB code and no package change in this task. All existing source-matching tests and analyzer snapshots pass with no edit
  - _Requirements: R9_

- [x] 15. Alias resolution in the sink (`Analyzer`)
  - [x] 15.1 Resolve the first segment of a qualified name through the alias table (`using X = ...`)
  - [x] 15.2 Tests: alias qualified type hit, alias of an unrelated namespace no hit
  - [x] 15.3 Review snapshot diffs by hand; any new C# finding must be a true positive
  - _Requirements: R8, R9_

- [x] 16. VB walker (`Analyzer`)
  - [x] 16.1 Add `Microsoft.CodeAnalysis.VisualBasic` 4.14.0 to `Directory.Packages.props` and the Analyzer project
  - [x] 16.2 `VisualBasicSourceWalker`: one test per row of the walker table in the design
  - [x] 16.3 `SourceRuleIndex.Build(rules, ignoreCase)`; case-insensitive gate, receiver and type names for VB
  - [x] 16.4 `SourceScanner`: pick files by project language; VB parse options with symbols; ANA004 for VB syntax errors
  - [x] 16.5 Tests: `vb-xna31-handmade` gives the expected XNA022, XNA024, XNA039/040 findings; `With` block, no-parentheses call, alias, project-level imports, different casing all covered; a VB file with a user type named like an XNA type and no gate gives no finding
  - _Requirements: R8_

- [x] 17. VB catalog (`rules/xna-to-monogame.json`, `rule-cases.json`)
  - [x] 17.1 Add XNA110 to XNA114 as in the design table; add `autoFixLanguages` to XNA022, XNA024, XNA039, XNA040
  - [x] 17.2 Add the `language` field to `rule-cases.json` (default `csharp`); positive and negative case per new rule and per changed rule
  - [x] 17.3 Check that XNA001 to XNA008 and XNA045 to XNA049 fire on the VB fixtures where the design says they do
  - [x] 17.4 Update snapshots and review the diff
  - _Requirements: R8, R13_

- [x] 18. Rewriter contracts (`Migrator`)
  - [x] 18.1 `SourceRewriteContext.Language` (default C#), `ISourceRewriter.Language` default member, registry key `(RuleId, Language)`, `SourceRewriteEngine` language argument
  - [x] 18.2 `ProjectMigrator.CompileFiles` and the rewrite call pick language by extension
  - [x] 18.3 `IMarkerInserter`, `CSharpMarkerInserter` (current class), `MarkerPrefixes.All`; grep every user of `MarkerInserter.Prefix` (manifest, diff viewer, App) and switch it to `MarkerPrefixes.All`
  - [x] 18.4 Tests: two rewriters for one rule ID with different languages register; same pair throws; engine skips the wrong language; all existing rewriter tests pass with no edit
  - _Requirements: R9, R13_

- [ ] 19. VB project generation (`Migrator`)
  - [x] 19.1 Rename `SdkCsprojGenerator` to `SdkProjectGenerator` with a language seam; keep a thin `SdkCsprojGenerator` alias only if tests need it
  - [x] 19.2 VB output: `MyType Empty`, `Option*` copy, `NamespaceImport` back to `<Import>`, `StartupObject` filter, no `Nullable`/`ImplicitUsings`, `GenerateAssemblyInfo=false` when `My Project/AssemblyInfo.vb` is kept
  - [x] 19.3 Omit `My Project` designer compile items and list each in the manifest; do not copy `.myapp` and `.settings` files silently (copy as manual work)
  - [x] 19.4 `SolutionGenerator`: VB project type GUID for `.vbproj`
  - [x] 19.5 Verify snapshots of `.vbproj` and `.sln` for `vb-xna40-basic`, `vb-xna31-handmade`, `mixed-cs-vb-sln`
  - [x] 19.6 Integration test (`XNAREBORN_INTEGRATION=1`): migrate `vb-xna40-basic` and run `dotnet build`; expect 0 errors once task 20 is merged; before that, record the entry point error
  - _Requirements: R10_

- [x] 20. VB rewriters (`Migrator`)
  - [x] 20.1 `VisualBasicMarkerInserter` (comment `'`, statement-start placement, multi-line statements)
  - [x] 20.2 VB entry point rewriter (`#If ... OrElse ... Then`, `Shared Sub Main` and `Module` `Sub Main`)
  - [x] 20.3 `BlendModeMap` shared by both languages; VB `SpriteBatchBeginRewriter` (positional and named arguments)
  - [x] 20.4 `VisualBasicMarkerRewriter` for XNA024, XNA039, XNA040
  - [x] 20.5 Register in `DefaultRewriters`; before and after tests; preservation test; `autoFixLanguages` coverage test (a VB rewriter exists exactly for the rules whose `autoFixLanguages` includes `vb`, ignoring ENTRYPOINT)
  - _Requirements: R11_

- [x] 21. Runner (`Runner`)
  - [x] 21.1 `GameLocator` and `MigratedCopyGuard`: `.vbproj`
  - [x] 21.2 `MsBuildOutputParser`: `BC` codes, and `vbc :` lines without a file
  - [x] 21.3 `HintCatalog`: hints for `BC30002` on `My` types and `BC30451` after a removed XNA name
  - [x] 21.4 Tests with real-format sample lines (written from scratch)
  - _Requirements: R12_

- [x] 22. Report and UI (`App`)
  - [x] 22.1 Language badge in the project summary and the report view
  - [x] 22.2 Marker highlight in the diff viewer reads `MarkerPrefixes.All`
  - [x] 22.3 Headless UI test: open `vb-xna40-basic`, see the VB badge and the XNA110 finding. No migration logic in the App project
  - _Requirements: R13_

- [ ] 23. Checkpoint B
  - Analyze all VB fixtures; hashes unchanged; findings match the fixture READMEs
  - Migrate `vb-xna40-basic`; the migrated copy builds with 0 errors and the project runs up to window creation in the integration environment
  - Migrate `vb-xna31-handmade`; the build errors that remain are exactly the `My` and marker items listed in the manifest
  - Read-only scan of [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) `GSMSample_4_0_Mango_VB` and `CardsStarterKit_4_0_VB`, [VaderConsulting/WindowsVBGame1](https://github.com/VaderConsulting/WindowsVBGame1), [playdeezgames/tggd_hj5](https://github.com/playdeezgames/tggd_hj5) (must not be detected as XNA); run [nilllzz/p3d-legacy](https://github.com/nilllzz/p3d-legacy) locally for timing only. Shallow clones in a temporary folder outside the repo, deleted afterwards. Record results in the PR text
  - Full test run and `dotnet format --verify-no-changes`
  - _Requirements: R6 to R13, R14_

- [ ] 24. Stretch: VB ports of the render state and effect pass rewriters
  - [ ] 24.1 `RenderStateRewriter` VB port for direct statements; `With` blocks stay marker only
  - [ ] 24.2 `EffectPassRewriter` VB port for the full `For Each ... Next` pattern; anything else stays marker only
  - [ ] 24.3 Add `vb` to `autoFixLanguages` of the ported rules only after the tests pass; update the coverage test
  - [ ] 24.4 Start only if checkpoint B shows these patterns in real VB code
  - _Requirements: R11_

- [x] 25. Stretch: adjacent API gaps found in research
  - [x] 25.1 Check MonoGame 3.8.6.1 for `EffectPool`, `GammaRamp`, `ClipPlane`, `VertexShader`, `PixelShader`, `PrimitiveType.TriangleFan` and the `Color` byte order change
  - [x] 25.2 For each one that is missing or different, add a rule with a positive and negative case; for each one that exists, record that in the PR text and add nothing
  - _Requirements: R3_

- [ ] 26. Final checkpoint
  - Full test suite, `dotnet build`, `dotnet format --verify-no-changes`
  - Update `.kiro/specs/project-analyzer`, `project-migrator`, `content-conversion` and `build-and-run` only where this spec changed a contract (version list, language field, importer-map aliases, rewriter registry key); no other edits
  - Re-run the candidate scans and update the table in `design.md` with the measured results
  - Review all manifests and diffs of the migrated fixtures
  - _Requirements: R1 to R14_

## Follow-up outside this spec

- Windows Phone VB projects get the generic XNA007 text. A friendlier message is an open question in the design.
- A `net8.0-windows` (WindowsDX) VB build was not tried; add it to the integration test once the C# WindowsDX case is verified.
