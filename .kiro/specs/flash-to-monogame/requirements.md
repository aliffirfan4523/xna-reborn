# Requirements: Flash (AS3 + Flixel) to MonoGame

## Introduction

XNA Reborn brings stranded XNA projects back to life on MonoGame. This spec widens the same idea to a second dead platform: Flash. The first slice takes ActionScript 3 **source** projects built on **Flixel 2.x** and produces a migrated C# copy that builds and runs on MonoGame 3.8.6.1 (net8.0).

The slice stays small on purpose. Flixel games are a good first target because the engine is open source, small, and most of the game code only talks to Flixel, not to the wider Flash API. The converter translates the language and maps Flixel calls onto a C# Flixel runtime. Everything else is flagged with `// XNAREBORN:` markers and explained in the report.

This spec follows the same safety rules as the XNA flow. The original project is never touched. Compiled `.swf` files are never decompiled.

## Scope

In scope for the first slice:
- AS3 source projects (`.as` files) that use Flixel 2.x (AdamAtomic/flixel 2.5x API).
- Flash Builder, FlashDevelop and FlashDevelop-style folder layouts (`.as3proj`, `.actionScriptProperties`, or a bare source folder).
- `[Embed]` of images, audio and data files.
- A C# Flixel runtime on MonoGame DesktopGL, shipped as source inside the migrated copy.

Out of scope for the first slice:
- Games that exist only as `.swf` (safety rule 6, no decompiling).
- Flash Pro `.fla` / `.xfl` timelines and library symbols.
- ActionScript 1 and 2, Flex / MXML, AIR-only APIs, Stage3D.
- HaxeFlixel and Flixel 3.x or later.
- Network features, ad and score-board SDKs (flagged, not converted).

## Glossary

- **AS3**: ActionScript 3.0, the language of Flash Player 9 and later.
- **Flixel**: the AS3 game library by Adam "Atomic" Saltsman. This spec targets the 2.x line (2.5x).
- **Flash project**: a folder with AS3 source and, usually, a project file for FlashDevelop or Flash Builder.
- **Flash analyzer**: the read-only component that parses AS3 and reports findings (rules FL001 and up).
- **Converter**: the component that turns AS3 source into C# in a migrated copy.
- **Flixel runtime**: the C# library that implements the Flixel 2.x API on MonoGame.
- **AS3 runtime**: the small C# support library for AS3 types that C# lacks (`AsArray`, `AsObject`, `AsClass`, `AsFunction`).
- **Marker**: a `// XNAREBORN: FLxxx <text>` comment left in converted code where a person must act.
- **Migrated copy**: the output folder produced by the converter. The existing `OutputGuard` and manifest apply unchanged.
- **Asset handle**: the C# stand-in for an AS3 `[Embed]`-ed `Class`. It names a content asset.

## Requirements

### R1: Detect Flash projects
**User story:** As a developer, I want the tool to recognise my Flixel project when I open it.

Acceptance criteria:
1. WHEN a folder is opened, THE Flash analyzer SHALL report it as a Flash project IF it contains `.as` files and at least one of: an `.as3proj` file, an `.actionScriptProperties` file, or an `.as` file that imports `org.flixel`.
2. THE Flash analyzer SHALL find the entry class: the class with a `[SWF(...)]` metadata tag, or else the only class that extends `FlxGame`.
3. IF more than one candidate entry class exists, THEN THE Flash analyzer SHALL list them all and SHALL report rule FL060.
4. THE Flash analyzer SHALL detect the Flixel version when it can (a `FlxG.LIBRARY_NAME` / `LIBRARY_MAJOR_VERSION` constant in bundled Flixel source, or the folder layout) and SHALL report the result as `2.x`, another version, or unknown.
5. IF the folder holds only `.swf` files and no `.as` files, THEN THE Flash analyzer SHALL report rule FL003 as a Blocker and SHALL NOT read or decode the `.swf` file contents.
6. IF the folder holds `.fla` or `.xfl` files, THEN THE Flash analyzer SHALL report rule FL004 and SHALL NOT parse them.
7. THE existing XNA detector SHALL keep its current behavior. A folder SHALL be classified as exactly one project kind (XNA, Flash, or neither), and IF both kinds match, THEN THE tool SHALL ask the user to choose.

