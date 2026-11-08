# Requirements: Build and Run

## Introduction

The build-and-run component compiles a migrated project with the .NET SDK, turns raw compiler output into structured diagnostics, links errors to known fixes, and launches the game so the user sees the result. It only ever operates on migrated copies, never on the original XNA project.

## Glossary

- **Migrated copy**: output folder produced by the migrator.
- **Diagnostic**: one structured build error or warning.
- **Fix hint**: a suggestion linked to a diagnostic through the hint catalog.
- **Run session**: one launch of the built game, with captured output and exit state.

## Requirements

### R1: Environment check
**User story:** As a developer, I want to know quickly if my machine can build the project.

Acceptance criteria:
1. THE component SHALL detect whether a compatible .NET SDK (8.0 or later) is installed and report its version.
2. IF no compatible SDK is found, THEN THE component SHALL report a clear message with where to get it and SHALL NOT attempt a build.
3. THE component SHALL report the detected operating system and whether the selected MonoGame target (DesktopGL or WindowsDX) is supported on it.

### R2: Build execution
**User story:** As a developer, I want the migrated project built with one action.

Acceptance criteria:
1. WHEN a build is requested, THE component SHALL run restore and build for the migrated copy using the `dotnet` CLI.
2. THE component SHALL stream build output lines to the caller as they arrive.
3. THE component SHALL support cancellation and SHALL terminate the build process tree when cancelled.
4. THE component SHALL enforce a configurable timeout (default 5 minutes).
5. THE component SHALL refuse to build any folder that does not contain the migration manifest marking it as a migrated copy.

### R3: Diagnostics parsing
**User story:** As a developer, I want errors as structured data, not a wall of text.

Acceptance criteria:
1. THE component SHALL parse MSBuild and compiler output into diagnostics with severity, code, message, file, line, column, and project.
2. THE component SHALL parse content builder (MGCB) errors into diagnostics with the asset path.
3. THE component SHALL de-duplicate repeated diagnostics emitted by multiple targets.
4. THE component SHALL keep the raw output available alongside the parsed diagnostics.

### R4: Fix hints
**User story:** As a developer, I want each error explained with what to do next.

Acceptance criteria:
1. THE component SHALL load a hint catalog (JSON) that maps diagnostic codes and message patterns to fix hints.
2. WHEN a diagnostic matches a hint, THE component SHALL attach the hint, including a link to the related analyzer rule ID where one exists.
3. THE component SHALL group diagnostics by hint so repeated errors appear as one issue with a count.
4. THE component SHALL report unmatched diagnostics as "no known fix" instead of hiding them.

### R5: Launching the game
**User story:** As a developer, I want to see the game run.

Acceptance criteria:
1. WHEN the build succeeds and the user confirms, THE component SHALL launch the built game executable as a separate process.
2. THE component SHALL capture standard output and standard error of the game.
3. THE component SHALL detect an early crash (exit within a configurable window, default 10 seconds, with a non-zero exit code) and report it as a startup failure with captured output.
4. THE component SHALL let the user stop the running game.
5. THE component SHALL set the game's working directory so relative content paths resolve.

### R6: Runtime failure hints
**User story:** As a developer, I want help with failures that only appear when the game starts.

Acceptance criteria:
1. THE component SHALL recognize common runtime failures, such as missing content files, content load type mismatches, and missing native graphics libraries, from captured output.
2. WHEN recognized, THE component SHALL attach a hint using the same catalog format.

### R7: Safety
**User story:** As a user, I want to stay in control of running old game code.

Acceptance criteria:
1. THE component SHALL require explicit user confirmation before the first launch of any migrated project in a session, explaining that the game's code will run on their machine.
2. THE component SHALL run the game with the current user's normal privileges and SHALL NOT request elevation.
3. THE component SHALL NOT transmit build output, source code, or game output anywhere.

### R8: Result model
**User story:** As the UI and the migrator, I want one result object to work with.

Acceptance criteria:
1. THE component SHALL return a result containing environment info, build status, diagnostics with hints, duration, and run session data.
2. THE component SHALL write the result into the migration manifest when invoked by the migrator.
3. THE component SHALL emit progress events for each stage: environment check, restore, build, launch, and running.
