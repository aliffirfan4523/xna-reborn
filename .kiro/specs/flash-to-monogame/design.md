# Design: Flash (AS3 + Flixel) to MonoGame (C# / .NET 8)

## Overview

Flixel 2.x games are plain AS3 classes that extend a small set of engine classes (`FlxGame`, `FlxState`, `FlxSprite`, `FlxGroup`). That shape makes a two-part answer work:

1. **Translate the language.** A hand-written AS3 parser builds a syntax tree. A converter walks the tree and writes C#.
2. **Replace the engine.** A C# port of Flixel 2.5x, keeping the original names, runs on MonoGame. Converted game code calls it unchanged.

The converter does not try to be a general Flash-to-C# tool. It handles the AS3 that Flixel games use, maps `org.flixel` onto the runtime, maps `[Embed]` onto MonoGame content, and leaves a `// XNAREBORN: FLxxx` marker wherever a person must decide.

```
Flash project folder (read-only)
   -> FlashProjectDetector      (is it Flash? entry class? Flixel version?)
   -> As3Lexer / As3Parser      (syntax tree per .as file)
   -> FlashAnalyzer + rules     (FL findings, readiness score)          [XnaReborn.Flash.Analyzer]
   -> TypeResolver              (types of expressions, Flixel API map)
   -> Emitters                  (C# files, markers)                      [XnaReborn.Flash.Converter]
   -> AssetPlanner              ([Embed] -> Content.mgcb / content copy)
   -> ProjectGenerators         (csproj, Program.cs, tool manifest, runtime sources)
   -> OutputGuard / OutputSession / Manifest   (existing, XnaReborn.Migrator)
   -> Runner build + launch     (existing, XnaReborn.Runner)
```

## Research findings

Everything below was read from primary sources on 2026-11-11. Licenses are what each repository states; where I could not confirm one, the text says so.

### 1. Parsing AS3 from .NET

