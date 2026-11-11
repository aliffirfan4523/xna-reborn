# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame + monogame-upgrade. Migrator 1.0.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 5 |
| Modified | 0 |
| Copied | 3 |
| Skipped | 1 |
| ManualWork | 1 |

## Manual work

- `Content/Prebuilt/logo.xnb`: Prebuilt .xnb content cannot be rebuilt for MonoGame and may not load. It is copied to the output folder as is.
- Mg37WindowsDx.csproj: MG020: SharpDX assembly references: needs manual work. The migrator drops the references that the package brings in (monogame-upgrade task 11.1). If your code uses SharpDX directly, check that it builds against SharpDX 4.2.0.
- Mg37WindowsDx.csproj: MG027: Prebuilt .xnb files: needs manual work. The migrator copies the files unchanged and lists them as manual work (monogame-upgrade task 12.3). If the game fails to load them, rebuild them from the source assets.
- Mg37WindowsDx.csproj: packages.config: MonoGame.Framework.WindowsDX 3.7.0.1708 was not carried over. Replaced by the MonoGame packages of the migrated project.
- Mg37WindowsDx.csproj: packages.config: SharpDX 4.0.1 was not carried over. The MonoGame WindowsDX package brings its own SharpDX; direct use in code needs manual work.

## Changed files

| File | Kind | Rules |
|---|---|---|
| .config/dotnet-tools.json | Created | - |
| Directory.Build.props | Created | - |
| Directory.Build.targets | Created | - |
| Directory.Packages.props | Created | - |
| Mg37WindowsDx.csproj | Created | MG001, MG002, MG003, MG004, MG005, MG006, MG007 |

## Copied and skipped

Copied unchanged: 3

Skipped: 1

- `packages.config`: Replaced by PackageReference items in the migrated project.
