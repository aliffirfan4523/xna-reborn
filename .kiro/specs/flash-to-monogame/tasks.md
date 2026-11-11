# Tasks: Flash (AS3 + Flixel) to MonoGame

How to read this list:
- Each numbered subtask (for example 4.2) is sized for one worker and one pull request.
- **MVP** marks the minimum demo slice. All MVP tasks should land by **Mon 23 Nov 2026**, so feature freeze on **24 Nov** has one day of slack. Tasks without MVP are second wave and start only after the checkpoint at task 14.
- Weight: **L** light (records, plain tests, known behavior), **H** heavy (parser, resolver, emitters, runtime logic).
- Workers do not edit specs, package versions or the XNA rule catalog. Shared files (`Directory.Packages.props`, solution, `packages.json`) are touched only by task 1.1 and by the task that names them.
- Every rule needs a positive and a negative fixture test. Fixtures are written from scratch. No third-party game code or assets in the repo.
- Every task ends with: `dotnet build` (warnings as errors), `dotnet test`, `dotnet format --verify-no-changes`.

Suggested waves (parallel inside a wave):
- Wave 1 (11 to 14 Nov): 1, 2, 3, 7, 8.1 to 8.2
- Wave 2 (13 to 18 Nov): 4, 5, 8.3 to 8.5, 9, 10
- Wave 3 (17 to 21 Nov): 6, 11, 12, 13
- Wave 4 (20 to 23 Nov): 14, 15, 16, 18
- After checkpoint: 17

- [ ] 1. Setup (MVP)
  - [x] 1.1 Create `XnaReborn.Flash.Analyzer`, `XnaReborn.Flash.Converter` and `XnaReborn.Flixel` projects, `tests/XnaReborn.Flash.Tests` and `tests/XnaReborn.Flixel.Tests`; add them to the solution; wire references as in design.md. No new package versions. Add `InternalsVisibleTo` for the test projects. (L)
  - [ ] 1.2 Add `ProjectKind` (Xna, Flash, Unknown) to Core and an optional `SourceKind` to the shared report and manifest models. Old JSON without the field must still load. Tests: round trip of old and new JSON. (L)
  - [x] 1.3 In `XnaReborn.Flixel`, add a folder `.editorconfig` that turns off naming rules for AS3-style member names, one placeholder class with lower-camel members, and a test. Prove that `dotnet build` and `dotnet format --verify-no-changes` pass. If a root rule still fires, record what was needed in the task notes. (L)
  - _Requirements: R3.1, R7.1, R12.8_

