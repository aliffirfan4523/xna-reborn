# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 87 / 100 |
| Projected score after automatic fixes | 96 / 100 |
| Projects analyzed | 1 |
| Findings | 6 |

Severity: Blocker 0, Error 4, Warning 1, Info 1

Classification: AutoFixable 5, Manual 0, Unsupported 1

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| SdxPrebuilt.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `SdxPrebuilt.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `SdxPrebuilt.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `SdxPrebuilt.csproj`
  - Location: `Game1.cs:1:1`
  - Code: `using SharpDX.Toolkit;`
- **[Error] SDX021** Toolkit graphics namespace
  - Project: `SdxPrebuilt.csproj`
  - Location: `Game1.cs:2:1`
  - Code: `using SharpDX.Toolkit.Graphics;`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `SdxPrebuilt.csproj`
  - Code: `TargetFrameworkVersion=v4.5`

### Unsupported

- **[Warning] SDX015** Prebuilt .tkb asset
  - Project: `SdxPrebuilt.csproj`
  - Code: `Content/Balls.tkb`

## Rules

### SDX001 SharpDX assembly reference

The project references SharpDX assemblies (SharpDX, SharpDX.Toolkit.*, SharpDX.Direct3D11 and others). They come from the SharpDX 2.x NuGet packages, which target .NET Framework 4.x and need the SharpDX build targets. They are not part of MonoGame.

**Resolution:** The migrator writes an SDK-style project that drops every SharpDX reference and references MonoGame instead. Code that used SharpDX types directly is reported by the SDX005 to SDX008 and SDX034 to SDX035 rules.

Migrator task: sharpdx-toolkit:8.1

### SDX002 SharpDX build targets import

The project imports SharpDX.targets or SharpDX.Toolkit.targets from the NuGet packages folder. Those files set the reference paths and run the Toolkit content compilers. They exist only after a NuGet restore of the old packages, and a build without them stops with MSB4019.

**Resolution:** The migrator drops the imports, the NuGet restore guard target and the import stamp from the SDK-style project it writes.

Migrator task: sharpdx-toolkit:8.1

### SDX003 .NET Framework 4.x target

The project targets .NET Framework 4.x (for example v4.5). MonoGame 3.8.6.1 desktop projects target net8.0.

**Resolution:** The migrator writes an SDK-style project for net8.0 (or the Windows variant for WindowsDX).

Migrator task: sharpdx-toolkit:8.1

### SDX015 Prebuilt .tkb asset

The project folder holds compiled .tkb files. When the source asset is not in the folder, nothing can rebuild the asset for MonoGame, and MonoGame has no importer for the .tkb format.

**Resolution:** Find the original image, font, model or sound and add it to the project, or leave the asset out. The migrator copies .tkb files unchanged and flags them.

### SDX020 Toolkit game namespace

The code imports SharpDX.Toolkit, which holds Game, GameTime and GraphicsDeviceManager. The same names live in Microsoft.Xna.Framework in MonoGame. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2

### SDX021 Toolkit graphics namespace

The code imports SharpDX.Toolkit.Graphics (SpriteBatch, Texture2D, SpriteFont, BasicEffect). MonoGame keeps these names in Microsoft.Xna.Framework.Graphics. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.Graphics.

Migrator task: sharpdx-toolkit:5.2
