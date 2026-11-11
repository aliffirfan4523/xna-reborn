# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 79 / 100 |
| Projected score after automatic fixes | 82 / 100 |
| Projects analyzed | 4 |
| Findings | 12 |

Severity: Blocker 0, Error 5, Warning 3, Info 4

Classification: AutoFixable 9, Manual 2, Unsupported 1

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Android/Mg37Android.csproj | MonoGameLegacy | - | Unknown | Android | - | - |
| Desktop/Mg37Desktop.csproj | MonoGameLegacy | - | Unknown | WindowsDX | - | - |
| Ios/Mg37Ios.csproj | MonoGameLegacy | - | Unknown | Ios | - | - |
| Uwp/Mg37Uwp.csproj | MonoGameLegacy | - | 3.3 to 3.8.1 (Medium) | Uwp | - | - |

## Findings

### AutoFixable

- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Android/Mg37Android.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Desktop/Mg37Desktop.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Ios/Mg37Ios.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Uwp/Mg37Uwp.csproj`
  - Code: `MonoGame.Framework`
- **[Warning] MG024** Content.mgcb platform is not valid for the 3.8.6.1 desktop targets
  - Project: `Uwp/Mg37Uwp.csproj`
  - Location: `Uwp/Content/Content.mgcb:6:1`
  - Code: `platform=WindowsStoreApp`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Android/Mg37Android.csproj`
  - Code: `MonoGamePlatform=Android`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Desktop/Mg37Desktop.csproj`
  - Code: `MonoGamePlatform=Windows`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Ios/Mg37Ios.csproj`
  - Code: `MonoGamePlatform=iOS`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Uwp/Mg37Uwp.csproj`
  - Code: `MonoGamePlatform=WindowsStoreApp`

### Manual

- **[Warning] MG012** Android project (Xamarin)
  - Project: `Android/Mg37Android.csproj`
  - Code: `ProjectTypeGuids={EFBA0AD7-5A72-4C68-AF49-83D382785DCF};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`
- **[Warning] MG013** iOS project (Xamarin)
  - Project: `Ios/Mg37Ios.csproj`
  - Code: `ProjectTypeGuids={FEACFBD2-3405-455C-9665-78FE426C6842};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`

### Unsupported

- **[Error] MG014** UWP / Windows Store project
  - Project: `Uwp/Mg37Uwp.csproj`
  - Code: `MonoGamePlatform=WindowsStoreApp`

## Rules

### MG003 MonoGame.Framework assembly reference (install folder or packages folder)

The project references MonoGame.Framework as an assembly with a HintPath into the installer folder or into a packages folder next to the solution. Neither exists on a clean machine, and the version is fixed by the path.

**Resolution:** The migrator replaces the reference with a PackageReference to MonoGame.Framework.DesktopGL or MonoGame.Framework.WindowsDX at 3.8.6.1, chosen from the old platform.

Migrator task: monogame-upgrade:11.1

### MG004 MonoGamePlatform build property

The project sets MonoGamePlatform, which only the old MonoGame installer targets read. It also drives the OutputPath pattern bin\$(MonoGamePlatform)\... and the platform that Content.mgcb is built for.

**Resolution:** The migrator drops the property. The target platform comes from the package that the migrated project references, and Content.mgcb gets the matching /platform.

Migrator task: monogame-upgrade:11.1

### MG012 Android project (Xamarin)

This is a Xamarin.Android project. This version of XNA Reborn does not generate Android output.

**Resolution:** The project is copied unchanged and listed as manual work (monogame-upgrade task 14.1). Move the shared game code to a desktop project and port the Android project to net8.0-android by hand.

### MG013 iOS project (Xamarin)

This is a Xamarin.iOS project. This version of XNA Reborn does not generate iOS output.

**Resolution:** The project is copied unchanged and listed as manual work (monogame-upgrade task 14.1). Move the shared game code to a desktop project and port the iOS project to net8.0-ios by hand.

### MG014 UWP / Windows Store project

This is a Windows Store or UWP project. MonoGame removed UWP support in 3.8.2, and there is no package for it in 3.8.6.1.

**Resolution:** The project is copied unchanged and listed as manual work (monogame-upgrade task 14.1). Move the shared game code to a desktop project.

### MG024 Content.mgcb platform is not valid for the 3.8.6.1 desktop targets

Content.mgcb is built for a platform that the 3.8.6.1 content builder does not accept or that is not a desktop target (WindowsStoreApp, WindowsPhone8, PlayStationMobile and PSVita were removed; MacOSX, NativeClient and RaspberryPi are not targets of the migrated copy).

**Resolution:** The migrator sets /platform to the platform of the chosen desktop target (Windows or DesktopGL).

Migrator task: monogame-upgrade:12.1
