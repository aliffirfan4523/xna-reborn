# UI design system

For workers building screens in `src/XnaReborn.App`. Use tokens and shared styles instead of literal colors, sizes and fonts. UI code never holds migration logic.

Before/after screenshots: `docs/ui/before-*.png`, `docs/ui/after-*.png`. Refresh them with
`XNAREBORN_SCREENSHOTS=<folder> XNAREBORN_SCREENSHOT_PREFIX=after dotnet test tests/XnaReborn.App.Tests --filter Capture_every_screen`.

## Files

| File | Holds |
| --- | --- |
| `Styles/Tokens.axaml` | colors (Light and Dark), spacing, radius, type scale, elevation, motion |
| `Styles/Icons.axaml` | vector icons (`StreamGeometry`, Material Design Icons paths, Apache-2.0) |
| `Styles/Controls.axaml` | shared styles (classes) and the Banner and EmptyState control themes |
| `Styles/Shared.axaml` | older spacing keys and severity icon and label strings |
| `Controls/` | Chip, StatTile, ProgressLabel, Banner, EmptyState, ScoreGauge, SeverityBadge |

The base is the Fluent theme. The accent is violet (`#5B2EB8` light, `#B9A1FF` dark), set in `App.axaml` (palette) and `AccentBrush`. The theme follows the system by default. The rail toggle and the Settings page change the same saved setting (System, Light, Dark).

## Tokens

Colors are brushes. Use `{DynamicResource Key}`.

- Surfaces: `BackgroundBrush`, `SurfaceBrush`, `SurfaceAltBrush`, `SurfaceHoverBrush`, `RailBrush`
- Lines: `BorderBrushSubtle`, `BorderBrushStrong`, `FocusRingBrush`
- Text: `TextBrush`, `TextMutedBrush`, `AccentTextBrush`, `OnAccentBrush` (text on `AccentBrush`)
- Accent: `AccentBrush`, `AccentHoverBrush`, `AccentPressedBrush`, `AccentSoftBrush`
- Status: `Success*`, `SeverityInfoBrush`, `SeverityWarningBrush`, `SeverityErrorBrush`, `SeverityBlockerBrush`, and the tints `SuccessSoftBrush`, `InfoSoftBrush`, `WarningSoftBrush`, `ErrorSoftBrush`
- Spacing (4 pt grid): `Space1`..`Space7` = 4, 8, 12, 16, 24, 32, 48. Also `CardPadding`, `ContentMaxWidth` (1080), `MinTarget` (32)
- Radius: `RadiusSmall` 6, `RadiusMedium` 10, `RadiusLarge` 16, `RadiusPill`
- Type: `FontSizeCaption` 12, `Body` 14, `Subtitle` 16, `Title` 20, `Headline` 28, `Display` 40 (font: Inter)
- Elevation: `ElevationLow` (cards), `ElevationHigh` (popups)
- Motion: `MotionFast` 120 ms, `MotionNormal` 180 ms. Page fade honors reduced motion (`Controls/MotionPreference.cs`: Windows "Show animations" off, or `XNAREBORN_REDUCE_MOTION=1`).

## Classes

- Text: `TextBlock.display | headline | title | subtitle | caption | muted | section`. Page titles use `headline` with `AutomationProperties.HeadingLevel="1"`.
- Buttons: `Classes="primary"` (one per screen), `secondary` (normal), `subtle` (low emphasis). All have a 32 px minimum and a 2 px focus ring.
- Surfaces: `Border.card` (add `flat`, `compact`), `Border.divider`.
- Navigation: `Button.nav-item`, `Border.step-dot` (shell only).

## Controls

All live in `XnaReborn.App.Controls`. `StatusKind` is Neutral, Accent, Success, Info, Warning, Error. Each kind gets a tint, a default icon and a spoken word, so color is never the only cue.

```xml
<controls:Chip Kind="Success" Text="Builds" />
<controls:Chip Kind="Accent" Icon="{StaticResource IconChart}" Text="Score 72" />
<controls:Banner Kind="Warning" Title="Dry run" Message="Nothing was written.">
  <Button Classes="secondary" Content="Back to setup" />
</controls:Banner>
<controls:EmptyState Title="No recent projects" Message="Open a project to see it here.">
  <Button Classes="primary" Content="Browse folder..." />
</controls:EmptyState>
<controls:StatTile Kind="Error" Label="Blockers" Value="3" />
<controls:ProgressLabel Label="Analyzing" Value="40" />   <!-- or IsIndeterminate="True" -->
<controls:ScoreGauge Score="40" ProjectedScore="90" />
<controls:SeverityBadge Severity="Warning" />
<PathIcon Data="{StaticResource IconCheck}" Width="16" Height="16" />
```

`Chip`, `Banner`, `StatTile` and `ProgressLabel` set their own `AutomationProperties.Name`. Give your own controls a name when they have no text.

## Shell

Left rail (brand, six steps with done, current, locked states, a Settings gear, theme toggle), header (project name, score chip), error banner, page area (max width 1080), status bar. Ctrl+1..6 jump to a step. Ctrl+7 and Ctrl+, open Settings. In XAML write digit gestures as `Ctrl+D1`..`Ctrl+D7`: a bare `Ctrl+1` is read as key code 1 (Cancel) and never fires. Settings is not a step: `MainViewModel.IsSettingsOpen` swaps the page and no step is current while it is open. The gear keeps the id `Nav_Settings`. The step buttons keep `Step_<Name>` automation ids; the error banner keeps `ErrorBanner`, `ErrorCopy`, `ErrorDismiss`.

`MainViewModel` adds `ProjectName`, `HasProject`, `HasScore`, `ScoreText`, `StatusText` (additive; nothing renamed).

## Settings page

`SettingsViewModel` and `Views/SettingsView.axaml`. Settings are defaults: Open, Migration setup and Build and run read them when a project is opened, and a choice made on a page for one run wins. Changes save at once through `ISettingsService` (`%APPDATA%/XnaReborn/settings.json`, versioned; a missing, corrupt or newer file gives the defaults and a notice on the page). Tests use `FakeSettingsService`.

- Layout: one `Border.card` per group (Appearance, Migration defaults, Build and run, Recent projects, Privacy and safety). Each setting is a bold label plus a `caption muted` help line; a `ComboBox` sits on the right, a `CheckBox` carries both lines as its content.
- Give every control an `AutomationId` that starts with `Setting` and a `Name`. Tab order is tested in `SettingsViewTests`.
- Add a setting: a property on `AppSettings`, a field in `SettingsViewModel` (and its `_saved` list), a row in the view, then read it where the default is applied.
- Never add a setting that skips the launch question, overwrites an output folder without asking, or uploads anything. `SettingsViewModelTests` checks setting names for such words. The page shows a read-only note: "AI repair is off. Nothing is uploaded."

## Accessibility rules

- Text and icons meet WCAG AA against their surface in both themes (text on `AccentBrush` uses `OnAccentBrush`). Check new colors against `SurfaceBrush` and `BackgroundBrush`. Disabled and locked steps are exempt, and still say "Locked".
- Interactive targets are at least 32 px.
- Status always has an icon and a word.
- Keep every `AutomationProperties.AutomationId` and `Name`. Tests use them. Shell tab stops (steps, Settings gear, theme toggle) are skipped by page tab-order tests (`IsShellStop` in `KeyboardAccessTests`).
- Full AA validation needs manual checks with assistive tools; contrast here was chosen by hand, not measured by a tool.
