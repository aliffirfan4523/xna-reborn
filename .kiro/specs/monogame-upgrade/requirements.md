# Requirements: MonoGame Upgrade

## Introduction

XNA Reborn already moves XNA projects to MonoGame 3.8.6.1 on net8.0. Many stranded projects are not XNA at all. They are old MonoGame projects (3.0 to 3.7.x) that were built against a Windows installer, `MonoGame.Common.props`, `packages.config`, .NET Framework 4.x, or Protobuild, and that do not build with a current SDK. This feature lets the analyzer recognise those projects, report what is wrong, and lets the migrator write an upgraded copy: an SDK-style `net8.0` project on MonoGame 3.8.6.1 with a working `Content.mgcb`.

The same rules as the rest of the product apply. The original project is never modified. The analyzer is read-only. Output goes to a new folder. Nothing is uploaded.

This spec reuses the analyzer, migrator, content and runner specs. It adds a project family, a MonoGame version detector, one new match type (`mgcbSetting`), one optional field on `projectItem`, a second rule catalog (`rules/monogame-upgrade.json`, ids `MG001` and up), and a few migrator tasks.

## Glossary

- **Legacy MonoGame project**: a `.csproj` that references MonoGame but is not an SDK-style 3.8 project on a supported target. Examples: `Reference Include="MonoGame.Framework"` with an install-folder `HintPath`, an import of `MonoGame.Common.props` or `MonoGame.Content.Builder.targets`, or a NuGet `MonoGame.Framework.*` package listed in `packages.config`.
- **Project family**: `Xna`, `MonoGameLegacy`, `MonoGameCurrent`, or `Other`. `MonoGameCurrent` is an SDK-style project already on a 3.8.x package. It is detected so the analyzer can say "nothing to do" (or "bump the pin") instead of "not a MonoGame project".
- **MonoGame version band**: the best-supported statement of the MonoGame version, with a confidence (`High` when an exact version string is read, `Medium` when only a band is known, `Low` when only a weak hint exists, `Unknown`).
- **Platform project**: a project that targets Android, iOS, UWP, Windows 8, Windows Phone, or another non-desktop target.
- **Desktop target**: `DesktopGL` or `WindowsDX`, as in the migrator spec.
- **mgcb file**: a MonoGame Content Builder description file (`Content.mgcb`).
- **Upgrade rule**: an entry in `rules/monogame-upgrade.json`. Same shape as an XNA rule; ids use `MG###`.
- **Protobuild project**: a repository that uses Protobuild (`Protobuild.exe`, `Build/Projects/*.definition`) to generate project files.

## Requirements

### R1: Family detection
**User story:** As a developer with an old MonoGame project, I want the tool to recognise it instead of saying "not an XNA project".

Acceptance criteria:
1. THE analyzer SHALL assign each discovered `.csproj` one project family.
2. WHEN a project references `MonoGame.Framework*` (as a `Reference`, a `PackageReference`, or a `packages.config` entry), imports a `MonoGame\v3.0\MonoGame.*` props or targets file, or sets `MonoGamePlatform` or `MonoGameContentReference`, THE analyzer SHALL mark it MonoGame.
3. WHEN a MonoGame project uses `Sdk="Microsoft.NET.Sdk"`, `net8.0` (or `net8.0-windows`), and `MonoGame.Framework.*` at version 3.8.6.1, THE analyzer SHALL mark it `MonoGameCurrent`. Any other MonoGame project SHALL be `MonoGameLegacy`.
4. THE analyzer SHALL NOT treat a project that references both XNA assemblies and MonoGame assemblies as `MonoGameLegacy`. It SHALL keep the `Xna` family and add a Diagnostic that lists the mix.
5. IF a folder contains only Protobuild definitions and no generated `.csproj`, THEN THE analyzer SHALL return a result that says so and says what to do. It SHALL NOT run any executable from the user's folder.
6. THE analyzer SHALL return projects of all families in one stable order, and `NoProjectReason` SHALL distinguish "no .csproj", "projects found but none use XNA or MonoGame", and "all MonoGame projects are already current".

### R2: Version and platform detection
**User story:** As a developer, I want to know which MonoGame version and platform my project was built for.

Acceptance criteria:
1. THE analyzer SHALL collect these signals: assembly version in a `MonoGame.Framework` reference, `HintPath` platform folder (`Assemblies\Windows`, `WindowsGL`, `DesktopGL`, `WindowsUniversal`, and others), NuGet package id and version from `PackageReference` and `packages.config`, `MonoGamePlatform` property, `TargetFrameworkVersion` or `TargetFramework`, `ProjectTypeGuids`, and the imports present.
2. WHEN an exact version string is found, THE analyzer SHALL report it with `High` confidence.
3. WHEN only a platform or import signal is found, THE analyzer SHALL report a band (for example "3.2 to 3.5") with `Medium` confidence.
4. WHEN signals disagree, THE analyzer SHALL report the most specific one and add a Diagnostic that lists the conflict.
5. WHEN no signal is found, THE analyzer SHALL report `Unknown` and SHALL NOT guess.
6. THE analyzer SHALL report the MonoGame platform (Windows DirectX, WindowsGL, DesktopGL, Linux, macOS, Android, iOS, Windows 8, Windows Phone, UWP, other) separately from the version.

