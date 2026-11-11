# Flash fixtures

Small synthetic AS3 and Flixel-style projects for the Flash to MonoGame flow.
Every file was written from scratch for XNA Reborn. No third-party game code or assets are included.
The `org.flixel` stubs in `flx-bundled-flixel` only carry version constants for the version probe.
`game.swf` and `timeline.fla` are plain text stubs and must never be decoded.

Each folder has an `expected.json` with the detection and reading result the tests check.

| Fixture | Purpose |
|---|---|
| `flx-hello` | Smallest Flixel project: FlashDevelop file, entry class, one state. |
| `flx-swf-only` | A compiled `.swf` without sources. |
| `flx-fla-only` | A Flash Pro `.fla` without sources. |
| `flx-plain-as3` | AS3 without Flixel. |
| `flx-two-entries` | Two classes that extend `FlxGame`. |
| `flx-missing-embed` | An `[Embed]` whose file does not exist. |
| `flx-not-flash` | A C# only project. |
| `flx-bundled-flixel` | Flixel source bundled in the project, with a version probe. |
