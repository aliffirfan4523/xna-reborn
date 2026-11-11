# device-xbox360-copy-solution

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A solution shaped like one that Game Studio's "Create Copy of Game for Xbox 360" command produces: a Windows project `Game` and `Xbox 360 Copy of Game`, whose three `Compile` items are links (`<Link>`) to the files of `Game`. `Program.cs` wraps `Main` in `#if WINDOWS || XBOX`. It is never built.

## What the analyzer should find

- XNA007 on the Xbox 360 project, and XNA052 (platform copy) on the same project only.
- XNA053 for the device symbols in `Program.cs`.
- No XNA052 and no XNA007 on the Windows project `Game`.

## Sources

The naming and linking follow the Microsoft documentation, [Adding New Platforms to an Existing Game](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb976061(v=xnagamestudio.41)).
