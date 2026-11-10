# Requirements: Device Projects to Desktop (Xbox 360, Zune, Windows Phone)

## Introduction

Today rule XNA007 only flags projects whose `XnaPlatform` is Xbox 360, Zune or Windows Phone, marks them Unsupported, and tells the user to migrate a Windows sibling by hand. Many stranded XNA games exist only as device projects (for example the MIT-licensed Windows Phone games listed in `design.md`). This feature lets the migrator turn them into a playable Windows MonoGame game: it detects the device platform, skips duplicate platform copies, folds device-only `#if` branches, replaces or stubs device-only APIs, and maps touch, tilt and the hardware Back button to mouse and keyboard.

Scope: C# projects built with XNA Game Studio 3.x (Zune) and 4.0 (Xbox 360, Windows Phone OS 7.x XNA projects). Out of scope: Silverlight UI pages, Visual Basic, XACT and effect conversion (other specs), and decompiled commercial games (safety rule 6).

## Glossary

- **Device platform**: the value of the `XnaPlatform` project property: `Windows`, `Xbox 360`, `Zune` or `Windows Phone`.
- **Platform copy**: a project created by Game Studio's "Create Copy of ... for ..." command. It links source, content and settings from the original project and is named like `Windows Copy of X` or `Xbox 360 Copy of X` ([Microsoft: Adding New Platforms to an Existing Game](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb976061(v=xnagamestudio.41))).
- **Device symbols**: the conditional compilation symbols `XBOX`, `XBOX360`, `ZUNE` and `WINDOWS_PHONE`. `WINDOWS` is the desktop symbol.
- **Compat library**: the generated `XnaReborn.Compat` project (migrator task 12). Device types live in it next to the storage and profile stand-ins.
- **Input mapping layer**: the part of the Compat library that turns desktop input (mouse, keyboard, game pad) into what the device API returned (touch, gestures, accelerometer readings, Back button).
- **Tilt source**: the desktop input that stands in for the accelerometer. Keys by default.

## Requirements

### R1: Detect the device platform
**User story:** As a developer, I want the tool to know which device a project was built for, so it can choose the right conversion.

Acceptance criteria:
1. THE analyzer SHALL read `XnaPlatform` for every code project and record a `DevicePlatform` of Windows, Xbox360, Zune, WindowsPhone or Unknown on `ProjectInfo`.
2. THE analyzer SHALL accept the spellings `Xbox 360`, `Xbox360`, `Zune`, `Windows Phone` and `WindowsPhone`, ignoring case and surrounding spaces.
3. WHEN `XnaPlatform` is missing and the project references `Microsoft.Phone` or `Microsoft.Devices.Sensors`, THE analyzer SHALL record WindowsPhone and say in the finding that the platform was inferred.
4. WHEN `XnaPlatform` is missing and the project has XNA 3.x references and the symbol `ZUNE` in `DefineConstants`, THE analyzer SHALL record Zune.
5. THE analyzer SHALL record `XnaProfile` (Reach or HiDef) when present, and the report SHALL show both values.
6. THE analyzer SHALL keep `XnaPlatform` Windows and Unknown projects free of findings from this feature.

### R2: Report a device project as convertible
**User story:** As a developer, I want to be told that a device project can be converted, and what it will cost.

Acceptance criteria:
1. WHEN a project matches XNA007, THE analyzer SHALL report it as AutoFixable with severity Warning, linked to the migrator task that converts it, instead of Unsupported.
2. THE XNA007 explanation SHALL name the platform found and list which device API rules fired for the project.
3. WHEN a Windows Phone project is a Silverlight application (it sets `SilverlightApplication` to true or targets the Silverlight framework identifier), THE analyzer SHALL report it as Unsupported with its own rule, because Silverlight pages have no MonoGame equivalent.
4. THE analyzer SHALL NOT lower the score twice for the same device project: XNA007 counts once per project.

### R3: Platform copy projects
**User story:** As a developer with a solution that has a Windows project and an Xbox 360 or Phone copy, I want one migrated game, not three.

Acceptance criteria:
1. THE analyzer SHALL detect a platform copy when at least half of its `Compile` items link (`<Link>` metadata, or an `Include` path that leaves the project folder) to files that belong to another code project in the same solution.
2. WHEN a solution holds a Windows project and one or more platform copies of it, THE migrator SHALL migrate the Windows project and SHALL skip each copy.
3. THE migrator SHALL record each skipped copy in the manifest as Skipped, with the reason "platform copy of &lt;project&gt;" and the project it duplicates.
4. WHEN a solution holds only device projects and no Windows project, THE migrator SHALL migrate the one chosen by this order: Windows Phone, Xbox 360, Zune, and SHALL record why in the manifest.
5. THE user SHALL be able to override the choice by excluding or including the skip plan item in the migration plan.
6. THE migrator SHALL NOT copy or merge files from a skipped copy into the migrated project.

