# xna40-custom-content

Synthetic fixture for the project analyzer.

- Original code written for this repository. No third-party code or assets.
- Released under the repository license.

## Purpose

A tiny XNA Game Studio 4.0 Windows game with a nested content project. The content project uses non-standard parts: a name override on a texture, a custom processor, an XACT sound bank, an effect, a font that names a font family not available outside XNA, and a reference to a custom content pipeline assembly.

The project is never built. The assets are placeholder text files.

## What the analyzer should find

- An XNA 4.0 game with a nested content project.
- Content items that use a name override, a custom processor, an XACT sound bank, an effect, and a sprite font.
- A custom pipeline assembly reference that is not present in the fixture.
- A font family that only exists on XNA-era systems.