| Option | Finding | Verdict |
|---|---|---|
| ANTLR 4 grammar | The runtime (`Antlr4.Runtime.Standard`) is under the [BSD 3-clause license](https://github.com/antlr/antlr4/blob/dev/LICENSE.txt) and the [ANTLR license page](http://antlr.org/license) describes it. I did not find an AS3 grammar I could confirm in the [antlr/grammars-v4](https://github.com/antlr/grammars-v4) repository (which states that it has [no common license](https://github.com/antlr/grammars-v4), each grammar has its own). The only AS3 grammar I found is an ANTLR 3 file, [AS3.g from the metaas project](https://gist.github.com/tonfever/21aebf26b05d5bc47cb6), with no license stated in what I read. | Rejected. Needs a new package, a code-generation build step, and a grammar of unclear license written for ANTLR 3. |
| Apache Royale compiler | [Apache Royale](https://royale.apache.org/) compiles AS3, MXML and CSS. The [compiler](https://github.com/apache/royale-compiler) is a full Java compiler under the Apache License 2.0. | Reference only. It is too large to port and it is Java. |
| Irony-based parser | [asparser](https://github.com/atsushieno/asparser) converts AS3 to C# using the Irony framework. Its author calls it "an experimental hack" and lists known gaps (semicolons required, `===`, `>>>`). I did not find a license file. | Do not reuse. Confirms the problems to plan for. |
| Hand-written parser | [ax3](https://github.com/innogames/ax3) (MIT) is an AS3 to Haxe converter with a hand-written parser. Its README documents the same hard parts: no automatic semicolon insertion, partial E4X. It uses `parse -> typed tree -> filters -> emit`. | **Chosen design reference.** We write our own parser in C#, and copy no code. |

**Decision: hand-written lexer and recursive-descent parser in C#, no new package.** Reasons:
- The repo's tech steering says "no new dependency without need". A parser is a few thousand lines of C# we fully own and can test with Verify.
- The hard parts of AS3 (regex versus division, automatic semicolon insertion, `Vector.<T>`, `get`/`set`/`override`/`include` contextual keywords, E4X) need hand control of the lexer. A generated grammar fights these.
- Trivia (comments) must survive into the C# output. A hand lexer keeps them cheaply.
- Roslyn is already the project's analysis tool for C#. A Roslyn-style immutable syntax tree (records) fits the project's style.

Language reference used for the grammar work: the [Adobe AS3 reference](https://help.adobe.com/en_US/FlashPlatform/beta/reference/actionscript/3/all-index-N.html) and the [AIR SDK reference mirror](https://airsdk.dev/reference/actionscript/3.0/XML.html), which notes that XML follows ECMA-357 (E4X) edition 2.

### 2. Prior art: AS3 converters

| Project | Direction | License | Use |
|---|---|---|---|
| [ax3](https://github.com/innogames/ax3) | AS3 to Haxe, typed pipeline, reads SWC for external types | MIT | Design model (parse tree, typed tree, filters). No code reuse. |
| [as3hx](https://github.com/HaxeFoundation/as3hx) | AS3 to Haxe | MIT | Idea source for a compatibility library (`Compat.hx`). |
| [as3tohaxe](https://github.com/geekrelief/as3tohaxe) | AS3 to Haxe (Haskell) | not checked | Its README says a complete conversion is not possible when native classes need overriding. A reminder to keep scope narrow. |
| [asparser](https://github.com/atsushieno/asparser) | AS3 to C# (Irony) | none found | Only prior art found for AS3 to C#. Experimental. |
| [Apache Royale](https://royale.apache.org/) | AS3 to JS | Apache 2.0 | Reference for language semantics. |
| Online AI converters (for example [codeconvert.ai](https://www.codeconvert.ai/actionscript-to-csharp-converter)) | AS to C# | service | Not used. They upload source to a third party, which safety rule 4 forbids. |

No open-source AS3 to C# converter that handles a real game was found. The converter is new work.

### 3. Language gaps and handling

| AS3 feature | C# approach | Class |
|---|---|---|
| `package` / `import` | `namespace` / `using`. Package-level functions and variables go in a generated static class. | Auto |
| One public type per file, plus helper classes outside the package block | Emit all in the file. | Auto |
| Access: `public`, `private`, `protected`, `internal` (default) | Same names. The AS3 default `internal` maps to C# `internal`. | Auto |
| `int`, `uint`, `Number`, `Boolean`, `String` | `int`, `uint`, `double`, `bool`, `string`. | Auto |
| `Number` default `NaN`, `int` default 0, `String` default `null` | Emit explicit `double.NaN` for uninitialized `Number` fields and locals. | Auto (FL028) |
| `/` always yields `Number` | If both operands are `int` or `uint`, emit `(double)a / b`. | Auto (FL027) |
| Implicit `Number` to `int` assignment | Emit explicit cast when the resolver sees a wider source type. | Auto |
| `>>>`, bitwise ops on `Number` | `(int)((uint)x >> n)`. Operands cast to `int` first. | Auto |
| Untyped `var x`, `*` | `dynamic` plus marker. | Auto + warn (FL023) |
| `undefined` | `null` plus marker where the value is tested. | Auto + warn |
| `Array` | `AsArray` (List of `object?` with AS3 method names). | Auto |
| `Vector.<T>` | `AsVector<T>` (List of `T` with AS3 method names and `length`). | Auto |
| `Object` as a bag, `{a:1}`, `obj[key]`, `delete`, `in` | `AsObject` (string-keyed dictionary with indexer). | Auto (FL029) |
| `flash.utils.Dictionary` | `AsDictionary` (reference-equality keys). Weak keys ignored. | Auto |
| `Function` values, closures, method references | `AsFunction`. Lambdas and method groups convert implicitly. Calls become `f.Call(...)`. | Auto (FL026) |
| `Class` values (`FlxState` class passed to `FlxGame`, `recycle(Bullet)`) | `AsClass` from `typeof(T)`. `new cls()` becomes `cls.New()`. | Auto |
| Getters and setters | C# properties. | Auto |
| Rest `...args`, `arguments` | `params object[]`. `arguments` is not converted. | Auto / Manual (FL022) |
| Default parameter values | C# optional parameters when the value is a constant, else overload plus marker. | Auto |
| `for each (var x in c)` | `foreach`. | Auto |
| `for (var k:String in o)` | `foreach` over `AsObject.Keys` / array indices as strings. | Auto |
| `as`, `is`, `instanceof` | C# `as` for reference types, `AsCast.To<T>()` for value types, `is`. | Auto |
| `==` and `===` on mixed types | `==` when types match. Otherwise a helper `AsOps.LooseEquals` plus marker. | Auto + warn |
| `switch` on strings, fallthrough | C# `switch` with explicit `goto case` for fallthrough. | Auto |
| `const` | `const` for literals, `static readonly` or `readonly` otherwise. | Auto |
| `super(...)` before other statements | Base initializer. Otherwise marker. | Auto + warn |
| Regex literals `/x/g` | `System.Text.RegularExpressions` wrapper `AsRegExp`. First slice: marker only (FL030). | Manual |
| E4X (`XML`, `XMLList`, `.@a`, `..`, `.()`) | First slice: parse as opaque node, marker (FL021). Later: `AsXml` over `System.Xml.Linq`. | Manual |
| `with` | Marker (FL020). | Manual |
| Dynamic classes, runtime property adds | `AsObject` base plus marker (FL024). | Manual |
| `namespace`, `use namespace`, `::` | Marker (FL025). | Manual |
| `flash.events` (`EventDispatcher`, `Event`) used by game code | `AsEventDispatcher` in the runtime (stretch task). | Auto (FL041) |
| `flash.display` (`Sprite`, `Bitmap`, `Stage`, `addChild`) used outside Flixel | Not supported in slice. | Manual (FL040) |
| `getDefinitionByName`, `ApplicationDomain` | Marker. | Manual (FL031) |
| `[Embed]`, `[SWF]`, `[Frame]`, other metadata | See asset section. Unknown metadata is dropped with an Info marker. | Auto |

The `int`/`Number` rules matter most. They are the behavior changes that compile fine and then play wrong, so each one has a golden test.

### 4. Flixel 2.x API surface

Source: [AdamAtomic/flixel](https://github.com/AdamAtomic/flixel) (MIT text in `license.txt`; the [Flixel download page](https://flixel.org/download.html) says the source is distributed under the MIT License; GitHub's detector shows "Other" because the file adds an author note). Documentation: [org.flixel.FlxGame](https://flixel.org/docs/org/flixel/FlxGame.html).

Files in `org/flixel` (read via the GitHub contents API): `FlxBasic`, `FlxButton`, `FlxCamera`, `FlxEmitter`, `FlxG`, `FlxGame`, `FlxGroup`, `FlxObject`, `FlxParticle`, `FlxPath`, `FlxPoint`, `FlxRect`, `FlxSave`, `FlxSound`, `FlxSprite`, `FlxState`, `FlxText`, `FlxTileblock`, `FlxTilemap`, `FlxTimer`, `FlxU`, plus `data/`, `plugin/`, and `system/` (`FlxAnim`, `FlxDebugger`, `FlxList`, `FlxPreloader`, `FlxQuadTree`, `FlxReplay`, `FlxTile`, `FlxTilemapBuffer`, `FlxWindow`, `debug/`, `input/`, `replay/`).

Constructor (from the docs): `FlxGame(GameSizeX, GameSizeY, InitialState, Zoom = 2, GameFramerate = 60, FlashFramerate = 30, UseSystemCursor = false)`. The Flixel demo "Mode" calls `super(320,240,MenuState,2,50,50)` and carries `[SWF(width="640", height="480", ...)]` and `[Frame(factoryClass="Preloader")]` metadata.

How games use it (from reading Mode's `Mode.as` and `PlayState.as`):
- The entry class extends `FlxGame` and calls `super` with sizes and the first state class.
- States extend `FlxState` and override `create()` and `update()`.
- Assets are `[Embed(source="data/x.png")] protected var ImgX:Class;` fields passed to `loadGraphic`, `FlxTilemap.loadMap`, `FlxSound.loadEmbedded`, `FlxG.play`.
- Game objects live in `FlxGroup`s and are collided with `FlxG.collide` / `FlxG.overlap`.
- `FlxEmitter` makes particles (gibs). `FlxText` makes HUD text. `FlxG.keys`, `FlxG.mouse` read input.

### 5. Prior C# Flixel ports

| Project | License found | Use |
|---|---|---|
| [bsimser/flixel](https://github.com/bsimser/flixel) | MIT (2018) | Look at it for API-shape ideas. Not checked in depth. |
| [StAidan/X-flixel](https://github.com/StAidan/X-flixel) (XNA) | No license file found (license endpoint returned 404) | Reference for ideas only. Copy nothing. |
| [initials/XNAFlixel](https://github.com/initials/XNAFlixel) (XNA) | No license file found (license endpoint returned 404) | Reference for ideas only. Copy nothing. |
| [born2snipe/flixel-gdx](https://github.com/born2snipe/flixel-gdx) (Java / libgdx) | not checked | Shows how a port replaces `BitmapData` with GPU textures. |
| [HaxeFlixel](https://github.com/haxeflixel/flixel) | MIT as far as I know (not checked) | Evolution of Flixel, API differs from 2.x. Not a source. |

These ports are old, small, or unlicensed, and none target MonoGame 3.8 on net8.0. **Decision: write the runtime fresh in C#, as a direct port of the MIT-licensed AS3 Flixel 2.5x source.** Port from the original AS3 files (we may translate MIT code and keep the notice). Do not copy code from the unlicensed ports.

### 6. Assets

- `[Embed(source="x.png")]` becomes a texture entry in `Content.mgcb`. The existing `ContentConverter`, `AssetNameResolver`, `MgcbDocument` and `MgcbWriter` are reused.
- `.mp3` maps to `Mp3Importer` with the song or sound processor, as the XNA content converter already does (see the [MonoGame `Mp3Importer` API page](https://docs.monogame.net/api/Microsoft.Xna.Framework.Content.Pipeline.Mp3Importer.html)). MonoGame community threads report that DesktopGL needs OGG output for `Song` ([issue 6609](https://github.com/MonoGame/MonoGame/issues/6609)). **Not verified here:** whether `dotnet-mgcb` 3.8.6.1 needs `ffmpeg` on the build machine for MP3. Task 12.3 verifies this on a real build. If it does, the plan is: use `SoundEffect` (PCM) for short effects, and report a hint in the Runner when `ffmpeg` is missing. We never bundle `ffmpeg`.
- Data embeds (`.txt` / `.csv` tile maps with `mimeType="application/octet-stream"`, `.xml`) are copied to `Content/` and read at run time through an asset handle that returns bytes or text.
- Fonts: Flixel's default font is an embedded TTF. The runtime uses a `SpriteFont` built from an installed font at conversion time (the existing `FontSubstituter` approach), with a bitmap-font option left for later.

### 7. Candidate games (read only, not cloned into the repo)

| Game | Source | License found | Notes |
|---|---|---|---|
| Mode (Flixel's demo) | [AdamAtomic/Mode](https://github.com/AdamAtomic/Mode) | Repo holds `license.txt` with the Flixel MIT text. GitHub shows "Other". | Entry `Mode.as`, `PlayState`, `Player`, `Enemy`, `Spawner`, `Bullet`, groups, emitters, tilemap, mp3 and png embeds. Best end goal. |
| FlxCollisions | [AdamAtomic/FlxCollisions](https://github.com/AdamAtomic/FlxCollisions) | `license.txt` with the Flixel MIT text. | Small physics and collision demos for Flixel 2.0. Good for collision tests. |
| Revenge (Flixel 2.55 platformer) | [yadurajiv/revenge](https://github.com/yadurajiv/revenge) | GitHub detects zlib. The repo also has `flixel-license.txt` and a separate `content-license.txt`. | FlashDevelop project (`revenge.as3proj`). Check the content license before any use beyond read-only analysis. |

Rejected: [fwienber/jumper](https://github.com/fwienber/jumper) (no license found), [canabalt-hf](https://github.com/ninjamuffin99/canabalt-hf) (HaxeFlixel, not AS3).

Games are never copied into the repository. A developer points the tests at a local checkout through the environment variable `XNAREBORN_FLASH_SAMPLES`. Tests skip when it is not set. Asset licenses inside these repos are not clear, so redistribution is out (safety rule 5).

## Project layout

New projects, following the structure steering:

| Project | Path | Responsibility |
|---|---|---|
| `XnaReborn.Flash.Analyzer` | `src/XnaReborn.Flash.Analyzer` | Read-only. Flash project detector, AS3 lexer and parser, syntax tree, project reader, FL rule matchers, report. References `XnaReborn.Core` and `XnaReborn.Analyzer` (for the rule engine and report writers). |
| `XnaReborn.Flash.Converter` | `src/XnaReborn.Flash.Converter` | Creates the migrated copy. Type resolver, emitters, asset planner, project generators. References `XnaReborn.Flash.Analyzer` and `XnaReborn.Migrator` (for `OutputGuard`, `OutputSession`, `Manifest`, `MgcbWriter`, generators). |
| `XnaReborn.Flixel` | `src/XnaReborn.Flixel` | The C# Flixel and AS3 runtime. References MonoGame (DesktopGL). Its `.cs` files are also embedded in the Converter as resources and written into each migrated copy as a project. No reference from other `src` projects (the Converter embeds files, it does not reference the project). |
| tests | `tests/XnaReborn.Flash.Tests`, `tests/XnaReborn.Flixel.Tests` | xUnit and Verify. |
| fixtures | `tests/fixtures/flash/*` | Small AS3 projects written from scratch, with expected results. |
| rules | `rules/flash-to-monogame.json` | Rule catalog. |
| Core additions | `src/XnaReborn.Core` | `ProjectKind` enum, optional `SourceKind` on the shared report and manifest models (backward compatible). |

Why not put this in the existing Analyzer and Migrator projects? They are named and scoped for XNA. A second language with its own parser would blur them and make the XNA path riskier to change. Keeping the Flash code in sibling projects lets the XNA tests stay untouched, and it makes the widening easy to remove if the hackathon scope changes.

The Flixel runtime is a normal project under `src/` so it compiles, is tested, and is covered by `dotnet format` here. The same files are embedded as resources and written to `<output>/XnaReborn.Flixel/` in each migrated copy, the same pattern as `XnaReborn.Compat` (see `ShimProjectGenerator`). The generated game project references that project by relative path. The runtime uses original AS3-style member names, so it needs a folder-scoped `.editorconfig` that turns off the naming rules there. That `.editorconfig` is also copied to the output.

## Reuse of existing parts

| Existing part | How the Flash flow uses it |
|---|---|
| `OutputGuard.Begin` / `OutputSession` | The converter writes into the staging folder and commits. Same refusal of non-empty folders and same cancellation behavior. |
| `Manifest` / `ManifestBuilder` / writers | Add an optional `SourceKind` and a `Markers` section (counts per rule id). Old manifests stay valid. The Runner's migrated-copy guard keeps checking the same marker. |
| `MgcbWriter` / `MgcbDocument` / `AssetNameResolver` / `ContentConverter` pieces | Embedded assets become `ConvertedAsset` entries and flow through the same writer. Sorted entries, `/` separators, same importer map. |
| `FontSubstituter` | Used for embedded fonts (FL013). |
| `SdkCsprojGenerator`, `PackageSelector`, `PackageVersions`, `BuildIsolationGenerator`, tool manifest generator | Same package pins (MonoGame 3.8.6.1). The csproj for a Flash game is simpler (no source csproj to read), so a small `FlashCsprojGenerator` wraps `PackageSelector` rather than adding options to `SdkCsprojGenerator`. |
| `ShimProjectGenerator` pattern | Model for writing the runtime project from embedded sources. |
| Runner (`BuildRunner`, `MigratedCopyGuard`, parsers, `HintCatalog`) | Unchanged logic. New hints in `hints.json` for FL rules. |
| `RuleCatalogLoader` / `RuleEngine` / `Scorer` / report writers | Reused. The rule schema gains four match types (see below). |
| App services (`IAnalysisService`, `IMigrationService`, `IRunService`) | Each gets a project-kind switch behind the interface. No logic moves into view models. |
| Diff builder | Shows converted files as created files, with the AS3 origin path as a label. |

## Parser design

- `As3Lexer`: produces tokens with leading and trailing trivia. Tracks "regex allowed" from the previous significant token. Handles `Vector.<T>` (`.<` token), `...`, `::`, `@`, `..`, hex, octal-free numbers, string escapes, and XML literals as a single opaque token run (balanced angle brackets).
- `As3Parser`: recursive descent with precedence climbing. Automatic semicolon insertion follows the ECMAScript rule: insert at a newline when the next token cannot continue the statement, and before `}`. Contextual keywords (`get`, `set`, `override`, `each`, `include`, `namespace`) are read by position.
- Syntax tree: immutable records (`As3CompilationUnit`, `As3Class`, `As3Method`, `As3Field`, `As3Statement`, `As3Expression` families). Every node has a `SourceSpan`. Unknown or unsupported syntax becomes an `As3Unsupported(reason, span, rawText)` node.
- Error recovery: on a syntax error, record a diagnostic and skip to the next `;`, `}`, or member start.
- Performance: single pass, no backtracking beyond one token of lookahead plus a small speculative scan for generics and arrow-free function expressions.
- Deterministic: no reflection, no time, no random.

## Type resolver

The resolver is a light pass, not a full AS3 type checker. It assigns a type to an expression when it can, from:
- declared types of locals, fields, parameters, and return types,
- literals,
- the Flixel API map (`flixel-api.json`), listing member types of `org.flixel.*` and the AS3 runtime. A test generates it from the runtime sources with Roslyn and fails when it differs from the checked-in file,
- the project's own classes (declared members, including base classes).

When it cannot decide, the type is `unknown`. The emitters treat `unknown` conservatively: no integer-division change, no cast, and a marker when the missing type would change meaning. This keeps the failure visible and the output compilable more often than not.

## What the converter does and what it leaves

The converter does:
- Language translation in the table above.
- `org.flixel` mapping, `[Embed]` mapping, entry point generation (`Program.cs` runs the entry class; the `[SWF]` and `[Frame]` tags are dropped; zoom and size come from the `FlxGame` arguments).
- Comment preservation.
- A report of every marker by rule id.

The converter leaves a marker (and, where useful, the original AS3 text as a comment) for:
- E4X, `with`, namespaces, dynamic classes, `arguments`, regex literals (first slice), reflection (`getDefinitionByName`).
- `flash.display`, `flash.net`, `flash.external`, filters, blend modes, `BitmapData` drawing outside Flixel.
- Ad and score SDKs (`com.mochibot`, `com.newgrounds`, `com.kongregate`, `mochi.as3` and similar).
- Flixel members the runtime lacks (FL055).
- Any loosely typed `==` it cannot prove safe.

Marker format, one per line, always followed by the original construct when it is short:

```csharp
// XNAREBORN: FL020 'with' statement is not supported. Original AS3 follows.
// with (obj) { x = 1; }
```

## Flixel runtime plan

**Namespace and names.** `org.flixel`, with the exact AS3 member names. Every public method is `virtual` where Flixel lets subclasses override it.

**Game loop.** `FlxGame` derives from `Microsoft.Xna.Framework.Game`. The constructor takes `(int width, int height, AsClass initialState, int zoom = 2, int gameFramerate = 60, int flashFramerate = 30, bool useSystemCursor = false)`. `IsFixedTimeStep = true` and `TargetElapsedTime = 1 / gameFramerate`. `FlxG.elapsed` is set as in Flixel 2.x. The flash framerate (draw rate) is accepted and ignored in the first slice; draw happens each frame.

**Rendering.** One `RenderTarget2D` at logical size. Objects draw through a `FlxCamera` with `SpriteBatch` (point sampling, alpha blend). The target is then scaled by `zoom` into the window. `FlxSprite` keeps a `Texture2D`, a frame rectangle, flip, scale, angle, alpha and color tint. `makeGraphic` creates a 1x1 white texture scaled and tinted (fast path) or a CPU-side `AsBitmapData` for pixel operations (stretch).

**Logic is graphics-free.** `FlxBasic`, `FlxObject`, `FlxGroup`, `FlxTimer`, `FlxQuadTree`, `FlxTilemap` collision, `FlxU`, `FlxPoint`, `FlxRect` and `FlxG.collide` / `overlap` / `separate` use no `GraphicsDevice`, so xUnit tests run headless. Drawing sits behind `draw()` overrides that need a `FlxCamera`.

**Input.** `FlxG.keys` and `FlxG.mouse` poll MonoGame `Keyboard` and `Mouse` each update. Key names map by name (`LEFT`, `A`, `SPACE`). Gamepad support is a stub in the first slice.

**Audio.** `FlxSound` wraps `SoundEffect` / `SoundEffectInstance`. Looping uses the instance. `FlxG.play(Class)` is a one-shot. `FlxG.playMusic` keeps one looping instance. Volume, mute, pause and fade follow Flixel semantics.

**Text.** `FlxText` renders with a `SpriteFont` asset. Alignment, color, shadow, size scaling approximate TextField. Not pixel-identical, and the migration summary says so.

**Save.** `FlxSave` stores a JSON file per game name under `%AppData%/XnaRebornFlixel/<game>/`. Values go through `AsObject`.

**Assets.** `AsClass` has two forms: a `Type`, and an asset handle (`AssetRef`: content name plus kind). `FlxSprite.loadGraphic(AsClass)`, `FlxTilemap.loadMap(object mapData, AsClass tiles, ...)`, `FlxSound.loadEmbedded(AsClass)` accept it. `new MapCSV()` on a data handle returns an `AsByteArray` whose `ToString()` decodes UTF-8, which matches how Flixel games read embedded CSV maps.

**Port method.** Each runtime class is ported by hand from the matching AS3 file, in dependency order, keeping the MIT notice at the top. A unit-test file per class mirrors Flixel's behavior on numbers that can be computed by hand (collision separation, quad tree overlap, timer ticks, group recycle).

## Rule catalog (proposed: FL001 to FL060)

Catalog file: `rules/flash-to-monogame.json`. Severity and classification use the existing enums. New match types: `as3Import` (package pattern), `as3Syntax` (named construct), `as3Metadata` (tag and attribute), `as3Api` (type and member, with an "unknown member of a known Flixel type" mode). Existing `filePattern` is used for project-level rules.

| Id | Title | Severity | Class | Match |
|---|---|---|---|---|
| FL001 | Flixel 2.x library in use | Info | AutoFixable | `as3Import` `org.flixel.*` |
| FL002 | Flixel version is not 2.x | Warning | Manual | `as3Api` / version probe |
| FL003 | Compiled `.swf` without source | Blocker | Unsupported | `filePattern` `**/*.swf` and no `.as` |
| FL004 | Flash Pro timeline project (`.fla`, `.xfl`) | Blocker | Unsupported | `filePattern` |
| FL005 | Other `.swc` library dependency | Error | Manual | `filePattern` `**/*.swc` |
| FL010 | Embedded image | Info | AutoFixable | `as3Metadata` Embed image |
| FL011 | Embedded audio | Info | AutoFixable | `as3Metadata` Embed mp3/wav |
| FL012 | Embedded data file | Info | AutoFixable | `as3Metadata` Embed other |
| FL013 | Embedded font | Warning | AutoFixable | `as3Metadata` Embed font |
| FL014 | Embed from SWF symbol | Error | Unsupported | `as3Metadata` `symbol=` or `.swf` |
| FL015 | Embed source file missing | Error | Manual | `as3Metadata` + file check |
| FL020 | `with` statement | Error | Manual | `as3Syntax` with |
| FL021 | E4X XML expressions | Error | Manual | `as3Syntax` e4x |
| FL022 | `arguments` object | Warning | Manual | `as3Syntax` arguments |
| FL023 | Untyped variable or `*` type | Warning | AutoFixable | `as3Syntax` untyped |
| FL024 | Dynamic class or runtime property add | Warning | Manual | `as3Syntax` dynamicClass |
| FL025 | Namespace declaration or `use namespace` | Error | Manual | `as3Syntax` namespace |
| FL026 | Function values and closures | Info | AutoFixable | `as3Syntax` closure |
| FL027 | Integer division changes meaning | Info | AutoFixable | `as3Syntax` intDivision |
| FL028 | Number default is NaN | Info | AutoFixable | `as3Syntax` numberDefault |
| FL029 | `Object` used as a property bag | Warning | AutoFixable | `as3Syntax` objectBag |
| FL030 | Regular expression literal | Warning | Manual | `as3Syntax` regex |
| FL031 | Reflection (`getDefinitionByName`, `ApplicationDomain`) | Error | Manual | `as3Api` |
| FL040 | `flash.display` used outside Flixel | Error | Manual | `as3Import` `flash.display.*` |
| FL041 | `flash.events` custom events | Warning | AutoFixable | `as3Import` `flash.events.*` (stretch) |
| FL042 | `flash.media` used directly | Warning | Manual | `as3Import` |
| FL043 | `flash.net` network or `SharedObject` used directly | Error | Manual | `as3Import` |
| FL044 | `flash.external`, `flash.system.Security` | Error | Unsupported | `as3Import` |
| FL045 | Filters, blend modes, `BitmapData.draw` of display objects | Error | Unsupported | `as3Api` |
| FL046 | Ad, sponsor or score SDK | Warning | Manual | `as3Import` (`com.mochibot.*`, `mochi.*`, `com.newgrounds.*`, `com.kongregate.*`, `com.cpmstar.*`) |
| FL047 | Preloader (`[Frame(factoryClass)]`) | Info | AutoFixable | `as3Metadata` Frame |
| FL050 | Flixel debugger settings | Info | AutoFixable | `as3Api` `forceDebugger`, `FlxG.debug` |
| FL051 | Flixel pixel access (`pixels`, `getPixel`, `stamp`) | Warning | Manual (stretch: AutoFixable) | `as3Api` |
| FL052 | Flixel `stage` access | Error | Manual | `as3Api` `FlxG.stage` |
| FL053 | Flixel plugins or replay | Warning | Manual | `as3Api` |
| FL055 | Flixel member not in the runtime | Error | Manual | `as3Api` unknown-member mode |
| FL060 | No or several `FlxGame` entry classes | Error | Manual | project probe |

Gaps in numbering are left on purpose for later rules.

## Safety design

- Original project: opened read-only (`IReadOnlyFileSystem` from the Analyzer is reused). Hash tests as for XNA.
- Output: only through `OutputGuard`. No write outside the staging folder.
- `.swf` files: never opened. FL003 is decided from file names and extensions alone.
- `[Embed]` paths: resolved relative to the declaring file, normalized, and refused when they leave the project root or are absolute.
- No network. No upload. No AI repair in this slice.
- Third-party licenses: runtime derived from Flixel MIT code carries the notice. Reference projects without a license are not copied.

## Error handling

- A parse failure is a diagnostic. The file is skipped by the converter and listed in the manifest as not converted.
- A type the resolver cannot decide becomes `unknown`, and a marker is written only when the output could change meaning.
- A missing asset is FL015 and the build will fail with a clear hint.
- Cancellation stops at file boundaries. The staging folder is discarded.

## Testing strategy

- Lexer and parser: table tests plus Verify snapshots of the syntax tree for each fixture. Negative tests for each recovery path.
- Rules: one positive and one negative fixture per rule id. Report snapshots reviewed in the PR.
- Converter: one golden C# output per fixture (Verify). Each golden file is also compiled with Roslyn against the runtime sources (compile check, no run).
- Language semantics: a small table of AS3 snippets with expected C# for each row of the language table (NaN default, int division, `>>>`, casts, `super` placement).
- Runtime: xUnit tests on graphics-free logic.
- End to end (integration, skipped without the .NET SDK): convert the platformer fixture, build, launch the test host headless for a few frames, exit code 0.
- Safety: hash tests on source folders after analysis and after conversion. A test that `.swf` bytes are never read (the file system fake throws on read).
- Local-only checkpoint: parse (and later convert) the three candidate games when `XNAREBORN_FLASH_SAMPLES` is set.

## Demo path (minimum slice)

1. Open the `flx-platformer` fixture (a small game written from scratch: menu state, play state, player, enemies, bullets, tilemap, one png and one mp3 or wav).
2. Analyze: readiness score and FL findings (embeds, closures, int division).
3. Migrate: converted C# files, `Content.mgcb`, runtime project, manifest.
4. Build and run: window opens at 640 by 480 (320 by 240 at zoom 2), player moves, enemies chase, tile collision works.
5. Show markers remaining (a few FL0xx) and the score after.

Stretch: convert Mode from a local checkout.

## Risks

- **Scope widening.** XNA Reborn is "XNA to MonoGame". This spec adds a second product line (Flash). It adds three projects and a second parser. Mitigation: keep it in sibling projects, label it "Flixel 2.x source projects", and cut it first if time runs short. It does not change XNA code paths, except two optional fields on shared models.
- **Deadline.** Feature freeze is 24 Nov 2026, 13 days from today. The full slice is too big. The minimum slice (tasks marked MVP) is sized to land first. Tilemaps, emitters and the Mode checkpoint are second wave.
- **Behavior drift.** `Number` as `double`, NaN defaults, text rendering and audio looping may differ from Flash. The migration summary lists the known ones.
- **Parser coverage.** Real games use odd syntax. The three candidate games plus fixtures are the test set. Unknown syntax becomes a marker, not a crash.
- **Runtime size.** Flixel is about 20 core classes. A hand port is days of work. The task list splits it into worker-sized pieces and orders them by what the demo needs.
- **Audio toolchain.** MP3 handling in `dotnet-mgcb` may need `ffmpeg`. Not verified yet (task 12.3).
- **Licensing.** Candidate game assets may not be redistributable. They stay outside the repo. Unlicensed Flixel ports are reference only.
- **Naming rules.** The runtime keeps AS3 member names. Repo analyzers and `dotnet format` may flag them. A folder-scoped `.editorconfig` handles it (task 1.3 proves it).
- **Dynamic code.** `dynamic` needs the C# runtime binder. It ships with .NET 8, but dynamic call sites are slower. Only untyped code uses it.

## Implementation notes (gaps and deviations)

- PR 1 (reader): `ProjectKind` is in Core. The optional `SourceKind` on the shared report and manifest models (task 1.2) is deferred to the converter PR, so shared snapshots stay untouched until then.
- `MonoGame.Framework.DesktopGL 3.8.6.1` was added to `Directory.Packages.props` because `XnaReborn.Flixel` must reference MonoGame. It is the version already pinned in `packages.json`.
- Parser: tests are plain xUnit assertions and a round trip of every fixture. Verify tree snapshots (task 4.5) are not done yet. A custom namespace modifier (such as `mx_internal`) is skipped without a record. The E4X filter body `.( ... )` is skipped as balanced tokens, not parsed.
- Existing fixture tests that list the folders of `tests/fixtures` now ignore the `flash` folder, which has its own fixture tests.
- PR 3 (runtime): `XnaReborn.Flixel` holds the AS3 runtime (`AsArray`, `AsVector<T>`, `AsObject`, `AsDictionary`, `AsFunction`, `AsClass`, `AssetRef`, `AsOps`, `AsGlobals`, `AsCast`) and a Flixel 2.x core written against the public API (not copied from any source): `FlxBasic`, `FlxGroup`, `FlxState`, `FlxObject`, `FlxU` (subset), `FlxSprite`, `FlxTileblock`, `FlxText`, `FlxG`, `FlxKeyboard`, `FlxGame`. Done: 7.1 to 7.4, 8.2, 8.5 (tests use `FlxTestHost`).
- Partial or skipped, by design for the demo slice: no `FlxQuadTree` (collide and overlap test every pair, a shortcut noted in code); no `FlxRect`, `FlxPath`, `FlxTimer`, `FlxCamera` (one fixed view, no scroll, flash, fade or shake), `FlxButton`, `FlxSound` beyond a play-once record, gamepad stub. So 8.1, 8.3, 8.4, 9.1 to 9.6 stay open.
- Text uses a built-in 5 by 7 pixel font instead of a `SpriteFont` (task 9.3 said SpriteFont). It needs no font file or content build step. Lower case draws as capitals; glyphs were not checked by eye.
- `FlxGame` could not be run here (no window). Its drawing and audio paths are unverified; a headless `FlxTestHost` covers the logic through `IFlxRenderer`, `IFlxAssets` and `IFlxKeyState`.
- The project does not generate XML docs (hundreds of AS3-named members); types carry summaries.
