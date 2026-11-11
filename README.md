# XNA Reborn

A desktop tool that analyzes old Microsoft XNA game projects, migrates them to MonoGame, then builds and runs the result.

Built for the Kiroween hackathon, Resurrection category.

> Work in progress. A fuller README draft is in `.kiro/README.md` and will replace this stub before submission.

## Build and test

Requires the .NET SDK (8 or newer) and the .NET 8 runtime.

```
dotnet build
dotnet test
```

## Layout

```
src/XnaReborn.Core       shared models and services
src/XnaReborn.Analyzer   read-only analysis
rules/                   rule catalogs (data)
tests/                   unit tests and fixture projects
.kiro/                   specs, steering, hooks
```

More projects (Migrator, Runner, Repair, App) are added as their specs are implemented.

## Device projects (Xbox 360, Zune, Windows Phone)

A project whose `XnaPlatform` is a device platform is converted to a desktop game instead of being rejected. Platform copies of the same game (`Xbox 360 Copy of X`) are skipped and the Windows project is migrated. `#if XBOX` and `#if WINDOWS_PHONE` branches are folded for the migrated platform. Touch, tilt and the Back button are stood in for by the mouse and keyboard through the generated `XnaReborn.Compat` library. The migrated copy gets a `DEVICE-CONTROLS.md` that lists the keys and values. Phone choosers, the media library and ad assemblies are left as `// XNAREBORN:` markers. Design: `.kiro/specs/device-to-desktop`.

## SharpDX Toolkit projects

A project that uses the SharpDX Toolkit 2.x (`SharpDX.Toolkit.*` references or targets, or a `ToolkitFont`, `ToolkitTexture`, `ToolkitModel`, `ToolkitAudio` or `ToolkitFxc` item) is analyzed with its own catalog, `rules/sharpdx-toolkit-to-monogame.json` (SDX001 to SDX063), and migrated to MonoGame 3.8.6.1 on net8.0. WindowsDX is the default target for these projects.

Covered: the project file, the namespaces (`SharpDX.Toolkit`, `.Graphics`, `.Content`, `.Input` and the math types of `SharpDX`), the `KeyboardManager`, right-handed `Matrix` factories, `MathUtil`, the graphics state collections and `Set*State` calls, `BackBuffer` size, `ToDisposeContent`, and the content (`TkFont` fonts, textures, `.fbx` and `.x` models, `.wav` sounds). Everything else is marked in the source with `// XNAREBORN: SDXnnn:` and listed in the manifest.

Not covered: Toolkit effects (`.fx`) and compiled `.tkb` files, raw `SharpDX.Direct3D11`, `DXGI`, `Direct2D1` and `XAudio2` code, left-handed matrices, `RectangleF` and `Color4`, mouse and touch managers, `GameSystem`, `GeometricPrimitive`, and Windows Store, Windows Phone and UWP projects (refused, no copy is made). A migrated Toolkit game was built but never run. Details and the results on real projects: `.kiro/specs/sharpdx-toolkit/design.md`.
## Safety

The original XNA project is never modified. All output goes to a new folder.

## License

Not chosen yet. See `LICENSE`.

## Flixel 2.x source projects (Flash)

A folder with ActionScript 3 sources that use Flixel 2.x (`org.flixel`) is analyzed with its own catalog, `rules/flash-to-monogame.json` (FL001 to FL060), and converted to a MonoGame 3.8.6.1 project on net8.0 DesktopGL. The converter writes C# for the game classes, a `Content.mgcb` for `[Embed]` pictures and sounds, and a copy of the `XnaReborn.Flixel` runtime project next to the game.

Covered: AS3 classes, getters and setters, `for each`, closures, `Vector.<T>`, `Object` bags, class values, `int` division and `Number` defaults, Flixel sprites, groups, states, text (built-in pixel font), tile blocks, collision and overlap, keyboard input and simple sounds.

Not covered: compiled `.swf` files without source (never opened), Flash Pro `.fla` timelines, E4X XML, `with`, namespaces, regular expressions, `flash.display` outside Flixel, tilemaps, emitters, cameras, timers, ad and score SDKs. Each of these leaves a `// XNAREBORN: FLnnn` marker in the converted code and is counted in `manifest.json`. A converted game is built but was not started in testing. Design and notes: `.kiro/specs/flash-to-monogame`. Third-party notices: `THIRD-PARTY-NOTICES.md`.
