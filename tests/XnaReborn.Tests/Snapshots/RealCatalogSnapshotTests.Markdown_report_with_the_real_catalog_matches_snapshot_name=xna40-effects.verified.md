# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 59 / 100 |
| Projected score after automatic fixes | 72 / 100 |
| Projects analyzed | 1 |
| Findings | 13 |

Severity: Blocker 0, Error 9, Warning 2, Info 2

Classification: AutoFixable 8, Manual 5, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna40Effects.csproj | 4.0 | Windows | HiDef | v4.0 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Xna40Effects.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Xna40Effects.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game, Microsoft.Xna.Framework.Graphics +1 more`
- **[Error] XNA005** Nested XNA content project
  - Project: `Xna40Effects.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Xna40Effects.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Error] XNA064** Fixed shader model in an effect
  - Project: `Xna40Effects.csproj`
  - Code: `Content/AlphaTest.fx:16, Content/Annotated.fx:13, Content/Defaults.fx:14 +9 more`
- **[Error] XNA068** Old sampler state name or filter
  - Project: `Xna40Effects.csproj`
  - Code: `Content/Gaussian.fx:5, Content/Gaussian.fx:6, Content/MipBias.fx:8`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Xna40Effects.csproj`
  - Code: `TargetFrameworkVersion=v4.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Xna40Effects.csproj`
  - Code: `XnaFrameworkVersion=v4.0`

### Manual

- **[Error] XNA065** Effect pass state MonoGame does not support
  - Project: `Xna40Effects.csproj`
  - Code: `Content/AlphaTest.fx:13, Content/AlphaTest.fx:14, Content/AlphaTest.fx:15`
- **[Error] XNA066** Effect syntax MonoGame does not parse
  - Project: `Xna40Effects.csproj`
  - Code: `Content/Annotated.fx:1, Content/Annotated.fx:13, Content/Annotated.fx:21`
- **[Error] XNA067** Effect uses VPOS
  - Project: `Xna40Effects.csproj`
  - Code: `Content/Vpos.fx:1`
- **[Warning] XNA017** Effect file (.fx) needs shader review
  - Project: `Xna40Effects.csproj`
  - Code: `Sm2.fx, Sm3.fx, Template.fx +8 more`
- **[Info] XNA069** Effect parameter with a default value
  - Project: `Xna40Effects.csproj`
  - Code: `Content/Annotated.fx:2, Content/Defaults.fx:1, Content/Template.fx:5`

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

### XNA017 Effect file (.fx) needs shader review

XNA compiled .fx files with the legacy DirectX 9 compiler. MonoGame uses a different compiler and imposes restrictions, so old shaders can fail to build or render differently. The migrator fixes shader models and two sampler state names, marks the constructs it cannot fix, and copies included files; a person still has to check the look.

**Resolution:** Read the notes and marker comments the migrator left in the copied effect, build it with the MonoGame content builder, fix compile errors, and compare the output. The MonoGame custom effects guide lists the limits.

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1

### XNA064 Fixed shader model in an effect

A compile statement names a Shader Model 1 to 3 profile. The DirectX compiler in MonoGame only accepts Shader Model 4 level 9 or higher, so the effect fails to build for WindowsDX. A macro that sets the model in a template-style #define is not found by this rule.

**Resolution:** The migrator replaces the models with macros that pick the original on OpenGL and a Shader Model 4 level 9 profile on DirectX. Check the rendered result.

Migrator task: audio-and-shaders:12

### XNA065 Effect pass state MonoGame does not support

A pass sets a render state that the MonoGame effect compiler does not accept, such as alpha testing. The effect fails to build. This rule looks for the common old state names; the migrator checks every pass against the full list of accepted states.

**Resolution:** Set the state in C# (for example AlphaTestEffect for alpha testing) or discard in the pixel shader. The migrator adds a marker comment above each line.

### XNA066 Effect syntax MonoGame does not parse

The effect uses an annotation, arguments in a compile statement, or Direct3D 10 shader calls. The MonoGame effect parser does not accept them and the effect fails to build.

**Resolution:** Remove the annotation, move the arguments into the shader function, or use VertexShader = compile and PixelShader = compile. The migrator adds a marker comment above each line.

### XNA067 Effect uses VPOS

A pixel shader reads its screen position from VPOS. The MonoGame effect compiler does not support that input on either target.

**Resolution:** Use SV_Position as the pixel shader input on DirectX, and compute the screen position from the vertex shader output on OpenGL. The migrator adds a marker comment.

### XNA068 Old sampler state name or filter

A sampler uses MipMapLodBias, or the GaussianQuad or PyramidalQuad filter. MonoGame calls the first MipLodBias and has neither filter.

**Resolution:** The migrator renames MipMapLodBias to MipLodBias and replaces the two filters with Linear. Check the look.

Migrator task: audio-and-shaders:13

### XNA069 Effect parameter with a default value

A top-level effect parameter has a default value. OpenGL targets ignore it, and the compiler removes parameters that the shader never uses.

**Resolution:** Set the value from code. The migration report lists defaults and unused parameters per file.
