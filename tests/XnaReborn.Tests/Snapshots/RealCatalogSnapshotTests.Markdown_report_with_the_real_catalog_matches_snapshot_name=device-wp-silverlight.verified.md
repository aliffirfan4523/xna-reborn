# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 82 / 100 |
| Projected score after automatic fixes | 88 / 100 |
| Projects analyzed | 1 |
| Findings | 6 |

Severity: Blocker 0, Error 3, Warning 2, Info 1

Classification: AutoFixable 5, Manual 0, Unsupported 1

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| WpSilverlight.csproj | 4.0 | WindowsPhone | Reach | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA003** XNA assembly references
  - Project: `WpSilverlight.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Graphics`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `WpSilverlight.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `WpSilverlight.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `WpSilverlight.csproj`
  - Code: `XnaPlatform=Windows Phone`
- **[Info] XNA002** XNA-specific project properties
  - Project: `WpSilverlight.csproj`
  - Code: `XnaFrameworkVersion=v4.0`

### Unsupported

- **[Error] XNA051** Windows Phone Silverlight application
  - Project: `WpSilverlight.csproj`
  - Code: `SilverlightApplication=true`

## Rules

### XNA002 XNA-specific project properties

The project sets XnaFrameworkVersion, a property that only the XNA Game Studio targets understand. The same project usually also sets ProjectTypeGuids (the XNA game GUID), XnaPlatform, XnaProfile, XnaOutputType, XnaCrossPlatformGroupID, and in Game Studio Express 1.0 XNAGlobalContentPipelineAssemblies and XNAProjectContentPipelineAssemblies. None has an MSBuild equivalent in MonoGame.

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

### XNA051 Windows Phone Silverlight application

The project is a Silverlight application (SilverlightApplication is true). Its pages, XAML and navigation have no MonoGame equivalent. This rule is provisional: the property set was written from the Microsoft documentation, not checked against a real hybrid project.

**Resolution:** Move the XNA game code into a plain XNA project first, then migrate that project. The Silverlight pages have to be rebuilt by hand.
