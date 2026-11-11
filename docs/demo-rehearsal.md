# Demo rehearsal checklist

For the owner. Follow it top to bottom to record the Kiroween demo (category: Resurrection). The recording itself is not done yet. This page only gets you ready.

Numbers below come from the repo: `demo/src/facts.ts`, `.kiro/specs/content-conversion/tasks.md`, `.kiro/specs/project-migrator/tasks.md` and `.kiro/XNAReborn.md`. Re-check them on screen during the rehearsal. If the app shows something else, say what the app shows.

| What | Value |
|---|---|
| Demo game | Pirate-Treasure-Hunter (XNA 4.0) |
| Score before | 81 |
| Score after automatic fixes | 91 (projected) |
| Assets built | 21 of 21 |
| Build | 0 warnings, 0 errors |
| Hand edits | 0 |

## 1. Before you record

- [ ] Free space on C: is not low. (The build, the NuGet cache and the screen recording all write to C:. No size is measured in the repo, so pick a margin you trust.) Check: `Get-PSDrive C`.
- [ ] .NET 8 SDK is installed. Check: `dotnet --list-sdks`. The app also checks this on the Build and run screen.
- [ ] Update the repo: `git switch master` then `git pull`. Leave nothing uncommitted.
- [ ] Build once: `dotnet build` in `F:\Project Folder\xna-reborn`. It must show 0 warnings.
- [ ] Close other apps. Turn off notifications (Windows Focus). Quit chat, mail and the browser.
- [ ] Clone the demo game outside the repo, for example `C:\demo\Pirate-Treasure-Hunter`: `git clone https://github.com/vnavarro/Pirate-Treasure-Hunter C:\demo\Pirate-Treasure-Hunter`. Do not copy it into the repo.
- [ ] Pick an output folder outside the repo that does not exist yet or is empty, for example `C:\demo\pirate-out`. (A parent NuGet.config or Directory.Build.props inside the repo can break the restore.)
- [ ] Start the app: `dotnet run --project src/XnaReborn.App`.
- [ ] Window: the default is 1180 x 760 (minimum 760 x 520). Keep it. Record at 1920 x 1080 with the window centered, or maximize it and record the window only.
- [ ] Theme: the app follows Windows (`RequestedThemeVariant="Default"`). Pick one theme in Windows before you start and keep it for the whole take. Light is easier to read when compressed. Dark looks better on a dark page. Screens for both are in `docs/ui/` (`after-light-*.png`, `after-dark-*.png`).
- [ ] Clear Recent projects if it shows other projects.
- [ ] Do a silent dry run of the whole path once. Then delete the output folder before the real take.

## 2. Click path

| # | Screen | Do | Check on screen |
|---|---|---|---|
| 1 | Open | Click **Browse folder...** and pick `C:\demo\Pirate-Treasure-Hunter` (or drop the folder). | "Reading your project. Nothing is written yet." |
| 2 | Report | Wait for the report. | Compatibility score 81. Findings listed. |
| 3 | Report | Point at the **Start here** tip and the **Who fixes it** split (the migrator does it, you fix these by hand, no modern equivalent). | Tip is visible. |
| 4 | Report | Click **Continue to migration setup**. | |
| 5 | Migration setup | Type or browse the output folder `C:\demo\pirate-out`. Keep **DesktopGL**. Switch **Substitute fonts that only XNA installed** on. Leave the other options off. | Plan shows Required steps. |
| 6 | Migration setup | Optional: click **Preview** first (writes nothing). Then click **Migrate**. | Migration finishes. |
| 7 | Diff | Show **Original** and **Migrated** side by side. Step through with **Next marker** if any marker exists. Click **Continue to Build and run**. | |
| 8 | Build and run | Click **Build and Run**. SDK check and build run. | 0 warnings, 0 errors. |
| 9 | Build and run | A dialog asks **Run this game?** Click **Run game**. | Launch step reads "Waiting for your OK" until you answer. |
| 10 | Game | The game window opens. Play a few seconds. Close it (or click **Stop**). | Game runs. |
| 11 | Build and run | Click **Go to Summary**. | |
| 12 | Summary | Show Before 81 and After 91 (projected), 21 of 21 assets, 0 hand edits. | Matches the table above. |

Note: the Summary score is projected from the automatic fixes. Say "projected", not "measured".

## 3. Timed script

Total about 90 seconds, matching the demo table in `.kiro/XNAReborn.md`. Trim the waits in editing, not by rushing.

