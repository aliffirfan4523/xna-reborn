# vb-monogame-already

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

The negative case for Visual Basic detection: an SDK-style Visual Basic project that already uses MonoGame 3.8.6.1 (DesktopGL). The code uses the `Microsoft.Xna.Framework` namespaces, which MonoGame keeps, but there is no XNA assembly reference, no `XnaFrameworkVersion` and no XNA targets. It is never built.

## What the analyzer should find

- The project is classified as an already-current MonoGame project, not as XNA (`IsXna` false). With the XNA catalog the report has no project and the reason says all MonoGame projects are current.
- No XNA rule fires.
