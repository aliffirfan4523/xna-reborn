# XNA Reborn compatibility report

Rule catalog: xna-to-monogame + sharpdx-toolkit-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 37 / 100 |
| Projected score after automatic fixes | 47 / 100 |
| Projects analyzed | 1 |
| Findings | 19 |

Severity: Blocker 0, Error 9, Warning 9, Info 1

Classification: AutoFixable 6, Manual 10, Unsupported 3

## Projects

| Project | Family | XNA version | MonoGame version | Platform | Profile | Target framework |
|---|---|---|---|---|---|---|
| SdxLowLevel.csproj | SharpDxToolkit | Unknown | - | Unknown | - | v4.5 |

## Findings

### AutoFixable

- **[Error] SDX001** SharpDX assembly reference
  - Project: `SdxLowLevel.csproj`
  - Code: `SharpDX, SharpDX.Toolkit, SharpDX.Toolkit.Game +2 more`
- **[Error] SDX002** SharpDX build targets import
  - Project: `SdxLowLevel.csproj`
  - Code: `..\packages\SharpDX.2.6.3\build\SharpDX.targets, ..\packages\SharpDX.Toolkit.2.6.3\build\SharpDX.Toolkit.targets`
- **[Error] SDX020** Toolkit game namespace
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:4:1`
  - Code: `using SharpDX.Toolkit;`
- **[Error] SDX021** Toolkit graphics namespace
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:5:1`
  - Code: `using SharpDX.Toolkit.Graphics;`
- **[Error] SDX023** SharpDX math namespace
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:1:1`
  - Code: `using SharpDX;`
- **[Info] SDX003** .NET Framework 4.x target
  - Project: `SdxLowLevel.csproj`
  - Code: `TargetFrameworkVersion=v4.5`

### Manual

- **[Error] SDX043** SetRenderTargets (depth buffer or several targets)
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:41:13`
  - Code: `GraphicsDevice.SetRenderTargets(depth, target);`
- **[Warning] SDX006** Direct use of SharpDX.DXGI
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:3:1`
  - Code: `using SharpDX.DXGI;`
- **[Warning] SDX032** Left-handed Matrix factories
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:39:24`
  - Code: `var view = Matrix.LookAtLH(new Vector3(0f, 0f, -5f), Vector3.Zero, Vector3.UnitY);`
- **[Warning] SDX033** Vector3.TransformCoordinate
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:40:25`
  - Code: `var point = Vector3.TransformCoordinate(Vector3.One, view);`
- **[Warning] SDX034** RectangleF
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:16:17`
  - Code: `private RectangleF bounds;`
- **[Warning] SDX034** RectangleF
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:29:26`
  - Code: `bounds = new RectangleF(0f, 0f, 10f, 10f);`
- **[Warning] SDX035** Color4 and Color3
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:17:17`
  - Code: `private Color4 tint;`
- **[Warning] SDX035** Color4 and Color3
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:30:24`
  - Code: `tint = new Color4(1f, 1f, 1f, 1f);`
- **[Warning] SDX045** RenderTarget2D.New
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:31:22`
  - Code: `target = RenderTarget2D.New(GraphicsDevice, 256, 256, PixelFormat.R8G8B8A8.UNorm);`
- **[Warning] SDX050** DrawQuad
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:42:13`
  - Code: `GraphicsDevice.DrawQuad(null);`

### Unsupported

- **[Error] SDX005** Direct use of SharpDX.Direct3D11
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:2:1`
  - Code: `using SharpDX.Direct3D11;`
- **[Error] SDX048** GeometricPrimitive
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:15:17`
  - Code: `private GeometricPrimitive cube;`
- **[Error] SDX048** GeometricPrimitive
  - Project: `SdxLowLevel.csproj`
  - Location: `Game1.cs:28:20`
  - Code: `cube = GeometricPrimitive.Cube.New(GraphicsDevice);`

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

### SDX005 Direct use of SharpDX.Direct3D11

The code uses the raw Direct3D 11 API through SharpDX.Direct3D11. MonoGame has no such layer. Code written against devices, contexts and buffers has to be rewritten against the MonoGame graphics API. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Rewrite the affected code with MonoGame GraphicsDevice, VertexBuffer, IndexBuffer and Effect types. This cannot be automated.

### SDX006 Direct use of SharpDX.DXGI

The code uses SharpDX.DXGI, usually for pixel formats and swap chain settings. Formats map to MonoGame SurfaceFormat in simple cases, but not by name. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Replace each DXGI format or setting with the MonoGame equivalent (SurfaceFormat, PresentationParameters) by hand.

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

### SDX032 Left-handed Matrix factories

The code builds left-handed matrices. XNA and MonoGame are right-handed. A rename would be wrong, because winding order and view direction change with the handedness. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Convert the scene to right-handed coordinates (flip Z and the triangle winding), then use the Create* factories.

### SDX033 Vector3.TransformCoordinate

TransformCoordinate divides by w. MonoGame Vector3.Transform does not. The result is the same for affine matrices and different for projection matrices. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Use Vector3.Transform for affine matrices. For a projection, transform as Vector4 and divide by w.

### SDX034 RectangleF

RectangleF is a SharpDX type with float fields. MonoGame has no such type, and SpriteBatch.Draw takes a Rectangle with integer fields. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Use Rectangle, or add a small struct of your own. With the compatibility shims on, the migrated copy gets a RectangleF stand-in (sharpdx-toolkit task 14) and no marker.

### SDX035 Color4 and Color3

Color4 and Color3 are float color structs of SharpDX. MonoGame has Color (bytes) and Vector4 (floats) but no type of this name. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Use Color for byte colors or Vector4 for float colors. With the compatibility shims on, the migrated copy gets Color4 and Color3 stand-ins (sharpdx-toolkit task 14) and no marker.

### SDX043 SetRenderTargets (depth buffer or several targets)

The code passes a depth buffer or several targets to SetRenderTargets. In MonoGame a render target owns its depth buffer, and several targets are set with SetRenderTargets(RenderTargetBinding[]). Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Create each render target with the depth format it needs and set them with SetRenderTargets(params RenderTargetBinding[]).

### SDX045 RenderTarget2D.New

RenderTarget2D.New is a static factory that takes a DXGI PixelFormat and a mip map count. MonoGame uses a constructor with SurfaceFormat and DepthFormat. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Call new RenderTarget2D(device, width, height, mipMap, SurfaceFormat.Color, DepthFormat.Depth24) and pick the formats by hand.

### SDX048 GeometricPrimitive

GeometricPrimitive (cube, sphere, teapot and so on) has no MonoGame equivalent. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Build the vertex and index buffers yourself, or load a model for the shape.

### SDX050 DrawQuad

GraphicsDevice.DrawQuad draws a full-screen quad. MonoGame has no such call. Matching is by syntax only, so a look-alike name in unrelated code can match.

**Resolution:** Draw a full-screen SpriteBatch pass instead.
