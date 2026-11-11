# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + monogame-upgrade (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 100 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 1 |
| Findings | 2 |

Severity: Blocker 0, Error 0, Warning 0, Info 2

Classification: AutoFixable 2, Manual 0, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Mg38OldPin.csproj | MonoGameLegacy | - | 3.8.0.1641 (High) | DesktopGL | - | netcoreapp3.1 |

## Findings

### AutoFixable

- **[Info] MG008** MonoGame package pinned below 3.8.6.1
  - Project: `Mg38OldPin.csproj`
  - Code: `MonoGame.Framework.DesktopGL`
- **[Info] MG009** Older .NET target (netcoreapp3.1, net5.0, net6.0, net7.0)
  - Project: `Mg38OldPin.csproj`
  - Code: `TargetFramework=netcoreapp3.1`

## Rules

### MG008 MonoGame package pinned below 3.8.6.1

The project already uses a MonoGame 3.8 package but pins an older version. The 3.8 line fixes bugs and adds net8.0 support. Packages not named MonoGame.Framework.* (for example MonoGame.Extended) are not checked, because they have their own versions.

**Resolution:** The migrator updates the MonoGame package versions, the target framework and the dotnet-mgcb tool manifest to 3.8.6.1.

Migrator task: monogame-upgrade:11.2

### MG009 Older .NET target (netcoreapp3.1, net5.0, net6.0, net7.0)

The project targets a .NET version that is out of support. MonoGame 3.8.2 and later build for net8.0.

**Resolution:** The migrator sets the target framework to net8.0 or net8.0-windows.

Migrator task: monogame-upgrade:11.2
