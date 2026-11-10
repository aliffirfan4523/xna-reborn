# XNA test candidates

Public GitHub repos that may be old XNA projects, for testing XNA Reborn.
Links only. Nothing here is cloned or bundled (see `.kiro` safety rule 5 and 6).

Collected 2026-11-11 with the GitHub search API. Metadata only: nothing was cloned, built or run.

## Files

| File | What it is |
|------|------------|
| `CURATED.md` | 28 hand-picked games, checked by reading repo pages. Includes Xbox 360, Zune and Windows Phone games. |
| `LIST-permissive.md` | 159 C# game candidates with MIT, BSD, Apache, zlib, MS-PL or similar |
| `LIST-copyleft.md` | 75 with GPL, LGPL, MPL or similar |
| `LIST-no-license.md` | 1055 with no license. Test locally only. Never bundle. |
| `LIST-other.md` | 40 with other licenses. Read before use. |
| `VERIFIED.md` | 78 permissive and copyleft repos whose code mentions `Microsoft.Xna.Framework`, with analyzer results |
| `xna-verified.csv` | Code-search hit count for all 234 permissive and copyleft candidates |
| `analyzer-summary.jsonl`, `analyzer-reports/` | XNA Reborn analyzer results on 14 shallow clones |
| `xna-repos-classified.csv` | All 6992 repos found, with tier, kind and license class |
| `harvest.ps1`, `classify.ps1`, `make-lists.ps1`, `verify.ps1`, `make-verified.ps1` | Scripts that rebuilt these files. Run in that order. The raw `xna-repos.csv` is not committed. |

## Numbers

| Group | Repos |
|-------|-------|
| Found in total | 6992 |
| Tier 1: has an XNA, XBLIG or Zune topic | 478 |
| Tier 2: "xna" in name or description | 1776 |
| Tier 3: only a README mention (weak, not listed) | 4738 |
| In the LIST files (tier 1 and 2, C#, game or unknown) | 1329 |

## Verification (2026-11-11)

Code search checked the 234 permissive and copyleft candidates for `Microsoft.Xna.Framework`.
The 1,055 no-license candidates were not checked.

| License group | Checked | Text found |
|---------------|---------|------------|
| Permissive | 159 | 50 |
| Copyleft | 75 | 28 |

MonoGame and FNA use the same namespace. So a hit is not proof of old XNA.

## Analyzer run on 14 clones

Shallow clones, outside this repo, read-only analysis with `rules/xna-to-monogame.json` (48 rules, `origin/master` at 95d1178).
The runner was a throwaway console app. The repo has no CLI.

| Repo | XNA project? | Score | Projected | Findings |
|------|--------------|-------|-----------|----------|
| Spike3D | yes | 19 | 33 | 63 |
| ZombieSmashersXNA4 | yes | 31 | 41 | 57 |
| Grey-Infection | yes | 37 | 47 | 71 |
| XNAMiner | yes | 38 | 50 | 57 |
| XNAWindowsLeapMotion | yes | 52 | 62 | 17 |
| Space-Shooter-Game | yes | 57 | 67 | 16 |
| XNA-GPUDust | yes | 57 | 67 | 10 |
| TouchAndPlay | yes | 60 | 70 | 10 |
| EvoNet | yes | 87 | 97 | 8 |
| Isometric-RTS | yes | 90 | 100 | 14 |
| MagicalLife | no | - | - | - |
| opengta2 | no | - | - | - |
| xnamugen | no | - | - | - |
| Chapter-Master-Restoration | no | - | - | - |

10 of 14 are real XNA projects. The 4 "no" repos use the XNA namespace through MonoGame or another engine.
Builds and launches were not tried. Scores are analyzer scores only.

## Read this first

- `kind` is a guess from the name and description. Expect tools, engines and MonoGame ports in the lists.
- Tier 2 repos may only mention XNA. Check for `Microsoft.Xna.Framework` in the code.
- Platform (Windows, Xbox 360, Zune, phone) is not in the metadata. `CURATED.md` has it for 28 games.
- License comes from GitHub's detection. `NONE` means no license file was found, so all rights are reserved.
- GitHub search returns up to 1000 results per query. The year split helps, but some repos are missed.

## Testing one

1. Clone it outside this repo.
2. Run the analyzer. Write down the score.
3. Run the migrator. Write down warnings and errors.
4. Build and launch it.
5. Write the result in the `Result` column.
