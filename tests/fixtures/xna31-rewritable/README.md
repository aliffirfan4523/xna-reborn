# xna31-rewritable

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A tiny XNA 3.1 Windows game (.NET 3.5, XNA Game Studio 3.1 targets, no content project, no content files). It is the demo input for the migrator: after the SDK project, the entry point unwrap, and the SpriteBatch, RenderState and EffectPass rewriters have run, it compiles against MonoGame.Framework.DesktopGL 3.8.6.1 with no other edits. It is never built as XNA.

The sprite texture is created in code, and `Program.cs` follows the XNA template with the class inside `#if WINDOWS || XBOX`.

3.1-only APIs used in `Game1.cs` (each is rewritten automatically):

- `SpriteBatch.Begin(SpriteBlendMode.AlphaBlend)`
- `SpriteBatch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.None)`
- `GraphicsDevice.RenderState.AlphaBlendEnable` and `GraphicsDevice.RenderState.CullMode`
- `Effect.Begin()` / `End()` and `EffectPass.Begin()` / `End()` in `DrawWithEffect`, which is never called and never constructs an `Effect`

## What the analyzer should find

- The project targets XNA 3.1.
- Source findings only for the APIs above, plus project-level findings (targets import, XNA properties and references, old .NET target, 3.1 version, AssemblyInfo).
- No nested content project.
