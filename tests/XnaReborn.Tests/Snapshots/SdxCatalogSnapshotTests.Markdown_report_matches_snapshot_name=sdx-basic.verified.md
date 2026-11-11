# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 84 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 1 |
| Findings | 24 |

Severity: Blocker 0, Error 5, Warning 16, Info 3

Classification: AutoFixable 24, Manual 0, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| SdxBasic.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `SdxBasic.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `SdxBasic.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:3:1`
  - Code: `using SharpDX.Toolkit;`
- **[Error] SDX021** Toolkit graphics namespace
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:4:1`
  - Code: `using SharpDX.Toolkit.Graphics;`
- **[Error] SDX023** SharpDX math namespace
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:2:1`
  - Code: `using SharpDX;`
- **[Warning] SDX010** Toolkit font (ToolkitFont)
  - Project: `SdxBasic.csproj`
  - Code: `Content\Arial16.xml`
- **[Warning] SDX024** Toolkit input namespace
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:5:1`
  - Code: `using SharpDX.Toolkit.Input;`
- **[Warning] SDX025** KeyboardManager
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:18:17`
  - Code: `private KeyboardManager keyboard;`
- **[Warning] SDX025** KeyboardManager
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:29:28`
  - Code: `keyboard = new KeyboardManager(this);`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:50:20`
  - Code: `view = Matrix.LookAtRH(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:51:26`
  - Code: `projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, 1.6f, 0.1f, 100f);`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:52:21`
  - Code: `world = Matrix.RotationY(angle) * Matrix.Scaling(2f) * Matrix.Translation(1f, 0f, 0f);`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:52:47`
  - Code: `world = Matrix.RotationY(angle) * Matrix.Scaling(2f) * Matrix.Translation(1f, 0f, 0f);`
- **[Warning] SDX030** Right-handed Matrix factories
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:52:68`
  - Code: `world = Matrix.RotationY(angle) * Matrix.Scaling(2f) * Matrix.Translation(1f, 0f, 0f);`
- **[Warning] SDX031** MathUtil members
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:49:21`
  - Code: `angle = MathUtil.Clamp(angle + 0.01f, 0f, MathUtil.TwoPi);`
- **[Warning] SDX031** MathUtil members
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:49:55`
  - Code: `angle = MathUtil.Clamp(angle + 0.01f, 0f, MathUtil.TwoPi);`
- **[Warning] SDX031** MathUtil members
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:51:50`
  - Code: `projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, 1.6f, 0.1f, 100f);`
- **[Warning] SDX040** Graphics state collections
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:59:42`
  - Code: `GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);`
- **[Warning] SDX040** Graphics state collections
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:61:56`
  - Code: `spriteBatch.Begin(SpriteSortMode.Deferred, GraphicsDevice.BlendStates.Opaque, GraphicsDevice.SamplerStates.PointClamp, null, null);`
- **[Warning] SDX040** Graphics state collections
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:61:91`
  - Code: `spriteBatch.Begin(SpriteSortMode.Deferred, GraphicsDevice.BlendStates.Opaque, GraphicsDevice.SamplerStates.PointClamp, null, null);`
- **[Warning] SDX041** Set*State calls
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:59:13`
  - Code: `GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `SdxBasic.csproj`
  - Code: `TargetFrameworkVersion=v4.5`
- **[Info] SDX011** Toolkit texture (ToolkitTexture)
  - Project: `SdxBasic.csproj`
  - Code: `Content\Balls.dds`
- **[Info] SDX060** ToDisposeContent
  - Project: `SdxBasic.csproj`
  - Location: `Game1.cs:35:27`
  - Code: `spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));`

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

### SDX010 Toolkit font (ToolkitFont)

The project builds fonts with the ToolkitFont build action. The source is a TkFont XML file that the Toolkit compiles to .tkb. MonoGame cannot read .tkb files and uses .spritefont files instead.

**Resolution:** The migrator converts each TkFont file to a .spritefont file and adds it to Content.mgcb. Line spacing and premultiply settings have no MonoGame field and are listed in the content report.