### R2: Read-only analysis
**User story:** As a developer, I want a report of what will and will not convert, before anything is written.

Acceptance criteria:
1. THE Flash analyzer SHALL open every file read-only.
2. THE Flash analyzer SHALL have a test proving that file hashes of a fixture project are unchanged after analysis (safety rule 2).
3. THE Flash analyzer SHALL parse every `.as` file into a syntax tree and SHALL report a parse failure as a diagnostic with file, line and column. One bad file SHALL NOT stop the analysis of the others.
4. THE Flash analyzer SHALL evaluate the rule catalog `rules/flash-to-monogame.json` (ids FL001 and up) and SHALL produce the same report model as the XNA analyzer: findings, locations, severity, classification, score.
5. THE Flash analyzer SHALL produce a conversion readiness score from 0 to 100 using the same scoring options as the XNA catalog.
6. THE Flash analyzer SHALL be deterministic: the same input SHALL give a byte-identical JSON report.
7. THE Flash analyzer SHALL honor a `CancellationToken` and SHALL report progress per file.

### R3: AS3 parsing
**User story:** As a maintainer, I want one solid AS3 parser that does not need a runtime install.

Acceptance criteria:
1. THE parser SHALL be written in C# with no new package dependency.
2. THE parser SHALL accept AS3 as used by Flixel 2.x and by typical Flixel games: packages, imports, classes, interfaces, metadata, `const`, `static`, getters and setters, rest parameters, default parameters, closures, `for each`, `for in`, `switch`, `try/catch/finally`, `as`, `is`, `new`, object and array literals, `Vector.<T>`, regex literals, and `include` (flagged).
3. THE parser SHALL handle automatic semicolon insertion.
4. THE parser SHALL tell a regex literal from a division operator by the previous token.
5. THE parser SHALL keep comments and trivia attached to tokens so the converter can keep comments in its output.
6. THE parser SHALL parse E4X (XML literals, `..`, `.@`, `.()`) as an opaque "unsupported E4X" node with a location, not as an error.
7. WHEN the parser meets a syntax it cannot read, THEN THE parser SHALL return a diagnostic with location and SHALL recover at the next statement or member boundary.
8. THE parser SHALL parse all fixtures and all candidate games listed in design.md without a diagnostic, as a checkpoint (the candidate games are read from a local path set by the developer; tests that need them are skipped when the path is not set).

### R4: Rule catalog
**User story:** As a maintainer, I want Flash rules to be data, as the XNA rules are.

Acceptance criteria:
1. THE rule catalog SHALL live in `rules/flash-to-monogame.json` and SHALL use the existing rule schema fields (id, title, severity, classification, category, match, explanation, resolution, migratorTask).
2. THE catalog SHALL use ids FL001 and up. The proposed set is in design.md.
3. WHEN a rule can be matched with an existing match type (`filePattern`, `projectItem`), THE catalog SHALL use that type.
4. THE system SHALL add only these new match types, each with a code matcher: `as3Import`, `as3Syntax`, `as3Metadata`, `as3Api`.
5. EACH rule SHALL have a positive fixture test and a negative fixture test.
6. EACH AutoFixable rule SHALL name the converter task that implements the fix.

### R5: Language conversion
**User story:** As a developer, I want my AS3 classes turned into C# that compiles.

