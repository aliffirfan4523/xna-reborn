# xna31-legacy

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A tiny XNA 3.1 Windows game (.NET 3.5, XNA Game Studio 3.1 targets, no content project). `Game1` uses several APIs that exist in 3.1 but changed or were removed in 4.0. It is never built.

3.1-only APIs used in `Game1.cs`:

- `SpriteBatch.Begin(SpriteBlendMode.AlphaBlend)` (also the three-argument overload with `SaveStateMode`)
- `SpriteBlendMode`
- `SaveStateMode`
- `GraphicsDevice.RenderState.AlphaBlendEnable`
- `GraphicsDevice.ResolveBackBuffer(...)` (with `ResolveTexture2D`)
- `Texture2D.FromFile(GraphicsDevice, string)`
- `new VertexDeclaration(GraphicsDevice, ...)` (also `GraphicsDevice.VertexDeclaration`)

## What the analyzer should find

- The project targets XNA 3.1 (framework version 3.1, 3.1 targets import, 3.1 references).
- Source-level findings for each API listed above, because they need rewriting for 4.0 or MonoGame.
- No nested content project.