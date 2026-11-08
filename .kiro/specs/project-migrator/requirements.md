# Requirements: Project Migrator

## Introduction

The project migrator takes an analyzed XNA project and produces a migrated copy that builds and runs on MonoGame with modern .NET. It applies the AutoFixable findings from the analyzer, records everything it changes, and never touches the original project.

## Glossary

- **Source project**: the original XNA project, treated as read-only.
- **Migrated copy**: the new folder the migrator writes.
- **Migration plan**: the ordered list of changes derived from analyzer findings.
- **Manifest**: a record of every file created, changed, skipped, or left for manual work.
- **Rewriter**: a Roslyn-based source transformation tied to a rule.

## Requirements

### R1: Safe output
**User story:** As a developer, I want my original project to stay untouched.

Acceptance criteria:
1. THE migrator SHALL write only to a new output folder and SHALL NOT modify, move, or delete anything in the source project.
2. IF the output folder exists and is not empty, THEN THE migrator SHALL stop and ask for confirmation or a different folder.
3. THE migrator SHALL refuse an output folder that is inside the source project folder.
4. WHEN a migration fails partway, THE migrator SHALL leave the source project unchanged and mark the output folder as incomplete in the manifest.

### R2: Migration plan
**User story:** As a developer, I want to preview what will change before it happens.

Acceptance criteria:
1. THE migrator SHALL build a migration plan from the analysis report before writing any file.
2. THE plan SHALL list each planned change with the rule it comes from and the files it affects.
3. THE migrator SHALL support a dry-run mode that produces the plan and a diff preview without writing the migrated copy.
4. THE migrator SHALL let the caller exclude individual planned changes.

### R3: Project file conversion
**User story:** As a developer, I want the old project file replaced with a modern one.

Acceptance criteria:
1. THE migrator SHALL generate an SDK-style `.csproj` targeting .NET 8 for each code project.
2. THE migrator SHALL preserve the assembly name, root namespace, and relevant compile settings.
3. THE migrator SHALL replace XNA assembly references with the selected MonoGame package (DesktopGL or WindowsDX).
4. THE migrator SHALL add the MonoGame content builder package when the project has content.
5. THE migrator SHALL remove XNA-specific properties, project type GUIDs, and Game Studio imports.
6. WHEN the original project has multiple code projects in a solution, THE migrator SHALL generate a matching `.sln`.

### R4: Entry point and platform code
**User story:** As a developer, I want the game to start on a modern system.

Acceptance criteria:
1. THE migrator SHALL ensure the migrated project has a working entry point that creates and runs the game class.
2. WHEN the original entry point uses XNA-specific startup code, THE migrator SHALL rewrite it to the MonoGame equivalent.
3. THE migrator SHALL set the output type to a Windows executable.

### R5: Source rewriting
**User story:** As a developer, I want known breaking changes fixed automatically.

Acceptance criteria:
1. THE migrator SHALL apply rewriters only for findings classified AutoFixable and only when the corresponding plan item is not excluded.
2. THE migrator SHALL use Roslyn to rewrite code so that formatting and comments in untouched code are preserved.
3. THE migrator SHALL support XNA 3.1 to 4.0 API rewrites, including `SpriteBatch.Begin` overloads that use `SpriteBlendMode`, `RenderState` usage replaced by state objects, and `Effect.Begin`/`EffectPass.Begin` replaced by `Pass.Apply`.
4. WHEN a rewrite cannot be applied with confidence, THE migrator SHALL leave the code unchanged and add a `// XNAREBORN:` comment marking the location and the manual step needed.
5. THE migrator SHALL produce a unified diff for every changed source file.

### R6: Content conversion
**User story:** As a developer, I want my assets rebuilt for MonoGame.

Acceptance criteria:
1. THE migrator SHALL generate a `Content.mgcb` file from the old content project items.
2. THE migrator SHALL map standard XNA importers and processors to their MonoGame equivalents and copy their processor parameters.
3. THE migrator SHALL copy source assets to the migrated copy preserving relative paths.
4. WHEN an item uses a custom importer or processor, THE migrator SHALL list it in the manifest as manual work and SHALL NOT drop the asset.
5. WHEN a project contains only prebuilt `.xnb` files and no source assets, THE migrator SHALL report this limitation in the manifest.
6. THE migrator SHALL flag `.fx` effects and XACT audio as manual work in the manifest.

### R7: Dead API handling
**User story:** As a developer, I want the project to compile even when it used dead services.

Acceptance criteria:
1. WHERE shims are enabled, THE migrator SHALL add a compatibility project that provides local stand-ins for removed APIs (for example storage and signed-in profile stubs).
2. THE migrator SHALL clearly mark shim usage in the manifest.
3. WHERE shims are disabled, THE migrator SHALL leave a `// XNAREBORN:` marker at each affected call site.

### R8: Manifest and diff
**User story:** As a developer, I want a full record of what happened.

Acceptance criteria:
1. THE migrator SHALL write a manifest (JSON and Markdown) listing created, changed, skipped, and manual-work items with reasons.
2. THE manifest SHALL include the analyzer report version, catalog version, migrator version, and timestamp.
3. THE migrator SHALL expose the changes as structured data so the UI can show a side-by-side diff.

### R9: Determinism and idempotence
**User story:** As a maintainer, I want predictable output.

Acceptance criteria:
1. GIVEN the same source project, report, and options, THE migrator SHALL produce byte-identical output.
2. WHEN run twice into two different folders, THE migrator SHALL produce equivalent results.

### R10: Verification hook
**User story:** As a developer, I want to know whether the migrated copy builds.

Acceptance criteria:
1. WHEN requested, THE migrator SHALL invoke the build-and-run component on the migrated copy and include the result in the manifest.
2. THE migrator SHALL report progress events and support cancellation.
