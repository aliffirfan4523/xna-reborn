# Design: MonoGame Upgrade (C# / .NET 8)

## Overview

Old MonoGame projects go through the same pipeline as XNA projects: discover, read, analyze, plan, migrate a copy, build, run. Most of the work already exists. This feature adds:

- a **project family** and a **MonoGame version detector** (new, small, in the analyzer);
- a **second rule catalog**, `rules/monogame-upgrade.json`, ids `MG001` to `MG032` (data);
- one **new match type**, `mgcbSetting`, and one optional field on `projectItem` (`metadata`);
- an **mgcb reader** (shared model in Core, so the analyzer and the migrator use the same parser);
- migrator work: a legacy-MonoGame input path for the SDK project generator, a `packages.config` mapper, an `MgcbUpgrader`, two source rewriters, and platform-project handling;
- a few runner hints.

```
path -> ProjectDiscovery -> ProjectReader -> MonoGameVersionDetector -> RuleEngine(MG catalog) -> Scorer -> AnalysisReport
                (family)      (+packages.config,                              ^
                               +.projitems,                        MgcbReader (Core parser)
                               +Content.mgcb)

report -> MigrationPlanner -> SdkCsprojGenerator / MgcbUpgrader / rewriters -> OutputWriter -> Runner (build, run)
                              (reused, with a legacy-MonoGame input path)
```

## What the research found

All claims here are from official MonoGame pages or the MonoGame changelog. Where a claim comes from a real project instead, it says so. Items marked **to confirm** are not proven yet and are settled in task 1.

### What 3.8 changed for existing projects

