# Demo Video Script (about 3 minutes)

> Check the official rules for the maximum length and required format. Aim for 2:30 to 3:00, and keep it tight.

## Preparation

- Use a clean machine or fresh user profile if possible, with the .NET 8 SDK installed.
- Have the demo project ready, and confirm it fails to build on the modern setup (record that failure).
- Close notifications, set screen resolution to 1080p, increase UI font size.
- Rehearse the flow 3 times. Record voiceover separately if you prefer clean audio.

## Script

| Time | On screen | Voiceover |
|---|---|---|
| 0:00 to 0:15 | Title card, then an old XNA game screenshot or the original project failing to build | "XNA was retired in 2013. Thousands of games are stuck on a toolchain that no longer builds. This is one of them." |
| 0:15 to 0:30 | Terminal or IDE showing the build failure | "On a modern machine, it won't even restore. Let's resurrect it with XNA Reborn." |
| 0:30 to 1:00 | Open the project in XNA Reborn; click Analyze; show the report and score | "XNA Reborn analyzes the project and explains what will migrate automatically, what needs manual work, and what's unsupported. Every finding links to a file and line." |
| 1:00 to 1:30 | Migration setup, plan checklist, diff view | "Before anything is written, you can see the plan and a diff. The original project is never touched; everything goes to a new copy." |
| 1:30 to 2:00 | Click Migrate, then Build; show grouped diagnostics if any appear | "It generates a modern project on MonoGame, rewrites known breaking changes, rebuilds the content pipeline, and builds it. Errors are grouped with fix hints." |
| 2:00 to 2:20 | First-launch confirmation, then the game starting | "Running old code is a decision, so it asks first. And here it is: the game is running again on modern Windows." |
| 2:20 to 2:40 | Summary screen: score before and after, manifest | "Everything is recorded in a manifest: what changed, what was skipped, and what still needs a human." |
| 2:40 to 3:00 | Show `.kiro/` folder: specs, steering, hooks | "We built it spec-first with Kiro: specs for every component, steering for safety rules, and hooks that run tests as we work. XNA Reborn: bring your old games back to life." |

## Footage checklist

- [ ] Original project failing to build
- [ ] Analyze screen with score
- [ ] Plan checklist and diff view
- [ ] Migration progress
- [ ] Build with grouped diagnostics (if any)
- [ ] Confirmation dialog and game running
- [ ] Summary and manifest
- [ ] `.kiro/` folder and one spec open
- [ ] Final title card with repo link

## After recording

- Add captions or on-screen labels for key steps.
- Check audio levels and that no private paths, tokens, or personal data are visible.
- Upload and confirm the link works while logged out.
