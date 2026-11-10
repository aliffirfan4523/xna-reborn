# xna40-prebuilt

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.xnb`, `.dat` and `.txt` files are ASCII placeholders, not real content.

## Purpose

A tiny XNA 4.0 Windows game (HiDef profile) with NO content project. The game project itself ships already built content as items with `CopyToOutputDirectory`:

- `Content\hero.xnb` as a `Content` item (`PreserveNewest`).
- `Content\sheet.xnb` as a `None` item (`Always`).
- `Data\levels.txt` as a `Content` item (`PreserveNewest`).

`Game1` loads `hero` through the content manager and reads `Data\levels.txt` with `File.ReadAllLines` relative to the game folder. It is never built.

## What the analyzer should find

- The project imports the XNA Game Studio 4.0 targets file and references the XNA Framework assemblies.
- XNA018 for the prebuilt `.xnb` files.
- No nested content project and no source-level findings.
