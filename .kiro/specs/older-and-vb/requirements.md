# Requirements: Older XNA Projects (1.0 / 2.0) and Visual Basic XNA Projects

## Introduction

XNA Reborn today reads C# projects from XNA Game Studio 3.0, 3.1 and 4.0. This spec adds two inputs:

- **Part A**: projects made with XNA Game Studio Express 1.0 (and 1.0 Refresh) and XNA Game Studio 2.0.
- **Part B**: Visual Basic XNA projects (`.vbproj`). Microsoft added Visual Basic to XNA Game Studio 4.0 Refresh. A few earlier VB projects exist as hand-made setups on XNA 3.x.

Both parts keep the product rules: the original project is never changed, every change goes to a migrated copy, and findings come from the JSON rule catalog first. Research, evidence and decisions are in `design.md`. Rule IDs used here are provisional; the block XNA100 to XNA119 is reserved for this spec.

## Glossary

- **Older project**: a C# project whose framework version is 1.0 or 2.0.
- **Express project**: a project made by XNA Game Studio Express 1.0 or 1.0 Refresh. Both report framework version 1.0, so the tool treats them as one version.
- **VB project**: a project file with the extension `.vbproj`.
- **Source language**: `CSharp` or `VisualBasic`. It is decided per project from the project file extension.
- **Implicit imports**: namespaces that a VB project lists as `<Import Include="..." />` items in its project file. VB source files rely on them without an `Imports` line.
- **My namespace**: the Visual Basic application model (`My.Application`, `My.Computer`, `My.Settings`, `My.Resources`), turned on by the `MyType` project property.
- **Fact sink**: the language-neutral part of the source matcher. Language walkers feed it names and member uses, and it decides which rules hit.

## Requirements: Part A (XNA 1.0 and 2.0)

### R1: Version detection
**User story:** As a developer with a 2007 project, I want it recognized as XNA 1.0 or 2.0, so that the right rules run.

