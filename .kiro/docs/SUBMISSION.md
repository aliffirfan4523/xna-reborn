# Kiroween Submission Text: XNA Reborn

> Fill in the bracketed items with your real results before submitting. Do not publish numbers you have not measured.

**Category:** Resurrection
**One-liner:** Your old XNA game, running on modern Windows in minutes.

## Inspiration

Microsoft retired XNA in 2013, but thousands of games, student projects, and tutorials were built on it. Many cannot be opened on a modern machine today. The framework is dead, but its spirit lives on in MonoGame. We wanted a tool that bridges the gap and makes the work of the past playable again.

## What it does

XNA Reborn is a desktop migration studio. You point it at an old XNA project and it:

1. **Analyzes** the project and produces a compatibility report with a score, listing what migrates automatically, what needs manual work, and what is unsupported.
2. **Migrates** it into a new folder as a modern .NET project on MonoGame, rewriting known breaking changes and rebuilding the content pipeline. The original is never touched.
3. **Builds and runs** the result, turning raw compiler errors into grouped issues with fix hints.
4. **Records** every change in a manifest so nothing happens silently.

In our demo, [project name] went from failing to build to running on Windows [in X minutes], with the compatibility score rising from [X]% to [Y]%.

## How we built it

- **C# on .NET 8** with **Roslyn** for code analysis and rewriting, and **Avalonia** for the desktop UI.
- A **data-driven rule catalog** (JSON) so new migration rules need no code changes.
- A **two-phase migrator**: all changes are computed in memory first, enabling dry run, diff preview, and safe failure.
- A **build-and-run component** that parses MSBuild and content builder output and matches it against a hint catalog.

## How we used Kiro

- **Specs:** every major component (analyzer, migrator, build-and-run, studio UI) started as a Kiro spec with requirements, design, and tasks. [Describe one concrete example, such as how a requirement changed the design or caught an edge case.]
- **Steering files:** product, tech, structure, safety, and rules files kept every generated change consistent, especially the rule that the original project is never modified.
- **Hooks:** hooks ran tests when rules or code changed, checked formatting, and reviewed new fixture projects for license attribution.
- **Spec-driven iteration:** [Describe what you built through specs versus conversational coding, and what worked better.]

## Challenges

- **Content pipeline and audio** are the hardest parts of migration; we handled standard assets automatically and flagged custom processors and XACT audio as manual work instead of hiding them.
- **Ambiguous code patterns:** when a rewrite cannot be done with confidence, the migrator leaves the code unchanged and adds a marker rather than guessing.
- **Trust:** running old game code is a risk, so we added a migrated-copy-only guard and an explicit first-launch confirmation.
- [Add your own real challenge from the build.]

## Accomplishments we're proud of

- A real old XNA project going from failing to running in the demo.
- A rule catalog that grows from real failures.
- A strict safety model: read-only analysis, copy-only migration, no uploads.
- [Add: test counts, number of rules, number of projects migrated, if measured.]

## What we learned

- [Add real lessons, for example how spec-first planning helped or what surprised you about XNA to MonoGame differences.]

## What's next

- Dead-API shims for storage and profile services
- XACT audio conversion
- Shader (`.fx`) porting assistance
- Batch analysis of project folders to rank migration difficulty
- Optional, opt-in AI-assisted repair with reviewable diffs

## Built with

C#, .NET 8, Roslyn, Avalonia, MonoGame, xUnit, Kiro

## Links

- Repo: [URL]
- Demo video: [URL]
