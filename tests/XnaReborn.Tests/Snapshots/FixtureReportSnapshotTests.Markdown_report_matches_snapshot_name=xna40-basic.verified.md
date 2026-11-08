# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 98 / 100 |
| Projected score after automatic fixes | 99 / 100 |
| Projects analyzed | 1 |
| Findings | 2 |

Severity: Blocker 0, Error 0, Warning 1, Info 1

Classification: AutoFixable 1, Manual 1, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40Basic.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna40Basic.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

### Manual

- **[Info] XNA910** Sprite font
  - Project: `Xna40Basic.csproj`
  - Code: `Fonts/Font.spritefont`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA910 Sprite font

Fonts must exist on the build machine.

**Resolution:** Check the font family.
