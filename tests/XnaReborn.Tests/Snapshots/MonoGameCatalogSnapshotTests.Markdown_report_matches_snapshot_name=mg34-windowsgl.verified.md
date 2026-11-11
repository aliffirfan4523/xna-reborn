# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 81 / 100 |
| Projected score after automatic fixes | 85 / 100 |
| Projects analyzed | 1 |
| Findings | 7 |

Severity: Blocker 0, Error 2, Warning 4, Info 1

Classification: AutoFixable 5, Manual 1, Unsupported 1

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Mg34WindowsGl.csproj | MonoGameLegacy | - | 3.2 to 3.6 (Medium) | WindowsGL | - | v4.0 |

## Findings

### AutoFixable

- **[Error] MG003** MonoGame.Framework assembly reference (install folder or packages folder)
  - Project: `Mg34WindowsGl.csproj`
  - Code: `MonoGame.Framework, MonoGame.Framework.Net`
- **[Warning] MG005** .NET Framework 4.x target (TargetFrameworkVersion)
  - Project: `Mg34WindowsGl.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] MG011** WindowsGL, Linux or macOS desktop project (OpenTK era)
  - Project: `Mg34WindowsGl.csproj`
  - Code: `MonoGamePlatform=WindowsGL`
- **[Warning] MG018** Tao.Sdl assembly reference
  - Project: `Mg34WindowsGl.csproj`
  - Code: `Tao.Sdl`
- **[Info] MG004** MonoGamePlatform build property
  - Project: `Mg34WindowsGl.csproj`
  - Code: `MonoGamePlatform=WindowsGL`

### Manual

- **[Warning] MG022** Lidgren.Network reference
  - Project: `Mg34WindowsGl.csproj`
  - Code: `Lidgren.Network`

### Unsupported

- **[Error] MG021** MonoGame.Framework.Net reference (removed in 3.8)
  - Project: `Mg34WindowsGl.csproj`
  - Code: `MonoGame.Framework.Net`

## Rules

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

### MG011 WindowsGL, Linux or macOS desktop project (OpenTK era)

The project targets the old OpenTK-based desktop platforms. MonoGame 3.5 replaced them with DesktopGL, and 3.8 has no WindowsGL, Linux or MacOS packages.

**Resolution:** The migrator moves the project to MonoGame.Framework.DesktopGL on net8.0.

Migrator task: monogame-upgrade:11.1

### MG018 Tao.Sdl assembly reference

The project references Tao.Sdl, the SDL binding that the WindowsGL platform used before 3.5. Current MonoGame desktop packages bring their own SDL2.

**Resolution:** The migrator drops the reference.

Migrator task: monogame-upgrade:11.1

### MG021 MonoGame.Framework.Net reference (removed in 3.8)

The project references MonoGame.Framework.Net, which held the Net and GamerServices namespaces. MonoGame 3.8 removed the assembly.

**Resolution:** Remove the networking and gamer services code, or replace it with your own. Source that uses the namespaces is reported by MG028 and MG029 (monogame-upgrade task 13.2 can add stand-ins).

### MG022 Lidgren.Network reference

The project references Lidgren.Network, which old MonoGame builds shipped for networking. The 3.8 packages do not include it, and the install-folder copy is not available.

**Resolution:** Add the Lidgren.Network package from NuGet to the migrated project by hand, and check that your code builds against it.
