# Design: Project Analyzer (C# / .NET 8)

## Overview

The analyzer is a pipeline: discover projects, read project files, load the rule catalog, run each rule against project data and source syntax, score, and build a report. Models live in `XnaReborn.Core`. The pipeline lives in `XnaReborn.Analyzer`. Nothing in the pipeline writes to disk.

```
path â”€â”€â–º ProjectDiscovery â”€â”€â–º ProjectReader â”€â”€â–º RuleEngine â”€â”€â–º Scorer â”€â”€â–º AnalysisReport
                               (csproj,           â–²   â–²
                                contentproj)      â”‚   â””â”€â”€ SourceScanner (Roslyn)
                                                  â””â”€â”€â”€â”€â”€â”€ RuleCatalogLoader (+ RuleSchema.json)
```

## Layout

```
src/XnaReborn.Core/
â”œâ”€â”€ Models/
â”‚   â”œâ”€â”€ Severity.cs, Classification.cs, XnaVersion.cs, XnaPlatform.cs
â”‚   â”œâ”€â”€ Finding.cs, SourceLocation.cs, Diagnostic.cs
â”‚   â”œâ”€â”€ ProjectInfo.cs, ContentItem.cs
â”‚   â”œâ”€â”€ AnalysisReport.cs, AnalysisProgress.cs
â”‚   â””â”€â”€ Rules/ (Rule.cs, RuleMatch.cs, RuleCatalog.cs, ScoringOptions.cs)
src/XnaReborn.Analyzer/
â”œâ”€â”€ ProjectAnalyzer.cs            # entry point
â”œâ”€â”€ Discovery/ProjectDiscovery.cs
â”œâ”€â”€ Reading/
â”‚   â”œâ”€â”€ ProjectReader.cs          # .csproj
â”‚   â”œâ”€â”€ ContentProjectReader.cs   # .contentproj (reused by the migrator)
â”‚   â””â”€â”€ XnaVersionDetector.cs
â”œâ”€â”€ Rules/
â”‚   â”œâ”€â”€ RuleSchema.json           # embedded resource
â”‚   â”œâ”€â”€ RuleCatalogLoader.cs      # parse + validate
â”‚   â”œâ”€â”€ RuleEngine.cs
â”‚   â””â”€â”€ Matchers/                 # one class per match type
â”œâ”€â”€ Source/SourceScanner.cs
â”œâ”€â”€ Scoring/Scorer.cs
â”œâ”€â”€ Reporting/ (JsonReportWriter.cs, MarkdownReportWriter.cs)
â””â”€â”€ IO/ReadOnlyFileSystem.cs      # the only way the analyzer touches disk
rules/
â””â”€â”€ xna-to-monogame.json
```

Models go in Core because the migrator, runner, and UI all use them. The loader and validator stay in the analyzer because only the analyzer needs schema validation.

## Core models

All are immutable records with XML docs.

```csharp
public enum Severity { Info, Warning, Error, Blocker }
public enum Classification { AutoFixable, Manual, Unsupported }
public enum XnaVersion { Unknown, V30, V31, V40, V10, V20 } // V10 and V20 were added by the older-and-vb spec; ProjectInfo also has Language (csharp or vb)
public enum XnaPlatform { Unknown, Windows, Xbox360, Zune, WindowsPhone }

public sealed record SourceLocation(string File, int Line, int Column);   // File is relative, forward slashes

public sealed record Finding(
    string RuleId, string Title, Severity Severity, Classification Classification,
    string Project,                 // relative path of the .csproj
    SourceLocation? Location,       // null for project-level matches
    string? Snippet);

public sealed record Diagnostic(string Code, string Message, string? File);

public sealed record ContentItem(
    string SourcePath, string? NameOverride, string? Importer, string? Processor,
    IReadOnlyDictionary<string, string> ProcessorParams);

public sealed record ProjectInfo(
    string Path, string AssemblyName, bool IsXna, XnaVersion XnaVersion,
    XnaPlatform Platform, string? Profile, string? TargetFramework,
    IReadOnlyList<string> References, IReadOnlyList<string> CompileFiles,
    IReadOnlyList<string> DefineConstants,
    IReadOnlyList<string> ContentProjects, IReadOnlyList<ContentItem> ContentItems);

public sealed record TriggeredRule(string Id, string Title, string Explanation, string Resolution, string? MigratorTask);

public sealed record AnalysisReport(
    int SchemaVersion, string CatalogName,
    IReadOnlyList<ProjectInfo> Projects,
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<TriggeredRule> TriggeredRules,
    IReadOnlyDictionary<Severity, int> CountsBySeverity,
    IReadOnlyDictionary<Classification, int> CountsByClassification,
    int Score, int ProjectedScore,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? NoProjectReason);       // set when no XNA project was found
```

