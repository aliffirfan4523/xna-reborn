# xna40-xact

Original synthetic code and data written for this repository. It contains no third-party code or assets and is released under the repository license. There are no audio files in the folder: the `.wav` files that `Content/Sounds.xap` names are generated from bytes by the tests that need them.

## Purpose

A tiny XNA 4.0 Windows game that plays cues from an XACT project. `Game1` builds an `AudioEngine`, two `WaveBank` objects and a `SoundBank`, then uses `GetCue`, `Cue.Play`, `SoundBank.PlayCue` and `AudioEngine.Update`. The nested content project has one `Sounds.xap` item (`XactImporter`, `XactProcessor`).

`Content/Sounds.xap` covers:

- Two categories (`Default`, and `Music` with an instance limit of 1 and a crossfade) and three variables (one global, one internal and read only).
- One RPC (`DistanceFade`) with two points on a volume curve.
- `Wave Bank`: three waves. `hit` and `hit_alt` point to the same file, `coin` has a compression preset.
- `Music Bank`: one wave in a streaming bank (`Streaming = 1`).
- `Extra Bank`: one wave (`ghost`) whose file is never created, so it is missing on purpose.
- Sound bank cues: `Hit` (single wave), `Step` (two sounds, random variation), `Theme` (loops forever, music category), `Layered` (two tracks, a finite loop, an RPC, a pitch), `Echo` (a reverb block that is not converted), `Ghost` (missing wave).

It is never built.

## What the analyzer should find

- XNA015 for `AudioEngine`, `WaveBank` and `SoundBank`.
- XNA016 for `Sounds.xap`.
- Streaming and unsupported XACT features (rules added later in the audio-and-shaders spec).