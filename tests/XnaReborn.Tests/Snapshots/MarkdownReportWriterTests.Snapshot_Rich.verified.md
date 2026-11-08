# XNA Reborn compatibility report

Rule catalog: sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 62 / 100 |
| Projected score after automatic fixes | 80 / 100 |
| Projects analyzed | 2 |
| Findings | 4 |

Severity: Blocker 1, Error 1, Warning 1, Info 1

Classification: AutoFixable 2, Manual 1, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Game/Game.csproj | 3.1 | Windows | HiDef | v3.5 |
| Lib\|Core/Lib.csproj | Unknown | Xbox360 | - | - |

## Findings

### AutoFixable

- **[Warning] XNA903** Legacy project import
  - Project: `Game/Game.csproj`
- **[Info] XNA910** Sprite font asset
  - Project: `Lib|Core/Lib.csproj`
  - Location: `Content/Font.spritefont:1:1`

### Manual

- **[Error] XNA902** Storage device
  - Project: `Game/Game.csproj`
  - Location: `Save/Store.cs:40:17`
  - Code: `var s = "'quoted'"; // café`

### Unsupported

- **[Blocker] XNA901** Xbox LIVE services
  - Project: `Game/Game.csproj`
  - Location: `Game1.cs:12:9`
  - Code: `Guide.ShowSignIn(1, false);`

## Rules

### XNA901 Xbox LIVE services

There is no Xbox LIVE in MonoGame.

**Resolution:** Remove the calls.

### XNA903 Legacy project import

The project imports XNA targets.

**Resolution:** Switch to an SDK-style project.

Migrator task: migrator-task-2

## Diagnostics

- ANA004: Source file has syntax errors. (`Game/Broken.cs`)
- ANA006: Solution lists a missing project.