| Time | Screen | Say |
|---|---|---|
| 0:00 to 0:10 | Open | "XNA ended in 2013. This game will not build on a modern machine. I open it in XNA Reborn." Pick the folder. |
| 0:10 to 0:25 | Report | "The score is 81. Every finding says who fixes it: the migrator, me, or nobody." Show **Start here**. |
| 0:25 to 0:40 | Migration setup | "DesktopGL, fonts replaced. The copy goes to a new folder. The original is never changed." |
| 0:40 to 0:55 | Migrate, Diff | "Migrate. Here is every change, original on the left, migrated on the right." |
| 0:55 to 1:10 | Build and run | "Build: no warnings, no errors. The game starts only after I say yes." Click **Run game**. |
| 1:10 to 1:20 | Game | Let it run. Say nothing. |
| 1:20 to 1:30 | Summary | "81 to 91, projected. 21 of 21 assets. Zero hand edits. Dead framework, running game." |

Rehearse the timing with the scripted scenes in `demo/` (`npm run dev`, then `?scene=demo`, about 24 seconds, see `demo/README.md`). They are promo visuals, not the app.

## 4. Second short path (only if the owner has confirmed the launch)

Do this only after a person has run the sample and confirmed it launches well. Until then, skip it. Record the result in the rehearsal table.

- **Flash sample.** Follow `docs/flash-demo-script.md` (about 3 minutes). Game: `tests/fixtures/flash/flx-platformer`, written for this project, so it can be shown. For a short cut, use steps 1 (Open), 5 (Build and run) and 6 (Summary) only.
- **SharpDX sample.** Use the migrated SharpDX sample the owner confirmed. Same click path as section 2. State its name and licence on screen.

## 5. Risks and fallbacks

| Risk | What to do |
|---|---|
| Pirate audio licences are unclear (2 mp3s from "chocobo eater", wavs from PacDV that disallow redistribution). | Do not show or publish its audio. Mute the game in the recording, or cut the sound in editing. For a shared version, replace all audio with CC0 sounds. |
| A parent `NuGet.config` can change package sources and break restore (`.kiro/specs`, migrator task 8.3). | Keep the output folder outside the repo. If restore fails, check parent folders for `NuGet.config`. |
| C: runs low on space. | Check before every take. Delete old output folders. |
| Build fails during the take. | Stop. Read **Problems and hints** on Build and run. Re-record. Keep a good dry-run recording as the fallback. |
| Game does not open. | Run `dotnet run` inside `C:\demo\pirate-out` to see console output. |
| Output folder already has files. | The app asks first (safety rule 3). Use a fresh folder to avoid the dialog on camera. |
| Backup game. | Meteorocks (BSD 2-clause, no audio, art origin not verified). Do not bundle it. |

## 6. Rehearsal table

Fill in one row per rehearsal. Pass means the whole click path ran with no retake.

| Date | Take | Theme | Open | Report | Setup | Diff | Build | Game runs | Summary 81 to 91 | Time | Pass or fail | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| | 1 | | | | | | | | | | | |
| | 2 | | | | | | | | | | | |
| | 3 | | | | | | | | | | | |
| | Flash (optional) | | | | | | | | | | | |
| | SharpDX (optional) | | | | | | | | | | | |

## 7. Say out loud (safety)

- [ ] "The original project is never modified. Everything goes to a copy in a new folder."
- [ ] "Nothing starts until I confirm. The game runs only after I click Run game."
- [ ] "Nothing is uploaded. Your code and assets stay on this machine."
- [ ] "An existing output folder is never overwritten without my OK."

## 8. After you record

- [ ] Export the video (MP4, 1080p) to a folder outside the repo. Do not commit it.
- [ ] Play it once, with sound on, from start to end.
- [ ] Check there is no Pirate audio, no personal paths, no notifications, and no other windows.
- [ ] Add captions that match the script in section 3. Say "projected" for 91.
- [ ] Credit the licensed samples on screen or in the description:
  - Pirate-Treasure-Hunter by vnavarro, MIT licence (text in its README). Audio not shown.
  - Stageshow scenes (if used): MIT, Copyright (c) 2026 Benji Taylor, see `demo/LICENSE-stageshow` and `demo/NOTICE-stageshow.md`.
  - Any other sample: name, author, licence.
- [ ] Do not show or ship a commercial game.
- [ ] Delete `C:\demo\pirate-out` if you no longer need it. The clone is the user's own copy and stays outside the repo.
- [ ] Tick the recording item in `.kiro/specs/studio-ui/tasks.md` (task 10) and the demo rehearsal in `.kiro/specs/build-and-run/tasks.md` (task 11) when the recording exists.
