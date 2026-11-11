# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 88 / 100 |
| Projected score after automatic fixes | 96 / 100 |
| Projects analyzed | 1 |
| Findings | 9 |

Severity: Blocker 0, Error 3, Warning 4, Info 2

Classification: AutoFixable 7, Manual 2, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Mg37WindowsDx.csproj | MonoGameLegacy | - | 3.7.0.1708 (High) | WindowsDX | - | v4.7 |

## Findings

### AutoFixable

- **[Error] MG001** Import of MonoGame.Content.Builder.targets (installer targets)
  - Project: `Mg37WindowsDx.csproj`
  - Code: `$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Content.Builder.targets`
- **[Error] MG002** Import of MonoGame.Common.props (installer props)
  - Project: `Mg37WindowsDx.csproj`
  - Code: `$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Common.props`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Mg37WindowsDx.csproj`
  - Code: `MonoGame.Framework`
- **[Warning] MG005** .NET Framework 4.x target (TargetFrameworkVersion)
  - Project: `Mg37WindowsDx.csproj`
  - Code: `TargetFrameworkVersion=v4.7`
- **[Warning] MG006** x86-only build (PlatformTarget x86)
  - Project: `Mg37WindowsDx.csproj`
  - Code: `PlatformTarget=x86`
- **[Warning] MG007** packages.config NuGet list
  - Project: `Mg37WindowsDx.csproj`
  - Code: `packages.config`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Mg37WindowsDx.csproj`
  - Code: `MonoGamePlatform=Windows`

### Manual

- **[Warning] MG020** SharpDX assembly references
  - Project: `Mg37WindowsDx.csproj`
  - Code: `SharpDX`
- **[Info] MG027** Prebuilt .xnb files
  - Project: `Mg37WindowsDx.csproj`
  - Code: `Content/Prebuilt/logo.xnb`

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

### MG007 packages.config NuGet list

The project lists its NuGet packages in packages.config. SDK-style projects use PackageReference items instead, and the MonoGame and SharpDX packages in the list are replaced by the 3.8.6.1 package.

**Resolution:** The migrator turns each package that is not MonoGame or SharpDX into a PackageReference with the same id and version. A package it cannot map is listed as manual work. It does not call the network.

Migrator task: monogame-upgrade:11.3

### MG020 SharpDX assembly references

The project references SharpDX assemblies directly. MonoGame.Framework.WindowsDX 3.8.6.1 depends on SharpDX 4.2.0, so a migrated project gets SharpDX from the package. The analyzer cannot tell whether your code uses SharpDX types itself.

**Resolution:** The migrator drops the references that the package brings in (monogame-upgrade task 11.1). If your code uses SharpDX directly, check that it builds against SharpDX 4.2.0.

### MG027 Prebuilt .xnb files

The project folder contains prebuilt .xnb files. They were built for one MonoGame version and platform, and a different MonoGame version can refuse to load them. XNA Reborn cannot rebuild an .xnb without its source asset.

**Resolution:** The migrator copies the files unchanged and lists them as manual work (monogame-upgrade task 12.3). If the game fails to load them, rebuild them from the source assets.
