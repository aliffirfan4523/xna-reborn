# Design: XACT Audio and .fx Shader Conversion (C# / .NET 8)

## Overview

Two features, one spec, because both finish what `content-conversion` leaves as manual work (R6 XACT, R7 effects).

- **Part A, XACT.** Parse the `.xap` text file, build every wave as a normal `SoundEffect` or `Song` asset, and generate a small compatibility layer so `AudioEngine`/`WaveBank`/`SoundBank`/`Cue` code keeps working.
- **Part B, .fx.** A small text scanner and rewriter fixes the safe, common differences (shader profiles, two sampler state names) and marks the rest. The runner then compiles each effect with `mgfxc` before the full build, retries with a higher profile on limit errors, and attaches hints.

Everything is data first. New facts (profile mapping, hints, unsupported states, XACT feature flags) live in JSON; code is added only for the scanner, the `.xap` reader, the shim and the one new match type.

## Decision for Part A: convert the `.xap`, copy compiled banks only as a fallback

Three approaches were compared.

| | A. Compile with XACT, use MonoGame runtime | B. Keep compiled banks, use MonoGame runtime | C. Convert the `.xap` (chosen) |
|---|---|---|---|
| Needs XACT or XNA tools | Yes. `xactbld` ships only with XNA Game Studio, Windows only | No | No |
| Works when only the `.xap` and wavs are in the repo | No, nothing compiles it | No | Yes |
| Works when only compiled banks are in the repo | n/a | Yes, within limits | No (nothing to read), falls back to B |
| Streaming wave banks | Throw at run time | Throw at run time | Converted to `Song` |
| Codecs | PCM and ADPCM only | PCM and ADPCM only | Anything ffmpeg reads, built to PCM |
| Format version risk | `.xgs` 42, `.xsb` 43 only (others print a warning) | Same | None, text input |
| Errors | At run time | At run time | At content build, with the asset path |
| Cost | Cannot be automated | Copy only | Parser, generator, shim |

