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

| Project | Language | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|---|
| VbXna40Basic.vbproj | Visual Basic | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `VbXna40Basic.vbproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1