| Change | Version | Source |
|---|---|---|
| MonoGame comes from NuGet and Visual Studio extensions, not an installer. 3.8 templates are not compatible with earlier versions. | 3.8 | [Migrating from 3.7](https://docs.monogame.net/articles/migration/migrate_37.html) |
| WindowsDX and DesktopGL projects are SDK-style. Other platforms replace the `MonoGame.Framework` `Reference` and the `MonoGame.Content.Builder.targets` import with `PackageReference` items for `MonoGame.Framework.{Platform}` and `MonoGame.Content.Builder.Task`. | 3.8 | [Migrating from 3.7](https://docs.monogame.net/articles/migration/migrate_37.html) |
| MGCB, 2MGFX and the Pipeline Tool ship as .NET tools. The Pipeline Tool is now the MGCB Editor. | 3.8.1 | [Migrating from 3.7](https://docs.monogame.net/articles/migration/migrate_37.html) |
| Only .NET 6 and Visual Studio 2022. Last version to build 32-bit games on Windows. | 3.8.1 | [What's new](https://docs.monogame.net/articles/whats_new.html) |
| .NET 8. MGCB becomes a local tool (`.config/dotnet-tools.json`) instead of a global one. UWP removed. | 3.8.2 | [What's new](https://docs.monogame.net/articles/whats_new.html) |
| Updating within 3.8.x: change the target framework version, the `PackageReference` versions, and the tool manifest versions, then `dotnet clean`, `dotnet restore`, `dotnet tool restore`. | 3.8.x | [Updating versions](https://docs.monogame.net/articles/migration/updating_versions.html) |
| Protobuild removed, replaced by solutions and projects. `MonoGame.Framework.Net` removed. | 3.8 | [Changelog 3.8](https://github.com/MonoGame/MonoGame/blob/develop/CHANGELOG.md) (#7040, #6900) |
| `Color.TransparentBlack` marked obsolete (3.8), then removed (3.8.1). Obsolete `SpriteBatch.Draw` overload removed (3.8). PS Vita removed (3.8.1). | 3.8, 3.8.1 | [Changelog](https://github.com/MonoGame/MonoGame/blob/develop/CHANGELOG.md) (#6817, #7505, #6818, #7531) |

### What changed before 3.8

| Change | Version | Source |
|---|---|---|
| Windows 8, Windows Phone 8, OUYA, PlayStation Mobile added | 3.0 | [Changelog 3.0](https://github.com/MonoGame/MonoGame/blob/develop/CHANGELOG.md) |
| Old Windows (OpenTK) project renamed WindowsGL | 3.2 | Changelog 3.2 |
| Windows Universal template added. Net and GamerServices moved to `MonoGame.Framework.Net`. New Pipeline GUI tool. Protobuild added. | 3.3 | Changelog 3.3 |
| Old XNA content pipeline extensions removed | 3.4 | Changelog 3.4 |
| New cross-platform "Desktop" project (DesktopGL). PlayStation Mobile removed. | 3.5 | Changelog 3.5 |
| OpenTK replaced with custom GL bindings. SDL2 backend for desktop GL. Raw asset loading and legacy Storage classes removed. OUYA removed. | 3.6 | Changelog 3.6 (#4874, #4428, #4726, #4320, #5194) |
| `Matrix.Scale` and `Matrix.Rotation` properties removed. Windows 8.1 and Windows Phone 8.1 support removed. SharpDX taken from NuGet. Template system moved to NuGet. | 3.7 | Changelog 3.7 (#5584, #5809, #5748, #6135) |

Removals in 3.6 and 3.7 are listed because code written for 3.5 or 3.6 can break on 3.7, and code from any of them can break on 3.8.6.1.

### What real old projects look like

Read through the GitHub API on 2026-11-11. Nothing was cloned.

| Signal | Seen in | What it shows |
|---|---|---|
| `<Import Project="$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Common.props" ...>` at the top, and `<Import Project="$(MSBuildExtensionsPath)\MonoGame\v3.0\MonoGame.Content.Builder.targets" />` at the bottom | Kosmonaut3d/DeferredEngine, craftworkgames/MonoGame.Squid, roy-t/MSDF, Kosmonaut3d/BloomFilter-for-Monogame-and-XNA, TheLastSliceGame/TheLastSliceGame | The installer-era layout. The `v3.0` folder name is kept across versions, so the folder is not a version signal. |
| `<MonoGamePlatform>Windows</MonoGamePlatform>` or `DesktopGL`, with `OutputPath` `bin\$(MonoGamePlatform)\$(Platform)\$(Configuration)\` | the four desktop projects above | Platform property. Also x86-only `PlatformTarget`. |
| `<Reference Include="MonoGame.Framework, Version=3.7.0.213, ...>` with `HintPath` `...\Program Files (x86)\MonoGame\v3.0\Assemblies\Windows\MonoGame.Framework.dll` | DeferredEngine | Exact version in the include, and an absolute install path. |
| `HintPath` `$(MonoGameInstallDirectory)\MonoGame\v3.0\Assemblies\DesktopGL\MonoGame.Framework.dll`, plus `OpenTK` and `NVorbis` references | MonoGame.Squid | DesktopGL from the installer, with separate OpenTK and NVorbis references. |
| `HintPath` `..\packages\MonoGame.Framework.WindowsDX.3.7.0.1708\lib\net45\MonoGame.Framework.dll`, with SharpDX packages | roy-t/MSDF | NuGet era (`packages.config`), exact version in the path, SharpDX referenced directly. |
| `<MonoGamePlatform>WindowsStoreApp</MonoGamePlatform>`, `HintPath` `Assemblies\WindowsUniversal`, `ProjectTypeGuids` `{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A}`, `OutputType` `AppContainerExe` | TheLastSliceGame | A UWP project. `WindowsStoreApp` here means UWP, so the property value alone does not separate Windows 8 from UWP. |
| `HintPath` `$(MSBuildExtensionsPath)\..\MonoGame\v3.0\Assemblies\WindowsGL\MonoGame.Framework.dll` and `Tao.Sdl.dll` | tinco/Farseer-Physics (samples) | WindowsGL, the 3.2 to 3.5 era. |
| `Content.mgcb` header: `/platform:DesktopGL`, `/platform:Windows`, `/platform:WindowsStoreApp`, `/profile:Reach`, `/compress:False`, empty `#--- References ---#` | Squid, DeferredEngine, TheLastSliceGame | The mgcb platform is a plain string. It must match the migrated target. |
| SDK-style `net6.0-windows` with `MonoGame.Framework.WindowsDX` and `MonoGame.Content.Builder.Task` `3.8.1.303` | discosultan/penumbra | An already-modern project on an older 3.8 pin. It needs a version bump, not a rewrite. |

### What the first migrated copy needs

Taken from the 3.8 migration guide, the changelog, and the XNA hand migration already done for this product:

1. An SDK-style project on `net8.0` or `net8.0-windows`, with `MonoGame.Framework.DesktopGL` or `.WindowsDX` and `MonoGame.Content.Builder.Task` at 3.8.6.1.
2. `.config/dotnet-tools.json` with `dotnet-mgcb` 3.8.6.1, and a `Content.mgcb` whose `/platform` matches the target.
3. No inherited `Directory.Build.props` (see `BuildIsolationGenerator`).
4. A `Main` that compiles on the new target.
5. Source fixes for removed APIs (`Color.TransparentBlack`, `MonoGame.Framework.Net`, `OpenTK`, UWP APIs), either done or marked.
6. Non-MonoGame NuGet packages carried over from `packages.config` with the same id and version.

## Project family and version detection

### Where it lives

| Piece | Project | Notes |
|---|---|---|
| `ProjectFamily` enum (`Xna`, `MonoGameLegacy`, `MonoGameCurrent`, `Other`) | Core | New. |
| `MonoGameVersionInfo(string? Exact, MonoGameBand Band, Confidence Confidence)`, `MonoGamePlatform` enum | Core | New. Immutable records. |
| `ProjectInfo` gains `Family`, `MonoGameVersion`, `MonoGamePlatform` (all with defaults so existing callers compile) | Core | `IsXna` stays and equals `Family == Xna`. |
| `MonoGameProjectDetector` | Analyzer/Discovery | Sits next to `XnaProjectDetector`. Pure function over the project XML. |
| `MonoGameVersionDetector` | Analyzer/Reading | Sits next to `XnaVersionDetector`. |
| `PackagesConfigReader` | Analyzer/Reading | Read-only XML. |
| `MgcbParser`, `MgcbFile` | Core/Mgcb | Shared by analyzer and migrator. No file access; text in, model out. |

`DiscoveredProject` gains `Family`. `DiscoveryResult.XnaProjects` stays. A new `MonoGameProjects` property is added. `NoProjectReason` gets the new wording from R1.6.

### Family rules

A project is `MonoGameLegacy` when any of these is true and it is not `MonoGameCurrent`:

- a `Reference` named `MonoGame.Framework*`;
- an `Import` whose path contains `MonoGame\v3.0\MonoGame.`;
- a `MonoGamePlatform` property or a `MonoGameContentReference` item;
- a `packages.config` entry or `PackageReference` named `MonoGame.Framework*` on a project that is not current.

It is `MonoGameCurrent` when it uses `Sdk="Microsoft.NET.Sdk"`, targets `net8.0` or `net8.0-windows`, and all `MonoGame.Framework.*` package versions equal the pin in `Projects/packages.json`. A project that uses the SDK but pins an older 3.8 version (penumbra, 3.8.1.303) is `MonoGameLegacy`; rules `MG008` and `MG009` fire and the migrator does a bump.

A project that references `Microsoft.Xna.Framework*` and `MonoGame.Framework*` stays `Xna`, with a Diagnostic `ANA008`.

### Version signals

`MonoGameVersionDetector` collects signals and returns the most specific one. It never runs MSBuild.

| Signal | Example | Result |
|---|---|---|
| Assembly version in a `Reference` include | `MonoGame.Framework, Version=3.7.0.213` | exact `3.7.0.213`, High |
| Version in a NuGet `HintPath` or `packages.config` | `MonoGame.Framework.WindowsDX.3.7.0.1708` | exact, High |
| `PackageReference` `Version` | `3.8.1.303` | exact, High |
| `HintPath` folder `Assemblies\WindowsGL` | Farseer samples | band 3.2 to 3.5, Medium. WindowsGL was named in 3.2 and DesktopGL arrived in 3.5. **To confirm:** the last version that shipped WindowsGL. |
| `HintPath` folder `Assemblies\DesktopGL` | Squid | band 3.5 and later, Medium |
| `HintPath` folder `Assemblies\WindowsUniversal` or `ProjectTypeGuids` UWP | TheLastSliceGame | band 3.3 to 3.8.1, Medium (template added in 3.3, removed in 3.8.2) |
| Import of `MonoGame.Common.props` | all installer-era samples above | "installer era", no version. **To confirm:** the first version that used `.props`. |
| `TargetFramework` `netcoreapp3.1` or `net5.0` with a `PackageReference` | | 3.8.0 era, Medium |

When two signals give different exact versions, the detector reports the higher one and adds `ANA002`-style Diagnostic `ANA009`. When there is none, the band is `Unknown`.

`MonoGamePlatform` values are mapped like this. The platform property alone is not enough, as TheLastSliceGame shows, so the detector also reads the `HintPath` folder and the project type GUID.

| Evidence | Platform |
|---|---|
| `Assemblies\Windows` or `MonoGamePlatform=Windows` | Windows (DirectX) |
| `Assemblies\WindowsGL`, `MonoGamePlatform=WindowsGL`, `Linux`, `MacOS` | WindowsGL, Linux, or macOS (OpenTK or SDL desktop) |
| `Assemblies\DesktopGL` or `MonoGamePlatform=DesktopGL` | DesktopGL |
| `Assemblies\WindowsUniversal`, UWP GUID, or `WindowsStoreApp` with an `AppContainerExe` output | UWP |
| `WindowsPhone`, `WindowsPhone81`, or `Windows8` (**names to confirm**) | Windows 8 or Windows Phone |
| Xamarin.Android GUID `{EFBA0AD7-5A72-4C68-AF49-83D382785DCF}` or `MonoAndroid` | Android |
| Xamarin.iOS GUIDs `{6BC8ED88-2882-458C-8E55-DFD12B67127B}` or `{FEACFBD2-3405-455C-9665-78FE426C6842}` (**to confirm**) | iOS |

## Rule catalog

A new file, `rules/monogame-upgrade.json`, in the same format as `rules/xna-to-monogame.json`. It has its own `scoring` section and a new optional top-level field, `"appliesTo": "monogame"` (the default, when missing, is `"xna"`). The engine runs a catalog only against projects of its family. This keeps XNA rules (for example XNA003, which matches `Microsoft.Xna.Framework*`) from firing on MonoGame projects, and the other way round.

Schema changes, all backward compatible (catalog `version` stays 1):

1. `id` pattern becomes `^(XNA|MG)[0-9]{3}$`.
2. Optional top-level `appliesTo`.
3. `matchMgcbSetting` definition.
4. Optional `metadata` object on `matchProjectItem`.
5. `migratorTask` pattern already accepts `monogame-upgrade:N.N`.

Ids are contiguous: `MG001` to `MG032`. Ids are never reused.

### Proposed rules

"Pos" is the positive fixture, "Neg" the negative. The negative is `mg38-current` unless the table says otherwise. Fixtures are described in the next section. The migrator task numbers refer to `tasks.md` in this folder.

| Id | Title | Severity | Classification | Match | Pos | Migrator task (AutoFixable) or related |
|---|---|---|---|---|---|---|
| MG001 | Import of `MonoGame.Content.Builder.targets` (installer targets) | Error | AutoFixable | `projectItem` Import `*MonoGame.Content.Builder.targets` | A, B | 11.1 |
| MG002 | Import of `MonoGame.Common.props` (installer props) | Error | AutoFixable | `projectItem` Import `*MonoGame.Common.props` | A, B | 11.1 |
| MG003 | `MonoGame.Framework` assembly reference (install folder or `packages\` HintPath) | Error | AutoFixable | `reference` `MonoGame.Framework*` | A, B, C | 11.1 |
| MG004 | `MonoGamePlatform` build property | Info | AutoFixable | `projectProperty` `MonoGamePlatform` | A, B, C | 11.1 |
| MG005 | .NET Framework 4.x target (`TargetFrameworkVersion`) | Warning | AutoFixable | `projectProperty` `TargetFrameworkVersion` matches `^v[2-4]\.` | A, B | 11.1 |
| MG006 | x86-only build (`PlatformTarget` x86) | Warning | AutoFixable | `projectProperty` `PlatformTarget` equals `x86` | A, B | 11.1 |
| MG007 | `packages.config` NuGet list | Warning | AutoFixable | `filePattern` `**/packages.config` | A | 11.3 |
| MG008 | MonoGame package pinned below 3.8.6.1 | Info | AutoFixable | `projectItem` PackageReference `MonoGame.*` with `metadata` Version not `3.8.6.1` | G | 11.2 |
| MG009 | Older .NET target (`netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`) | Info | AutoFixable | `projectProperty` `TargetFramework` matches `^(netcoreapp3\.1\|net[5-7]\.0)` | G | 11.2 |
| MG010 | Protobuild project definitions | Error | Manual | `filePattern` `**/Protobuild.exe` | F | - |
| MG011 | WindowsGL, Linux or macOS desktop project (OpenTK era) | Warning | AutoFixable | `projectProperty` `MonoGamePlatform` matches `^(WindowsGL\|Linux\|MacOS)$` | C | 11.1 |
| MG012 | Android project (Xamarin) | Warning | Manual | `projectProperty` `ProjectTypeGuids` matches `EFBA0AD7-5A72-4C68-AF49-83D382785DCF` | E | 14.1 (related) |
| MG013 | iOS project (Xamarin) | Warning | Manual | `projectProperty` `ProjectTypeGuids` matches the two iOS GUIDs | E | 14.1 (related) |
| MG014 | UWP / Windows Store project | Error | Unsupported | `projectProperty` `MonoGamePlatform` matches `^(WindowsStoreApp\|WindowsUniversal)$` | E | 14.1 (related) |
| MG015 | Windows 8 / Windows Phone project (names to confirm) | Error | Unsupported | `projectProperty` `MonoGamePlatform` matches `^(WindowsPhone8?1?\|Windows8)$` | D | 14.1 (related) |
| MG016 | OUYA, PlayStation Mobile or PS Vita project | Error | Unsupported | `projectProperty` `MonoGamePlatform` matches `^(Ouya\|PSM\|PSMobile\|PSVita)$` (names to confirm) | D | 14.1 (related) |
| MG017 | `OpenTK` assembly reference | Warning | AutoFixable | `reference` `OpenTK*` | B | 11.1 |
| MG018 | `Tao.Sdl` assembly reference | Warning | AutoFixable | `reference` `Tao.Sdl*` | C | 11.1 |
| MG019 | `NVorbis` assembly reference | Info | AutoFixable | `reference` `NVorbis*` | B | 11.1 |
| MG020 | `SharpDX` assembly references | Warning | Manual | `reference` `SharpDX*` | A | 11.1 (drop); sharpdx-toolkit spec |
| MG021 | `MonoGame.Framework.Net` reference (removed in 3.8) | Error | Unsupported | `reference` `MonoGame.Framework.Net*` | C | 13.2 (related) |
| MG022 | `Lidgren.Network` reference | Warning | Manual | `reference` `Lidgren.Network*` | C | - |
| MG023 | `Content.mgcb` references the MonoGame install folder | Error | AutoFixable | `mgcbSetting` `reference` matches `(?i)MonoGame\\v3\.0\\` | B | 12.1 |
| MG024 | `Content.mgcb` platform not valid for the 3.8.6.1 desktop targets | Warning | AutoFixable | `mgcbSetting` `platform` matches `^(MacOSX\|WindowsStoreApp\|WindowsUniversal\|WindowsPhone\|WindowsPhone81\|NativeClient\|Ouya\|PlayStationMobile\|RaspberryPi)$` (names to confirm) | D, E | 12.1 |
| MG025 | `Content.mgcb` loads a custom pipeline assembly | Warning | Manual | `mgcbSetting` `reference` matches `.` and not the install folder | B | 12.2 (related) |
| MG026 | `Content.mgcb` uses a non-standard importer or processor | Warning | Manual | `mgcbSetting` `entry` with `nonStandardPair: true` | B | 12.2 (related) |
| MG027 | Prebuilt `.xnb` files | Info | Manual | `filePattern` `**/*.xnb` | A | 12.3 (related) |
| MG028 | `Microsoft.Xna.Framework.Net` usage | Error | Unsupported | `namespace` `Microsoft.Xna.Framework.Net`, `includeChildren` | I | 13.2 (related) |
| MG029 | `Microsoft.Xna.Framework.GamerServices` usage | Error | Unsupported | `namespace` `Microsoft.Xna.Framework.GamerServices`, `includeChildren` | I | 13.2 (related) |
| MG030 | `OpenTK` API usage | Error | Manual | `namespace` `OpenTK`, `includeChildren` | I | - |
| MG031 | UWP API usage (`Windows.UI.Xaml`, `Windows.ApplicationModel`) | Warning | Manual | `namespace` `Windows.UI.Xaml`, `includeChildren` | I | - |
| MG032 | `Color.TransparentBlack` (removed in 3.8.1) | Error | AutoFixable | `member` `TransparentBlack`, `receiver` `Color`, `kind` Access, namespaces `Microsoft.Xna.Framework` | I | 13.1 |

32 rules: 10 project and package (MG001 to MG010), 6 platform (MG011 to MG016), 6 reference (MG017 to MG022), 5 content (MG023 to MG027), 5 source (MG028 to MG032). Severity and classification follow the XNA catalog conventions, so the existing scoring weights apply unchanged.

Notes on specific rules:

- **MG003** only fires on `Reference` items. A migrated SDK-style project uses `PackageReference`, so `mg38-current` does not match. This is the negative test.
- **MG010** reports Protobuild. The tool does not run Protobuild. The resolution says: generate the project files with the Protobuild version the project used, outside this tool, then open the generated folder. A repository with `.definition` files and a generated `.csproj` is analyzed from the `.csproj` and carries this rule as a warning-level note.
- **MG012 to MG016** are platform rules. They fire per project, so the report says which project in a solution is a platform project. They are Manual or Unsupported because this version does not generate Android, iOS, or UWP output (R9).
- **MG020** does not decide whether the code uses SharpDX. A direct SharpDX user is covered by the sibling `sharpdx-toolkit` spec. Here the migrator drops the SharpDX `Reference` items that the 3.8 package brings in, and marks the rule. **To confirm:** which SharpDX version `MonoGame.Framework.WindowsDX` 3.8.6.1 depends on (read from the nuspec in task 1).
- **MG024** needs the platform names confirmed against the 3.8.6.1 content builder. The migrator always writes the platform of the chosen target, so the rule is informational for the report and does not depend on the list being complete.
- **MG032** is safe to automate because `Color.TransparentBlack` and `Color.Transparent` are both (0, 0, 0, 0). The pattern is syntactic (`Color.TransparentBlack`), which matches the existing `member` limits.

### Rules considered and not proposed

| Candidate | Why not |
|---|---|
| `Matrix.Scale` / `Matrix.Rotation` removed in 3.7 | `Scale` and `Rotation` are common member names, and a syntax-only `member` match cannot tell a `Matrix` from other types. It would give false positives. A semantic check needs the MonoGame assemblies, which the analyzer does not require. Left to the compiler error and a runner hint. |
| Obsolete `SpriteBatch.Draw` overload removed in 3.8 | The changelog does not name the overload. Left to the compiler error. |
| Legacy `Microsoft.Xna.Framework.Storage` classes (removed in 3.6) | **To confirm** against the 3.8.6.1 API reference before writing a rule. Task 1 settles it. |
| `.config/dotnet-tools.json` with an old `dotnet-mgcb` | A `filePattern` cannot read the version, and the migrator always rewrites the manifest. Not a rule. |
| Raw asset loading through `ContentManager` (removed in 3.6) | Not detectable from syntax. |

## Match types

| Need | Existing type that fits |
|---|---|
| Installer imports (MG001, MG002) | `projectItem` |
| Assembly references (MG003, MG017 to MG022) | `reference` |
| Build properties and platform GUIDs (MG004 to MG006, MG009, MG011 to MG016) | `projectProperty` with `equals` or `matches` |
| `packages.config`, Protobuild, `.xnb` (MG007, MG010, MG027) | `filePattern` |
| Source API use (MG028 to MG032) | `namespace` and `member` |
| `PackageReference` version below the pin (MG008) | **extension:** `projectItem.metadata` |
| `Content.mgcb` contents (MG023 to MG026) | **new type:** `mgcbSetting` |

### Extension: `projectItem.metadata`

```json
{ "type": "projectItem", "itemType": "PackageReference", "include": "MonoGame.*",
  "metadata": { "name": "Version", "notEquals": "3.8.6.1" } }
```

`metadata.name` is an attribute or child element of the item. `equals`, `notEquals`, and `matches` are optional. Without `metadata`, behaviour is unchanged. This follows the same pattern as `nonStandardPair` on `contentItem`: an optional field, schema version unchanged. The pin comes from the rule, not from code, so a new MonoGame release is a catalog edit. **Open point:** the rule pins the literal `3.8.6.1`; when the pin in `packages.json` moves, the catalog must move with it. Task 6.1 adds a test that fails when the two disagree.

### New type: `mgcbSetting`

Why no existing type fits: `contentItem` reads items from `.contentproj` files (XNA). `Content.mgcb` is a different file with a different grammar (`/key:value` lines, `#begin` blocks), and the global settings (`/platform`, `/reference`) are not items at all. `filePattern` can see that the file exists but not what is in it.

```json
{ "type": "mgcbSetting", "setting": "reference", "matches": "(?i)MonoGame\\\\v3\\.0\\\\" }
{ "type": "mgcbSetting", "setting": "platform",  "matches": "^(MacOSX|WindowsStoreApp)$" }
{ "type": "mgcbSetting", "setting": "entry",     "nonStandardPair": true }
```

- `setting` is one of `platform`, `profile`, `config`, `compress`, `reference`, `entry`.
- `matches` is a regex on the value. `equals` is also accepted.
- With `setting: "entry"`, `importer` and `processor` filter entries, and `nonStandardPair: true` keeps entries whose pair is not in `rules/importer-map.json` (same list as the XNA rule XNA050).
- One finding per project per rule (project-level), with the `Content.mgcb` path as the file and the line of the first match. Line numbers come from the parser.

Implementation: `MgcbParser` in Core turns text into `MgcbFile(GlobalSettings, References, Entries)`, each with a 1-based line number. `MgcbSettingMatcher` sits with the other matchers in `Analyzer/Rules/Matchers`. `ProjectInfo` gains `MgcbFiles` (paths) and the reader stores parsed files in `ProjectReadResult` so the matcher does not read disk. All file access still goes through `ReadOnlyFileSystem`.

## Analyzer changes in detail

1. **Discovery**: `ProjectDiscovery` already enumerates `.csproj` and `.sln`. It now asks both detectors and records the family. It also lists `Protobuild.exe` and `Build/Projects/*.definition` (file names only) so R1.5 can explain the Protobuild case.
2. **Reading**: `ProjectReader` fills the new `ProjectInfo` fields. It also reads `packages.config` next to the project and follows `Import` of `.projitems` (R3.3). Compile and content items from a `.projitems` file are added to the project with their path made relative to the project folder.
3. **Engine**: `RuleEngine` takes a list of catalogs and runs each against the projects whose family matches `appliesTo`. Findings from both catalogs go into one report. Duplicate ids across catalogs are rejected when the catalogs load.
4. **Scoring**: unchanged. Each catalog carries its own weights. The report uses the weights of the catalog that owns each rule.
5. **Report**: the project table gains family, MonoGame version (with confidence), and platform. JSON adds fields only. Markdown adds two columns. The no-project reasons are updated.

`ProjectAnalyzer.AnalyzeAsync(path, RuleCatalog catalog, ...)` keeps working. A new overload takes `IReadOnlyList<RuleCatalog>`. The caller (App, CLI, tests) decides which catalogs to load.

## Migrator changes in detail

### What is reused as it is

| Component | Use |
|---|---|
| `OutputGuard`, `OutputWriter`, `ManifestWriter` | Same safety, same manifest. Manifest entries gain the rule ids `MG###`. |
| `MigrationPlanner` | Works from AutoFixable findings and rule ids. No change except that the plan item id includes the project. |
| `SolutionGenerator`, `ToolManifestGenerator`, `BuildIsolationGenerator` | Same. |
| `PackageSelector` | Same output. New input: it can take a `MonoGamePlatform` instead of an `XnaPlatform`. |
| `RewriterRegistry`, `SourceRewriteEngine`, `MarkerInserter`, `EntryPointRewriter`, `DiffBuilder` | Same contracts. |
| Compat shims (`XnaReborn.Compat`) | Reused for `Microsoft.Xna.Framework.GamerServices`; `Net` is a stretch (task 13.2). |
| `ImporterMap` (`rules/importer-map.json`) | Reused to decide which `.mgcb` entries are standard. |

### New or changed

| Component | Project | What it does |
|---|---|---|
| `LegacyMonoGameProjectGenerator` (a mode of `SdkCsprojGenerator`) | Migrator/Projects | Builds an SDK project from a legacy MonoGame `ProjectInfo`: assembly name, compile and content items, `DefineConstants` filtered of SDK defaults, project references, desktop target from `MonoGamePlatform`, packages from `PackageSelector`, plus mapped packages. Drops everything listed in R6.4. |
| `PackagesConfigMapper` | Migrator/Projects | `packages.config` to `PackageReference`. MonoGame and SharpDX packages are dropped (the MonoGame package brings its own dependencies). Unknown or `developmentDependency` entries are copied with the same id and version. A package whose `targetFramework` is a Windows 8 or Windows Phone moniker goes to manual work. No network. |
| `MgcbUpgrader` | Migrator/Content | Text-level edit of an existing `Content.mgcb`: set `/platform` to the target, delete install-folder `/reference` lines, keep everything else byte for byte including entry order. Custom pipeline references and non-standard pairs are copied and listed. |
| `ColorTransparentBlackRewriter` (MG032) | Migrator/Source/Rewriters | `Color.TransparentBlack` to `Color.Transparent`, only when the file imports `Microsoft.Xna.Framework`. |
| `EntryPointRewriter` registered for legacy MonoGame | Migrator/Source | Existing rewriter. Old MonoGame templates often wrap `Program` in `#if WINDOWS \|\| LINUX`. The rewriter already handles `#if` around `Main`; the registration is the only new part. |
| Platform project handling | Migrator/Planning | Platform projects (MG012 to MG016) become `Skipped` plan items. Their files are copied unchanged. The manifest lists them as manual work. |
| Shared project linking | Migrator/Projects | Compile and content items that came from a `.projitems` file are written as `Compile Include="..\Shared\X.cs" Link="X.cs"` in the migrated desktop project. |

### Choosing the desktop target

| Old platform | New target | Package | TFM |
|---|---|---|---|
| Windows (DirectX) | WindowsDX | `MonoGame.Framework.WindowsDX` | `net8.0-windows` |
| WindowsGL, Linux, macOS, DesktopGL | DesktopGL | `MonoGame.Framework.DesktopGL` | `net8.0` |
| UWP, Windows 8, Windows Phone, Android, iOS (shared code only) | DesktopGL by default | `MonoGame.Framework.DesktopGL` | `net8.0` |

DesktopGL is the default for the last row because it needs no Windows-only API and is the safest target for shared code. The user can switch to WindowsDX. The existing note stays true: the `net8.0-windows` target for WindowsDX 3.8.x is **unverified** and is checked by the integration test in task 15.2.

### Pipeline for one legacy project

1. Guard (`OutputGuard`).
2. Plan: AutoFixable `MG###` findings become plan items; Manual and Unsupported findings become manifest entries.
3. Project: `LegacyMonoGameProjectGenerator` writes the `.csproj`; `SolutionGenerator` writes the `.sln` (R9.4); `ToolManifestGenerator` and `BuildIsolationGenerator` write their files.
4. Source: copy `.cs` files, run rewriters by rule id, add markers.
5. Content: `MgcbUpgrader` patches `Content.mgcb`; assets are copied unchanged; prebuilt `.xnb` and extension DLLs are copied and listed.
6. Write (materialize `FileChange` records), manifest, optional verify through the Runner.

The original is read only. Steps 3 to 5 produce in-memory `FileChange` records, as for XNA.

## Runner changes

`hints.json` gains entries linked to rules. Sample diagnostics for the tests are written by hand (no real build needed).

| Hint | Match | Rule |
|---|---|---|
| H100 | `CS0234` message contains `Microsoft.Xna.Framework.Net` | MG028 |
| H101 | `CS0234` or `CS0246` message contains `GamerServices` | MG029 |
| H102 | `CS0117` message contains `TransparentBlack` | MG032 |
| H103 | `CS0246` message contains `OpenTK` | MG030 |
| H104 | `NETSDK1100` or message contains `Windows targeting` | none (WindowsDX on a non-Windows host: advise DesktopGL) |
| H105 | MGCB error message contains a missing pipeline assembly path | MG025 |

No runner logic changes. `MigratedCopyGuard` already refuses a folder without the manifest marker.

## Studio UI

Small and read-only: the project list shows family, version with confidence, and platform; the findings view already groups by rule. A platform project is shown as "not migrated, see manual work". UI code calls the same analyzer and migrator APIs and holds no migration logic.

## Safety design

- Analyzer additions use `ReadOnlyFileSystem` only. The write-API source scan test covers the new files.
- No executable from the user's folder is run (Protobuild, scripts, build targets).
- The migrator writes only into the new output folder. `OutputGuard` rules are unchanged.
- No network. Package versions come from `packages.json` and from the project's own files.
- Fixtures are original and synthetic. See the next section for real projects used by hand.

## Fixtures

Written from scratch under `tests/fixtures/`, each with a `README.md` that states it is original and under the repository license. They copy the shapes seen in the research, not any file.

| Fixture | Shape | Positive for |
|---|---|---|
| A `mg37-windowsdx-pkgconfig` | Installer imports, `packages.config` with `MonoGame.Framework.WindowsDX` 3.7.0.1708 and a SharpDX package, `net47`, x86, a `.xnb` | MG001 to MG007, MG020, MG027 |
| B `mg37-desktopgl-installdir` | `Assemblies\DesktopGL` HintPaths, `OpenTK` and `NVorbis` references, `Content.mgcb` with an install-folder reference, a custom pipeline DLL reference and a custom importer | MG001 to MG006, MG017, MG019, MG023, MG025, MG026 |
| C `mg34-windowsgl` | `Assemblies\WindowsGL`, `Tao.Sdl`, `MonoGame.Framework.Net` and `Lidgren.Network` references, no `.props` import | MG003, MG004, MG011, MG018, MG021, MG022 |
| D `mg35-win8-wp8-ouya` | Windows 8 / Windows Phone / OUYA style platform properties and an mgcb with a legacy platform | MG015, MG016, MG024 |
| E `mg37-multiplatform-sln` | A solution with a shared project, a desktop project, an Android project, an iOS project, and a UWP project | MG012 to MG014, MG024 |
| F `mg3x-protobuild` | `Protobuild.exe` (a text placeholder, never executed), `Build/Projects/Game.definition`, no `.csproj` | MG010 |
| G `mg38-old-pin` | SDK-style, `netcoreapp3.1`, `MonoGame.Framework.DesktopGL` 3.8.0.1641 | MG008, MG009 |
| H `mg38-current` | SDK-style, `net8.0`, 3.8.6.1, `Content.mgcb` with `/platform:DesktopGL` | negative for every rule; score 100 |
| I `mg37-source-apis` | A project with source that uses `Microsoft.Xna.Framework.Net`, `GamerServices`, `OpenTK`, `Windows.UI.Xaml`, and `Color.TransparentBlack`, plus a file that uses `Color.Transparent` | MG028 to MG032 |

Expected results are Verify snapshots in `tests/XnaReborn.Tests/Snapshots/`.

## Real projects for manual checks

Read only. Not cloned into this repository. Their code and assets are not copied here. A person clones them into a scratch folder outside the repo, runs the analyzer, migrates to a new folder, builds, and records the result in the checkpoint task. Licenses were read from the GitHub API on 2026-11-11 and must be rechecked in the repository before use.

| Project | License | Why it is useful |
|---|---|---|
| [Kosmonaut3d/DeferredEngine](https://github.com/Kosmonaut3d/DeferredEngine) | MIT | 3.7.0.213 on Windows, net461, x86, installer imports, `Content.mgcb` with models and effects. Large content set. |
| [craftworkgames/MonoGame.Squid](https://github.com/craftworkgames/MonoGame.Squid) | MIT | DesktopGL installer layout with `OpenTK` and `NVorbis` references, net452. Small. |
| [roy-t/MSDF](https://github.com/roy-t/MSDF) | MIT | WindowsDX from NuGet with `packages.config` (3.7.0.1708), SharpDX packages, net47. |
| [Kosmonaut3d/BloomFilter-for-Monogame-and-XNA](https://github.com/Kosmonaut3d/BloomFilter-for-Monogame-and-XNA) | MIT | Smallest installer-era Windows project (net45). A good first end-to-end run. |
| [TheLastSliceGame/TheLastSliceGame](https://github.com/TheLastSliceGame/TheLastSliceGame) | MIT | UWP project (`WindowsStoreApp`, `AppContainerExe`). Tests the platform-project path. |
| [tinco/Farseer-Physics](https://github.com/tinco/Farseer-Physics) | Not stated (`NOASSERTION`) | WindowsGL layout with `Tao.Sdl`. **Not a test candidate until the license is checked.** Listed only because it is the clearest WindowsGL sample found. |

Three or more permissive candidates are listed (five MIT). They are code libraries and demos more than finished games, and the DeferredEngine and BloomFilter projects are the only ones likely to run end to end. A finished 3.x game with a permissive license should be added when one is found. Task 17 asks for that.

## Error handling

- A failing parser on one file adds a Diagnostic and the project is still analyzed with what was read.
- A failing rewriter on one file copies the original file unchanged, adds a marker, and adds a manifest entry (existing behavior).
- A `packages.config` entry that cannot be mapped goes to manual work. It never stops the migration.
- Cancellation is checked between steps and files, as before.

## Testing strategy

1. **Detector tests**: family and version for each fixture, signals alone and in conflict, XNA plus MonoGame mix gives `ANA008`.
2. **Parser tests**: `MgcbParser` round-trip line numbers on real-shaped input, `PackagesConfigReader`, `.projitems` import.
3. **Matcher tests**: `mgcbSetting` and `projectItem.metadata`, positive and negative.
4. **Catalog tests**: schema accepts `MG###`, rejects other ids, rejects duplicate ids across catalogs, `appliesTo` routing (an XNA rule never fires on a MonoGame project and the reverse).
5. **Rule tests**: for every rule, a positive fixture test and a negative fixture test (`Category=Rules`).
6. **Scoring tests**: `mg38-current` scores 100; fixture B has a projected score above its score.
7. **Report snapshots** for every fixture, JSON and Markdown, plus a determinism test.
8. **Safety tests**: hash-unchanged and no-new-files over every new fixture for analysis and for migration; write-API scan.
9. **Migrator tests**: project generation snapshots, `MgcbUpgrader` before and after, `PackagesConfigMapper` cases, rewriter before and after including the ambiguous case, platform project skipped, shared project linked.
10. **Integration** (`XNAREBORN_INTEGRATION=1`): migrate fixtures A and B, run `dotnet build` on the result, verify the source hash.
11. No test uses the network.

## Open questions

1. **Platform names.** The `MonoGamePlatform` values for Windows 8, Windows Phone, OUYA, PSM and Vita, the GUID for Xamarin.iOS, and the mgcb platform names are marked "to confirm". Task 1 settles them from the 3.7 and 3.8.6.1 sources and from sample projects. If a name is wrong, the rule is a catalog edit.
2. **`MonoGame.Common.props` start version.** Not found in the changelog. The detector does not use it as a version signal until it is confirmed.
3. **WindowsGL end of life.** The last release that shipped WindowsGL is not stated in the sources read.
4. **SharpDX with WindowsDX 3.8.6.1.** Confirm the dependency version from the nuspec, then decide whether MG020 can become AutoFixable.
5. **WindowsDX on `net8.0-windows`.** Still unverified for 3.8.x in this product. The integration test decides.
6. **Android and iOS output.** Not generated here. A follow-up spec could target `net8.0-android` and `net8.0-ios`.
7. **Sibling specs.** `sharpdx-toolkit` owns direct SharpDX code. `flash-to-monogame` is unrelated. This spec only drops redundant SharpDX references and defers the rest.

## Sources

- [MonoGame docs: Migrating from 3.7](https://docs.monogame.net/articles/migration/migrate_37.html)
- [MonoGame docs: What's new](https://docs.monogame.net/articles/whats_new.html)
- [MonoGame docs: Updating versions](https://docs.monogame.net/articles/migration/updating_versions.html)
- [MonoGame CHANGELOG.md](https://github.com/MonoGame/MonoGame/blob/develop/CHANGELOG.md)
- [MonoGame 3.8 Protobuild removal commit](https://github.com/MonoGame/MonoGame/commit/6138570b31a9edcd5daec43774106b822ac4f4e6)
- [MonoGame 3.8.1 release thread](https://community.monogame.net/t/monogame-3-8-1/17786)
- NuGet search for `MonoGame.Framework.*` package ids and first/latest versions (queried 2026-11-11)
- GitHub repository contents for the projects in the tables above (queried 2026-11-11)
