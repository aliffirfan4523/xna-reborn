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

## Device projects (Xbox 360, Zune, Windows Phone)

A project whose `XnaPlatform` is a device platform is converted to a desktop game instead of being rejected. Platform copies of the same game (`Xbox 360 Copy of X`) are skipped and the Windows project is migrated. `#if XBOX` and `#if WINDOWS_PHONE` branches are folded for the migrated platform. Touch, tilt and the Back button are stood in for by the mouse and keyboard through the generated `XnaReborn.Compat` library. The migrated copy gets a `DEVICE-CONTROLS.md` that lists the keys and values. Phone choosers, the media library and ad assemblies are left as `// XNAREBORN:` markers. Design: `.kiro/specs/device-to-desktop`.

## Safety

The original XNA project is never modified. All output goes to a new folder.

## License

Not chosen yet. See `LICENSE`.
