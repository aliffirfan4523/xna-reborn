# Day-by-Day Plan: XNA Reborn

**Start:** Sun 8 Nov 2026
**Deadline:** Sat 28 Nov 2026 (about 3 weeks)
**Rule:** aim to submit on Fri 27 Nov. Check the official page for the exact deadline time and timezone, and keep 28 Nov as pure buffer.

## Principles

1. **Vertical slice first.** One real old project going from failing to running beats ten half-done features.
2. **Migrate the demo project by hand first.** Doing it manually on day 1 to 2 shows which rules are really needed.
3. **Specs drive the work.** Run each spec's tasks in Kiro in order, and commit `.kiro/` from day 1.
4. **Feature freeze on 24 Nov.** After that: polish, docs, video.

## Week 1: Foundation and Analyzer (8 to 14 Nov)

| Date | Focus | Done when |
|---|---|---|
| Sun 8 | Create repo, solution, copy `.kiro/` (specs, steering, hooks). Pick 3 candidate demo projects and check each license. | Repo builds, `.kiro/` committed, 3 candidates listed with licenses |
| Mon 9 | **Migrate the best demo candidate by hand** to MonoGame. Write down every problem you hit. | Game runs; a list of real issues exists |
| Tue 10 | Analyzer spec tasks 1 to 3: models, fixtures, discovery | Discovery tests pass |
| Wed 11 | Tasks 4 to 5: project file reading, XNA version detection, rule catalog and engine | First rules fire on fixtures |
| Thu 12 | Task 6: Roslyn source scanning | API usage findings with file and line |
| Fri 13 | Tasks 7 to 9: scoring, reports, safety test | Markdown and JSON reports generated |
| Sat 14 | Task 10 checkpoint, buffer, fix gaps. Add rules from your manual-migration list. | Analyzer reports look right on all fixtures |

## Week 2: Migrator and Runner (15 to 21 Nov)

| Date | Focus | Done when |
|---|---|---|
| Sun 15 | Migrator tasks 1 to 3: output guard, planner, dry run | Guard and plan tests pass |
| Mon 16 | Task 4: SDK-style csproj and solution generation | Generated project restores |
| Tue 17 | Tasks 5 to 6: entry point rewriter, rewriter framework, diffs | Rewriters tested on fixtures |
| Wed 18 | Task 7: content conversion (`.mgcb`, asset copy, manual flags) | Content builds for the demo project |
| Thu 19 | Tasks 8 to 9: write step, manifest, **checkpoint: migrate and build the demo project** | Demo project builds from the migrated copy |
| Fri 20 | Runner tasks 1 to 5: guard, environment, process runner, build and parse | Build output parsed into diagnostics |
| Sat 21 | Runner tasks 6 to 8: hints, launcher, first-launch confirmation | **Checkpoint: game launches from code** |

## Week 3: UI, Polish, Submit (22 to 28 Nov)

| Date | Focus | Done when |
|---|---|---|
| Sun 22 | UI tasks 1 to 3: shell, open screen, report screen | Report screen shows real analysis |
| Mon 23 | UI tasks 5 to 6: migration setup and diff view | Plan and diff visible |
| Tue 24 | UI task 7 to 8: build and run screen, **end-to-end checkpoint. FEATURE FREEZE.** | Full flow works on the demo project |
| Wed 25 | Stretch (pick ONE): XNA 3.1 to 4.0 rewriters, or dead-API shims, or a second demo project. Fix bugs. | One extra capability works, or bugs fixed |
| Thu 26 | README, screenshots, tests green, `dotnet format`, clean up, rehearse demo | README complete, tests pass |
| Fri 27 | Record and edit demo video, write submission text, test a fresh clone, **submit** | Submission filed |
| Sat 28 | Buffer only. Re-check the submission page, video link, repo visibility, license. | Everything verified |

## Cut list (drop in this order if behind)

1. Dead-API shims
2. XNA 3.1 to 4.0 rewriters
3. AI-assisted repair and batch mode
4. Side-by-side diff (fall back to unified diff text)
5. UI polish (fall back to a simpler layout)

**Never cut:** analyzer report, migrated copy, build and run, the read-only safety guarantees, the demo video.

## Daily habits

- Start each day by running the full test suite.
- Commit at least once a day with a clear message.
- Keep a short `docs/KIRO_LOG.md` noting what Kiro specs, steering, and hooks did for you. It becomes the "how I used Kiro" section.
- Record 10 seconds of screen capture whenever something impressive works, so you have footage for the video.

## Risks to watch

| Risk | Early warning | Response |
|---|---|---|
| Demo project is hard to migrate | Manual migration on 9 Nov takes more than a day | Switch to a simpler project right away |
| Content pipeline surprises | Content build fails on 18 Nov | Use a demo project with standard textures and fonts only |
| Package or SDK version issues | Restore fails | Pin versions in one config file and test on a clean machine |
| Running out of time | Missing a checkpoint by more than a day | Apply the cut list immediately |
