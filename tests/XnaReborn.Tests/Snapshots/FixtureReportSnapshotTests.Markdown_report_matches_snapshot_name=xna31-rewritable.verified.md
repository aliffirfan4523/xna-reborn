# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 91 / 100 |
| Projected score after automatic fixes | 92 / 100 |
| Projects analyzed | 1 |
| Findings | 4 |

Severity: Blocker 0, Error 2, Warning 2, Info 0

Classification: AutoFixable 2, Manual 2, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna31Rewritable.csproj | 3.1 | Windows | - | v3.5 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna31Rewritable.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.targets`
- **[Warning] XNA904** XNA 3.1 project
  - Project: `Xna31Rewritable.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

### Manual

- **[Error] XNA905** SpriteBlendMode
  - Project: `Xna31Rewritable.csproj`
  - Location: `Game1.cs:43:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA905** SpriteBlendMode
  - Project: `Xna31Rewritable.csproj`
  - Location: `Game1.cs:47:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.None);`

## Rules

### XNA903 XNA Game Studio targets import

The project imports the XNA build targets.

**Resolution:** The migrator writes an SDK-style project.

Migrator task: project-migrator:4.1

### XNA904 XNA 3.1 project

XNA 3.1 APIs differ from 4.0.

**Resolution:** The migrator applies 3.1 to 4.0 rewrites.

Migrator task: project-migrator:6.2

### XNA905 SpriteBlendMode

SpriteBlendMode was replaced by BlendState.

**Resolution:** Use BlendState.AlphaBlend or Additive.
