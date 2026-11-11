# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 48 / 100 |
| Projected score after automatic fixes | 59 / 100 |
| Projects analyzed | 1 |
| Findings | 15 |

Severity: Blocker 0, Error 12, Warning 1, Info 2

Classification: AutoFixable 8, Manual 7, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Xna31Legacy.csproj | 3.1 | Windows | - | v3.5 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Xna31Legacy.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Xna31Legacy.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:44:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:49:17`
  - Code: `SpriteBlendMode.Additive,`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:42:13`
  - Code: `GraphicsDevice.RenderState.AlphaBlendEnable = true;`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `Xna31Legacy.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `Xna31Legacy.csproj`
  - Code: `TargetFrameworkVersion=v3.5`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Xna31Legacy.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

### Manual

- **[Error] XNA023** SaveStateMode (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:51:17`
  - Code: `SaveStateMode.SaveState);`
- **[Error] XNA027** ResolveTexture2D (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:14:17`
  - Code: `private ResolveTexture2D snapshot;`
- **[Error] XNA027** ResolveTexture2D (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:27:28`
  - Code: `snapshot = new ResolveTexture2D(`
- **[Error] XNA028** GraphicsDevice.ResolveBackBuffer (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:55:13`
  - Code: `GraphicsDevice.ResolveBackBuffer(snapshot);`
- **[Error] XNA034** GraphicsDevice.VertexDeclaration (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:41:13`
  - Code: `GraphicsDevice.VertexDeclaration = declaration;`
- **[Error] XNA036** VertexElements on vertex types (removed)
  - Project: `Xna31Legacy.csproj`
  - Location: `Game1.cs:35:17`
  - Code: `VertexPositionColor.VertexElements);`
- **[Info] XNA006** XNA 3.x project
  - Project: `Xna31Legacy.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

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

### XNA006 XNA 3.x project

The project targets XNA 3.0 or 3.1. XNA 4.0 broke much of the graphics, storage, and effect API, and MonoGame follows the 4.0 API. Premultiplied alpha is also the default from 4.0 on, so sprite blending can look different after the move. Each concrete break has its own rule.

**Resolution:** Fix the findings from the XNA 3.x rules, then compare the rendered result of the migrated game with the original, especially alpha-blended sprites.

### XNA022 SpriteBlendMode (replaced by BlendState)

SpriteBlendMode and the SpriteBatch.Begin overloads that take it were removed in XNA 4.0 and are not in MonoGame. Uses outside a Begin call cannot be rewritten safely.

**Resolution:** The migrator rewrites Begin(SpriteBlendMode.X) to Begin(SpriteSortMode.Deferred, BlendState.X) and marks other uses for manual work.

Migrator task: project-migrator:10.1

### XNA023 SaveStateMode (removed)

SaveStateMode, used in SpriteBatch.Begin and Effect.Begin, was removed in XNA 4.0 together with StateBlock. Graphics state is no longer saved and restored for you.

**Resolution:** Delete the argument and set the blend, depth, rasterizer, and sampler state explicitly before each draw.

### XNA024 GraphicsDevice.RenderState (replaced by state objects)

XNA 4.0 replaced RenderState with BlendState, DepthStencilState, and RasterizerState. A user type with a member named RenderState would also match.

**Resolution:** The migrator replaces known RenderState assignments with state objects and marks combinations it cannot map with confidence.

Migrator task: project-migrator:10.2

### XNA027 ResolveTexture2D (removed)

ResolveTexture2D copied the back buffer into a texture. XNA 4.0 removed it, and MonoGame has no back buffer resolve.

**Resolution:** Draw the scene into a RenderTarget2D and use that render target as a texture.

### XNA028 GraphicsDevice.ResolveBackBuffer (removed)

ResolveBackBuffer was removed in XNA 4.0. Any method with the same name in a game class would also match.

**Resolution:** Call SetRenderTarget with a RenderTarget2D before drawing the scene, then SetRenderTarget(null) and draw the render target.

### XNA034 GraphicsDevice.VertexDeclaration (removed)

XNA 4.0 removed the device property because every vertex buffer now carries its declaration. Known limit: the receiver must be named GraphicsDevice, so code that uses another variable name is missed. The name is needed to avoid the valid static VertexDeclaration property of the 4.0 vertex types.

**Resolution:** Delete the assignment and create the VertexBuffer with a vertex type or a VertexDeclaration.

### XNA036 VertexElements on vertex types (removed)

XNA 3.x vertex structs exposed a VertexElements array. In 4.0 they expose a VertexDeclaration, and the VertexDeclaration constructor no longer takes a GraphicsDevice.

**Resolution:** Use VertexPositionColor.VertexDeclaration, or new VertexDeclaration(new VertexElement(...)) for custom vertex types, and implement IVertexType on custom structs.

### XNA046 Properties/AssemblyInfo.cs duplicates SDK attributes

Properties/AssemblyInfo.cs declares assembly attributes (title, product, company, version). An SDK-style project generates the same attributes from project properties, and the build then fails with CS0579 duplicate attribute. Pirate-Treasure-Hunter got four errors from this one file.

**Resolution:** The migrator writes <GenerateAssemblyInfo>false</GenerateAssemblyInfo> in the generated project so the kept AssemblyInfo.cs does not clash.

Migrator task: project-migrator:4.1
