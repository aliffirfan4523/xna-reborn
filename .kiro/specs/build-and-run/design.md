# Design: Build and Run (C# / .NET 8)

## Overview

`XnaReborn.Runner` is a class library that wraps the `dotnet` CLI. It has no UI dependency and exposes an async API that reports progress, diagnostics, and run state.

## Project layout

```
src/XnaReborn.Runner/
├── Environment/
│   ├── DotnetLocator.cs
│   └── EnvironmentInfo.cs
├── Build/
│   ├── BuildRunner.cs
│   ├── ProcessRunner.cs           # shared process wrapper
│   └── BuildResult.cs
├── Diagnostics/
│   ├── MsBuildOutputParser.cs
│   ├── MgcbOutputParser.cs
│   ├── Diagnostic.cs
│   └── DiagnosticGrouper.cs
├── Hints/
│   ├── HintCatalog.cs
│   ├── hints.json
│   └── HintMatcher.cs
├── Run/
│   ├── GameLauncher.cs
│   ├── RunSession.cs
│   └── RuntimeFailureDetector.cs
├── Safety/
│   └── MigratedCopyGuard.cs
└── ProjectRunner.cs               # entry point
```

## Key dependencies

| Package | Use |
|---|---|
| `System.Diagnostics.Process` | Starting `dotnet` and the game |
| `System.Text.RegularExpressions` | Output parsing (source-generated regex) |
| `System.Text.Json` | Hint catalog and result serialization |
| `Microsoft.Extensions.Logging` | Structured logs |
| `xUnit`, `Verify.Xunit` | Tests |

## Core types

```csharp
public sealed record EnvironmentInfo(
    string? DotnetVersion, bool SdkCompatible, string Os, bool TargetSupported);

public sealed record Diagnostic(
    DiagnosticSeverity Severity,   // Error | Warning | Info
    string Code,                   // e.g. CS0246, MSB3202, MGCB
    string Message,
    string? File, int? Line, int? Column,
    string? Project, string? AssetPath);

public sealed record FixHint(
    string Id, string Title, string Advice, string? RuleId);

public sealed record DiagnosticGroup(
    FixHint? Hint, IReadOnlyList<Diagnostic> Items);

public sealed record BuildResult(
    bool Succeeded, TimeSpan Duration,
    IReadOnlyList<DiagnosticGroup> Groups, string RawOutput);

public sealed record RunSession(
    int? ExitCode, bool StartupFailure, string Stdout, string Stderr,
    IReadOnlyList<FixHint> RuntimeHints);
```

## Pipeline

`ProjectRunner.BuildAndRunAsync(migratedFolder, options, progress, ct)`

1. **Guard**: `MigratedCopyGuard` verifies the manifest marker. Refuse otherwise.
2. **Environment**: `DotnetLocator` runs `dotnet --version` and `dotnet --list-sdks`; compare with the minimum.
3. **Restore + build**: `BuildRunner` runs `dotnet build <project> -nologo -v:minimal -consoleloggerparameters:NoSummary` (restore is implicit). Lines stream to the caller.
4. **Parse**: parsers convert output into `Diagnostic` records; `DiagnosticGrouper` de-duplicates and groups by hint.
5. **Hint matching**: `HintMatcher` applies the catalog.
6. **Stop on failure**: if the build fails, return the `BuildResult`.
7. **Confirm and launch**: ask the caller to confirm (first launch per session), then `GameLauncher` starts the executable.
8. **Monitor**: capture output, detect early exit, and run `RuntimeFailureDetector` on captured text.
9. **Return** the combined result.

## Process handling

`ProcessRunner` centralizes process start with:
- `RedirectStandardOutput/Error`, `UseShellExecute = false`, `CreateNoWindow = true` for builds.
- Async line events pushed through `Channel<string>` so the UI never blocks.
- Cancellation kills the entire process tree (`Process.Kill(entireProcessTree: true)`).
- A timeout linked to the cancellation token.

The game itself is launched with `UseShellExecute = false`, a working directory set to the output folder, and captured streams.

## Output parsing

MSBuild/compiler line format:

```
Game1.cs(42,17): error CS0246: The type or namespace name 'GamerServices' could not be found [C:\...\MyGame.csproj]
```

Regex (source-generated) captures file, line, column, severity, code, message, and project. MGCB errors follow a similar MSBuild-style line with the asset path in the file field; the parser tags those diagnostics with `AssetPath`. Anything unparsed stays in `RawOutput`.

## Hint catalog

```json
{
  "version": 1,
  "hints": [
    {
      "id": "H001",
      "match": { "code": "CS0234", "messageContains": "GamerServices" },
      "title": "GamerServices no longer exists",
      "advice": "Remove Xbox Live usage or enable the compatibility shims in migration options.",
      "ruleId": "XNA001"
    },
    {
      "id": "H010",
      "match": { "code": "CS0117", "messageContains": "RenderState" },
      "title": "XNA 3.1 RenderState usage",
      "advice": "Replace with BlendState, DepthStencilState, and RasterizerState.",
      "ruleId": "XNA021"
    },
    {
      "id": "R001",
      "match": { "runtimeContains": "Could not find file" },
      "title": "Missing content file",
      "advice": "Check that Content.mgcb built successfully and the Content folder is copied to the output.",
      "ruleId": null
    }
  ]
}
```

Matching order: code plus message substring first, then code only, then message regex. The first match wins.

## Runtime failure detection

`RuntimeFailureDetector` scans captured stdout/stderr for known patterns:
- `ContentLoadException` and "Could not find file" (content missing or not built)
- "Unable to load shared library" / "libSDL2" (native dependency on non-Windows hosts)
- `NoSuitableGraphicsDeviceException` (graphics profile or driver)
- `InvalidOperationException` from `ContentManager` type mismatches

Detected patterns map to hints from the same catalog.

## Safety design

- `MigratedCopyGuard` requires the manifest marker so the original XNA project can never be built or run by mistake.
- First launch requires an explicit callback `Func<LaunchConfirmation, Task<bool>>` supplied by the caller.
- No elevation, no network calls.

## Error handling

- Missing SDK, bad path, and guard failures return typed errors, not exceptions.
- Unexpected exceptions are caught at the pipeline boundary and returned as a failed result with diagnostics.
- A crashed game never crashes the host app.

## Testing strategy

1. **Parser tests** against saved real compiler and MGCB output samples (snapshots).
2. **Hint tests**: each hint has a sample diagnostic that must match, and an unrelated one that must not.
3. **Process tests** with a tiny fake `dotnet` script to simulate success, failure, timeout, and cancellation without a real build.
4. **Guard tests**: unmigrated folder refused.
5. **Launcher tests** with a small test executable that exits immediately with an error to cover early-crash detection.
6. **Integration test** (marked slow): build the migrated simplest fixture for real.


## Update from older-and-vb

- GameLocator and MigratedCopyGuard also accept .vbproj. The build output parser needed no change for Visual Basic (BC codes). Hints H501 and H502 cover the My namespace.

