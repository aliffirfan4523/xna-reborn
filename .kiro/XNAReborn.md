# XNA Reborn: Bring Microsoft XNA Games Back to Life

**Hackathon:** Kiroween
**Category:** Resurrection
**Tagline:** Your old XNA game, running on modern Windows in minutes.

---

## 1. Submission Description

Microsoft ended XNA in 2013, leaving thousands of games, tutorials, and learning projects stranded on a toolchain that no longer installs or builds on modern machines.

**XNA Reborn** is a desktop migration and compatibility studio that brings those projects back to life. Instead of rebuilding XNA from scratch, it builds on **MonoGame**, the open source successor that keeps XNA's API and namespaces. XNA Reborn analyzes an old project, explains exactly what will and won't carry over, applies automatic fixes, and builds and launches the result on a modern .NET and Windows setup.

Where a migration guide hands you a checklist, XNA Reborn does the work: a compatibility report, one-click project conversion, content and audio handling, shims for dead APIs, and AI-assisted repair for the cases rules can't cover.

---

## 2. Why It Fits "Resurrection"

- XNA is a dead framework with a large, real backlog of stranded software.
- The project does not recreate the past; it reanimates existing work on a living foundation.
- The outcome is visible and emotional: an old game that wouldn't build now runs.

---

## 3. Core Features

### MVP (must work for the demo)
1. **Project analyzer.** Scan an XNA 3.1/4.0 project and produce a compatibility report: detected XNA version, target platforms, API usage, content pipeline items, and blockers.
2. **Findings classification.** Every finding is tagged auto-fixable, needs manual work, or unsupported.
3. **One-click migration.** Create a migrated copy (never modify the original):
   - Convert the old `.csproj` to an SDK-style .NET project
   - Replace XNA references with MonoGame packages
   - Apply source fixes for known breaking changes
4. **Content conversion.** Convert the old content project to a MonoGame `Content.mgcb` pipeline.
5. **Build and run loop.** Build the migrated project, show errors, and launch the game.
6. **Report export.** Save the report as Markdown and JSON.

### Stretch
7. **Shims for dead APIs.** Stub or replace Xbox Live / Games for Windows Live, Zune, Windows Phone, and storage APIs behind a clear compatibility layer.
8. **XACT audio replacement.** Detect XACT projects and convert their audio to plain assets.
9. **Shader (.fx) assessment.** Flag effects that need porting and, where possible, retarget them.
10. **AI-assisted repair.** For errors rules can't fix, propose a patch with a before/after diff and ask for approval.
11. **Compatibility score and history.** Show a score per project and how it improved after each fix.
12. **Batch mode.** Analyze a folder of projects and rank them by migration difficulty.

---

## 4. Demo Script (about 90 seconds)

| Time | Action | What judges see |
|---|---|---|
| 0:00 | Open a real old XNA project | Build fails on a modern machine |
| 0:10 | Press Analyze | Compatibility report: findings grouped as auto-fixable, manual, unsupported |
| 0:30 | Press Migrate | Migrated copy created, diff view of changes |
| 0:50 | Build and run | Game launches on modern Windows |
| 1:10 | Show a harder project | AI-assisted repair suggests a fix, you approve it |
| 1:25 | Show the score | Compatibility rose from 38% to 94% |

Use projects you are allowed to use, such as sample projects and starter kits with permissive licenses. Check each license before including it, and do not redistribute commercial games.

---

## 5. Tech Stack

| Layer | Choice | Notes |
|---|---|---|
| Language | C# on .NET 8 | Same ecosystem as XNA and MonoGame |
| Desktop UI | Avalonia UI (or WPF if Windows-only is fine) | Avalonia gives a modern look and optional cross-platform later |
| Code analysis and rewriting | Roslyn (`Microsoft.CodeAnalysis`) | Syntax and semantic analysis, rewriters for source fixes |
| Project files | `System.Xml.Linq` + MSBuild APIs | Read and rewrite old `.csproj` and `.contentproj` |
| Target framework | MonoGame (DesktopGL or WindowsDX) | Templates and NuGet packages |
| Content pipeline | MonoGame Content Builder (`dotnet mgcb`) | Generates `Content.mgcb` |
| Rules | JSON or YAML rule catalog | Data-driven so new rules need no code changes |
| Process execution | `System.Diagnostics.Process` | Run `dotnet build` and launch the game |
| Reports | Markdown + JSON via `System.Text.Json` | Exportable results |
| AI repair (stretch) | Anthropic API via HTTP client | Suggests patches with diffs; never applies without approval |
| Testing | xUnit + approval/snapshot tests (Verify) | Fixture projects with known outcomes |
| Packaging | `dotnet publish` single-file | Easy to demo |

---

## 6. Architecture

```
┌──────────────────────────────────────────────┐
│            Avalonia Desktop UI               │
│  Open project · Report · Diff · Build/Run    │
└───────────────┬──────────────────────────────┘
                │
┌───────────────▼──────────────────────────────┐
│               Studio Core Services           │
│  Orchestration · Progress events · Settings  │
└──┬───────────┬────────────┬──────────────┬───┘
   │           │            │              │
┌──▼──────┐ ┌──▼───────┐ ┌──▼─────────┐ ┌──▼──────────┐
│Analyzer │ │Migrator  │ │Content &   │ │Build & Run  │
│(Roslyn  │ │(csproj + │ │Audio       │ │(dotnet CLI) │
│ + rules)│ │ rewriters│ │conversion  │ │             │
└──┬──────┘ └──┬───────┘ └──┬─────────┘ └──┬──────────┘
   └───────────┴────────────┴──────────────┘
                     │
            ┌────────▼─────────┐
            │  Rule Catalog    │
            │  (JSON/YAML)     │
            └──────────────────┘
                     │
            ┌────────▼─────────┐
            │ AI Repair (opt.) │
            └──────────────────┘
```

