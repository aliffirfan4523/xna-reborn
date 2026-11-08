# XNA Reborn

A desktop tool that analyzes old Microsoft XNA game projects, migrates them to MonoGame, then builds and runs the result.

Built for the Kiroween hackathon, Resurrection category.

> Work in progress. A fuller README draft is in `.kiro/README.md` and will replace this stub before submission.

## Build and test

Requires the .NET SDK (8 or newer) and the .NET 8 runtime.

```
dotnet build
dotnet test
```

## Layout

```
src/XnaReborn.Core       shared models and services
src/XnaReborn.Analyzer   read-only analysis
rules/                   rule catalogs (data)
tests/                   unit tests and fixture projects
.kiro/                   specs, steering, hooks
```

More projects (Migrator, Runner, Repair, App) are added as their specs are implemented.

## Safety

The original XNA project is never modified. All output goes to a new folder.

## License

Not chosen yet. See `LICENSE`.
