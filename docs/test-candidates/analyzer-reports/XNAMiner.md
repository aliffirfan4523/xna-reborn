# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 38 / 100 |
| Projected score after automatic fixes | 50 / 100 |
| Projects analyzed | 6 |
| Findings | 57 |

Severity: Blocker 3, Error 30, Warning 12, Info 12

Classification: AutoFixable 50, Manual 1, Unsupported 6

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj | 4.0 | Xbox360 | HiDef | v4.0 |
| ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj | 4.0 | Windows | HiDef | v4.0 |
| ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj | 4.0 | Xbox360 | HiDef | v4.0 |
| ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj | 4.0 | WindowsPhone | Reach | v4.0 |
| ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj | 4.0 | Windows | HiDef | v4.0 |
| XNAMiner/XNAMiner/XNAMiner.csproj | 4.0 | Windows | Reach | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.Game +7 more`
- **[Error] XNA003** XNA assembly references
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.Game +7 more`
- **[Error] XNA003** XNA assembly references
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.Game +7 more`
- **[Error] XNA003** XNA assembly references
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.GamerServices, Microsoft.Xna.Framework.Graphics +5 more`
- **[Error] XNA003** XNA assembly references
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `Microsoft.Xna.Framework.Avatar, Microsoft.Xna.Framework.GamerServices, Microsoft.Xna.Framework.Graphics +5 more`
- **[Error] XNA003** XNA assembly references
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +1 more`
- **[Error] XNA005** Nested XNA content project
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA005** Nested XNA content project
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA005** Nested XNA content project
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA005** Nested XNA content project
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA005** Nested XNA content project
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `Content\ImageProcessingContent.contentproj`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `Content\ImageProcessingContent.contentproj`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `..\XNAMinerContent\XNAMinerContent.contentproj`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `Microsoft.Net.Client.3.5, Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0 +4 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Code: `Microsoft.Net.Client.3.5, Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0 +5 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `Microsoft.Net.Client.3.5, Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0 +4 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `Microsoft.Net.Client.3.5, Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0 +5 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/ParallelTasks.csproj`
  - Code: `Microsoft.Net.Client.3.5, Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0 +5 more`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +2 more`

### Manual

- **[Error] XNA013** XNA-only SpriteFont family
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Code: `../XNAMinerContent/SpriteFont1.spritefont: Segoe UI Mono`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Location: `ParallelTasks/ParallelTasks/ImageProcessing/Game1.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/ImageProcessing.csproj`
  - Location: `ParallelTasks/ParallelTasks/ImageProcessing/Game1.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `XNAMiner/XNAMiner/XNAMiner.csproj`
  - Location: `XNAMiner/XNAMiner/Game1.cs:14:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Error] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `ParallelTasks/ParallelTasks/ImageProcessing/(360) ImageProcessing.csproj`
  - Code: `XnaPlatform=Xbox 360`
- **[Error] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(360) ParallelTasks.csproj`
  - Code: `XnaPlatform=Xbox 360`
- **[Error] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `ParallelTasks/ParallelTasks/ParallelTasks/(WP7) ParallelTasks.csproj`
  - Code: `XnaPlatform=Windows Phone`

## Rules

### XNA001 XNA Game Studio build targets import

The project file imports the XNA Game Studio MSBuild targets. They exist only on machines with XNA Game Studio 4.0 and Visual Studio 2010 installed. Without them the build stops at once with MSB4019 (imported project not found), as it did for Pirate-Treasure-Hunter.

**Resolution:** The migrator writes an SDK-style project for .NET 8 that does not import the XNA targets.

Migrator task: project-migrator:4.1

### XNA002 XNA-specific project properties

The project sets XnaFrameworkVersion, a property that only the XNA Game Studio targets understand. The same project usually also sets ProjectTypeGuids (the XNA game GUID), XnaPlatform, XnaProfile, XnaOutputType, and XnaCrossPlatformGroupID. None has an MSBuild equivalent in MonoGame.

**Resolution:** The migrator drops XNA-specific properties and project type GUIDs when it generates the SDK-style project. The graphics profile is carried into Content.mgcb as /profile.

Migrator task: project-migrator:4.1

