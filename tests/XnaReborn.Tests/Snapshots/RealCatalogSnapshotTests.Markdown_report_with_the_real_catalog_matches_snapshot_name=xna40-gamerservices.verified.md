# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 55 / 100 |
| Projected score after automatic fixes | 62 / 100 |
| Projects analyzed | 1 |
| Findings | 7 |

Severity: Blocker 1, Error 4, Warning 1, Info 1

Classification: AutoFixable 5, Manual 1, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40GamerServices.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Xna40GamerServices.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Xna40GamerServices.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +2 more`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Xna40GamerServices.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Xna40GamerServices.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Xna40GamerServices.csproj`
  - Code: `XnaFrameworkVersion=v4.0`

### Manual

- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `Xna40GamerServices.csproj`
  - Location: `Game1.cs:5:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `Xna40GamerServices.csproj`
  - Location: `Game1.cs:3:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`

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

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA012 Storage (StorageDevice, StorageContainer)

Code uses StorageDevice and StorageContainer for save games. MonoGame removed the Storage namespace because it did not port across platforms.

**Resolution:** Write saves with System.IO under Environment.SpecialFolder.ApplicationData and read packaged files with TitleContainer.OpenStream. The optional shim project (project-migrator:12.1) can stand in for the calls while you rewrite.

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1