`Finding` does not repeat the explanation and resolution. The report holds them once per rule in `TriggeredRules`.

## Entry point

```csharp
public sealed class ProjectAnalyzer
{
    public Task<AnalysisReport> AnalyzeAsync(
        string path, RuleCatalog catalog,
        IProgress<AnalysisProgress>? progress, CancellationToken ct);
}
```

The caller loads the catalog with `RuleCatalogLoader.LoadAsync(path, ct)`. This keeps the analyzer testable with small in-memory catalogs.

## Discovery

- `.csproj`: one project.
- `.sln`: read `Project("{GUID}") = "Name", "relative\path.csproj", "{GUID}"` lines with a regex. No MSBuild dependency. Skip entries that are not `.csproj` or do not exist.
- Folder: enumerate `*.sln` and `*.csproj` recursively, skipping `bin`, `obj`, `.git`, `.vs`. If solutions exist, use their projects plus any `.csproj` not in a solution.
- A project is XNA if any of these is true: an `Import` whose path contains `Microsoft.Xna.GameStudio`, a property `XnaFrameworkVersion`, or a reference named `Microsoft.Xna.Framework*`.
- Result is a sorted list plus an optional `NoProjectReason`.

## Project reading

Use `System.Xml.Linq` on a stream opened through `ReadOnlyFileSystem`. Do not use MSBuild evaluation: it can run targets and it fails on old projects without the XNA targets installed.

- Properties: first value wins per property name across all `PropertyGroup`s. `DefineConstants` is the union of all groups, split on `;`.
- References: `Reference` item `Include`, reduced to the assembly simple name (text before the first comma).
- Compile items: `Compile` `Include`, normalized to forward slashes. Wildcards are expanded against the project folder.
- Content: `NestedContentProject` items point to `.contentproj` files. `ContentProjectReader` reads each `Compile` item with `Name`, `Importer`, `Processor`, and `ProcessorParameters_*` children. The content-conversion spec extends this reader (nested roots, graphics profile, pipeline references).

### Version detection

`XnaVersionDetector` collects signals and returns the most specific:

| Signal | Example | Result |
|---|---|---|
| `XnaFrameworkVersion` | `v4.0` | 4.0 |
| Reference text | `Microsoft.Xna.Framework, Version=3.1.0.0` | 3.1 |
| Targets import path | `...XNA Game Studio\v4.0\Microsoft.Xna.GameStudio.targets` | 4.0 |

Disagreement adds Diagnostic `ANA002` and picks the highest-signal version. No signal gives `Unknown`.

## Rule catalog

Schema: `src/XnaReborn.Analyzer/Rules/RuleSchema.json` (JSON Schema 2020-12). It is embedded as a resource and also read by the "validate rules" hook.

Catalog shape:

```json
{
  "$schema": "../src/XnaReborn.Analyzer/Rules/RuleSchema.json",
  "version": 1,
  "name": "xna-to-monogame",
  "scoring": { "severityWeights": { ... }, "classificationFactors": { ... } },
  "rules": [ ]
}
```

Example rule (shape only, not part of the empty catalog):

