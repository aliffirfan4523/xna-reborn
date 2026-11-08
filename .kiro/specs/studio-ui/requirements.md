# Requirements: Studio UI

## Introduction

The Studio UI is the Avalonia desktop application that connects the analyzer, migrator, and runner into one guided flow: open a project, understand it, migrate it, and watch it run. It contains no migration logic; it calls the core libraries.

## Requirements

### R1: Open a project
**User story:** As a developer, I want to start by pointing at my old project.

Acceptance criteria:
1. THE app SHALL let the user open a folder, `.sln`, or `.csproj` through a file dialog or drag and drop.
2. THE app SHALL show a list of recently opened projects.
3. IF the selected path has no XNA project, THEN THE app SHALL show the analyzer's explanation instead of a blank screen.

### R2: Analysis report screen
**User story:** As a developer, I want to understand what will happen to my project.

Acceptance criteria:
1. WHEN analysis completes, THE app SHALL show the compatibility score prominently along with counts per classification.
2. THE app SHALL list findings grouped by classification (AutoFixable, Manual, Unsupported) with severity indicators.
3. WHEN a finding is selected, THE app SHALL show its explanation, suggested resolution, and a code snippet with file and line.
4. THE app SHALL allow filtering by severity and classification and searching by text.
5. THE app SHALL offer export of the report as Markdown and JSON.

### R3: Migration setup
**User story:** As a developer, I want to choose how the migration runs.

Acceptance criteria:
1. THE app SHALL let the user choose the output folder and the MonoGame target (DesktopGL or WindowsDX).
2. THE app SHALL show the migration plan as a checklist where each item can be excluded.
3. THE app SHALL offer a shims toggle that is off by default with a short explanation.
4. THE app SHALL show a warning when the output folder is invalid or non-empty and require an explicit choice before continuing.

### R4: Diff view
**User story:** As a developer, I want to review changes before trusting them.

Acceptance criteria:
1. THE app SHALL show a list of changed files and a side-by-side diff for the selected file.
2. THE app SHALL show `// XNAREBORN:` markers prominently as items needing manual attention.
3. THE app SHALL be able to show the diff from a dry run before anything is written.

### R5: Build and run screen
**User story:** As a developer, I want to see the game come back to life.

Acceptance criteria:
1. THE app SHALL show environment status (SDK version, target support).
2. THE app SHALL stream build output live and show structured diagnostics grouped by fix hint.
3. WHEN the build succeeds, THE app SHALL offer a Run button that triggers the first-launch confirmation dialog.
4. THE app SHALL show the running game's status and captured output and allow stopping it.
5. WHEN a startup failure occurs, THE app SHALL show the runtime hint and captured output.

### R6: Progress and responsiveness
**User story:** As a user, I want the app to never feel frozen.

Acceptance criteria:
1. THE app SHALL run analysis, migration, build, and launch off the UI thread.
2. THE app SHALL show progress and a Cancel button for every long operation.
3. THE app SHALL show errors as readable messages with a "copy details" action.

### R7: Before and after story
**User story:** As a presenter, I want the improvement to be visible.

Acceptance criteria:
1. THE app SHALL show the original compatibility score and the post-migration result (build success, remaining manual items) in one summary view.
2. THE app SHALL show a final summary with counts of automatic fixes, manual items, and build status.

### R8: Look and accessibility
**User story:** As a user, I want a pleasant, usable tool.

Acceptance criteria:
1. THE app SHALL support light and dark themes following the system setting.
2. THE app SHALL be fully usable with the keyboard and expose accessible names for controls.
3. THE app SHALL not rely on color alone to convey severity.
