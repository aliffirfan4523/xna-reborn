# Design: Project Migrator (C# / .NET 8)

## Overview

`XnaReborn.Migrator` consumes an `AnalysisReport` and the source project path and writes a migrated copy. It is built from small steps that each produce a list of `FileChange` records, so the UI can preview, exclude, and diff changes before anything is written.

## Project layout

```
src/XnaReborn.Migrator/
├── Planning/
│   ├── MigrationPlanner.cs
│   └── MigrationPlan.cs
├── Projects/
│   ├── SdkCsprojGenerator.cs
│   ├── SolutionGenerator.cs
│   └── PackageSelector.cs        # DesktopGL vs WindowsDX
├── Source/
│   ├── RewriterRegistry.cs
│   ├── Rewriters/
│   │   ├── SpriteBatchBeginRewriter.cs
│   │   ├── RenderStateRewriter.cs
│   │   ├── EffectPassRewriter.cs
│   │   ├── EntryPointRewriter.cs
│   │   └── MarkerInserter.cs     # adds // XNAREBORN: comments
│   └── DiffBuilder.cs
├── Content/
│   ├── MgcbGenerator.cs
│   ├── ImporterMap.cs            # XNA -> MonoGame importer/processor names
│   └── AssetCopier.cs
├── Shims/
│   └── ShimProjectGenerator.cs   # stretch
├── Output/
│   ├── OutputGuard.cs            # folder safety checks
│   ├── ManifestWriter.cs
│   └── FileChange.cs
└── ProjectMigrator.cs            # entry point
```

## Key dependencies

| Package | Use |
|---|---|
| `Microsoft.CodeAnalysis.CSharp` + `.Workspaces` | Roslyn syntax rewriting and formatting |
| `System.Xml.Linq` | Reading old project files, writing new ones |
| `DiffPlex` | Unified and side-by-side diffs |
| `System.Text.Json` | Manifest |
| `XnaReborn.Analyzer` | Findings and project info |
| `xUnit`, `Verify.Xunit` | Tests and snapshots |

## Core types

```csharp
public sealed record MigrationOptions(
    string OutputFolder,
    MonoGameTarget Target,          // DesktopGL | WindowsDX
    bool EnableShims,
    bool DryRun,
    IReadOnlySet<string> ExcludedPlanItemIds);

public sealed record PlanItem(
    string Id,
    string RuleId,
    string Description,
    IReadOnlyList<string> AffectedFiles);

public sealed record FileChange(
    string RelativePath,
    ChangeKind Kind,               // Created | Modified | Copied | Skipped | ManualWork
    string? OriginalText,
    string? NewText,
    string? Reason);

public sealed record MigrationResult(
    MigrationPlan Plan,
    IReadOnlyList<FileChange> Changes,
    Manifest Manifest,
    bool Completed);
```

## Migration pipeline

`ProjectMigrator.MigrateAsync(source, report, options, progress, ct)`

1. **Guard**: `OutputGuard` checks the output folder (not inside source, not non-empty unless confirmed).
2. **Plan**: `MigrationPlanner` turns AutoFixable findings into plan items and applies exclusions.
3. **Dry run exit**: if `DryRun`, return the plan and computed changes without writing.
4. **Project files**: generate SDK-style `.csproj` and `.sln`.
5. **Source**: copy `.cs` files, then run each applicable rewriter over its syntax tree. Untouched files are copied byte for byte.
6. **Content**: generate `Content.mgcb`, copy source assets, record manual items.
7. **Shims** (if enabled): add a compatibility project and rewrite references.
8. **Write**: materialize all `FileChange` records to the output folder.
9. **Manifest**: write JSON and Markdown.
10. **Verify** (optional): call the Runner to build the migrated copy and attach the result.

All work in steps 4 to 7 produces in-memory `FileChange` records. Nothing is written until step 8, which makes dry run, previews, and the diff view simple and keeps failure handling clean.

## SDK-style project generation

Target output:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{original}</AssemblyName>
    <RootNamespace>{original}</RootNamespace>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="..." />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="..." />
  </ItemGroup>
  <ItemGroup>
    <MonoGameContentReference Include="Content\Content.mgcb" />
  </ItemGroup>
