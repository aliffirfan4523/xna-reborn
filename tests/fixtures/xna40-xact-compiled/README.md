# xna40-xact-compiled

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.xgs`, `.xwb` and `.xsb` files are one-line text placeholders, not XACT data.

## Purpose

The same game as `xna40-xact` but the project has only compiled XACT banks and no `.xap`. The code calls `AudioEngine`, `WaveBank` and `SoundBank` with the compiled file names. It is the "code present, source data absent" case: the converter can only copy the banks.

It is never built.

## What the analyzer should find

- XNA015 for the XACT types.
- No `.xap` file, so XNA016 stays silent.
- Compiled banks (rule added later in the audio-and-shaders spec).