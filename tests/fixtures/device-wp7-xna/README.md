# device-wp7-xna

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A tiny Windows Phone (OS 7.x) XNA 4.0 game. `XnaPlatform` is `Windows Phone`, `XnaProfile` is `Reach`, and the project references `Microsoft.Phone` and `Microsoft.Devices.Sensors`. `Game1` asks for a 480x800 back buffer and uses `TouchPanel` gestures, an `Accelerometer` with `ReadingChanged`, `GamePad.GetState` with `Buttons.Back`, `PhoneApplicationService.Current.State` and `Guide.IsTrialMode`, with `#if WINDOWS_PHONE` / `#else` blocks. The one texture is generated in code, so there are no asset files. It is only built after migration.

## What the analyzer should find

- XNA007 (Windows Phone device project), with `Platform` Windows Phone read from `XnaPlatform` (not inferred).
- XNA053 (device symbol `WINDOWS_PHONE`), XNA054, XNA057, XNA060, XNA061 and XNA062.
- XNA063 is not expected: there are no ad assemblies. XNA051 and XNA052 are not expected: the project is not a Silverlight application and links no files.

## Sources

The project shape follows the Microsoft documentation for [Windows Phone XNA projects](https://learn.microsoft.com/en-us/previous-versions/windows/xna/ff604985(v=xnagamestudio.41)) and [adding new platforms](https://learn.microsoft.com/en-us/previous-versions/windows/xna/bb976061(v=xnagamestudio.41)). No code was copied from any game.
