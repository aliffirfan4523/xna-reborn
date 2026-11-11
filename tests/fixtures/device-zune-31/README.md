# device-zune-31

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A tiny XNA 3.1 Zune project: `XnaPlatform` is `Zune`, `DefineConstants` has `ZUNE`, and the code uses `GamePad.GetState` and a `MediaLibrary`. It is never built.

## What the analyzer should find

- XNA006 (XNA 3.1) and XNA007 (Zune device project).
- XNA053 (the `ZUNE` symbol), XNA056 (media library) and XNA062 (`GamePad.GetState`).

## Sources

The Zune project shape follows the Microsoft documentation, [Migrating from Zune to Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff604985(v=xnagamestudio.41)). No code was copied from any game.