</Project>
```

Nullable and implicit usings are disabled so old code compiles unchanged. Package versions are pinned in one configuration file and should be confirmed against current MonoGame releases at build time.

## Source rewriters

Each rewriter implements `ISourceRewriter { string RuleId; SyntaxNode Rewrite(SyntaxNode root, SemanticModel? model, RewriteContext ctx); }` and is registered by rule ID.

| Rewriter | Before | After |
|---|---|---|
| `SpriteBatchBeginRewriter` | `Begin(SpriteBlendMode.AlphaBlend)` | `Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend)` |
| `RenderStateRewriter` | `GraphicsDevice.RenderState.AlphaBlendEnable = true` | `GraphicsDevice.BlendState = BlendState.AlphaBlend` (with matching state objects) |
| `EffectPassRewriter` | `effect.Begin(); pass.Begin(); ... pass.End(); effect.End();` | `foreach (var pass in effect.CurrentTechnique.Passes) { pass.Apply(); ... }` |
| `EntryPointRewriter` | XNA-specific startup, including a `Program` class wrapped in `#if WINDOWS || XBOX` (the `WINDOWS` symbol no longer exists after the project is regenerated, so `Main` disappears with CS5001) | MonoGame startup with `using var game = new Game1(); game.Run();`, with the conditional wrapper removed so `Main` always compiles |
| `MarkerInserter` | any unresolved case | adds `// XNAREBORN: <rule id>: <what to do>` above the statement |

Rules for rewriters:
1. Use semantic analysis when available so only real XNA types are rewritten.
2. If the pattern is ambiguous, do nothing and insert a marker.
3. Preserve trivia (comments, whitespace) on untouched nodes.
4. Every rewriter has before/after fixture tests.

## Content conversion

`ImporterMap` holds the standard mappings (names are the same in many cases):

| XNA | MonoGame |
|---|---|
| TextureImporter / TextureProcessor | TextureImporter / TextureProcessor |
| FontDescriptionImporter / FontDescriptionProcessor | FontDescriptionImporter / FontDescriptionProcessor |
| FbxImporter, XImporter / ModelProcessor | FbxImporter, XImporter / ModelProcessor |
| WavImporter / SoundEffectProcessor | WavImporter / SoundEffectProcessor |
| Mp3Importer / SongProcessor | Mp3Importer / SongProcessor |
| EffectImporter / EffectProcessor | EffectImporter / EffectProcessor (needs shader review) |

`MgcbGenerator` writes entries in the `.mgcb` format:

```
#begin Textures/hero.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:PremultiplyAlpha=True
/build:Textures/hero.png
```

Custom processors, XACT, and prebuilt-only `.xnb` projects go to the manifest as manual work. The asset is copied regardless so nothing is lost.

## Dead API shims (stretch)

A generated `XnaReborn.Compat` project provides:
- A local `StorageDevice`-like helper writing to the user's app data folder
- Stubs for signed-in profile and Guide calls that return fixed offline values

Shim use is always listed in the manifest. The default is off so users see the real cost of dead APIs.

## Output safety

`OutputGuard`:
1. Resolves full paths and rejects output inside the source folder (and the reverse).
2. Rejects non-empty folders without explicit confirmation.
3. Writes to a temporary folder and moves it into place on success, or leaves it marked incomplete on failure.

## Manifest

```json
{
  "version": 1,
  "migrator": "0.1.0",
  "catalogVersion": "1.0.0",
  "completed": true,
  "changes": [
    { "path": "Game1.cs", "kind": "Modified", "rules": ["XNA020"] },
    { "path": "Content/Content.mgcb", "kind": "Created" },
    { "path": "Audio/Sounds.xap", "kind": "ManualWork", "reason": "XACT project" }
  ],
  "build": { "attempted": true, "succeeded": true, "errors": [] }
}
```

## Error handling

- A failing rewriter on one file is caught, the original file is copied unchanged, and a marker plus manifest entry is added.
- A fatal error aborts before the move step, leaving the source untouched and the output marked incomplete.
- Cancellation is checked between steps and between files.

## Testing strategy

1. **Rewriter tests**: before/after fixtures for each rewriter, including ambiguous cases that must produce markers.
2. **Project generation snapshots** for each fixture project.
3. **Content tests**: generated `.mgcb` snapshots, custom processor flagged, asset copy verified.
4. **Safety tests**: output inside source, non-empty output, failure midway, source hash unchanged.
5. **Determinism test**: two runs, compare output trees by hash.
6. **End-to-end test**: migrate the simplest fixture and run `dotnet build` on the result (marked as an integration test).
