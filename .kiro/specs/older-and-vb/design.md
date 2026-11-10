# Design: Older XNA Projects (1.0 / 2.0) and Visual Basic XNA Projects

## Overview

This design extends the analyzer, migrator and runner in place. No new project is needed. Both parts reuse the existing flow (discover, read, scan, plan, migrate, build). The work is mostly data (catalog, importer map) plus four small code changes:

1. Two more framework versions in `XnaVersion`, with better detection signals.
2. A source language on every project, and a matcher and rewriter contract that no longer assume C#.
3. A Roslyn VB walker next to the C# walker.
4. A VB project generator next to the C# project generator.

Everything in the "Evidence" section was checked against real files or a real build during research on 2026-11-11. Items that were not checked are listed in "Not verified" and become checks inside tasks.

## Evidence

### What was read in this repository

- `XnaVersionDetector` knows only `3.0`, `3.1`, `4.0`. For `v1.0` or `v2.0` the property parses to null, so the version stays Unknown. Rules that carry `xnaVersions` (XNA006, XNA022 to XNA043) do not run on Unknown, so an older project would show almost no API findings.
- The import signal only looks at imports whose path contains `XNA Game Studio`. The Express path is `Microsoft\XNA\Game Studio Express\v1.0\`, which does not contain that text.
- `XnaProjectDetector.IsXna` accepts `XnaFrameworkVersion`, an import containing `Microsoft.Xna.GameStudio`, or an XNA reference. An Express library with no XNA reference would be missed.
- Rule XNA001 matches imports with `*Microsoft.Xna.GameStudio*`. The Express targets are named `Microsoft.Xna.Common.targets` and `Microsoft.Xna.ContentPipeline.targets`, so XNA001 does not fire.
- Discovery, `SourceScanner`, `ProjectMigrator.CompileFiles`, `GameLocator` and `MigratedCopyGuard` all filter on `.csproj` or `.cs`.
- `SourceWalker` is tied to C# syntax types. `SourceRuleMatcher.Match` and the `SourceScanner` hook already use the language-neutral `SyntaxTree` type, so the entry points need no signature change.
- `ISourceRewriter` is keyed by rule ID. `RewriterRegistry.Register` throws on a repeated ID. `MarkerInserter` writes `// XNAREBORN:` and parses C#.
- `ProjectXml.ReadItems` turns both a top-level `<Import Project=...>` and an `ItemGroup` `<Import Include=...>` into an item of type `Import`. This is harmless for C# projects. It becomes a collision for VB, where `ItemGroup` imports are namespaces.
- `ProjectReader` splits `DefineConstants` on `;` only.
- `ContentProjectReader` and `ProjectInfo.ContentProjects` assume a separate content project file.

### Real project shapes (read from public files, nothing copied into this repo)

| Aspect | Express 1.0 | XNA 2.0 | XNA 3.x | XNA 4.0 |
|---|---|---|---|---|
| `XnaFrameworkVersion` | `v1.0` | `v2.0` | `v3.0` / `v3.1` | `v4.0` |
| Game project type GUID | `{9F340DF3-2AED-4330-AC16-78AC2D9B4738}` (Windows and Xbox 360) | `{6D335F3A-9D43-41b4-9D22-F6F17C4BE596}` Windows, `{2DF5C3F4-5A5F-47a9-8E94-23B4456F55E2}` Xbox 360 | not re-checked | `{6D335F3A-...}` for every platform (also VB) |
| Targets imports | `$(MSBuildExtensionsPath)\Microsoft\XNA\Game Studio Express\v1.0\Microsoft.Xna.ContentPipeline.targets` and `...\Microsoft.Xna.Common.targets` | `...\Microsoft\XNA Game Studio\v2.0\Microsoft.Xna.GameStudio.Common.targets` and `...NestedContent.targets` | `...\XNA Game Studio\v3.1\Microsoft.Xna.GameStudio.targets` (existing fixture, not re-checked) | `...\XNA Game Studio\Microsoft.Xna.GameStudio.targets` (no version folder) |
| Framework reference | `Microsoft.Xna.Framework` with no `Version=` | `Version=2.0.0.0`, token `6d5c3888ef60e27d` | token `6d5c3888ef60e27d` (3.1 VB projects) | `Version=4.0.0.0`, token `842cf8be1de50553` |
| Content | `Content` items inside the game project with `XNAUseContentPipeline`, `Importer`, `Processor`, `Name`, `Link` | `NestedContentProject` to a `.contentproj` (type GUID `{96E2B04D-8817-42c6-938A-82C39BA4D311}`, `Compile` items with `Importer`/`Processor`) | `NestedContentProject` | `ProjectReference` to the content project |
| `TargetFrameworkVersion` | absent | absent | `v3.5` (existing fixture) | `v4.0` and a profile |
| Other XNA properties | `XNAGlobalContentPipelineAssemblies`, `XNAProjectContentPipelineAssemblies` | `XnaCrossPlatformGroupID` | same | `XnaProfile`, `XnaOutputType`, `XnaRefreshLevel` |
| Solution | Format 9.00, "Visual C# Express 2005" | Format 9.00, "Visual Studio 2005" | Format 10.00 (VS 2008) | Format 11.00 (VS 2010) |

Real files used: `jackpal/Dandy-Dungeon` (1.0, Windows and Xbox 360 projects), `frham20/SharpEngine` (1.0 and its solution), `SimonDarksideJ/XNAGameStudio` Movipa sample (2.0 Windows, Xbox 360 and content project), `jmonasterio/xgc3` (a 2.0-era solution with the VS 2005 header) and the 4.0 VB samples listed below. The 3.x column rests on the existing synthetic fixtures plus the two 3.1 VB projects.

### Real VB project shapes

