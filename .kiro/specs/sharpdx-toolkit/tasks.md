# Tasks: SharpDX Toolkit to MonoGame

Each numbered task is sized for one worker. Sub-tasks are steps inside that task. Rules: every rule needs a positive and a negative fixture test, no test uses the network, `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass before a task is merged, and a worker does not edit the rule catalog of another task. Rule table: see `design.md` ("Proposed rules").

- [x] 1. Fixtures
  - [x] 1.1 Add the seven synthetic fixtures from `design.md` ("Fixtures"): `sdx-basic`, `sdx-effects`, `sdx-lowlevel`, `sdx-mixed`, `sdx-store-app`, `sdx-prebuilt`, `sdx-negative`. Each has an `expected` file in the same style as the XNA fixtures. All content is original; `.tkb` and image bytes are written at test time so the repo stays text-only.
  - [x] 1.2 Add a `README` line per fixture that says what it proves and that it is synthetic (no code or assets from any candidate game).
  - [x] 1.3 Fixture loader test: each fixture loads and has the expected file list.
  - _Requirements: R2, R8_

- [x] 2. Detection and model
  - [x] 2.1 Implement `SharpDxToolkitDetector` (references, imports, build actions, `packages.config`) next to `XnaProjectDetector`, read-only.
  - [x] 2.2 Add `IsSharpDxToolkit` to `DiscoveredProject`, `SourceFramework` and `SourceFrameworks` to `ProjectInfo` and the report, `DiscoveryResult.MigratableProjects`, and the new "no XNA or Toolkit project" reason text. Keep `IsXna` and `XnaProjects` unchanged.
  - [x] 2.3 Positive tests: `sdx-basic`, `sdx-effects`, `sdx-lowlevel` are Toolkit projects; `sdx-mixed` reports both. Negative tests: plain SharpDX without Toolkit (`sdx-negative`), an XNA fixture, and `not-xna` are not Toolkit projects.
  - [x] 2.4 Read-only test: file hashes of every Toolkit fixture are identical before and after analysis.
  - _Requirements: R1, R7_

- [x] 3. Schema and engine changes
  - [x] 3.1 Widen the rule id pattern to `^(XNA|SDX)[0-9]{3}$` in `RuleSchema.json` and `RuleCatalogValidator`. Test: an `SDX` id validates; an id with another prefix is rejected; the XNA catalog still validates.
  - [x] 3.2 Add `appliesTo` to the catalog schema and model. The analyzer runs a catalog only on projects of that framework. `AnalysisService` loads both catalog files. A broken Toolkit catalog must not stop XNA analysis (a diagnostic is reported). Positive: Toolkit fixture runs the SDX catalog. Negative: an XNA fixture never runs it, and a Toolkit fixture never runs the XNA catalog.
  - [x] 3.3 Add optional `names` to the `member` match: schema, `MemberMatch` record, JSON converter, `SourceRuleIndex`. Exactly one of `name` or `names`. Positive: a rule with `names` matches each listed name once per position. Negative: an unlisted name, a missing receiver, a wrong argument count, and a rule with both `name` and `names` (rejected).
  - [x] 3.4 Existing tests and XNA snapshots stay green with no snapshot change.
  - _Requirements: R2_

- [x] 4. Rule catalog `rules/sharpdx-toolkit-to-monogame.json`
  - Each sub-task adds its rules, one positive and one negative fixture test per rule, the Verify snapshot update (review the diff), and the `migratorTask` link on AutoFixable rules. Do not add a rule before task 3 is merged.
  - [x] 4.1 Project, platform and interop: SDX001 to SDX008. Confirm the two project type GUID prefixes in SDX004 against the Toolkit template (`ProjectTemplate.csproj`). Negative for SDX004: a normal desktop project GUID.
  - [x] 4.2 Content: SDX010 to SDX015. Negative: a project with only XNA `Compile` content items.
  - [x] 4.3 Namespaces and input: SDX020 to SDX028. Negative for SDX023: `using SharpDX.Toolkit.Graphics;` alone must not fire it. Negative for SDX025: a field named `keyboard` of a user-defined type.
  - [x] 4.4 Math: SDX030 to SDX035. Negative for SDX030: an instance named `matrix` calling `Translation`, and `Matrix.Translation` as a property. Negative for SDX032: a RH call.
  - [x] 4.5 Graphics: SDX040 to SDX051. Negative for SDX041: `SetBlendState` with two arguments. Negative for SDX042/SDX043: argument counts at the edges (1 vs 2, and 9). Negative for SDX051: `spriteBatch.Begin()` with no arguments.
  - [x] 4.6 Game: SDX060 to SDX063.
  - [x] 4.7 Catalog test: 45 rules, ids unique, every AutoFixable rule has a `migratorTask` that points at a task in this file, every Manual and Unsupported rule has a resolution text, and the catalog validates against the schema.
  - _Requirements: R2_

- [x] 5. Rename map and namespace rewriter
  - [x] 5.1 Create `rules/sharpdx-toolkit-map.json` (namespaces, `Matrix` factories, `MathUtil` members, state collection names) with sources listed. Confirm every MonoGame name against the 3.8.6.1 API docs and note any that do not exist. Loader in Core, with a schema test.
  - [x] 5.2 Implement `ToolkitNamespaceRewriter` for SDX020 to SDX024: `using`, aliases, fully qualified names, duplicate merge. Before/after tests, an idempotence test (a second run changes nothing), a trivia-preservation test, and a negative test that leaves `using SharpDX.Direct3D11;` alone.
  - [x] 5.3 Register it in `DefaultRewriters`.
  - _Requirements: R4_

- [x] 6. Math and graphics rewriters
  - [x] 6.1 `ToolkitMathRewriter` (SDX030, SDX031). Before/after tests per renamed member. Negative: instance receivers, LH factories (left alone and marked), `Matrix.Translation` as a property.
  - [x] 6.2 `ToolkitGraphicsRewriter` (SDX040 to SDX044, SDX060). Before/after tests for each pattern. Marker tests for names that do not map (`BlendStates.Default`, `RasterizerStates.*`, `BackBuffer` used other than `.Width`/`.Height`). Negative: two-argument `SetRenderTargets` is not touched. Note the `ToDisposeContent` behavior change in the manifest text.
  - [x] 6.3 Marker coverage: every Manual rule produces a `// XNAREBORN: SDXnnn:` marker at the first match, on `sdx-lowlevel`.
  - _Requirements: R4_