Migrator task: sharpdx-toolkit:9.2

### SDX011 Toolkit texture (ToolkitTexture)

The project builds textures with the ToolkitTexture build action. The Toolkit mostly copies the image into a .tkb file. MonoGame loads textures built by the content pipeline.

**Resolution:** The migrator adds each texture whose format the MonoGame builder accepts to Content.mgcb. Other formats are listed as manual work in the content report.

Migrator task: sharpdx-toolkit:9.3

### SDX020 Toolkit game namespace

The code imports SharpDX.Toolkit, which holds Game, GameTime and GraphicsDeviceManager. The same names live in Microsoft.Xna.Framework in MonoGame. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2

### SDX021 Toolkit graphics namespace

The code imports SharpDX.Toolkit.Graphics (SpriteBatch, Texture2D, SpriteFont, BasicEffect). MonoGame keeps these names in Microsoft.Xna.Framework.Graphics. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.Graphics.

Migrator task: sharpdx-toolkit:5.2

### SDX023 SharpDX math namespace

The code imports SharpDX for its math types (Vector2, Vector3, Matrix, Color, Rectangle). MonoGame keeps these names in Microsoft.Xna.Framework. Types that exist only in SharpDX are reported by SDX034 and SDX035. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.

Migrator task: sharpdx-toolkit:5.2

### SDX024 Toolkit input namespace

The code imports SharpDX.Toolkit.Input (Keys, KeyboardState, MouseManager). MonoGame has Keys and KeyboardState in Microsoft.Xna.Framework.Input. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the using directive to Microsoft.Xna.Framework.Input.

Migrator task: sharpdx-toolkit:5.2

### SDX025 KeyboardManager

The game creates a KeyboardManager and reads keys from it. MonoGame has the static Keyboard.GetState() and needs no manager. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator removes the manager field and its construction and replaces GetState() on it with Keyboard.GetState(). When the manager is used in any other way the code is left alone and marked.

Migrator task: sharpdx-toolkit:7.1

### SDX030 Right-handed Matrix factories

The code builds matrices with the SharpDX factory methods. The right-handed ones map to Matrix.Create* in MonoGame, which is also right-handed. Only calls on the type name Matrix match, not calls on a variable. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator renames the calls (LookAtRH to CreateLookAt, PerspectiveFovRH to CreatePerspectiveFieldOfView, Scaling to CreateScale and so on).

Migrator task: sharpdx-toolkit:6.1

### SDX031 MathUtil members

The code uses SharpDX MathUtil. MonoGame has the same helpers on MathHelper, some under other names (PiOver2, PiOver4, ToRadians, ToDegrees). Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites MathUtil.X to MathHelper.X and renames the members that differ.

Migrator task: sharpdx-toolkit:6.1

### SDX040 Graphics state collections

The code reads states from GraphicsDevice.BlendStates, SamplerStates, DepthStencilStates or RasterizerStates. MonoGame has static BlendState, SamplerState and DepthStencilState members. Names that do not map one to one (BlendStates.Default, RasterizerStates.*, SamplerStates.Default) are marked, not renamed. Face culling may look inverted after a change of handedness. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites GraphicsDevice.BlendStates.X to BlendState.X and does the same for samplers and depth states. Names without an equivalent get a marker.

Migrator task: sharpdx-toolkit:6.2

### SDX041 Set*State calls

The code sets a state with GraphicsDevice.SetBlendState(state) and its relatives. MonoGame uses properties on the device. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator rewrites the one-argument call to an assignment (GraphicsDevice.BlendState = state).

Migrator task: sharpdx-toolkit:6.2

### SDX060 ToDisposeContent

ToDisposeContent(x) registers x for disposal when the game unloads and returns x. MonoGame Game has no such method. Removing the call drops that automatic disposal. That changes nothing visible for objects that live as long as the game. The rule uses the type match because the call is normally a bare call of an inherited method. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** The migrator replaces ToDisposeContent(x) with x. Dispose objects that must not outlive their use in UnloadContent.

Migrator task: sharpdx-toolkit:6.2
