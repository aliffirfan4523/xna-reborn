# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 99 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 1 |
| Findings | 2 |

Severity: Blocker 0, Error 0, Warning 2, Info 0

Classification: AutoFixable 2, Manual 0, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Zune31.csproj | 3.1 | Zune | - | v3.5 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Zune31.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.Common.targets`
- **[Warning] XNA904** XNA 3.1 project
  - Project: `Zune31.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA904 XNA 3.1 project

XNA 3.1 APIs differ from 4.0.

**Resolution:** The migrator applies 3.1 to 4.0 rewrites.

Migrator task: project-migrator:6.2
