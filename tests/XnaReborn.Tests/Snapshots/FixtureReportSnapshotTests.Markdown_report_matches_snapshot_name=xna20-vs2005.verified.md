# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 91 / 100 |
| Projected score after automatic fixes | 92 / 100 |
| Projects analyzed | 2 |
| Findings | 4 |

Severity: Blocker 0, Error 2, Warning 2, Info 0

Classification: AutoFixable 2, Manual 2, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Game/Game.csproj | 2.0 | Windows | - | - |
| Game360/Game360.csproj | 2.0 | Xbox360 | - | - |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Game/Game.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.Common.targets, $(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.NestedContent.targets`
- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Game360/Game360.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.Common.targets, $(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.NestedContent.targets`

### Manual

- **[Error] XNA905** SpriteBlendMode
  - Project: `Game/Game.csproj`
  - Location: `Game/Game1.cs:41:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA905** SpriteBlendMode
  - Project: `Game360/Game360.csproj`
  - Location: `Game/Game1.cs:41:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA905 SpriteBlendMode

SpriteBlendMode was replaced by BlendState.

**Resolution:** Use BlendState.AlphaBlend or Additive.
