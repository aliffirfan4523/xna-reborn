# Choosing and Vetting the Demo Project

The demo project decides whether your video lands. Choose it first and test it before writing much code.

## Ideal profile

| Criterion | Ideal |
|---|---|
| XNA version | 4.0 (3.1 only if you plan to build the rewriters) |
| Target | Windows |
| Code projects | One game project (plus at most a content project) |
| Content | Standard textures, sprite fonts, WAV or MP3 audio; source assets present |
| Avoid for the main demo | XACT audio, custom processors, Xbox Live, networking, skinned models |
| Size | Small (under about 50 MB) so cloning and demos are quick |
| Visual impact | Clear 2D visuals that look good in a short video |
| Startup | Opens a window and shows something within a few seconds |
| License | Clear license that allows redistribution (for example a permissive license file) |
| Current state | Fails to build or open on a modern machine today |

## Where to look

- Official XNA samples and starter kits, if you can find copies with a clear license.
- Open source XNA games on GitHub with a license file.
- Your own old XNA projects. Using your own code removes licensing risk entirely.

Do not use projects with no license, commercial games, or code copied from tutorials without permission.

## Vetting steps (timebox: about 4 hours per candidate)

1. **License check.** Find the license file and note what it allows. If unclear, drop the candidate or ask the author.
2. **Record the failure.** Try to open and build the original on your machine and capture the error. This is your "before" footage.
3. **Inventory.** List code projects, content items, importers and processors, audio types, and any GamerServices, Net, or Storage usage.
4. **Manual migration.** Create a new MonoGame project, bring over code and content, and fix errors by hand. Write down every problem and how you solved it. Each one is a candidate rule.
5. **Confirm it runs.** The game should reach its main screen and behave correctly.
6. **Score the candidate** using the table below.

## Scoring sheet

| Criterion | 0 | 1 | 2 |
|---|---|---|---|
| License is clear and permissive | no | unclear | yes |
| Fails to build today (strong "before") | builds | partly | fails |
| Standard content only | many custom parts | some | all standard |
| Manual migration effort | over 1 day | half a day | under 4 hours |
| Visual appeal in video | low | medium | high |
| Few dead APIs | many | some | none |

Pick the highest score. 10 or more is a strong demo candidate.

## Two-project strategy (recommended)

1. **Hero project:** clean success, used for the main demo.
2. **Honest project:** has a few manual items (for example XACT audio or a custom processor), used briefly to show the manual-work flags and `// XNAREBORN:` markers. This proves the tool is truthful about limits.

## Record-keeping

For each candidate keep a short file in `samples/<name>/`:
- `LICENSE` (copied from the original) and a `SOURCE.md` with the origin URL and date retrieved
- `NOTES.md` with manual migration problems and fixes
- Screenshots of the before and after

## Red flags

- You cannot find the license.
- Manual migration needs more than a day.
- The game depends on online services or hardware you do not have.
- The content is only compiled `.xnb` files with no sources.
