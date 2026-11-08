# multi-project-sln

Synthetic fixture for the project analyzer.

- Original code written for this repository. No third-party code or assets.
- Released under the repository license.

## Purpose

A solution with three real XNA Game Studio 4.0 projects: a Windows game, a class library the game references, and a nested content project. The solution also lists a fourth project, `Missing/Missing.csproj`, that does not exist on disk. That entry is intentional.

The project is never built. The asset is a placeholder text file.

## What the analyzer should find

- One solution with four project entries.
- Three projects that exist: a game, a library, and a content project nested in the game.
- The game references the library and is linked to its content project.
- One project entry whose file is missing from the folder.