- [ ] 2. Fixtures (MVP)
  - [x] 2.1 `tests/fixtures/flash/flx-hello`: FlashDevelop project (`.as3proj`), entry class extending `FlxGame`, one `FlxState` with `FlxText`, no embeds. Expected results file (detector output, score range, no FL errors). (L)
  - [x] 2.2 `tests/fixtures/flash/flx-platformer`: the demo game. Menu state, play state, player (`FlxSprite` with animations and input), enemies in a `FlxGroup`, bullets with `recycle`, a hand-written tile layer from `FlxTileblock` first (tilemap comes in task 17), one generated png (drawn by a test helper, 16 by 16 sprite sheet), one short wav written by a test helper. `[Embed]` for each, `[SWF]` metadata with zoom 2. Uses `Number` fields without initializers, `int / int`, closures, getters/setters, `for each`, `super()` first, a `Vector.<FlxSprite>`. (H)
  - [x] 2.3 Negative and edge fixtures: `flx-swf-only` (a text file named `game.swf`, never decoded), `flx-fla-only` (a stub `.fla`), `flx-plain-as3` (AS3 without Flixel), `flx-two-entries`, `flx-missing-embed`, `flx-not-flash` (C# only). Expected results for each. (L)
  - [x] 2.4 Language-gap and embed-variant fixtures: `flx-gaps` (one file per FL020 to FL031 construct, each small, each with a known expected marker), `flx-embeds` (image, audio, csv map, xml, font, symbol embed). (H)
  - _Requirements: R12.1, R12.2_

- [x] 3. AS3 lexer (MVP)
  - [x] 3.1 Define `As3Token`, `As3TokenKind`, `Trivia`, `SourceSpan` records. (L)
  - [x] 3.2 Implement `As3Lexer`: identifiers, keywords, numbers (hex, exponent), strings with escapes, comments as trivia, operators including `>>>`, `===`, `!==`, `...`, `::`, `.<`, `..`, `@`. (H)
  - [x] 3.3 Regex versus division by previous token; XML literal as one opaque token run. (H)
  - [x] 3.4 Tests: token tables, trivia round trip (concatenating tokens and trivia reproduces the input exactly), regex/division cases, error tokens for unterminated strings. Fuzz-style test: random bytes never throw. (L)
  - _Requirements: R3.3, R3.4, R3.5_

- [ ] 4. AS3 parser (MVP)
  - [x] 4.1 Syntax tree records (compilation unit, package, import, class, interface, member, metadata, statements, expressions, `As3Unsupported`). Every node has a span. (L)
  - [x] 4.2 Parse declarations: package, imports, classes, interfaces, fields, methods, getters and setters, constructors, modifiers, metadata (`[Embed(...)]`, `[SWF(...)]`), `const`, `static`, `override`, rest and default parameters, `include` as unsupported. (H)
  - [x] 4.3 Parse statements and expressions: all control flow including `for each`, `for in`, `switch`, `try`; precedence climbing; `as`, `is`, `new`, `delete`, `typeof`, `in`; object, array and function literals; `Vector.<T>`. (H)
  - [x] 4.4 Automatic semicolon insertion, error recovery at statement and member boundaries, E4X and `with` and namespace forms as unsupported nodes. (H)
  - [x] 4.5 Tests and checkpoint: Verify snapshots of the tree for every fixture; negative tests per recovery path; if `XNAREBORN_FLASH_SAMPLES` is set, parse every `.as` file of Mode, FlxCollisions and Revenge with zero diagnostics (skipped when unset). (L)
  - _Requirements: R3_

- [x] 5. Project detection and reading (MVP)
  - [x] 5.1 `FlashProjectDetector`: `.as3proj`, `.actionScriptProperties`, `org.flixel` imports, `.swf`-only and `.fla` cases. Returns `ProjectKind`. Uses `IReadOnlyFileSystem`. (L)
  - [x] 5.2 `FlashProjectReader`: source roots, entry class (`[SWF]` or the `FlxGame` subclass), Flixel version probe, bundled `org/flixel` source detection (skip converting it). (H)
  - [x] 5.3 Combined detection: a folder matching both XNA and Flash returns a conflict result for the UI. XNA tests stay green. (L)
  - [x] 5.4 Tests per fixture, including `flx-swf-only` where the file system fake throws if `game.swf` is read. (L)
  - _Requirements: R1_

- [ ] 6. Rules and analyzer (MVP)
  - [x] 6.1 Add the four match types (`as3Import`, `as3Syntax`, `as3Metadata`, `as3Api`) to the Core rule model, the JSON converter and the rule schema. Existing XNA catalog tests stay green. (H)
  - [x] 6.2 Implement the four matchers over the syntax tree. (H)
  - [x] 6.3 Write `rules/flash-to-monogame.json` with the rules in design.md (FL001 to FL060). Each AutoFixable rule links its converter task (see 11 to 13). (H)
  - [x] 6.4 Positive and negative fixture test for every rule id; Verify snapshots of the reports. Review the snapshot diff in the PR. (L)
  - [x] 6.5 `FlashProjectAnalyzer` facade: progress, cancellation, deterministic JSON and markdown report, score. Hash-unchanged test on every fixture. (H)
  - [x] 6.6 Checkpoint: analyze the three candidate games locally (when `XNAREBORN_FLASH_SAMPLES` is set) and record scores and the top findings in the task notes. (L)
  - Task 6.1 notes: `As3ImportMatch`, `As3SyntaxMatch`, `As3MetadataMatch` and `As3ApiMatch` are Core records with converter entries and schema branches (appended at the end of the `match` oneOf, so the existing validator index logic is unchanged). The schema also accepts `FL###` ids and `appliesTo: "flash"`. The Flash analyzer still reads its own catalog format (`FlashRule`); the Core records exist so a Core-format catalog can carry the same rules. `RuleEngine` ignores these types.
  - Task 6.6 notes (local checkouts of Mode, FlxCollisions, Revenge via `XNAREBORN_FLASH_SAMPLES`; run on 2026-11-11, Flixel version reads "unknown" because none bundles `org/flixel`):
    - Mode: score 87, projected 89. Findings: FL011 x21, FL010 x16, FL027 x14, FL028 x11, FL001 x11, FL012 x2, FL043 (flash.net in MenuState, Error), FL053 (plugins or replay, Warning), FL047, FL050.
    - FlxCollisions: score 99, projected 100. FL010 x16, FL001 x7, FL027 x5, FL047, FL050.
    - Revenge: score 91, projected 92. FL005 (a .swc library, Error), FL026 x10, FL010 x10, FL001 x4.
    - Top blockers for a full conversion: Mode needs `flash.net` and plugin handling by hand; Revenge needs its .swc replaced by source.
  - _Requirements: R2, R4_

- [ ] 7. AS3 runtime support (MVP)
  - [x] 7.1 `AsArray`, `AsVector<T>`: all methods in R8.1 with AS3 semantics. Tests for edge cases (negative indexes, sparse assignment, `splice` forms, `sort` flags). (H)
  - [x] 7.2 `AsObject`, `AsDictionary`, `AsClass` (type form and asset form), `AsFunction` (arity-tolerant call, implicit conversions from `Action` and `Func` with up to 4 arguments). (H)
  - [x] 7.3 `AsOps` and `AsGlobals`: `trace`, `getTimer`, `isNaN`, casts (`int()`, `uint()`, `Number()`, `String()`, `Boolean()`), loose equality, `AsCast.To<T>`, unsigned shift helper, `Math` extras. (L)
  - [x] 7.4 Tests for 7.1 to 7.3 including NaN and string-to-number cases. (L)
  - _Requirements: R8_

- [x] 8. Flixel runtime: logic (MVP)
  - [x] 8.1 `FlxPoint`, `FlxRect`, `FlxU` (math helpers, `getAngle`, `rotatePoint`, `formatTime`, `floor`, `ceil`), `FlxG` constants and random. Ported from the AS3 source with the MIT notice. Tests with hand-computed values. (L)
  - [x] 8.2 `FlxBasic`, `FlxObject` (motion, `overlaps`, `separate` helpers, `flicker`, `kill/revive`), `FlxGroup` (`add`, `remove`, `recycle`, `getFirstAvailable`, `countLiving`, `setAll`, `callAll`, `sort`). Tests: group recycling, kill/revive, object motion step. (H)
  - [x] 8.3 `FlxQuadTree`, `FlxList`, and `FlxG.collide` / `overlap` / `separate` / `worldBounds`. Tests: separation on X and Y, one-way platforms, group against group, notify callbacks through `AsFunction`. (H)
  - [x] 8.4 `FlxTimer` and the timer manager plugin, `FlxPath` (data only), `FlxG` state switching (`switchState`, `resetState`). Tests: ticks, loops, pause. (L)
  - [x] 8.5 `FlxState`, `FlxGroup` lifecycle (`create`, `update`, `draw`, `destroy`) wired through a headless `FlxTestHost` that steps the game with no graphics device. Tests: a two-state script runs deterministically. (H)
  - _Requirements: R7.1 to R7.6_

- [x] 9. Flixel runtime: rendering, input, audio (MVP)
  - [x] 9.1 `FlxGame : Game` with fixed timestep, logical-size render target, zoom scaling with point sampling, window title and size. Needs `GraphicsDevice`. A smoke test runs only when a graphics device is available and is skipped otherwise. (H)
  - [x] 9.2 `FlxCamera` (single camera): scroll, bounds, follow, `flash`, `fade`, `shake`, background color. `FlxSprite` drawing: frames, animation (`addAnimation`, `play`), flip, scale, angle, alpha, color, `makeGraphic`, `loadGraphic`. Frame and animation math tested headless. (H)
  - [x] 9.3 `FlxText` with `SpriteFont`, alignment, color, shadow; `FlxButton`; `FlxTileblock`. (H)
  - [x] 9.4 Input: `FlxG.keys`, `FlxG.mouse`, gamepad stub, key name map. Tests with a fake input provider. (L)
  - [x] 9.5 Audio: `FlxSound`, `FlxG.play`, `playMusic`, volume, mute, pause, loop, fade. Wrap `SoundEffect` behind an interface so logic is testable. (H)
  - [x] 9.6 Asset loading through `AssetRef` and `Content.RootDirectory` (textures, sounds, data as bytes or text, sprite fonts). Tests with an in-memory asset provider. (L)
  - _Requirements: R7.2, R7.5, R7.7_

- [ ] 10. Type resolver and API map (MVP)
  - [x] 10.1 Roslyn-based generator that builds `flixel-api.json` (member names, parameter and return types, static flags) from the runtime sources, and a test that fails when the checked-in file is out of date. (H)
  - [x] 10.2 `TypeResolver`: declared types, literals, member access on project classes and runtime types, `unknown` fallback. (H)
  - [x] 10.3 Tests: table of expressions with expected types, including int/int, Number to int, `Vector` element types, `unknown` cases. (L)
  - _Requirements: R5.4 to R5.6_

- [ ] 11. Converter: emitters (MVP)
  - [x] 11.1 Type mapping, namespaces, class and member emission, access modifiers, getters and setters, constructors with base initializer, field initializers including `NaN`. Golden tests. (H)
  - [x] 11.2 Statement emission: all control flow, `for each`, `for in`, `switch` fallthrough, `try/catch` (catch type filters). Golden tests. (H)
  - [x] 11.3 Expression emission with resolver: int division, casts, `>>>`, `as`/`is`, object and array literals, `Vector.<T>`, closures to `AsFunction`, `Class` values, loose equality helper. Golden tests for each row of the language table. (H)
  - [x] 11.4 Marker emitter: `// XNAREBORN: FLxxx` with the original AS3 text; unsupported nodes; comment preservation from trivia. Tests for each marker rule id (FL020 to FL031, FL040 to FL055 subset). (H)
  - [x] 11.5 Compile check: a test helper compiles each golden C# output with Roslyn against the runtime sources and fails on errors. (H)
  - _Requirements: R5_

- [ ] 12. Converter: assets (MVP)
  - [x] 12.1 `AssetPlanner`: find `[Embed]` fields, resolve paths relative to the `.as` file, reject paths leaving the project, classify image, audio, data, font, symbol. Tests including path traversal attempts and `flx-missing-embed`. (H)
  - [x] 12.2 Feed images and audio to `ConvertedAsset` and the existing `MgcbWriter`; copy data files to `Content/`; emit asset handles (`new AssetRef(...)`) in the C# fields; fonts through `FontSubstituter`. Golden `Content.mgcb` tests. (H)
  - [ ] 12.3 Audio check on a real build: convert `flx-platformer`, run `dotnet-mgcb` 3.8.6.1 on the wav and an mp3 test file (generated or public domain, written by a test helper), and record whether `ffmpeg` is needed on the build machine and whether DesktopGL plays the result. Write the finding into the task notes, add a Runner hint if `ffmpeg` is needed. No network except package restore from the local cache. (H)
  - _Requirements: R6_

- [ ] 13. Converter: project output and pipeline (MVP)
  - [x] 13.1 `FlashCsprojGenerator` (using `PackageSelector`, MonoGame 3.8.6.1), `Program.cs` generator for the entry class, tool manifest, build isolation props. Golden tests. (L)
  - [x] 13.2 Embed the Flixel runtime sources as resources; `FlixelRuntimeProjectGenerator` writes them (with `.editorconfig` and `THIRD-PARTY-NOTICES.md`) to `XnaReborn.Flixel/` in the output. Test: the written project builds. (H)
  - [x] 13.3 `FlashMigrator`: orchestration through `OutputGuard.Begin` / `OutputSession`, manifest with `SourceKind`, marker counts and assets. Progress and cancellation. (H)
  - [x] 13.4 Safety tests: source hashes unchanged, non-empty output refused without confirmation, cancellation leaves no output, `.swf` never read. (L)
  - _Requirements: R9_

- [ ] 14. Checkpoint: demo slice (MVP)
  - [ ] 14.1 Convert `flx-platformer`, build with the .NET SDK, launch via the Runner for a few seconds, confirm exit state. Record the marker list and the score. This is the demo-day proof. (H)
  - [ ] 14.2 Fix whatever the checkpoint finds in the owning task's area (open a follow-up PR per fix). Update the spec notes with deviations and reasons. (H)
  - _Requirements: R5 to R10, R12.4_

- [ ] 15. Runner hints (MVP)
  - [x] 15.1 Add Flash hints to `hints.json`, each linked to an FL rule: missing asset, `dynamic` binder error, missing Flixel member, `AsFunction` conversion, `ffmpeg` missing (if task 12.3 says so). Matching tests, including "no known fix". (L)
  - [ ] 15.2 Source map comment or `#line` support in emitted C# so diagnostics point at the `.as` file; test with a broken fixture. (H)
  - _Requirements: R10_

- [ ] 16. UI flow (MVP)
  - [x] 16.1 Project-kind switch in `OpenProjectViewModel` and the services (`IAnalysisService`, `IMigrationService`, `IRunService`) using the detector; conflict prompt when both kinds match. No conversion logic in view models. (H)
  - [ ] 16.2 Report screen: show FL findings, entry class, Flixel version; swf-only explanation. (L)
  - [ ] 16.3 Summary screen: marker counts by rule id with links; the list of approximated behaviors. (L)
  - [ ] 16.4 Headless UI tests for the Flash path using the fixtures. (L)
  - _Requirements: R11, R13_

- [ ] 17. Second wave: fuller Flixel coverage (after checkpoint 14)
  - [ ] 17.1 `FlxTilemap` and `FlxTilemapBuffer`: `loadMap` from CSV text and array, auto-tiling modes, collision against tiles, `getTile`, `setTile`, `ray`. Fixture update: `flx-platformer` gains a tilemap and a csv embed. (H)
  - [ ] 17.2 `FlxEmitter` and `FlxParticle`. (H)
  - [ ] 17.3 `FlxSave` (JSON file under app data) and `FlxG.stream`-free audio extras. Tests with a temp folder. (L)
  - [ ] 17.4 Pixel access (`AsBitmapData`, `getPixel`, `setPixel`, `stamp`) and FL051 to AutoFixable. (H)
  - [ ] 17.5 E4X subset (`AsXml` over `System.Xml.Linq`: child access, attributes, `length()`, `toString()`), FL021 to AutoFixable for that subset. (H)
  - [ ] 17.6 Regular expressions (`AsRegExp`), FL030 to AutoFixable for simple patterns. (L)
  - [ ] 17.7 `AsEventDispatcher` and `Event` for game-defined events, FL041 to AutoFixable. (L)
  - [ ] 17.8 Checkpoint: convert Mode from a local checkout (`XNAREBORN_FLASH_SAMPLES`), build, run; list the remaining markers. If the result is runnable, add a demo script. (H)
  - _Requirements: R7.3_

- [ ] 18. Docs and final checks
  - [x] 18.1 `THIRD-PARTY-NOTICES.md` (Flixel MIT, reference projects with licenses); README section "Flixel 2.x source projects" with the scope and limits; update `.kiro/README.md` index. (L)
  - [x] 18.2 Demo script for the Flash path (open, analyze, migrate, run, show markers). (L)
  - [ ] 18.3 Final checkpoint: full build, tests, format on master; confirm no test uses the network; confirm the XNA flow tests are unchanged and green. (L)
  - _Requirements: R12, R13_

## Minimum demo slice (what must land before 24 Nov)

Tasks 1 to 16, with `flx-platformer` as the demo and `FlxTileblock` standing in for tilemaps. Checkpoint 14 is the gate. If the schedule slips, cut in this order: 16.3, 15.2, 9.5 (play silent, log the missing audio), 9.3 (`FlxButton`), keeping 1 to 8, 9.1 to 9.2, 9.4, 9.6, 10 to 13, 14, 16.1 to 16.2.

## Open points to settle during the work

- Whether `dotnet-mgcb` 3.8.6.1 needs `ffmpeg` for MP3 (task 12.3).
- Whether `net8.0` DesktopGL audio plays `SoundEffect` from converted MP3 on Windows and Linux (task 12.3).
- Which text approach gives the best match for Flixel's default font without redistributing a font file (task 9.3).
- Whether `Manifest` needs a schema version bump for `SourceKind` (task 1.2).
