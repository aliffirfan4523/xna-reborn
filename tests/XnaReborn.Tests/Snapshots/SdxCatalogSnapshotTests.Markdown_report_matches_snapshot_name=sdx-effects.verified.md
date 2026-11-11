# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 72 / 100 |
| Projected score after automatic fixes | 83 / 100 |
| Projects analyzed | 1 |
| Findings | 11 |

Severity: Blocker 0, Error 6, Warning 4, Info 1

Classification: AutoFixable 7, Manual 4, Unsupported 0

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| SdxEffects.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `SdxEffects.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `SdxEffects.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:2:1`
  - Code: `using SharpDX.Toolkit;`
- **[Error] SDX021** Toolkit graphics namespace
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:3:1`
  - Code: `using SharpDX.Toolkit.Graphics;`
- **[Error] SDX023** SharpDX math namespace
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:1:1`
  - Code: `using SharpDX;`
- **[Warning] SDX010** Toolkit font (ToolkitFont)
  - Project: `SdxEffects.csproj`
  - Code: `Content\Arial16.xml`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `SdxEffects.csproj`
  - Code: `TargetFrameworkVersion=v4.5`

### Manual

- **[Error] SDX013** Toolkit effect (ToolkitFxc)
  - Project: `SdxEffects.csproj`
  - Code: `Content\Tint.fx`
- **[Warning] SDX046** Texture2D.New
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:27:21`
  - Code: `pixel = Texture2D.New(GraphicsDevice, 1, 1, PixelFormat.R8G8B8A8.UNorm);`
- **[Warning] SDX049** Effect parameter SetResource
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:34:13`
  - Code: `tint.Parameters["Texture"].SetResource(pixel);`
- **[Warning] SDX051** SpriteBatch.Begin(sortMode, effect)
  - Project: `SdxEffects.csproj`
  - Location: `Game1.cs:35:13`
  - Code: `spriteBatch.Begin(SpriteSortMode.Deferred, tint);`

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

### SDX013 Toolkit effect (ToolkitFxc)

The project builds effects with the ToolkitFxc build action (the tkfxc compiler). Toolkit effects use a syntax that the MonoGame effect compiler rejects. Checked with the compiler of dotnet-mgcb 3.8.6.1: a pass that names its shader without the compile keyword (PixelShader = Name;) fails with "Unexpected token 'P' found. Expected Compile"; a Profile = 9.1; pass state fails with "Unexpected token 'P' found. Expected CloseBracket"; a Direct3D 10 sampler block (Filter = MIN_MAG_MIP_POINT;) fails with "Unexpected token 'M' found. Expected None, Linear, Point, or Anisotropic." Texture2D<float4> declarations parse. The Toolkit can also generate wrapper classes that are not in source control.

**Resolution:** Rewrite each .fx file in the MonoGame effect syntax by hand and add it to Content.mgcb. The migrator copies the original and flags it. Code that used a generated wrapper class needs the wrapper removed.

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

### SDX046 Texture2D.New

Texture2D.New is a static factory that takes a DXGI PixelFormat. MonoGame uses new Texture2D(device, width, height) with a SurfaceFormat. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Use the Texture2D constructor and set the pixels with SetData.

### SDX049 Effect parameter SetResource

SetResource sets a texture or sampler on an effect parameter. MonoGame sets textures with SetValue(Texture) and sets samplers on the device. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Use EffectParameter.SetValue(texture) and GraphicsDevice.SamplerStates.

### SDX051 SpriteBatch.Begin(sortMode, effect)

The Toolkit has a Begin(SpriteSortMode, Effect) overload that XNA and MonoGame lack. The rule looks for any two-argument call on a variable named spriteBatch, so it also finds Begin(SpriteSortMode, BlendState), which MonoGame accepts. It misses a SpriteBatch field with another name. Matching is by syntax only.

**Resolution:** Pass the effect in the long overload: Begin(sortMode, blendState, samplerState, depthStencilState, rasterizerState, effect).
