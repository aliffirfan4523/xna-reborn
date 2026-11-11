# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 61 / 100 |
| Projected score after automatic fixes | 65 / 100 |
| Projects analyzed | 1 |
| Findings | 7 |

Severity: Blocker 0, Error 5, Warning 1, Info 1

Classification: AutoFixable 3, Manual 2, Unsupported 2

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Mg37SourceApis.csproj | MonoGameLegacy | - | Unknown | WindowsDX | - | - |

## Findings

### AutoFixable

- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Mg37SourceApis.csproj`
  - Code: `MonoGame.Framework`
- **[Error] MG032** Color.TransparentBlack (removed in 3.8.1)
  - Project: `Mg37SourceApis.csproj`
  - Location: `Colors.cs:8:39`
  - Code: `public Color Clear { get; } = Color.TransparentBlack;`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Mg37SourceApis.csproj`
  - Code: `MonoGamePlatform=Windows`

### Manual

- **[Error] MG030** OpenTK API usage
  - Project: `Mg37SourceApis.csproj`
  - Location: `GlCode.cs:1:1`
  - Code: `using OpenTK;`
- **[Warning] MG031** UWP API usage (Windows.UI.Xaml)
  - Project: `Mg37SourceApis.csproj`
  - Location: `UwpCode.cs:1:1`
  - Code: `using Windows.UI.Xaml;`

### Unsupported

- **[Error] MG028** Microsoft.Xna.Framework.Net usage
  - Project: `Mg37SourceApis.csproj`
  - Location: `NetCode.cs:1:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] MG029** Microsoft.Xna.Framework.GamerServices usage
  - Project: `Mg37SourceApis.csproj`
  - Location: `Services.cs:1:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`

## Rules

### MG003 MonoGame.Framework assembly reference (install folder or packages folder)

The project references MonoGame.Framework as an assembly with a HintPath into the installer folder or into a packages folder next to the solution. Neither exists on a clean machine, and the version is fixed by the path.

**Resolution:** The migrator replaces the reference with a PackageReference to MonoGame.Framework.DesktopGL or MonoGame.Framework.WindowsDX at 3.8.6.1, chosen from the old platform.

Migrator task: monogame-upgrade:11.1

### MG004 MonoGamePlatform build property

The project sets MonoGamePlatform, which only the old MonoGame installer targets read. It also drives the OutputPath pattern bin\$(MonoGamePlatform)\... and the platform that Content.mgcb is built for.

**Resolution:** The migrator drops the property. The target platform comes from the package that the migrated project references, and Content.mgcb gets the matching /platform.

Migrator task: monogame-upgrade:11.1

### MG028 Microsoft.Xna.Framework.Net usage

The code uses the Microsoft.Xna.Framework.Net namespace (network sessions). It moved to MonoGame.Framework.Net in 3.3 and MonoGame 3.8 removed it.

**Resolution:** Replace the networking code with your own or a NuGet networking library. Stand-in types can be added with the compatibility shims (monogame-upgrade task 13.2).

### MG029 Microsoft.Xna.Framework.GamerServices usage

The code uses the Microsoft.Xna.Framework.GamerServices namespace (Guide, SignedInGamer). It moved to MonoGame.Framework.Net in 3.3 and MonoGame 3.8 removed it.

**Resolution:** Remove the calls or use the compatibility shims (monogame-upgrade task 13.2).

### MG030 OpenTK API usage

The code uses OpenTK types directly. The 3.8 DesktopGL package does not ship OpenTK, so these types no longer exist.

**Resolution:** Replace the OpenTK calls with MonoGame APIs or a NuGet OpenTK package of your choice. The migrator leaves a marker at each use (monogame-upgrade task 13.4).

### MG031 UWP API usage (Windows.UI.Xaml)

The code uses UWP types from Windows.UI.Xaml. They exist only in a UWP project, and the migrated desktop project does not target UWP.

**Resolution:** Move this code into the platform project, which is copied unchanged, or replace it with a desktop equivalent. The migrator leaves a marker at each use (monogame-upgrade task 13.4).

### MG032 Color.TransparentBlack (removed in 3.8.1)

Color.TransparentBlack was marked obsolete in MonoGame 3.8 and removed in 3.8.1. It and Color.Transparent are both (0, 0, 0, 0), so the replacement does not change behavior.

**Resolution:** The migrator replaces Color.TransparentBlack with Color.Transparent in files that import Microsoft.Xna.Framework.

Migrator task: monogame-upgrade:13.1