```json
{
  "id": "XNA001",
  "title": "Games for Windows Live / GamerServices",
  "severity": "Blocker",
  "classification": "Unsupported",
  "category": "Api",
  "match": { "type": "reference", "name": "Microsoft.Xna.Framework.GamerServices" },
  "explanation": "Xbox Live and GamerServices have no modern equivalent.",
  "resolution": "Remove the dependency or replace it with your own sign-in and storage."
}
```

`RuleCatalogLoader` validates with the schema (JsonSchema.Net, no network) and then runs checks the schema cannot express: duplicate ids, regex that fails to compile, `member.arguments.min > max`. Errors name the rule id and the JSON path.

### Match types

| `match.type` | Evaluated by | Data it reads |
|---|---|---|
| `reference` | project | `ProjectInfo.References` |
| `projectProperty` | project | raw property map |
| `projectItem` | project | raw items (Reference, Compile, NestedContentProject, Import) |
| `filePattern` | project | files on disk under the project folder (glob) |
| `contentItem` | project | `ProjectInfo.ContentItems`. Optional `nonStandardPair: true` keeps only items whose importer or processor is not in the stock list of `ImporterMap` (`rules/importer-map.json`, embedded in Core) |
| `spriteFontFamily` | project | `FontName` in `.spritefont` files under the project folder and nested content project folders |
| `namespace` | source | `using` directives and qualified names |
| `type` | source | identifiers, gated by imported or qualified namespace |
| `member` | source | member access and invocation syntax |

Project-level matchers produce one finding per project per rule. Source matchers produce one finding per location.

### Source matching limits

XNA assemblies are not installed on most modern machines, so the semantic model cannot resolve XNA types. Source matching is syntactic:

- `type`: an identifier equals `name` and the file has a `using` for one of `namespaces`, or the name is qualified with one.
- `member`: the member name equals `name`, the file imports one of `namespaces`, the `receiver` (if given) equals the identifier left of the dot, and the argument count is within `arguments`. A rule about an instance method (for example `graphics.GraphicsDevice.ResolveBackBuffer`) can match unrelated types that have the same member name. Rule authors reduce this with `receiver`, `arguments`, and fixtures with a negative case.

This trade-off is deliberate: it keeps the analyzer working on the user's machine with no XNA install.

## Source scanning

`SourceScanner` reads each compile file through `ReadOnlyFileSystem`, parses with `CSharpSyntaxTree.ParseText` using `LanguageVersion.Latest` and the project's `DefineConstants`, then walks the tree once and hands nodes to the source matchers.

- Files are scanned with `Parallel.ForEachAsync` and a cancellation token. Results go into a per-file list and are sorted after the loop.
- Parse errors become Diagnostic `ANA004` but the partial tree is still scanned.
- Missing file: `ANA003`. File over 5 MB: `ANA005`. Unreadable project file: `ANA001`.
- Snippet is the trimmed source line, max 200 characters.

## Scoring

```
penalty(rule)  = severityWeight[rule.severity] Ã— classificationFactor[rule.classification]
score          = clamp(round(100 âˆ’ Î£ penalty(r) for each distinct rule r that fired), 0, 100)
projectedScore = same sum, skipping rules whose classification is AutoFixable
```

Initial weights (tunable in the catalog without code changes):

| Severity | Weight | | Classification | Factor |
|---|---|---|---|---|
| Info | 1 | | AutoFixable | 0.25 |
| Warning | 3 | | Manual | 1.0 |
| Error | 8 | | Unsupported | 1.5 |
| Blocker | 20 | | | |

If the catalog has no `scoring` section, these defaults apply. The score is per analysis, across all projects. Rules are counted once per run.

## Reports

- **JSON**: `System.Text.Json`, camelCase, enums as strings, `schemaVersion: 1`, indented, LF line endings.
- **Markdown**: title, project table (version, platform, profile), score and projected score, counts, findings grouped by classification then severity, each with file:line and snippet, rule appendix, diagnostics.
- Paths are relative to the project root. No timestamps. All lists are sorted.

