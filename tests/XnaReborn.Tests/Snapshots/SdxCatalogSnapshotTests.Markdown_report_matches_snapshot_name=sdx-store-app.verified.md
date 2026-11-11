# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 64 / 100 |
| Projected score after automatic fixes | 70 / 100 |
| Projects analyzed | 1 |
| Findings | 5 |

Severity: Blocker 1, Error 3, Warning 0, Info 1

Classification: AutoFixable 4, Manual 0, Unsupported 1

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| SdxStoreApp.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `SdxStoreApp.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `SdxStoreApp.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `SdxStoreApp.csproj`
  - Location: `Game1.cs:1:1`
  - Code: `using SharpDX.Toolkit;`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `SdxStoreApp.csproj`
  - Code: `TargetFrameworkVersion=v4.5`

### Unsupported

- **[Blocker] SDX004** Windows Store or Windows Phone project
  - Project: `SdxStoreApp.csproj`
  - Code: `ProjectTypeGuids={BC8A1FFA-BEE3-4634-8014-F334798102B3};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`

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

### SDX004 Windows Store or Windows Phone project

The project type GUIDs include the Windows Store app type (BC8A1FFA), the Windows Phone 8 type (C089C8C0), the Windows Phone 8.1 type (76F1466A) or the Universal Windows type (A5A43C5B). The first two are the non-desktop project types of the Toolkit project template (read from ProjectTemplate.csproj in the Toolkit repository). The last two were found on real Toolkit projects. MonoGame desktop cannot host any of them.

**Resolution:** Migrate the game code into a desktop Toolkit project first, or port it by hand. XNA Reborn refuses these projects and creates no copy for them.

### SDX020 Toolkit game namespace

The code imports SharpDX.Toolkit, which holds Game, GameTime and GraphicsDeviceManager. The same names live in Microsoft.Xna.Framework in MonoGame. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2
