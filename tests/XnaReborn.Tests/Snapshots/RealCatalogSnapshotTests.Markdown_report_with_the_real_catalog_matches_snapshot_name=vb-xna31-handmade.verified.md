# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 79 / 100 |
| Projected score after automatic fixes | 91 / 100 |
| Projects analyzed | 1 |
| Findings | 12 |

Severity: Blocker 0, Error 8, Warning 3, Info 1

Classification: AutoFixable 10, Manual 2, Unsupported 0

## Projects

| Project | Language | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|---|
| VbXna31Handmade.vbproj | Visual Basic | 3.1 | Unknown | - | v3.5 |

## Findings

### AutoFixable

- **[Error] XNA003** XNA assembly references
  - Project: `VbXna31Handmade.vbproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:10:21`
  - Code: `Private mode As SpriteBlendMode = SpriteBlendMode.AlphaBlend`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:10:39`
  - Code: `Private mode As SpriteBlendMode = SpriteBlendMode.AlphaBlend`
- **[Error] XNA022** SpriteBlendMode (replaced by BlendState)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:29:27`
  - Code: `spriteBatch.Begin(SpriteBlendMode.AlphaBlend)`
- **[Error] XNA024** GraphicsDevice.RenderState (replaced by state objects)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:24:14`
  - Code: `With GraphicsDevice.RenderState`
- **[Error] XNA039** EffectPass.Begin and End (replaced by Apply)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:34:13`
  - Code: `pass.Begin()`
- **[Error] XNA040** Effect.Begin and End (replaced by EffectPass.Apply)
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:32:9`
  - Code: `effect.Begin`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `VbXna31Handmade.vbproj`
  - Code: `TargetFrameworkVersion=v3.5`
- **[Warning] XNA111** Visual Basic application model (MyType)
  - Project: `VbXna31Handmade.vbproj`
  - Code: `MyType=WindowsForms`
- **[Warning] XNA113** My Project designer files
  - Project: `VbXna31Handmade.vbproj`
  - Code: `My Project\Application.Designer.vb`

### Manual

- **[Error] XNA112** My namespace used in code
  - Project: `VbXna31Handmade.vbproj`
  - Location: `Game1.vb:39:31`
  - Code: `Dim title As String = My.Application.Info.Title`
- **[Info] XNA110** Visual Basic project
  - Project: `VbXna31Handmade.vbproj`
  - Code: `Game1.vb, Module1.vb, My Project\Application.Designer.vb`

## Rules

### XNA003 XNA assembly references

The project references XNA assemblies (Microsoft.Xna.Framework and related) by strong name with Private=False. They do not resolve on a machine without XNA, so the project cannot build. MonoGame ships the same API under the same namespaces in its own package. This rule also fires for assemblies MonoGame does not provide (GamerServices, Net, Avatar, Storage, Xact, Video); those have their own rules for code that uses them.

**Resolution:** The migrator replaces the XNA references with the MonoGame DesktopGL or WindowsDX package.

Migrator task: project-migrator:4.1

### XNA004 Old .NET Framework target

The project targets the .NET Framework (3.5 for XNA 3.x, 4.0 with the Client profile for XNA 4.0). That framework version is not available to an SDK-style build, and current MonoGame releases need modern .NET.

**Resolution:** The migrator sets the target framework of the generated project to net8.0.

Migrator task: project-migrator:4.1

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

### XNA110 Visual Basic project

The project is written in Visual Basic. The migrator supports it with these limits. Matching is by syntax only, so a user type with the same name as an XNA type can match. A member use without parentheses in expression position (Dim t = fx.Begin) is a plain access, so a rule that looks for calls can miss it. The migrator rewrites the entry point and SpriteBatch.Begin and marks the other known breaks with a comment, but it does not rewrite them. The My namespace is never migrated.

**Resolution:** Fix the findings that carry a marker comment by hand. The migrated project builds with MyType set to Empty, so code that uses My.Application, My.Computer, My.Settings or My.Resources has to be changed.

### XNA111 Visual Basic application model (MyType)

The project uses the Visual Basic application model (MyType). In an SDK-style MonoGame project the application base classes it needs do not build: MyType Console fails with BC30002, and a game does not need the model.

**Resolution:** The migrator writes the SDK-style Visual Basic project with MyType set to Empty. Code that uses the My namespace is reported by XNA112.

Migrator task: older-and-vb:19

### XNA112 My namespace used in code

The code uses the Visual Basic My namespace (My.Application, My.Computer, My.Settings, My.Resources). The migrated project sets MyType to Empty, so these types do not exist and the build fails. They cannot be replaced automatically.

**Resolution:** Replace each My call with the .NET API (System.IO, System.Environment, an app.config reader, embedded resources).

### XNA113 My Project designer files

The project compiles designer files from the My Project folder. They are generated for the Visual Basic application model and refer to types that SDK-style projects do not have.

**Resolution:** The migrator leaves these files out of the migrated project and lists each in the manifest as omitted.

Migrator task: older-and-vb:19
