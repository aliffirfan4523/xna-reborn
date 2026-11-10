# XNA Reborn: test candidates

Old XNA games with public source, for testing the migrator.
Found by web search on 2026-11-11. Only repo pages and search snippets were read.
Nothing was cloned, built or run. "Platform" and "License" come from repo text.
Check the license before copying code or assets. Fill in the Result column as you test.

## A. Built for Xbox 360, Zune or Windows Phone (hardest, best tests)

| # | Game | Platform | XNA | License / notes | Source | Result |
|---|------|----------|-----|-----------------|--------|--------|
| 1 | Switch! | Xbox 360 | 3.1 | Not stated. Has a MonoGame port to compare: [switch-mono](https://github.com/elennick/switch-mono) | https://github.com/elennick/switch-xna | |
| 2 | Battle Bubbles (Four Corners) | Xbox 360 | ? | GPLv3 | https://github.com/dudash/battlebubbles | |
| 3 | Grey Infection | Xbox 360 | 4.0 | MIT (per GitHub). Assets not included. Repo says it may not compile for Windows. Analyzer score 37 (projected 47). Engine: [TLSA.Engine](https://github.com/VisualStudioEX3/TLSA.Engine) | https://github.com/VisualStudioEX3/Grey-Infection | |
| 4 | Flotilla | Xbox 360, PC | 4.0 | Code is zlib license. Art and audio not free. Author already ported it to FNA. Modded fork: [flotilla-non-terminal-adventures](https://github.com/BurkusCat/flotilla-non-terminal-adventures) | https://github.com/blendogames/flotilla | |
| 5 | XNACPC (Amstrad CPC emulator) | Xbox 360, PC | ? | Not stated | https://github.com/gavinpugh/xnacpc | |
| 6 | Old School Adventure | Xbox, Windows Phone | ? | All rights reserved. Repo is already moved to MonoGame | https://github.com/ZenithMoonStudios/OldSchoolAdventure | |
| 7 | Quatrix, Snakez, Quatrix HD, Combat Effective | Zune | ? | Not stated. Four games in one repo | https://github.com/Extron/XNAZuneGames | |
| 8 | Super Space Escape | Windows Phone 7 | ? | "Open source", license not stated | https://github.com/jkeech/Super-Space-Escape | |
| 9 | Solitaire | Windows Phone 7 | ? | Microsoft sample | https://github.com/microsoft/solitaire-wp | |
| 10 | Match'em Poker | Windows Phone | ? | Microsoft sample | https://github.com/microsoft/matchem-poker-wp | |
| 11 | Zune game book samples | Zune | 3.0 | Book code (Apress) | https://github.com/Apress/zune-game-dev-using-xna-3.0 | |
| 12 | 2D RPG | Xbox, Windows, Zune | 2.0 to 3.1 | Not stated | https://github.com/BlainMaguire/2DRPG | |
| 22 | Short Circuit (Xbox 360 port) | Xbox 360 (XBLIG, shut down) | ? | Authors released source so fans can build it. License not stated | https://github.com/magius96/ShortCircuit_XBox360 | |
| 23 | The Great Paper Adventure | PC, Xbox 360 | ? | Not stated | https://github.com/thibault-p/The-Great-Paper-Adventure | |

## B. Windows or multi-platform XNA (easier, good for volume)

| # | Game | Notes | Source | Result |
|---|------|-------|--------|--------|
| 13 | Zombie Smashers XNA4 | MIT | https://github.com/glitchculture/ZombieSmashersXNA4 | |
| 14 | Snails | 2D platformer, GPL | https://github.com/xesf/SnailsXNA | |
| 15 | Lemma | 3D first-person parkour. Code open, some assets | https://github.com/et1337/Lemma | |
| 16 | Infiniminer | Multiplayer mining game (Zachtronics). Fork by nkast | https://github.com/nkast/Infiniminer | |
| 17 | DwarfCorp | 3D colony game | https://github.com/Blecki/dwarfcorp | |
| 18 | Angry Ninjas | Uses Cocos2D-XNA, not plain XNA | https://github.com/xamarin/AngryNinjas | |
| 19 | Bastion source (fan project) | Name suggests a fan project. Unverified | https://github.com/LeftRight92/BastionSource | |
| 20 | Pirate-Treasure-Hunter | Already migrated by hand. Use as the baseline | (already in the project notes) | |
| 21 | Meteorocks | Backup test. BSD 2-clause | (already in the project notes) | |
| 24 | Ending (XNA version) | Roguelike puzzle game. License not seen | https://github.com/PumpkinPaul/EndingXNA | |
| 25 | NumbeRun | Puzzle game from 2007. Very old XNA and Visual C# 2005 | https://github.com/marcomontalbano/numberun | |
| 26 | Tower Defense Game | Small XNA student-style game | https://github.com/wahabjawed/tower-defense-game | |
| 27 | PokÃ©mon3D (legacy) | Game, client and server. Framework not confirmed (XNA or MonoGame). Check first | https://github.com/nilllzz/p3d-legacy | |

## C. Big sample bundles (many projects in one repo)

| Name | Notes | Source |
|------|-------|--------|
| XNA Game Studio archive | 100+ articles and samples. Platformer, Blackjack, Racing Game and more. Builds on Windows. Xbox and phone deploy is dead | https://github.com/SimonDarksideJ/XNAGameStudio |
| Catapult Wars Lab (inside the archive) | Full 2D catapult game for Windows Phone 7 | https://github.com/SimonDarksideJ/XNAGameStudio/wiki/Home/3590f4c351b1288c777266260b91bdf24430e626 |
| Same archive, other mirrors | Some samples already moved to MonoGame | https://github.com/tgc-utn/xna-game-studio , https://github.com/rbwhitaker/MonoGameSamples |

## D. Where to find more

- GitHub topic "xna": 394 public repos when checked. https://github.com/topics/xna
- Sleepwalker and other Swf2XNA games: https://github.com/debreuil/Swf2XNA/wiki

## Out of scope

- Shooter (tutorial code, 33 MB build output, no asset terms). Rejected earlier.
- Engines only (Cocos2D-XNA, WitolProductions XNA-Engine). They are not games.
- DishSoap (https://github.com/Hdmaniak/DishSoap). It decompiles the commercial Xbox 360 game The Dishwasher: Dead Samurai. No source from the owner. Skip.
- Z0MB1ES-godot. A Godot port of an XNA game, not XNA source.

## How to test one

1. Clone it outside the XNA Reborn repo. Do not bundle it.
2. Run the analyzer. Write down the score.
3. Run the migrator. Write down warnings and errors.
4. Build and launch it. Write down what breaks.
5. Put the result in the table above.

