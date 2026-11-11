# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 73 / 100 |
| Projected score after automatic fixes | 76 / 100 |
| Projects analyzed | 2 |
| Findings | 7 |

Severity: Blocker 0, Error 4, Warning 1, Info 2

Classification: AutoFixable 5, Manual 0, Unsupported 2

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Ouya/Mg35Ouya.csproj | MonoGameLegacy | - | Unknown | Ouya | - | - |
| Phone/Mg35Phone.csproj | MonoGameLegacy | - | Unknown | WindowsPhone | - | - |

## Findings

### AutoFixable

- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Ouya/Mg35Ouya.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Phone/Mg35Phone.csproj`
  - Code: `MonoGame.Framework`
- **[Warning] MG024** Content.mgcb platform is not valid for the 3.8.6.1 desktop targets
  - Project: `Phone/Mg35Phone.csproj`
  - Location: `Phone/Content/Content.mgcb:6:1`
  - Code: `platform=WindowsPhone8`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Ouya/Mg35Ouya.csproj`
  - Code: `MonoGamePlatform=Ouya`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Phone/Mg35Phone.csproj`
  - Code: `MonoGamePlatform=WindowsPhone8`

### Unsupported

- **[Error] MG015** Windows Phone 8 project
  - Project: `Phone/Mg35Phone.csproj`
  - Code: `MonoGamePlatform=WindowsPhone8`
- **[Error] MG016** OUYA, PlayStation Mobile or PS Vita project
  - Project: `Ouya/Mg35Ouya.csproj`
  - Code: `MonoGamePlatform=Ouya`

## Rules

### MG003 MonoGame.Framework assembly reference (install folder or packages folder)

The project references MonoGame.Framework as an assembly with a HintPath into the installer folder or into a packages folder next to the solution. Neither exists on a clean machine, and the version is fixed by the path.

**Resolution:** The migrator replaces the reference with a PackageReference to MonoGame.Framework.DesktopGL or MonoGame.Framework.WindowsDX at 3.8.6.1, chosen from the old platform.

Migrator task: monogame-upgrade:11.1

### MG004 MonoGamePlatform build property

The project sets MonoGamePlatform, which only the old MonoGame installer targets read. It also drives the OutputPath pattern bin\$(MonoGamePlatform)\... and the platform that Content.mgcb is built for.

**Resolution:** The migrator drops the property. The target platform comes from the package that the migrated project references, and Content.mgcb gets the matching /platform.

Migrator task: monogame-upgrade:11.1

### MG015 Windows Phone 8 project

This is a Windows Phone 8 project. MonoGame 3.7 removed Windows Phone 8.1 support and the content builder no longer accepts the WindowsPhone8 platform.

**Resolution:** The project is copied unchanged and listed as manual work (monogame-upgrade task 14.1). Move the shared game code to a desktop project.

### MG016 OUYA, PlayStation Mobile or PS Vita project

This project targets a platform that MonoGame no longer supports: OUYA (removed in 3.6), PlayStation Mobile (removed in 3.5) or PS Vita (removed in 3.8.1).

**Resolution:** The project is copied unchanged and listed as manual work (monogame-upgrade task 14.1). Move the shared game code to a desktop project.

### MG024 Content.mgcb platform is not valid for the 3.8.6.1 desktop targets

Content.mgcb is built for a platform that the 3.8.6.1 content builder does not accept or that is not a desktop target (WindowsStoreApp, WindowsPhone8, PlayStationMobile and PSVita were removed; MacOSX, NativeClient and RaspberryPi are not targets of the migrated copy).

**Resolution:** The migrator sets /platform to the platform of the chosen desktop target (Windows or DesktopGL).

Migrator task: monogame-upgrade:12.1
