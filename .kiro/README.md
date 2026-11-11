# XNA Reborn

**Bring Microsoft XNA games back to life on modern Windows.**

XNA Reborn is a desktop migration and compatibility studio. It analyzes an old Microsoft XNA project, explains what will and will not carry over, migrates it to a modern .NET and [MonoGame](https://monogame.net) project, and builds and launches the result. It does not recreate XNA; it builds on MonoGame, the open source successor that keeps XNA's API.

Built for the **Kiroween** hackathon, **Resurrection** category.

## Why

Microsoft ended XNA in 2013. Many games, tutorials, and learning projects can no longer be built on modern machines. Migration guides exist, but they are checklists. XNA Reborn does the work and shows its reasoning.

## Features

- **Analyze:** compatibility report with score, findings grouped as auto-fixable, manual, or unsupported, each with file, line, and explanation.
- **Migrate:** creates a migrated copy with an SDK-style project, MonoGame references, rewritten source for known breaking changes, and a rebuilt content pipeline.
- **Preview:** migration plan with per-change exclusions and a diff view before anything is written.
- **Build and run:** builds with the .NET SDK, groups errors by fix hint, and launches the game.
- **Manifest:** a full record of what was changed, skipped, or needs manual work.

## Safety

- The original project is never modified. All output goes to a new folder.
- Builds and runs only work on migrated copies.
- You confirm before the game's code runs for the first time.
- Nothing is uploaded anywhere. (AI-assisted repair, if enabled, is opt-in per project.)

## Quick start

> Verify these commands against your final project setup.

Requirements: Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
git clone <repo-url>
cd xna-reborn
dotnet build
dotnet run --project src/XnaReborn.App
```

Then:
1. Open a sample project from `samples/`.
2. Click **Analyze** and review the report.
3. Choose an output folder and click **Migrate**.
4. Click **Build**, then **Run**.

## How it works

```
Open project → Analyzer (Roslyn + rule catalog) → Report
            → Migrator (SDK csproj, rewriters, content) → Migrated copy + manifest
            → Runner (dotnet build, diagnostics, hints) → Game launch
```

Rules live in `rules/xna-to-monogame.json`, so new detections need no code changes.

## Project structure

```
src/XnaReborn.Core       shared models
src/XnaReborn.Analyzer   read-only analysis
src/XnaReborn.Migrator   creates migrated copies
src/XnaReborn.Runner     build and launch
src/XnaReborn.App        Avalonia desktop UI
rules/                   rule catalogs
tests/                   unit tests and fixture projects
.kiro/                   specs, steering, hooks
```

## Built with Kiro

This project was built spec-first with Kiro. The `.kiro/` folder contains the specs (requirements, design, tasks) for each component, steering files for product, tech, structure, safety, and rules, and hooks that run tests and format checks. See `docs/KIRO_LOG.md` for how they shaped the build.

## Limitations

- Custom content processors, XACT audio, and `.fx` shaders are flagged as manual work.
- Projects with only prebuilt `.xnb` files and no source assets cannot be fully rebuilt.
- Xbox 360, Zune, and Windows Phone targets are migrated as Windows desktop projects only.
- Xbox Live and networking APIs have no modern equivalent.

## Credits and licenses

Sample projects are included under their original licenses with attribution in `samples/*/LICENSE`. XNA is a trademark of Microsoft. MonoGame is an open source project; see its license for details.

## License

[Choose an OSI-approved license, for example MIT, and add a LICENSE file.]

- Flash to MonoGame (Flixel 2.x source projects): see specs/flash-to-monogame (requirements, design, tasks, implementation notes).
