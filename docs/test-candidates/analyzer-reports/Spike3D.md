# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 19 / 100 |
| Projected score after automatic fixes | 33 / 100 |
| Projects analyzed | 1 |
| Findings | 63 |

Severity: Blocker 16, Error 42, Warning 2, Info 3

Classification: AutoFixable 11, Manual 20, Unsupported 32

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| SpaceRun/SpaceRun/SpaceRun.csproj | 3.1 | Windows | - | v3.5 |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\Microsoft.Xna.GameStudio.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA005** Nested XNA content project
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:132:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:127:13`
  - Code: `GraphicsDevice.RenderState.DepthBufferEnable = true;`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:128:13`
  - Code: `GraphicsDevice.RenderState.DepthBufferWriteEnable = true;`
- **[Error] XNA046** Properties/AssemblyInfo.cs duplicates SDK attributes
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `Properties/AssemblyInfo.cs`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `TargetFrameworkVersion=v3.5`
- **[Warning] XNA047** NoStdLib breaks SDK-style builds
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `NoStdLib=true`
- **[Info] XNA002** XNA-specific project properties
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `XnaFrameworkVersion=v3.1`
- **[Info] XNA045** ClickOnce bootstrapper packages
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `Microsoft.Net.Framework.2.0, Microsoft.Net.Framework.3.0, Microsoft.Net.Framework.3.5 +2 more`

### Manual

- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/AIShip.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Asteroid.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Entity.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/EntityManager.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/HUD.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level1.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ModelManager.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Network.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ParticleSystem.cs:14:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Planet.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/PlayerShip.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Quad.cs:10:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Ship.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Utilities.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA012** Storage (StorageDevice, StorageContainer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Waypoint.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Storage;`
- **[Error] XNA034** GraphicsDevice.VertexDeclaration (removed)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ParticleSystem.cs:139:13`
  - Code: `graphics.GraphicsDevice.VertexDeclaration = quadVertexDeclaration;`
- **[Error] XNA036** VertexElements on vertex types (removed)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ParticleSystem.cs:132:84`
  - Code: `quadVertexDeclaration = new VertexDeclaration(graphics.GraphicsDevice, VertexPositionNormalTexture.VertexElements);`
- **[Info] XNA006** XNA 3.x project
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Code: `XnaFrameworkVersion=v3.1`

### Unsupported

- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/AIShip.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Asteroid.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Entity.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/EntityManager.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/HUD.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level1.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ModelManager.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Network.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ParticleSystem.cs:9:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Planet.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/PlayerShip.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Ship.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Utilities.cs:8:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Blocker] XNA009** GamerServices (Xbox Live, Guide, gamer profiles)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Waypoint.cs:7:1`
  - Code: `using Microsoft.Xna.Framework.GamerServices;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/AIShip.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Asteroid.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Entity.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/EntityManager.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Game1.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/HUD.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Level1.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ModelManager.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Network.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/ParticleSystem.cs:13:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Planet.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/PlayerShip.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Ship.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Utilities.cs:12:1`
  - Code: `using Microsoft.Xna.Framework.Net;`
- **[Error] XNA010** Net (NetworkSession, system link, Live multiplayer)
  - Project: `SpaceRun/SpaceRun/SpaceRun.csproj`
  - Location: `SpaceRun/SpaceRun/Waypoint.cs:11:1`
  - Code: `using Microsoft.Xna.Framework.Net;`

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

### XNA006 XNA 3.x project

The project targets XNA 3.0 or 3.1. XNA 4.0 broke much of the graphics, storage, and effect API, and MonoGame follows the 4.0 API. Premultiplied alpha is also the default from 4.0 on, so sprite blending can look different after the move. Each concrete break has its own rule.

**Resolution:** Fix the findings from the XNA 3.x rules, then compare the rendered result of the migrated game with the original, especially alpha-blended sprites.

### XNA009 GamerServices (Xbox Live, Guide, gamer profiles)

Code uses GamerServices (Guide, SignedInGamer, achievements, leaderboards, trial mode). MonoGame removed this namespace and no replacement exists for the Xbox Live services behind it.

**Resolution:** Remove the calls or replace them with your own sign-in, saving, and leaderboard code. Remove GamerServicesComponent from the Game constructor.

### XNA010 Net (NetworkSession, system link, Live multiplayer)

Code uses the XNA Net namespace (NetworkSession, PacketReader, PacketWriter). MonoGame removed it, and it depended on Xbox Live matchmaking.

**Resolution:** Rewrite networking with System.Net.Sockets or a library such as Lidgren.Network.

### XNA012 Storage (StorageDevice, StorageContainer)

Code uses StorageDevice and StorageContainer for save games. MonoGame removed the Storage namespace because it did not port across platforms.

**Resolution:** Write saves with System.IO under Environment.SpecialFolder.ApplicationData and read packaged files with TitleContainer.OpenStream. The optional shim project (project-migrator:12.1) can stand in for the calls while you rewrite.

### XNA022 SpriteBlendMode (replaced by BlendState)

SpriteBlendMode and the SpriteBatch.Begin overloads that take it were removed in XNA 4.0 and are not in MonoGame. Uses outside a Begin call cannot be rewritten safely.

**Resolution:** The migrator rewrites Begin(SpriteBlendMode.X) to Begin(SpriteSortMode.Deferred, BlendState.X) and marks other uses for manual work.

Migrator task: project-migrator:10.1

### XNA024 GraphicsDevice.RenderState (replaced by state objects)

XNA 4.0 replaced RenderState with BlendState, DepthStencilState, and RasterizerState. A user type with a member named RenderState would also match.

**Resolution:** The migrator replaces known RenderState assignments with state objects and marks combinations it cannot map with confidence.

Migrator task: project-migrator:10.2

### XNA034 GraphicsDevice.VertexDeclaration (removed)

XNA 4.0 removed the device property because every vertex buffer now carries its declaration. Known limit: the receiver must be named GraphicsDevice, so code that uses another variable name is missed. The name is needed to avoid the valid static VertexDeclaration property of the 4.0 vertex types.

**Resolution:** Delete the assignment and create the VertexBuffer with a vertex type or a VertexDeclaration.

### XNA036 VertexElements on vertex types (removed)

XNA 3.x vertex structs exposed a VertexElements array. In 4.0 they expose a VertexDeclaration, and the VertexDeclaration constructor no longer takes a GraphicsDevice.

**Resolution:** Use VertexPositionColor.VertexDeclaration, or new VertexDeclaration(new VertexElement(...)) for custom vertex types, and implement IVertexType on custom structs.

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
