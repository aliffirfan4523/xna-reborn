# sdx-negative

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

The negative case for Toolkit detection and rules. `PlainSharpDx` uses SharpDX and `SharpDX.Direct3D11` directly but no Toolkit. `MonoGameGame` is a MonoGame project that already uses `Matrix.CreateLookAt`, `BlendState.Opaque` and `Keyboard.GetState()`. Neither project is built.

## What the analyzer should find

- No Toolkit project, and no SDX finding.