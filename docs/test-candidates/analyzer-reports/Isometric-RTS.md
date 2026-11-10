# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 90 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 2 |
| Findings | 14 |

Severity: Blocker 0, Error 7, Warning 4, Info 3

Classification: AutoFixable 14, Manual 0, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| MarsRTS/MarsRTS/MarsRTS.csproj | 4.0 | Windows | HiDef | v4.0 |
| RTSAssetData/RTSAssetData.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics`
- **[Error] XNA003** XNA assembly references
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +6 more`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `..\MarsRTSContent\MarsRTSContent.contentproj`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `NoStdLib=true`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `RTSAssetData/RTSAssetData.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `MarsRTS/MarsRTS/MarsRTS.csproj`
  - Code: `.NETFramework,Version=v4.0,Profile=Client, Microsoft.Net.Client.3.5, Microsoft.Net.Framework.3.5.SP1 +1 more`

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
