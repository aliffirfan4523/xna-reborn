# Design: SharpDX Toolkit to MonoGame (C# / .NET 8)

## Overview

A SharpDX Toolkit game is close to an XNA game in shape and far from one in detail. The class names match (`Game`, `GraphicsDeviceManager`, `SpriteBatch`, `Texture2D`, `SpriteFont`, `BasicEffect`), so many files need only a namespace change. The differences are in four places: the math library (SharpDX, not XNA), the graphics state API, input, and the content build (build actions in the game project, `.tkb` output, Toolkit effect syntax).

The plan is to add a second source framework to the existing pipeline, not a second pipeline:

| Stage | Reuse | New |
|---|---|---|
| Discovery | `ProjectDiscovery`, `XnaProjectDetector` pattern | `SharpDxToolkitDetector`, a `SourceFramework` value on `DiscoveredProject` |
| Reading | `ProjectReader`, `ProjectItem` list (already keeps any item type) | Toolkit content item reader |
| Rules | `RuleCatalogLoader`, matchers, `SourceRuleMatcher`, scoring | A second catalog file, id pattern widened, `names` on `member` |
| Plan | `MigrationPlanner` | Catalog-aware lookup of `migratorTask` |
| Project files | `SdkCsprojGenerator`, `PackageSelector`, `SolutionGenerator`, `ToolManifestGenerator` | A SharpDX clean-up step and WindowsDX default |
| Source | `RewriterRegistry`, `ISourceRewriter`, `SourceRewriteEngine`, `MarkerInserter`, `DiffBuilder` | Four rewriters, one rename map |
| Content | `ContentConverter`, `MgcbWriter`, `AssetNameResolver`, `ImporterMap` | `TkFont` to `.spritefont` converter and Toolkit item classification |
| Run | `BuildRunner`, `HintCatalog` | Hint entries for SDX rules |
| UI | Report binding | A label for the source framework |

## Sources

All facts about the Toolkit below were read from these primary sources. Links point at the archived repository, so they stay readable.

