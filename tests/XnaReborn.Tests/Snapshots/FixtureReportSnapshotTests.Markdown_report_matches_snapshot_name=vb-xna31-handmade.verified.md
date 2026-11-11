# XNA Reborn compatibility report

Rule catalog: xna-test-sample (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 92 / 100 |
| Projected score after automatic fixes | 92 / 100 |
| Projects analyzed | 1 |
| Findings | 3 |

Severity: Blocker 0, Error 3, Warning 0, Info 0

Classification: AutoFixable 0, Manual 3, Unsupported 0

## Projects

| Project | Language | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|---|
| VbXna31Handmade.vbproj | Visual Basic | 3.1 | Unknown | - | v3.5 |

## Findings

### Manual

- **[Error] XNA905** SpriteBlendMode
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:10:21`
  - Code: `Private mode As SpriteBlendMode = SpriteBlendMode.AlphaBlend`
- **[Error] XNA905** SpriteBlendMode
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:10:39`
  - Code: `Private mode As SpriteBlendMode = SpriteBlendMode.AlphaBlend`
- **[Error] XNA905** SpriteBlendMode
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:29:27`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend)`

## Rules

### XNA905 SpriteBlendMode

SpriteBlendMode was replaced by BlendState.

**Resolution:** Use BlendState.AlphaBlend or Additive.
