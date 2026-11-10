# Requirements: SharpDX Toolkit to MonoGame

## Introduction

The SharpDX Toolkit was a high-level game framework on top of SharpDX (a managed DirectX 11 wrapper). Its API was modeled on XNA: a `Game` class, a `GraphicsDeviceManager`, `SpriteBatch`, `Texture2D`, `SpriteFont`, `BasicEffect`, a `ContentManager`. The Toolkit and SharpDX are archived and deprecated, and the last Toolkit package on NuGet is 2.6.3. Projects written on it are stranded the same way XNA projects are.

This spec lets XNA Reborn take a Toolkit game and produce a migrated copy on MonoGame 3.8.6.1, SDK-style, `net8.0`. It reuses the analyzer, migrator, rewriter framework, content converter and runner. It does not recreate the Toolkit. Where the Toolkit has no MonoGame equivalent, the tool says so instead of guessing.

The original project is never modified. All work goes to a migrated copy.

Primary sources are listed in `design.md` ("Sources").

## Glossary

- **Toolkit project**: a `.csproj` that references `SharpDX.Toolkit*` assemblies, imports `SharpDX.Toolkit.targets`, or has `ToolkitFxc`, `ToolkitFont`, `ToolkitModel`, `ToolkitTexture` or `ToolkitAudio` items.
- **Source framework**: which framework a project was written for. Today `Xna`; this spec adds `SharpDxToolkit`.
- **Toolkit content item**: an item with one of the five Toolkit build actions in the game project. There is no separate content project.
- **`.tkb`**: the Toolkit's compiled asset format. `ContentManager` appends `.tkb` to asset names that have no extension.
- **Toolkit effect**: a `.fx` file compiled by `ToolkitFxc` (the `tkfxc` compiler). It uses Toolkit-specific technique syntax.
- **Rename map**: a JSON data file that lists Toolkit namespaces, types and members and their MonoGame names.
- **SDX rule**: a rule in the Toolkit catalog, with an id from `SDX001`.

## Requirements

### R1: Detection
**User story:** As a developer, I want XNA Reborn to recognize my SharpDX Toolkit game, so I do not have to tell it what the project is.

Acceptance criteria:
1. WHEN a project file references an assembly whose name starts with `SharpDX.Toolkit`, THE analyzer SHALL classify the project as a Toolkit project.
2. WHEN a project file imports a target file whose name matches `SharpDX.Toolkit.targets` or `SharpDX.targets`, or has an item of type `ToolkitFxc`, `ToolkitFont`, `ToolkitModel`, `ToolkitTexture` or `ToolkitAudio`, THE analyzer SHALL classify the project as a Toolkit project.
3. WHEN a `packages.config` next to the project lists a `SharpDX.Toolkit*` package, THE analyzer SHALL classify the project as a Toolkit project.
4. THE analyzer SHALL record the source framework on each discovered project and in the analysis report (`Xna` or `SharpDxToolkit`).
5. WHEN a project matches both XNA and Toolkit signals, THE analyzer SHALL report the project as mixed and SHALL run both rule catalogs.
6. WHEN a project references only low-level `SharpDX` assemblies without `SharpDX.Toolkit*`, THE analyzer SHALL NOT classify it as a Toolkit project and SHALL say so in the discovery result.
7. WHEN no XNA or Toolkit project is found, THE analyzer SHALL return a result that names both frameworks in its reason.
8. THE analyzer SHALL open every file read-only, and a test SHALL prove file hashes are unchanged after analysis of a Toolkit fixture.

### R2: Rule catalog and findings
**User story:** As a developer, I want a report that says what carries over and what does not.

Acceptance criteria:
1. THE rules for Toolkit projects SHALL live in their own catalog file, `rules/sharpdx-toolkit-to-monogame.json`, with ids `SDX001` and up.
2. THE analyzer SHALL run the Toolkit catalog only on projects classified as Toolkit projects, and the XNA catalog only on XNA projects.
3. THE catalog SHALL use only existing `match.type` values. Where the existing `member` match needs more power, the change SHALL be a backward-compatible optional field, not a new type.
4. EACH rule SHALL have a positive fixture test and a negative fixture test.
5. EACH AutoFixable rule SHALL name the migrator spec task that fixes it (`migratorTask`).
6. THE score SHALL use the same severity weights and classification factors as the XNA catalog unless the Toolkit catalog sets its own.
7. THE report SHALL show findings from the two catalogs in one list, grouped by rule category, so the UI needs no special case.
8. IF a Toolkit type or member name is also used by an unrelated type, THEN the rule MAY produce a false positive, and the explanation SHALL say that matching is by syntax (the existing "Source matching limits").

### R3: Project conversion
**User story:** As a developer, I want the old project file replaced with a modern one.

Acceptance criteria:
1. THE migrator SHALL generate an SDK-style `.csproj` for `net8.0` for each Toolkit code project, reusing the existing project generator.
2. THE migrator SHALL remove every `SharpDX*` assembly reference, every import of `SharpDX*.targets`, the NuGet restore guard targets that mention SharpDX, and the Toolkit build actions from the new project.
3. THE migrator SHALL add the MonoGame package selected by the migration options and the content builder package when the project has content.
4. WHEN the migration options do not choose a target, THE migrator SHALL default a Toolkit project to `WindowsDX`, because Toolkit games are Direct3D 11 on Windows and their effects are HLSL.
5. THE migrator SHALL preserve assembly name, root namespace and output type.
6. THE migrator SHALL NOT copy `packages/`, `packages.config`, `bin/` or `obj/` into the migrated copy, and SHALL list `packages.config` as removed in the manifest.
7. IF a project is a Windows Store or Windows Phone project, THEN THE migrator SHALL stop for that project and report it as unsupported (rule `SDX004`).