Why C:
1. MonoGame has no `.xap` importer or processor. Its pipeline folder has audio importers and processors but nothing for XACT (checked in the v3.8.6.1 tree). The MonoGame forum answer is the same: compile the `.xap` in XACT first ([community.monogame.net/t/xact-xgs-format-not-recognized/2675](https://community.monogame.net/t/xact-xgs-format-not-recognized/2675)).
2. The MonoGame runtime cannot stream XACT wave banks on desktop. `WaveBank.Default.cs` throws `NotImplementedException("XACT streaming is not implemented on this platform.")`, and it is the only platform file.
3. The runtime only decodes PCM and ADPCM. Any other codec throws `NotSupportedException("Unsupported sound format!")` in both `SoundEffect.OpenAL.cs` and `SoundEffect.XAudio.cs`.
4. Many repositories do not commit build outputs. In the analyzed corpus, `Space-Shooter-Game` calls `new AudioEngine("Content\\game_sound.xgs")` and the repository has no `.xap` or `.xgs`. Of the four sample `.xap` repositories checked for compiled banks, only one (`XNAUnleashed`) also had them.
5. The XACT features real games use are small. In seven real `.xap` files (111 waves, 109 cues) every sound had one track except one sound in one file, every cue had one sound entry except in one file, and the variation type was always 3 (random without immediate repeat). A shim that covers volume, pitch, loops, variations, categories and a few RPC curves covers them.

Why not drop B: if a project has compiled banks and no `.xap`, copying them and letting the MonoGame runtime load them is the best that can be done. This is the behavior `XNA015` already describes. It is kept as `Auto` mode's fallback and as `CopyOnly`.

## MonoGame XACT runtime (what exists today)

Findings from the MonoGame v3.8.6.1 source (read, not executed; no XACT banks were available to run):

- `MonoGame.Framework/Audio/Xact/` has `AudioEngine`, `WaveBank`, `SoundBank`, `Cue`, `AudioCategory`, `XactSound`, `XactClip`, `RpcCurve`. They compile for DesktopGL and WindowsDX (the same code; only `WaveBank.Default.cs` and the `SoundEffect` platform files differ).
- `AudioEngine(string)` reads the `.xgs` via `TitleContainer`; magic `0x46534758`; warns (not throws) when the format version is not 42. `ContentVersion` is 39.
- `SoundBank` expects magic `SDBK` and format 43 (warns otherwise).
- `WaveBank` handles wave bank versions 1 to 3 and 42 or newer. Non-streaming banks load all sounds at construction; streaming banks call `PlatformCreateStream`, which throws on desktop.
- Codecs: `SoundEffect(MiniFormatTag codec, ...)` handles `Pcm` in shared code and `Adpcm` in the platform code. Anything else (WMA, XMA) throws.
- `Cue.IsStopping`, `IsPreparing`, `IsCreated` are documented as always false.
- The unit test (`Tests/Framework/Audio/XactTest.cs`) uses precompiled `Tests.xgs`, `Tests.xwb`, `Tests.xsb` assets. There is no test that builds from a `.xap`.

Conclusion: MonoGame 3.8.6.1 can play compiled, non-streaming, PCM or ADPCM XACT banks on both targets. It cannot build XACT projects. The XNA015 text already says this; the new rule `NEW-A1` and the `Auto` fallback make it a Warning with the exact limits.

## `.xap` format (read from real files)

Read-only, from real files in permissive repositories (see candidates). The format is plain text, nested blocks written as a name line, `{`, `key = value;` lines, child blocks, `}`.

```
Signature = XACT2;
Version = 16;
Content Version = 43;
Release = August 2007;
Options { Verbose Report = 0; Generate C/C++ Headers = 1; }
Global Settings {
    Windows File = Win\Sounds.xgs;
    Category { Name = Music; Public = 1; Background Music = 1; Volume = 0;
        Category Entry { Name = Global; }
        Instance Limit { Max Instances = 255; Behavior = 0;
            Crossfade { Fade In = 0; Fade Out = 0; Crossfade Type = 0; } } }
    Variable { Name = Distance; Public = 1; Global = 0; Internal = 0; External = 0; ...
               Initial Value = 0.000000; Min = 0.000000; Max = 1000000.000000; }
    RPC { Name = BackgroundMusic;
        RPC Curve { Name = Sound::<guid>; Property = 0; Sound = 1;
            Variable Entry { Name = Volume; }
            RPC Point { X = 4.85; Y = -9600.0; Curve = 0; } } }
}
Wave Bank { Name = Wave Bank; Windows File = Win\Wave Bank.xwb; Streaming = 1; Seek Tables = 1;
    Wave { Name = hit; File = hit.wav;
        Cache { Format Tag = 0; Channels = 2; Sampling Rate = 44100; Bits Per Sample = 1;
                Play Region Offset = 44; Play Region Length = 173568;
                Loop Region Offset = 0; Loop Region Length = 0; File Type = 1; } } }
Sound Bank { Name = Sound Bank; Windows File = Win\Sound Bank.xsb;
    Sound { Name = hit; Volume = -1200; Pitch = 0; Priority = 0;
        Category Entry { Name = Default; }
        Track { Volume = 0;
            Play Wave Event { Loop Count = 255; Break Loop = 0;
                Event Header { Timestamp = 0; Relative = 0; Random Recurrence = 0; Random Offset = 0; }
                Wave Entry { Bank Name = Wave Bank; Entry Name = hit; Entry Index = 7; Weight = 255; Weight Min = 0; } } } }
    Cue { Name = Hit;
        Variation { Variation Type = 3; Variation Table Type = 1; New Variation on Loop = 0; }
        Sound Entry { Name = hit; Index = 5; Weight Min = 0; Weight Max = 255; } } }
```

Observations used by the design (counts are from the seven files read: Catapult, Dandy Dungeon, Movipa, Pickture, RobotGame, VectorRumble and MonoGame's own `Tests.xap`; `Tut02_Stream` was read only for the streaming key):

- Units. `Volume` values are hundredths of a decibel (`-1200` = -12 dB; positive values such as `203` occur). `Pitch` is expected to be hundredths of a semitone (all observed values were 0); the range -1200 to 1200 maps to the XNA `Pitch` range -1 to 1. Task 1 records the answer from the archived XACT documentation. MonoGame's own runtime divides the compiled pitch value by 1000, not 1200; every pitch seen in the real files is 0, so the risk is small.
- `Loop Count = 255` appears on 19 play-wave events (music cues) and is treated as infinite. `Loop Count = 2` appears in the SDK streaming sample; it is treated as "play, then repeat 2 more times". Task 1 confirms both from the documentation and from MonoGame's `Tests.xap`.
- Streaming is a wave bank key: `Streaming = 1;` (found in `Tut02_Stream.xap`; absent in the other files).
- Category `Instance Limit` has `Behavior` values 0, 1, 2 and 4 in the corpus. The names (fail to play, queue, replace oldest, replace quietest, replace lowest priority) follow the XACT documentation and are confirmed from the documentation in task 1.
- `Variation Type = 3` in all 109 cues that have a variation block; `Variation Table Type = 1` in every one of the 106 cues outside `Tests.xap` (not counted there). The other types follow the order of the XACT UI (0 ordered, 1 ordered from random, 2 random, 3 random without immediate repeat, 4 shuffle); the same order is used by MonoGame's `SoundBank` table flags. Task 1 confirms from the documentation.
- Waves point to files relative to the `.xap` folder. The `Windows File` of the global settings, wave bank and sound bank gives the stems the game uses at run time: `Win\Sounds.xgs` becomes `Content\Sounds.xgs` in game code (observed in `XNAUnleashed`: the `.xap` says `Win\Chapter7.xgs`, `Win\Wave Bank.xwb`, `Win\Sound Bank.xsb`, and the compiled files sit beside the `.xap`).
- `Bits Per Sample` is a flag (0 or 1), not a bit count. The reader keeps the flag and does not use it for sizes.
- Features seen only in some files: RPC curves (`Movipa`: 1 RPC with 2 curves, `RobotGame`: 3 RPCs, 44 RPC entries), compression presets (`Movipa`, `RobotGame`, `VectorRumble`), multi-track sounds (`VectorRumble`: 18 events in 17 sounds). Not seen in any file: DSP effect presets with reverb settings beyond defaults, filters, stop or volume events. Those keys are collected in the unsupported list if they appear; task 1 adds a fixture block for each key the XACT documentation names.

Unknown keys never stop parsing (R1.4).

## Architecture

```
src/XnaReborn.Core/Models/Xact/        XapProject and friends (immutable records)
src/XnaReborn.Analyzer/Xact/           XapReader (text in, XapProject + findings out)
src/XnaReborn.Analyzer/Matching/       FileContentMatcher (new match type fileContent)
src/XnaReborn.Migrator/Audio/          XactPlanner, XactAssetBuilder, XactCatalogWriter, XactReport
src/XnaReborn.Migrator/Shims/          XactShimSources.cs (shim C# as text), Scan() extension
src/XnaReborn.Migrator/Source/Rewriters/XactAliasRewriter.cs
src/XnaReborn.Migrator/Effects/        FxScanner, FxDocument, FxProfileRewriter, FxSamplerRewriter,
                                       FxMarkers, FxIncludeCollector, EffectRewriteStep
src/XnaReborn.Runner/Effects/          EffectBuildCheck, MgfxcOutputParser, ProfileEscalator
src/XnaReborn.Runner/Hints/hints.json  new hint entries
rules/                                 xna-to-monogame.json (changes in the rule table), effect-profiles.json (new data)
tests/fixtures/                        xna40-xact, xna40-xact-compiled, xna40-effects
```

UI code gets the data through the existing `ContentReport` and `BuildResult`; no migration logic goes into `XnaReborn.App`.

## Part A design

### Models (Core)

```csharp
public sealed record XapProject(
    string Path, string? Signature, int? Version, int? ContentVersion,
    string? EngineFileStem,                          // stem of Global Settings "Windows File"
    IReadOnlyList<XapCategory> Categories,
    IReadOnlyList<XapVariable> Variables,
    IReadOnlyList<XapRpc> Rpcs,
    IReadOnlyList<XapWaveBank> WaveBanks,
    IReadOnlyList<XapSoundBank> SoundBanks,
    IReadOnlyList<XapUnsupported> Unsupported);

public sealed record XapCategory(string Name, bool IsPublic, bool BackgroundMusic, int VolumeCentiDb,
    int MaxInstances, int Behavior, int FadeInMs, int FadeOutMs, int CrossfadeType, IReadOnlyList<string> Parents);
public sealed record XapVariable(string Name, bool IsGlobal, bool IsPublic, bool ReadOnly, bool IsInternal,
    float InitialValue, float Min, float Max);
public sealed record XapRpcCurve(string Name, int Property, bool IsSound, string VariableName, IReadOnlyList<XapRpcPoint> Points);
public sealed record XapRpcPoint(float X, float Y, int CurveShape);
public sealed record XapRpc(string Name, IReadOnlyList<XapRpcCurve> Curves);
public sealed record XapWave(string Name, string File, int Channels, int SamplingRate,
    long PlayRegionLength, long LoopRegionOffset, long LoopRegionLength, string? CompressionPreset);
public sealed record XapWaveBank(string Name, string? FileStem, bool Streaming, IReadOnlyList<XapWave> Waves);
public sealed record XapWaveEntry(string BankName, string EntryName, int Weight);
public sealed record XapPlayWaveEvent(int LoopCount, bool BreakLoop, int TimestampMs, bool Relative,
    IReadOnlyList<XapWaveEntry> Entries);
public sealed record XapTrack(int VolumeCentiDb, IReadOnlyList<XapPlayWaveEvent> Events);
public sealed record XapSound(string Name, int VolumeCentiDb, int PitchCents, int Priority,
    IReadOnlyList<string> Categories, IReadOnlyList<string> RpcNames, IReadOnlyList<XapTrack> Tracks);
public sealed record XapSoundEntry(string SoundName, int WeightMin, int WeightMax);
public sealed record XapCue(string Name, int VariationType, int VariationTableType, bool NewVariationOnLoop,
    IReadOnlyList<XapSoundEntry> Entries);
public sealed record XapSoundBank(string Name, string? FileStem, IReadOnlyList<XapSound> Sounds, IReadOnlyList<XapCue> Cues);
public sealed record XapUnsupported(int Line, string Path, string Reason);
```

All records are documented with XML comments (tech rule).

### Reader

`XapReader.ReadAsync(path, fileSystem, ct)` uses the analyzer's `IReadOnlyFileSystem`.

1. Read text; strip BOM.
2. Header lines before the first block: `key = value;`.
3. A block starts with a line `Name` (letters, digits, spaces) followed by `{` on the next line. A property is `Key = Value;` split on the first ` = `, trailing `;` removed, value trimmed. This yields a generic `XapBlock(name, properties, children, line)` tree.
4. A mapper turns the tree into the typed records. Properties it reads are listed in a table in code; every other property or child block adds an `XapUnsupported` entry only when it is on the "semantic" list (below) and is silently kept otherwise. This keeps noise down: layout keys like `Last Modified Low` never produce notes.
5. Errors (unbalanced braces, a property outside a block) return a finding with the line and stop mapping that `.xap`.

Semantic list for unsupported notes (data, in `rules/xact-features.json`): `DSP`, `Reverb`, `Filter`, `Stop Event`, `Volume Event`, `Pitch Event`, `Marker Event`, `Set Variable Event`, `Transition`, `Interactive`, `Occlusion`, `Obstruction`. These names come from the XACT documentation and are checked against fixtures in task 1. Names not seen in any real file are written as "expected, unverified" in the JSON `status` field so the report can say so.

### Planner and content entries

`XactPlanner.Plan(project, options, contentRoot)` returns:

- `WaveAsset(AssetName, SourceRelativePath, Kind, Note)` for each distinct file: asset name `xact/<engine stem>/<bank>/<wave>` (sanitised, `/` separators). If `engine stem` is null, the `.xap` file stem is used.
- Findings: missing wave files, unsupported items, compression presets.

`Kind` is `SoundEffect` by default. It is `Song` when the wave bank is streaming and the option `StreamingWavesAsSong` is on.

`.mgcb` entries (no new `MgcbWriter` code path; the entries are ordinary `ConvertedAsset` records that the existing writer already formats):

```
#begin xact/Sounds/Wave Bank/hit.wav
/importer:WavImporter
/processor:SoundEffectProcessor
/processorParam:Quality=Best
/build:Sounds/hit.wav;xact/Sounds/Wave Bank/hit
```

For a `Song`: `/processor:SongProcessor`, `/processorParam:Quality=Best`. `SongProcessor` converts to Vorbis on DesktopGL and to Windows Media on WindowsDX (`DefaultAudioProfile.ConvertStreamingAudio`). Both go through `ffmpeg`. `DefaultAudioProfile.ProbeFormat` calls `ffprobe`, so even wave files need both tools. This dependency is new to the project (no earlier spec mentions it) and is part of R7.3.

Open point for task 6: confirm `MgcbWriter.FindProblems` and the importer map accept `WavImporter` + `SongProcessor`. The generated entries do not come from the XNA importer map; if `FindProblems` rejects the pair, the planner passes an `allowedPairs` list.

### Catalog (generated C#)

`XactCatalogWriter` writes `XnaReborn.Compat/XactCatalog.g.cs`. It is static data, no JSON parsing at run time, and it is covered by Verify snapshots.

```csharp
// Generated by XNA Reborn. Do not edit.
namespace XnaReborn.Compat.Xact
{
    internal static partial class XactCatalog
    {
        internal const string ContentRoot = "Content";

        internal static readonly XactEngineData[] Engines =
        {
            new XactEngineData("Sounds",
                categories: new[] { new XactCategoryData("Music", 1.0f, true, 255, XactInstanceBehavior.FailToPlay, ...) },
                variables: new[] { new XactVariableData("Distance", false, 0f, 0f, 1000000f) },
                rpcs: ...),
        };

        internal static readonly XactWaveBankData[] WaveBanks = { ... "Wave Bank", "Wave Bank", streaming: false,
            waves: new[] { new XactWaveData("hit", "xact/Sounds/Wave Bank/hit", XactWaveKind.SoundEffect) } };

        internal static readonly XactSoundBankData[] SoundBanks = { ... cues, sounds, tracks, events ... };
    }
}
```

Unit conversion happens in the writer: `gain = 10^(centiDb / 2000)`, `pitch = clamp(cents / 1200, -1, 1)`. Floats are written with `R` formatting and an `f` suffix, using invariant culture, so the output is stable.

If a project has more than one `.xap`, the three arrays hold all of them, keyed by file stem. A stem used twice reports a conflict finding (as asset names do in R2.3 of content conversion).

### Shim (`XnaReborn.Compat.Xact`)

Text in `Shims/XactShimSources.cs`, written the same way as `Storage` and `GamerServices`. It references the MonoGame package already referenced by the shim project.

Why a new namespace and aliases, not the XNA namespace: MonoGame already defines these types in `Microsoft.Xna.Framework.Audio`. A second type with the same name in another assembly causes ambiguity errors (CS0433) in game code. A using alias takes priority over names imported by using-namespace directives, so one alias line per used type moves the game to the shim without touching the rest of the file.

Classes and behavior:

| Type | Behavior |
|---|---|
| `AudioEngine(string)`, `AudioEngine(string, TimeSpan, string)` | Looks up the engine by file stem. Creates categories and variables from the catalog. Creates one private `ContentManager(new EmptyServiceProvider(), contentRootFullPath)`. |
| `AudioEngine.Update()` | Advances timers: finite loops, delayed events, cue cleanup, queued instances. Cheap when nothing plays. |
| `AudioEngine.GetCategory/GetGlobalVariable/SetGlobalVariable/Dispose/Disposing/IsDisposed` | As MonoGame. |
| `WaveBank(engine, path)`, `WaveBank(engine, path, offset, packetSize)` | Looks up by stem. Loads all `SoundEffect` waves now (as XNA does for non-streaming banks). `Song` waves load lazily. `IsPrepared` is true after construction. The streaming overload does not throw; it uses the same code. |
| `SoundBank(engine, path)`, `GetCue`, `PlayCue(name)`, `PlayCue(name, listener, emitter)`, `IsInUse`, `Dispose` | Looks up by stem. `PlayCue` creates and plays a cue and lets it clean itself up when finished. |
| `Cue` | Picks a sound by the cue variation; applies volume (sound x track x category x RPC), pitch (sound + RPC); one `SoundEffectInstance` per play-wave event; states from instances. |
| `AudioCategory` | `Stop`, `Pause`, `Resume`, `SetVolume`, `Name`; applies to live cues in the category. |

Playback rules:

- **Variations.** Type 0 ordered, 1 ordered from random start, 2 random, 3 random without immediate repeat, 4 shuffle. Random uses a per-engine `Random` with an optional seed field in the catalog for tests.
- **Loops.** Loop count 255 sets `IsLooped = true`. Finite counts N restart the instance N more times from `Update`. Wave loop regions (`Loop Region Length > 0`) loop the whole wave and add a note, because `SoundEffectInstance` has no loop points. (MonoGame reads loop points from the wave's `smpl` chunk when it builds the asset; the shim relies on that.)
- **Song waves.** Played with `MediaPlayer`. Limits: one song at a time, volume is `MediaPlayer.Volume`, pitch is ignored, loop sets `IsRepeating`. Each limit is a note in the report. If the game also uses `MediaPlayer`, the two compete, which is a Warning in the report.
- **Instance limits.** `Behavior` 0 fail to play, 1 queue, 2 replace oldest, 3 replace quietest, 4 replace lowest priority.
- **3D.** `Apply3D(listener, emitter)` calls `SoundEffectInstance.Apply3D`.
- **Stopping.** `Stop(AsAuthored)` and `Stop(Immediate)` both stop at once. A fade is a note. (`IsStopping` is false, as in MonoGame.)
- **Unsupported items.** The catalog carries a list per cue. The shim plays what it can and never throws for them.

Parity guarantee: a test in the shim project loads the MonoGame assembly by reflection and asserts that every public member of `Microsoft.Xna.Framework.Audio.{AudioEngine,WaveBank,SoundBank,Cue,AudioCategory}` exists on the shim type with the same signature. A member the shim cannot support is present and throws `NotSupportedException` with a message that names `XnaReborn.Compat`. (`AudioEngine.RendererDetails`, if present in 3.8.6.1, is such a member.)

### Source aliasing (`XactAliasRewriter`)

`ISourceRewriter` with `RuleId = "XNA015"`. It parses the file with Roslyn (as other rewriters do) and, for each used shim type name, inserts:

```csharp
using AudioEngine = XnaReborn.Compat.Xact.AudioEngine;
```

- Type-name uses are `IdentifierName` or `QualifiedName` nodes in type positions whose name is one of the five types and which are not declared in the file. A file with a type declaration of the same name is left alone with a note.
- The alias goes next to the `using Microsoft.Xna.Framework.Audio;` directive (same compilation unit or namespace block). If no such directive exists (fully qualified use only), the qualified name is replaced.
- Existing aliases to the XNA types are re-pointed.
- Text edits are position-based so the rest of the file is byte identical, like the other rewriters.
- `ShimProjectGenerator.Scan` gains the `Microsoft.Xna.Framework.Audio` namespace with these five type names so the project reference is added only when needed (R6.5).

### Integration with content conversion

`ContentConverter` step 3 (classify) changes from "XACT extensions: ManualWork" to:

1. If the mode is `CopyOnly`, or the `.xap` fails to parse, or a compiled bank has no `.xap`: ManualWork + copy, as today.
2. Otherwise: the `.xap` is marked `Converted`, the planner's `WaveAsset`s are added to the asset list, the catalog and shim files are added as `FileChange` records, and compiled banks next to the `.xap` are copied and listed as "ignored".

`ContentReport` gets an `Audio` section (R7.1). `Manifest` lists the generated files.

## Part B design

### What MGFX needs (research)

Primary sources: [Custom Effects](https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html), [MGFXC](https://docs.monogame.net/articles/getting_started/tools/mgfxc.html) and the v3.8.6.1 source of the effect compiler (`Tools/MonoGame.Effect.Compiler`). Everything below is from those; "run" means the probe was executed with `dotnet-mgfxc` 3.8.6.1 on Windows.

**Shader models.**
- DirectX (`DirectX_11` profile, WindowsDX): the regex accepts `vs_1..5_0|1` with an optional `_level_9_1|9_2|9_3`, but validation throws when the major version is 3 or lower: "Invalid profile 'X'. Vertex shader 'F' must be SM 4.0 level 9.1 or higher!" (`ShaderProfile.DirectX11.cs`). Supported: `_4_0_level_9_1`, `_4_0_level_9_3`, `_4_0`, `_4_1`, `_5_0` (the last three need the HiDef graphics profile at run time, per the docs). Run: all six real XNA effects tested failed on DirectX unmodified for this reason only.
- OpenGL (`OpenGL`, DesktopGL): translated to GLSL with MojoShader. Supported: `vs_2_0`, `ps_2_0`, `vs_3_0`, `ps_3_0` (docs); the source rejects major versions above 3 (`ShaderProfile.OpenGL.cs`). Run: all six real effects compiled unmodified.
- Bug to know: for pixel shaders the DirectX error message prints the *vertex* model name (`pass.vsModel`), so a pixel-only pass shows `Invalid profile ''`. The hint must not depend on the name.
- Macros the compiler defines: `HLSL` and `SM4` for DirectX; `OpenGL` and `GLSL` for OpenGL; also `2MGFX` (docs). The source defines `OPENGL` (upper case) for the OpenGL profile. The preprocessor is case sensitive, so the rewriter uses `OPENGL`, the spelling in the source and in the docs example.
- The docs' own pattern is `#if OPENGL / #define VS_SHADERMODEL vs_3_0 / #else / #define VS_SHADERMODEL vs_4_0_level_9_1 / #endif`.

**Grammar** (`MGFX.tpg`, the TinyPG file the compiler is generated from). The parser is a loose parser over HLSL text: it recognises `technique`, `pass`, sampler declarations and `compile` statements and passes everything else through as "code" to the Microsoft HLSL compiler (`D3DCompiler`, with `EnableBackwardsCompatibility` on DirectX, so legacy syntax like `tex2D`, `sampler2D`, `POSITION0`, `COLOR0` and `half` compiles).
- Technique: `technique [Name] { pass ... }`. Annotations (`< ... >`) are not in the grammar. Run: `Unexpected token '<' found. Expected OpenBracket`.
- Pass body accepts only: `VertexShader = compile <model> <fn>();`, `PixelShader = compile <model> <fn>();`, and these states: `CullMode`, `FillMode`, `AlphaBlendEnable`, `SrcBlend`, `DestBlend`, `BlendOp`, `ColorWriteEnable`, `DepthBias`, `SlopeScaleDepthBias`, `ZEnable`, `ZWriteEnable`, `ZFunc`, `MultiSampleAntiAlias`, `ScissorTestEnable`, `StencilEnable`, `StencilFail`, `StencilFunc`, `StencilMask`, `StencilPass`, `StencilRef`, `StencilWriteMask`, `StencilZFail`. Run: `AlphaTestEnable = true;` and `SeparateAlphaBlendEnabled` fail with `Unexpected token 'A' found. Expected CloseBracket`. `SetVertexShader(CompileShader(...))` fails the same way. A call with arguments (`compile ps_2_0 F(true)`) fails with `Expected CloseParenthesis`. Run: the render states `AlphaBlendEnable`, `SrcBlend`, `DestBlend`, `ZEnable`, `CullMode` all pass.
- The model after `compile` is any identifier, so a macro works (it is expanded by the preprocessor first).
- Samplers: `sampler | sampler1D | sampler2D | sampler3D | samplerCUBE | SamplerState Name [: register(...)]* [= sampler_state] { ... };`. Inside: `Texture = <T>;` or `(T)`, `MinFilter`, `MagFilter`, `MipFilter`, `Filter` (values `None|Linear|Point|Anisotropic`), `AddressU/V/W` (`Clamp|Wrap|Mirror|Border`), `BorderColor` (hex `0xRRGGBB[AA]`), `MaxMipLevel|MaxLod`, `MaxAnisotropy`, `MipLodBias`. Run: `MipMapLodBias` fails; renaming to `MipLodBias` compiles on both profiles. Run: `MagFilter = GaussianQuad` fails; `Linear` compiles on both.
- `#include "x"` resolves relative to the including file; `#line` and `#pragma` pass through (`Preprocessor.cs`).
- The sampler register `register(s0)` and `register(vs, c0)` forms parse. Run: `GPUDust` effects use both and compile.

**Documented limits** (docs page, "Effect Writing Tips"): pixel shader inputs must match vertex shader outputs in size and order; default values on parameters do not work on GL targets; the compiler removes unused parameters; preshaders are not supported.

**Other probes (run, both profiles):**
- `half4` return and locals compile. The HLSL docs say Direct3D 10 targets map `half` to `float` ([HLSL scalar types](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-scalar)). No rewrite, no marker.
- `texture Tex;` with `Texture = <Tex>` compiles on both.
- `VPOS` input fails: DirectX `X4576`, OpenGL `X4502 invalid ps_2_0 input semantic 'VPOS'`.
- A parameter with a default value and an unused parameter both compile (the effects of these show at run time, not at build time).
- Error formats. Parse errors: `path,(line,col) : Unexpected token 'X' found. Expected ...`. HLSL errors: `path(line,col[,line,col]): error Xnnnn: text`. Profile errors have no location.

**Real effects tested (6 files from 3 permissive projects; not copied into the repo):**

| File | Unmodified, OpenGL | Unmodified, DirectX | After profile macro, DirectX |
|---|---|---|---|
| EvoNet `VertexColor.fx` (vs_2_0, ps_2_0) | pass | fail, profile | pass at `_4_0_level_9_1` |
| EvoNet `WaterEffect.fx` (`#define PS_SHADERMODEL ps_2_0`) | pass | fail, profile | pass |
| EvoNet `GrassDisplay.fx` (same style, three samplers) | pass | fail, profile | pass |
| Grey-Infection `GreyScale.fx` (ps_2_0) | pass | fail, profile | pass |
| XNA-GPUDust `FluidPhase.fx` (vs_3_0, ps_3_0) | pass | fail, profile | fail at `_4_0_level_9_1` (X5608, 72 slots over a 64 limit); pass at `_4_0_level_9_3` and `_4_0` |
| XNA-GPUDust `ColorPhase.fx` (vs_3_0, ps_3_0) | pass | fail, profile | fail at `_4_0_level_9_1` (X4505); pass at `_4_0_level_9_3` and `_4_0` |

So the profile rewrite alone fixed four of six, and the escalation ladder fixed the other two. Shader model 3 effects must start at `_4_0_level_9_3`. What this does not prove: that the output renders the same. That needs a person to look (checkpoint).

### Profile mapping (data: `rules/effect-profiles.json`)

| Original | OpenGL branch | DirectX branch | Needs HiDef |
|---|---|---|---|
| `vs_1_1`, `vs_2_0` | same | `vs_4_0_level_9_1` | no |
| `ps_1_1` to `ps_1_4`, `ps_2_0` | same | `ps_4_0_level_9_1` | no |
| `vs_2_a`, `ps_2_a`, `ps_2_b` | `vs_3_0` / `ps_3_0` | `_4_0_level_9_3` | no |
| `vs_3_0`, `ps_3_0` | same | `_4_0_level_9_3` | no |
| Escalation step after `_9_3` | n/a | `vs_4_0`, `ps_4_0` | yes |

The `_2_a`/`_2_b` GL mapping is a guess to be confirmed in task 12 (the GL regex does not parse the `a`/`b` suffix, so validation passes, but MojoShader support is unchecked). Anything not in the table is left alone and gets a marker.

### Scanner (`FxScanner`)

A tokenizer, not a parser. `shortcut: not a full HLSL parser; top-level shapes only, upgrade if marker false positives show up in the candidate runs.`

1. Tokenize: line and block comments (kept as ranges, never matched), preprocessor lines (one token each, with `\` continuation), identifiers, numbers, strings, punctuation.
2. Split the top level by brace depth into declarations: `technique`, `sampler*`, `struct`, `cbuffer`, functions (identifier, `(`, ..., `)` then `{`), global variables (`type name [: semantic] [= expr] ;`).
3. Inside techniques, read passes and their `key = value;` statements. `compile` statements keep the token range of the model so the rewriter can replace exactly that range.
4. Track `#if/#ifdef/#else/#endif` depth so a `#define` is known to be conditional or not.
5. Parameter usage: a global is "used" when its name appears as an identifier token inside any function body.

`FxDocument` holds the lists, each element with `(Line, Start, Length)`. All edits are position-based against the original text, applied from the end of the file to the start so earlier positions stay valid.

### Rewriters

All are pure functions `string -> FxRewriteResult(text, notes)`, one class per rule, in a fixed order: includes, profile, sampler, markers. Each is idempotent.

`FxProfileRewriter` (rule `NEW-B1`):
1. Collect `compile` statements. If every model is already a macro that is defined inside an `#if OPENGL`/`#if SM4` block, report "already portable" and stop.
2. Read distinct models. Map each with `effect-profiles.json`. If a model is not in the table, leave that statement and add a marker.
3. If there is exactly one vertex model and one pixel model, the macros are `VS_SHADERMODEL` and `PS_SHADERMODEL`; otherwise `XR_VS_2_0`, `XR_PS_3_0` and so on, one per model.
4. Replace each model token with its macro. Remove an unconditional `#define VS_SHADERMODEL <model>` line if it exists (same for `PS_`), and put the block there; otherwise put it before the first non-comment, non-`#include` line.
5. The block:

```hlsl
// XNA Reborn: shader models set per target (OpenGL keeps the original; DirectX needs Shader Model 4 level 9).
#if OPENGL
    #define VS_SHADERMODEL vs_2_0
    #define PS_SHADERMODEL ps_2_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif
```

`FxSamplerRewriter` (rule `NEW-B5`): renames `MipMapLodBias`; replaces `GaussianQuad`/`PyramidalQuad`; marks unknown sampler states (R10). Only inside `sampler_state { }` ranges.

`FxMarkers` (rules `NEW-B2`, `NEW-B3`, `NEW-B4`, `NEW-B6`, `NEW-B7`): the unsupported pass state list, annotations, `SetVertexShader`/`SetPixelShader`, arguments in `compile`, `VPOS`, default values and unused parameters. The pass-state allow list is data (`rules/effect-profiles.json`, key `passStates`) taken from the grammar above; any other `Identifier = ...;` inside a pass is marked. A marker is:

```hlsl
// XNA Reborn [NEW-B2]: AlphaTestEnable is not supported by the MonoGame effect compiler. Set alpha testing in C# (AlphaTestEffect) or discard in the pixel shader.
```

(`[NEW-B2]` becomes the real rule ID when the catalog task assigns one.)

`FxIncludeCollector`: finds `#include "..."` ranges, resolves them relative to the including file, returns the files to copy (R12). Cycles are cut by a visited set.

### Integration (Migrator)

`EffectRewriteStep` is called by `ContentConverter` where `.fx` items are copied today: for each `.fx` item, scan, rewrite, return a `FileChange` with the new text and the notes. `AssetCopier` stays unchanged for other items. The `.fx` entry in `Content.mgcb` keeps `EffectImporter`/`EffectProcessor`. The original profile mapping is recorded in the manifest. Findings go into `ContentReport` as Warning (`NeedsManualWork = true`) or Info.

New option `MigrationOptions.RewriteEffects` (default true). When false, `.fx` files are copied as today.

`ToolManifestGenerator` also pins `dotnet-mgfxc` 3.8.6.1 in `.config/dotnet-tools.json` (same version as `dotnet-mgcb`), so `dotnet tool restore` brings both. That is a one-line addition plus a test.

### Build check (Runner, `EffectBuildCheck`)

```csharp
public sealed record EffectCheckResult(string Path, EffectCheckStatus Status, string Profile, int Escalations,
    IReadOnlyList<BuildDiagnostic> Diagnostics, string? SkipReason);

public sealed class EffectBuildCheck
{
    public Task<IReadOnlyList<EffectCheckResult>> RunAsync(string migratedRoot, MonoGameTarget target,
        IReadOnlyList<string> effectPaths, IProcessRunner runner, CancellationToken ct);
}
```

1. Guard with `MigratedCopyGuard` (never touches an original).
2. For each effect (sorted ordinal): `dotnet tool run mgfxc -- <abs.fx> <obj/xnareborn-fx/<name>.mgfxo> /Profile:<OpenGL|DirectX_11>` through `IProcessRunner` (so tests use a fake runner).
3. Parse stdout and stderr with `MgfxcOutputParser` (below).
4. On DirectX failure with a limit code, `ProfileEscalator` bumps the macros in the migrated `.fx` (text edit on the `#else` branch of the block written by `FxProfileRewriter`; if the block is not there, no escalation, just the diagnostics) and retries. Max two retries. The file change goes into the manifest.
5. Skip with a reason when: no `.fx`; tool missing (`dotnet tool run` exit code and "Cannot find" text, or restore not done); OS cannot run it. (`mgfxc` for DirectX uses `D3DCompiler`; on Linux and macOS MonoGame runs it through Wine, `WineHelper.RunFxc2`, so the check reports Skipped when Wine is missing. XNA Reborn targets Windows, so this is a documented limit.)
6. Return results in input order. Diagnostics reuse `BuildDiagnostic` with `AssetPath` set, so `DiagnosticGrouper` and the hint matcher work unchanged.

`MgfxcOutputParser` patterns (from the runs above):

| Pattern | Example |
|---|---|
| `^(?<path>.+?)\((?<l>\d+),(?<c>\d+)(,\d+,\d+)?\): (?<sev>error\|warning) (?<code>[A-Z]+\d+): (?<msg>.*)$` | `probe/vpos.fx(12,19,12,21): error X4576: Input signature ...` |
| `^(?<path>.+?),\((?<l>\d+),(?<c>\d+)\) : (?<msg>.*)$` | `probe/alphatest.fx,(13,24) : Unexpected token 'A' found. Expected CloseBracket` |
| `^Invalid profile '(?<m>.*)'\. (?<kind>Vertex\|Pixel) shader '(?<fn>.+)' must be (?<req>.*)!$` | `Invalid profile 'vs_3_0'. Vertex shader 'FloodFillVS' must be SM 4.0 level 9.1 or higher!` |
| `^Failed to (parse\|compile) '(?<path>.+)'!$` | summary line, folded into the previous diagnostic |

Parse errors get the code `MGFX0001` (they have none), profile errors `MGFX0002`, so the hint catalog can match by code. Duplicates (the compiler prints some errors twice) are removed by the existing grouper.

### Hints (data, `hints.json`)

| Id | Match | Advice (summary) |
|---|---|---|
| `fx-sm-too-low` | code `MGFX0002`, message contains `SM 4.0 level 9.1` | The effect names a Shader Model 2 or 3 profile. Use the `#if OPENGL` macros; migration does this for `compile` statements. The name in the message can be empty for pixel shaders. |
| `fx-sm-too-high-gl` | code `MGFX0002`, message contains `SM 3.0 or lower` | OpenGL targets support up to Shader Model 3. |
| `fx-limits` | regex `X(4505\|5608\|5426)` or `too many .* slots` | The shader is too big for the level 9.1 target. Use `_4_0_level_9_3`, or `_4_0` with `GraphicsProfile.HiDef`. |
| `fx-parse-error` | code `MGFX0001` | MonoGame's effect parser is stricter than XNA's. Check the line for an unsupported pass state, an annotation or arguments in `compile`. |
| `fx-vpos` | regex `X4576\|X4502.*VPOS` | `VPOS` is not available. Use `SV_Position` as a pixel shader input (DirectX) and compute screen position from the vertex output (OpenGL). |
| `audio-no-ffmpeg` | message contains `ffprobe` or `ffmpeg` | The audio importers need `ffmpeg` and `ffprobe` on the path. |

Hint text follows the existing `FixHint` shape (id, title, summary, resolution).

## Rule changes (proposed, one catalog task each)

IDs are not assigned here. Other specs add rules at the same time; each catalog task takes the next free `XNAnnn` when it runs and replaces the `NEW-` label in the markers, rewriters and tests. Classification values are the schema's: `AutoFixable`, `Manual`, `Unsupported`.

| Rule | Today | Proposal | Match | Fixture (positive / negative) | Implemented by |
|---|---|---|---|---|---|
| XNA015 code uses XACT runtime | Warning, Manual, `type AudioEngine` | Warning, **AutoFixable**. Explanation: converted when a `.xap` is present, otherwise see `NEW-A1`. Resolution updated. Add `type` entries for `WaveBank`, `SoundBank`, `Cue` only if not already present | `type` (existing) | `xna40-xact` / `not-xna` | task 7 (alias rewriter) |
| XNA016 `.xap` file | Warning, Manual, `filePattern **/*.xap` | Warning, **AutoFixable**. Resolution: converted to assets, catalog and shim; unsupported features are listed as notes | `filePattern` (existing) | `xna40-xact` / `xna40-basic` | tasks 4 to 6 |
| XNA017 `.fx` file | Warning, Manual, `contentItem .fx` | Keep Warning and **Manual** (a shader still needs a visual check), explanation lists the automatic fixes; resolution points to the check results | `contentItem` (existing) | `xna40-effects` / `xna40-basic` | task 17 (text only) |
| NEW-A1 compiled XACT banks | none | Warning, Manual, Content. Banks (`.xgs`, `.xwb`, `.xsb`) are copied; MonoGame loads only PCM or ADPCM, non-streaming banks | `filePattern` (`**/*.xwb`) | `xna40-xact-compiled` / `xna40-basic` | task 4 (copy fallback) |
| NEW-A2 streaming wave bank | none | Info, **AutoFixable**. `Streaming = 1` waves become `Song` assets | `fileContent` (`**/*.xap`, `^\s*Streaming\s*=\s*1\s*;`) | `xna40-xact` (streaming bank) / `xna40-xact` (non-streaming bank) | task 5 |
| NEW-A3 XACT features not converted | none | Warning, Manual, Api. DSP, reverb, filters, stop or volume events | `fileContent` (`**/*.xap`, the semantic list) | `xna40-xact` with a DSP block / `xna40-xact` without | task 5 (notes) |
| NEW-B1 fixed shader model in `compile` | none | Error on DirectX, **AutoFixable**. Profiles replaced by macros | `fileContent` (`**/*.fx`, `compile\s+(vs\|ps)_[1-3]_[0-4ab]`) | `xna40-effects` fixed model / already-portable effect | task 12 |
| NEW-B2 unsupported pass state | none | Error, Manual. List in `passStates` data | `fileContent` (`**/*.fx`, the state names) | `xna40-effects` AlphaTest / plain blend states | task 13 (marker) |
| NEW-B3 annotations, compile arguments, `SetPixelShader` | none | Error, Manual | `fileContent` | `xna40-effects` / plain | task 13 |
| NEW-B4 `VPOS` | none | Error, Manual | `fileContent` (`\bVPOS\b`) | `xna40-effects` / plain | task 13 |
| NEW-B5 old sampler state names | none | Error, **AutoFixable**. `MipMapLodBias`, `GaussianQuad`, `PyramidalQuad` | `fileContent` | `xna40-effects` / plain sampler | task 13 |
| NEW-B6 parameter defaults and unused parameters | none | Info, Manual. Defaults ignored on OpenGL; unused parameters are removed | `fileContent` (defaults only; unused-ness needs the scanner, so reported by the migrator note) | `xna40-effects` / plain | task 13 |

The new match type, `fileContent`:

```json
"match": { "type": "fileContent", "glob": "**/*.fx", "regex": "compile\\s+(vs|ps)_[1-3]_", "maxBytes": 1048576 }
```

The analyzer reads each file that matches `glob` (read-only, text, at most `maxBytes`, comments blanked for `.fx`, `.fxh`) and reports one finding with the first matching line numbers. It is added because no existing type reads file text (`filePattern` and `contentItem` match names; `member` and `type` match C#). The schema, loader tests and a hash-unchanged test cover it (task 3).

## Options (additions to `MigrationOptions`)

| Option | Default | Effect |
|---|---|---|
| `XactMode` | `Auto` | `Auto`, `ConvertOnly`, `CopyOnly` |
| `StreamingWavesAsSong` | true | Waves in `Streaming = 1` banks become `Song` |
| `RewriteEffects` | true | Run the `.fx` rewriters |

The UI shows these under "Content" in migration setup; the UI task is out of scope here and listed as a follow-up.

## Error handling

- A `.xap` that fails to parse: copied, finding, others continue.
- A missing wave: Missing asset; the catalog still lists the wave, marked missing; the shim plays nothing for it and logs through `Debug.WriteLine`.
- A `.fx` that the scanner cannot place: copied unchanged with a finding.
- A tool failure during the check: Skipped with a reason, never a crash.
- All generated files go through the existing `OutputWriter` (never overwrites an existing output folder without confirmation).

## Testing strategy

1. **Reader tests** (`XapReader`): each fixture block, CRLF and BOM, unknown keys, unbalanced braces.
2. **Planner tests**: asset names, shared files, streaming to Song, missing files, presets.
3. **Catalog snapshots** (Verify): `xna40-xact` catalog; determinism test.
4. **Shim tests** (in a test project that references the generated shim sources and the MonoGame package): cue selection per variation type with a seeded random, volume and pitch math, loop and finite-loop logic through an `IInstanceFactory` seam so no audio device is needed, category limits and behaviors, lookup by stem, error messages. Parity test by reflection against the MonoGame assembly.
5. **Alias rewriter tests**: using alias placement, qualified names, local type conflict, idempotence, snapshot.
6. **Scanner tests**: comments, `#if` depth, includes, sampler forms, techniques with several passes.
7. **Rewriter tests** per rule: positive and negative fixture, snapshot of the rewritten `.fx`, idempotence, byte-identical rest of file.
8. **Check tests** with a fake `IProcessRunner` that replays the real outputs recorded above (stored as text fixtures): parser patterns, escalation order, skip reasons.
9. **Hint tests**: each hint has a positive text and a negative text.
10. **Hash tests**: analysis leaves fixture hashes unchanged.
11. **Slow integration tests** (skipped without `ffmpeg`/`mgfxc`): `mgfxc` on `xna40-effects` (rewritten files must pass on OpenGL; DirectX where the machine can run it); `dotnet-mgcb` on the converted `xna40-xact` content with generated WAV bytes.

## Fixtures (original, written from scratch)

- `xna40-xact`: `Game1.cs` that builds an `AudioEngine`, `WaveBank`, `SoundBank` and plays two cues; `Content/Sounds.xap` with two categories, three variables, one RPC on volume, a normal wave bank (3 waves, one shared file), a streaming wave bank (1 wave), a sound bank with a single-wave cue, a random-variation cue (2 sounds), a looping music cue, a multi-track sound, a cue with a DSP block (unsupported), and one wave whose file is missing. Wave bytes are written at test time.
- `xna40-xact-compiled`: `Game1.cs` with the same calls and `Content/Sounds.xgs`, `Wave Bank.xwb`, `Sound Bank.xsb` as small text placeholders with the right extension (the analyzer and copier only need the names).
- `xna40-effects`: eight small `.fx` files covering: fixed SM2 profile; SM3 profile; template style `#define VS_SHADERMODEL` unconditional; already conditional; `MipMapLodBias`; `GaussianQuad`; `AlphaTestEnable`; annotation and compile arguments; `VPOS`; default value and unused parameter; an `#include` of a `.fxh`. Each file is under 40 lines.

All three carry a `README.md` saying they are original and synthetic (safety rule 5).

## Test candidates (read only, nothing in the repository)

Licenses are what the GitHub API reports on 2026-11-11. Check the license file before using any of them in a demo. They are for manual runs outside CI.

| Repository | License | Use | What it offers |
|---|---|---|---|
| [jackpal/Dandy-Dungeon](https://github.com/jackpal/Dandy-Dungeon) | Apache-2.0 | XACT | `dandy-xna/Media/Sounds/DandySounds.xap`, 12 cues, 12 waves, one category set. A full game. |
| [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) | MIT (repo level; samples were Microsoft Permissive License, check per sample) | XACT | `Catapult` (9 cues), `Movipa` (11 cues, 1 RPC with 2 curves, compression preset), `RobotGame` (55 cues, 3 RPCs, 44 RPC entries), `VectorRumble` (multi-track), `Pickture`, `Minjie` |
| [HowdyChad/XNAUnleashed](https://github.com/HowdyChad/XNAUnleashed) | MIT | XACT and fx | `Chapter7/SoundDemo` has the `.xap` and the compiled `.xgs`, `.xwb`, `.xsb` (tests the "both present" rule); 34 `.fx` files (`Blur`, `Chalk`, `AmbientTexture`) |
| [MonoGame/MonoGame](https://github.com/MonoGame/MonoGame) `Tests/Assets/Audio/Tests.xap` | Microsoft Public License (GitHub shows NOASSERTION; confirm in `LICENSE.txt`) | XACT | 3 waves, one with an ADPCM compression preset, with the matching compiled `Tests.xgs/.xwb/.xsb` that the MonoGame test uses; the only upstream proof that its runtime plays XACT |
| [RobLach/Overly-Professional](https://github.com/RobLach/Overly-Professional) | MIT | XACT | `src/WhiteBoard/Content/Audio/audio.xap` |
| [wahabjawed/Space-Shooter-Game](https://github.com/wahabjawed/Space-Shooter-Game) | MIT | XACT, edge case | Calls `new AudioEngine("Content\\game_sound.xgs")` and has no `.xap` or compiled bank. Tests the "code present, data absent" warning. |
| [petriw/XNA-Shader-Programming](https://github.com/petriw/XNA-Shader-Programming) | MIT | fx and XACT | 55 `.fx` tutorial files, one `Music.xap`; the best corpus for rewriter false positives |
| [pampersrocker/EvoNet](https://github.com/pampersrocker/EvoNet) | MIT | fx | 4 `.fx`; three tested here (see the table); template style macros |
| [TinkerWorX/XNA-GPUDust](https://github.com/TinkerWorX/XNA-GPUDust) | MIT | fx | 6 pixel shader `.fx`; two tested; needs `_4_0_level_9_3` |
| [VisualStudioEX3/Grey-Infection](https://github.com/VisualStudioEX3/Grey-Infection) | MIT | fx | `GreyScale.fx`, the smallest case |

Not used: repositories without a license, and `billionare/FPSLighting` (a bundle of DirectX SDK sample files; used only to read the `Streaming` key name).

Manual run procedure (task 19): clone to a scratch folder outside the repository, run the analyzer and migrator with an output folder outside the repository, build, record results in the checkpoint notes, delete the clones.

## Risks and open questions

1. **Pitch units, loop count meaning, behavior names and variation types** are inferred from real files and MonoGame's reader. Fixtures written by hand cannot prove them, so task 1 checks the archived XACT documentation instead. If it disagrees, only constants in the generator change.
2. **Song through MediaPlayer** is the weakest part of the shim (one song, no pitch). If candidate runs show games that play music through a streaming cue and also through `MediaPlayer`, the option `StreamingWavesAsSong` should default to false. Decision at the task 10 checkpoint.
3. **Audio needs ffmpeg and ffprobe** at content build time, even for WAV files (`DefaultAudioProfile.ProbeFormat`). Environment detection in the runner reports it (R7.3). This is new and affects the demo machine.
4. **mgfxc on DirectX needs Windows** (or Wine). The check is Skipped elsewhere.
5. **The `_2_a`/`_2_b` GL mapping** is unverified.
6. **Rendering equality** after the profile rewrite is not verified by any automatic test. A person compares the picture for each candidate effect at the shader checkpoint.
7. **Rule numbering conflicts** with sibling specs. Handled by the `NEW-` labels (see above).
8. **Alias precedence.** The design relies on C# name lookup: an alias in a namespace or compilation unit wins over types imported by using-namespace directives at the same level. A test in task 7 proves it for the DesktopGL and WindowsDX packages (a file that has both `using Microsoft.Xna.Framework.Audio;` and the alias must compile).
9. **ContentManager with an empty service provider** is expected to work for `SoundEffect` and `Song` because the MonoGame `ContentReader` does not use the graphics device for them (`ContentManager.cs` reads `IGraphicsDeviceService` only for loading textures from image files). Task 8 proves it with a test that loads a real asset.

## Sources

MonoGame (tag v3.8.6.1, read from the repository):
- [Custom Effects](https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html) and [MGFXC](https://docs.monogame.net/articles/getting_started/tools/mgfxc.html)
- Effect compiler: [MGFX.tpg](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/TPGParser/MGFX.tpg), [ShaderProfile.DirectX11.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/ShaderProfile.DirectX11.cs), [ShaderProfile.OpenGL.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/ShaderProfile.OpenGL.cs), [EffectObject.hlsl.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/EffectObject.hlsl.cs), [Preprocessor.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/Preprocessor.cs), [ShaderData.sharpdx.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tools/MonoGame.Effect.Compiler/Effect/ShaderData.sharpdx.cs)
- Pipeline: [EffectProcessor.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework.Content.Pipeline/Processors/EffectProcessor.cs), [DefaultAudioProfile.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework.Content.Pipeline/Audio/DefaultAudioProfile.cs), [WavImporter.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework.Content.Pipeline/WavImporter.cs)
- XACT runtime: [AudioEngine.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/Xact/AudioEngine.cs), [WaveBank.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/Xact/WaveBank.cs), [WaveBank.Default.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Platform/Audio/Xact/WaveBank.Default.cs), [SoundBank.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/Xact/SoundBank.cs), [Cue.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/Xact/Cue.cs), [XactSound.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/Xact/XactSound.cs), [SoundEffect.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/MonoGame.Framework/Audio/SoundEffect.cs), [XactTest.cs](https://github.com/MonoGame/MonoGame/blob/v3.8.6.1/Tests/Framework/Audio/XactTest.cs)
- Forum: [XACT XGS format not recognized](https://community.monogame.net/t/xact-xgs-format-not-recognized/2675)

Archived XNA and Microsoft documentation:
- [Getting Started with XACT](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff827592(v=xnagamestudio.41)) (XACT is supported on Windows and Xbox 360)
- [XNA Framework audio overview](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb195055(v=xnagamestudio.10)), [How to: Add a Sound File to Your Game Using XACT](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb203879(v=xnagamestudio.10)), [How to: Apply Basic 3D Positional Effects to a Cue](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb447687(v=xnagamestudio.10))
- [Audio input and output formats](https://learn.microsoft.com/en-us/archive/blogs/etayrien/audio-input-and-output-formats) (the XACT build keeps PCM input as PCM by default)
- [HLSL scalar types](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-scalar) (`half` maps to `float` on Direct3D 10 targets)
- [What Is a Profile?](https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_GraphicsProfile.html) (Reach is Shader Model 2, HiDef is Shader Model 3 or above)

Real `.xap` and `.fx` files were read from the repositories in the candidate table to learn the formats; none was copied into this repository.

## Implementation notes and gaps

Kept up to date by the worker that implements the tasks. Each entry names the task.

- **Tasks 1 to 3 (fixtures, reader, `fileContent`).**
  - `rules/xact-features.json` is embedded in `XnaReborn.Core` and loaded by `XactFeatures`. Loop count (255 infinite, N repeats N more times, 0 once) and the five variation types are marked confirmed, from the MonoGame v3.8.6.1 `PlayWaveEvent.cs` source. Volume units, pitch units, instance behavior names and the `.xap` block names for unsupported features are still marked `expected, unverified`: the archived XACT documentation was not read, so nothing there was confirmed from it. Pitch also differs from MonoGame's compiled-bank reader (divides by 1000), which stays an open risk.
  - `fixtures/xna40-xact` has four `Wave Bank` waves counted as three plus one: the missing `ghost.wav` sits in its own `Extra Bank` so the normal bank keeps the three waves the task asks for.
  - The reader reports `ANA070` (unbalanced braces) and `ANA071` (malformed line, unreadable file). The spec reserved `ANA050` to `ANA059` for another spec, so these two were picked outside that block.
  - The reader lists unsupported items by looking for the `unsupported` terms in `xact-features.json` inside block names and keys (case-insensitive substring). Values are not searched. A block that matches is reported once and not searched further.
  - `fileContent` returns `path:line` for up to three matching lines per file. It skips files with a NUL byte and files over `maxBytes`. Comments are blanked only for `.fx` and `.fxh`.

- **Tasks 11 to 14 (effect scanner, rewriters, migrator step).**
  - Data is in `rules/effect-profiles.json` (embedded in Core as `EffectProfiles`). `vs_2_a`/`ps_2_b` to `vs_3_0`/`ps_3_0` on OpenGL is now confirmed by a real `dotnet-mgfxc` 3.8.6.1 run (both profiles). Deviation from the design table: `vs_1_1` and `ps_1_x` map to `vs_2_0`/`ps_2_0` on OpenGL, not "same", because the OpenGL compiler only accepts models 2 and 3. Those rows stay `expected, unverified`.
  - The model regex is `(vs|ps)_N_[0-4ab]`, so `vs_2_a` and `ps_2_b` are literals. A model that matches the shape but is not in the table gets a manual note and is left as written.
  - `VPOS` is detected only as a semantic (an identifier after `:`), because a technique can be named `Vpos`.
  - Parameter "unused" means the name appears nowhere outside its declaration (the design said function bodies only); a texture used only by a `sampler_state` is therefore not reported.
  - Marker notes carry line numbers of the migrated file, because markers run after the profile block is inserted. The file note in the report names the migrated path.
  - `FxRuleIds` use the provisional `NEW-B1` to `NEW-B6` labels until task 17. `NEW-B7` is not used.
  - `EffectRewriteStep` copies `#include` files only when the effect itself is rewritten (option `RewriteEffects`). A file that is not UTF-8 is copied unchanged with a note.
  - Verified with the real tool (slow tests, `XNAREBORN_INTEGRATION=1`): all rewritten fixtures that should be portable compile on OpenGL and DirectX_11; the unrewritten SM2 fixture fails on DirectX with the level 9.1 message; `AlphaTest` and `Annotated` still fail after the rewrite, with a marker on each offending line. Rendered output was not compared (task 18 needs a person).

- **Tasks 15 and 16 (effect build check, hints).**
  - `MgfxcOutputParser`, `ProfileEscalator`, `EffectBuildCheck` are in `XnaReborn.Runner/Effects`. The check takes a compiler profile (`OpenGL` or `DirectX11`) instead of `MonoGameTarget`, because the Runner does not reference the Migrator. It is not yet called by `ProjectRunner` or the UI; wiring it into the build flow is a follow-up.
  - Recorded outputs under `tests/XnaReborn.Tests/Runner/Samples/mgfxc-*.txt`: the parse error, both profile errors and the success line come from real `dotnet-mgfxc` 3.8.6.1 runs (paths shortened). The HLSL error sample is shaped after the format seen in the research runs and its message text is illustrative, not a recorded output; no real HLSL error output was captured.
  - A real run showed that `mgfxc` does not create the output folder, so the check creates `obj/xnareborn-fx` first. The slow test `Build_check_runs_the_pinned_tool_in_a_migrated_copy` restores the pinned tools in a temp migrated copy and runs the real check.
  - Hint ids are `H301` to `H309` (the design's names are `fx-*`; the catalog uses `H` ids). The limit hint is split over a message regex and two codes, and VPOS over a code and a message regex, because the matcher tests the code and the message separately. The audio hint is `H306`.
  - The shader that needs a level above `_4_0_level_9_1` is never tested here: no real shader was found that triggers X4505/X5608 within the fixtures, so escalation is tested with a fake runner only.

- **Task 17 (catalog rules), shader part only.**
  - New rules `XNA064` to `XNA069` use the `fileContent` match on `**/*.fx` (labels `NEW-B1` to `NEW-B6`; `NEW-B7` was never used). `XNA064` and `XNA068` are AutoFixable and link `audio-and-shaders:12` and `:13`. `XNA017` keeps Manual with new text. The rule ids in markers, notes and tests are now these ids.
  - `XNA065` lists common old state names by regex; it is not the complete allow list the migrator uses, so the migrator can mark more than the analyzer reports. `XNA064` does not see a template-style `#define VS_SHADERMODEL vs_2_0`, because the same text inside `#if OPENGL` in an already portable effect would be a false positive.
  - Analyzer scores recorded in the real-catalog snapshots: `xna40-effects` 59/100 (72 after automatic fixes).
  - The XACT rules `NEW-A1` to `NEW-A3` and the XNA015/XNA016 changes were done later (see the notes on tasks 4, 6, 7 and 17 below).

- **Tasks 4.2 and 5 (planner, catalog writer), not wired in.**
  - `XactPlanner` and `XactCatalogWriter` (`XnaReborn.Migrator/Audio`) are pure and tested, but nothing in `ContentConverter` or `ProjectMigrator` calls them: tasks 4.1, 4.3, 6, 7 and the whole shim (8 to 10) are not implemented, so a migrated project still gets the old manual-work treatment for `.xap` files. Adding the converter step without the shim would produce unusable output, so it was left out on purpose.
  - The catalog calls constructors of data types that the shim must define. Their signatures are fixed in `XactCatalogContract.Source` (a compile-test stand-in and the contract for task 8). The catalog test compiles the fixture catalog with Roslyn against that source (task 5.4).
  - Volume uses `gain = 10^(centiDb/2000)` and pitch `cents/1200` clamped to -1..1; both unit statements are still `expected, unverified` in `rules/xact-features.json`.
  - Rule labels `NEW-A2` (streaming) and `NEW-A3` (unsupported features) were used in the planner's notes; they are now XNA071 and XNA072.
  - The catalog snapshot has long lines (one expression per bank). Readable enough to diff, not pretty; line-wrapping the generated code is a possible later change.

- **Tasks 8 and 9 (shim), the XACT shim as real source.**
  - The shim is real C# under `src/XnaReborn.Migrator/Shims/Xact/*.cs`, not raw strings. The Migrator project removes them from its own compile list and embeds them; `XactShimSources.Files` reads them back and `ShimProjectGenerator.Generate(..., xactCatalog)` writes them to `XnaReborn.Compat/Xact/` with `XactCatalog.g.cs`. Reason: the shim is compiled and run against the real MonoGame assembly by the new test project `tests/XnaReborn.Xact.Tests` (it links the same files), which a string could not give. That project is new (a MonoGame package reference in a test project, central version already pinned).
  - Seams: `IXactVoice` and `IXactVoiceFactory` instead of `IInstanceFactory`. The engine takes the factory, a `Random` and its data through an internal constructor; the public constructors look the catalog up by file stem. Tests use `Advance(seconds)` instead of waiting for `Update`.
  - Parity test: every public member of MonoGame 3.8.6.1 `AudioEngine`, `WaveBank`, `SoundBank`, `Cue` and `AudioCategory` exists on the shim type with the same signature, and the interfaces and value-type-ness match. No member needed the `NotSupportedException` fallback (`AudioEngine.RendererDetails` does not exist in 3.8.6.1).
  - Choices not in the design: `AudioCategory` is a struct (as in MonoGame). `GetCategory` accepts `Global`, `Default` and `Music` even when the .xap lists no such category. An unknown cue, category or variable throws with a message that lists what exists. Variables are clamped to their minimum and maximum; a read-only one throws. Instance-limit counts of 0 and 255 mean no limit.
  - RPC curves are applied in the shim, in XACT units: property 0 is volume (hundredths of a decibel), property 1 is pitch (hundredths of a semitone). Other properties (reverb send, filters) and the curve shape are ignored; the interpolation is linear. Curve properties 0 and 1 are inferred, not confirmed from the XACT documentation.
  - `Song` waves play through `MediaPlayer` (one song at a time, pitch ignored, volume is `MediaPlayer.Volume`). The song voice and the real `SoundEffectInstance` voice are not covered by an automatic test: they need an audio device and a built asset. Task 10 runs them with real built assets.
  - Not done: a wave's loop region only matters through the event loop count, so a wave with a loop region and loop count 0 plays once. Cue fades, `Relative` event timestamps, cue `NewVariationOnLoop`, DSP, reverb and filters are ignored. `Stop(AsAuthored)` stops at once.
  - Task 8.4 is open only for the real-asset load test (`ContentManager` with an empty service provider); it needs the audio tools and runs in task 10.

- **Tasks 4, 6 (not 6.4), 7 and the XACT part of 17 (converter, aliases, rules).**
  - `MigrationOptions` and `ContentOptions` gained `Xact` (`XactMode`: `Auto`, `ConvertOnly`, `CopyOnly`) and `StreamingWavesAsSong` (default true). Both are trailing optional parameters, so existing callers are unchanged. The App has no switch for them yet (a UI toggle is a follow-up; no migration logic was added to the App).
  - `ContentConverter` converts a readable `.xap` that sits in a content project. Each distinct wave file becomes an entry with `WavImporter` (or Mp3, Ogg, Wma by extension), `SoundEffectProcessor` or `SongProcessor`, `Quality=Best` and the destination `xact/<engine>/<bank>/<wave>.<ext>`, plus a copy of the wave file next to `Content.mgcb`. `MgcbWriter.FindProblems` accepts the pairs, so no allowed-pairs list was needed. A wave in another format (for example `.aif`) is left out, reported, and marked missing in the catalog. The `.xap` itself is copied and counts as Converted. `ContentConversionResult.Xact` carries the parsed projects and plans; `ContentReport.Audio` (`AudioReport`) carries the counts. The manifest has no extra audio section: the generated files are in its change list with reasons, the shim files in its shim block, and the audio notes in its notes.
  - `ProjectMigrator` builds one `XactCatalog.g.cs` from all converted projects, creates the shim generator when none exists (so `EnableShims` is not needed), writes the stand-in to `XnaReborn.Compat/Xact/`, and runs the `XNA015` alias rewriter only for projects whose `.xap` was converted. The project reference to `XnaReborn.Compat` is added when an alias was written (R6.5); `ShimProjectGenerator.Scan` was left as it is. The catalog's content root is always `Content`, the folder the MonoGame content builder writes to.
  - Compiled banks: a bank that is a content-pipeline item and a bank that a project copies to its output folder are both handled. With a converted `.xap` they are copied and the report says they are not used. Without one they are copied and the report carries `XNA070` with the MonoGame limits (PCM and ADPCM only, no streaming banks). `CopyOnly` adds nothing.
  - Rules: `NEW-A1` is `XNA070` (`**/*.xwb`), `NEW-A2` is `XNA071` (`Streaming = 1`, Info, AutoFixable), `NEW-A3` is `XNA072` (the semantic list, Manual). `XNA015` and `XNA016` are now AutoFixable and link `audio-and-shaders:7` and `:4`, as the table above says. Side effect: for a project that has only compiled banks, `XNA015` still counts as AutoFixable in the projected score, although the migrator then converts nothing; the rule engine cannot say "only if a .xap exists". Scores now: `xna40-xact` 86 (97 after fixes), `xna40-xact-compiled` 89 (97), `xna40-custom-content` 76 (86).
  - Runner: `AudioToolCheck` looks for `ffmpeg` and `ffprobe` (`-version`) when a `Content.mgcb` outside `bin` and `obj` has an audio processor. `ProjectRunner` runs it before the build, puts the warning in `RunnerResult.Warnings` and in the build output as a `warning:` line, and still builds. The existing hint `H306` covers the build error. The App does not show the warnings as their own element.
  - Alias placement: the block goes after the whole line of `using Microsoft.Xna.Framework.Audio;` (a trailing comment stays on it), keeps indent and CRLF, and works in block and file-scoped namespaces. A test builds the rewritten fixture game and the stand-in against the MonoGame DesktopGL assembly: it compiles, and the same file with the stand-in namespace imported but no alias fails with CS0104. That proves design risk 8 for DesktopGL. WindowsDX is covered by the task 10 build, not by a unit test.
