# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 83 / 100 |
| Projected score after automatic fixes | 94 / 100 |
| Projects analyzed | 1 |
| Findings | 11 |

Severity: Blocker 0, Error 4, Warning 5, Info 2

Classification: AutoFixable 9, Manual 2, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Mg37DesktopGl.csproj | MonoGameLegacy | - | 3.5 or later (Medium) | DesktopGL | - | v4.5.2 |

## Findings

### AutoFixable

- **[Error] MG001** Import of MonoGame.Content.Builder.targets (installer targets)
  - Project: `Mg37DesktopGl.csproj`
  - Code: `$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Content.Builder.targets`
- **[Error] MG002** Import of MonoGame.Common.props (installer props)
  - Project: `Mg37DesktopGl.csproj`
  - Code: `$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Common.props`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Mg37DesktopGl.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG023** Content.mgcb references the MonoGame install folder
  - Project: `Mg37DesktopGl.csproj`
  - Location: `Content/Content.mgcb:13:1`
  - Code: `reference=..\..\..\..\Program Files (x86)\MonoGame\v3.0\Tools\MonoGame.Content.Pipeline.dll`
- **[Warning] MG005** .NET Framework 4.x target (TargetFrameworkVersion)
  - Project: `Mg37DesktopGl.csproj`
  - Code: `TargetFrameworkVersion=v4.5.2`
- **[Warning] MG006** x86-only build (PlatformTarget x86)
  - Project: `Mg37DesktopGl.csproj`
  - Code: `PlatformTarget=x86`
- **[Warning] MG017** OpenTK assembly reference
  - Project: `Mg37DesktopGl.csproj`
  - Code: `OpenTK`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Mg37DesktopGl.csproj`
  - Code: `MonoGamePlatform=DesktopGL`
- **[Info] MG019** NVorbis assembly reference
  - Project: `Mg37DesktopGl.csproj`
  - Code: `NVorbis`

### Manual

- **[Warning] MG025** Content.mgcb loads a custom pipeline assembly
  - Project: `Mg37DesktopGl.csproj`
  - Location: `Content/Content.mgcb:14:1`
  - Code: `reference=Pipeline\GlowPipeline.dll`
- **[Warning] MG026** Content.mgcb uses a non-standard importer or processor
  - Project: `Mg37DesktopGl.csproj`
  - Location: `Content/Content.mgcb:26:1`
  - Code: `Data/level.lvl (LevelImporter/LevelProcessor)`

## Rules

### MG001 Import of MonoGame.Content.Builder.targets (installer targets)

The project imports MonoGame.Content.Builder.targets from the MonoGame installer folder (MonoGame\v3.0). The installer is gone and the file does not exist on a machine with only the .NET SDK, so the build stops with MSB4019 (imported project not found).

**Resolution:** The migrator writes an SDK-style project that references the MonoGame.Content.Builder.Task package instead of importing the installer targets.

Migrator task: monogame-upgrade:11.1

### MG002 Import of MonoGame.Common.props (installer props)

The project imports MonoGame.Common.props from the MonoGame installer folder. The file sets MonoGameInstallDirectory for the old HintPath references. Without the installer the import fails with MSB4019.

**Resolution:** The migrator drops the import. The SDK-style project gets MonoGame from NuGet and needs no install folder.

Migrator task: monogame-upgrade:11.1

### MG003 MonoGame.Framework assembly reference (install folder or packages folder)

The project references MonoGame.Framework as an assembly with a HintPath into the installer folder or into a packages folder next to the solution. Neither exists on a clean machine, and the version is fixed by the path.

**Resolution:** The migrator replaces the reference with a PackageReference to MonoGame.Framework.DesktopGL or MonoGame.Framework.WindowsDX at 3.8.6.1, chosen from the old platform.

Migrator task: monogame-upgrade:11.1

### MG004 MonoGamePlatform build property

The project sets MonoGamePlatform, which only the old MonoGame installer targets read. It also drives the OutputPath pattern bin\$(MonoGamePlatform)\... and the platform that Content.mgcb is built for.

**Resolution:** The migrator drops the property. The target platform comes from the package that the migrated project references, and Content.mgcb gets the matching /platform.

Migrator task: monogame-upgrade:11.1

### MG005 .NET Framework 4.x target (TargetFrameworkVersion)

The project targets .NET Framework 2.0 to 4.x. MonoGame 3.8.6.1 supports only modern .NET, and a .NET Framework project cannot reference the MonoGame 3.8 packages.

**Resolution:** The migrator writes TargetFramework net8.0 (DesktopGL) or net8.0-windows (WindowsDX).

Migrator task: monogame-upgrade:11.1

### MG006 x86-only build (PlatformTarget x86)

The project builds for x86 only, as the 3.x templates did. The 3.8 desktop packages run as x64 or AnyCPU, and an x86 build of a net8.0 game needs a 32-bit runtime that most machines do not have.

**Resolution:** The migrator drops PlatformTarget so the game builds for the default architecture.

Migrator task: monogame-upgrade:11.1

### MG017 OpenTK assembly reference

The project references OpenTK next to MonoGame. Since 3.6 MonoGame uses its own OpenGL bindings, and the 3.8 DesktopGL package does not ship OpenTK.

**Resolution:** The migrator drops the reference. Code that uses OpenTK types directly is reported by MG030.

Migrator task: monogame-upgrade:11.1

### MG019 NVorbis assembly reference

The project references NVorbis for Ogg playback, as the old DesktopGL layout required. The 3.8 DesktopGL package includes Ogg support.

**Resolution:** The migrator drops the reference.

Migrator task: monogame-upgrade:11.1

### MG023 Content.mgcb references the MonoGame install folder

Content.mgcb has a /reference line that points into the MonoGame installer folder. That folder does not exist on a machine without the installer, so the content build fails to load the pipeline assembly.

**Resolution:** The migrator removes the install-folder /reference lines. The content builder package provides the pipeline.

Migrator task: monogame-upgrade:12.1

### MG025 Content.mgcb loads a custom pipeline assembly

Content.mgcb loads a pipeline extension assembly that is not part of MonoGame. An extension built for an older MonoGame usually fails to load in the 3.8.6.1 content builder and has to be rebuilt against it.

**Resolution:** The migrator copies the assembly and keeps the /reference line, then lists it as manual work (monogame-upgrade task 12.2). Rebuild the extension against MonoGame 3.8.6.1.

### MG026 Content.mgcb uses a non-standard importer or processor

Some content items use an importer or processor that is not in the stock list, so it comes from a custom pipeline assembly. Those items do not build until the assembly is rebuilt for MonoGame 3.8.6.1.

**Resolution:** The migrator keeps the entries unchanged and lists them as manual work (monogame-upgrade task 12.2). Rebuild the extension that provides the importer or processor.
