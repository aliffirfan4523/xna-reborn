# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame + monogame-upgrade. Migrator 1.0.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 4 |
| Modified | 0 |
| Copied | 1 |
| Skipped | 0 |
| ManualWork | 0 |

## Manual work

- Mg34WindowsGl.csproj: MG021: MonoGame.Framework.Net reference (removed in 3.8): needs manual work. Remove the networking and gamer services code, or replace it with your own. Source that uses the namespaces is reported by MG028 and MG029 (monogame-upgrade task 13.2 can add stand-ins).
- Mg34WindowsGl.csproj: MG022: Lidgren.Network reference: needs manual work. Add the Lidgren.Network package from NuGet to the migrated project by hand, and check that your code builds against it.
- Mg34WindowsGl.csproj: reference 'Lidgren.Network' pointed into the MonoGame install folder or a packages folder and was dropped; add a package for it by hand if the code uses it.
- Mg34WindowsGl.csproj: reference 'MonoGame.Framework.Net' was dropped: it was removed in MonoGame 3.8, so network code needs manual work.

## Changed files

| File | Kind | Rules |
|---|---|---|
| Directory.Build.props | Created | - |
| Directory.Build.targets | Created | - |
| Directory.Packages.props | Created | - |
| Mg34WindowsGl.csproj | Created | MG003, MG004, MG005, MG011, MG018 |

## Copied and skipped

Copied unchanged: 1

Skipped: 0
