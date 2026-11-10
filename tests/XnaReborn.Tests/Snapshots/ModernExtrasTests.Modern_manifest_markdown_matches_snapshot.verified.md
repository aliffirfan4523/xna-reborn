# XNA Reborn migration manifest

Status: Completed

Rule catalog: c. Migrator 0.1.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 0 |
| Modified | 0 |
| Copied | 0 |
| Skipped | 0 |
| ManualWork | 0 |

## Manual work

None.

## Changed files

None.

## Copied and skipped

Copied unchanged: 0

Skipped: 0

## Modern extras

Opt-in window extras: AllowWindowResize, HighDpi.

Helper files:
- `Game/XnaReborn.Modern.cs`

Hook added to:
- `Game/Program.cs`

Limits:
- The game is drawn at its own resolution and scaled to the window with black bars. Mouse and touch positions are not remapped, so they only line up while the window has the game's own size.
- The game's resolution is read from the first frame. A game that changes its own resolution later keeps drawing at the first size.
- A game that draws to its own render target and does not restore the one the helper bound shows that frame unscaled.
- High-DPI is Windows only. Windows no longer enlarges the window, so it looks smaller on a high-DPI screen; use window resize or fullscreen with it.
- XNA Reborn has only checked that the helper compiles. Run the game to see how the window looks and behaves.
