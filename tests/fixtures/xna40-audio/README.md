# xna40-audio

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.wav`, `.mp3` and `.wma` files are ASCII placeholders, not audio.

## Purpose

A tiny XNA 4.0 Windows game (HiDef profile) that loads sound effects and songs and plays them. `Game1` uses `Content.Load<SoundEffect>`, `Content.Load<Song>`, `SoundEffect.Play` and `MediaPlayer.Play`. All of these exist in MonoGame. The nested content project covers the audio importer and processor pairs:

- `Audio/beep.wav` with `WavImporter` and `SoundEffectProcessor`.
- `Audio/quiet.wav` with `WavImporter`, `SoundEffectProcessor` and `Quality` set to `Low`.
- `Audio/theme.mp3` with `Mp3Importer` and `SongProcessor`.
- `Audio/jingle.mp3` with `Mp3Importer` and `SoundEffectProcessor` (an mp3 loaded as a sound effect).
- `Audio/legacy.wma` with `WmaImporter` and `SongProcessor`.

It is never built.

## What the analyzer should find

- The project imports the XNA Game Studio 4.0 targets file and references the XNA Framework assemblies.
- The project has a nested content project.
- XNA019 for the `.wma` content item.
- No source-level findings.
