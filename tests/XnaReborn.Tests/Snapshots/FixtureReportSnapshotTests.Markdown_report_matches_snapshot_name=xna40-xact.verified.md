# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 87 / 100 |
| Projected score after automatic fixes | 88 / 100 |
| Projects analyzed | 1 |
| Findings | 2 |

Severity: Blocker 0, Error 1, Warning 1, Info 0

Classification: AutoFixable 1, Manual 0, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40Xact.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna40Xact.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

### Unsupported

- **[Error] XNA908** XACT audio project
  - Project: `Xna40Xact.csproj`
  - Code: `Content/Sounds.xap`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA908 XACT audio project

XACT is not supported by MonoGame.

**Resolution:** Export the sounds as plain wav files.
