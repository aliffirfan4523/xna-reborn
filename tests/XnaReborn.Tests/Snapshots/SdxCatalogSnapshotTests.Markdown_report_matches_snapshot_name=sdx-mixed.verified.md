# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 82 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 2 |
| Findings | 13 |

Severity: Blocker 0, Error 8, Warning 2, Info 3

Classification: AutoFixable 13, Manual 0, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| Toolkit/ToolkitGame.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |
| Xna/XnaGame.csproj | Xna | 4.0 | - | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `Toolkit/ToolkitGame.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `Toolkit/ToolkitGame.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `Toolkit/ToolkitGame.csproj`
  - Location: `Toolkit/Game1.cs:2:1`
  - Code: `using SharpDX.Toolkit;`
- **[Error] SDX023** SharpDX math namespace
  - Project: `Toolkit/ToolkitGame.csproj`
  - Location: `Toolkit/Game1.cs:1:1`
  - Code: `using SharpDX;`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Xna/XnaGame.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Xna/XnaGame.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +1 more`
- **[Error] XNA005** Nested XNA content project
  - Project: `Xna/XnaGame.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Xna/XnaGame.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `Toolkit/ToolkitGame.csproj`
  - Location: `Toolkit/Game1.cs:21:20`
  - Code: `view = Matrix.LookAtRH(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Xna/XnaGame.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `Toolkit/ToolkitGame.csproj`
  - Code: `TargetFrameworkVersion=v4.5`
- **[Info] SDX011** Toolkit texture (ToolkitTexture)
  - Project: `Toolkit/ToolkitGame.csproj`
  - Code: `Content\Balls.dds`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Xna/XnaGame.csproj`
  - Code: `XnaFrameworkVersion=v4.0`

## Rules

### SDX001 SharpDX assembly reference

The project references SharpDX assemblies (SharpDX, SharpDX.Toolkit.*, SharpDX.Direct3D11 and others). They come from the SharpDX 2.x NuGet packages, which target .NET Framework 4.x and need the SharpDX build targets. They are not part of MonoGame.

**Resolution:** The migrator writes an SDK-style project that drops every SharpDX reference and references MonoGame instead. Code that used SharpDX types directly is reported by the SDX005 to SDX008 and SDX034 to SDX035 rules.

Migrator task: sharpdx-toolkit:8.1

### SDX002 SharpDX build targets import

The project imports SharpDX.targets or SharpDX.Toolkit.targets from the NuGet packages folder. Those files set the reference paths and run the Toolkit content compilers. They exist only after a NuGet restore of the old packages, and a build without them stops with MSB4019.

**Resolution:** The migrator drops the imports, the NuGet restore guard target and the import stamp from the SDK-style project it writes.

Migrator task: sharpdx-toolkit:8.1

### SDX003 .NET Framework 4.x target

The project targets .NET Framework 4.x (for example v4.5). MonoGame 3.8.6.1 desktop projects target net8.0.

**Resolution:** The migrator writes an SDK-style project for net8.0 (or the Windows variant for WindowsDX).

Migrator task: sharpdx-toolkit:8.1

### SDX011 Toolkit texture (ToolkitTexture)

The project builds textures with the ToolkitTexture build action. The Toolkit mostly copies the image into a .tkb file. MonoGame loads textures built by the content pipeline.

**Resolution:** The migrator adds each texture whose format the MonoGame builder accepts to Content.mgcb. Other formats are listed as manual work in the content report.

Migrator task: sharpdx-toolkit:9.3

### SDX020 Toolkit game namespace

The code imports SharpDX.Toolkit, which holds Game, GameTime and GraphicsDeviceManager. The same names live in Microsoft.Xna.Framework in MonoGame. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2

### SDX023 SharpDX math namespace

The code imports SharpDX for its math types (Vector2, Vector3, Matrix, Color, Rectangle). MonoGame keeps these names in Microsoft.Xna.Framework. Types that exist only in SharpDX are reported by SDX034 and SDX035. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2

### SDX030 Right-handed Matrix factories

The code builds matrices with the SharpDX factory methods. The right-handed ones map to Matrix.Create* in MonoGame, which is also right-handed. Only calls on the type name Matrix match, not calls on a variable. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator renames the calls (LookAtRH to CreateLookAt, PerspectiveFovRH to CreatePerspectiveFieldOfView, Scaling to CreateScale and so on).

Migrator task: sharpdx-toolkit:6.1

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

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1