- The VB 4.0 Refresh game project (`GameStateManagementSample (Windows).vbproj`) has `ProjectTypeGuids` `{6D335F3A-...};{F184B08F-C81C-45F6-A57F-5ABD9991F28F}`, `XnaFrameworkVersion v4.0`, `OutputType WinExe`, `OptionExplicit/OptionStrict/OptionCompare/OptionInfer`, `VBRuntime Embed`, a block of `<Import Include="Microsoft.Xna.Framework" />` items, `NoStdLib true`, `XNA` references, a `ProjectReference` to `Content.contentproj`, `My Project\AssemblyInfo.vb`, and `Microsoft.VisualBasic.targets` plus the 4.0 XNA targets.
- Its `Program.vb` wraps `Shared Sub Main` in `#If WINDOWS OrElse XBOX Then ... #End If`, the same trap as the C# entry point.
- VB `DefineConstants` can be comma separated (`XBOX,XBOX360`), and `WINDOWS_PHONE` is common.
- A hand-made VB project on XNA 3.1 (`VaderConsulting/WindowsVBGame1`) has no XNA targets import, no type GUID, `MyType WindowsForms`, `StartupObject WindowsGame1.My.MyApplication` and plain `Microsoft.Xna.Framework` references. Only the reference and the `XnaFrameworkVersion`-less state identify it. Its solution carries the VB GUID.
- MonoGame VB projects already exist (`playdeezgames/tggd_hj5`, MIT, uses `MonoGame.Framework.DesktopGL`). They contain `Microsoft.Xna.Framework` namespaces in code but no XNA assembly reference. They are the negative case for detection.

### Feasibility build (VB on MonoGame, done in a temporary folder outside the repo)

A hand-written SDK-style `.vbproj` with `MonoGame.Framework.DesktopGL 3.8.6.1`, `MonoGame.Content.Builder.Task 3.8.6.1`, a `MonoGameContentReference` and the pinned `dotnet-mgcb` tool built with 0 errors on `net8.0` when `MyType` was `Empty`. Two results matter for the design:

- `MyType` `Console` fails with `BC30002: Type 'Global.Microsoft.VisualBasic.ApplicationServices.ConsoleApplicationBase' is not defined`.
- With `MyType` `Empty`, any use of `My.Application` fails to compile. `My.*` code can therefore never be auto-migrated; it needs a rule and a manual fix.

The content builder step ran from the same tool manifest design already used for C#, so the `MonoGameContentReference` flow is language-neutral. A `net8.0-windows` (WindowsDX) VB build was not tried.

### Roslyn VB shape check (also in a temporary folder)

Parsing a snippet with `Microsoft.CodeAnalysis.VisualBasic 4.14.0` (the same line as the pinned `Microsoft.CodeAnalysis.CSharp 4.14.0`, net8.0 compatible) showed:

- `device.RenderState.AlphaBlendEnable` is a `SimpleMemberAccessExpression`, same as C#.
- Inside `With device.RenderState`, `.CullMode` is a member access whose `Expression` is null.
- `fx.Begin()` and `fx.Begin` (statement position) are both `InvocationExpression` nodes. The second has a null `ArgumentList`.
- `Imports Microsoft.Xna.Framework.Graphics` gives an imports clause with qualified names. `Imports Xna = Microsoft.Xna.Framework` has an alias.
- `Protected Overrides Sub LoadGraphicsContent(...)` is a `MethodStatement` with modifiers.
- `VisualBasicParseOptions` takes preprocessor symbols as `KeyValuePair<string, object>`. With `WINDOWS` defined, `#If WINDOWS OrElse XBOX Then` is an active region.

### API and content-pipeline changes that matter

