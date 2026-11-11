# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 39 / 100 |
| Projected score after automatic fixes | 40 / 100 |
| Projects analyzed | 1 |
| Findings | 3 |

Severity: Blocker 2, Error 0, Warning 1, Info 0

Classification: AutoFixable 1, Manual 0, Unsupported 2

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Wp7Xna.csproj | 4.0 | WindowsPhone | Reach | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Wp7Xna.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`

### Unsupported

- **[Blocker] XNA901** GamerServices reference
  - Project: `Wp7Xna.csproj`
  - Code: `Microsoft.Xna.Framework.GamerServices`
- **[Blocker] XNA909** GamerServices namespace
  - Project: `Wp7Xna.csproj`
  - Location: `Game1.cs:5:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`

## Rules

### XNA901 GamerServices reference

Xbox Live has no modern equivalent.

**Resolution:** Remove the dependency.

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA909 GamerServices namespace

Code uses GamerServices.

**Resolution:** Remove the calls.
