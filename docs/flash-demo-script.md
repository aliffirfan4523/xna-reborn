# Demo script: a Flixel 2.x game comes back to life

About 3 minutes. Game: `tests/fixtures/flash/flx-platformer` (written for this project, so it can be shown and shipped).

Before you start
- Build the app once (`dotnet build`) and have an empty folder ready for the output, for example `C:\demo\platformer-out`.
- Have `tests/fixtures/flash/flx-swf-only` ready as the "no source" example.

Steps

1. Open. Pick the `flx-platformer` folder. Say: "An AS3 game with Flixel 2.x, source only, no Flash Player needed." The app shows the project kind as Flash and names the entry class.
2. Report. Show the score and the findings by rule id (FL...). Point at one or two: what converts by itself, what gets a marker. Say: "The score tells you how much work is left before anything runs."
3. Migration setup. Choose the empty output folder. Say: "The original folder is never changed. A non-empty output folder is refused unless you confirm."
4. Migrate, then Diff. Open `PlayState` and show the AS3 line next to the C# line, and one `// XNAREBORN: FL...` marker with the original AS3 text.
5. Build and run. Press Build and run. The game window opens: menu, then the play state with the player, enemies and bullets. Move and jump, shoot, hit an enemy.
6. Summary. Show the marker counts by rule id and the list of approximated behaviors.
7. Close with the "no source" example: open `flx-swf-only`. The report explains that a lone `.swf` is never opened or decoded, and what to provide instead.

If something goes wrong
- Build fails: open the build log on the Build and run screen. The hint under the error names the FL rule that explains it.
- No window appears: run `dotnet run` inside the output folder to see the console output.
- Keep a recording of a good run as the fallback.

Not shown, on purpose: tilemaps, emitters, E4X XML and pixel access. They are second wave and leave markers.
