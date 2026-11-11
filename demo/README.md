# XNA Reborn demo scenes

Scripted faux-UI scenes for the hackathon demo, built with [Stageshow](https://github.com/benjitaylor/skills) (skill folder `stageshow/`, MIT, Copyright (c) 2026 Benji Taylor). They are promo visuals, not the app. Nothing here touches `src/` or the Avalonia UI.

Screen names and labels come from `src/XnaReborn.App` (Open, Report, Migration setup, Review changes, Build and run, Summary). The numbers are in `src/facts.ts`: score 81 to 91 (projected), 21 of 21 assets built, 0 warnings, 0 errors, 0 hand edits, taken from the work brief and `.kiro/specs`. Findings and diff lines are placeholder bars, no invented counts. The game picture is drawn placeholder art, not Pirate-Treasure-Hunter art.

## Scenes

- `?scene=still-1` to `?scene=still-6`: one still per screen.
- `?scene=demo`: the cursor demo of the whole flow, about 24 seconds, loops.
- No `scene`: a gallery of all of them.
- Add `&still=1` to see the reduced-motion poster. Real `prefers-reduced-motion` is respected too.

## Install and run

Needs Node 20.19 or newer.

```
cd demo
npm install
npm run dev        # http://127.0.0.1:5173
npm run build      # type check and production build into dist/
```

## Film or export

Filming uses Google Chrome through `playwright-core` (set `CHROME_PATH` for another Chrome). It works offline. With `npm run dev` running:

```
# PNG of one screen
node scripts/film.mjs "http://127.0.0.1:5173/?scene=still-2&still=1" --select "#example" --still --out shots/report.png

# contact sheet and motion check of the demo
node scripts/film.mjs "http://127.0.0.1:5173/?scene=demo" --select "#example" --gate --every 1000 --count 24 --out shots/demo.png
```

To record video, open `?scene=demo` full screen and use any screen recorder. The `shots/` and `dist/` folders are ignored by git.

## Accessibility

Each scene has a `role="img"` wrapper with alt text. Text colors are set to pass 4.5:1 on white. Reduced motion shows the poster (Summary).

## Licences and attribution

- `kit/` and `scripts/` are copied from Stageshow. Its licence is `LICENSE-stageshow` and its notices are `NOTICE-stageshow.md`.
- `kit/brands/` keeps its own `SOURCES.md` and `LICENSE-*.txt` files. The scenes use none of those brand marks, and no app icons or photos are bundled.
- No fonts or images are fetched at runtime. The scenes make no network calls.
- No third-party game art, audio or screenshots are used.
