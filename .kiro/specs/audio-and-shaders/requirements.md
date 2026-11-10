# Requirements: XACT Audio and .fx Shader Conversion

## Introduction

Two kinds of XNA content still end in manual work after content conversion: XACT audio (`.xap` projects and `AudioEngine`, `WaveBank`, `SoundBank`, `Cue` code) and `.fx` effect files. This spec turns both into things that build and run on MonoGame 3.8.6.1 (net8.0, `dotnet-mgcb` 3.8.6.1) for DesktopGL and WindowsDX, and says honestly what it cannot convert.

Part A converts XACT projects into plain `SoundEffect` and `Song` assets plus a generated compatibility layer that keeps the `Cue`/`SoundBank` code working. Part B rewrites the safe, common parts of `.fx` files and checks every effect with the MonoGame effect compiler before the full build.

This spec adds to `content-conversion` (R6 and R7), `project-migrator` (shims and source rewriting) and `build-and-run` (hints). It does not change their existing requirements. Research, sources and evidence are in `design.md`.

## Glossary

- **XACT**: Microsoft Cross-Platform Audio Creation Tool. A Windows authoring tool that is no longer updated.
- **`.xap`**: XACT project. A text file listing categories, variables, wave banks, sound banks, sounds and cues.
- **`.xgs` / `.xwb` / `.xsb`**: compiled XACT global settings, wave bank and sound bank.
- **Cue**: the named thing game code plays. A cue picks one sound; a sound plays one or more waves.
- **XACT catalog**: the generated C# data file that describes the converted banks to the shim.
- **XACT shim**: the generated classes `AudioEngine`, `WaveBank`, `SoundBank`, `Cue` and `AudioCategory` in `XnaReborn.Compat`, built on `SoundEffectInstance`.
- **MGFX**: MonoGame's effect format and compiler (`mgfxc`, also run by the `EffectProcessor`).
- **Shader model / profile**: the `vs_x_y` and `ps_x_y` target named in a `compile` statement.
- **Marker**: a comment line `// XNA Reborn: ...` inserted above a line that needs a person's attention.
- **Escalation**: retrying a DirectX effect compile with the next higher profile after a limit error.

## Part A: XACT audio

### R1: XACT project reading
**User story:** As a developer, I want my XACT project understood without needing XACT installed.

Acceptance criteria:
1. WHEN a content project or the migrated source tree contains a `.xap`, THE reader SHALL parse it as text and return its global settings, categories, variables, RPC curves, wave banks, waves, sound banks, sounds, tracks, play-wave events and cues.
2. THE reader SHALL record, for each wave, its source file path relative to the `.xap`, channels, sampling rate, play region, loop region and compression preset name.
3. THE reader SHALL record, for each wave bank, its name, the stem of its Windows output file and whether `Streaming = 1`.
4. THE reader SHALL record any block or key it does not understand in an unsupported-items list, with the line number, and SHALL continue.
5. IF a `.xap` cannot be parsed, THEN THE reader SHALL return a finding with the file and line, and the converter SHALL treat that `.xap` as manual work and copy it unchanged.
6. THE reader SHALL accept UTF-8 with or without a byte order mark, LF or CRLF line endings, and tab or space indentation.
7. THE reader SHALL open files read-only and SHALL NOT need XACT, XNA Game Studio or network access.

### R2: Conversion mode
**User story:** As a developer, I want a predictable choice between converting and copying.

Acceptance criteria:
1. THE migrator SHALL offer the XACT modes `Auto` (default), `ConvertOnly` and `CopyOnly` in `MigrationOptions`.
2. WHILE the mode is `Auto`, WHEN a parsable `.xap` exists, THE converter SHALL convert it.
3. WHILE the mode is `Auto`, WHEN compiled banks (`.xgs`, `.xwb`, `.xsb`) exist and no `.xap` exists for them, THE converter SHALL copy them unchanged, SHALL NOT generate a shim, and SHALL add a Warning that explains the MonoGame runtime limits (design, section "MonoGame XACT runtime").
4. WHILE the mode is `CopyOnly`, THE converter SHALL copy all XACT files unchanged and SHALL NOT generate assets, catalog, shim or source aliases.
5. WHEN a `.xap` is converted and compiled banks for the same project also exist, THE converter SHALL copy the compiled banks unchanged, SHALL NOT use them, and SHALL say so in the report.
6. IF the mode is `ConvertOnly` and a `.xap` cannot be converted, THEN THE converter SHALL report an error finding for it and SHALL NOT fall back silently.

