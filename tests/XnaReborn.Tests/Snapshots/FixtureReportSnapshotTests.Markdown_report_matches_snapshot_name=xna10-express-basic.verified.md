# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 92 / 100 |
| Projected score after automatic fixes | 92 / 100 |
| Projects analyzed | 1 |
| Findings | 1 |

Severity: Blocker 0, Error 1, Warning 0, Info 0

Classification: AutoFixable 0, Manual 1, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| XnaExpress10Basic.csproj | 1.0 | Windows | - | - |

## Findings

### Manual

- **[Error] XNA905** SpriteBlendMode
  - Project: `XnaExpress10Basic.csproj`
  - Location: `Game1.cs:55:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`

## Rules

### XNA905 SpriteBlendMode

SpriteBlendMode was replaced by BlendState.

**Resolution:** Use BlendState.AlphaBlend or Additive.