**Key principle:** the original project is never modified. All work happens in a migrated copy.

---

## 7. Project Structure

```
xna-reborn/
├── .kiro/
│   ├── specs/
│   ├── steering/
│   ├── hooks/
│   └── settings/
├── src/
│   ├── XnaReborn.Core/          # models, rule catalog loader, services
│   ├── XnaReborn.Analyzer/      # project + source analysis (Roslyn)
│   ├── XnaReborn.Migrator/      # csproj conversion, rewriters, content
│   ├── XnaReborn.Runner/        # build and launch
│   ├── XnaReborn.Repair/        # AI-assisted repair (stretch)
│   └── XnaReborn.App/           # Avalonia UI
├── rules/
│   └── xna-to-monogame.json
├── tests/
│   ├── XnaReborn.Tests/
│   └── fixtures/                # small sample XNA projects with expected results
├── README.md
└── XnaReborn.sln
```

---

## 8. Kiro Setup

Keep the `.kiro` directory committed and not gitignored, and check the official Kiroween rules for exact requirements.

### 8.1 Specs (`.kiro/specs/`)

1. `project-analyzer`: read projects, scan APIs, classify findings, report (included in this package)
2. `project-migrator`: create migrated copy, SDK-style csproj, MonoGame references, source rewrites
3. `content-conversion`: content project to `Content.mgcb`, spritefonts, textures, models
4. `build-and-run`: build, parse errors, launch, map errors to fixes
5. `dead-api-shims`: Xbox Live, Zune, Windows Phone, storage replacements (stretch)
6. `audio-xact` (stretch)
7. `ai-repair` (stretch)
8. `studio-ui`: Avalonia screens for report, diff, and run

Write requirements as testable statements, for example: *"When a project references `Microsoft.Xna.Framework.GamerServices`, the analyzer shall report a finding with severity Blocker and classification Manual."*

### 8.2 Steering files (`.kiro/steering/`)

| File | Contents |
|---|---|
| `product.md` | What XNA Reborn is, the Resurrection pitch, demo flow |
| `tech.md` | C#/.NET 8 conventions, Roslyn usage, nullable enabled, async patterns |
| `structure.md` | Solution layout and where new code goes |
| `safety.md` | Never modify the original project, always work on a copy, never overwrite without confirmation, never upload user source code without explicit opt-in |
| `rules.md` | How to add a migration rule: data first, code only when needed, every rule needs a fixture and a test |

### 8.3 Hooks (`.kiro/hooks/`)

- **On save of a rule file:** validate JSON/YAML against the rule schema and run rule tests.
- **On save of analyzer or migrator code:** run the fixture-based tests.
- **On new fixture project added:** regenerate the expected report snapshot and ask for review.
- **Pre-commit check:** `dotnet format --verify-no-changes` and `dotnet build -warnaserror`.

### 8.4 MCP servers (`.kiro/settings/`)

Optional:
- **Filesystem MCP** scoped to the fixtures folder for inspecting sample projects.
- **Docs lookup** for MonoGame and Roslyn APIs.

### 8.5 Workflow suggestions

- Build the analyzer first, because everything else depends on its findings model.
- Grow the rule catalog from real failures: migrate a fixture, note what broke, add a rule, repeat.
- Keep a short log of how specs, steering, and hooks shaped decisions for the submission write-up.

---

## 9. Build Order

1. Solution setup and the findings and report models
2. Project analyzer on 2 to 3 fixture projects (spec 1)
3. Rule catalog with the first 15 to 20 rules
4. Migrator: SDK-style csproj + MonoGame references on one fixture
5. Source rewriters for the most common breaking changes
6. Content project conversion
7. Build and run loop
8. Avalonia UI: open, analyze, report
9. UI: migrate, diff, run
10. **Checkpoint: the minimum winning demo** (one real old project goes from failing to running)
11. Stretch: dead-API shims, XACT, AI repair, batch mode, score history
12. Polish, README, demo video

---

## 10. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Content pipeline and audio are the hardest parts | Pick demo projects that avoid XACT and custom processors; document limits honestly |
| Existing guides and tools overlap | Lead with the studio experience: report, auto-fix, diff, and run in one place |
| Old shaders (`.fx`) may not port cleanly | Flag them in the report and treat porting as stretch |
| Licensing of sample projects | Use only projects with clear permissive licenses and credit them |
| Scope creep | Hold the MVP line at step 10 of the build order |

---

## 11. Submission Checklist

- [ ] Category set to Resurrection
- [ ] Description names the dead technology and the living replacement
- [ ] Public repo with an open source license
- [ ] `.kiro` directory included (specs, steering, hooks)
- [ ] Demo video showing a real project going from failing to running
- [ ] README with setup and a sample project to try
- [ ] Write-up describing how Kiro was used
