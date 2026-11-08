# Design: Content Conversion (C# / .NET 8)

## Overview

Content conversion lives inside `XnaReborn.Migrator` as a `Content` module and reuses the analyzer's `ContentProjectReader`. It reads the XNA content project, resolves asset names, maps importers and processors, writes `Content.mgcb`, and copies assets. It produces a `ContentReport` that goes into the manifest.

## Layout

```
src/XnaReborn.Migrator/Content/
├── ContentConverter.cs         # entry point
├── AssetNameResolver.cs
├── ImporterMap.cs              # data-driven mapping
├── ProcessorParamMapper.cs
├── MgcbWriter.cs
├── FontChecker.cs
├── AssetCopier.cs
├── ContentReport.cs
└── data/
    ├── importer-map.json
    └── xna-only-fonts.json
```

## XNA content project shape

Items in a `.contentproj` look like:

```xml
<ItemGroup>
  <Compile Include="Textures\hero.png">
    <Name>hero</Name>
    <Importer>TextureImporter</Importer>
    <Processor>TextureProcessor</Processor>
    <ProcessorParameters_PremultiplyAlpha>True</ProcessorParameters_PremultiplyAlpha>
  </Compile>
</ItemGroup>
```

Nested projects are referenced from the game project through `NestedContentProject` items. Custom pipeline assemblies appear as references inside the content project. The reader extracts all of this; the converter does not parse XML itself.

## Core types

```csharp
public sealed record ContentItem(
    string SourcePath,            // relative to content root
    string? NameOverride,
    string? Importer,
    string? Processor,
    IReadOnlyDictionary<string, string> ProcessorParams);

public sealed record ConvertedAsset(
    ContentItem Item,
    string AssetName,
    string MonoGameImporter,
    string MonoGameProcessor,
    AssetStatus Status,           // Converted | ManualWork | Missing | Conflict | Prebuilt
    string? Note);

public sealed record ContentReport(
    IReadOnlyList<ConvertedAsset> Assets,
    IReadOnlyDictionary<string,int> CountsByType,
    IReadOnlyList<Finding> Findings);
```

## Conversion pipeline

`ContentConverter.ConvertAsync(sourceProject, report, options)`

1. **Read** all content projects with `ContentProjectReader`.
2. **Resolve names** with `AssetNameResolver`; detect conflicts.
3. **Classify each item**:
   - Standard importer/processor pair in `importer-map.json` → convertible.
   - Custom importer/processor (not in map) → ManualWork.
   - XACT extensions → ManualWork.
   - Source file missing → Missing.
4. **Fonts**: `FontChecker` reads the family from each `.spritefont` and checks it against `xna-only-fonts.json`.
5. **Write** `Content.mgcb` with `MgcbWriter`, ordered by asset name.
6. **Copy** all source assets with `AssetCopier`, preserving relative paths.
7. **Detect prebuilt-only** `.xnb` files and add them to the report.
8. **Return** `FileChange` records plus the `ContentReport`; nothing is written until the migrator's write step.

## MGCB output

```
#----------------------------- Global Properties ----------------------------#

/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:
/profile:HiDef
/compress:False

#-------------------------------- References --------------------------------#


#---------------------------------- Content ---------------------------------#

#begin Textures/hero.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:PremultiplyAlpha=True
/build:Textures/hero.png
```

Rules:
- `/platform` follows the selected MonoGame target (DesktopGL or Windows).
- `/profile` is mapped from the XNA graphics profile (Reach or HiDef) and defaults to HiDef when unknown.
- When the asset name differs from the path, the build line uses the `source;destination` form so `Content.Load<T>(name)` still resolves.
- Entries are sorted by asset name for deterministic output. Exact formatting should be verified against the current MonoGame content builder version during implementation.

## Importer and processor map (data)

```json
{
  "version": 1,
  "pairs": [
    { "importer": "TextureImporter", "processor": "TextureProcessor" },
    { "importer": "FontDescriptionImporter", "processor": "FontDescriptionProcessor" },
    { "importer": "FbxImporter", "processor": "ModelProcessor" },
    { "importer": "XImporter", "processor": "ModelProcessor" },
    { "importer": "WavImporter", "processor": "SoundEffectProcessor" },
    { "importer": "Mp3Importer", "processor": "SongProcessor" },
    { "importer": "WmaImporter", "processor": "SongProcessor" },
    { "importer": "EffectImporter", "processor": "EffectProcessor", "note": "shader review" },
    { "importer": "XmlImporter", "processor": "PassThroughProcessor" }
  ]
}
```

New standard pairs are added in the data file with a test; anything not listed becomes manual work.

## Fonts

XNA Game Studio shipped a set of fonts that modern systems do not have (for example Kootenay, Lindsey, Miramonte, Pericles, Pescadero, and Segoe UI Mono). A `.spritefont` that names one of these builds fine in XNA but fails in MonoGame on a clean machine. `xna-only-fonts.json` holds the list (confirm its contents against the XNA documentation when you implement this). `FontChecker`:
1. Reads the `<FontName>` element.
2. If listed, adds Warning finding `XNA013` with a suggested substitute.
3. If font substitution is enabled in options, rewrites the `.spritefont` copy (never the original) to the chosen substitute and records the change.

## Audio and effects

- WAV, MP3, WMA go through standard mappings.
- XACT files are copied unchanged and listed as manual work, with a note that audio must be re-exported as plain assets.
- `.fx` files are included in the `.mgcb` using `EffectImporter`/`EffectProcessor` and flagged for shader review. Shader model differences can make them fail to compile; the content build error will be mapped to a hint by the runner.

## Prebuilt `.xnb`

`.xnb` files without sources are copied to the migrated copy and listed as Prebuilt. The report states that they cannot be rebuilt and that loading them is not guaranteed.

## Error handling

- Per-item failures are caught; the asset is copied, marked ManualWork, and reported.
- Missing files are reported, not fatal.
- Conflicts are reported with both items listed.

## Testing strategy

1. **Reader tests** using fixture `.contentproj` files including nested projects.
2. **Mapping tests**: every pair in the map, plus a custom pair that must become manual work.
3. **Name tests**: path-based names, name overrides, conflicts.
4. **MGCB snapshots** via Verify for each fixture.
5. **Font tests**: XNA-only font flagged, system font not flagged, substitution rewrites only the migrated copy.
6. **Prebuilt tests**: xnb-only fixture produces Prebuilt entries and the limitation note.
7. **Determinism test**: byte-identical output on repeated runs.
8. **Integration test (slow)**: run the content build on the simplest fixture through the runner.