Acceptance criteria:
1. THE analyzer SHALL support the framework versions "1.0" and "2.0" in `XnaVersion`, the report JSON, the rule schema and the rule models, in addition to "3.0", "3.1" and "4.0".
2. WHEN `XnaFrameworkVersion` is `v1.0` or `v2.0`, THE detector SHALL report that version.
3. WHEN a targets import path contains a version folder such as `v1.0` or `v2.0`, THE detector SHALL read the version from it, including the Express path `Microsoft\XNA\Game Studio Express\v1.0\`.
4. WHEN a framework reference carries `Version=2.0.0.0`, THE detector SHALL read 2.0 from it.
5. WHEN no version property, import version or reference version exists and `ProjectTypeGuids` contains the Express GUID `{9F340DF3-2AED-4330-AC16-78AC2D9B4738}`, THE detector SHALL report 1.0.
6. WHEN signals disagree, THE detector SHALL keep the existing priority order and report ANA002 as it does today.
7. THE detector SHALL NOT treat a missing `TargetFrameworkVersion` or a Visual Studio 2005 solution header as a conflict. These are weak hints only.

### R2: Project recognition and reading
**User story:** As a developer, I want older project files read without errors, even though they look different from 3.x files.

Acceptance criteria:
1. THE analyzer SHALL recognize an Express project that has no `Microsoft.Xna.GameStudio` import and imports `Microsoft.Xna.Common.targets` and `Microsoft.Xna.ContentPipeline.targets` instead.
2. THE analyzer SHALL recognize a 2.0 project that imports `Microsoft.Xna.GameStudio.Common.targets` and `Microsoft.Xna.GameStudio.NestedContent.targets`.
3. THE project reader SHALL read items of type `Content` that carry `XNAUseContentPipeline=true` as content items of the game project, with `Importer`, `Processor`, `Name` and `Link` metadata when present.
4. THE project reader SHALL read a project without `TargetFrameworkVersion` and leave `TargetFramework` null.
5. THE analyzer SHALL read Visual Studio 2005 solutions (format 9.00) with the same solution reading it uses today.

### R3: Rules for older APIs
**User story:** As a developer, I want the API problems of my old project listed with the same rules as 3.x projects.

Acceptance criteria:
1. THE catalog SHALL run the 3.x API rules XNA022 to XNA043 on versions 1.0 and 2.0 as well.
2. THE catalog SHALL add rules for APIs that exist only before 3.0 and break a MonoGame build: `LoadGraphicsContent`, `UnloadGraphicsContent`, `ResourceManagementMode` and `ResourceUsage`.
3. THE catalog SHALL add an informational rule for projects of version 1.0 or 2.0.
4. THE catalog SHALL match the Express build targets with the XNA001 rule.
5. THE rule engine SHALL support a `projectItem` match that also tests item metadata.
6. THE rule engine SHALL support a `methodDeclaration` match for overridden methods, because the `member` match only finds member accesses and calls.
7. EACH new or changed rule SHALL have a positive and a negative case in `rule-cases.json`. The negative case for a widened rule SHALL show that a 4.0 project does not trigger it.

### R4: Content of older projects
**User story:** As a developer, I want textures and models of my old game carried over.

Acceptance criteria:
1. WHEN an Express project builds content inside the game project, THE analyzer SHALL report the XNA101 finding and THE migrator SHALL convert those items into `Content.mgcb` with the same rules it uses for a nested content project.
2. THE shared importer map SHALL map the 1.0 processors `SpriteTextureProcessor` and `ModelTextureProcessor` to `TextureProcessor` with a note in the content report.
3. THE analyzer SHALL NOT report XNA050 for the two legacy processors above.
4. THE migrator SHALL treat 2.0 nested content projects exactly like 3.x nested content projects.
5. WHEN an Express project lists custom pipeline assemblies in `XNAProjectContentPipelineAssemblies`, THE analyzer SHALL report them through the existing rule XNA008 or XNA050 and SHALL NOT drop them silently.

### R5: Migration of older projects
**User story:** As a developer, I want my 2007 game to build as a MonoGame project with minimal manual work.

Acceptance criteria:
1. THE migrator SHALL generate an SDK-style `.csproj` for an older project with the same rules it uses for 3.x projects, including `GenerateAssemblyInfo=false` when `Properties/AssemblyInfo.cs` is kept.
2. THE migrator SHALL rewrite `override ... LoadGraphicsContent(bool loadAllContent)` to `LoadContent()` and `UnloadGraphicsContent(bool unloadAllContent)` to `UnloadContent()`, and SHALL keep the parameter name alive as a local `const bool` set to `true`, so the method body still compiles.
3. IF a `LoadGraphicsContent` or `UnloadGraphicsContent` method is called through `base.` or by another method, THEN THE migrator SHALL leave the file unchanged and add a marker for manual work.
4. THE migrator SHALL leave the source project unchanged and SHALL write a manifest entry for every changed, created or skipped file.

## Requirements: Part B (Visual Basic)

### R6: Discovery
**User story:** As a developer, I want to open a folder, solution or `.vbproj` of my VB game.

Acceptance criteria:
1. WHEN given a `.vbproj`, THE analyzer SHALL analyze that project.
2. WHEN given a folder, THE analyzer SHALL find `.vbproj` files next to `.csproj` files, with the same skipped folders as today.
3. WHEN a `.sln` lists a project with the VB project type GUID `{F184B08F-C81C-45F6-A57F-5ABD9991F28F}` or a path ending in `.vbproj`, THE analyzer SHALL include it.
4. THE analyzer SHALL decide the source language of a project from the project file extension and store it in `ProjectInfo.Language`.
5. WHEN a solution mixes C# and VB projects, THE analyzer SHALL analyze each with its own language.
6. THE analyzer SHALL NOT mark a VB project that references MonoGame packages and no XNA assembly as an XNA project.

### R7: Reading `.vbproj`
**User story:** As a developer, I want my VB project file understood.

Acceptance criteria:
1. THE reader SHALL read a `.vbproj` as XML and SHALL NOT evaluate MSBuild.
2. THE reader SHALL keep the project-level namespace imports (`<Import Include="Microsoft.Xna.Framework" />` inside an `ItemGroup`) as a separate item type, so they cannot be confused with targets imports.
3. THE reader SHALL split `DefineConstants` on `;` and `,`, because VB projects use both.
4. THE reader SHALL read `MyType`, `OptionExplicit`, `OptionStrict`, `OptionCompare`, `OptionInfer`, `RootNamespace` and `StartupObject` from the project.
5. THE reader SHALL read compile items ending in `.vb` for a VB project and SHALL ignore `.cs` items in it.
6. THE version detector SHALL work on VB projects with the same signals as C# projects and SHALL report Unknown for a VB project without any signal.

### R8: Scanning VB source
**User story:** As a developer, I want the same API findings in VB code as in C# code.

Acceptance criteria:
1. THE scanner SHALL parse `.vb` files with `Microsoft.CodeAnalysis.VisualBasic` and the project's `DefineConstants`.
2. THE matcher SHALL apply the existing namespace, type and member rules to VB code, with identifier comparison that ignores case.
3. THE matcher SHALL count the implicit imports of the project as imported namespaces when it opens the namespace gate of a type or member rule.
4. THE matcher SHALL read `Imports` lines of a file, including alias lines (`Imports Xna = Microsoft.Xna.Framework`), and SHALL resolve an alias at the start of a qualified name.
5. THE matcher SHALL match a member use inside a `With` block (`.RenderState`) by its member name, with no receiver.
6. THE matcher SHALL treat `fx.Begin` without parentheses as an invocation with zero arguments when Roslyn gives it as an invocation.
7. THE scanner SHALL report a VB syntax error as ANA004 and SHALL NOT stop scanning other files.
8. THE catalog SHALL support a `languages` field on a rule. A rule without it applies to both languages.
9. THE catalog SHALL support an `autoFixLanguages` field. For languages not in it, the effective classification of a finding is `Manual`, so the score and the projected score stay honest.

### R9: Language-neutral contracts
**User story:** As a maintainer, I want one place for rule logic, so a new language does not copy it.

Acceptance criteria:
1. THE matcher SHALL split into per-language walkers that only extract facts, and one fact sink that holds the rule logic.
2. THE C# behavior of the matcher SHALL stay identical. All current analyzer snapshots SHALL pass without change.
3. THE source rewriter contract SHALL carry the source language in `SourceRewriteContext` and in `ISourceRewriter`. A rewriter without a language declaration SHALL be a C# rewriter.
4. THE rewriter registry SHALL accept two rewriters for one rule ID when their languages differ.
5. THE rewrite engine SHALL run only the rewriters whose language matches the file.
6. THE marker inserter SHALL write a comment in the syntax of the file language: `// XNAREBORN:` for C# and `' XNAREBORN:` for VB.

