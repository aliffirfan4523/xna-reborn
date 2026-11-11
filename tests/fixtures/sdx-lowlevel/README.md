# sdx-lowlevel

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A SharpDX Toolkit 2.6.3 project that is hard to migrate. It uses `SharpDX.Direct3D11` and `SharpDX.DXGI` directly, `RectangleF`, `Color4`, `GeometricPrimitive`, `Matrix.LookAtLH`, `Vector3.TransformCoordinate`, `RenderTarget2D.New`, a depth-buffer `SetRenderTargets` and `DrawQuad`. It is never built.

## What the analyzer should find

- A Toolkit project.
- Manual and Unsupported source findings for each of the APIs listed above.