| Change | Between | Source | Effect on a MonoGame migration | Rule |
|---|---|---|---|---|
| `LoadGraphicsContent(bool)` and `UnloadGraphicsContent(bool)` became `LoadContent()` and `UnloadContent()`; the device is virtualized and `LoadContent` runs once | 1.0 to 2.0 | [Shawn Hargreaves, Virtualizing the GraphicsDevice](https://shawnhargreaves.com/blog/virtualizing-the-graphicsdevice-in-xna-game-studio-2-0.html) | The override does not exist in MonoGame. Compile error. | new XNA102, XNA103 |
| `ResourceManagementMode` (Manual textures and vertex buffers, device reset reloads) | 1.0 only, from the same post | same | Type is not in MonoGame. Constructor overloads that take it fail. | new XNA104 |
| `ResourceUsage` in resource constructors | 1.0 (assumed with the above, see Not verified) | same | Same. | new XNA105 |
| `TextureProcessor`, `SpriteTextureProcessor`, `ModelTextureProcessor` merged into one `TextureProcessor` with parameters; custom processor parameters added | 1.0 to 2.0 | [Shawn Hargreaves, Content processor parameters](https://shawnhargreaves.com/blog/content-processor-parameters-in-xna-game-studio-2-0.html) | The two legacy names are not in the MonoGame pipeline. | importer-map aliases |
| `Draw` is called once per `Update` with a fixed time step | 1.0 to 2.0 | [Shawn Hargreaves, Game timing](https://shawnhargreaves.com/blog/game-timing-in-xna-game-studio-2-0.html) | Behavior only. Frame-rate counters that counted `Draw` calls will read differently. | text in XNA100 |
| `Microsoft.Xna.Framework.Net` and system link added | 2.0 | [Shawn Hargreaves, System link](https://shawnhargreaves.com/blog/system-link-networking-in-xna-game-studio-2-0.html) | A 1.0 project has no networking. 2.0 projects that use it hit XNA010. | existing XNA009, XNA010 (namespace rules, no version gate) |
| Studio support for every Visual Studio 2005 edition | 2.0 | [Announcing XNA Game Studio 2.0](https://learn.microsoft.com/en-us/archive/blogs/xna/announcing-xna-game-studio-2-0) | Explains why 2.0 projects come with `.sln` format 9.00. | detection hint only |
| 4.0 break list: state objects instead of render states, premultiplied alpha, strongly typed vertex buffers, atomic render targets, `Effect.Begin/End` to `EffectPass.Apply`, removal of low-level shader APIs, `EffectPool`, `StateBlock`, `GammaRamp`, `ClipPlane`, triangle fans, point sprites, `Color` byte order | 1.0 to 4.0 (and 3.x to 4.0) | [Breaking changes in XNA Game Studio 4.0](https://shawnhargreaves.com/blog/breaking-changes-in-xna-game-studio-4-0.html) | The same breaks as for 3.x. | XNA022 to XNA043 widened |
| Project upgrade wizard does not change code; only project structure | 3.x to 4.0 | [Upgrading Projects to XNA Game Studio 4.0](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff827883(v=xnagamestudio.41)) | Confirms API fixes are the tool's job. | none |

Adjacent gaps found while reading the 4.0 list, not covered by any current rule and not part of this spec: `EffectPool`, `GammaRamp`, `ClipPlane`, `VertexShader`/`PixelShader`/`Set*ShaderConstant`, `PrimitiveType.TriangleFan`, and the `Color` BGRA to RGBA byte order change. Whether MonoGame provides each of them must be checked before a rule is written. They are listed as follow-up in the tasks.

### Not verified (becomes a check inside a task)

- Whether 1.0 Refresh can be told apart from 1.0. Both use framework version 1.0; treated as one version.
- In which exact version `ResourceUsage` and `ResourceManagementMode` were removed (1.0 or 2.0). The rules list both versions; they are harmless on projects that never used them.
- The base class members of a 1.0 `Game` (for example a `components` container in the 1.0 template). Task 10 migrates the candidate 1.0 projects and reads the compile errors.
- The Zune and Windows Phone project type GUIDs and the 3.x content project GUID. They are not needed: `XnaPlatform` decides the platform.
- Whether a `net8.0-windows` VB project builds. Only DesktopGL was built.
- Behavior of `My.Resources` and `My.Settings` under `MyType Empty`. They are treated as unsupported.

## Part A design: XNA 1.0 and 2.0

### Version model

- `XnaVersion` gets `V10` and `V20`, appended after `V40` so the existing numeric values do not change. Serialization stays string based: `"1.0"`, `"2.0"`.
- `XnaVersionJsonConverter`, `RuleSchema.json` (`xnaVersion` enum) and `XnaVersionDetector.Parse/Format` add the two values.
- The converter error message lists all five values.

### Detection signals

Order stays: `XnaFrameworkVersion`, targets import, framework reference. Two changes and one addition:

1. Import filter: replace `Include.Contains("XNA Game Studio")` with a check that the path contains `Game Studio` and ends with `.targets`. This covers `XNA\Game Studio Express\v1.0\` and `XNA Game Studio\v2.0\`.
2. `Parse` accepts `1.0` and `2.0`.
3. New lowest-priority signal: `ProjectTypeGuids` containing `{9F340DF3-2AED-4330-AC16-78AC2D9B4738}` means 1.0, but only when no other signal exists. It never creates an ANA002 conflict. Reason: the Express template always writes `XnaFrameworkVersion`, so the GUID is a fallback for a hand-edited file.

`XnaProjectDetector.IsXna` also accepts an import whose path matches `\Microsoft.Xna.*.targets` and the Express GUID.

Weak hints are not signals: a missing `TargetFrameworkVersion`, the `.sln` header, and the public key token. The token (`6d5c3888ef60e27d` up to 3.1, `842cf8be1de50553` for 4.0) was seen consistently but the existing fixtures use the 4.0 token on 3.1, so the token is not trusted.

### Reading changes

- New `ProjectItemMatch.Metadata`: optional `IReadOnlyDictionary<string,string>`; every pair must equal the item metadata (case-insensitive). JSON: `"metadata": {"XNAUseContentPipeline": "true"}`. Schema updated. Existing rules do not set it.
- New `InProjectContentReader` in the Analyzer (`Reading/`). It turns `Content` items with `XNAUseContentPipeline=true` into `ContentItem` records, with `Name` from metadata (or file name) and asset path from `Link` when present. `Include` is relative to the project folder and may contain `..\`.
- `ProjectInfo` gets no new field for this. In-project items are added to `ContentItems`. A new `ContentSourceKind` is not exposed in the report; the migrator learns the origin from a new optional record field `ContentItem.InProject` (default false).

Reason for adding a field to `ContentItem` and not to `ProjectInfo`: the content converter already reads `ContentItems` per project but gets file paths by walking `ContentProjects`. It needs to know when a path is relative to the game project instead of a content project file.

### Content conversion for in-project content

`ContentConverter` today finds content project paths in `ProjectInfo.ContentProjects`. For `InProject` items it uses the game project folder as the base and writes the same `Content.mgcb`. Everything else (asset names, processor parameters, fonts, copy, report) is unchanged. `SdkCsprojGenerator.hasContent` becomes `ContentProjects.Count > 0 || ContentItems.Any(i => i.InProject)`.

The importer map gets an `aliases` array:

```json
"aliases": [
  { "importer": "TextureImporter", "processor": "SpriteTextureProcessor", "mapsTo": "TextureProcessor", "note": "XNA 1.0. Merged into TextureProcessor in 2.0." },
  { "importer": "TextureImporter", "processor": "ModelTextureProcessor",  "mapsTo": "TextureProcessor", "note": "XNA 1.0. Merged into TextureProcessor in 2.0." }
]
```

The `ImporterMap` loader treats an alias as standard (so `NonStandardPair` is false and XNA050 does not fire) and exposes `Resolve(importer, processor)` that returns the replacement processor. The parameters of the old processors are not guessed. The content report records the substitution as `Converted with note`, and the exact default parameters of the legacy processors are checked in task 7 against a real 1.0 candidate before the note text is written.

### Rule changes (Part A)

| Rule | Change | Match | Severity | Classification | Versions | Tests |
|---|---|---|---|---|---|---|
| XNA001 | Widen `include` from `*Microsoft.Xna.GameStudio*` to `*Microsoft.Xna.*.targets`. | `projectItem` `Import` | Error | AutoFixable (unchanged) | all | pos: Express `Microsoft.Xna.Common.targets`, 2.0 `...GameStudio.Common.targets`, 3.1 targets. neg: `Microsoft.CSharp.targets`, a VB namespace import `Microsoft.Xna.Framework` |
| XNA002 | Explanation text also names `XNAGlobalContentPipelineAssemblies`, `XNAProjectContentPipelineAssemblies`. No match change. | unchanged | Info | AutoFixable | all | existing cases |
| XNA022 to XNA043 (22 rules) | `xnaVersions` from `["3.0","3.1"]` to `["1.0","2.0","3.0","3.1"]`. | unchanged | unchanged | unchanged | + 1.0, 2.0 | pos: same API on a 2.0 project. neg: same API on a 4.0 project |
| XNA004 | No change. The property is absent in 1.0 and 2.0 projects, so the rule stays silent for them, and the generator always writes `net8.0`. | unchanged | Warning | AutoFixable | all | one case: no `TargetFrameworkVersion`, no finding |
| XNA005, XNA007, XNA008, XNA009, XNA010, XNA050 | No change. They have no version gate or fit the older shapes (nested content in 2.0, `XnaPlatform` `Xbox 360` in 1.0 and 2.0). | unchanged | unchanged | unchanged | all | add one older-shape case each in `rule-cases.json` |
| **XNA100** | New. XNA 1.0 or 2.0 project. Text: older default behavior (premultiplied alpha in 4.0, `Draw` timing), what the migrator does, and that VB does not apply. | `projectProperty` `XnaFrameworkVersion` matches `^v[12]\.0$` | Info | Manual | `["1.0","2.0"]` | pos: `v1.0`, `v2.0`. neg: `v3.1`, `v4.0` |
| **XNA101** | New. Content built inside the game project. | `projectItem` `Content` with metadata `XNAUseContentPipeline=true` | Warning | AutoFixable, `migratorTask` `older-and-vb:8` | all | pos: Express fixture. neg: a `Content` item with `XNAUseContentPipeline=false`; a 4.0 project with only `Compile` items in the content project |
| **XNA102** | New. `LoadGraphicsContent` override. | `methodDeclaration` `LoadGraphicsContent`, namespaces `["Microsoft.Xna.Framework"]` | Error | AutoFixable, `migratorTask` `older-and-vb:9`; `languages ["csharp"]` | `["1.0"]` | pos: override in a `Game` subclass. neg: a method named `LoadContent`; a method with the same name in a class with no XNA using |
| **XNA103** | New. `UnloadGraphicsContent` override. | `methodDeclaration` | Error | AutoFixable, `migratorTask` `older-and-vb:9`; `languages ["csharp"]` | `["1.0"]` | same shape as XNA102 |
| **XNA104** | New. `ResourceManagementMode`. | `type` `ResourceManagementMode`, namespaces `["Microsoft.Xna.Framework.Graphics"]` | Error | Manual; `languages ["csharp"]` | `["1.0","2.0"]` | pos: `ResourceManagementMode.Manual`. neg: a user enum with the same name and no XNA using |
| **XNA105** | New. `ResourceUsage`. | `type` `ResourceUsage`, same namespace | Error | Manual; `languages ["csharp"]` | `["1.0","2.0"]` | same shape as XNA104 |

Legacy processors (`SpriteTextureProcessor`, `ModelTextureProcessor`) get no rule. The importer-map alias handles them and the content report shows the substitution. A rule would add a finding for something the tool fixes silently with a documented note.

### New match type: `methodDeclaration`

Needed because the `member` match is built on member access and invocation nodes, and `LoadGraphicsContent` appears only as a declaration.

```json
{ "type": "methodDeclaration", "name": "LoadGraphicsContent", "namespaces": ["Microsoft.Xna.Framework"], "overrides": true }
```

- `name`: the method name. For C# compared ordinally.
- `namespaces`: at least one must be imported by the file (same gate as `member`).
- `overrides`: when true, the declaration must carry the `override` modifier (C#) or `Overrides` (VB).

Core record `MethodDeclarationMatch(string Name, IReadOnlyList<string> Namespaces, bool Overrides = false)`, converter case, schema entry, and a sink method `MethodDeclaration(name, isOverride, position)`. The match type is language-neutral so the VB walker can add it without a catalog change. It is the only new match type in this spec; every other need fits an existing type.

### Rewriter: `LoadGraphicsContent` and `UnloadGraphicsContent`

New `LoadGraphicsContentRewriter` (registered for XNA102 and XNA103). It parses the file with Roslyn C#, finds an override of the two names whose single parameter is `bool`, and:

- renames the method to `LoadContent` / `UnloadContent` and removes the parameter,
- inserts `const bool <paramName> = true;` as the first statement of the body, using the file's indentation and line ending,
- changes nothing else.

Reason: the 1.0 template guards content with `if (loadAllContent)`. Keeping the parameter name as a constant keeps every body valid, including bodies that pass the flag on, with no flow analysis. The compiler may warn about an unreachable `else`; the migrator accepts that over guessing.

It leaves the file unchanged and adds a marker (rule XNA102 or XNA103) when:

- the method is called by name anywhere in the file (`base.LoadGraphicsContent(x)`, `LoadGraphicsContent(false)`),
- the method has an unexpected parameter list,
- a `LoadContent` method already exists in the same class (two overloads would clash).

This follows the `RenderStateRewriter` rule: change only what is safe, mark the rest.

## Part B design: Visual Basic

### Language model

```csharp
/// <summary>The language of a project's source files.</summary>
[JsonConverter(typeof(SourceLanguageJsonConverter))] // "csharp", "vb"
public enum SourceLanguage { CSharp, VisualBasic }
```

- Lives in `XnaReborn.Core.Models`.
- `ProjectInfo` gets a last parameter `SourceLanguage Language = SourceLanguage.CSharp`. A default value keeps current callers and tests compiling. The report JSON gains `"language"` (schema minor increment; unknown fields are ignored by older readers).
- Language is derived in one place, `ProjectLanguage.FromPath(path)`: `.vbproj` is VB, anything else C#.
- `Rule` gets two optional fields: `languages` (which languages the rule runs for; null means all) and `autoFixLanguages` (for which languages an AutoFixable rule is really fixable; null means all).
- `RuleEngine` (project-level rules) skips a rule when the project language is not in `languages`. The source scanner skips it per file the same way.
- `Scorer` and the planner call `Finding`'s effective classification: AutoFixable becomes Manual when the finding's project language is not in `autoFixLanguages`. The analyzer owns the data, so it needs no reference to the migrator.

### Discovery and project reading

- `ProjectDiscovery` treats `.csproj` and `.vbproj` the same: file extension checks at the four places that use `.csproj`, the folder search pattern, the `.sln` entry filter, and the error message text. The solution line regex already captures any path.
- `XnaProjectDetector.IsXna` works on VB XML as it is, because it looks at element names. It needs no VB branch.
- `ProjectXml.ReadItems`: an `Import` element inside an `ItemGroup` becomes item type `NamespaceImport`. A top-level `Import` stays `Import`. The detector and XNA001 only read `Import`, so VB namespace imports cannot trigger them.
- `ProjectReader`: `DefineConstants` split on `;` and `,`. Entries of the form `NAME=value` keep only `NAME`. Compile expansion is already extension-neutral. `ProjectReadResult.Items` already exposes the namespace imports.
- `ProjectReadResult` gets `ImplicitImports` (a list of namespaces from `NamespaceImport` items) so the scanner needs no knowledge of item types.
- Properties `MyType`, `OptionExplicit`, `OptionStrict`, `OptionCompare`, `OptionInfer`, `RootNamespace`, `StartupObject` are already available through `Properties`. No new parsing is needed.

### Matcher contract (language neutral)

Today `SourceWalker` mixes two jobs: it walks C# nodes, and it applies rule logic (namespace gate, type gate, member shape). Split them:

```csharp
internal readonly record struct MemberUse(
    string Name, string? LeftText, string? Receiver, bool IsInvocation, int? ArgumentCount, int Position);

internal interface ISourceFactSink
{
    void Import(string dottedNamespace, int position);                  // using / Imports
    void Alias(string alias, string target);                            // using X = ... / Imports X = ...
    void NamespaceChain(string dottedText, int position);               // longest pure name chain
    void SimpleName(string name, string? leftText, bool qualified, int position);
    void MemberAccess(MemberUse use);
    void MethodDeclaration(string name, bool isOverride, int position); // new
}

internal interface ISourceWalker
{
    void Walk(SyntaxNode root, ISourceFactSink sink);
}
```

- `RuleHitCollector : ISourceFactSink` holds today's logic from `SourceWalker`: `MatchUsing`, `AddNamespaceHits`, `MatchType`, `MatchMember`, `IsGateOpen`, `IsShapeMatch`, `AddHit` and the `_seen` set. It takes the `SourceRuleIndex`, a `StringComparison`, and the project's implicit imports (added to the import set before the walk).
- `CSharpSourceWalker` is today's `SourceWalker` with the rule logic removed. It keeps the C#-only checks (`IsNonUsePosition`, `IsInNamespaceName`, `TryGetLeft`) because they are syntax questions.
- `VisualBasicSourceWalker` is new.
- `SourceRuleMatcher.Match(tree, rules, implicitImports)` picks the walker with `tree.Options.Language` (`LanguageNames.CSharp` or `LanguageNames.VisualBasic`). The `Func<SyntaxTree, ...>` hook in `SourceScanner` is kept; the new argument is closed over.
- `SourceRuleIndex.Build(rules, ignoreCase)` creates the type and member dictionaries with `OrdinalIgnoreCase` for VB. `Namespaces.Contains`, `Receiver` and `IsNamespaceOrChild` take the same comparison.

Check that proves the refactor is safe: all existing source-matching tests and analyzer snapshots must pass with no diff before any VB code is added (task 14).

### VB walker rules

| VB construct | Roslyn node (verified) | Fact sent |
|---|---|---|
| `Imports A.B` | `SimpleImportsClauseSyntax`, no alias | `Import("A.B")` |
| `Imports X = A.B` | `SimpleImportsClauseSyntax` with alias | `Alias("X","A.B")` and `Import("A.B")` |
| `A.B.C` in a type position | `QualifiedNameSyntax` | `NamespaceChain("A.B.C")`, and `SimpleName("C", "A.B", qualified: true)` for the last identifier |
| `a.b.Name` | `MemberAccessExpressionSyntax` (`SimpleMemberAccessExpression`) | `NamespaceChain` for the pure chain, `MemberAccess` with `LeftText` from the left side, `Receiver` = last identifier left of the dot |
| `fx.Begin(1)` | `InvocationExpressionSyntax` over a member access | `MemberAccess` with `IsInvocation = true`, `ArgumentCount = 1` |
| `fx.Begin` as a statement | `InvocationExpressionSyntax` with null `ArgumentList` | `IsInvocation = true`, `ArgumentCount = 0` |
| `device.Vertices(0).SetSource(a, b, c)` | invocation of a member access whose left side is another invocation | `Receiver` = the invoked name (`Vertices`), because VB writes indexing like a call. Rules that set `receiver` are not affected; XNA035 has none. |
| `With x ... .Prop` | `MemberAccessExpressionSyntax` with null `Expression` | `MemberAccess` with `LeftText = null`, `Receiver = null`. A rule with a `receiver` does not match; a rule without one matches by name and gate. |
| `Dim m As SpriteBlendMode` | `IdentifierNameSyntax` in `AsClause` | `SimpleName("SpriteBlendMode", null, false)` |
| `Protected Overrides Sub X(...)` | `MethodStatementSyntax` with `Overrides` modifier | `MethodDeclaration("X", isOverride: true)` |

Behaviors that differ from C# and are intentional:

- Names compare without case.
- The gate counts project-level imports. For the VB 4.0 samples the project lists `Microsoft.Xna.Framework` and `...Graphics`, so most VB files have an open gate with no `Imports` line.
- Alias use at the start of a qualified name is resolved through the alias table (`Xna.Graphics.Texture2D` becomes `Microsoft.Xna.Framework.Graphics.Texture2D`). The same resolution is applied to C# (`using X = ...`) behind the sink; because it can add hits, it is a separate change with its own snapshot review (task 15).
- Active preprocessor symbols come from `DefineConstants` as `KeyValuePair<string, object>` with value `true`. Inactive `#If` code is not walked, as for C#.

Limits that stay, and are written in the XNA110 explanation: matching is syntax-only, so a user type with the same name can match; a member use without parentheses in expression position (`Dim t = fx.Begin`) is a plain access, so a rule with `kind: Invocation` will not match it; `My.*` is never migrated.

### SDK-style `.vbproj` generation

`SdkCsprojGenerator` is split by a thin language seam, not by copy:

- Common part (stays in the existing class and is renamed `SdkProjectGenerator`): `OutputType`, `TargetFramework`, `AssemblyName`, `RootNamespace`, icon, `DefineConstants` filtering (`DEBUG`, `TRACE`, `XBOX`, `XBOX360`, `ZUNE`, `WINDOWS_PHONE` dropped as today), package references, `MonoGameContentReference`, project references, assembly references with `HintPath`, copy-to-output items.
- C# only: `Nullable` and `ImplicitUsings`.
- VB only: `MyType Empty`, the `Option*` properties, `NamespaceImport` items written back as `<Import Include="..." />`, `StartupObject` only when it names a type.
- Output path: same relative path as the source project, with the `.vbproj` extension.

Example output for the VB 4.0 game sample shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>GameStateManagementSample</AssemblyName>
    <RootNamespace>GameStateManagementSample</RootNamespace>
    <MyType>Empty</MyType>
    <OptionExplicit>On</OptionExplicit>
    <OptionStrict>Off</OptionStrict>
    <OptionCompare>Binary</OptionCompare>
    <OptionInfer>On</OptionInfer>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <DefineConstants>WINDOWS</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.6.1" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.6.1" />
  </ItemGroup>
  <ItemGroup>
    <MonoGameContentReference Include="Content\Content.mgcb" />
  </ItemGroup>
  <ItemGroup>
    <Import Include="Microsoft.Xna.Framework" />
    <Import Include="Microsoft.Xna.Framework.Graphics" />
  </ItemGroup>
</Project>
```

Other VB generator rules:

- Project type GUIDs (`ProjectTypeGuids`), `VBRuntime`, `NoStdLib`, `DocumentationFile`, `NoWarn`, ClickOnce and publish settings are dropped. This extends XNA002, XNA045, XNA047 to VB with no rule change, because those rules match by property and item name.
- `My Project/AssemblyInfo.vb` is kept and sets `GenerateAssemblyInfo=false` (rule XNA114, parallel to XNA046).
- Compile items under `My Project/` with `Designer.vb` or `Application.Designer.vb` in the name are not copied (XNA113). Each is listed in the manifest as "omitted, uses the My application model". If code still uses `My.*`, the build fails and rule XNA112 has already told the user why.
- `SolutionGenerator` takes the project language to write the VB project type GUID `{F184B08F-...}` for `.vbproj` entries and the C# GUID for `.csproj`.
- The shim project (`XnaReborn.Compat`) stays C#. A VB project can reference a C# project, so no VB shim is needed.

### Rewriter contract (language neutral)

```csharp
public sealed record SourceRewriteContext(
    string FilePath,
    string Text,
    IReadOnlyCollection<string> OriginalSymbols,
    SourceLanguage Language = SourceLanguage.CSharp);

public interface ISourceRewriter
{
    string RuleId { get; }
    SourceLanguage Language => SourceLanguage.CSharp;   // default interface member
    SourceRewriteResult Rewrite(SourceRewriteContext context);
}
```

- A default interface member keeps every current rewriter compiling and C#-only.
- `RewriterRegistry` key changes from `RuleId` to `(RuleId, Language)`. `TryGet(ruleId, language, out rewriter)` is added. The old `TryGet(ruleId, out)` stays and means C#. `RuleIds` returns distinct IDs. A duplicate `(RuleId, Language)` still throws.
- `SourceRewriteEngine.Rewrite(filePath, text, symbols, enabledRuleIds, language)`: skips rewriters of another language. The language comes from the file extension in `ProjectMigrator.CompileFiles` (`.cs`, `.vb`).
- `MarkerInserter` becomes `IMarkerInserter` with two implementations. `CSharpMarkerInserter` is today's class; `VisualBasicMarkerInserter` writes `' XNAREBORN: RULE: message`. Tests and UI code that look for the `XNAREBORN:` text (task 18 greps for `Prefix`) get both prefixes from a single `MarkerPrefixes.All`.
- The VB marker goes above the first line of the statement that contains the position. A VB statement can span lines with `_` or implicit continuation, so the insert uses the statement node's `SpanStart`, not the position.

### VB rewriter port decisions

| C# rewriter | Rule | VB decision | Reason |
|---|---|---|---|
| `EntryPointRewriter` | ENTRYPOINT | **Port.** Unwrap `#If WINDOWS OrElse XBOX Then` / `#End If` around `Shared Sub Main` or a `Module` `Sub Main`. Same symbol set, same skip rules (has `#Else`, not platform-only, no end). | The real VB 4.0 sample has exactly this wrapper. Without it the migrated project has no entry point. Directive trivia exists in the VB tree, so the port is small. |
| `SpriteBatchBeginRewriter` | XNA022 | **Port.** Handles `Begin(SpriteBlendMode.X)`, `Begin(SpriteBlendMode.X, SpriteSortMode.Y, SaveStateMode.Z)` and named arguments (`blendMode:=`). The mapping table moves to a shared `BlendModeMap` class used by both languages. | High frequency, simple shape, same mapping. |
| `RenderStateRewriter` | XNA024 | **Marker only** in the first release. `autoFixLanguages ["csharp"]`. | VB code uses `With device.RenderState ... End With` and the leading-dot form often. A safe port needs `With` handling. A stretch task covers the direct-statement subset. |
| `EffectPassRewriter` | XNA039, XNA040 | **Marker only.** `autoFixLanguages ["csharp"]`. | The pattern spans `For Each ... Next`, optional parentheses, and `Begin`/`End` without parentheses. A wrong rewrite breaks drawing silently. A stretch task covers it after real VB samples are scanned. |
| `LoadGraphicsContentRewriter` | XNA102, XNA103 | **Not ported.** Rules are C# only. | No known VB project on XNA 1.0 or 2.0. |

The marker-only VB behavior is implemented as `VisualBasicMarkerRewriter(ruleId, message)`, a generic rewriter that inserts one marker per hit and changes nothing else. It reuses the analyzer's matcher to find the hit positions (the migrator already references the analyzer).

### Rule changes (Part B)

| Rule | Change | Match | Severity | Classification | Languages | Tests |
|---|---|---|---|---|---|---|
| XNA024, XNA039, XNA040 | Add `autoFixLanguages ["csharp"]`. | unchanged | unchanged | AutoFixable for C#, effective Manual for VB | all | pos: VB file with the use gets a Manual effective classification. neg: the same C# use stays AutoFixable |
| XNA022 | Add `autoFixLanguages ["csharp","vb"]` (explicit). | unchanged | unchanged | AutoFixable | all | pos in VB, pos in C# |
| XNA046 | No change (C# glob). | unchanged | | | | |
| **XNA110** | New. Visual Basic project: lists the support limits (syntax-only matching, rewriters for entry point and `SpriteBatch.Begin` only, no `My` namespace). | `projectItem` `Compile` with `include` `*.vb` | Info | Manual | `["vb"]` | pos: VB fixture. neg: C# fixture; a VB project with no `.vb` compile items |
| **XNA111** | New. VB application model (`MyType`). | `projectProperty` `MyType` matches `^(WindowsForms|WindowsFormsWithCustomSubMain|Console|Web|Custom|Windows)$` | Warning | AutoFixable, `migratorTask` `older-and-vb:19` | `["vb"]` | pos: `WindowsForms`. neg: `Empty`; property missing |
| **XNA112** | New. `My` namespace used in code (`My.Application`, `My.Computer`, `My.Settings`, `My.Resources`). | `namespace` `My`, `includeChildren true` | Error | Manual | `["vb"]` | pos: `My.Application.Log.WriteEntry`. neg: `Me.MyField`; a C# file with a `My.Namespace` name |
| **XNA113** | New. `My Project` designer files compiled into the project. | `projectItem` `Compile` with `include` `*My Project*Designer.vb` | Warning | AutoFixable, `migratorTask` `older-and-vb:19` | `["vb"]` | pos: `My Project\Application.Designer.vb`. neg: `My Project\AssemblyInfo.vb`; `Game1.vb` |
| **XNA114** | New. `My Project/AssemblyInfo.vb` duplicates SDK attributes. | `filePattern` `**/My Project/AssemblyInfo.vb` | Error | AutoFixable, `migratorTask` `project-migrator:4.1` extended in `older-and-vb:19` | `["vb"]` | pos: file present. neg: no file |

Project-level rules XNA001 to XNA008, XNA045 to XNA049 run on VB projects unchanged, because they match XML names that are the same in `.vbproj`. XNA005 and XNA049 follow `ProjectReference` to `.contentproj` the same way (verified in the VB 4.0 sample).

`languages` and `autoFixLanguages` are optional rule fields. `RuleSchema.json` adds them as arrays of `"csharp"` and `"vb"`. The catalog loader fails if a rule lists an unknown language.

### Runner

- `GameLocator` and `MigratedCopyGuard` search `*.csproj` and `*.vbproj`. Rules for choosing a game (OutputType `WinExe`/`Exe`, not a library) are unchanged.
- `MsBuildOutputParser`: accept codes that start with `BC` as well as `CS`. Lines that start with `vbc :` have no file and no line; they are kept as project-level errors, as `MSB` lines are today.
- `HintCatalog`: add a hint for `BC30002` on `ApplicationServices` or `My` types ("A `My` namespace type is not available in .NET SDK projects. Replace `My.*` calls with the .NET API; see rule XNA112"), and one for `BC30451` after a removed XNA name pointing to the migration manifest.
- `RuntimeFailureDetector` and the launch flow need no change.

### UI and reports

- Project summary rows show a language badge. The Markdown report adds "Language" to the project table.
- The diff viewer already shows `.vb` files as text. Marker lines use the VB prefix, so the filter that highlights markers reads `MarkerPrefixes.All`.

## Fixtures (all written from scratch, original, repository license)

| Fixture | Purpose |
|---|---|
| `xna10-express-basic` | Express 1.0 C# game. GUID `{9F340DF3-...}`, `v1.0`, Express targets, in-project `Content` items with `SpriteTextureProcessor`, `LoadGraphicsContent`/`UnloadGraphicsContent`, `ResourceManagementMode`, `SpriteBlendMode`. No `TargetFrameworkVersion`. |
| `xna20-vs2005` | 2.0 C# game and a nested content project (type GUID `{96E2B04D-...}`). `.sln` format 9.00. Uses `LoadContent`, `RenderState`, `Effect.Begin`. Windows and Xbox 360 variants of the game project to cover XNA007. |
| `xna10-clean` | An Express project that uses none of the removed APIs. Negative case: only the project-level findings. |
| `vb-xna40-basic` | VB 4.0 game and content project reference, project-level imports, `Program.vb` with the `#If WINDOWS OrElse XBOX Then` wrapper, `My Project/AssemblyInfo.vb`, comma separated `DefineConstants`. |
| `vb-xna31-handmade` | VB 3.1 with plain references, `MyType WindowsForms`, `Application.Designer.vb`, `SpriteBlendMode`, a `With device.RenderState` block, `fx.Begin` without parentheses, a `My.Application` call. |
| `vb-monogame-already` | VB project with MonoGame package reference and `Imports Microsoft.Xna.Framework`. Negative: not detected as XNA. |
| `mixed-cs-vb-sln` | A solution with a C# game and a VB library. Each project gets its own language. |

Each folder has a `README.md` that says "Original synthetic code written for this repository". Existing `rule-cases.json` is used for rule cases. New cases add a `language` field (default `csharp`).

## Testing strategy

- Rule cases: one positive and one negative per new or changed rule. The widened XNA022 to XNA043 get a version-gate case: a 2.0 project triggers, a 4.0 project does not.
- Detector: unit tests per signal, conflict case, GUID fallback, Express path, weak hints produce no conflict.
- Read-only: a hash test runs the analyzer over every new fixture and compares folder hashes before and after.
- Matcher refactor: the existing source-matching tests and snapshots pass without edits, before VB code lands.
- VB walker: one test per row of the walker table, plus case-insensitivity, project-level imports and alias cases.
- Rewriters: before and after text tests for the entry point, `SpriteBatch.Begin`, `LoadGraphicsContent`; preservation test (untouched bytes identical); marker placement in a multi-line statement.
- Generators: Verify snapshots of the generated `.vbproj` and `.sln` per fixture. One integration test, gated by `XNAREBORN_INTEGRATION=1` as for the current migrator integration test, runs `dotnet build` on the migrated `vb-xna40-basic` copy.
- No test needs the network. The integration build restores packages and is skipped when the variable is not set.

## Test candidates (read only; nothing cloned into the repo)

Licenses were read from the GitHub API on 2026-11-11. File headers were not audited. Projects with no license field or `NOASSERTION` are not listed as usable. Copyleft projects may be run through the analyzer locally but their code must never be copied or redistributed with the tool.

### Part A

| Project | License | Version and shape | Use |
|---|---|---|---|
| [jackpal/Dandy-Dungeon](https://github.com/jackpal/Dandy-Dungeon) | Apache-2.0 | Express 1.0. `WindowsDandy` and `Xbox360Dandy` projects, in-project `Content` items with `SpriteTextureProcessor`. Depends on a prebuilt F# DLL. | Detection, XNA007 on the Xbox 360 project, in-project content, legacy processor alias. Expect a partial build. |
| [frham20/SharpEngine](https://github.com/frham20/SharpEngine) | MIT | Express 1.0 library, solution "Visual C# Express 2005". | Detection and XNA001 on Express targets. |
| [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) | MIT (repository). Sample headers say Microsoft; confirm each sample's license before using it as a fixture reference. | 2.0 Movipa sample: Windows and Xbox 360 projects, nested content, XACT, custom pipeline projects. | 2.0 detection, nested content, XNA015/016 and XNA050. |
| [keithjohnston/infiniminer](https://github.com/keithjohnston/infiniminer) and [craftworkgames/infiniminer](https://github.com/craftworkgames/infiniminer) | MIT | 2.0 C# game with networking. | XNA010 on a real 2.0 game; widened-rule checks. |
| [jmonasterio/xgc3](https://github.com/jmonasterio/xgc3) | MIT | 2.0-era solution with the VS 2005 header, Xbox 360 projects. | Solution reading, XNA007. |
| [tommy-xr/quarx](https://github.com/tommy-xr/quarx) | Unlicense | 2.0 engine with Windows and Xbox 360 project variants. | Multi-variant solution. |
| [scoy/KoduGameLab](https://github.com/scoy/KoduGameLab) | MIT | Large; contains 1.0 and 2.0 projects. | Scale and performance check only (about 190 MB). |

### Part B

| Project | License | Shape | Use |
|---|---|---|---|
| [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) VB samples `GSMSample_4_0_Mango_VB`, `CardsStarterKit_4_0_VB` | MIT (repository), see the note above | VB 4.0 Refresh Windows, Xbox 360 and Windows Phone projects, `Program.vb` with the `#If` wrapper, content `ProjectReference`. | Main VB check: detection, reading, entry point rewrite, generated project build. Windows Phone and Xbox 360 projects give XNA007. |
| [SimonDarksideJ/XNAGameStudio](https://github.com/SimonDarksideJ/XNAGameStudio) `PaddleBattle_4_0_Mango_VB` | same | Windows Phone only. | Negative: phone-only project is Unsupported. |
| [VaderConsulting/WindowsVBGame1](https://github.com/VaderConsulting/WindowsVBGame1) | MIT | Hand-made VB on XNA 3.1, `MyType WindowsForms`, no XNA targets. | Detection by reference only, XNA111. |
| [VaderConsulting/XNAEngine](https://github.com/VaderConsulting/XNAEngine) | MIT | VB library on XNA 3.1. | Library output type. |
| [playdeezgames/tggd_hj5](https://github.com/playdeezgames/tggd_hj5) | MIT | VB with MonoGame DesktopGL 3.8.2.1105. | Negative: not XNA. |
| [Pac-Dessert1436/PacDessert1436.MGDesktopGL.ScriptingPack](https://github.com/Pac-Dessert1436/PacDessert1436.MGDesktopGL.ScriptingPack) | MIT | SDK-style VB with MonoGame and project-level imports (`net10.0-windows`). | Reference for the shape of a working VB MonoGame project; negative for detection. |
| [nilllzz/p3d-legacy](https://github.com/nilllzz/p3d-legacy) | GPL-3.0 | Large VB game on XNA 4.0 (`2.5DHero.vbproj`). | Local, read-only stress run only. Copyleft: never copy or redistribute. |

## Reserved identifiers

Sibling specs are written at the same time. To avoid a clash:

- Rule IDs XNA100 to XNA119 belong to this spec (this design uses XNA100 to XNA105 and XNA110 to XNA114).
- Diagnostic codes ANA050 to ANA059 are reserved. No new code is planned.
- Task references use the form `older-and-vb:N`.

## Risks and open questions

1. **Widening is safe, narrowing is not.** An API that does not exist in 1.0 cannot appear in 1.0 code, so listing 1.0 for a rule never adds a false hit except by name collision. The version gate exists to keep 4.0 and MonoGame code clean, and that stays true.
2. **In-project content touches the content converter.** `content-conversion` is the plan of record for that code. Task 8 changes only the source of the file list and adds one field. If another spec changes `ContentConverter` at the same time, merge task 8 last.
3. **VB `My` usage is common in VB samples.** The tool reports it but cannot fix it. Real VB games may therefore stay at "builds with errors" until a person edits them. This is stated in XNA110.
4. **VB rewriters are partial.** XNA024, XNA039 and XNA040 stay as markers. The projected score uses the effective classification, so the VB score will not promise more than the tool does.
5. **Shared types.** `SourceLanguage`, `Rule.Languages`, `Rule.AutoFixLanguages`, `ProjectInfo.Language` and `ContentItem.InProject` change shared records. Task 2 (Part A) and task 12 (Part B) land them first, with defaults, so later tasks do not collide.
6. **Open question for the owner:** should a Windows Phone VB project (for example the PaddleBattle sample) get a friendlier explanation than the generic XNA007 text? The default in this design is no change.

## Sources

Content was paraphrased from these sources, not quoted at length.

- [Announcing XNA Game Studio 2.0](https://learn.microsoft.com/en-us/archive/blogs/xna/announcing-xna-game-studio-2-0)
- [XNA Game Studio 2.0 Released](https://learn.microsoft.com/en-us/archive/blogs/xna/xna-game-studio-2-0-released)
- [Virtualizing the GraphicsDevice in XNA Game Studio 2.0](https://shawnhargreaves.com/blog/virtualizing-the-graphicsdevice-in-xna-game-studio-2-0.html)
- [Content processor parameters in XNA Game Studio 2.0](https://shawnhargreaves.com/blog/content-processor-parameters-in-xna-game-studio-2-0.html)
- [Game timing in XNA Game Studio 2.0](https://shawnhargreaves.com/blog/game-timing-in-xna-game-studio-2-0.html)
- [System link networking in XNA Game Studio 2.0](https://shawnhargreaves.com/blog/system-link-networking-in-xna-game-studio-2-0.html)
- [Wildcard content using MSBuild (Express 1.0 project and targets)](https://shawnhargreaves.com/blog/wildcard-content-using-msbuild.html)
- [Breaking changes in XNA Game Studio 4.0](https://shawnhargreaves.com/blog/breaking-changes-in-xna-game-studio-4-0.html)
- [Upgrading Projects to XNA Game Studio 4.0](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff827883(v=xnagamestudio.41))
- [Game Content Project (XNA Game Studio 3.0 docs)](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb976064(v=xnagamestudio.30))
- [Microsoft XNA Game Studio 4.0 Refresh download page (adds Visual Basic)](https://www.microsoft.com/en-us/download/details.aspx?id=27599)
- [Visual Basic Support in XNA Game Studio (Mango)](https://github.com/SimonDarksideJ/XNAGameStudio/wiki/Visual-Basic-Support-in-XNA-Game-Studio-(Mango))
- [VB Support for the XNA platform (VB team blog)](https://learn.microsoft.com/en-us/archive/blogs/vbteam/vb-support-for-the-xna-platform)
- [VisualBasicSyntaxTree.ParseText](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.visualbasic.visualbasicsyntaxtree.parsetext)
- [VisualBasicParseOptions constructor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.visualbasic.visualbasicparseoptions.-ctor)
- [Microsoft.CodeAnalysis.VisualBasic 4.14.0 on NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.VisualBasic/4.14.0)
- [MonoGame: Migrating from XNA](https://github.com/MonoGame/docs.monogame.github.io/blob/main/articles/migration/migrate_xna.md/)