### R3: Reading old MonoGame files
**User story:** As a developer, I want my project files read accurately without running my build.

Acceptance criteria:
1. THE analyzer SHALL read `.csproj`, `packages.config`, and `Content.mgcb` as text or XML and SHALL NOT evaluate MSBuild, run Protobuild, or run any executable.
2. THE analyzer SHALL read each `Content.mgcb` named by a `MonoGameContentReference` item, or found by convention at `Content/Content.mgcb`, and extract global properties (`/platform`, `/profile`, `/config`, `/compress`, `/outputDir`, `/intermediateDir`), `/reference` entries, and each content entry with its importer, processor, processor parameters, and build path.
3. THE analyzer SHALL follow `Import` of `.projitems` files from shared projects, so compile and content items in them are seen.
4. WHEN a file cannot be parsed, THE analyzer SHALL add a Diagnostic and continue with the other projects.
5. THE analyzer SHALL keep the `ReadOnlyFileSystem` rule: no write APIs and a hash-unchanged test over every new fixture.

### R4: Upgrade rule catalog
**User story:** As a maintainer, I want upgrade rules to be data.

Acceptance criteria:
1. THE analyzer SHALL load `rules/monogame-upgrade.json` with the same loader, schema, and checks as `rules/xna-to-monogame.json`.
2. THE rule schema SHALL accept ids that match `^(XNA|MG)[0-9]{3}$`, and the loader SHALL reject duplicate ids across both catalogs when both are loaded in one run.
3. THE catalog SHALL declare which family it applies to. THE engine SHALL run a catalog only against projects of that family.
4. THE analyzer SHALL support the existing nine match types plus `mgcbSetting`, and `projectItem` SHALL accept an optional `metadata` filter. Existing catalogs and fixtures SHALL keep working with no change.
5. THE catalog SHALL require a `migratorTask` link on every `AutoFixable` rule, as before.
6. EVERY rule SHALL have a positive and a negative fixture test.

### R5: Findings and score
**User story:** As a presenter, I want a before and after number for an old MonoGame project too.

Acceptance criteria:
1. THE analyzer SHALL report findings, triggered rules, counts, score, and projected score for MonoGame projects with the same shape and weights as for XNA projects.
2. A `MonoGameCurrent` project with no findings SHALL score 100.
3. WHEN a solution holds XNA and MonoGame projects, THE report SHALL keep the family on each project, and the score SHALL count each distinct rule once.
4. THE JSON report SHALL keep `schemaVersion` and SHALL add the new project fields without removing or renaming existing ones.

### R6: Migrated project file
**User story:** As a developer, I want an SDK-style project that builds on MonoGame 3.8.6.1 and net8.0.

Acceptance criteria:
1. THE migrator SHALL write an SDK-style `.csproj` for each migrated project, with `net8.0` for DesktopGL and `net8.0-windows` for WindowsDX (same as the XNA path), the package versions pinned in `packages.json`, and `MonoGame.Content.Builder.Task`.
2. THE migrator SHALL choose the desktop target from the old platform: `Windows` gives WindowsDX; `WindowsGL`, `DesktopGL`, `Linux`, and `MacOS` give DesktopGL; the user MAY override the choice.
3. THE migrator SHALL keep the project's assembly name, root namespace, compile items, content items, `DefineConstants` (without the Debug and Trace defaults the SDK already sets), and `ProjectReference` items to other migrated projects.
4. THE migrator SHALL drop the MonoGame install-folder `Reference`, the `MonoGame.Common.props` and `MonoGame.Content.Builder.targets` imports, `MonoGamePlatform`, `PlatformTarget` x86, `TargetFrameworkVersion`, project type GUIDs, and per-configuration `OutputPath` values that use `$(MonoGamePlatform)`.
5. THE migrator SHALL map `packages.config` entries that are not MonoGame packages to `PackageReference` using the id and version from the file. IF an entry cannot be mapped without guessing, THEN THE migrator SHALL list it as manual work and SHALL NOT call the network.
6. THE migrator SHALL set `GenerateAssemblyInfo=false` when a kept `Properties/AssemblyInfo.cs` exists, as in the XNA path.
7. THE migrator SHALL write the `.config/dotnet-tools.json` manifest with the pinned `dotnet-mgcb` when the project has content.
8. THE migrator SHALL block parent `Directory.Build.props` and `Directory.Packages.props` inheritance in the copy, as in the XNA path.