### R3: Wave assets in the content file
**User story:** As a developer, I want every XACT wave to become a normal asset.

Acceptance criteria:
1. THE converter SHALL add one `Content.mgcb` entry per distinct wave source file, named `xact/<xap stem>/<wave bank name>/<wave name>`, using the importer and processor for the target kind.
2. THE converter SHALL build a wave as a `SoundEffect` (`WavImporter`, `SoundEffectProcessor`, `Quality=Best`) unless R3.3 applies.
3. WHERE a wave belongs to a wave bank with `Streaming = 1`, THE converter SHALL build it as a `Song` (`WavImporter`, `SongProcessor`) when the option `StreamingWavesAsSong` is on (default on).
4. IF two waves point to the same source file, THEN THE converter SHALL build it once and map both wave names to that asset in the catalog.
5. THE converter SHALL copy every wave source file into the migrated copy and report any wave file that is missing on disk as a Missing asset, without stopping.
6. WHEN a wave has a compression preset other than none, THE converter SHALL add an Info note that the preset is not applied and the wave is built as PCM.
7. THE converter SHALL write the entries in a deterministic order and SHALL NOT modify the original wave files.

### R4: XACT catalog
**User story:** As a developer, I want the sound design (cues, variations, volumes) kept.

Acceptance criteria:
1. THE converter SHALL generate `XactCatalog.g.cs` in the shim project containing, per `.xap`: the Windows file stems for the engine, wave banks and sound banks; the content root; categories; global and cue variables; RPC curves on volume and pitch; waves with asset name, kind (`SoundEffect` or `Song`), loop count and loop flag; sounds with volume, pitch, priority, category and tracks; cues with variation type and entries.
2. THE converter SHALL convert units in the generator, not at run time: volume in hundredths of a decibel to a linear gain, pitch in hundredths of a semitone to the XNA pitch range of -1 to 1.
3. THE generator SHALL produce byte-identical output for identical input.
4. IF a cue or sound uses an XACT feature the shim does not support, THEN THE converter SHALL still include it, SHALL record the unsupported feature in the catalog, and SHALL add a Warning finding that names the cue and the feature.
5. THE converter SHALL write the catalog only into the migrated copy.

### R5: XACT shim behavior
**User story:** As a developer, I want my `SoundBank.PlayCue` code to keep working.

Acceptance criteria:
1. THE shim SHALL provide the public constructors and members of `AudioEngine`, `WaveBank`, `SoundBank`, `Cue` and `AudioCategory` that MonoGame 3.8.6.1 provides, with the same names and signatures.
2. WHEN game code passes a file path to a shim constructor, THE shim SHALL look the file up in the catalog by file name stem (case-insensitive, any folder prefix ignored) and SHALL NOT read the file from disk.
3. IF no catalog entry matches, THEN THE shim SHALL throw `FileNotFoundException` with a message that names the file and says which converted banks exist.
4. WHEN a `SoundBank` plays a cue, THE shim SHALL choose a sound by the cue's variation type (ordered, ordered from random, random, random without immediate repeat, shuffle) and SHALL play the sound's waves with the sound volume, track volume, pitch, category volume and cue variables applied.
5. THE shim SHALL support `Cue.Play`, `Pause`, `Resume`, `Stop(AudioStopOptions)`, `IsPlaying`, `IsPaused`, `IsStopped`, `Name`, `SetVariable`, `GetVariable`, `Apply3D` and `Dispose`.
6. THE shim SHALL support `AudioEngine.Update`, `GetCategory`, `GetGlobalVariable`, `SetGlobalVariable`, `Dispose`, and `AudioCategory.Stop`, `Pause`, `Resume`, `SetVolume`.
7. WHERE a play-wave event has an infinite loop count, THE shim SHALL loop the wave until the cue is stopped.
8. WHERE a category has a maximum instance count, THE shim SHALL apply the category's behavior (fail to play, queue, replace oldest, replace quietest, replace lowest priority) when the limit is reached.
9. WHERE a catalog entry is unsupported, THE shim SHALL play the supported parts of the cue and SHALL NOT throw.
10. THE shim SHALL load waves through a `ContentManager` with an empty service provider and SHALL NOT need a `Game` or graphics device.
11. THE shim SHALL be available for both DesktopGL and WindowsDX, using the same source.

