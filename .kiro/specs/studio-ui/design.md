# Design: Studio UI (Avalonia, C# / .NET 8)

## Overview

`XnaReborn.App` is an MVVM Avalonia application. ViewModels call services that wrap the core libraries (`Analyzer`, `Migrator`, `Runner`). Views are thin XAML.

## Project layout

```
src/XnaReborn.App/
├── App.axaml
├── Program.cs
├── Services/
│   ├── IAnalysisService.cs        # wraps ProjectAnalyzer
│   ├── IMigrationService.cs       # wraps ProjectMigrator
│   ├── IRunService.cs             # wraps ProjectRunner
│   ├── IDialogService.cs          # file pickers, confirmations
│   └── RecentProjectsStore.cs
├── ViewModels/
│   ├── MainViewModel.cs           # navigation + workflow state
│   ├── OpenProjectViewModel.cs
│   ├── ReportViewModel.cs
│   ├── MigrationSetupViewModel.cs
│   ├── DiffViewModel.cs
│   ├── BuildRunViewModel.cs
│   └── SummaryViewModel.cs
├── Views/
│   └── (one AXAML view per ViewModel)
├── Controls/
│   ├── ScoreGauge.axaml
│   ├── SeverityBadge.axaml
│   └── DiffViewer.axaml
└── Styles/
```

## Key dependencies

| Package | Use |
|---|---|
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` | UI framework and theme |
| `CommunityToolkit.Mvvm` | Observable properties and commands |
| `Microsoft.Extensions.DependencyInjection` | Service wiring |
| `AvaloniaEdit` | Code and diff text display |
| `DiffPlex` | Diff computation (shared with the migrator) |

## Workflow state

```
Open → Analyzing → Report → MigrationSetup → (DryRun/Diff) → Migrating → BuildRun → Summary
```

`MainViewModel` owns a `WorkflowState` and exposes the current step to a stepper at the top of the window. Each step is reachable only when its prerequisite data exists, and the user can always go back.

## Screens

| Screen | Content |
|---|---|
| Open | Drop zone, Browse button, recent projects |
| Report | Score gauge, classification counts, grouped findings list, detail pane with snippet, filter bar, export |
| Migration setup | Output folder, target choice, shims toggle, plan checklist, Preview and Migrate buttons |
| Diff | Changed files list, side-by-side diff, manual-marker highlights |
| Build and run | Environment status, live log, grouped diagnostics with hints, Run/Stop buttons, game output |
| Summary | Before and after score, counts, build result, links to manifest and output folder |

## Threading and responsiveness

- Each long operation is an `AsyncRelayCommand` that takes a `CancellationToken`.
- Progress from the core libraries arrives via `IProgress<T>` and is marshalled with `Dispatcher.UIThread`.
- Large lists (findings, log lines) use virtualized `ItemsControl` and append in batches to avoid UI stalls.

## Dialogs

`IDialogService` abstracts file pickers and confirmations so ViewModels stay testable. The first-launch confirmation passes through this service and displays a clear message that the game's code will run on the user's machine.

## Theming and accessibility

- FluentTheme with `RequestedThemeVariant="Default"` to follow the system.
- Severity uses icon plus text label plus color.
- `AutomationProperties.Name` on interactive controls, logical tab order, visible focus.

## Error display

A shared `ErrorBanner` shows a short message with an expandable details area and a Copy button. Core libraries return typed results, so the UI renders failures instead of catching exceptions broadly.

## Testing strategy

1. **ViewModel unit tests** with fake services: workflow transitions, filtering, exclusions, cancellation.
2. **Avalonia headless tests** for key views (report list renders, diff viewer shows both sides).
3. **Manual demo script** rehearsal with the demo project (tracked in the build order).
