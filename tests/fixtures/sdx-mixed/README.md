# sdx-mixed

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The `.dds` file is an ASCII placeholder. The `Xna` project is a copy of the `xna40-basic` fixture.

## Purpose

A solution with one SharpDX Toolkit project (`Toolkit`) and one XNA 4.0 project (`Xna`). It proves that each project gets the handling for its own framework. It is never built.

## What the analyzer should find

- Two migratable projects: one Toolkit project and one XNA project.
- Toolkit rules fire only on the Toolkit project. XNA rules fire only on the XNA project.