# Structure

- `src/XnaReborn.Core`: shared models and services
- `src/XnaReborn.Analyzer`: read-only analysis
- `src/XnaReborn.Migrator`: creates migrated copies
- `src/XnaReborn.Runner`: build and launch
- `src/XnaReborn.Repair`: AI-assisted repair (optional)
- `src/XnaReborn.App`: Avalonia UI
- `rules/`: rule catalogs
- `tests/fixtures/`: small sample XNA projects with expected results

New code goes in the project that owns the responsibility. UI code never contains migration logic.