### R7: Content upgrade
**User story:** As a developer, I want my content to keep building with the asset names the game already loads.

Acceptance criteria:
1. THE migrator SHALL copy every asset and keep every asset name, importer, processor, and processor parameter in `Content.mgcb` unchanged unless a rule says otherwise.
2. THE migrator SHALL rewrite `/platform` to match the chosen target, SHALL remove `/reference` entries that point into an old MonoGame install folder, and SHALL keep the remaining `/reference` entries.
3. THE migrator SHALL flag each custom pipeline assembly reference and each non-standard importer or processor pair as manual work, because extensions built for an older MonoGame have to be rebuilt.
4. THE migrator SHALL NOT rebuild or modify prebuilt `.xnb` files. It SHALL copy them and list them as manual work.
5. WHEN the project has no `Content.mgcb`, THE migrator SHALL NOT invent one.
6. THE migrator SHALL produce byte-identical `Content.mgcb` for identical input.

### R8: Source upgrade
**User story:** As a developer, I want mechanical API changes done for me and the rest marked.

Acceptance criteria:
1. THE migrator SHALL fix AutoFixable source rules with rewriters registered by rule id, using the existing rewriter contract and `MarkerInserter`.
2. THE migrator SHALL leave a `// XNAREBORN: <rule id>: <what to do>` marker at each Manual source finding, and SHALL leave untouched files byte for byte.
3. WHEN the entry point sits inside a platform-only `#if`, THE migrator SHALL unwrap it with the existing `EntryPointRewriter`.
4. WHEN shims are enabled, THE migrator SHALL offer the `XnaReborn.Compat` stand-ins for `Microsoft.Xna.Framework.GamerServices` and `Microsoft.Xna.Framework.Net` usage. The default stays off.

### R9: Platform projects and solutions
**User story:** As a developer with an Android, iOS, or UWP project in my solution, I want my game code migrated and the platform project reported honestly.

Acceptance criteria:
1. WHEN a solution holds one or more platform projects, THE migrator SHALL migrate the desktop projects, SHALL copy each platform project unchanged into the output as `Skipped`, and SHALL list each as manual work with the reason.
2. THE migrator SHALL NOT generate Android, iOS, or UWP projects in this version.
3. WHEN the game code lives in a shared project, THE migrator SHALL link its compile and content items into the migrated desktop project, and SHALL leave the `.shproj` and `.projitems` files in place as copies.
4. THE generated `.sln` SHALL list only projects that the migrator wrote as SDK-style.
5. IF the solution holds no desktop project, THEN THE migrator SHALL create a DesktopGL project from the shared code only when the user confirms, and SHALL otherwise stop with a clear message.

### R10: Build, run, and hints
**User story:** As a developer, I want to see the upgraded copy build and run.

Acceptance criteria:
1. THE runner SHALL build and launch the migrated copy through the existing `MigratedCopyGuard`, which requires the manifest marker.
2. THE hint catalog SHALL gain hints for the errors that follow from the MG rules (for example `CS0234` for `Microsoft.Xna.Framework.Net`, `CS0117` for `Color.TransparentBlack`, `NETSDK1100`, and an MGCB error that names a missing pipeline assembly).
3. EACH new hint SHALL link to its `MG###` rule, with a matching and a non-matching sample diagnostic in the tests.

### R11: Safety
**User story:** As a user, I want my project untouched.

Acceptance criteria:
1. THE feature SHALL write only inside the new output folder. A test SHALL hash every file in each new fixture before and after analysis and migration and assert the source did not change.
2. THE feature SHALL NOT run `Protobuild.exe`, any `.exe`, any script, or any MSBuild target found in the user's folder.
3. THE feature SHALL NOT overwrite an existing output folder without explicit confirmation.
4. THE feature SHALL NOT call the network. Package ids and versions come from the project's own files and from `packages.json`.
5. Fixtures SHALL be original and synthetic. Real open-source games listed in the design are used for manual checks outside the repository, and nothing from them is copied into the repository.

### R12: Determinism and tests
**User story:** As a maintainer, I want predictable output.

Acceptance criteria:
1. GIVEN identical input and options, THE analyzer and migrator SHALL produce identical reports and identical output trees.
2. THE test suite SHALL include a positive and a negative fixture test per rule, a determinism test, a source-hash-unchanged test, and snapshot reports for every new fixture.
3. NO test SHALL need the network. Tests that run `dotnet build` on a migrated fixture SHALL be integration tests behind `XNAREBORN_INTEGRATION=1`.
