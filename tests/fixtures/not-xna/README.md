# not-xna

Synthetic fixture for the project analyzer.

- Original code written for this repository. No third-party code or assets.
- Released under the repository license.

## Purpose

A plain .NET 8 console project in SDK style. It has no XNA references. The analyzer should discover it as a project and report no XNA signals. This is the negative case for discovery.

The project is never built.

## What the analyzer should find

- One SDK-style console project targeting net8.0.
- No XNA Game Studio references, content projects, or XNA framework assemblies.
