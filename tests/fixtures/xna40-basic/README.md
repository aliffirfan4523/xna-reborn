# xna40-basic

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.png` file is an ASCII placeholder, not an image.

## Purpose

A tiny, clean XNA 4.0 Windows game (HiDef profile). `Game1` draws one texture with `SpriteBatch` and moves it with the keyboard. It uses only APIs that MonoGame also provides. It has a nested content project with one texture and one sprite font (Arial). It is never built.

## What the analyzer should find

- The project imports the XNA Game Studio 4.0 targets file.
- The project references the XNA Framework assemblies.
- The project has a nested content project.
- No source-level findings. All code APIs are supported by MonoGame, so this project should score high.