# Requirements: Content Conversion

## Introduction

Content conversion turns an XNA content project (`.contentproj`) and its source assets into a MonoGame content pipeline (`Content.mgcb`) inside the migrated copy. It is the riskiest part of migration, so it prefers honesty over guessing: anything it cannot convert with confidence is copied, flagged, and listed as manual work.

## Glossary

- **Content project**: the XNA `.contentproj` that lists assets, importers, and processors.
- **Asset name**: the name the game uses to load an asset (path without extension, unless overridden).
- **MGCB file**: the MonoGame content builder description file (`Content.mgcb`).
- **Importer / Processor**: the pipeline stages that read a source asset and produce a built asset.
- **Prebuilt asset**: a compiled `.xnb` file with no source asset available.

## Requirements

### R1: Content project reading
**User story:** As a developer, I want my content project understood accurately.

Acceptance criteria:
1. THE converter SHALL read each content project referenced by the game project, including nested content project references.
2. THE converter SHALL extract each item's source path, asset name override, importer, processor, and processor parameters.
3. THE converter SHALL read the content root directory and the graphics profile where present.
4. THE converter SHALL read references to custom pipeline assemblies.
5. WHEN a content project cannot be parsed, THE converter SHALL report it as a finding and continue with other projects.

### R2: Asset names
**User story:** As a developer, I want `Content.Load` calls to keep working.

Acceptance criteria:
1. THE converter SHALL preserve each asset's loadable name, using the item's name override when present and its relative path without extension otherwise.
2. THE converter SHALL preserve the content root directory so existing `Content.RootDirectory` assignments remain valid.
3. IF two assets resolve to the same asset name, THEN THE converter SHALL report the conflict and SHALL NOT silently drop either asset.

### R3: MGCB generation
**User story:** As a developer, I want a working MonoGame content file generated.

Acceptance criteria:
1. THE converter SHALL generate a `Content.mgcb` with global properties (platform, profile mapped from the XNA graphics profile, compression setting) matching the selected MonoGame target.
2. THE converter SHALL write one entry per convertible asset with its importer, processor, mapped processor parameters, and asset name.
3. THE converter SHALL map standard XNA importers and processors to their MonoGame equivalents using a data table.
4. THE converter SHALL write entries in a deterministic order.
5. THE converter SHALL write the generated file into the migrated copy and never modify the original.

### R4: Asset copying
**User story:** As a developer, I want no asset lost.

Acceptance criteria:
1. THE converter SHALL copy every source asset into the migrated copy, preserving relative paths.
2. THE converter SHALL copy assets that it cannot convert and mark them as manual work.
3. THE converter SHALL report any asset referenced by the content project but missing on disk.

### R5: Fonts
**User story:** As a developer, I want sprite fonts to work or be flagged clearly.

Acceptance criteria:
1. THE converter SHALL convert `.spritefont` items using the font description importer and processor.
2. THE converter SHALL compare each font family against a list of fonts that shipped only with XNA Game Studio and are not normally installed on modern systems.
3. WHEN a font family is on that list or cannot be found on the build machine, THE converter SHALL add a Warning finding that explains the problem and suggests a substitute font.
4. THE converter SHALL NOT change a font family automatically unless the user enables font substitution.

### R6: Audio
**User story:** As a developer, I want standard audio to convert and complex audio to be flagged.

Acceptance criteria:
1. THE converter SHALL convert `.wav`, `.mp3`, and `.wma` assets using the matching MonoGame importers and processors.
2. THE converter SHALL flag XACT projects and banks (`.xap`, `.xgs`, `.xwb`, `.xsb`) as manual work and SHALL copy them unchanged.

### R7: Models, textures, and effects
**User story:** As a developer, I want visual assets handled predictably.

Acceptance criteria:
1. THE converter SHALL convert textures, `.fbx`, and `.x` models using standard importers and processors with their parameters.
2. THE converter SHALL flag items that use custom importers or processors (including skinned-model processors) as manual work.
3. THE converter SHALL flag `.fx` effects as requiring shader review and SHALL still include them in the generated file.

### R8: Prebuilt assets
**User story:** As a developer, I want to know when assets cannot be rebuilt.

Acceptance criteria:
1. WHEN a project contains `.xnb` files and no corresponding source assets, THE converter SHALL list them as prebuilt, copy them, and report that they cannot be rebuilt.
2. THE converter SHALL NOT claim that prebuilt assets will load correctly.

### R9: Content report
**User story:** As a developer, I want a clear summary of conversion.

Acceptance criteria:
1. THE converter SHALL produce a report with counts of assets converted, copied as manual work, missing, and conflicting, grouped by type.
2. THE converter SHALL include the report in the migration manifest.
3. THE converter SHALL expose the report as structured data for the UI.

### R10: Determinism and verification
**User story:** As a maintainer, I want predictable output.

Acceptance criteria:
1. GIVEN identical input and options, THE converter SHALL produce byte-identical `Content.mgcb` output.
2. WHEN requested, THE converter SHALL hand the result to the build-and-run component so that the content build is verified, and SHALL attach content build errors with the asset path.
