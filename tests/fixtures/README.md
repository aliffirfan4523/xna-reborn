# Fixtures

Small synthetic sample projects. Each folder has its own `README.md` that says it is original code under the repository license. Fixtures are never built or run.

## Older XNA fixtures

Fixtures for the `older-and-vb` spec (rules `XNA100` to `XNA105`). Each README lists the expected findings.

| Fixture | Shape |
|---|---|
| `xna10-express-basic` | Game Studio Express 1.0 C# game: Express project type GUID, `Game Studio Express\v1.0` targets, content listed inside the game project, `LoadGraphicsContent`, `ResourceManagementMode`, `SpriteBlendMode` |
| `xna10-clean` | Express 1.0 project that uses none of the removed APIs (negative) |
| `xna20-vs2005` | 2.0 solution in format 9.00: Windows game, Xbox 360 project, nested `Content.contentproj`, `RenderState`, `Effect.Begin` |

## Visual Basic fixtures

Fixtures for Part B of the `older-and-vb` spec.

| Fixture | Shape |
|---|---|
| `vb-xna40-basic` | VB 4.0 game: VB project type GUID, project-level `Import` items, `#If WINDOWS OrElse XBOX Then` around `Shared Sub Main`, comma separated defines, content project reference |
| `vb-xna31-handmade` | Hand-made VB 3.1 project: plain references only, `MyType` WindowsForms, `SpriteBlendMode`, `With ... RenderState`, `fx.Begin` without parentheses, one `My.Application` call |
| `vb-monogame-already` | SDK-style VB project on MonoGame 3.8.6.1 (negative: not XNA) |
| `mixed-cs-vb-sln` | Solution with a C# XNA game and a VB XNA library |
## MonoGame upgrade fixtures

Fixtures for the `monogame-upgrade` spec (rules `MG001` to `MG032`). The "Positive for" column lists the rules the fixture must trigger. `mg38-current` is the default negative and scores 100. A test checks that every folder is listed here with the same rules as `FixturePaths.MonoGamePositives`.

| Fixture | Shape | Positive for |
|---|---|---|
| `mg37-windowsdx-pkgconfig` | Installer imports, `packages.config`, SharpDX reference, `net47`, x86, a prebuilt `.xnb` | MG001, MG002, MG003, MG004, MG005, MG006, MG007, MG020, MG027 |
| `mg37-desktopgl-installdir` | `Assemblies\DesktopGL` HintPaths, `OpenTK` and `NVorbis` references, `Content.mgcb` with install-folder and custom pipeline references | MG001, MG002, MG003, MG004, MG005, MG006, MG017, MG019, MG023, MG025, MG026 |
| `mg34-windowsgl` | `Assemblies\WindowsGL`, `Tao.Sdl`, `MonoGame.Framework.Net` and `Lidgren.Network` references | MG003, MG004, MG011, MG018, MG021, MG022 |
| `mg35-win8-wp8-ouya` | Windows Phone 8 and OUYA platform projects, an mgcb with a legacy platform | MG015, MG016, MG024 |
| `mg37-multiplatform-sln` | Solution with shared project, desktop, Android, iOS and UWP projects | MG012, MG013, MG014, MG024 |
| `mg3x-protobuild` | `Protobuild.exe` placeholder and a `.definition` file, no `.csproj` | MG010 |
| `mg38-old-pin` | SDK-style `netcoreapp3.1` with MonoGame 3.8.0.1641 | MG008, MG009 |
| `mg38-current` | SDK-style `net8.0` with MonoGame 3.8.6.1 | none |
| `mg37-source-apis` | Source that uses `Net`, `GamerServices`, `OpenTK`, `Windows.UI.Xaml` and `Color.TransparentBlack` | MG028, MG029, MG030, MG031, MG032 |

## SharpDX Toolkit fixtures

Fixtures for the `sharpdx-toolkit` spec (rules `SDX001` to `SDX063`). They are original and synthetic. `.dds` and `.tkb` files are ASCII placeholders. A test checks that `FixturePaths.SharpDxNames` lists every folder.

| Fixture | Shape |
|---|---|
| `sdx-basic` | Toolkit 2.6.3 game: SharpDX references and targets imports, `packages.config`, `ToolkitFont` and `ToolkitTexture` items, RH matrices, `MathUtil`, state collections, `KeyboardManager` |
| `sdx-effects` | `ToolkitFxc` item with an original Toolkit-syntax `.fx`, `Texture2D.New`, `SetResource`, two-argument `Begin` |
| `sdx-lowlevel` | `SharpDX.Direct3D11` and `DXGI` use, `RectangleF`, `Color4`, `GeometricPrimitive`, LH matrices, `TransformCoordinate`, depth-buffer `SetRenderTargets`, `DrawQuad` |
| `sdx-mixed` | Solution with one Toolkit project and one XNA 4.0 project |
| `sdx-store-app` | Toolkit project with the Windows Store project type GUID |
| `sdx-prebuilt` | Compiled `.tkb` asset with no source |
| `sdx-negative` | Plain SharpDX (no Toolkit) next to a MonoGame project that uses the MonoGame spellings. No SDX rule fires |
