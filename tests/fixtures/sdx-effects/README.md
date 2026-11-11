# sdx-effects

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A SharpDX Toolkit 2.6.3 project with one `ToolkitFxc` item. `Tint.fx` is an original effect in Toolkit syntax (`Profile = 9.1;`, `PixelShader = PSTint;`, `Texture2D<float4>`, a separate `SamplerState`). The code also uses `Texture2D.New`, `SetResource` and `SpriteBatch.Begin(sortMode, effect)`. It is never built.

## What the analyzer should find

- A Toolkit project with a `ToolkitFxc` and a `ToolkitFont` item.
- Source uses of `Texture2D.New`, `SetResource` and the two-argument `Begin`.