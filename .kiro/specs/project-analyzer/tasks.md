# Tasks: Project Analyzer

- [x] 1. Models
  - [x] 1.1 Add enums, `Finding`, `SourceLocation`, `Diagnostic`, `ContentItem`, `ProjectInfo`, `TriggeredRule`, `AnalysisReport`, `AnalysisProgress` to `XnaReborn.Core`
  - [x] 1.2 Add rule models: `Rule`, `RuleMatch` (one record per match type), `RuleCatalog`, `ScoringOptions`
  - [x] 1.3 XML docs on all public types; model and JSON serialization tests
  - _Requirements: R6, R8_

- [x] 2. Fixtures
  - [x] 2.1 Write the synthetic fixtures listed in the design, each with a `README.md` stating it is original and under the repository license
  - [x] 2.2 Add a fixture-path helper to the test project
  - _Requirements: R1 to R7_

- [x] 3. Discovery
  - [x] 3.1 Implement `IReadOnlyFileSystem` and `ReadOnlyFileSystem`
  - [x] 3.2 Implement `ProjectDiscovery` for `.csproj`, `.sln`, and folders, with skip rules and stable order
  - [x] 3.3 XNA project identification and `NoProjectReason`
  - [x] 3.4 Discovery tests for every fixture
  - _Requirements: R1, R9_

- [x] 4. Project reading
  - [x] 4.1 Implement `ProjectReader` (properties, references, compile items, imports)
  - [x] 4.2 Implement `ContentProjectReader` (items, importer, processor, parameters, nested projects)
  - [x] 4.3 Implement `XnaVersionDetector` with `ANA002` on conflict
  - [x] 4.4 Reader and version tests
  - _Requirements: R2, R3_

- [x] 5. Rule catalog and engine
  - [x] 5.1 Create `RuleSchema.json` as an embedded resource (draft is in `src/XnaReborn.Analyzer/Rules/`)
  - [x] 5.2 Implement `RuleCatalogLoader` with schema validation and extra checks
  - [x] 5.3 Implement `RuleEngine` and the project-level matchers: `reference`, `projectProperty`, `projectItem`, `filePattern`, `contentItem`
  - [x] 5.4 Catalog tests and a positive and negative test per matcher (`Category=Rules`)
  - _Requirements: R4, R6_

- [x] 6. Source scanning
  - [x] 6.1 Implement `SourceScanner` with Roslyn, define constants, and parallel scanning
  - [x] 6.2 Implement source matchers: `namespace`, `type`, `member`
  - [x] 6.3 Locations, snippets, and diagnostics `ANA003`, `ANA004`, `ANA005`
  - [x] 6.4 Positive and negative tests per matcher
  - _Requirements: R5, R6_

- [x] 7. Scoring
  - [x] 7.1 Implement `Scorer` with defaults and catalog overrides
  - [x] 7.2 Score and projected score tests
  - _Requirements: R7_

- [x] 8. Reports
  - [x] 8.1 Implement `ProjectAnalyzer.AnalyzeAsync` with progress and cancellation
  - [x] 8.2 Implement JSON and Markdown writers
  - [x] 8.3 Verify snapshot tests for every fixture, and a determinism test
  - _Requirements: R6, R8, R10_

- [x] 9. Safety test
  - [x] 9.1 Hash-unchanged and no-new-files test over every fixture
  - [x] 9.2 Source scan test for write APIs in `XnaReborn.Analyzer`
  - _Requirements: R9_

- [x] 10. Checkpoint: analyzer reports
  - Full test suite, `dotnet format --verify-no-changes`, review of every report snapshot, add rules from the manual-migration list
  - _Requirements: R1 to R10_

- [ ] 11. Follow-ups from the first rule catalog
  - [ ] 11.1 `ProjectReader` follows a `ProjectReference` to a `.contentproj` like a `NestedContentProject`, so XNA017, XNA019, XNA020, XNA021 and the font rule see that content; XNA049 becomes AutoFixable under `project-migrator:7.1`
  - [ ] 11.2 Move the importer and processor map to `rules/importer-map.json`, loaded from Core (shared with `content-conversion` task 4.1); add the optional `nonStandardPair` flag to the `contentItem` match and a rule for custom processors
  - _Requirements: R2, R4_
