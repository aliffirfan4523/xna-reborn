# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 97 / 100 |
| Projected score after automatic fixes | 100 / 100 |
| Projects analyzed | 1 |
| Findings | 2 |

Severity: Blocker 0, Error 1, Warning 1, Info 0

Classification: AutoFixable 2, Manual 0, Unsupported 0

## Projects

| Project | Language | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|---|
| VbXna31Handmade.vbproj | Visual Basic | 3.1 | Unknown | - | v3.5 |

## Findings

### AutoFixable

- **[Error] XNA003** XNA assembly references
  - Project: `VbXna31Handmade.vbproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Warning] XNA004** Old .NET Framework target
  - Project: `VbXna31Handmade.vbproj`
  - Code: `TargetFrameworkVersion=v3.5`

## Rules

### XNA003 XNA assembly references

The project references XNA assemblies (Microsoft.Xna.Framework and related) by strong name with Private=False. They do not resolve on a machine without XNA, so the project cannot build. MonoGame ships the same API under the same namespaces in its own package. This rule also fires for assemblies MonoGame does not provide (GamerServices, Net, Avatar, Storage, Xact, Video); those have their own rules for code that uses them.

**Resolution:** The migrator replaces the XNA references with the MonoGame DesktopGL or WindowsDX package.

Migrator task: project-migrator:4.1

### XNA004 Old .NET Framework target

The project targets the .NET Framework (3.5 for XNA 3.x, 4.0 with the Client profile for XNA 4.0). That framework version is not available to an SDK-style build, and current MonoGame releases need modern .NET.

**Resolution:** The migrator sets the target framework of the generated project to net8.0.

Migrator task: project-migrator:4.1