### R6: Source aliasing
**User story:** As a developer, I want my C# code to use the shim without hand edits.

Acceptance criteria:
1. WHEN a `.cs` file names `AudioEngine`, `WaveBank`, `SoundBank`, `Cue` or `AudioCategory` from `Microsoft.Xna.Framework.Audio` and the project was converted, THE rewriter SHALL add a using alias for each used type that points to `XnaReborn.Compat.Xact`, in the same scope as the `Audio` using directive.
2. THE rewriter SHALL replace fully qualified uses of these types with the shim type.
3. THE rewriter SHALL NOT change types from `Microsoft.Xna.Framework.Audio` that the shim does not replace (`SoundEffect`, `SoundEffectInstance`, `AudioListener`, `AudioEmitter` and others).
4. IF a file declares its own type with one of these names, THEN THE rewriter SHALL leave the file unchanged and add a manual-work note.
5. THE migrator SHALL add the shim project reference to the migrated game project only when at least one alias was written.
6. THE rewriter SHALL produce no change for a project converted in `CopyOnly` mode.

### R7: Audio reporting
**User story:** As a developer, I want to see what became of each cue.

Acceptance criteria:
1. THE converter SHALL add to the content report: banks converted, waves built as `SoundEffect` and as `Song`, cues with full support, cues with unsupported features, missing waves and notes.
2. THE migration manifest SHALL list every generated file (assets, catalog, shim) with its reason.
3. WHEN the migrated project builds audio, THE runner SHALL check that `ffmpeg` and `ffprobe` are on the path (the MonoGame audio importers call them) and SHALL report a clear Warning with a hint if they are missing.

## Part B: .fx shaders

### R8: Effect scanning
**User story:** As a developer, I want the tool to understand my shader's structure before it changes anything.

Acceptance criteria:
1. THE scanner SHALL read each `.fx` and each file it includes with `#include "..."` (relative to the including file) and SHALL return its techniques, passes, `compile` statements, sampler declarations, global parameters, function entry points and `#define` lines, each with line numbers.
2. THE scanner SHALL ignore text inside `//` and `/* */` comments.
3. THE scanner SHALL NOT need to parse every HLSL expression. IF it cannot place a construct, THEN it SHALL leave that text alone and continue.
4. THE scanner SHALL open files read-only.
5. WHEN an included file is missing, THE scanner SHALL report it as a finding with the include path.

### R9: Shader profile rewrite
**User story:** As a developer, I want a Shader Model 2 or 3 effect to compile for DirectX without hand edits.

