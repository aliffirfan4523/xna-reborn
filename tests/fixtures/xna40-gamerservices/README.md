# xna40-gamerservices

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A tiny XNA 4.0 Windows game that depends on Xbox LIVE era services. The project references `Microsoft.Xna.Framework.GamerServices` and `Microsoft.Xna.Framework.Storage`. `Game1` adds `GamerServicesComponent`, calls `Guide.ShowSignIn`, reads `Gamer.SignedInGamers` and calls `StorageDevice.BeginShowSelector`. It is never built.

## What the analyzer should find

- Blocker-level findings for GamerServices: the assembly reference, the `GamerServicesComponent`, `Guide` and `Gamer.SignedInGamers` usages.
- Blocker-level findings for Storage: the assembly reference and the `StorageDevice.BeginShowSelector` call.
- The usual project-level findings (XNA 4.0 targets, XNA references). No content project.