### XNA003 XNA assembly references

The project references XNA assemblies (Microsoft.Xna.Framework and related) by strong name with Private=False. They do not resolve on a machine without XNA, so the project cannot build. MonoGame ships the same API under the same namespaces in its own package. This rule also fires for assemblies MonoGame does not provide (GamerServices, Net, Avatar, Storage, Xact, Video); those have their own rules for code that uses them.

**Resolution:** The migrator replaces the XNA references with the MonoGame DesktopGL or WindowsDX package.

Migrator task: project-migrator:4.1

### XNA004 Old .NET Framework target

The project targets the .NET Framework (3.5 for XNA 3.x, 4.0 with the Client profile for XNA 4.0). That framework version is not available to an SDK-style build, and current MonoGame releases need modern .NET.

**Resolution:** The migrator sets the target framework of the generated project to net8.0.

Migrator task: project-migrator:4.1

### XNA005 Nested XNA content project

Assets live in a nested .contentproj that imports the XNA content pipeline targets, which only XNA Game Studio can build. MonoGame builds content from a Content.mgcb file. Projects that reference the content project with a ProjectReference instead are covered by XNA049.

**Resolution:** The migrator generates Content.mgcb from the content project, copies the source assets, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1

### XNA007 Project targets Xbox 360, Zune, or Windows Phone

XnaPlatform names a device platform. Xbox 360 and Zune builds depend on device APIs, and neither platform can be a MonoGame DesktopGL target. Code in this project is likely to use platform-only APIs.

**Resolution:** Migrate the Windows project of the solution instead. Keep shared code files and remove the device-specific project and its #if branches by hand.

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA013 XNA-only SpriteFont family

A .spritefont names a font from the XNA Redistributable Font Pack, which XNA Game Studio 4.0 installed and Windows does not ship (Andy, Jing Jing, Kootenay, Lindsey, Miramonte, Moire, Motorwerk, News Gothic, Pericles, Pescadero, Quartz MS, Segoe Keycaps, Segoe UI Mono, Wasco Sans). The MonoGame content builder looks the font up in the system font folders, so the content build fails with 'Could not find ... font file' for each such file (Pirate-Treasure-Hunter: Pescadero in three files, Segoe UI Mono in one). The list matches src/XnaReborn.Migrator/Content/data/xna-only-fonts.json.

**Resolution:** Change FontName to an installed or bundled font, or add a .ttf to the content folder and name it. The migrator can substitute a close Windows font in the migrated copy when font substitution is turned on. Glyph widths differ from the original, so check the layout.

### XNA045 ClickOnce bootstrapper packages

The project lists ClickOnce bootstrapper packages, which come from the Publish tab of Visual Studio 2010 (the XNA templates include the XNA Framework redistributable). The same settings usually bring a hard-coded PublishUrl, ManifestKeyFile, and manifest signing. Nothing in an SDK-style MonoGame build uses them. Known limit: only BootstrapperPackage items trigger the rule, not the publish properties alone.

**Resolution:** The migrator writes a new SDK-style project that carries none of the publish settings. Publish the MonoGame game with dotnet publish.

Migrator task: project-migrator:4.1

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA047 NoStdLib breaks SDK-style builds

NoStdLib=true tells the compiler not to reference the standard library. The old XNA project templates set it. An SDK-style project supplies the references itself, so the compiler would miss System.Object and the build would fail.

**Resolution:** The migrator writes a new SDK-style project from a fixed property set that does not include NoStdLib.

Migrator task: project-migrator:4.1

### XNA049 Content project referenced with ProjectReference

The game project references a .contentproj with a ProjectReference (XnaReferenceType Content), the form used when the content project is a sibling project. MonoGame cannot load that reference and builds content from Content.mgcb instead. The analyzer reads this content project like a nested one, so the content-item rules (XNA017, XNA019, XNA020, XNA021) see the assets behind this form.

**Resolution:** The migrator removes the reference, generates Content.mgcb from the content project like it does for a nested content project (XNA005), copies the source assets into the game project, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1

## Diagnostics

- ANA001: Content project could not be read: file not found (`ParallelTasks/ParallelTasks/ImageProcessing/Content/Content.contentproj`)