### R4: Device conditional compilation
**User story:** As a developer, I want `#if XBOX` and `#if WINDOWS_PHONE` blocks resolved, because those symbols no longer exist.

Acceptance criteria:
1. THE analyzer SHALL find conditional directives (`#if`, `#elif`) that name a device symbol or `WINDOWS`, including inside inactive branches.
2. THE migrator SHALL fold each such directive whose condition depends only on platform symbols, keeping the branch that is true for the chosen platform set and removing the others, with all other text unchanged byte for byte.
3. THE chosen platform set SHALL be: the source project's own device symbols true and the other device symbols false when no Windows sibling is migrated; `WINDOWS` true and all device symbols false when a Windows sibling is migrated; `WINDOWS` is false for a device-only project.
4. WHEN a condition also depends on a symbol that is not a platform symbol (for example `DEBUG`) and that cannot decide the result, THE migrator SHALL leave the directive unchanged and insert an `// XNAREBORN: XNA053:` marker.
5. THE migrator SHALL handle `!`, `&&`, `||`, parentheses, `#else` and `#elif`, and SHALL leave `#if` blocks it cannot parse unchanged with a marker.
6. THE migrated project SHALL NOT define `WINDOWS`, `XBOX`, `XBOX360`, `ZUNE` or `WINDOWS_PHONE` (this keeps the earlier decision in `project-migrator` that entry-point code is rewritten, not rescued by a define).
7. THE entry-point rewriter SHALL still run after folding, and SHALL give the same result as before on files with no device symbols.

### R5: Device-only APIs
**User story:** As a developer, I want code that calls dead device APIs to compile and behave sensibly offline.

Acceptance criteria:
1. THE migrator SHALL map each device-only API in the table below, using the Compat library where a stand-in is listed and a manual marker otherwise.

   | API group | Namespace or type | Treatment |
   |---|---|---|
   | Marketplace and trial | `Guide.IsTrialMode`, `Guide.ShowMarketplace`, `MarketplaceDetailTask`, `MarketplaceReviewTask`, `MarketplaceSearchTask`, `LicenseInformation` | Stand-in: trial mode is always false, show calls do nothing |
   | Zune media library | `MediaLibrary`, `MediaSource`, `PlayCount`, `Rating`, Zune `Album` and `Artist` browsing | Manual marker; the game is left to be adapted |
   | Phone shell and tombstoning | `Microsoft.Phone.Shell.PhoneApplicationService` (`State`, `Activated`, `Deactivated`, `Closing`, `Launching`) | Stand-in: in-memory `State`, events never raised |
   | Launchers | `Microsoft.Phone.Tasks` launchers (`WebBrowserTask`, `EmailComposeTask`, `PhoneCallTask`, `SmsComposeTask`, `MarketplaceHubTask`) | Stand-in: `Show()` records a line in the log and does nothing |
   | Choosers | `Microsoft.Phone.Tasks` choosers (`PhotoChooserTask`, `CameraCaptureTask`, `SaveRingtoneTask`) | Manual marker; their `Completed` event never fires |
   | Accelerometer | `Microsoft.Devices.Sensors.Accelerometer` | Input mapping layer (R6) |
   | Touch | `TouchPanel`, `GestureType`, `GestureSample` | Input mapping layer (R6) |
   | Back button and game pad | `GamePad.GetState`, `Buttons.Back` | Input mapping layer (R6) |
   | Screen shape | `PreferredBackBufferWidth/Height` of 480x800, 800x480, 480x272, 240x320 and `SupportedOrientations` | Kept as written; the window starts windowed (R7) |

2. THE migrator SHALL leave a type or member untouched when its name matches but the file has no `using` for the device namespace (a user type with the same name is not rewritten).
3. THE manifest SHALL list every stand-in that was added and every API left as a marker.
4. IF a project references a third-party device assembly (for example `Microsoft.Advertising.Mobile`), THEN THE analyzer SHALL report it as Manual and THE migrator SHALL leave the reference out and mark the using sites.
5. WHERE the Compat library is generated, THE library SHALL use the original namespaces of the APIs it stands in for, so game code compiles without edits to its `using` lines, as the storage and profile stand-ins already do.

### R6: Input mapping layer
**User story:** As a player on a PC, I want to play a touch or tilt game with a mouse and keyboard.

