# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame + monogame-upgrade. Migrator 1.0.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 5 |
| Modified | 1 |
| Copied | 1 |
| Skipped | 0 |
| ManualWork | 0 |

## Manual work

- Content/Content.mgcb: Data/level.lvl: the content file was not found inside the source folder, so it was not copied.
- Content/Content.mgcb: MG025: the pipeline assembly 'Pipeline/GlowPipeline.dll' was not found inside the source folder, so it was not copied. Build it for MonoGame 3.8.6.1 and add it by hand.
- Content/Content.mgcb: MG026: 1 entry uses a custom importer or processor (LevelImporter/LevelProcessor). They are kept in Content.mgcb and build only with a pipeline assembly for MonoGame 3.8.6.1.
- Content/Content.mgcb: Textures/hero.png: the content file was not found inside the source folder, so it was not copied.
- Mg37DesktopGl.csproj: MG025: Content.mgcb loads a custom pipeline assembly: needs manual work. The migrator copies the assembly and keeps the /reference line, then lists it as manual work (monogame-upgrade task 12.2). Rebuild the extension against MonoGame 3.8.6.1.
- Mg37DesktopGl.csproj: MG026: Content.mgcb uses a non-standard importer or processor: needs manual work. The migrator keeps the entries unchanged and lists them as manual work (monogame-upgrade task 12.2). Rebuild the extension that provides the importer or processor.

## Changed files

| File | Kind | Rules |
|---|---|---|
| .config/dotnet-tools.json | Created | - |
| Content/Content.mgcb | Modified | MG023 |
| Directory.Build.props | Created | - |
| Directory.Build.targets | Created | - |
| Directory.Packages.props | Created | - |
| Mg37DesktopGl.csproj | Created | MG001, MG002, MG003, MG004, MG005, MG006, MG017, MG019, MG023 |

## Copied and skipped

Copied unchanged: 1

Skipped: 0
