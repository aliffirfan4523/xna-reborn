# device-windows-plain

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

The negative fixture for the device rules. A Windows XNA 4.0 game with `#if WINDOWS`, a user class named `GamePad` in its own namespace (`WindowsPlain.Input`, no `using Microsoft.Xna.Framework.Input`), and a user type named `Accelerometer` without the `Microsoft.Devices.Sensors` using. It is never built.

## What the analyzer should find

- No XNA007, XNA051, XNA052, XNA054 to XNA063. The look-alike names must not fire.
- XNA053 does fire, for `#if WINDOWS`: a plain Windows `#if` is still folded by the migrator.