- [x] 7. Input rewriter
  - [x] 7.1 `ToolkitInputRewriter` (SDX025): remove the `KeyboardManager` field and construction, replace `GetState()` with `Keyboard.GetState()`. Negative and marker tests when the manager is passed elsewhere. MouseManager and PointerManager are not rewritten (markers only, SDX026/SDX027).
  - [x] 7.2 Compile test: the migrated `sdx-basic` input code compiles against MonoGame 3.8.6.1.
  - _Requirements: R4_

- [x] 8. Project generation
  - [x] 8.1 Extend `SdkCsprojGenerator` for Toolkit projects: drop SharpDX references, imports, NuGet restore guard target and import stamp, the five build actions, `packages.config`. Snapshot tests on `sdx-basic` and `sdx-effects`. Negative: XNA fixture output is byte-identical to before.
  - [x] 8.2 Default `PackageSelector` to WindowsDX for Toolkit projects. Confirm the `EntryPointRewriter` covers the `NETFX_CORE` `[MTAThread]` shape from the Toolkit `Program.cs` with a fixture. Mixed solution test: only the Toolkit project gets Toolkit handling.
  - [x] 8.3 Refuse Windows Store and Windows Phone projects per project (SDX004), with a manifest entry and no partial copy for that project.
  - _Requirements: R3_

