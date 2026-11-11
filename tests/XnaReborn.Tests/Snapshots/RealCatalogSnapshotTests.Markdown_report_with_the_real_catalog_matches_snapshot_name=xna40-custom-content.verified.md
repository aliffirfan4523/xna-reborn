# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 74 / 100 |
| Projected score after automatic fixes | 83 / 100 |
| Projects analyzed | 1 |
| Findings | 10 |

Severity: Blocker 0, Error 5, Warning 4, Info 1

Classification: AutoFixable 6, Manual 4, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| CustomContent.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `CustomContent.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `CustomContent.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics`
- **[Error] XNA005** Nested XNA content project
  - Project: `CustomContent.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `CustomContent.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `CustomContent.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `CustomContent.csproj`
  - Code: `XnaFrameworkVersion=v4.0`

### Manual

- **[Error] XNA013** XNA-only SpriteFont family
  - Project: `CustomContent.csproj`
  - Code: `Content/Fonts/Hud.spritefont: Pericles`
- **[Warning] XNA016** XACT project file (.xap)
  - Project: `CustomContent.csproj`
  - Code: `Content/Audio/Sounds.xap`
- **[Warning] XNA017** Effect file (.fx) needs shader review
  - Project: `CustomContent.csproj`
  - Code: `Shaders/Glow.fx`
- **[Warning] XNA050** Custom content importer or processor
  - Project: `CustomContent.csproj`
  - Code: `Textures/ship.png`

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

### XNA005 Nested XNA content project

Assets live in a nested .contentproj that imports the XNA content pipeline targets, which only XNA Game Studio can build. MonoGame builds content from a Content.mgcb file. Projects that reference the content project with a ProjectReference instead are covered by XNA049.

**Resolution:** The migrator generates Content.mgcb from the content project, copies the source assets, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1

### XNA013 XNA-only SpriteFont family

A .spritefont names a font from the XNA Redistributable Font Pack, which XNA Game Studio 4.0 installed and Windows does not ship (Andy, Jing Jing, Kootenay, Lindsey, Miramonte, Moire, Motorwerk, News Gothic, Pericles, Pescadero, Quartz MS, Segoe Keycaps, Segoe UI Mono, Wasco Sans). The MonoGame content builder looks the font up in the system font folders, so the content build fails with 'Could not find ... font file' for each such file (Pirate-Treasure-Hunter: Pescadero in three files, Segoe UI Mono in one). The list matches src/XnaReborn.Migrator/Content/data/xna-only-fonts.json.

**Resolution:** Change FontName to an installed or bundled font, or add a .ttf to the content folder and name it. The migrator can substitute a close Windows font in the migrated copy when font substitution is turned on. Glyph widths differ from the original, so check the layout.

### XNA016 XACT project file (.xap)

The project contains an XACT authoring file. XACT is a Windows-only tool that is no longer updated, and the MonoGame content builder does not compile .xap files.

**Resolution:** Compile the project in XACT and copy the .xgs, .xwb, and .xsb outputs, or export the sounds as wav or ogg files and use SoundEffect and Song.

### XNA017 Effect file (.fx) needs shader review

XNA compiled .fx files with the legacy DirectX 9 compiler. MonoGame uses a different compiler and imposes restrictions, so old shaders can fail to build or render differently. The migrator fixes shader models and two sampler state names, marks the constructs it cannot fix, and copies included files; a person still has to check the look.

**Resolution:** Read the notes and marker comments the migrator left in the copied effect, build it with the MonoGame content builder, fix compile errors, and compare the output. The MonoGame custom effects guide lists the limits.

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA050 Custom content importer or processor

A content item uses an importer or processor that is not part of stock XNA 4.0. It comes from a custom content pipeline assembly, which was built against the XNA pipeline and does not load in the MonoGame content builder as it is.

**Resolution:** Port the pipeline project to MonoGame and reference the rebuilt assembly from Content.mgcb, or replace the asset with one that a stock importer and processor can build.
