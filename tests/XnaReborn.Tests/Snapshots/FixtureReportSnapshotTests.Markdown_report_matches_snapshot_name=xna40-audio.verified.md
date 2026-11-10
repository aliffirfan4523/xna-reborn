# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 99 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 1 |
| Findings | 1 |

Severity: Blocker 0, Error 0, Warning 1, Info 0

Classification: AutoFixable 1, Manual 0, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40Audio.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna40Audio.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1
