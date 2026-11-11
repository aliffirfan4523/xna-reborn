# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 86 / 100 |
| Projected score after automatic fixes | 96 / 100 |
| Projects analyzed | 1 |
| Findings | 9 |

Severity: Blocker 0, Error 4, Warning 3, Info 2

Classification: AutoFixable 7, Manual 2, Unsupported 0

## Projects

| Project | Language | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|---|
| VbXna40Basic.vbproj | Visual Basic | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `VbXna40Basic.vbproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `VbXna40Basic.vbproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA049** Content project referenced with ProjectReference
  - Project: `VbXna40Basic.vbproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA114** My Project AssemblyInfo.vb
  - Project: `VbXna40Basic.vbproj`
  - Code: `My Project/AssemblyInfo.vb`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `VbXna40Basic.vbproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `VbXna40Basic.vbproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `VbXna40Basic.vbproj`
  - Code: `XnaFrameworkVersion=v4.0`

### Manual

- **[Warning] XNA053** Device conditional compilation symbols
  - Project: `VbXna40Basic.vbproj`
  - Location: `Program.vb:1:5`
  - Code: `#If WINDOWS OrElse XBOX Then`
- **[Info] XNA110** Visual Basic project
  - Project: `VbXna40Basic.vbproj`
  - Code: `Game1.vb, Program.vb, My Project\AssemblyInfo.vb`

## Rules

### XNA001 XNA Game Studio build targets import

The project file imports the XNA Game Studio MSBuild targets (Microsoft.Xna.GameStudio.targets from 3.0 on, Microsoft.Xna.Common.targets and Microsoft.Xna.ContentPipeline.targets in Game Studio Express 1.0). They exist only on machines with the matching XNA Game Studio and Visual Studio installed. Without them the build stops at once with MSB4019 (imported project not found), as it did for Pirate-Treasure-Hunter.

**Resolution:** The migrator writes an SDK-style project for .NET 8 that does not import the XNA targets.

Migrator task: project-migrator:4.1

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

### XNA047 NoStdLib breaks SDK-style builds

NoStdLib=true tells the compiler not to reference the standard library. The old XNA project templates set it. An SDK-style project supplies the references itself, so the compiler would miss System.Object and the build would fail.

**Resolution:** The migrator writes a new SDK-style project from a fixed property set that does not include NoStdLib.

Migrator task: project-migrator:4.1

### XNA049 Content project referenced with ProjectReference

The game project references a .contentproj with a ProjectReference (XnaReferenceType Content), the form used when the content project is a sibling project. MonoGame cannot load that reference and builds content from Content.mgcb instead. The analyzer reads this content project like a nested one, so the content-item rules (XNA017, XNA019, XNA020, XNA021) see the assets behind this form.

**Resolution:** The migrator removes the reference, generates Content.mgcb from the content project like it does for a nested content project (XNA005), copies the source assets into the game project, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1

### XNA053 Device conditional compilation symbols

An #if or #elif directive names a platform symbol (XBOX, XBOX360, ZUNE, WINDOWS_PHONE, or WINDOWS). The migrated project defines none of them, so the wrong branch would be compiled. Inactive branches count too.

**Resolution:** The migrator keeps the branch that is true for the chosen platform and removes the other branches. A condition that also depends on another symbol, such as DEBUG, is left as it is with a marker.

Migrator task: device-to-desktop:6.1

### XNA110 Visual Basic project

The project is written in Visual Basic. The migrator supports it with these limits. Matching is by syntax only, so a user type with the same name as an XNA type can match. A member use without parentheses in expression position (Dim t = fx.Begin) is a plain access, so a rule that looks for calls can miss it. The migrator rewrites the entry point and SpriteBatch.Begin and marks the other known breaks with a comment, but it does not rewrite them. The My namespace is never migrated.

**Resolution:** Fix the findings that carry a marker comment by hand. The migrated project builds with MyType set to Empty, so code that uses My.Application, My.Computer, My.Settings or My.Resources has to be changed.

### XNA114 My Project AssemblyInfo.vb

The project has a My Project/AssemblyInfo.vb file with assembly attributes. An SDK-style project generates the same attributes, so the build fails with duplicate attribute errors.

**Resolution:** The migrator keeps the file and turns off the generated assembly information (GenerateAssemblyInfo false), as it does for AssemblyInfo.cs.

Migrator task: project-migrator:4.1
