# Fixtures

Small synthetic sample projects. Each folder has its own `README.md` that says it is original code under the repository license. Fixtures are never built or run.

## MonoGame upgrade fixtures

Fixtures for the `monogame-upgrade` spec (rules `MG001` to `MG032`). The "Positive for" column lists the rules the fixture must trigger. `mg38-current` is the default negative and scores 100. A test checks that every folder is listed here with the same rules as `FixturePaths.MonoGamePositives`.

| Fixture | Shape | Positive for |
|---|---|---|
| `mg37-windowsdx-pkgconfig` | Installer imports, `packages.config`, SharpDX reference, `net47`, x86, a prebuilt `.xnb` | MG001, MG002, MG003, MG004, MG005, MG006, MG007, MG020, MG027 |
| `mg37-desktopgl-installdir` | `Assemblies\DesktopGL` HintPaths, `OpenTK` and `NVorbis` references, `Content.mgcb` with install-folder and custom pipeline references | MG001, MG002, MG003, MG004, MG005, MG006, MG017, MG019, MG023, MG025, MG026 |
| `mg34-windowsgl` | `Assemblies\WindowsGL`, `Tao.Sdl`, `MonoGame.Framework.Net` and `Lidgren.Network` references | MG003, MG004, MG011, MG018, MG021, MG022 |
| `mg35-win8-wp8-ouya` | Windows Phone 8 and OUYA platform projects, an mgcb with a legacy platform | MG015, MG016, MG024 |
| `mg37-multiplatform-sln` | Solution with shared project, desktop, Android, iOS and UWP projects | MG012, MG013, MG014, MG024 |
| `mg3x-protobuild` | `Protobuild.exe` placeholder and a `.definition` file, no `.csproj` | MG010 |
| `mg38-old-pin` | SDK-style `netcoreapp3.1` with MonoGame 3.8.0.1641 | MG008, MG009 |
| `mg38-current` | SDK-style `net8.0` with MonoGame 3.8.6.1 | none |
| `mg37-source-apis` | Source that uses `Net`, `GamerServices`, `OpenTK`, `Windows.UI.Xaml` and `Color.TransparentBlack` | MG028, MG029, MG030, MG031, MG032 |
