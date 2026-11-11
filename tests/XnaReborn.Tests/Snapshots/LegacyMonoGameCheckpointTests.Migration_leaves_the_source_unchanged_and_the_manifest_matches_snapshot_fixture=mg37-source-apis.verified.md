# XNA Reborn migration manifest

Status: Completed

Rule catalog: xna-to-monogame + monogame-upgrade. Migrator 1.0.0. Manifest format 1.

## Summary

| Kind | Files |
|---|---|
| Created | 4 |
| Modified | 5 |
| Copied | 1 |
| Skipped | 0 |
| ManualWork | 0 |

## Manual work

- GlCode.cs: MG030: 1 using(s) left as they are with a marker. OpenTK is not part of MonoGame 3.8.6.1. Port the OpenGL code by hand or remove it.
- Mg37SourceApis.csproj: MG028: Microsoft.Xna.Framework.Net usage: needs manual work. Replace the networking code with your own or a NuGet networking library. Stand-in types can be added with the compatibility shims (monogame-upgrade task 13.2).
- Mg37SourceApis.csproj: MG029: Microsoft.Xna.Framework.GamerServices usage: needs manual work. Remove the calls or use the compatibility shims (monogame-upgrade task 13.2).
- Mg37SourceApis.csproj: MG030: OpenTK API usage: needs manual work. Replace the OpenTK calls with MonoGame APIs or a NuGet OpenTK package of your choice. The migrator leaves a marker at each use (monogame-upgrade task 13.4).
- Mg37SourceApis.csproj: MG031: UWP API usage (Windows.UI.Xaml): needs manual work. Move this code into the platform project, which is copied unchanged, or replace it with a desktop equivalent. The migrator leaves a marker at each use (monogame-upgrade task 13.4).
- NetCode.cs: MG028: 1 using(s) left as they are with a marker. MonoGame 3.8 has no Microsoft.Xna.Framework.Net. Replace the network code or remove the feature.
- Services.cs: MG029: 1 using(s) left as they are with a marker. MonoGame 3.8 has no Microsoft.Xna.Framework.GamerServices. Replace the code or remove the feature.
- UwpCode.cs: MG031: 1 using(s) left as they are with a marker. This is a UWP API. It does not exist in the desktop project. Replace it by hand or remove it.

## Changed files

| File | Kind | Rules |
|---|---|---|
| Colors.cs | Modified | MG032 |
| Directory.Build.props | Created | - |
| Directory.Build.targets | Created | - |
| Directory.Packages.props | Created | - |
| GlCode.cs | Modified | MG030 |
| Mg37SourceApis.csproj | Created | MG003, MG004 |
| NetCode.cs | Modified | MG028 |
| Services.cs | Modified | MG029 |
| UwpCode.cs | Modified | MG031 |

## Copied and skipped

Copied unchanged: 1

Skipped: 0
