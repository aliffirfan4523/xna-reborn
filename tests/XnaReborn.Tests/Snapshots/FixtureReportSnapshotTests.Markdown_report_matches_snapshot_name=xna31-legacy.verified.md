# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 83 / 100 |
| Projected score after automatic fixes | 84 / 100 |
| Projects analyzed | 1 |
| Findings | 5 |

Severity: Blocker 0, Error 3, Warning 2, Info 0

Classification: AutoFixable 2, Manual 3, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna31Legacy.csproj | 3.1 | Windows | - | v3.5 |

## Findings

### AutoFixable

- **[Warning] XNA903** XNA Game Studio targets import
  - Project: `Xna31Legacy.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.targets`
- **[Warning] XNA904** XNA 3.1 project
  - Project: `Xna31Legacy.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

### Manual

- **[Error] XNA905** SpriteBlendMode
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:44:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA905** SpriteBlendMode
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:49:17`
  - Code: `SpriteBlendMode.Additive,`
- **[Error] XNA906** ResolveBackBuffer
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:55:13`
  - Code: `GraphicsDevice.ResolveBackBuffer(snapshot);`

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

### XNA906 ResolveBackBuffer

ResolveBackBuffer was removed.

**Resolution:** Render to a RenderTarget2D instead.
