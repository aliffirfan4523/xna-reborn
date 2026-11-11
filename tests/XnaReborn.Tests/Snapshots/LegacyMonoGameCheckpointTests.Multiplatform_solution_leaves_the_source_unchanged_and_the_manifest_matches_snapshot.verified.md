# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame + monogame-upgrade. Migrator 1.0.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 5 |
| Modified | 0 |
| Copied | 2 |
| Skipped | 0 |
| ManualWork | 7 |

## Manual work

- `Android/Activity1.cs`: Platform project (Android): this version does not migrate it, so the files are copied unchanged.
- `Android/Mg37Android.csproj`: Platform project (Android): this version does not migrate it, so the files are copied unchanged.
- `Ios/Mg37Ios.csproj`: Platform project (iOS): this version does not migrate it, so the files are copied unchanged.
- `Ios/Program.cs`: Platform project (iOS): this version does not migrate it, so the files are copied unchanged.
- `Uwp/App.cs`: Platform project (UWP): this version does not migrate it, so the files are copied unchanged.
- `Uwp/Content/Content.mgcb`: Platform project (UWP): this version does not migrate it, so the files are copied unchanged.
- `Uwp/Mg37Uwp.csproj`: Platform project (UWP): this version does not migrate it, so the files are copied unchanged.
- Android/Mg37Android.csproj: Android project: not migrated, copied unchanged. See manual work.
- Ios/Mg37Ios.csproj: iOS project: not migrated, copied unchanged. See manual work.
- Uwp/Mg37Uwp.csproj: UWP project: not migrated, copied unchanged. See manual work.

## Changed files

| File | Kind | Rules |
|---|---|---|
| Desktop/Mg37Desktop.csproj | Created | MG003, MG004 |
| Directory.Build.props | Created | - |
| Directory.Build.targets | Created | - |
| Directory.Packages.props | Created | - |
| Mg37Multi.sln | Created | - |

## Copied and skipped

Copied unchanged: 2

Skipped: 0