Acceptance criteria:
1. THE converter SHALL write one `.cs` file for each `.as` file, in a path that mirrors the AS3 package path.
2. THE converter SHALL map `package a.b` to a C# namespace and keep type and member names unchanged. The AS3 package `org.flixel` SHALL map to the runtime namespace `org.flixel`.
3. THE converter SHALL map types as follows: `int` to `int`, `uint` to `uint`, `Number` to `double`, `Boolean` to `bool`, `String` to `string`, `void` to `void`, `Array` to `AsArray`, `Vector.<T>` to `AsVector<T>`, `Object` to `AsObject`, `Function` to `AsFunction`, `Class` to `AsClass`, `*` and missing types to `dynamic`.
4. WHEN a `Number` field or local has no initializer, THE converter SHALL initialize it to `double.NaN`, because AS3 does (not 0).
5. WHEN both operands of `/` are `int` or `uint`, THE converter SHALL emit floating-point division, because AS3 division always gives a `Number`.
6. WHEN AS3 assigns a `Number` to an `int` or `uint` (allowed in AS3), THE converter SHALL emit an explicit cast.
7. THE converter SHALL convert getters and setters to C# properties, `override` to `override`, rest parameters to `params object[]`, default parameter values to optional parameters, `for each` to `foreach`, and closures to lambdas or local functions.
8. THE converter SHALL map `trace(...)`, `getTimer()`, `Math.*`, `isNaN`, `parseInt`, `parseFloat`, `int()`, `uint()`, `Number()`, `String()` and `Boolean()` to calls in the AS3 runtime.
9. THE converter SHALL turn `super(...)` as the first statement of a constructor into a C# base initializer. IF `super(...)` is not first, THEN THE converter SHALL keep the call as a method-style statement and add a marker.
10. WHEN the converter meets a construct it does not support (FL020, FL021, FL025 and similar), THEN THE converter SHALL write a marker that names the rule id and SHALL leave the original AS3 text in a comment, so the file still compiles where practical.
11. THE converter SHALL be deterministic: the same input SHALL give identical output files.
12. THE converter SHALL emit the original AS3 comments next to the code they describe.
13. THE converter SHALL NOT use string search and replace on source text. It SHALL work from the syntax tree.

### R6: Embedded assets
**User story:** As a developer, I want my pictures, sounds and data to keep working.

Acceptance criteria:
1. WHEN an `[Embed(source="...")] var X:Class` declares an image (`png`, `jpg`, `gif`), THE converter SHALL add a texture entry to the generated `Content.mgcb` and SHALL emit an asset handle for `X`.
2. WHEN the source is an `mp3` or `wav`, THE converter SHALL add an audio entry using the same importer mapping as the XNA content converter, and SHALL emit an asset handle.
3. WHEN the source is any other file (`xml`, `txt`, `csv`, `json`, `tmx`, `mimeType="application/octet-stream"`), THE converter SHALL copy it to the output as a content file and SHALL emit a data asset handle that can be read as bytes or text.
4. IF the source path is missing on disk, THEN THE converter SHALL report rule FL015 and SHALL continue.
5. IF the embed refers to a font (`fontName`, `.ttf`, `.otf`), THEN THE converter SHALL use the existing font substitution and SHALL report rule FL013.
6. IF the embed refers to a SWF symbol (`symbol=`) or a `.swf` source, THEN THE converter SHALL report rule FL014 as Unsupported and SHALL NOT read the `.swf`.
7. THE converter SHALL resolve each `[Embed]` path relative to the `.as` file that declares it, as the Flash compiler does, and SHALL reject any path that leaves the project folder.
8. THE converter SHALL give each asset a stable asset name and SHALL reuse the existing `AssetNameResolver` and `MgcbWriter` behavior (sorted entries, `/` separators).
9. THE converter SHALL never upload assets anywhere.

### R7: Flixel runtime
**User story:** As a developer, I want Flixel code to behave like Flixel on MonoGame.