### R4: Source rewriting
**User story:** As a developer, I want known renames done for me and everything else marked.

Acceptance criteria:
1. THE migrator SHALL rewrite `using` directives, fully qualified names and namespace aliases from the Toolkit namespaces to the MonoGame namespaces, driven by the rename map.
2. THE migrator SHALL rewrite the Matrix factory methods, `MathUtil` members, graphics state collections, `Set*State` calls, one-argument `SetRenderTargets` calls, `BackBuffer.Width` and `BackBuffer.Height`, `ToDisposeContent`, and `KeyboardManager` usage, as listed in the rule table in `design.md`.
3. THE migrator SHALL apply a rewriter only for findings classified AutoFixable and only when the plan item is not excluded.
4. WHEN a pattern is ambiguous, THE rewriter SHALL leave the code unchanged and insert a `// XNAREBORN: <rule id>: <what to do>` marker.
5. THE migrator SHALL insert a marker for every Manual finding at the first line where it was found.
6. THE rewriters SHALL preserve formatting and comments in code they do not change, and files with no changes SHALL be copied byte for byte.
7. THE rename map SHALL be data, so adding a rename needs no code change.
8. THE migrator SHALL NOT change the meaning of handedness: IF the source uses a left-handed matrix factory, THEN THE migrator SHALL NOT rewrite it and SHALL mark it.

### R5: Content conversion
**User story:** As a developer, I want my fonts, textures, models and sounds to load again.

Acceptance criteria:
1. THE analyzer SHALL read Toolkit content items from the game project into the same content model the XNA content reader fills, with the build action as the item kind.
2. THE converter SHALL convert each `ToolkitFont` XML file (`TkFont` root) into a MonoGame `.spritefont` file, mapping font name, size, spacing, line spacing, kerning, style, character regions and default character.
3. THE converter SHALL write `Content.mgcb` entries for `ToolkitTexture` items that use a texture format the MonoGame 3.8.6.1 content builder accepts, and SHALL copy and flag the rest as manual work.
4. THE converter SHALL write `Content.mgcb` entries for `ToolkitModel` items whose format the MonoGame content builder imports, and SHALL copy and flag the rest as manual work.
5. THE converter SHALL write `Content.mgcb` entries for `ToolkitAudio` items in `.wav` format, and SHALL copy and flag the rest as manual work.
6. THE converter SHALL NOT convert Toolkit effects. It SHALL copy each `.fx` file, list it as manual work, and explain that Toolkit effect syntax does not compile with the MonoGame effect compiler (rule `SDX013`).
7. THE converter SHALL keep asset names loadable: a Toolkit asset name without an extension SHALL stay without an extension.
8. IF an asset is loaded with an explicit extension in source code, THEN THE converter SHALL record it in the content report so a person can check the name.
9. IF only `.tkb` files exist and no source asset is found, THEN THE converter SHALL report them as unsupported prebuilt content (rule `SDX015`) and SHALL copy them unchanged.
10. THE converter SHALL write content into the migrated copy only, and SHALL NOT run the Toolkit compilers.

### R6: Build, run and repair hints
**User story:** As a developer, I want to know why the migrated game fails to build.

Acceptance criteria:
1. THE runner SHALL build the migrated copy the same way it builds any migrated copy.
2. WHEN a build error mentions `SharpDX`, `RectangleF`, `Color4`, `GameSystem`, `ToolkitFxc` or an unconverted effect, THE runner SHALL attach a fix hint that names the matching SDX rule.
3. THE runner SHALL NOT upload source code or assets anywhere. AI repair stays off by default and needs a per-project opt-in.

### R7: Reporting and UI
**User story:** As a developer, I want to see that the tool understood my project.

Acceptance criteria:
1. THE analysis report and the migration manifest SHALL show the source framework.
2. THE manifest SHALL list every SDX rule that fired, its classification, the files changed, the files marked, and the content converted or left for manual work.
3. THE UI SHALL show the source framework as a label and SHALL read all of it from the report. The UI SHALL NOT contain migration logic.

### R8: Safety and licensing
**User story:** As a maintainer, I want the tool to stay safe and the repo to stay clean.

Acceptance criteria:
1. THE tool SHALL NOT modify the original project, and SHALL refuse an output folder that is inside the source or non-empty without confirmation.
2. THE repo SHALL NOT contain source or assets copied from a candidate game. Fixtures SHALL be original and synthetic.
3. THE candidate games SHALL be used read-only from shallow clones outside the repo, and only games with a permissive license SHALL be used.
4. ANY test SHALL work with no network access.

### R9: Scope limits
**User story:** As a maintainer, I want the limits written down, so nobody expects a full port.

Acceptance criteria:
1. THE tool SHALL support SharpDX Toolkit 2.x desktop projects (`SharpDX.Toolkit` 2.4.0 to 2.6.3).
2. THE tool SHALL NOT migrate code that calls `SharpDX.Direct3D11`, `SharpDX.DXGI` (beyond format names), `SharpDX.Direct2D1` or `SharpDX.XAudio2` directly. It SHALL flag it (rules `SDX005` to `SDX008`).
3. THE tool SHALL NOT migrate Windows Store, Windows Phone 8 or Silverlight-hosted Toolkit games (rule `SDX004`).
4. THE tool SHALL NOT translate Toolkit effect files (rule `SDX013`).
5. THE tool SHALL NOT promise visual equality. Handedness, sampler state, premultiplied alpha and effect behavior can differ, and the report SHALL say which rules touch them.