### R10: SDK-style `.vbproj` generation
**User story:** As a developer, I want a VB project that builds with `dotnet build`.

Acceptance criteria:
1. THE migrator SHALL write `<Project Sdk="Microsoft.NET.Sdk">` with `OutputType`, `TargetFramework`, `AssemblyName`, `RootNamespace`, and the MonoGame package references already chosen for C# projects.
2. THE migrator SHALL set `<MyType>Empty</MyType>`. THE migrator SHALL NOT write `MyType` values that need Windows Forms or the console application base class, because the .NET SDK does not provide them.
3. THE migrator SHALL copy `OptionExplicit`, `OptionStrict`, `OptionCompare` and `OptionInfer`, and the project-level namespace imports.
4. THE migrator SHALL drop `Nullable` and `ImplicitUsings` for VB, because they are C# properties.
5. THE migrator SHALL keep `StartupObject` only when it names a type. THE migrator SHALL drop values such as `Sub Main` and `*.My.MyApplication`.
6. THE migrator SHALL set `GenerateAssemblyInfo=false` when `My Project/AssemblyInfo.vb` is kept.
7. THE migrator SHALL omit the compile items of `My Project` designer files and SHALL list each omitted file in the manifest as manual work.
8. THE migrator SHALL reference the content file with `MonoGameContentReference` and write the tool manifest exactly as it does for C#.
9. WHEN a solution mixes languages, THE migrator SHALL write both projects and a solution file that lists each with the right project type GUID.

### R11: VB rewriters
**User story:** As a developer, I want the common fixes done for VB too, and the rest clearly marked.

Acceptance criteria:
1. THE migrator SHALL port the entry point fix: it unwraps `#If WINDOWS OrElse XBOX Then ... #End If` around `Sub Main`, with the same safety rules as the C# rewriter.
2. THE migrator SHALL port the XNA022 fix: `Begin(SpriteBlendMode.X)` becomes `Begin(SpriteSortMode.Deferred, BlendState.X)`, including named arguments.
3. FOR XNA024, XNA039 and XNA040 THE migrator SHALL add a marker comment above the statement in VB and SHALL NOT change the code. These three rules list only `csharp` in `autoFixLanguages`.
4. THE VB rewriters SHALL keep every byte outside the edited spans, including line continuation characters and `Region` blocks.
5. IF a rewriter cannot place a marker safely (for example in a multi-line statement), THEN THE rewriter SHALL place it above the first line of the statement.

### R12: Build and run
**User story:** As a developer, I want to build and start the migrated VB game from the app.

Acceptance criteria:
1. THE runner SHALL find the game project among `.csproj` and `.vbproj` files of the migrated copy.
2. THE migrated-copy guard SHALL accept `.vbproj` files in its checks for a solution or a single project.
3. THE build output parser SHALL parse VB compiler lines (`error BC30451`) and the lines without a file (`vbc : error BC30002`).
4. THE hint catalog SHALL contain a hint for a VB build error that comes from the `My` namespace.

### R13: Report and UI
**User story:** As a developer, I want to see which language each project has and what is limited.

Acceptance criteria:
1. THE report JSON and Markdown SHALL show the language of each project. The schema version stays at the next minor increment and old readers SHALL ignore the new field.
2. THE report SHALL list the rule XNA110 for a VB project, with the list of limits in its explanation.
3. THE migrated-project summary SHALL show the VB markers as manual work.

### R14: Safety and quality
**User story:** As a user, I want the same safety for every project type.

Acceptance criteria:
1. THE analyzer SHALL open `.vbproj`, `.vb` and `.sln` files read-only. A test SHALL prove the file hashes of the new fixtures are unchanged after analysis.
2. THE migrator SHALL write to a new folder only and SHALL refuse an existing non-empty folder, as for C#.
3. THE fixtures SHALL be written from scratch in this repository, with a `README.md` that states they are original. THE repository SHALL NOT contain code or assets copied from third-party projects.
4. THE tests SHALL NOT use the network. Candidate projects SHALL be used only for read-only checks outside the repository.
5. EVERY new rule SHALL have a positive and a negative fixture test. EVERY new match type or field SHALL have unit tests and a schema test.
6. `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` SHALL pass at each checkpoint.