Acceptance criteria:
1. THE Flixel runtime SHALL expose the Flixel 2.5x public API with the same type, method and property names as AS3 (lower-camel members), in the namespace `org.flixel`, so converted game code needs no renames.
2. THE Flixel runtime SHALL provide, at minimum for the first demo: `FlxGame`, `FlxState`, `FlxBasic`, `FlxObject`, `FlxSprite`, `FlxGroup`, `FlxText`, `FlxPoint`, `FlxRect`, `FlxG`, `FlxU`, `FlxTimer`, `FlxButton`, `FlxSound`, `FlxCamera` (single camera), and the input classes behind `FlxG.keys`, `FlxG.mouse` and `FlxG.gamepads` (gamepads may be a stub).
3. THE Flixel runtime SHALL provide, for the full slice: `FlxTilemap`, `FlxTileblock`, `FlxEmitter`, `FlxParticle`, `FlxPath`, `FlxSave`, `FlxG.collide`/`overlap`/`separate` with the quad tree, `FlxG.camera` effects (`flash`, `fade`, `shake`), `FlxG.worldBounds`, `FlxG.random`, and `FlxG.play`/`playMusic`.
4. THE Flixel runtime SHALL run the update loop with a fixed step taken from the `FlxGame` frame-rate argument, and SHALL set `FlxG.elapsed` the same way Flixel 2.x does.
5. THE Flixel runtime SHALL render the game at its logical size (for example 320 by 240) to an off-screen target and scale it by the zoom factor with point sampling, so pixel art stays sharp.
6. THE Flixel runtime SHALL keep game logic separate from drawing, so object, group, collision, timer and tilemap logic can be tested without a graphics device.
7. THE Flixel runtime SHALL load assets only through the asset handle, from the `Content` folder next to the executable.
8. THE Flixel runtime SHALL write `FlxSave` data only to a per-game folder under the user's application data folder, and SHALL NOT touch the network.
9. THE Flixel runtime SHALL keep the Flixel MIT license and copyright notice in the source files it derives from Flixel, and the migrated copy SHALL include a `THIRD-PARTY-NOTICES.md` with that notice.
10. WHEN a Flixel member exists in AS3 Flixel 2.5x but not in the runtime, THE converter SHALL report rule FL055 for each use.

### R8: AS3 runtime support
**User story:** As a developer, I want AS3 collections and dynamic types to behave close to the original.

Acceptance criteria:
1. THE AS3 runtime SHALL provide `AsArray` (push, pop, shift, unshift, splice, slice, concat, indexOf, join, sort, sortOn, reverse, `length`, indexer, and sparse-safe growth on assignment), `AsVector<T>`, `AsObject` (dynamic property bag with string keys and indexer), `AsDictionary`, `AsClass`, `AsFunction`, and static helpers for `trace`, `getTimer`, `isNaN`, `int`/`uint`/`Number`/`String`/`Boolean` casts.
2. THE AS3 runtime SHALL implement `AsFunction` so that method references and lambdas convert to it, and so that it can be called with fewer or more arguments than declared, as AS3 allows.
3. THE AS3 runtime SHALL give `AsClass.New(...)` so that code like `recycle(Bullet)` works.
4. THE AS3 runtime SHALL NOT take any dependency beyond the .NET base library and MonoGame.
5. THE AS3 runtime SHALL be part of the same shipped source set as the Flixel runtime.

### R9: Migration output and safety
**User story:** As a developer, I want my original project left alone and my output folder protected.

