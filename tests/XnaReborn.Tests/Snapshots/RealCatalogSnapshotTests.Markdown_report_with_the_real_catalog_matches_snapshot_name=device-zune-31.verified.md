# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 87 / 100 |
| Projected score after automatic fixes | 96 / 100 |
| Projects analyzed | 1 |
| Findings | 11 |

Severity: Blocker 0, Error 3, Warning 5, Info 3

Classification: AutoFixable 8, Manual 3, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Zune31.csproj | 3.1 | Zune | - | v3.5 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Zune31.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.Common.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Zune31.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Zune31.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Zune31.csproj`
  - Code: `TargetFrameworkVersion=v3.5`
- **[Warning] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `Zune31.csproj`
  - Code: `XnaPlatform=Zune`
- **[Warning] XNA053** Device conditional compilation symbols
  - Project: `Zune31.csproj`
  - Location: `Game1.cs:17:5`
  - Code: `#if ZUNE`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Zune31.csproj`
  - Code: `XnaFrameworkVersion=v3.1`
- **[Info] XNA062** GamePad.GetState (keyboard fallback and Back button)
  - Project: `Zune31.csproj`
  - Location: `Game1.cs:31:32`
  - Code: `GamePadState pad = GamePad.GetState(PlayerIndex.One);`

### Manual

- **[Warning] XNA056** Zune media library
  - Project: `Zune31.csproj`
  - Location: `Game1.cs:11:17`
  - Code: `private MediaLibrary library;`
- **[Warning] XNA056** Zune media library
  - Project: `Zune31.csproj`
  - Location: `Game1.cs:25:27`
  - Code: `library = new MediaLibrary();`
- **[Info] XNA006** XNA 3.x project
  - Project: `Zune31.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

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

### XNA006 XNA 3.x project

The project targets XNA 3.0 or 3.1. XNA 4.0 broke much of the graphics, storage, and effect API, and MonoGame follows the 4.0 API. Premultiplied alpha is also the default from 4.0 on, so sprite blending can look different after the move. Each concrete break has its own rule.

**Resolution:** Fix the findings from the XNA 3.x rules, then compare the rendered result of the migrated game with the original, especially alpha-blended sprites.

### XNA007 Project targets Xbox 360, Zune, or Windows Phone

XnaPlatform names a device platform (Xbox 360, Zune, or Windows Phone). The project cannot run on a desktop as it is, because it depends on device APIs and device-only #if branches. The device rules XNA053 to XNA063 list what the code uses.

**Resolution:** The migrator turns the project into a Windows DesktopGL game: it skips platform copies of the same game, folds the device #if branches, and adds stand-ins that map touch, tilt, and the Back button to the mouse and keyboard. What it cannot convert is listed as manual work.

Migrator task: device-to-desktop:5.1

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA053 Device conditional compilation symbols

An #if or #elif directive names a platform symbol (XBOX, XBOX360, ZUNE, WINDOWS_PHONE, or WINDOWS). The migrated project defines none of them, so the wrong branch would be compiled. Inactive branches count too.

**Resolution:** The migrator keeps the branch that is true for the chosen platform and removes the other branches. A condition that also depends on another symbol, such as DEBUG, is left as it is with a marker.

Migrator task: device-to-desktop:6.1

### XNA056 Zune media library

Code browses the device media library (songs, albums, playlists). MonoGame has no media library.

**Resolution:** Replace the library calls with your own list of files, or remove the feature.

### XNA062 GamePad.GetState (keyboard fallback and Back button)

Code reads the game pad. A phone or Zune has no pad, only a Back button, and many desktops have no pad either. The check is by name only: a game class called GamePad in another namespace is not told apart from the XNA one.

**Resolution:** The migrator changes the call to a replacement that returns the real pad when one is connected, and keyboard input otherwise (Escape is Back).

Migrator task: device-to-desktop:8.1
