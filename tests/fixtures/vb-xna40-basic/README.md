# vb-xna40-basic

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license. The PNG file is an ASCII placeholder (a copy of the file in `xna40-basic`), not a real image.

## Purpose

A tiny Visual Basic XNA 4.0 game in the shape of the Game Studio 4.0 VB template: the VB project type GUID next to the XNA one, project-level `Import` items for the XNA namespaces (the code has no `Imports` lines), `MyType` Empty, the `Option*` properties, comma separated `DefineConstants`, a `ProjectReference` to the content project, and `Program.vb` with `Shared Sub Main` inside `#If WINDOWS OrElse XBOX Then`. It is never built.

## What the analyzer should find

- Language Visual Basic, XNA version 4.0, platform Windows.
- Project imports `Microsoft.Xna.Framework`, `Microsoft.Xna.Framework.Graphics` and `Microsoft.Xna.Framework.Input`; defines `WINDOWS`, `DEBUG` and `TRACE`.
- XNA001 fires once, for the XNA targets import and not for the namespace imports. XNA002, XNA003, XNA004, XNA047 (`NoStdLib`) and XNA049 (content project reference) fire too.
- No XNA 3.x API finding.