- [x] 9. Content conversion
  - [x] 9.1 `ToolkitContentReader` in the analyzer: the five build actions into `ContentItem` records with `Link`-based asset names. Read-only. Tests on `sdx-basic`.
  - [x] 9.2 `TkFontConverter` (pure function): `TkFont` XML to `.spritefont`, per the table in `design.md`. Tests for every mapped field, character regions as characters, `DefaultCharacter`, and a warning for non-default `LineSpacing` and `NoPremultiply`. Negative: a file with a different root element is rejected with a clear message.
  - [x] 9.3 Toolkit branch in `ContentConverter`: classification, `.mgcb` entries for textures, models and `.wav` audio, ManualWork for the rest, effects copied and flagged (SDX013), `.tkb` prebuilt (SDX015), asset names kept without extension, and a content report entry for asset names that carry an extension. Verify with the dotnet-mgcb 3.8.6.1 tool which texture and model formats it accepts, using generated bytes, and record the result in the importer map notes. Add missing pairs to `rules/importer-map.json` only if needed, and keep XNA tests green.
  - _Requirements: R5_

- [x] 10. Planner, manifest and report
  - [x] 10.1 `MigrationPlanner` resolves `migratorTask` and rule ids across both catalogs; plan item ids are unique across catalogs. Test on `sdx-mixed`.
  - [x] 10.2 Manifest JSON and Markdown show the source framework and each SDX rule that fired. Snapshot tests. Negative: the XNA manifest snapshot is unchanged.
  - _Requirements: R7_

- [ ] 11. Effect check (research task, docs only)
  - [ ] 11.1 Take a Toolkit-syntax `.fx` file written for the fixture (original), run it through the MonoGame effect compiler used by `dotnet-mgcb` 3.8.6.1, and record the exact error. Update the explanation and resolution text of SDX013 with what was observed. If the compiler accepts the form, say so and downgrade SDX013's explanation.
  - _Requirements: R5, R9_

- [ ] 12. Runner hints and UI label
  - [ ] 12.1 Add the hint entries from `design.md` ("Runner and repair") to `HintCatalog`, each with a match test and a non-match test.
  - [ ] 12.2 Show the source framework label in the studio through the existing report binding. No logic in the view. ViewModel test only.
  - _Requirements: R6, R7_

- [ ] 13. Checkpoint: end-to-end on real candidates (read only)
  - [ ] 13.1 Shallow-clone `zlynn1990/EcoSim`, `nuclearpidgeon/graphicsproj2` and `Luaancz/Yggmire` outside the repo. Run the analyzer on each. Record project count, framework detected, rule counts by id, false positives found (manual review of a sample), and score. Do not commit the clones.
  - [ ] 13.2 Migrate EcoSim to an output folder outside the source, build it for WindowsDX and DesktopGL, and record the result and every manual edit needed. The build is expected to need manual work; record it honestly.
  - [ ] 13.3 Run the analyzer on `kobush/ModernMoleculeViewer` and confirm SDX004 fires as Unsupported.
  - [ ] 13.4 Write the findings into this spec as a "Candidate results" section. Add any new rule they require to the catalog by the normal rule process. Hashes of the source clones are identical before and after (read-only proof).
  - [ ] 13.5 Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` on master. Stop for owner review before task 14.
  - _Requirements: R1 to R9_

- [ ] 14. Stretch: compatibility stand-ins
  - Only start after the checkpoint shows that `RectangleF`, `Color4` or `GameSystem` appear often.
  - [ ] 14.1 Generate small stand-in types for the most frequent unmapped SharpDX types through the existing `Shims` generator, behind `MigrationOptions.EnableShims`, off by default. Positive and negative tests like the Storage shims. Link SDX034/SDX035 to this task only if it ships.
  - _Requirements: R4, R9_

- [ ] 15. Final checkpoint
  - [ ] 15.1 Update `docs/` and the README support matrix to say what Toolkit support covers and what it does not (R9).
  - [ ] 15.2 Confirm `rules/xna-to-monogame.json` and all XNA snapshots are unchanged by this work, except where an earlier task says otherwise.
  - [ ] 15.3 Run the full build, tests and format check. Stop for owner review.
  - _Requirements: R8, R9_