Acceptance criteria:
1. WHEN a project uses `TouchPanel`, THE migrated game SHALL enable the mouse as a touch point and mouse gestures at startup, so that a left-button press and drag appear as one touch location and as Tap, FreeDrag and DragComplete gestures.
2. WHEN a project uses `Microsoft.Devices.Sensors.Accelerometer`, THE Compat library SHALL provide an `Accelerometer` with the same public surface as the Windows Phone type (`IsSupported`, `State`, `Start`, `Stop`, `CurrentValue`, `ReadingChanged`, `TimeBetweenUpdates`, `Dispose`) and `ReadingChanged` SHALL fire from the game loop, never from another thread.
3. THE tilt source SHALL be the arrow keys and W, A, S, D by default: each held key moves the reading toward a fixed tilt value, and releasing the keys returns it to the resting value, with the change eased over a few frames instead of jumping.
4. THE resting reading SHALL be the device lying face up, and the tilt values, easing time and key bindings SHALL be constants in one place that the generated `DEVICE-CONTROLS.md` repeats.
5. WHERE the user chooses the mouse tilt source, THE tilt SHALL follow the mouse offset from the window centre, with the same value range as the keys.
6. THE Compat library SHALL provide a `GamePad` replacement for calls to `GamePad.GetState(PlayerIndex)` that returns the real pad state when a pad is connected, and a keyboard-built state when none is: arrow keys or W, A, S, D for the left stick and D-pad, Space or Enter for A, Escape for Back, and Tab for Start.
7. WHEN the hardware Back button was how the device game exited or went back, THE Escape key SHALL be reported as `Buttons.Back` pressed.
8. THE migrator SHALL change `GamePad.GetState(...)` calls to the Compat replacement only in files that import `Microsoft.Xna.Framework.Input` and only when the receiver is the identifier `GamePad`.
9. IF a file already calls `GamePad.GetCapabilities` or `GamePad.SetVibration`, THEN THE migrator SHALL leave those calls alone, because MonoGame supports them.
10. THE migrated project SHALL start with the mouse cursor visible, because device games hide or never show it.
11. THE mapping layer SHALL NOT start a thread, open a window, or call the network.

### R7: Window and screen
**User story:** As a developer, I want a phone game to open in a usable window.

Acceptance criteria:
1. WHEN the source platform is a device, THE migrated game SHALL start in a window, not full screen, even when the code sets `IsFullScreen = true`.
2. THE requested back buffer size SHALL be kept, so a 480x800 game opens in a portrait window of that size and a 272x480 Zune HD game keeps its logical size.
3. WHERE the requested height is larger than the usable height of the display, THE Compat library SHALL make the window user-resizable so the player can still reach the whole picture.
4. THE migrator SHALL NOT change any game coordinates, textures or touch positions.

### R8: Safe, transparent, and deterministic output
**User story:** As a developer, I want to see exactly what changed.

Acceptance criteria:
1. THE migrator SHALL write only to the migrated copy and SHALL NOT change the source project (project-migrator R1).
2. EVERY change from this feature SHALL appear as a plan item with the rule that caused it, so it can be excluded and shown in the diff preview.
3. WHEN the input mapping layer is included, THE migrator SHALL write `DEVICE-CONTROLS.md` to the output folder, listing every key, mouse action and tilt value the player needs.
4. THE migrator SHALL record Compat library use and its device types in the manifest.
5. TWO runs on the same input SHALL give identical output.
6. THE device features SHALL NOT upload any file, call any web service, or require the network.

### R9: Tests and fixtures
**User story:** As a maintainer, I want every rule and rewrite tested without third-party code.

Acceptance criteria:
1. EVERY new or changed rule SHALL have one positive and one negative fixture test.
2. ALL fixtures SHALL be written from scratch for this feature, with no code or art copied from a real game.
3. EVERY new rewriter SHALL have before/after tests, an ambiguous-case test that produces a marker, and a test that an untouched file keeps its bytes.
4. THE tilt, gesture-mouse and game-pad mapping logic SHALL be pure functions, tested without a window or graphics device.
5. AT LEAST ONE migrated fixture SHALL be built with `dotnet build` in an integration test.
6. THE analyzer SHALL keep its read-only test (file hashes unchanged) for the new match types.
7. NO test SHALL need the network.

### R10: Candidate games
**User story:** As a maintainer, I want real open-source device games to check the feature against.

Acceptance criteria:
1. `design.md` SHALL list at least three permissively licensed open-source Phone, Zune or Xbox 360 XNA games, with license, platform, and what each one tests.
2. THE checks on those games SHALL be read-only: shallow clone to a scratch folder outside the repository, run the analyzer, then migrate to a scratch output folder, and delete the clone afterward.
3. NO candidate game or asset SHALL be committed to the repository (safety rules 5 and 6).
