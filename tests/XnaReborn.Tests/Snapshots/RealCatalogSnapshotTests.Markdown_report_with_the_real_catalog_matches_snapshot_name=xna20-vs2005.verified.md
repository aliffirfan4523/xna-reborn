# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 84 / 100 |
| Projected score after automatic fixes | 99 / 100 |
| Projects analyzed | 2 |
| Findings | 20 |

Severity: Blocker 0, Error 14, Warning 1, Info 5

Classification: AutoFixable 18, Manual 2, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| Game/Game.csproj | 2.0 | Windows | - | - |
| Game360/Game360.csproj | 2.0 | Xbox360 | - | - |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Game/Game.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.Common.targets, $(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.NestedContent.targets`
- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `Game360/Game360.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.Common.targets, $(MSBuildExtensionsPath)\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.NestedContent.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `Game/Game.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA003** XNA assembly references
  - Project: `Game360/Game360.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA005** Nested XNA content project
  - Project: `Game/Game.csproj`
  - Code: `Content\Content.contentproj`
- **[Error] XNA005** Nested XNA content project
  - Project: `Game360/Game360.csproj`
  - Code: `..\Game\Content\Content.contentproj`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `Game/Game.csproj`
  - Location: `Game/Game1.cs:41:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `Game360/Game360.csproj`
  - Location: `Game/Game1.cs:41:31`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend);`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `Game/Game.csproj`
  - Location: `Game/Game1.cs:30:13`
  - Code: `GraphicsDevice.RenderState.AlphaBlendEnable = true;`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `Game360/Game360.csproj`
  - Location: `Game/Game1.cs:30:13`
  - Code: `GraphicsDevice.RenderState.AlphaBlendEnable = true;`
- **[Error] XNA039** EffectPass.Begin and End (replaced by Apply)
  - Project: `Game/Game.csproj`
  - Location: `Game/Game1.cs:35:17`
  - Code: `pass.Begin();`
- **[Error] XNA039** EffectPass.Begin and End (replaced by Apply)
  - Project: `Game360/Game360.csproj`
  - Location: `Game/Game1.cs:35:17`
  - Code: `pass.Begin();`
- **[Error] XNA040** Effect.Begin and End (replaced by EffectPass.Apply)
  - Project: `Game/Game.csproj`
  - Location: `Game/Game1.cs:32:13`
  - Code: `effect.Begin();`
- **[Error] XNA040** Effect.Begin and End (replaced by EffectPass.Apply)
  - Project: `Game360/Game360.csproj`
  - Location: `Game/Game1.cs:32:13`
  - Code: `effect.Begin();`
- **[Warning] XNA007** Project targets Xbox 360, Zune, or Windows Phone
  - Project: `Game360/Game360.csproj`
  - Code: `XnaPlatform=Xbox 360`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Game/Game.csproj`
  - Code: `XnaFrameworkVersion=v2.0`
- **[Info] XNA002** XNA-specific project properties
  - Project: `Game360/Game360.csproj`
  - Code: `XnaFrameworkVersion=v2.0`
- **[Info] XNA052** Platform copy of another project
  - Project: `Game360/Game360.csproj`
  - Code: `2 of 2 Compile items are links`

### Manual

- **[Info] XNA100** XNA 1.0 or 2.0 project
  - Project: `Game/Game.csproj`
  - Code: `XnaFrameworkVersion=v2.0`
- **[Info] XNA100** XNA 1.0 or 2.0 project
  - Project: `Game360/Game360.csproj`
  - Code: `XnaFrameworkVersion=v2.0`

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

### XNA005 Nested XNA content project

Assets live in a nested .contentproj that imports the XNA content pipeline targets, which only XNA Game Studio can build. MonoGame builds content from a Content.mgcb file. Projects that reference the content project with a ProjectReference instead are covered by XNA049.

**Resolution:** The migrator generates Content.mgcb from the content project, copies the source assets, and lists anything it cannot convert as manual work.

Migrator task: project-migrator:7.1

### XNA007 Project targets Xbox 360, Zune, or Windows Phone

XnaPlatform names a device platform (Xbox 360, Zune, or Windows Phone). The project cannot run on a desktop as it is, because it depends on device APIs and device-only #if branches. The device rules XNA053 to XNA063 list what the code uses.

**Resolution:** The migrator turns the project into a Windows DesktopGL game: it skips platform copies of the same game, folds the device #if branches, and adds stand-ins that map touch, tilt, and the Back button to the mouse and keyboard. What it cannot convert is listed as manual work.

Migrator task: device-to-desktop:5.1

### XNA022 SpriteBlendMode (replaced by BlendState)

SpriteBlendMode and the SpriteBatch.Begin overloads that take it were removed in XNA 4.0 and are not in MonoGame. Uses outside a Begin call cannot be rewritten safely.

**Resolution:** The migrator rewrites Begin(SpriteBlendMode.X) to Begin(SpriteSortMode.Deferred, BlendState.X) and marks other uses for manual work.

Migrator task: project-migrator:10.1

### XNA024 GraphicsDevice.RenderState (replaced by state objects)

XNA 4.0 replaced RenderState with BlendState, DepthStencilState, and RasterizerState. A user type with a member named RenderState would also match.

**Resolution:** The migrator replaces known RenderState assignments with state objects and marks combinations it cannot map with confidence.

Migrator task: project-migrator:10.2

### XNA039 EffectPass.Begin and End (replaced by Apply)

XNA 4.0 replaced EffectPass.Begin and End with EffectPass.Apply. Known limit: the rule only sees calls on a variable named pass, which is the usual loop variable, so passes held in variables with other names are missed.

**Resolution:** The migrator rewrites the Begin/End pattern to pass.Apply() and marks patterns it cannot rewrite with confidence.

Migrator task: project-migrator:10.3

### XNA040 Effect.Begin and End (replaced by EffectPass.Apply)

XNA 4.0 removed Effect.Begin and End. Known limit: the rule only sees calls on a variable named effect, so effects held in other variables (basicEffect, shader, and so on) are missed.

**Resolution:** The migrator replaces the Begin/End pattern with a loop over CurrentTechnique.Passes that calls Apply, and marks patterns it cannot rewrite with confidence.

Migrator task: project-migrator:10.3

### XNA052 Platform copy of another project

At least half of this project's Compile items are links to files of another project. Game Studio's Create Copy of project for a platform command makes projects like this: they share the code of the original and add nothing of their own.

**Resolution:** The migrator migrates one project of the group (the Windows one when there is one) and skips each copy. You can change the choice in the migration plan.

Migrator task: device-to-desktop:5.2

### XNA100 XNA 1.0 or 2.0 project

The project targets XNA Game Studio Express 1.0 or XNA Game Studio 2.0. Both are older than the 3.x line. Everything in XNA 4.0 and MonoGame that changed since 3.1 changed since these versions too. Defaults also differ: premultiplied alpha is the default from 4.0 on, and from 2.0 on Draw is called once per Update with a fixed time step, so a frame counter that counted Draw calls reads differently. Visual Basic projects are covered by a separate group of rules.

**Resolution:** Fix the findings from the XNA 1.0 and 2.0 rules and the XNA 3.x rules, then compare the rendered result of the migrated game with the original, especially alpha-blended sprites and anything that depends on frame timing.
