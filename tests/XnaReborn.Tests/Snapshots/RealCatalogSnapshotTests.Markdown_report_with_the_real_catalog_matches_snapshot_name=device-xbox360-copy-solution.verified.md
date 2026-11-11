# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 91 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 2 |
| Findings | 13 |

Severity: Blocker 0, Error 5, Warning 5, Info 3

Classification: AutoFixable 13, Manual 0, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Game/Game.csproj | 4.0 | Windows | HiDef | v4.0 |
| Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj | 4.0 | Xbox360 | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Game/Game.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Game/Game.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics`
- **[Error] XNA003** XNA assembly references
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Game/Game.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Game/Game.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `XnaPlatform=Xbox 360`
- **[Warning] XNA053** Device conditional compilation symbols
  - Project: `Game/Game.csproj`
  - Location: `Game/Program.cs:5:5`
  - Code: `#if WINDOWS || XBOX`
- **[Warning] XNA053** Device conditional compilation symbols
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Location: `Game/Program.cs:5:5`
  - Code: `#if WINDOWS || XBOX`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Game/Game.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `XnaFrameworkVersion=v4.0`
- **[Info] XNA052** Platform copy of another project
  - Project: `Xbox 360 Copy of Game/Xbox 360 Copy of Game.csproj`
  - Code: `3 of 3 Compile items are links`

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

### XNA007 Project targets Xbox 360, Zune, or Windows Phone

XnaPlatform names a device platform (Xbox 360, Zune, or Windows Phone). The project cannot run on a desktop as it is, because it depends on device APIs and device-only #if branches. The device rules XNA053 to XNA063 list what the code uses.

**Resolution:** The migrator turns the project into a Windows DesktopGL game: it skips platform copies of the same game, folds the device #if branches, and adds stand-ins that map touch, tilt, and the Back button to the mouse and keyboard. What it cannot convert is listed as manual work.

Migrator task: device-to-desktop:5.1

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA052 Platform copy of another project

At least half of this project's Compile items are links to files of another project. Game Studio's Create Copy of project for a platform command makes projects like this: they share the code of the original and add nothing of their own.

**Resolution:** The migrator migrates one project of the group (the Windows one when there is one) and skips each copy. You can change the choice in the migration plan.

Migrator task: device-to-desktop:5.2

### XNA053 Device conditional compilation symbols

An #if or #elif directive names a platform symbol (XBOX, XBOX360, ZUNE, WINDOWS_PHONE, or WINDOWS). The migrated project defines none of them, so the wrong branch would be compiled. Inactive branches count too.

**Resolution:** The migrator keeps the branch that is true for the chosen platform and removes the other branches. A condition that also depends on another symbol, such as DEBUG, is left as it is with a marker.

Migrator task: device-to-desktop:6.1
