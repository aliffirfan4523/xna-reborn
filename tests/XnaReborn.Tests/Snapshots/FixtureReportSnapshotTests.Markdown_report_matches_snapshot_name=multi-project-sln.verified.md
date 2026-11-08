# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 99 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 2 |
| Findings | 2 |

Severity: Blocker 0, Error 0, Warning 2, Info 0

Classification: AutoFixable 2, Manual 0, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Game/Game.csproj | 4.0 | Windows | HiDef | v4.0 |
| Library/Library.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Game/Game.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Library/Library.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

## Diagnostics

- ANA006: Solution lists a project that does not exist: Missing/Missing.csproj (`Missing/Missing.csproj`)
