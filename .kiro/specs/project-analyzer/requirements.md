# Requirements: Project Analyzer

## Introduction

The analyzer reads an old XNA project and produces a compatibility report: what the project is, what will carry over to MonoGame, and what will not. It is strictly read-only. Every finding comes from a rule in the JSON catalog (`rules/xna-to-monogame.json`), so new detections need data, not code.

## Glossary

- **Project root**: the folder, `.sln`, or `.csproj` the user opened.
- **XNA project**: a `.csproj` that imports the XNA Game Studio targets, sets an XNA framework version, or references `Microsoft.Xna.Framework*`.
- **Rule**: one entry in the catalog. It has an id, severity, classification, match, explanation, and resolution.
- **Finding**: one place where a rule matched, with file and line when the match is in a file.
- **Classification**: `AutoFixable` (the migrator can fix it), `Manual` (a person must fix it), `Unsupported` (no modern equivalent).
- **Severity**: `Info`, `Warning`, `Error`, `Blocker`.
- **Diagnostic**: a problem in the analysis itself (unreadable file, parse failure). Not a finding about the user's project.

## Requirements

### R1: Project discovery
**User story:** As a developer, I want to point at a folder, solution, or project and have my XNA projects found.

Acceptance criteria:
1. WHEN given a `.csproj`, THE analyzer SHALL analyze that project.
2. WHEN given a `.sln`, THE analyzer SHALL analyze every project listed in it that exists on disk.
3. WHEN given a folder, THE analyzer SHALL search it recursively for `.sln` and `.csproj` files, skipping `bin`, `obj`, `.git`, and `.vs` folders.
4. THE analyzer SHALL identify XNA projects by their XNA targets import, XNA version property, or XNA framework references.
5. IF no XNA project is found, THEN THE analyzer SHALL return a result that says why (for example "no .csproj found", "projects found but none use XNA") instead of an empty report.
6. THE analyzer SHALL return projects in a stable order (by relative path).

### R2: Project file reading
**User story:** As a developer, I want my project file understood accurately.

Acceptance criteria:
1. THE analyzer SHALL read `.csproj` and `.contentproj` files as XML and SHALL NOT evaluate or execute MSBuild targets.
2. THE analyzer SHALL extract: project type, assembly name, target framework version, XNA platform, XNA profile, define constants, references, compile items, content items, nested content project items, and imports.
3. WHEN a file cannot be parsed, THE analyzer SHALL add a Diagnostic and continue with the other projects.
4. THE analyzer SHALL read each content project referenced by a game project, including nested ones, and extract each item's source path, name override, importer, processor, and processor parameters.

### R3: XNA version detection
**User story:** As a developer, I want to know which XNA version my project targets.

Acceptance criteria:
1. THE analyzer SHALL detect XNA 3.0, 3.1, and 4.0 from the XNA version property, the framework reference versions, and the targets import path.
2. WHEN signals disagree, THE analyzer SHALL report the most specific version and add a Diagnostic that lists the conflict.
3. WHEN no signal is found, THE analyzer SHALL report `Unknown` and SHALL NOT guess.
4. THE analyzer SHALL report the XNA platform (Windows, Xbox 360, Zune, Windows Phone) and profile (Reach, HiDef) when present.

### R4: Rule catalog
**User story:** As a maintainer, I want rules to be data.

Acceptance criteria:
1. THE analyzer SHALL load rules from `rules/xna-to-monogame.json` and validate them against `RuleSchema.json`.
2. IF the catalog is invalid, THEN THE analyzer SHALL fail with a message that names the rule id and the problem, and SHALL NOT run a partial analysis.
3. THE analyzer SHALL reject duplicate rule ids.
4. THE analyzer SHALL require a `migratorTask` link on every `AutoFixable` rule.
5. THE analyzer SHALL support these match types: `reference`, `namespace`, `type`, `member`, `projectProperty`, `projectItem`, `filePattern`, and `contentItem`.
6. THE analyzer SHALL work with an empty catalog and return a report with no findings.

### R5: Source scanning
**User story:** As a developer, I want API usage found with exact locations.

Acceptance criteria:
1. THE analyzer SHALL parse every `.cs` file listed as a compile item with Roslyn, using the project's define constants.
2. THE analyzer SHALL evaluate `namespace`, `type`, and `member` rules against the syntax tree and report file, line, column, and the source line as a snippet.
3. THE analyzer SHALL match by syntax only. It SHALL NOT require XNA assemblies to be installed.
4. WHEN a `.cs` file cannot be read or has parse errors, THE analyzer SHALL add a Diagnostic and still scan what parsed.
5. WHEN a compile item points to a missing file, THE analyzer SHALL add a Diagnostic.
6. THE analyzer SHALL skip files larger than 5 MB and add a Diagnostic.

### R6: Findings and classification
**User story:** As a developer, I want every problem to say how serious it is and who must fix it.

Acceptance criteria:
1. EACH finding SHALL carry: rule id, title, severity, classification, project, file (relative, forward slashes), line, column, and snippet when the match is in a source file.
2. THE analyzer SHALL report a finding for each match location, and for each match of a project-level rule (one per project).
3. THE analyzer SHALL sort findings by severity (highest first), then rule id, then file, then line, so output is deterministic.
4. THE report SHALL include, for each rule that fired, its explanation and resolution.

### R7: Compatibility score
**User story:** As a presenter, I want one number that shows improvement.

Acceptance criteria:
1. THE analyzer SHALL compute a score from 0 to 100 from the findings, using the severity weights and classification factors in the catalog's `scoring` section.
2. EACH distinct rule that fired SHALL be counted once, so a rule that matches 200 times does not sink the score.
3. THE analyzer SHALL also compute a projected score that treats `AutoFixable` findings as resolved.
4. A project with no findings SHALL score 100.
5. THE score SHALL be an integer, clamped to 0 to 100, and identical for identical input.

### R8: Reports
**User story:** As a developer, I want to save and share the report.

Acceptance criteria:
1. THE analyzer SHALL produce a structured report object with: schema version, catalog name, projects (with version, platform, profile), findings, triggered rules, counts by severity and classification, scores, and diagnostics.
2. THE analyzer SHALL export the report as JSON and as Markdown.
3. THE exports SHALL be deterministic: no timestamps, no absolute paths, stable ordering.
4. THE JSON export SHALL include a `schemaVersion` field.

### R9: Read-only guarantee
**User story:** As a user, I want to know my project is never touched.

Acceptance criteria:
1. THE analyzer SHALL open every file with read access only and SHALL NOT create, modify, rename, or delete anything under the project root.
2. THE analyzer SHALL NOT write temporary files next to the project.
3. A test SHALL hash every file under each fixture before and after analysis and assert that nothing changed and nothing was added.
4. A test SHALL scan the analyzer's source for write APIs (`File.Write*`, `File.Delete`, `FileMode.Create`, `FileAccess.Write`, and similar) and fail when it finds one.

### R10: Responsiveness and cancellation
**User story:** As a user, I want large projects to analyze without freezing the app.

Acceptance criteria:
1. THE analyzer SHALL expose an async entry point that takes a `CancellationToken`.
2. THE analyzer SHALL report progress through `IProgress<AnalysisProgress>`.
3. WHEN cancelled, THE analyzer SHALL stop promptly and throw `OperationCanceledException`.
4. THE analyzer SHALL scan source files in parallel and still produce deterministic output.
