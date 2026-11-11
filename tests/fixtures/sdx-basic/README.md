# sdx-basic

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.dds` file is an ASCII placeholder, not an image.

## Purpose

A tiny SharpDX Toolkit 2.6.3 game in the shape of the Toolkit project template. `Game1 : Game` draws a texture and text with `SpriteBatch`, uses the SharpDX math types (`Matrix.LookAtRH`, `MathUtil`), the Toolkit state collections, `SetBlendState`, `ToDisposeContent` and a `KeyboardManager`. The content is one `ToolkitFont` and one `ToolkitTexture` item. It is never built.

## What the analyzer should find

- A Toolkit project (SharpDX.Toolkit references, both SharpDX targets imports, `packages.config`).
- Build actions `ToolkitFont` and `ToolkitTexture`.
- Source uses of the Toolkit namespaces, `KeyboardManager`, RH matrix factories, `MathUtil`, state collections and `Set*State`.