Acceptance criteria:
1. WHEN a `compile` statement names a fixed profile (`vs_1_1` to `vs_3_0`, `ps_1_1` to `ps_3_0`, including `_2_a` and `_2_b`), THE rewriter SHALL replace the profile with a macro and SHALL insert one `#if OPENGL ... #else ... #endif` block at the top of the file that defines the macro.
2. THE OpenGL branch SHALL keep the original profile (or `vs_3_0`/`ps_3_0` for `_2_a`/`_2_b`). THE DirectX branch SHALL use the mapping table in `design.md` (Shader Model 2 to `_4_0_level_9_1`; `_2_a`, `_2_b`, and Shader Model 3 to `_4_0_level_9_3`).
3. WHERE a file already uses a macro for the profile (for example `#define VS_SHADERMODEL vs_2_0` outside any `#if`), THE rewriter SHALL replace that define with the conditional block and SHALL NOT add a second one.
4. WHERE a file already selects its profile with `#if OPENGL` or `#if SM4`, THE rewriter SHALL NOT change the profile and SHALL report the file as already portable.
5. THE rewriter SHALL use the idiomatic names `VS_SHADERMODEL` and `PS_SHADERMODEL` when the file uses one vertex model and one pixel model, and one macro per distinct model otherwise.
6. THE rewriter SHALL change nothing else in the file and SHALL leave the rest of the text byte for byte identical.
7. THE rewriter SHALL write the result only into the migrated copy, and the original `.fx` SHALL stay unchanged.
8. GIVEN the same input, THE rewriter SHALL produce byte-identical output, and running it on its own output SHALL change nothing.

### R10: Sampler state rewrite
**User story:** As a developer, I want old sampler state names to compile.

Acceptance criteria:
1. WHEN a `sampler_state` block uses `MipMapLodBias`, THE rewriter SHALL rename it to `MipLodBias`.
2. WHEN a `sampler_state` block sets `MinFilter`, `MagFilter`, `MipFilter` or `Filter` to `GaussianQuad` or `PyramidalQuad`, THE rewriter SHALL replace the value with `Linear` and add a note that the filter changed.
3. THE rewriter SHALL NOT change sampler forms MonoGame already accepts: `sampler`, `sampler2D`, `sampler3D`, `samplerCUBE`, `SamplerState`, `: register(sN)`, `= sampler_state {...}`, and `Texture = <name>` or `Texture = (name)`.
4. WHEN a `sampler_state` block uses a state MonoGame does not know (for example `SRGBTexture`), THE rewriter SHALL add a marker above that line and SHALL NOT delete it.

### R11: Markers for unsafe differences
**User story:** As a developer, I want to be told exactly where I have to work.

Acceptance criteria:
1. WHEN a pass sets a state outside the MonoGame effect grammar (for example `AlphaTestEnable`, `AlphaFunc`, `AlphaRef`, `FogEnable`, `Lighting`, `SeparateAlphaBlendEnabled`, `SrcBlendAlpha`), THE rewriter SHALL add a marker above the line and SHALL NOT delete it.
2. WHEN a technique or pass has an annotation (`technique T < ... >`), or a `compile` statement passes arguments (`compile ps_2_0 F(true)`), or `SetVertexShader`/`SetPixelShader` is used, THE rewriter SHALL add a marker above the line.
3. WHEN a pixel shader uses the `VPOS` semantic, THE rewriter SHALL add a marker that explains the `SV_Position` replacement.
4. WHEN a global parameter has a default value (`float4 Tint = float4(1,0,0,1);`), THE rewriter SHALL add an Info marker that default values do not work on OpenGL targets.
5. WHEN a global parameter is declared but never used in any function body, THE rewriter SHALL add an Info marker that the compiler removes unused parameters and that `Parameters["name"]` returns null.
6. THE rewriter SHALL write each marker with the rule label and one sentence, and SHALL NOT insert the same marker twice in one file.
7. THE rewriter SHALL return a note per marker with `NeedsManualWork = true` for markers R11.1 to R11.3 and `false` for R11.4 and R11.5.

### R12: Effect include files
**User story:** As a developer, I want shaders that include other files to still compile.

Acceptance criteria:
1. WHEN an `.fx` includes another file with `#include "x.fxh"` and the file exists next to it, THE migrator SHALL copy the included file to the same relative path in the migrated copy, even when the content project does not list it.
2. THE migrator SHALL scan included files with the same rewriter, because they may contain `compile` statements.
3. IF an include cannot be found, THEN THE migrator SHALL report a Warning finding and SHALL still include the `.fx` in the content file.

### R13: Effect build check
**User story:** As a developer, I want shader errors reported per file with a hint, before the full build.