## Read-only enforcement

- `ReadOnlyFileSystem` is the only class that opens files. It uses `FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete` and exposes read-only methods (read text, open read stream, enumerate, exists).
- Other analyzer classes take `IReadOnlyFileSystem`. No raw `System.IO.File` calls outside it.
- Two tests back this up (see below).

## Error handling

- Per-file and per-project failures become Diagnostics. Analysis continues.
- An invalid catalog stops before analysis begins.
- Cancellation throws `OperationCanceledException` and returns no partial report.

## Diagnostic codes

| Code | Meaning |
|---|---|
| ANA001 | Project or content project could not be read or parsed |
| ANA002 | XNA version signals disagree |
| ANA003 | Compile item points to a missing file |
| ANA004 | Source file has syntax errors |
| ANA005 | Source file skipped because it is too large |
| ANA006 | A `.sln` lists a project that does not exist on disk |
| ANA007 | A folder could not be read (no access or removed during the scan) |

Rule ids use `XNA###`. `XNA013` is reserved for the XNA-only font warning, which the content-conversion spec raises.

## Fixtures

Written from scratch and committed under `tests/fixtures/<name>/`. Each has a `README.md` stating it is original, synthetic, and released under the repository license.

| Fixture | Purpose |
|---|---|
| `xna40-basic` | Clean 4.0 game using only APIs MonoGame also has. Only project-level findings. High score. |
| `xna31-legacy` | 3.1 project using 3.1-only APIs. |
| `xna40-gamerservices` | References GamerServices and uses `Guide`. Blocker. |
| `xna40-custom-content` | Content project with a custom processor, an `.xap`, and a name override. |
| `multi-project-sln` | Solution with game, library, and nested content project. |
| `not-xna` | Plain console `.csproj`. Negative case for discovery. |
| `empty-folder` | Folder with no projects. |

Expected results are Verify snapshots stored in `tests/XnaReborn.Tests/Snapshots/`.

## Testing strategy

1. **Model tests**: records compare by value, enums serialize as strings.
2. **Discovery tests**: each fixture, plus no-project reasons, skipped folders, stable ordering.
3. **Reader tests**: properties, references, compile wildcards, content items, broken XML gives ANA001.
4. **Version tests**: each signal alone, conflicting signals, no signal.
5. **Catalog tests** (`Category=Rules`): valid, invalid per schema, duplicate id, AutoFixable without `migratorTask`, empty catalog.
6. **Matcher tests**: for every match type, a positive and a negative fixture.
7. **Scoring tests**: no findings gives 100, one rule counted once for many matches, clamping, projected score.
8. **Report snapshots** (Verify) for each fixture, JSON and Markdown, plus a determinism test (two runs, identical bytes).
9. **Safety tests**: hash-unchanged and no-new-files test over every fixture; source scan for write APIs.
10. **Cancellation test**: a cancelled token throws `OperationCanceledException`.
11. No test uses the network.

## Open questions

1. **Shared importer map. DECIDED.** The map lives in `rules/importer-map.json` and is loaded from Core. The analyzer and the migrator both use it. The `contentItem` match gains an optional `nonStandardPair` flag (catalog schema version stays 1). See follow-up task 11.2.
2. **XNA013 font rule. DECIDED.** Added as the `spriteFontFamily` match type (task 10). The content-conversion `FontChecker` can reuse its font list.
3. **Score formula.** The weights above are a starting point. Tune against the demo project so the before and after numbers tell the story honestly.
4. **Content `ProjectReference` items. DECIDED.** The project reader follows a `ProjectReference` to a `.contentproj` like a `NestedContentProject`. See follow-up task 11.1.
5. **Entry point inside `#if WINDOWS || XBOX`. DECIDED.** The migrator's `EntryPointRewriter` fixes it in source (see the project-migrator design). The analyzer has no check for it yet; a preprocessor check would be a new match type, added only if the demo needs it.
