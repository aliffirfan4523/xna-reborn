# xna20-vs2005

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The PNG file is a copy of the tiny image in `xna40-basic`.

## Purpose

A tiny XNA Game Studio 2.0 solution in the Visual Studio 2005 layout (solution format 9.00): a Windows game project, an Xbox 360 project that links the same source files, and a nested content project (`Content.contentproj`, type GUID `{96E2B04D-...}`). The code uses `LoadContent` (so the 2.0 method names are already in place) together with APIs that 4.0 removed. It is never built.

## What the analyzer should find

- Two projects, both XNA version 2.0, no ANA002 conflict. The Windows project has the nested content project and the `hero` texture with `TextureProcessor`.
- XNA001 (the `Microsoft.Xna.GameStudio.Common.targets` and `NestedContent.targets` imports), XNA002, XNA003, XNA005, XNA007 (Xbox 360 project) and XNA100.
- XNA022 for `SpriteBlendMode` and XNA024 for `RenderState`, plus the effect `Begin` rules.
- No XNA101 to XNA105: the 1.0-only content methods and resource modes are not used.