Acceptance criteria:
1. WHEN the migrator has written a migrated copy that contains `.fx` files and the user starts a build, THE runner SHALL compile each `.fx` with `mgfxc` 3.8.6.1 for the selected target (`OpenGL` for DesktopGL, `DirectX_11` for WindowsDX) before the content build.
2. THE runner SHALL write compiled output only to a temporary folder under the migrated copy's `obj` folder.
3. THE runner SHALL return, per effect, a status (Passed, Failed, Skipped), the escalation used, and diagnostics with file, line, column, code and message.
4. THE runner SHALL parse both error formats `path(line,col): error Xnnnn: text` and `path,(line,col) : text` and SHALL parse messages that have no location.
5. WHEN a DirectX compile fails with a shader-limit error (`X4505`, `X5608`, or another error that names `ps_2_0` or `vs_2_0` limits), THE runner SHALL retry with the next profile in the escalation ladder (`_4_0_level_9_1` to `_4_0_level_9_3` to `_4_0`), SHALL rewrite the migrated copy's profile macro, and SHALL add a finding with the final profile.
6. WHEN the escalation reaches `_4_0`, THE runner SHALL add a Warning that the game must use `GraphicsProfile.HiDef` at run time.
7. THE runner SHALL attach a fix hint to each diagnostic that matches the hint catalog.
8. IF `mgfxc` is not available (for example the tool is not restored, or the platform cannot run it), THEN THE runner SHALL mark the check Skipped with the reason and SHALL NOT fail the build.
9. THE runner SHALL NOT upload any shader or any other file.

### R14: Effect hints
**User story:** As a developer, I want plain advice for the common shader errors.

Acceptance criteria:
1. THE hint catalog SHALL contain hints for: fixed shader model on DirectX; shader model above 3.0 on OpenGL; shader instruction or register limits; a grammar parse error (`Unexpected token ... Expected ...`); the `VPOS` error (`X4576` on DirectX, `X4502` on OpenGL); unsupported pass state; annotations; and compile arguments.
2. THE hint for the fixed-shader-model error SHALL note that MonoGame prints the vertex shader's profile in the pixel shader message, so the profile name can be empty.
3. EACH hint SHALL have an id, a title, a summary, and a resolution, and SHALL be matched by code or message text in data (the hint catalog), not in code.

## Cross-cutting

### R15: Rules and scoring
**User story:** As a maintainer, I want the rule catalog to describe what is automatic.

Acceptance criteria:
1. THE catalog SHALL change as shown in the rule change table in `design.md`, one change per task, each with a positive and a negative fixture test.
2. WHEN a rule becomes AutoFixable, THE rule SHALL link to the migrator task that implements the fix.
3. THE spec SHALL add the match type `fileContent` only because no existing match type reads file text.

### R16: Safety and determinism
**User story:** As a maintainer, I want the safety rules to hold.

Acceptance criteria:
1. THE converter, rewriters and runner SHALL NOT modify the user's original project.
2. THE analyzer and readers SHALL open files read-only, and a test SHALL prove the file hashes of the `xna40-xact` and `xna40-effects` fixtures are unchanged after analysis.
3. THE system SHALL NOT upload user source code, audio or shaders anywhere.
4. THE system SHALL NOT overwrite an existing output folder without confirmation.
5. THE tests SHALL NOT use the network. Candidate games are checked by hand outside the repository, and nothing from them is copied into the repository.
6. GIVEN identical input and options, THE converter, catalog generator and rewriter SHALL produce byte-identical output.

### R17: Tests and fixtures
**User story:** As a maintainer, I want every rule proven.

Acceptance criteria:
1. THE fixtures SHALL be written from scratch (small, synthetic, original) and added under `tests/fixtures/` with a `README.md` that states they are original.
2. EACH new rule SHALL have a positive and a negative fixture test, and snapshot reports SHALL be reviewed.
3. THE tests SHALL write real WAV bytes at test time, so the repository stays text-only.
4. A slow integration test (skipped when the tool is missing) SHALL run `mgfxc` on the effect fixtures and `dotnet-mgcb` on the converted audio fixture.
