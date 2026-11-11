# vb-xna31-handmade

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A hand-made Visual Basic XNA 3.1 project: no XNA targets import, no project type GUID and no `XnaFrameworkVersion`. Plain `Microsoft.Xna.Framework` references with version 3.1 are the only XNA signal. It uses `MyType` WindowsForms, a stand-in `Application.Designer.vb`, `SpriteBlendMode`, a `With GraphicsDevice.RenderState` block, a call without parentheses (`effect.Begin`) and one `My.Application` call. It is never built.

## What the analyzer should find

- Language Visual Basic, XNA version 3.1 (from the references), not Unknown.
- Imports from the project: `Microsoft.VisualBasic`, `System`, `System.Collections.Generic`. Defines `DEBUG` and `TRACE` (the `=1` values are dropped).
- XNA003 for the references and XNA004 for the old .NET target. No XNA001 and no XNA002.
- Source findings in `Game1.vb` from the Visual Basic walker: XNA022 (`SpriteBlendMode`, three places), XNA024 (`RenderState` in the `With` block), XNA039 and XNA040 (`pass.Begin()` and `effect.Begin` without parentheses), XNA112 (`My.Application`).
- Project findings XNA110, XNA111 (`MyType` WindowsForms) and XNA113 (the designer file).