Acceptance criteria:
1. THE converter SHALL write only to a new output folder through `OutputGuard.Begin`, and SHALL never write into the source folder.
2. THE converter SHALL refuse a non-empty output folder unless the user confirms, as the XNA migrator does.
3. THE converter SHALL write the migration manifest (`xnareborn-manifest.json` plus the markdown view) with a source kind of `flash`, the list of created files, the list of markers by rule id, the list of assets, and the package versions.
4. THE converter SHALL write a runnable layout: `<name>.csproj`, `Program.cs`, `Content/Content.mgcb`, `.config/dotnet-tools.json`, the Flixel runtime project, and build-isolation props, using the existing generators where they fit.
5. THE converter SHALL pin MonoGame to the version in the existing `packages.json` (3.8.6.1) and SHALL NOT bring in any other package.
6. THE converter SHALL write the Flixel runtime and AS3 runtime as source files in the output (not as a binary), so the user can read and change them.
7. THE original project files SHALL have the same hashes after a conversion; a test SHALL prove it.
8. THE converter SHALL honor a `CancellationToken` and SHALL not leave a half-written output folder when cancelled (the `OutputSession` staging behavior applies).
9. THE system SHALL NOT upload any source or asset anywhere. AI-assisted repair, if offered for Flash projects later, SHALL stay off by default and opt-in per project.

### R10: Build and run
**User story:** As a developer, I want to see the converted game run.

Acceptance criteria:
1. THE Runner SHALL build and launch a Flash-derived migrated copy with no change to its guard logic: the manifest marks it as a migrated copy.
2. THE Runner SHALL map compiler errors in converted files back to the AS3 origin when a `#line` directive or a source map comment is present, and otherwise SHALL show the C# location.
3. THE hint catalog SHALL get Flash-specific hints, each linked to an FL rule id. The first set SHALL cover: missing asset, `dynamic` binder error, missing Flixel member, and `AsFunction` conversion errors.
4. WHEN the game exits within the early-crash window, THE Runner SHALL report a startup failure with captured output, as it does for XNA.

### R11: UI flow
**User story:** As a developer, I want the same open, analyze, migrate, run flow for a Flash project.

Acceptance criteria:
1. WHEN the user opens a Flash project, THE UI SHALL show the project kind and the entry class, and SHALL route analysis to the Flash analyzer.
2. THE UI SHALL show FL findings in the existing report screen with no new screen. Flash-specific wording (AS3 file, line, column) SHALL come from the report data.
3. THE UI SHALL show the converted C# files in the existing diff view as new files next to the original AS3 file where a pair exists.
4. THE UI SHALL show the marker count by rule id after migration, and SHALL link each marker to the file and line.
5. UI code SHALL contain no conversion logic.
6. IF the opened folder holds only a `.swf`, THEN THE UI SHALL explain that compiled-only games are out of scope and that the tool does not decompile.

### R12: Quality, licensing and tests
**User story:** As a maintainer, I want confidence in the converter and clean licensing.

Acceptance criteria:
1. THE project SHALL include fixture projects written from scratch under `tests/fixtures/flash/`. They SHALL NOT copy any third-party game code or assets.
2. EACH rule SHALL have a positive fixture and a negative fixture. Reports SHALL use Verify snapshots.
3. THE converter SHALL have a golden test per fixture: input AS3, expected C# files (Verify), and a compile check of the output with Roslyn against the runtime sources.
4. THE project SHALL have an end-to-end test that converts a fixture, builds it with the .NET SDK, and checks the exit code (marked as an integration test; no network access, restore from the local package cache).
5. NO test SHALL depend on network access.
6. THE project SHALL NOT bundle a third-party game. Real games used for checkpoints SHALL be read from a local folder outside the repository.
7. THE project SHALL keep third-party license notices (Flixel MIT) and SHALL list any reference project used, with its license, in `THIRD-PARTY-NOTICES.md`.
8. ALL public APIs SHALL carry XML documentation. `dotnet build` (warnings as errors) and `dotnet format --verify-no-changes` SHALL pass.

### R13: Transparency about limits
**User story:** As a developer, I want to know what the tool did not convert.

Acceptance criteria:
1. THE migration summary SHALL list, per rule id, how many markers remain and which files hold them.
2. THE migration summary SHALL state which AS3 behaviors are approximated (for example `Number` to `double`, NaN defaults, int division, text rendering, audio looping).
3. THE tool SHALL describe the first slice as "Flixel 2.x source projects" in the UI and in the README. It SHALL NOT claim to convert "Flash games" in general.
