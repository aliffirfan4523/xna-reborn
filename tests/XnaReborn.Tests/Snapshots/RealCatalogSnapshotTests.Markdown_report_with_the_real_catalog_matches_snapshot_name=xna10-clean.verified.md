# XNA Reborn compatibility report

Rule catalog: xna-to-monogame (report format 1)

## Summary

| Item | Value |
|---|---|
| Compatibility score | 95 / 100 |
| Projected score after automatic fixes | 99 / 100 |
| Projects analyzed | 1 |
| Findings | 4 |

Severity: Blocker 0, Error 2, Warning 0, Info 2

Classification: AutoFixable 3, Manual 1, Unsupported 0

## Projects

| Project | XNA version | Platform | Profile | Target framework |
|---|---|---|---|---|
| XnaExpress10Clean.csproj | 1.0 | Windows | - | - |

## Findings

### AutoFixable

- **[Error] XNA001** XNA Game Studio build targets import
  - Project: `XnaExpress10Clean.csproj`
  - Code: `$(MSBuildExtensionsPath)\Microsoft\XNA\Game Studio Express\v1.0\Microsoft.Xna.Common.targets`
- **[Error] XNA003** XNA assembly references
  - Project: `XnaExpress10Clean.csproj`
  - Code: `Microsoft.Xna.Framework, Microsoft.Xna.Framework.Game`
- **[Info] XNA002** XNA-specific project properties
  - Project: `XnaExpress10Clean.csproj`
  - Code: `XnaFrameworkVersion=v1.0`

### Manual

- **[Info] XNA100** XNA 1.0 or 2.0 project
  - Project: `XnaExpress10Clean.csproj`
  - Code: `XnaFrameworkVersion=v1.0`

## Rules

### XNA001 XNA Game Studio build targets import

The project file imports the XNA Game Studio MSBuild targets (Microsoft.Xna.GameStudio.targets from 3.0 on, Microsoft.Xna.Common.targets and Microsoft.Xna.ContentPipeline.targets in Game Studio Express 1.0). They exist only on machines with the matching XNA Game Studio and Visual Studio installed. Without them the build stops at once with MSB4019 (imported project not found), as it did for Pirate-Treasure-Hunter.

**Resolution:** The migrator writes an SDK-style project for .NET 8 that does not import the XNA targets.

Migrator task: project-migrator:4.1

### XNA002 XNA-specific project properties

The project sets XnaFrameworkVersion, a property that only the XNA Game Studio targets understand. The same project usually also sets ProjectTypeGuids (the XNA game GUID), XnaPlatform, XnaProfile, XnaOutputType, XnaCrossPlatformGroupID, and in Game Studio Express 1.0 XNAGlobalContentPipelineAssemblies and XNAProjectContentPipelineAssemblies. None has an MSBuild equivalent in MonoGame.

**Resolution:** The migrator drops XNA-specific properties and project type GUIDs when it generates the SDK-style project. The graphics profile is carried into Content.mgcb as /profile.

Migrator task: project-migrator:4.1

### XNA003 XNA assembly references

The project references XNA assemblies (Microsoft.Xna.Framework and related) by strong name with Private=False. They do not resolve on a machine without XNA, so the project cannot build. MonoGame ships the same API under the same namespaces in its own package. This rule also fires for assemblies MonoGame does not provide (GamerServices, Net, Avatar, Storage, Xact, Video); those have their own rules for code that uses them.

**Resolution:** The migrator replaces the XNA references with the MonoGame DesktopGL or WindowsDX package.

Migrator task: project-migrator:4.1

### XNA100 XNA 1.0 or 2.0 project

The project targets XNA Game Studio Express 1.0 or XNA Game Studio 2.0. Both are older than the 3.x line. Everything in XNA 4.0 and MonoGame that changed since 3.1 changed since these versions too. Defaults also differ: premultiplied alpha is the default from 4.0 on, and from 2.0 on Draw is called once per Update with a fixed time step, so a frame counter that counted Draw calls reads differently. Visual Basic projects are covered by a separate group of rules.

**Resolution:** Fix the findings from the XNA 1.0 and 2.0 rules and the XNA 3.x rules, then compare the rendered result of the migrated game with the original, especially alpha-blended sprites and anything that depends on frame timing.
