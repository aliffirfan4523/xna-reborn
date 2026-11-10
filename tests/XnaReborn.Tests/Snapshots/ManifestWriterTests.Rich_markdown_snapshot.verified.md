# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame. Migrator 0.1.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 1 |
| Modified | 1 |
| Copied | 1 |
| Skipped | 1 |
| ManualWork | 1 |

## Manual work

- `Sound.xap`: XACT not supported
- Port the audio by hand

## Changed files

| File | Kind | Rules |
|---|---|---|
| App.csproj | Created | XNA001 |
| Game1.cs | Modified | XNA022, XNA024 |

## Copied and skipped

Copied unchanged: 1

Skipped: 1

- `Old.sln`: Replaced by new solution

## Build

Failed.

- Game1.cs(1): error CS0246
- Game1.cs(3): error CS0103
