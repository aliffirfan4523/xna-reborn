# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 27 / 100 |
| Projected score after automatic fixes | 28 / 100 |
| Projects analyzed | 1 |
| Findings | 4 |

Severity: Blocker 2, Error 1, Warning 1, Info 0

Classification: AutoFixable 1, Manual 0, Unsupported 3

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40GamerServices.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna40GamerServices.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

### Unsupported

- **[Blocker] XNA901** GamerServices reference
  - Project: `Xna40GamerServices.csproj`
  - Code: `Microsoft.Xna.Framework.GamerServices`
- **[Blocker] XNA909** GamerServices namespace
  - Project: `Xna40GamerServices.csproj`
  - Location: `Game1.cs:3:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Error] XNA902** Storage reference
  - Project: `Xna40GamerServices.csproj`
  - Code: `Microsoft.Xna.Framework.Storage`

## Rules

### XNA901 GamerServices reference

Xbox Live has no modern equivalent.

**Resolution:** Remove the dependency.

### XNA902 Storage reference

StorageDevice depends on Xbox storage.

**Resolution:** Use plain file APIs.

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA909 GamerServices namespace

Code uses GamerServices.

**Resolution:** Remove the calls.