- Toolkit repository and status (archived, "deprecated and no longer supported", MIT): [sharpdx/Toolkit](https://github.com/sharpdx/Toolkit)
- NuGet history of `SharpDX.Toolkit` (2.4.0, 2.4.1, 2.4.2, 2.5.0, 2.6.0, 2.6.1, 2.6.2, 2.6.3): [NuGet flat container index](https://api.nuget.org/v3-flatcontainer/sharpdx.toolkit/index.json). `SharpDX.Toolkit.Game` has the same list.
- Port to SharpDX 3.0 by a third party, never merged into a release: [Toolkit PR #4](https://github.com/sharpdx/Toolkit/pull/4)
- Build integration (build actions, `.tkb` output, tasks): [Build/SharpDX.Toolkit.targets](https://github.com/sharpdx/Toolkit/blob/master/Build/SharpDX.Toolkit.targets) and [CompilerDependencyTask.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.CompilerTask/CompilerDependencyTask.cs)
- Game, device manager, time: [Game.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Game/Game.cs), [GraphicsDeviceManager.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Game/GraphicsDeviceManager.cs), [GameTime.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Game/GameTime.cs)
- Graphics: [SpriteBatch.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/SpriteBatch.cs), [GraphicsDevice.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/GraphicsDevice.cs), [Texture2D.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/Texture2D.cs), [BlendStateCollection.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/BlendStateCollection.cs), [EffectPass.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/EffectPass.cs), [Model.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/Model.cs)
- Input: [KeyboardManager.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Input/KeyboardManager.cs), [KeyboardState.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Input/KeyboardState.cs), [MouseState.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Input/MouseState.cs)
- Content: [ContentManager.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit/Content/ContentManager.cs), [FontDescription.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Compiler/Font/FontDescription.cs), [Arial16.xml](https://github.com/sharpdx/Toolkit/blob/master/Source/VisualStudio/ToolkitGame/Arial16.xml), [Bloom.fx](https://github.com/sharpdx/Toolkit/blob/master/Source/VisualStudio/ToolkitGame/Bloom.fx)
- The project template a typical Toolkit game started from (class shape, `ToDisposeContent`, input managers, state collections): [ProjectTemplate.csproj](https://github.com/sharpdx/Toolkit/blob/master/Source/VisualStudio/ToolkitGame/ProjectTemplate.csproj), [Program.cs](https://github.com/sharpdx/Toolkit/blob/master/Source/VisualStudio/ToolkitGame/Program.cs)
- SharpDX 2.6.3 math (the version Toolkit 2.6.x used): [Matrix.cs](https://github.com/sharpdx/SharpDX/blob/2.6.3/Source/SharpDX/Matrix.cs), [MathUtil.cs](https://github.com/sharpdx/SharpDX/blob/2.6.3/Source/SharpDX/MathUtil.cs), [Vector3.cs](https://github.com/sharpdx/SharpDX/blob/2.6.3/Source/SharpDX/Vector3.cs)
- MonoGame side: [MathHelper](https://docs.monogame.net/api/Microsoft.Xna.Framework.MathHelper.html), [GraphicsDevice](https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.GraphicsDevice.html)

What was not checked: MonoGame member names other than `MathHelper` and `GraphicsDevice.SetRenderTarget` came from the XNA 4.0 surface MonoGame implements. Task 5.1 confirms each one against the 3.8.6.1 API docs before a rewriter ships. Mouse coordinate units of the Toolkit `MouseState` were not confirmed, which is why the mouse is Manual.

## What the Toolkit is

| Assembly (NuGet id) | Namespace | Holds |
|---|---|---|
| `SharpDX` | `SharpDX` | Math (`Vector2/3/4`, `Matrix`, `Color`, `Color4`, `Rectangle`, `RectangleF`, `MathUtil`), `Utilities`, COM base |
| `SharpDX.Direct3D11`, `SharpDX.DXGI` | same | Raw Direct3D 11 and DXGI |
| `SharpDX.Toolkit` | `SharpDX.Toolkit`, `SharpDX.Toolkit.Content` | `Component`, `ContentManager` |
| `SharpDX.Toolkit.Game` | `SharpDX.Toolkit` | `Game`, `GameTime`, `GraphicsDeviceManager`, `GameSystem`, `GameWindow` |
| `SharpDX.Toolkit.Graphics` | `SharpDX.Toolkit.Graphics` | `GraphicsDevice`, `SpriteBatch`, `Texture2D`, `SpriteFont`, `Effect`, `BasicEffect`, `Model`, `GeometricPrimitive`, state collections |
| `SharpDX.Toolkit.Input` | `SharpDX.Toolkit.Input` | `KeyboardManager`, `MouseManager`, `PointerManager` |
| `SharpDX.Toolkit.Audio` | `SharpDX.Toolkit.Audio` | `SoundEffect`, `AudioManager` (XAudio2) |
| `SharpDX.Toolkit.Compiler` | build time | `tkfxc`, `tkfont`, `tkmodel` compilers |

`Game` lives in `SharpDX.Toolkit`, not in `SharpDX.Toolkit.Game`. The word "Game" in the package name is the assembly.

## What maps and what does not

### One-to-one by namespace or name

| Toolkit | MonoGame | Notes |
|---|---|---|
| `using SharpDX.Toolkit;` (`Game`, `GameTime`, `GraphicsDeviceManager`) | `using Microsoft.Xna.Framework;` | Same class names. `GameTime` has `ElapsedGameTime`, `TotalGameTime`, `IsRunningSlowly`. The Toolkit adds `FrameCount`. |
| `using SharpDX.Toolkit.Graphics;` | `using Microsoft.Xna.Framework.Graphics;` | `SpriteBatch`, `Texture2D`, `SpriteFont`, `BasicEffect`, `Effect`, `RenderTarget2D`, `SpriteSortMode`, `SpriteEffects` |
| `using SharpDX.Toolkit.Content;` | `using Microsoft.Xna.Framework.Content;` | `Content.Load<T>("name")` keeps its shape |
| `using SharpDX.Toolkit.Input;` | `using Microsoft.Xna.Framework.Input;` | `Keys`, `ButtonState`, `KeyboardState` names. The common `Keys` names (`A`, `D0`, `Enter`, `Escape`, `Space`, `Left`, `Up`, `NumPad0`, `LeftShift`) are the same. |
| `using SharpDX;` (only for math types) | `using Microsoft.Xna.Framework;` | `Vector2`, `Vector3`, `Vector4`, `Matrix`, `Color`, `Rectangle`, `Quaternion`, `BoundingBox`, `Ray` keep their names |
| `Game()`, `Initialize`, `LoadContent`, `UnloadContent`, `Update(GameTime)`, `Draw(GameTime)`, `Exit`, `Run()`, `Window.Title`, `IsMouseVisible`, `IsFixedTimeStep`, `TargetElapsedTime`, `Content.RootDirectory` | same | |
| `GraphicsDeviceManager`: `PreferredBackBufferWidth/Height`, `IsFullScreen`, `SynchronizeWithVerticalRetrace`, `ApplyChanges()` | same | `PreferMultiSampling` also exists in MonoGame |
| `SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Matrix)`, `Draw(texture, position, color)` and the long overload, `DrawString`, `End` | same | Same argument order as XNA 4.0 |
| `GraphicsDevice.Clear(Color)` | same | `Color` converts |
| `SpriteFont.MeasureString` | same | |
| `Texture2D.Width/Height` | same | |
| `EffectPass.Apply()`, `Effect.CurrentTechnique.Passes` | same | |
| `Keys`, `KeyboardState.IsKeyDown(Keys)` | same | |

### Renamed (AutoFixable)

| Toolkit | MonoGame | Rule |
|---|---|---|
| `Matrix.LookAtRH` | `Matrix.CreateLookAt` | SDX030 |
| `Matrix.PerspectiveFovRH` | `Matrix.CreatePerspectiveFieldOfView` | SDX030 |
| `Matrix.OrthoOffCenterRH` | `Matrix.CreateOrthographicOffCenter` | SDX030 |
| `Matrix.Scaling`, `Matrix.Translation(...)` | `Matrix.CreateScale`, `Matrix.CreateTranslation` | SDX030 |
| `Matrix.RotationX/Y/Z`, `Matrix.RotationYawPitchRoll` | `Matrix.CreateRotationX/Y/Z`, `Matrix.CreateFromYawPitchRoll` | SDX030 |
| `MathUtil.Clamp/Lerp/SmoothStep/Pi/TwoPi` | `MathHelper.*` (same names) | SDX031 |
| `MathUtil.PiOverTwo`, `PiOverFour` | `MathHelper.PiOver2`, `PiOver4` | SDX031 |
| `MathUtil.DegreesToRadians`, `RadiansToDegrees` | `MathHelper.ToRadians`, `ToDegrees` | SDX031 |
| `GraphicsDevice.BlendStates.Opaque/AlphaBlend/Additive/NonPremultiplied` | `BlendState.Opaque/AlphaBlend/Additive/NonPremultiplied` | SDX040 |
| `GraphicsDevice.SamplerStates.PointClamp/PointWrap/LinearClamp/LinearWrap/AnisotropicClamp/AnisotropicWrap` | `SamplerState.*` (same names) | SDX040 |
| `GraphicsDevice.DepthStencilStates.Default/DepthRead/None` | `DepthStencilState.*` (same names) | SDX040 |
| `GraphicsDevice.SetBlendState(s)` | `GraphicsDevice.BlendState = s` | SDX041 |
| `GraphicsDevice.SetDepthStencilState(s)` | `GraphicsDevice.DepthStencilState = s` | SDX041 |
| `GraphicsDevice.SetRasterizerState(s)` | `GraphicsDevice.RasterizerState = s` | SDX041 |
| `GraphicsDevice.SetRenderTargets(rt)` | `GraphicsDevice.SetRenderTarget(rt)` | SDX042 |
| `GraphicsDevice.BackBuffer.Width/Height` | `GraphicsDevice.PresentationParameters.BackBufferWidth/Height` | SDX044 |
| `ToDisposeContent(x)` | `x` | SDX060 |
| `new KeyboardManager(this)` and `keyboard.GetState()` | `Keyboard.GetState()` | SDX025 |

Names that do not match one-to-one stay out of the AutoFixable tables on purpose: `BlendStates.Default`, `RasterizerStates.*` (the Toolkit has `CullFront`/`CullBack`/`WireFrame`; MonoGame has `CullClockwise`/`CullCounterClockwise`/`CullNone`, and which face is "back" depends on winding), `SamplerStates.Default`. The rewriter marks them.

### Does not map (Manual or Unsupported)

| Toolkit | Why | Rule |
|---|---|---|
| Left-handed matrix factories (`LookAtLH`, `PerspectiveFovLH`, `OrthoLH`, `OrthoOffCenterLH`) | XNA and MonoGame are right-handed. A rename is wrong, because winding and view direction change too. | SDX032 |
| `Vector3.TransformCoordinate` | Divides by w. MonoGame `Vector3.Transform` does not. Same result for affine matrices, different for projection. | SDX033 |
| `RectangleF` | No such type in MonoGame. `SpriteBatch.Draw` takes `Rectangle` for destinations. | SDX034 |
| `Color4`, `Color3` | Float color structs with no MonoGame type. | SDX035 |
| `MouseManager`, `MouseState` with float `X`/`Y`, `PointerManager` | MonoGame has the static `Mouse` and `TouchPanel`. Units and event model differ. | SDX026, SDX027 |
| `KeyboardState.IsKeyPressed/IsKeyReleased` | Toolkit tracks edges. MonoGame needs the previous state kept by the game. | SDX028 |
| `GraphicsDevice.SetRenderTargets(depthBuffer, rt, ...)` | MonoGame render targets own their depth buffer. | SDX043 |
| `RenderTarget2D.New(...)`, `Texture2D.New(...)` | Static factories with `PixelFormat` (DXGI) and `MipMapCount`. MonoGame uses constructors with `SurfaceFormat`. | SDX045, SDX046 |
| `Model.Draw(device, world, view, projection)`, `BasicEffect.EnableDefaultLighting(model, bool)` | Different `Model` API and ownership of effects. | SDX047 |
| `GeometricPrimitive` | No MonoGame equivalent. | SDX048 |
| `Effect.Parameters["x"].SetResource(...)` | Samplers and textures are set differently. MonoGame has `SetValue(Texture)` and sets samplers on the device. | SDX049 |
| `GraphicsDevice.DrawQuad(...)`, `Quad` | No MonoGame equivalent. A full-screen `SpriteBatch` pass replaces it. | SDX050 |
| `SpriteBatch.Begin(SpriteSortMode, Effect)` | This two-argument overload does not exist in XNA or MonoGame. | SDX051 |
| `GameSystem`, `GameSystemCollection` | MonoGame has `GameComponent` and `DrawableGameComponent` with a different lifecycle. | SDX062 |
| `SharpDXElement`, `GameContext` hosting (WPF, WinRT) | Game hosted inside another UI. | SDX063 |
| `SharpDX.Direct3D11`, `DXGI`, `Direct2D1`, `XAudio2` used directly | Raw API access. | SDX005 to SDX008 |
| Toolkit effects (`.fx` with `Profile = ...;`, `PixelShader = name;`, `Texture2D<float4>`, separate `SamplerState`) | Compiled by `tkfxc`. It is not the syntax of the MonoGame effect compiler. | SDX013 |
| `.tkb` files with no source | Compiled format with no importer. | SDX015 |

The Toolkit effect syntax claim comes from the sample `Bloom.fx`: it declares `technique`/`pass` blocks with `Profile = 9.1;` and `PixelShader = PSBrightPassFilter;` with no `compile` keyword, and uses `Texture2D<float4> Texture;` and `SamplerState PointSampler;` globals. Task 11 re-checks this against the MonoGame effect compiler on a real file before the explanation text is final.

## How the Toolkit content build differed from XNA

| | XNA 4.0 | Toolkit 2.x |
|---|---|---|
| Where assets are listed | A separate `.contentproj` (nested content project) | In the game `.csproj`, as items with a build action |
| Item kinds | `Compile` items with `Importer` and `Processor` metadata | `ToolkitFxc`, `ToolkitFont`, `ToolkitModel`, `ToolkitTexture`, `ToolkitAudio` |
| Compiler | MSBuild content pipeline, `.xnb` output | MSBuild tasks (`EffectCompilerTask`, `FontCompilerTask`, `ModelCompilerTask`, `TextureCompilerTask`, `AudioCompilerTask`) from `SharpDX.Toolkit.targets`, `.tkb` output |
| Output extension | `.xnb` | `.tkb` (`Path.ChangeExtension(link, "tkb")`) |
| Load name | Asset name without extension | Name without extension gets `.tkb` appended. A name with an extension is loaded as that file. |
| Font source | `.spritefont` (`XNAProject/Asset Type="Graphics:FontDescription"`) | XML with a `TkFont` root: `FontName`, `Size`, `Spacing`, `LineSpacing`, `UseKerning`, `Format`, `CharacterRegions/CharacterRegion/Start+End` (numbers), `DefaultCharacter` (number), `Style`, `NoPremultiply`, `AntiAlias` |
| Textures | Texture importer and processor | `ToolkitTexture` is mostly a pass-through copy ("a future version may provide compression"), so `.dds` and common images are loaded at run time |
| Effects | `.fx` (Effect 9) via EffectImporter | `.fx` compiled by `tkfxc` to a binary effect, with Toolkit syntax and Direct3D 11 shader models |
| Models | FBX/X importers, ModelProcessor | `tkmodel` using Assimp (the targets copy `Assimp32.dll`/`Assimp64.dll`) |
| Generated code | None | `CompilerDependencyTask` can add `*.Generated.cs` files to compile (effect wrapper classes) |

The generated code row matters: a Toolkit project can compile a file that is not in source control. The migrated copy has none, so a build error about a missing generated class maps to SDX013.

## Detection

New class `SharpDxToolkitDetector` in `src/XnaReborn.Analyzer/Discovery/`, next to `XnaProjectDetector`. Same inputs (a project `XDocument` and the project folder), same read-only rules.

Signals, in this order. Any one is enough:

1. `Reference` items whose `Include` simple name starts with `SharpDX.Toolkit`.
2. `Import` items whose `Project` ends with `SharpDX.Toolkit.targets`.
3. Items of type `ToolkitFxc`, `ToolkitFont`, `ToolkitModel`, `ToolkitTexture`, `ToolkitAudio`.
4. A sibling `packages.config` with a `SharpDX.Toolkit*` package id (read only through `IReadOnlyFileSystem`).

`SharpDX.targets` alone (low-level SharpDX, no Toolkit) is not enough. A game on raw SharpDX is not what this spec migrates.

Evidence this works on real projects: the open-source candidates below use `<Reference Include="SharpDX.Toolkit.Game">` with `$(SharpDXPackageBinDir)` hint paths, import `SharpDX.targets` and `SharpDX.Toolkit.targets`, and have `ToolkitFont` and `ToolkitTexture` items (checked by reading their `.csproj` files).

Model change:

```csharp
public enum SourceFramework { Xna, SharpDxToolkit }

public sealed record DiscoveredProject(
    string RelativePath,
    string FullPath,
    bool IsXna,
    bool IsSharpDxToolkit);   // Frameworks derived: both can be true (mixed)
```

`IsXna` stays so the existing XNA code does not change. `DiscoveryResult.XnaProjects` stays. A new `DiscoveryResult.MigratableProjects` returns projects where either flag is true. `ProjectReader` fills `ProjectInfo.SourceFrameworks` (a set), and the report carries it. The "no XNA project found" reason text names both frameworks.

## Rule catalog

### Files

- `rules/sharpdx-toolkit-to-monogame.json`: the SDX rules. Same shape as the XNA catalog (`version`, `name`, `scoring`, `rules`).
- `rules/sharpdx-toolkit-map.json`: the rename map used by the namespace rewriter and the AutoFixable rewriters. It is data, so a rename is a data change. Sources listed in the file like `importer-map.json` does.

### Schema changes (small, backward compatible)

1. Rule id pattern `^XNA[0-9]{3}$` becomes `^(XNA|SDX)[0-9]{3}$` in `RuleSchema.json` and `RuleCatalogValidator`. The XNA catalog still validates.
2. `member` match gets an optional `names` array as an alternative to `name`. Exactly one of `name` or `names` is required. `SourceRuleIndex` indexes a rule under each name.
3. A catalog may declare `"appliesTo": "SharpDxToolkit"`. The analyzer runs a catalog only on projects of that framework. The XNA catalog defaults to `Xna`, so it needs no edit.

### Why `names` and no new match type

All 45 rules fit the existing types: `reference`, `projectItem`, `projectProperty`, `namespace`, `filePattern`, `type`, `member`. The one gap is the rename families. Without `names`, `Matrix.RotationX`, `RotationY`, `RotationZ`, `Scaling`, `Translation`, `LookAtRH` and the rest would be about twenty near-identical rules, each with its own fixtures, and the report would list twenty findings for one idea. `names` keeps one rule per idea.

Rejected, with reasons:

- `argumentPattern` on `member` (match a string literal argument, such as `Content.Load<Texture2D>("Balls.dds")`). Useful, but the content converter already records asset names and the rewrite does not depend on it. Revisit if the candidate runs show a real cost.
- `fileContent` (glob plus regex) for `Profile =` inside `.fx` files. Every `ToolkitFxc` item is a Toolkit effect by definition, so `projectItem` already finds them.
- A `baseType` match for `class X : Game`. `type` with `name: "Game"` and `namespaces: ["SharpDX.Toolkit"]` already gates on the using directive. The migrator needs no more than that.

### Proposed rules

Categories use the existing enum (`Project`, `Reference`, `Api`, `Content`, `Platform`). "Task" is the task in `tasks.md` that implements the fix or the rule.

| Id | Title | Severity | Class | Match type | Match | Cat. | Task |
|---|---|---|---|---|---|---|---|
| SDX001 | SharpDX assembly reference | Error | AutoFixable | reference | `SharpDX*` | Reference | 8.1 |
| SDX002 | SharpDX build targets import | Error | AutoFixable | projectItem | Import `*SharpDX*.targets` | Project | 8.1 |
| SDX003 | .NET Framework 4.x target | Info | AutoFixable | projectProperty | `TargetFrameworkVersion` matches `^v[1-4]\.` | Project | 8.1 |
| SDX004 | Windows Store or Windows Phone project | Blocker | Unsupported | projectProperty | `ProjectTypeGuids` matches `BC8A1FFA\|C089C8C0` (GUID prefixes) | Platform | n/a |
| SDX005 | Direct use of SharpDX.Direct3D11 | Error | Unsupported | namespace | `SharpDX.Direct3D11` | Api | n/a |
| SDX006 | Direct use of SharpDX.DXGI | Warning | Manual | namespace | `SharpDX.DXGI` | Api | n/a |
| SDX007 | Direct use of SharpDX.Direct2D1 | Error | Unsupported | namespace | `SharpDX.Direct2D1` | Api | n/a |
| SDX008 | Direct use of SharpDX.XAudio2 | Error | Unsupported | namespace | `SharpDX.XAudio2` | Api | n/a |
| SDX010 | Toolkit font (`ToolkitFont`) | Warning | AutoFixable | projectItem | `ToolkitFont` | Content | 9.2 |
| SDX011 | Toolkit texture (`ToolkitTexture`) | Info | AutoFixable | projectItem | `ToolkitTexture` | Content | 9.3 |
| SDX012 | Toolkit model (`ToolkitModel`) | Warning | AutoFixable | projectItem | `ToolkitModel` | Content | 9.3 |
| SDX013 | Toolkit effect (`ToolkitFxc`) | Error | Manual | projectItem | `ToolkitFxc` | Content | n/a |
| SDX014 | Toolkit audio (`ToolkitAudio`) | Warning | AutoFixable | projectItem | `ToolkitAudio` | Content | 9.3 |
| SDX015 | Prebuilt `.tkb` asset | Warning | Unsupported | filePattern | `**/*.tkb` | Content | n/a |
| SDX020 | Toolkit game namespace | Error | AutoFixable | namespace | `SharpDX.Toolkit` (no children) | Api | 5.1 |
| SDX021 | Toolkit graphics namespace | Error | AutoFixable | namespace | `SharpDX.Toolkit.Graphics` | Api | 5.1 |
| SDX022 | Toolkit content namespace | Warning | AutoFixable | namespace | `SharpDX.Toolkit.Content` | Api | 5.1 |
| SDX023 | SharpDX math namespace | Error | AutoFixable | namespace | `SharpDX` (no children) | Api | 5.1 |
| SDX024 | Toolkit input namespace | Warning | AutoFixable | namespace | `SharpDX.Toolkit.Input` | Api | 5.1 |
| SDX025 | KeyboardManager | Warning | AutoFixable | type | `KeyboardManager` in `SharpDX.Toolkit.Input` | Api | 7.1 |
| SDX026 | MouseManager | Warning | Manual | type | `MouseManager` in `SharpDX.Toolkit.Input` | Api | n/a |
| SDX027 | PointerManager (touch) | Warning | Manual | type | `PointerManager` in `SharpDX.Toolkit.Input` | Api | n/a |
| SDX028 | Edge-triggered key state | Warning | Manual | member | names `IsKeyPressed`, `IsKeyReleased` | Api | n/a |
| SDX030 | Right-handed Matrix factories | Warning | AutoFixable | member | receiver `Matrix`, names `LookAtRH`, `PerspectiveFovRH`, `OrthoOffCenterRH`, `Scaling`, `Translation`, `RotationX`, `RotationY`, `RotationZ`, `RotationYawPitchRoll` | Api | 6.1 |
| SDX031 | MathUtil members | Warning | AutoFixable | member | receiver `MathUtil`, names `Clamp`, `Lerp`, `SmoothStep`, `Pi`, `TwoPi`, `PiOverTwo`, `PiOverFour`, `DegreesToRadians`, `RadiansToDegrees` | Api | 6.1 |
| SDX032 | Left-handed Matrix factories | Warning | Manual | member | receiver `Matrix`, names `LookAtLH`, `PerspectiveFovLH`, `OrthoLH`, `OrthoOffCenterLH` | Api | n/a |
| SDX033 | Vector3.TransformCoordinate | Warning | Manual | member | `TransformCoordinate` | Api | n/a |
| SDX034 | RectangleF | Warning | Manual | type | `RectangleF` in `SharpDX` | Api | n/a |
| SDX035 | Color4 | Warning | Manual | type | `Color4` in `SharpDX` | Api | n/a |
| SDX040 | Graphics state collections | Warning | AutoFixable | member | receiver `GraphicsDevice`, names `BlendStates`, `SamplerStates`, `DepthStencilStates`, `RasterizerStates` (Access) | Api | 6.2 |
| SDX041 | Set*State calls | Warning | AutoFixable | member | receiver `GraphicsDevice`, names `SetBlendState`, `SetDepthStencilState`, `SetRasterizerState`, 1 argument | Api | 6.2 |
| SDX042 | SetRenderTargets (one target) | Warning | AutoFixable | member | `SetRenderTargets`, arguments 1 to 1 | Api | 6.2 |
| SDX043 | SetRenderTargets (depth buffer or several) | Error | Manual | member | `SetRenderTargets`, arguments 2 to 8 | Api | n/a |
| SDX044 | BackBuffer size | Warning | AutoFixable | member | receiver `GraphicsDevice`, `BackBuffer` (Access) | Api | 6.2 |
| SDX045 | RenderTarget2D.New | Warning | Manual | member | receiver `RenderTarget2D`, `New` | Api | n/a |
| SDX046 | Texture2D.New | Warning | Manual | member | receiver `Texture2D`, `New` | Api | n/a |
| SDX047 | Toolkit Model API | Warning | Manual | type | `Model` in `SharpDX.Toolkit.Graphics` | Api | n/a |
| SDX048 | GeometricPrimitive | Error | Unsupported | type | `GeometricPrimitive` in `SharpDX.Toolkit.Graphics` | Api | n/a |
| SDX049 | Effect parameter SetResource | Warning | Manual | member | `SetResource` (Invocation) | Api | n/a |
| SDX050 | DrawQuad | Warning | Manual | member | `DrawQuad` (Invocation) | Api | n/a |
| SDX051 | SpriteBatch.Begin(sortMode, effect) | Warning | Manual | member | receiver `spriteBatch`, `Begin`, arguments 2 to 2 | Api | n/a |
| SDX060 | ToDisposeContent | Info | AutoFixable | member | `ToDisposeContent` (Invocation) | Api | 6.2 |
| SDX061 | PreferredGraphicsProfile | Info | Manual | member | `PreferredGraphicsProfile` (Access) | Api | n/a |
| SDX062 | GameSystem components | Warning | Manual | type | `GameSystem` in `SharpDX.Toolkit` | Api | n/a |
| SDX063 | Hosted game (SharpDXElement) | Error | Unsupported | type | `SharpDXElement` in `SharpDX.Toolkit` | Platform | n/a |

Totals: 45 rules. 20 AutoFixable, 18 Manual, 7 Unsupported. Ids have gaps by category on purpose (000s project and interop, 010s content, 020s namespaces and input, 030s math, 040s graphics, 060s game), so a new rule has a place.

Notes on the table:

- SDX023 matches `SharpDX` with no children, so `using SharpDX.Toolkit.Graphics;` does not also fire it. The rewriter replaces `using SharpDX;` with `using Microsoft.Xna.Framework;` and then lets SDX034 and SDX035 mark `RectangleF`, `Color4`, and the other SharpDX-only types that the file still uses.
- SDX001 uses the wildcard `SharpDX*`, so one finding per reference. SDX005 to SDX008 are about source use, which is the part with real cost.
- `Matrix.Translation` is also a property on the SharpDX `Matrix` instance (`TranslationVector` in 2.x). The rule needs `Kind: Invocation` for the factory form. Task 6.1 has a negative fixture for the property form.
- SDX051 uses `receiver: "spriteBatch"`, the same syntactic trick the XNA catalog uses for `pass` and `effect`. It misses a field with another name. That is a known limit, stated in the explanation.
- SDX004 GUID prefixes are the two values the Toolkit's own project template used for the WinRT and Windows Phone 8 project types. Task 4.1 confirms them against the template file.

All rules point out in the explanation that matching is by syntax (R2.8).

## Source rewriting

### Framework

No change to `ISourceRewriter` (text in, text out, registered by rule id) or `SourceRewriteEngine`. New rewriters are added in `DefaultRewriters`. Each parses the text it receives with Roslyn when it needs a tree and edits only the spans it changes, so untouched bytes survive (the existing "preserve trivia" test is the model).

The engine runs rewriters in rule id order. That order is useful here: SDX020 to SDX024 (namespaces) run before SDX025 and SDX030 and later rules, so later rewriters see MonoGame namespaces.

### Rewriters

| Rewriter | Rules | What it does |
|---|---|---|
| `ToolkitNamespaceRewriter` | SDX020 to SDX024 | Reads `rules/sharpdx-toolkit-map.json` and rewrites `using` directives, `using` aliases and fully qualified names. Merges duplicates, so two Toolkit usings that map to one MonoGame namespace become one `using`. Leaves `using SharpDX.Direct3D11;` alone and lets SDX005 mark it. |
| `ToolkitMathRewriter` | SDX030, SDX031 | Renames `Matrix.*` factories and `MathUtil.*` members to `Matrix.Create*` and `MathHelper.*` from the map. Receiver must be the type name itself, so an instance named `matrix` is never touched. |
| `ToolkitGraphicsRewriter` | SDX040 to SDX044, SDX060 | State collection access, `Set*State` calls (one argument only), one-target `SetRenderTargets`, `BackBuffer.Width/Height`, `ToDisposeContent` unwrapping. Names that do not map one-to-one get a marker. |
| `ToolkitInputRewriter` | SDX025 | Removes the `KeyboardManager` field and its `new KeyboardManager(this)` assignment, replaces `x.GetState()` on that field with `Keyboard.GetState()`. Leaves the code and adds a marker when the manager is used any other way (passed to a method, stored in a collection). |

Each Manual finding gets a marker from `MarkerInserter`. No rewriter guesses.

### `ToDisposeContent` and disposal

`ToDisposeContent(x)` registers `x` for disposal when the game unloads. MonoGame's `Game` has no such method. Unwrapping drops that automatic disposal. For process-lifetime objects (the usual case) this changes nothing visible. The manifest note for SDX060 says so. This is the one rewrite where behavior is not identical, and it is Info severity for that reason: the report must not hide it.

### Handedness

SharpDX offers both `LookAtRH` and `LookAtLH`. XNA and MonoGame use right-handed matrices. A project using the RH functions renames cleanly. A project using the LH functions is marked and left alone (SDX032), because switching handedness also changes winding and mesh data.

## Project generation

The existing `SdkCsprojGenerator` writes a fresh SDK-style project from `ProjectInfo`, dropping what it knows about. Added for Toolkit projects:

1. Drop references named `SharpDX*`, imports of `SharpDX*.targets`, the `EnsureNuGetPackageBuildImports` target and the `NuGetPackageImportStamp` property, the five Toolkit build actions, and `HintPath` items under `$(SharpDXPackageBinDir)`.
2. Drop `packages.config`, `.nuget/`, and `packages/` from the copy.
3. Keep `System.*` references that the generator already maps. `System.Windows.Forms` stays a manual finding if source code uses WinForms types.
4. `PackageSelector` defaults to `WindowsDX` for Toolkit projects. The reason: the effects are HLSL for Direct3D, and WindowsDX is the MonoGame target that compiles effects with the Direct3D shader compiler. DesktopGL stays selectable.
5. The target framework follows the existing generator (`net8.0`, or the Windows variant it already chooses for WindowsDX).

Output type and entry point: Toolkit `Program.Main` calls `using (var program = new Game1()) program.Run();`, wrapped in `#if NETFX_CORE` only for the `[MTAThread]` attribute. The existing `EntryPointRewriter` already handles platform `#if` blocks. Task 8.2 adds a fixture to confirm it covers `NETFX_CORE`.

## Content conversion

### Reading

`ToolkitContentReader` in `src/XnaReborn.Analyzer/Reading/` builds `ContentItem` records from the five build actions in the game project. These items have no importer or processor, so the reader sets the item kind and leaves them null. It fills `AssetName` the way the Toolkit did: the `Link` metadata when present, otherwise the project-relative path, without the extension.

### Converting

`ContentConverter` gets a Toolkit branch, and `MgcbWriter` stays unchanged.

| Build action | Output | Status |
|---|---|---|
| `ToolkitFont` | A generated `.spritefont` plus an `.mgcb` entry with `FontDescriptionImporter`/`FontDescriptionProcessor` | Converted |
| `ToolkitTexture` | `.mgcb` entry with `TextureImporter`/`TextureProcessor` for formats the 3.8.6.1 builder accepts | Converted. Others ManualWork. |
| `ToolkitModel` | `.mgcb` entry with `FbxImporter`/`ModelProcessor` (and `XImporter` for `.x`) | Converted for those formats. Others ManualWork. |
| `ToolkitAudio` | `.mgcb` entry with `WavImporter`/`SoundEffectProcessor` for `.wav` | Converted. Others ManualWork. |
| `ToolkitFxc` | Copy of the `.fx` | ManualWork (SDX013) |
| `.tkb` with no source | Copy | Prebuilt (SDX015) |

The importer pairs come from `rules/importer-map.json`, loaded from Core as today. If a Toolkit asset type needs a pair that is missing, task 9.3 adds it to that file and the XNA tests must stay green.

### TkFont to spritefont

`TkFontConverter` (pure function, text in, text out) maps:

| `TkFont` | `.spritefont` (`FontDescription`) |
|---|---|
| `FontName` | `FontName` |
| `Size` | `Size` |
| `Spacing` | `Spacing` |
| `UseKerning` | `UseKerning` |
| `Style` | `Style` |
| `CharacterRegions/CharacterRegion/Start`, `End` (numbers) | `CharacterRegions/CharacterRegion/Start`, `End` as characters (`&#32;`) |
| `DefaultCharacter` (number) | `DefaultCharacter` as a character |
| `LineSpacing`, `Format`, `NoPremultiply`, `AntiAlias` | no field in MonoGame. Dropped, and each non-default value is written to the content report. |

`LineSpacing` and `NoPremultiply` can change how text looks, so a non-default value becomes a Warning in the report, not a silent drop. The existing `FontChecker` then checks the family against `xna-only-fonts.json`, because a Toolkit font on a machine without the family fails the same way an XNA font does.

### Asset names

The Toolkit appends `.tkb` to a name with no extension, so `Content.Load<Texture2D>("Balls")` and the source file `Content\Balls.dds` line up. MonoGame also loads without an extension, and `AssetNameResolver` already makes the asset name from the relative path without extension. So a plain name keeps working. A name written with an extension (`Load<Texture2D>("Balls.dds")`) does not. The converter records every source asset name with its extension in the report so the person can find them. Rewriting string literals is out of scope.

## Runner and repair

`HintCatalog` is data. Add entries that map common errors after migration to SDX rules:

| Build message contains | Hint |
|---|---|
| `The type or namespace name 'RectangleF'` | SDX034: `RectangleF` has no MonoGame type. Use `Rectangle`, or a small struct of your own. |
| `'Color4'` | SDX035 |
| `'GameSystem'` | SDX062 |
| `'GeometricPrimitive'` | SDX048 |
| `SharpDX.Direct3D11` | SDX005 |
| `does not contain a definition for 'SetResource'` | SDX049 |
| Missing generated type that was an effect wrapper | SDX013 |

No AI repair changes. The repair project stays optional and off by default. Nothing in this spec uploads code.

## UI

The report has `SourceFrameworks`. The studio shows it as a label next to the project name through the existing report binding. No UI logic is added. The target picker keeps both options, with WindowsDX preselected by the migration options default, not by the view.

## Fixtures

All fixtures are original and synthetic, like the XNA fixtures. They go in `tests/fixtures/`:

| Folder | Purpose |
|---|---|
| `sdx-basic` | Toolkit game: `.csproj` with SharpDX references and imports, `ToolkitFont` and `ToolkitTexture` items, `Game1 : Game`, `SpriteBatch`, `SpriteFont`, `Content.Load`, `GraphicsDeviceManager`, `Matrix.LookAtRH`, `MathUtil`, state collections, `KeyboardManager` |
| `sdx-effects` | Adds a `ToolkitFxc` item and a Toolkit-syntax `.fx` |
| `sdx-lowlevel` | Uses `SharpDX.Direct3D11`, `RectangleF`, `Color4`, LH matrices, `GeometricPrimitive` |
| `sdx-mixed` | A solution with one Toolkit project and one XNA project |
| `sdx-store-app` | Windows Store project type GUID (SDX004) |
| `sdx-prebuilt` | `.tkb` files with no source |
| `sdx-negative` | Plain SharpDX (no Toolkit) and a MonoGame project that uses `Matrix.CreateLookAt`, `BlendState.Opaque`, `Keyboard.GetState()`. No SDX rule may fire. |

Each rule has one positive test (the rule fires, with the expected file and line) and one negative test (a look-alike that must not fire), per the project rules. The snapshot reports get a Verify snapshot per fixture. Rewriters get before/after tests plus a "second run changes nothing" test.

## Test candidates (read only)

Found with GitHub code search for `SharpDX.Toolkit.Game` and `ToolkitFont`, then checked by reading each project file and the license. Use shallow clones outside the repo. Do not copy code or assets into the repo.

| Repo | License | Why it is useful | Checked |
|---|---|---|---|
| [zlynn1990/EcoSim](https://github.com/zlynn1990/EcoSim) | MIT | Small desktop sim on Toolkit 2.6.2. `Game`, `SpriteBatch`, `ToolkitTexture`, `ToolkitFont`. Uses `Matrix.Translation`, `Matrix.Scaling`, `MathUtil.Lerp`, `RectangleF`, `BlendStates`, `KeyboardManager`/`MouseManager` states. Plus a ProjectReference to an OpenCL library. | Project file read; source usage counted. |
| [nuclearpidgeon/graphicsproj2](https://github.com/nuclearpidgeon/graphicsproj2) | MIT | University 3D project: `ToolkitFxc`, `ToolkitFont`, `ToolkitModel`, `ToolkitTexture`, `.fbx`, `.dds`, 2 `.fx`. Good for the effect, model and handedness rules. | Project file read. |
| [Luaancz/Yggmire](https://github.com/Luaancz/Yggmire) | MIT | Larger multi-project solution. The `SharpClient` project has all four content actions and 4 `.fx`. Tests multi-project and mixed output (the other projects are not Toolkit). | Project files read. |
| [kobush/ModernMoleculeViewer](https://github.com/kobush/ModernMoleculeViewer) | MIT | Toolkit + `ToolkitFxc`, but the projects are Windows 8.1 and Windows 10 app projects. Use it as the negative case for SDX004 (should be Unsupported), not as a migration target. | Project files read. |

Not used: `RobJellinghaus/Holofunk` (MIT) turned out to use FNA, not the Toolkit. `jburdett/AirHockeyGame`, `Mubashwer/BallDash`, `equadon/pacmandx` and others use the Toolkit but have no license, so they are excluded under the licensing rule.

The Toolkit repository itself (MIT) also has sample content (`Bloom.fx`, `Arial16.xml`) that was read for syntax. Nothing from it is copied.

What was not checked: that any candidate builds today, that its NuGet packages restore, or that its content compiles. Task 13 does that. The candidates are on .NET Framework 4.5 and Visual Studio-era project files, so some may not open cleanly.

## Error handling

- A Toolkit project that also contains XNA references: run both catalogs. Findings keep their own rule ids. The plan item ids are unique across catalogs (`<rule id>:<n>`).
- A catalog that fails validation is never partly loaded (existing behavior). A broken Toolkit catalog must not break XNA analysis: the analyzer reports `ANA` diagnostic and carries on with the XNA catalog.
- A rewriter that throws is isolated by the engine (existing behavior), and the file is copied unchanged with a marker.

## Risks and open questions

1. Syntax-only matching is the largest risk. `Begin`, `Draw`, `Model`, and `Scaling` are common names. Receivers and namespace gates reduce it, and the explanations say so. Task 13 counts false positives on the candidates.
2. Effects are the real wall. Most 3D Toolkit games depend on them, and SDX013 is Manual. A later spec could translate the common Toolkit effect forms. Not in scope.
3. Handedness and winding. A game that sets `RasterizerStates.CullBack` may look inverted after migration. The rewriter marks it, and the report lists the rule under "visual risk".
4. Premultiplied alpha. The Toolkit texture path and MonoGame's `TextureProcessor` can differ on premultiplication. Not verified. The content report notes it for fonts (`NoPremultiply`) and textures.
5. WindowsDX for a Toolkit project is a default chosen from the build model, not tested. Task 13 builds the EcoSim copy with both targets.
6. Whether the shared migrator `Shims` project should provide `RectangleF`, `Color4` stand-ins (like the Storage shims) is open. Task 14 is a stretch task for that. Do not start it before task 13 shows how often these types appear.
7. SharpDX 3.x and later: the Toolkit never shipped a 3.x NuGet package, so a project on `SharpDX` 3.x with a hand-built Toolkit is possible but rare. Detection works on names, not versions. The rules were written against 2.6.3 sources; any difference found on a candidate becomes a new rule.

## Implementation notes

Written as the work lands. Gaps and deviations are recorded here.

### Tasks 1 to 3 (fixtures, detection, schema and engine)

- Fixtures use ASCII placeholders for `.dds` and `.tkb` bytes, like the XNA fixtures use for `.png`, so the repo stays text-only. Tasks that need real bytes (9.3) write them at test time.
- `sdx-negative` holds two projects: plain SharpDX, and an SDK-style MonoGame game. The MonoGame project classifies as `MonoGameLegacy` (3.8.4 pin) after the `monogame-upgrade` merge, so the report for this folder is not "nothing found". The negative proof is that no project is a Toolkit project and no SDX rule fires. `sdx-mixed` embeds a copy of the `xna40-basic` project as its XNA half.
- Deviation from design "Detection": the sibling `monogame-upgrade` spec had already merged a single `ProjectFamily` enum on `DiscoveredProject` and `ProjectInfo` and an `appliesTo` string on the catalog. This work builds on that instead of adding `IsSharpDxToolkit` plus `SourceFrameworks` next to it. `ProjectFamily.SharpDxToolkit` is the new value, `DiscoveredProject.IsSharpDxToolkit` and `DiscoveryResult.MigratableProjects` (XNA or Toolkit) are derived, and the report carries the framework as the `family` property that the MonoGame work already writes (omitted for XNA, so XNA reports are unchanged).
- Consequence: a project with both XNA and Toolkit signals is one project with one family. It classifies as `SharpDxToolkit` and gets diagnostic `ANA020`. The design sentence "run both catalogs on a Toolkit project that also contains XNA references" is therefore not implemented. Mixed solutions (separate projects) work. Revisit only if a real candidate needs the hybrid.
- `appliesTo` accepts `"SharpDxToolkit"` (as written in this spec) next to the MonoGame work's lowercase `"xna"` and `"monogame"`. The mixed casing is deliberate to keep the spec text and the merged schema both valid.
- `member` match: `name` became nullable and `names` was added (`MemberMatch.AllNames`). The schema requires `type` and `namespaces` and the validator checks exactly one of `name` or `names`, because a JSON-schema `oneOf` there produces unreadable errors.
- `ProjectAnalyzer.AnalyzeAsync(path, catalogs, catalogDiagnostics, ...)` is new so the app can pass a "Toolkit catalog skipped" diagnostic (ANA021) into the report. The existing overloads delegate to it.
- The XNA-catalog snapshot tests do not cover the Toolkit fixtures. They are in `FixturePaths.SharpDxNames` and `EveryNameRows` only. Toolkit snapshots with the real SDX catalog arrive with task 4.
- Task 3.2 was finished with task 4 (see below), because the app wiring needs the catalog file.

### Task 4 (rule catalog)

- `rules/sharpdx-toolkit-to-monogame.json` has the 45 rules of the design table (20 AutoFixable, 18 Manual, 7 Unsupported). `tests/XnaReborn.Tests/Sdx/sdx-rule-cases.json` holds one positive and one negative snippet per rule. Every case is a Toolkit project (a `packages.config` with a Toolkit package is the only Toolkit signal, which no rule reads).
- `AnalysisService` loads the XNA catalog (required) and then the Toolkit catalog. A missing or invalid Toolkit catalog adds diagnostic `ANA021` to the report and leaves XNA analysis alone. The App project copies the Toolkit catalog next to the app.
- SDX004: the two GUID prefixes were checked against `ProjectTemplate.csproj` in the Toolkit repository: WinRT `{BC8A1FFA-BEE3-4634-8014-F334798102B3}` and Windows Phone 8 `{C089C8C0-30E0-4E22-80C0-CE093F111A43}`. The desktop template sets no `ProjectTypeGuids`.
- SDX060 (`ToDisposeContent`) is a `type` match, not a `member` match as the design table says. The call is almost always a bare call of an inherited method, and the member matcher only sees `a.b` calls. Changing the member matcher to see bare calls would change XNA rules. A `ToDisposeContent` written as `this.ToDisposeContent(...)` is not found (known limit).
- SDX030 uses `kind: Invocation` so `Matrix.Translation` as a property never matches. SDX040 and SDX044 use `kind: Access`.
- SDX035 also lists `Color3`, SDX062 also lists `GameSystemCollection`, and SDX063 also lists `GameContext`, through `alsoNames`, as the design text for those rows describes.
- SDX051 (`spriteBatch.Begin` with exactly two arguments) also finds `Begin(SpriteSortMode, BlendState)`, which MonoGame accepts. The match cannot see argument types. The explanation says so. Task 13 counts how often this is a false positive.
- Source rule explanations end with a sentence that matching is by syntax only (R2.8). A test checks it.
- Known limit: member rules gate on `using` directives. A file that uses `GraphicsDevice` without importing `SharpDX.Toolkit` or `SharpDX.Toolkit.Graphics` is not matched.
- Snapshots: `sdx-negative` reports "Found 1 MonoGame project(s), but no rule catalog for them was loaded", because the MonoGame catalog is not part of the Toolkit snapshot test. No SDX rule fires.

### Tasks 5 to 7 (rename map and source rewriters)

- `rules/sharpdx-toolkit-map.json` is embedded in Core and loaded by `ToolkitMap` (like the importer map). A test compiles every MonoGame name in it against the MonoGame.Framework.DesktopGL 3.8.6.1 assembly from the NuGet cache (`MonoGameFact`, skipped when the package is not cached). All names exist. Left out on purpose and marked instead: `BlendStates.Default`, `SamplerStates.Default`, `SamplerStates.LinearMirror` and `PointMirror`, and every `RasterizerStates` member.
- The registry is keyed by rule ID, so a rewriter that serves five rules (the namespace rewriter, the graphics rewriter) is registered once per rule ID: `ToolkitNamespaceRewriter` for SDX020 to SDX024, `ToolkitGraphicsRewriter` for SDX040, SDX041, SDX042, SDX044 and SDX060, `ToolkitMathRewriter` for SDX030 and SDX031, `ToolkitInputRewriter` for SDX025. `ToolkitMarkerRewriter` covers the 22 Manual and Unsupported source rules (SDX005 to SDX008 reuse `NamespaceMarkerRewriter`). A test checks that every source rule of the catalog has a rewriter.
- The engine runs rewriters in rule ID order, so the namespace rewriters run first. The later rewriters and the markers cannot ask for the Toolkit `using` any more, because it is gone. They gate on "the file imports or names a SharpDX or Microsoft.Xna.Framework namespace" and on the exact type name or receiver. A user class with a `Matrix.Scaling` method in a file that also imports MonoGame would be renamed. That is a known limit.
- `SetRenderTargets` with one argument is renamed to `SetRenderTarget`. A one-argument call that passes an array is not detected.
- SDX060: a bare `ToDisposeContent(x);` statement of a variable is removed (a lone `x;` is not valid C#). Every ToDisposeContent rewrite carries the disposal-change note ("MonoGame does not, so dispose it yourself").
- Code in an inactive `#if` branch is not rewritten, because the files are parsed with the original project's symbols (same as the other rewriters). The migrated project defines no `NETFX_CORE`, so that code stays dead.
- Task 7.2: `sdx-basic` goes through every SDX rewriter and then compiles with Roslyn against MonoGame 3.8.6.1 with no errors (`SdxMarkerTests`). The `ProjectMigrator` does not run these rewriters on a Toolkit project yet (it still stops when there is no XNA project). That is task 8.2 and task 10.
- Not enabled by the migrator yet: the marker rewriters need to be enabled by their findings, like the device markers (`_markerRuleIds` in `ProjectMigrator`). Task 8.2 adds the Toolkit rule IDs there.
