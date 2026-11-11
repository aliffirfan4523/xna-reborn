# xna10-express-basic

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The two PNG files are copies of the tiny image in `xna40-basic`.

## Purpose

A tiny XNA Game Studio Express 1.0 Windows game, written the way the 1.0 template was: framework version `v1.0`, the Express project type GUID, the `Microsoft.Xna.Common.targets` and `Microsoft.Xna.ContentPipeline.targets` imports from the `Game Studio Express\v1.0` folder, no `TargetFrameworkVersion`, framework references without a version, and the content listed inside the game project. It is never built.

## What the analyzer should find

- XNA version 1.0, with no ANA002 conflict.
- XNA001 (Express targets imports), XNA002, XNA003, XNA100 (1.0 project) and XNA101 (content built inside the game project).
- Two content items from the project, marked in-project: `ship` with the legacy `SpriteTextureProcessor` and `backdrop` with `TextureProcessor`. XNA050 does not fire for the legacy processor.
- XNA102 and XNA103 for the `LoadGraphicsContent` and `UnloadGraphicsContent` overrides.
- XNA104 and XNA105 for `ResourceManagementMode` and `ResourceUsage`.
- XNA022 for `SpriteBlendMode` and XNA024 for `RenderState`.
