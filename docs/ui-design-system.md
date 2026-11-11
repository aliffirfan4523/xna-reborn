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

The base is the Fluent theme. The accent is violet (`#5B2EB8` light, `#B9A1FF` dark), set in `App.axaml` (palette) and `AccentBrush`. The theme follows the system; the rail has a toggle (System, Light, Dark; not saved between runs).

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

Left rail (brand, six steps with done, current, locked states, theme toggle), header (project name, score chip), error banner, page area (max width 1080), status bar. Ctrl+1..6 jump to a step (the gestures are `Ctrl+D1`..`Ctrl+D6`). The step buttons keep `Step_<Name>` automation ids; the error banner keeps `ErrorBanner`, `ErrorCopy`, `ErrorDismiss`.

`MainViewModel` adds `ProjectName`, `HasProject`, `HasScore`, `ScoreText`, `StatusText` (additive; nothing renamed).

## Accessibility rules

- Text and icons meet WCAG AA against their surface in both themes (text on `AccentBrush` uses `OnAccentBrush`). Check new colors against `SurfaceBrush` and `BackgroundBrush`. Disabled and locked steps are exempt, and still say "Locked".
- Interactive targets are at least 32 px.
- Status always has an icon and a word.
- Keep every `AutomationProperties.AutomationId` and `Name`. Tests use them. Shell tab stops (steps, theme toggle) are skipped by page tab-order tests (`IsShellStop` in `KeyboardAccessTests`).
- Contrast is measured, not guessed: `ContrastTests` reads `Styles/Tokens.axaml` and checks every foreground and background pair in Light and Dark (text 4.5:1; icons, input borders and focus rings 3:1). A new color or a new place that uses a color goes into the pair list there. Fix a failing color in the token file, never per screen. `XNAREBORN_CONTRAST_REPORT=<file>` writes every ratio.
- Input borders (TextBox, ComboBox, CheckBox, RadioButton, ToggleSwitch) use `BorderBrushStrong` through aliases in `Tokens.axaml`, so they are covered by that test.
- Keyboard focus is Fluent's two-tone adorner (`FocusRingBrush` outside, `FocusRingInnerBrush` inside), set through the `SystemControlFocusVisual*` keys in `Tokens.axaml`. `Controls.axaml` turns it on for text inputs too. `ScreenAuditTests` presses Tab through every screen and fails on a stop with no ring.
- An icon or glyph never carries meaning alone: give a severity glyph `Classes="icon"` (hidden from screen readers) and put the word beside it. `ScreenAuditTests` also fails on an unnamed control, a control under 32 px, and an icon outside a named control.
- Live regions: the error banner is `Assertive`; `Banner` (non-error), the status bar, build status and indeterminate `ProgressLabel` are `Polite`.
- Headings: one level-1 title per screen, sections level 2 and below, no level skipped.
- Motion: every animation checks `MotionPreference.Reduce()`. `MainWindow` adds the class `reduce-motion`, which stops the indeterminate progress bar. Fluent's own short control transitions (Expander, toggles) are not switched off.
- Layout: `TextScaleTests` lays out each screen at 800x600, at 100% and 150% text, and fails on cut-off text, overlapping controls, or content outside the window. Use `Grid` columns with `TextWrapping="Wrap"` or a `WrapPanel` instead of a horizontal `StackPanel` for text, and `NarrowLayout.Attach` to stack side-by-side panels in a narrow window.
- Key gestures: write digits as `Ctrl+D1`. `Ctrl+1` is read as the key with code 1 (Backspace).
- Still needs a person with a real screen reader and a real display; the headless tests cannot hear or see.
