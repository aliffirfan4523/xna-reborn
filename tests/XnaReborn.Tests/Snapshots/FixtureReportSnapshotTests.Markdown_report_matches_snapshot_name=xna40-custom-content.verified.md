# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 83 / 100 |
| Projected score after automatic fixes | 84 / 100 |
| Projects analyzed | 1 |
| Findings | 4 |

Severity: Blocker 0, Error 1, Warning 2, Info 1

Classification: AutoFixable 1, Manual 2, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| CustomContent.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `CustomContent.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`

### Manual

- **[Warning] XNA907** Custom content processor
  - Project: `CustomContent.csproj`
  - Code: `Textures/ship.png`
- **[Info] XNA910** Sprite font
  - Project: `CustomContent.csproj`
  - Code: `Fonts/Hud.spritefont`

### Unsupported

- **[Error] XNA908** XACT audio project
  - Project: `CustomContent.csproj`
  - Code: `Content/Audio/Sounds.xap`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA907 Custom content processor

Custom processors need a pipeline assembly.

**Resolution:** Port the processor to MonoGame.

### XNA908 XACT audio project

XACT is not supported by MonoGame.

**Resolution:** Export the sounds as plain wav files.

### XNA910 Sprite font

Fonts must exist on the build machine.

**Resolution:** Check the font family.
