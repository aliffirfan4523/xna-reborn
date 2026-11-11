# Design: Device Projects to Desktop (C# / .NET 8)

## Overview

Device conversion touches three projects, each in its own place:

| Project | Change |
|---|---|
| `XnaReborn.Core` | `DevicePlatform` enum, new fields on `ProjectInfo`, two new `match.type` values in the catalog schema |
| `XnaReborn.Analyzer` | `PlatformDetector`, `projectLinks` and `preprocessorSymbol` matchers, new and changed rules |
| `XnaReborn.Migrator` | Platform copy selection, `PlatformDirectiveRewriter`, `GamePadCallRewriter`, `DeviceInstallRewriter`, device sources inside the Compat library, `DEVICE-CONTROLS.md` |

The UI gets no logic. It shows plan items and the manifest as it does today.

## Research findings

What the primary sources say, and what that means for the design.

1. **Platforms and versions.** Zune and Zune HD are supported only up to XNA Game Studio 3.1. Windows Phone is new in 4.0, so a Zune project has no direct upgrade path to Phone ([Microsoft: Migrating from Zune to Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff604985(v=xnagamestudio.41))). A Zune project is therefore also an XNA 3.x project: rule XNA006 and the 3.1 to 4.0 rewriters (migrator task 10) apply first.
2. **Platform copies.** Game Studio's "Create Copy of ... for ..." command adds a project named like `Xbox 360 Copy of X`, links code, content and settings to the original, replaces `XnaPlatform`, `ProjectGuid` and references, and allows one copy per platform ([Microsoft: Adding New Platforms to an Existing Game](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb976061(v=xnagamestudio.41))). The same page says platform code is selected with the cross-platform conditional compilation symbols.
3. **Symbols.** The symbols are `WINDOWS`, `XBOX` or `XBOX360`, and `WINDOWS_PHONE` ([Game Development Stack Exchange summary of the Microsoft topic](https://gamedev.stackexchange.com/questions/45256/what-are-the-statements-in-xna)). Which are active depends on the project that opens the file, even when several projects link the same file ([Rob Miles: XNA Cross Platform Compilation Symbols](https://rob-miles.squarespace.com/journal/2010/6/8/xna-cross-platform-compilation-symbols.html)). This is why the analyzer must read inactive branches, and why the migrator must pick one platform set.
4. **Phone input.** Touch is in `Microsoft.Xna.Framework.Input.Touch` (its own assembly). Accelerometer input moved to the Windows Phone SDK, assembly `Microsoft.Devices.Sensors`, and is event driven. Hardware buttons cannot be used by Phone games ([Microsoft: Migrating from Zune to Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff604985(v=xnagamestudio.41))). `TouchPanel.GetState` gives raw touch, `ReadGesture` gives gestures ([MSDN Magazine: Touch Gestures on Windows Phone](https://msdn.microsoft.com/en-us/library/Gg650664(v=MSDN.10))). Rob Miles notes that the Zune HD has accelerometer support in the XNA style, while Windows Phone does not ([robmiles.com](https://rob-miles.squarespace.com/journal/2010/3/22/windows-phone-accelerometer-support-in-xna.html)).
5. **Phone screen.** Windows Phone OS 7.1 is at least 800x480; the back buffer is scaled by hardware, so a game may ask for the old Zune size, for example 272x480 ([Microsoft: Migrating from Zune to Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff604985(v=xnagamestudio.41))). Width greater than height picks landscape, otherwise portrait ([Shawn Hargreaves: Orientation and rotation on Windows Phone](https://shawnhargreaves.com/blog/orientation-and-rotation-on-windows-phone.html)).
6. **Tombstoning.** Phone games are deactivated when the user leaves; the documented technique is to save in `Game.OnExiting` to isolated storage ([Microsoft: Handling Interruptions on Windows Phone](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff827866(v=xnagamestudio.40))). `OnExiting` exists in MonoGame, so that pattern works unchanged. Only `PhoneApplicationService` needs a stand-in.
7. **MonoGame touch on desktop.** `TouchPanel` is in MonoGame in the same namespace as XNA and has `EnableMouseTouchPoint` and `EnableMouseGestures`, `EnabledGestures`, `IsGestureAvailable`, `GetState`, `GetCapabilities` ([MonoGame API: TouchPanel](https://docs.monogame.net/api/Microsoft.Xna.Framework.Input.Touch.TouchPanel.html)). A community report says both switches must be on for touch locations and gestures to appear from a pointer ([Game Development Stack Exchange](https://gamedev.stackexchange.com/questions/104269/touch-taps-only-on-very-short-flicks-on-win7-with-monogame)). The MonoGame touch guides are [How to work with Touch Input](https://docs.monogame.net/articles/getting_to_know/howto/input/HowTo_UseMultiTouchInput.html) and [Detecting Gestures](https://docs.monogame.net/articles/getting_to_know/howto/input/HowTo_Detect_Gestures.html).
8. **MonoGame accelerometer on desktop.** MonoGame has an `Accelerometer`, but in namespace `MonoGame.Framework.Devices.Sensors`, not `Microsoft.Devices.Sensors`, and its own page calls it "currently stubs, for cross-platform development" ([MonoGame API: Accelerometer](https://docs.monogame.net/api/MonoGame.Framework.Devices.Sensors.Accelerometer.html)). So the original `using Microsoft.Devices.Sensors;` does not resolve, and the stub would not deliver readings anyway.
9. **Real project shape.** The MIT-licensed `b3nk4n/vaderpixx-game` csproj (read through the GitHub API, nothing cloned) has `XnaPlatform` = `Windows Phone`, `XnaProfile` = `Reach`, the XNA project type GUID, `Microsoft.Devices.Sensors`, `Microsoft.Phone` and ad assemblies `Microsoft.Advertising.Mobile` as references, and an `AccelerometerHelper` folder in source. This confirms the existing XNA007 regex catches such a project and that third-party device assemblies are common.

### Not verified (must be checked in task 1 or by the task named)

- The exact `.csproj` properties of an XNA plus Silverlight hybrid phone project (`SilverlightApplication`, `TargetFrameworkIdentifier`). Task 1 writes the fixture from the Microsoft documentation. XNA052 below stays provisional until a real project confirms it.
- The Zune HD accelerometer type in XNA 3.1 (the "Zune HD extensions"). Rob Miles describes it but I did not find the type name in a Microsoft page. Task 7 only adds a Zune accelerometer adapter after the type shape is confirmed from the XNA 3.1 documentation archive; until then Zune accelerometer use is a Manual marker.
- Whether setting `TouchPanel.EnableMouseTouchPoint` before `Game.Run()` works. The design installs it from a game component (see below) so it does not depend on this. Task 8 proves it with a run.
- Whether `MediaLibrary` throws on DesktopGL. It is treated as Manual either way.
- Whether `System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication` (used by Phone save code) works in the migrated net8.0 copy. Task 7 adds a one-line test; if it fails, the storage stand-in grows an `IsolatedStorage` wrapper.

## Decision: input mapping is a generated library plus thin source rewrites

**Choice.** Behavior lives in the generated `XnaReborn.Compat` library. Source rewrites are used only where MonoGame already owns the name, so a library cannot take its place. Concretely:

| Device API | How it is handled | Why |
|---|---|---|
| `Microsoft.Devices.Sensors.Accelerometer` | Compat library type with the same namespace | MonoGame does not have that namespace, so game code compiles unchanged, exactly like the storage and profile stand-ins |
| `Microsoft.Phone.Shell`, `Microsoft.Phone.Tasks`, `Guide.IsTrialMode`, `MarketplaceDetailTask` | Compat library types with the same namespaces | Same reason |
| `TouchPanel`, `GestureType`, `GestureSample` | No stand-in. A `DeviceInput` game component turns on `EnableMouseTouchPoint` and `EnableMouseGestures` | MonoGame already has these types and a built-in mouse path. Replacing them would break types the game passes around |
| `GamePad.GetState(PlayerIndex)` | One source rewrite to `XnaReborn.Compat.Input.CompatGamePad.GetState(...)` | MonoGame defines a static `GamePad` in the same namespace, so a library class with that name would collide |
| Back button, Escape to exit | Inside `CompatGamePad` | The game already tests `Buttons.Back`, so no code change is needed beyond the call redirect |
| Window mode, cursor | Inside the `DeviceInput` component | Needs runtime objects, not syntax |

**Why not source rewrites for everything.** A rewrite puts tilt maths, key bindings and easing into the user's own files. Every game would get a different copy, a tuning change would need a new rewriter, and the diff the user reviews would be large. A library is generated once per migration, is unit tested once, and the bindings sit in one file the user can edit.

**Why not a library for everything.** It cannot work for `GamePad`. Two static classes named `GamePad` in `Microsoft.Xna.Framework.Input` would be an ambiguity error, and shadowing by a using alias would need an edit in every file anyway. One call-site rewrite is the smallest change that works.

**Consequence.** The rewrite for `GamePad.GetState` is syntactic (the analyzer cannot resolve XNA types, see `project-analyzer` design, source matching limits). The guard is: the file imports `Microsoft.Xna.Framework.Input` and the receiver is exactly `GamePad`. A user class named `GamePad` in another namespace is the known false-positive risk; the negative fixture covers the guarded case, and the rewrite is a plan item the user can exclude.

**Plan-item driven, not `EnableShims` driven.** The existing option `EnableShims` stays off by default so the user sees the cost of dead storage and profile APIs (project-migrator design). Device input is different: without it the converted game cannot be played. The device library is therefore generated whenever a device plan item (XNA054 to XNA062) is included, and it appears in the manifest. `MigrationOptions` does not change. Excluding the plan items switches it off.

## Platform detection

`PlatformDetector` (Analyzer) reads the raw property map that `projectProperty` already uses.

```csharp
/// <summary>The device an XNA project was built for.</summary>
public enum DevicePlatform { Unknown, Windows, Xbox360, Zune, WindowsPhone }
```

Rules of detection, in order:

1. `XnaPlatform` matches `^\s*(Windows)\s*$` → Windows; `Xbox ?360` → Xbox360; `Zune` → Zune; `Windows ?Phone` → WindowsPhone (all case-insensitive).
2. Missing `XnaPlatform`: references `Microsoft.Phone` or `Microsoft.Devices.Sensors` → WindowsPhone (inferred); XNA 3.x references plus `ZUNE` in `DefineConstants` → Zune (inferred).
3. Otherwise Unknown.

`ProjectInfo` gains `DevicePlatform Platform`, `string? XnaProfile`, `bool PlatformInferred` and `IReadOnlyList<string> LinkedSourceProjects` (paths of other projects this project links `Compile` items from). All have defaults so existing tests and snapshots do not change. XNA-only property `XnaPlatform` is already removed by the project generator (XNA002).

## Platform copies

Game Studio names copies like `Xbox 360 Copy of X` and links their items. The analyzer does not trust the name. `ProjectLinkReader` marks a `Compile` item as linked when it has `Link` metadata or its `Include` begins with `..\`/`../` and resolves to a file under another code project of the solution. A project is a copy when linked items are at least half of its `Compile` items.

`MigrationPlanner` groups projects of one solution by their linked-source target:

```
Solution
├─ X                     Windows   ← migrated
├─ Xbox 360 Copy of X    Xbox360   ← plan item "skip copy", rule XNA052
└─ Windows Phone Copy of X          ← plan item "skip copy", rule XNA052
```

Choice order when no Windows project exists is Windows Phone, then Xbox 360, then Zune. Reason: Phone is the XNA 4.0 platform with the closest input story (touch plus tilt, which the layer maps), and Zune is XNA 3.x and needs the extra upgrade rewrites. The reason is written to the manifest and the user can flip the plan items.

Because of the linking, the sibling that is migrated already contains all shared files. The copy's own files (its `Program.cs`, `Game1.cs` if not linked) are not merged: R3.6.

## Conditional compilation folding (XNA053)

`PlatformDirectiveRewriter : ISourceRewriter`, text in and text out like `EntryPointRewriter`, so it sees inactive branches and leaves every other byte alone.

1. Tokenize the file into lines; recognise `#if`, `#elif`, `#else`, `#endif` (whitespace after `#` allowed) outside string literals and block comments.
2. For each `#if` group, parse every condition with a small recursive-descent parser: identifiers, `!`, `&&`, `||`, parentheses, `true`, `false`.
3. Evaluate with three-valued logic under a `PlatformSymbolSet`:

   | Source of the set | `WINDOWS` | `XBOX`, `XBOX360` | `ZUNE` | `WINDOWS_PHONE` |
   |---|---|---|---|---|
   | Windows sibling migrated | true | false | false | false |
   | Xbox 360 project migrated alone | false | true | false | false |
   | Zune project migrated alone | false | false | true | false |
   | Phone project migrated alone | false | false | false | true |

   Any other identifier (`DEBUG`, `TRACE`, user symbols) is Unknown. `Unknown && false` is false, `Unknown || true` is true, otherwise Unknown.
4. A group whose conditions are all known: keep the first true branch's body (or the `#else` body) and drop the directive lines and the other bodies. A group with any Unknown condition that is still undecided is left unchanged and gets a `// XNAREBORN: XNA053: ...` marker line above `#if`.
5. Nested groups are handled inside out. A parse failure leaves the file unchanged, with one marker, and the manifest records the file. A throw is isolated by `SourceRewriteEngine` as for every rewriter.

Run order: `PlatformDirectiveRewriter` before `EntryPointRewriter`, because the common `#if WINDOWS || XBOX` around `Main` becomes a plain `Main` under the Windows or Xbox set; and the entry-point rewriter keeps handling the cases that remain. Since rewriters run in rule ID order today, the engine gets an explicit order table (XNA053 first) instead of relying on IDs.

Example, Phone project migrated alone:

```csharp
// before
#if WINDOWS_PHONE
    graphics.PreferredBackBufferWidth = 480;
    graphics.PreferredBackBufferHeight = 800;
    graphics.IsFullScreen = true;
#else
    graphics.PreferredBackBufferWidth = 1280;
#endif
// after
    graphics.PreferredBackBufferWidth = 480;
    graphics.PreferredBackBufferHeight = 800;
    graphics.IsFullScreen = true;
```

(Full screen is later overridden at run time by the window rule, R7.)

## The device part of the Compat library

Extends `ShimProjectGenerator` and `ShimSources` (migrator task 12). New files in `XnaReborn.Compat/`:

```
XnaReborn.Compat/
├─ Device/
│   ├─ Accelerometer.cs        namespace Microsoft.Devices.Sensors
│   ├─ TiltMapper.cs           pure: pressed keys / mouse offset -> reading
│   ├─ PhoneShell.cs           namespace Microsoft.Phone.Shell (PhoneApplicationService)
│   ├─ PhoneTasks.cs           namespace Microsoft.Phone.Tasks (launchers; choosers fail fast)
│   ├─ Marketplace.cs          Guide.IsTrialMode / ShowMarketplace, LicenseInformation
│   └─ DeviceControls.cs       the single table of keys and tilt constants
└─ Input/
    ├─ CompatGamePad.cs        namespace XnaReborn.Compat.Input
    ├─ GamePadMapper.cs        pure: KeyboardState + GamePadState -> GamePadState
    └─ DeviceInput.cs          GameComponent: touch switches, cursor, window, Update ticks
```

Stand-ins for `Guide` marketplace members extend the existing `Guide` in the GamerServices stand-in; they are not a second type.

### Accelerometer

Public surface follows the Windows Phone type so game code compiles unchanged: constructor, static `IsSupported` (true), `State` (`SensorState.Ready` after `Start`), `Start()`, `Stop()`, `CurrentValue`, `TimeBetweenUpdates`, `ReadingChanged` of `EventHandler<AccelerometerReadingEventArgs>`, `Dispose()`. `AccelerometerReadingEventArgs` carries `X`, `Y`, `Z` and `Timestamp`.

Delivery: `DeviceInput.Update` calls `Accelerometer.Tick(gameTime)` for every started instance (kept in a static list, weak references). `Tick` reads the tilt source, eases toward the target, and raises `ReadingChanged` at most once per `TimeBetweenUpdates` (default 20 ms). Raising from `Update` is on purpose: the Phone event arrives on another thread, and games' handlers often just copy values to fields; same-thread delivery is safe for them and cannot race.

If no `DeviceInput` component exists (a game that is not a `Game` subclass, or the install rewrite was excluded), `Start()` throws `InvalidOperationException` with a message naming the rule and `DEVICE-CONTROLS.md`, because the Phone API itself throws on an unsupported sensor (the emulator case, [Stack Overflow](https://stackoverflow.com/questions/2473542/how-to-determine-orientation-of-windows-phone-7-in-xna-game)) and games already wrap `Start()` in try/catch.

### TiltMapper (pure)

```csharp
internal readonly record struct TiltInput(bool Left, bool Right, bool Up, bool Down, Vector2? MouseOffset);
internal static class TiltMapper
{
    public static Vector3 Target(TiltInput input, TiltSettings settings);   // g units
    public static Vector3 Ease(Vector3 current, Vector3 target, float seconds, TiltSettings settings);
}
```

`TiltSettings` defaults (all in `DeviceControls.cs`): resting reading `(0, 0, -1)` (face up, gravity along -Z, units of g), full tilt `0.7` on X/Y with Z kept so the vector has length near 1, ease time `0.12 s`. The axis signs follow the Windows Phone documentation (X to the right, Y toward the top of the screen, Z out of the screen) and are covered by golden-value tests. Games in landscape swap axes themselves, which the mapping does not touch.

### CompatGamePad and GamePadMapper (pure)

`CompatGamePad.GetState(PlayerIndex)` calls `GamePad.GetState`; if the pad `IsConnected`, it returns it unchanged. Otherwise it returns a state built by `GamePadMapper` from `Keyboard.GetState()`:

| Input | Maps to |
|---|---|
| Arrow keys, W A S D | left thumb stick (±1) and D-pad |
| Space, Enter | A |
| Z, X, C | B, X, Y respectively (fixed letters) |
| Escape | Back |
| Tab | Start |
| Q, E | left and right shoulder |

`IsConnected` is true for player one in the keyboard case, so games that wait for a pad do not stall. Players two to four return the real, disconnected state. A connected pad plus pressed Escape still reports Back (OR), so keyboard Escape always exits. Overloads with a `GamePadDeadZone` argument are forwarded. `GetCapabilities`, `SetVibration` and `GetState` with other arguments are not rewritten.

### DeviceInput (game component)

Added by the `DeviceInstallRewriter` (below) as `Components.Add(new XnaReborn.Compat.Input.DeviceInput(this));` at the end of the `Game` subclass constructor. In `Initialize` it:

1. sets `TouchPanel.EnableMouseTouchPoint = true` and `TouchPanel.EnableMouseGestures = true`;
2. sets `game.IsMouseVisible = true`;
3. finds `IGraphicsDeviceManager` through `game.Services`; when it is a `GraphicsDeviceManager`, sets `IsFullScreen = false` and calls `ApplyChanges()`; when the requested height exceeds the usable display height, sets `Window.AllowUserResizing = true`.

`EnabledGestures` is not touched: the game sets it for its own needs and MonoGame honours it ([MonoGame: Detecting Gestures](https://docs.monogame.net/articles/getting_to_know/howto/input/HowTo_Detect_Gestures.html)). In `Update` it ticks accelerometers. Putting this in a component, not in `Main`, avoids depending on whether static `TouchPanel` can be set before `Run()`.

### Phone shell, launchers, choosers, marketplace

- `PhoneApplicationService.Current.State` is a `Dictionary<string, object>`; events exist and never fire. Games that save in `OnExiting` still work.
- Launchers: `Show()` writes one line to `Debug` and `Console` and returns. `WebBrowserTask` does NOT open a browser by default (the game's URL would leave the sandbox without consent); `DeviceControls.OpenWebLinks` is a constant the user can flip.
- Choosers: the type exists so the code compiles, `Show()` throws `NotSupportedException` with the rule ID, and the analyzer reported the use as Manual first.
- Marketplace: `Guide.IsTrialMode` is false, `ShowMarketplace` does nothing, `LicenseInformation.IsTrial()` is false.

## Source rewriters added

| Rewriter | Rule | Change |
|---|---|---|
| `PlatformDirectiveRewriter` | XNA053 | Folds `#if` groups (above) |
| `GamePadCallRewriter` | XNA061 | `GamePad.GetState(` → `XnaReborn.Compat.Input.CompatGamePad.GetState(` where guarded |
| `DeviceInstallRewriter` | XNA060, XNA061 | Adds the `DeviceInput` component line to the `Game` subclass constructor; adds nothing if there is no `Game` subclass, and then inserts a marker |

All three follow the migrator rules: syntax-guarded, ambiguous → marker, trivia preserved. `MarkerInserter` adds the markers.

## Rule changes

IDs XNA051 and above are provisional. Another spec may claim some of them at the same time. Task 2 takes the first free range in `rules/xna-to-monogame.json` and updates this table and `requirements.md` in the same change.

| Id | Title | Severity | Classification | Match type | Match | Notes |
|---|---|---|---|---|---|---|
| XNA007 (edit) | Project targets Xbox 360, Zune, or Windows Phone | Warning (was Error) | AutoFixable (was Unsupported) | `projectProperty` (existing) | `XnaPlatform` ~ `^\s*(Xbox ?360|Zune|Windows ?Phone)\s*$`, case-insensitive | New resolution text; `migratorTask: device-to-desktop:5.1` |
| XNA051 | Windows Phone Silverlight application | Error | Unsupported | `projectProperty` (existing) | `SilverlightApplication` ~ `^true$` | Provisional until a real hybrid project is checked (see "Not verified") |
| XNA052 | Platform copy of another project | Info | AutoFixable | `projectLinks` (**new**) | `minLinkedRatio: 0.5` | Plan item "skip copy"; `migratorTask: device-to-desktop:5.2` |
| XNA053 | Device conditional compilation symbols | Warning | AutoFixable | `preprocessorSymbol` (**new**) | `names: [XBOX, XBOX360, ZUNE, WINDOWS_PHONE, WINDOWS]` | Reads inactive branches; `migratorTask: device-to-desktop:6.1` |
| XNA054 | Marketplace and trial APIs | Warning | AutoFixable | `member` (existing) | `IsTrialMode`, `ShowMarketplace` with receiver `Guide`; namespaces `Microsoft.Xna.Framework.GamerServices` | One match per rule, so the marketplace task and license types are a separate rule, XNA055. `migratorTask: device-to-desktop:7.3` |
| XNA055 | Marketplace launchers and license types | Warning | AutoFixable | `type` (existing) | the four type names; namespaces `Microsoft.Phone.Tasks`, `Microsoft.Phone.Marketplace` | `migratorTask: device-to-desktop:7.3` |
| XNA056 | Zune media library | Warning | Manual | `type` (existing) | `MediaLibrary`, `MediaSource`; namespace `Microsoft.Xna.Framework.Media` | |
| XNA057 | Phone application service (tombstoning) | Info | AutoFixable | `type` (existing) | `PhoneApplicationService`; namespace `Microsoft.Phone.Shell` | `migratorTask: device-to-desktop:7.2` |
| XNA058 | Phone launchers | Info | AutoFixable | `type` (existing) | `WebBrowserTask`, `EmailComposeTask`, `PhoneCallTask`, `SmsComposeTask`, `MarketplaceHubTask`; namespace `Microsoft.Phone.Tasks` | |
| XNA059 | Phone choosers | Warning | Manual | `type` (existing) | `PhotoChooserTask`, `CameraCaptureTask`, `SaveRingtoneTask`; namespace `Microsoft.Phone.Tasks` | |
| XNA060 | Accelerometer | Info | AutoFixable | `type` (existing) | `Accelerometer`; namespace `Microsoft.Devices.Sensors` | `migratorTask: device-to-desktop:7.1` |
| XNA061 | TouchPanel and gestures | Info | AutoFixable | `type` (existing) | `TouchPanel`; namespace `Microsoft.Xna.Framework.Input.Touch` | `migratorTask: device-to-desktop:8.2` |
| XNA062 | GamePad.GetState (keyboard fallback and Back button) | Info | AutoFixable | `member` (existing) | `GetState`, receiver `GamePad`, `arguments: [1, 2]`; namespace `Microsoft.Xna.Framework.Input` | `migratorTask: device-to-desktop:8.1` |
| XNA063 | Third-party device assembly | Warning | Manual | `reference` (existing) | name `Microsoft.Advertising.*` and `AdDuplex.*` | List can grow as data |

Notes:
- The user asked about orientation and 480x800 portrait. The requirement R7 keeps them as written, so they need no rule: the window opens at the requested size. A rule that only reports them would add noise without changing what the migrator does. If task 11 shows a game that breaks, add a `member` rule then.
- XNA009 (GamerServices as a whole) stays Unsupported. The marketplace members in XNA054 are a subset the Compat `Guide` can answer; both can fire for one file. Task 4 must make sure that scoring counts them once per rule, as today.
- Zune accelerometer (XNA 3.1 `Input.Accelerometer`) is not in the table until its type name is verified (task 7.4).
- The existing `member` match has `receiver`, `arguments` and `namespaces`; the table names only fields it already has. If the existing field for namespaces is spelled differently in the catalog, use that spelling.

### New match types

| `match.type` | Evaluated by | Data it reads |
|---|---|---|
| `projectLinks` | project | `ProjectInfo.LinkedSourceProjects` and compile item count; `minLinkedRatio` (default 0.5) |
| `preprocessorSymbol` | source | `#if` and `#elif` directives, including those in inactive branches, via `SyntaxTrivia` directive trivia; fires once per file per rule with the first location |

`preprocessorSymbol` reads directives from the syntax tree's trivia (`IfDirectiveTriviaSyntax`, `ElifDirectiveTriviaSyntax`), which Roslyn produces even for disabled text, so inactive branches are seen. The matcher walks the condition expression and compares identifier names, so `WINDOWS_PHONE_7` does not match `WINDOWS`.

Neither type needs semantic information. Both are added to the catalog schema; the schema version stays 1 (same approach as `nonStandardPair`).

## Manifest and controls file

- `Manifest` changes: new `ChangeKind.Skipped` entries for platform copies (the kind exists), a `shims` list entry `device` with the types added, and `manualWork` entries for choosers, media library, third-party assemblies.
- `DEVICE-CONTROLS.md` is generated from `DeviceControls` constants so the file and the code cannot drift. It lists keys, mouse actions, tilt values and what is not available. It is a normal `FileChange` in the plan.

## Error handling

- A rewriter that throws on one file: original copied, marker added, manifest entry (existing behavior).
- A solution with two Windows projects linking to each other: the planner picks none as a copy (ratio test needs a one-way link), reports an analyzer note, and migrates both.
- A cycle in links (A links B, B links A): treated as no copy.
- A copy whose linked-to project is not in the solution: kept as a normal project; the missing-file case is reported as a missing-file finding by the analyzer as today.
- `DeviceInput` cannot find a graphics manager: it skips the window step and logs one line.

## Testing strategy

1. **Fixtures from scratch** under `tests/fixtures/device/`: `wp7-xna`, `xbox360-copy-solution`, `zune-31`, `wp-silverlight`, `windows-plain` (negative for most rules). No third-party code.
2. **Rule tests**: positive and negative fixture per rule and per match type; the `preprocessorSymbol` positive fixture has the symbol only in an inactive branch.
3. **Rewriter tests**: before/after, ambiguous marker, byte-exact untouched file, determinism.
4. **Pure mapper tests**: `TiltMapper`, `GamePadMapper`, directive evaluator tables (no window).
5. **Integration**: migrate `wp7-xna` and build it with `dotnet build` (marked integration, like the existing end-to-end test).
6. **Read-only analyzer test** covers the new matchers (file hashes unchanged).
7. **Snapshot** (Verify): reports of each fixture, manifest and `DEVICE-CONTROLS.md`.

## Test candidates (read-only, nothing cloned into this repo)

Licenses are as GitHub reports them and as listed in `docs/test-candidates/xna-repos-classified.csv`. The rows were checked on 2026-11-11 through the GitHub API without cloning.

| # | Repo | Platform | License | Why it is useful | Caveat |
|---|---|---|---|---|---|
| 1 | [b3nk4n/vaderpixx-game](https://github.com/b3nk4n/vaderpixx-game) | Windows Phone (`XnaPlatform` Windows Phone, Reach) | MIT | Accelerometer helper, touch, `Microsoft.Phone` and ad assembly references: exercises XNA060, 061, 062, 063 | Needs the ad assemblies dropped. Art and sound license not checked, so assets stay out of the repo |
| 2 | [b3nk4n/spacepixx-game](https://github.com/b3nk4n/spacepixx-game) | Windows Phone | MIT | Same author, similar structure; a second sample for regression | Same |
| 3 | [b3nk4n/astropixx-game](https://github.com/b3nk4n/astropixx-game) | Windows Phone | MIT | Third sample of the family; lets us check that results are not specific to one game | Same |
| 4 | [randyrants/zuneshuffle](https://github.com/randyrants/zuneshuffle) | Zune (XNA 3.0) | MS-PL | Tiny (about 130 KB): Zune pad input and media library use, so XNA056 and the Zune path plus XNA006 | XNA 3.0, so it needs the 3.x rewrites first. A media-player app, not a game |
| 5 | [Generalkidd/XNAMiner](https://github.com/Generalkidd/XNAMiner) | Xbox 360, Windows, Phone, Zune (one codebase) | MIT | Already analyzed (score 38, see `VERIFIED.md`); likely has several platform symbols and copies | Not yet checked for copy projects |
| 6 | [VisualStudioEX3/Grey-Infection](https://github.com/VisualStudioEX3/Grey-Infection) | Xbox 360 | MIT (per GitHub) | Xbox 360 `GamePad` code and `#if XBOX` branches; already analyzed (score 37) | Assets not included; repo says it may not compile for Windows |
| 7 | [blendogames/flotilla](https://github.com/blendogames/flotilla) | Xbox 360 and PC | zlib (code only) | Xbox and PC in one codebase; platform symbols | Art and audio are not free; use the code only |

Not used: `martinsuchan/WP.Vlak` and `WP.NuPogodi` (GPL-2.0, copyleft; read-only analysis is fine, but they cannot become repo fixtures), `jkeech/Super-Space-Escape`, `Extron/XNAZuneGames` (no license), and the `Bitsits` repos (all rights reserved). `SimonDarksideJ/XNAGameStudio` is MIT but about 2.6 GB; if used, only a sparse checkout of the Catapult Wars Lab folder, and the per-sample license inside the folder must be read first.

Procedure (task 11): shallow clone one repo at a time into a scratch folder outside the repository, run the analyzer, migrate to a scratch output folder, record score and build result in a short table, delete the folders.

## Risks and shortcuts

- Syntactic matching can hit a user class named `GamePad` or `Accelerometer`. Guard: the namespace import, and the plan item can be excluded.
- shortcut: tilt axes and ease time are tuned by hand, not measured from a real device; revisit when a real game feels wrong.
- shortcut: the window is not scaled to fit small screens, only made resizable; add a letterbox scale if players report it.
- shortcut: only one tilt source is active at a time (keys by default); combine them if needed.
- XNA 3.1 Zune projects need task 10 rewriters first. A Zune game can still fail to build after this feature because of XNA 3.1 content (for example `.wma` or prebuilt `.xnb`), which the content-conversion spec covers.

## Implementation notes (analyzer, tasks 1 to 4)

IDs XNA051 to XNA063 are final: the first free range in `rules/xna-to-monogame.json` (XNA044 is a gap in the catalog, XNA100 to XNA119 is reserved by the older-and-vb spec, and monogame-upgrade uses MG ids). These differences from the text above came out of the code:

- **No `DevicePlatform` enum.** `ProjectInfo.Platform` already is an `XnaPlatform` with the same four values plus Unknown, so it is reused. `ProjectInfo` gained `PlatformInferred` and `LinkedSourceProjects` (both omitted from JSON reports when empty, so old snapshots do not change). `PlatformDetector` replaced the old `ParsePlatform` and now accepts `Xbox360` and `WindowsPhone`.
- **Inferred platform and XNA007.** XNA007 is a `projectProperty` rule, so it fires only on a stated `XnaPlatform`. An inferred platform is on `ProjectInfo` and the Markdown project table shows `(inferred)`; the device API rules still fire for it.
- **`TypeMatch.alsoNames`.** The type match takes one name, but XNA055, XNA056, XNA058 and XNA059 need several. An optional `alsoNames` array was added to the `type` match (schema, model, index). Nothing else about type matching changed.
- **XNA054** matches `Guide.IsTrialMode` only. `Guide.ShowMarketplace` is not matched (one name per member rule); the Compat `Guide` stand-in (task 7.3) still provides both. **XNA063** matches `Microsoft.Advertising.*` only; `AdDuplex.*` can be added as another rule later.
- **`projectLinks`** counts a `Compile` item as a link when it has `Link` metadata or its `Include` starts with `..\` or `../`. `LinkedSourceProjects` is resolved by walking up from each linked file to the nearest folder holding a `.csproj`. The matcher does not check that the owner is in the same solution; the planner (task 5.2) does that.
- **Fixture names** are flat (`tests/fixtures/device-wp7-xna`, and so on) because `FixturePaths` and the snapshot tests list top-level fixture folders. The Silverlight fixture is unconfirmed against a real project (see its README).
- **Scores.** XNA053 includes `WINDOWS`, so every Windows project with an `#if WINDOWS || XBOX` wrapper (the old Program.cs template) now gets one AutoFixable Warning. The `xna31-rewritable` real-catalog score moved from 76 to 75; the projected score is unchanged.
- XNA062 and XNA061 also fire on Windows projects that read a game pad or touch (Info, AutoFixable, 0.25 points each). R1.6 is not kept strictly for these two; a platform gate needs a rule field that the monogame-upgrade work is adding (`appliesTo`), so it is left for a follow-up.

## Implementation notes (migrator, tasks 5 and 6)

- **Skip decisions** live in `PlatformCopies.Find(report)` (Migrator, `Planning/`), a pure function of the report. A project is a copy when it has an XNA052 finding and its `LinkedSourceProjects` names a project that is in the report. Link cycles are dropped (no copies). Per group the project order is Windows (or unknown platform), Windows Phone, Xbox 360, Zune; among equals an original beats a copy. The planner replaces the XNA052 finding items with one skip item per skipped project (`XNA052:<project>`), and hides the other items of a skipped project while its skip item is included. Excluding the skip item migrates that project too. A project that is not the group's pick is skipped even when it is the original (the chosen project is then a copy).
- **Manifest.** A skipped project is a `Skipped` change at its project path, with the reason `platform copy of <project>; <kept> is migrated instead`, rule XNA052, and a note. Device-only groups also get a note saying which project was chosen and why.
- **Project generation** is unchanged: the target stays the user's choice (DesktopGL by default), and the generator already drops the device defines (XBOX, XBOX360, ZUNE, WINDOWS_PHONE). The XNA007 plan item now names the platform and which `#if` symbols become true.
- **Platform symbol set** comes from the platform of the project that is migrated (`PlatformSymbolSet.For`). A skipped sibling does not matter: when a Windows sibling is migrated, the migrated project is the Windows one. An unknown platform folds nothing.
- **XNA053 runs before ENTRYPOINT** through an explicit run-first list in `SourceRewriteEngine` (the registry still sorts by rule ID). For files whose entry point sits in `#if WINDOWS || XBOX`, the manifest now lists XNA053 instead of ENTRYPOINT; the text result is the same. ENTRYPOINT still runs afterwards and keeps its old behavior when XNA053 is excluded.
- **Entry point exception.** A group with no `#else` or `#elif`, a platform-only condition and a `static void Main(` in its body is unwrapped even when the platform set says false (a Phone or Zune project migrated alone would otherwise lose its `Main`). This is what ENTRYPOINT already did.
- Groups that mention a platform symbol but cannot be decided (for example `WINDOWS && DEBUG`) or cannot be parsed (for example `==`) are kept with one `// XNAREBORN: XNA053:` marker above the `#if`; unbalanced directives leave the file unchanged with one marker. Groups with no platform symbol (`#if DEBUG`, `#if false`) are never touched.
