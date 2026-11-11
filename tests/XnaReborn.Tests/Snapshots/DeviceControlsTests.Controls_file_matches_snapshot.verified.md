# Device controls

This game was built for a phone, a Zune or an Xbox 360. On a desktop the device input is stood in for by the keyboard and the mouse.
The numbers below come from `XnaReborn.Compat/Device/DeviceControls.cs`. Change them there.

## Keyboard

| Action | Keys |
|---|---|
| Left stick and D-pad left, tilt left | Left, A |
| Left stick and D-pad right, tilt right | Right, D |
| Left stick and D-pad up, tilt up (top edge down) | Up, W |
| Left stick and D-pad down, tilt down (top edge up) | Down, S |
| A button | Space, Enter |
| B button | Z |
| X button | X |
| Y button | C |
| Back button (the phone's hardware Back button) | Escape |
| Start button | Tab |
| Left shoulder button | Q |
| Right shoulder button | E |

A connected game pad is used as it is. Escape is also reported as the Back button while a pad is connected.
Only player one has the keyboard. Players two to four use their own pads.

## Mouse and touch

- The mouse cursor is visible.
- Hold the left mouse button to touch the screen. A press is a Tap, a drag is FreeDrag, and releasing it is DragComplete.
- Only one touch point is possible, so pinch and other two-finger gestures are not available.

## Tilt (accelerometer)

| Setting | Value |
|---|---|
| Source | the move keys above |
| Resting reading (device lying face up) | (0.0, 0.0, -1.0) g |
| Full tilt on X and Y | 0.7 g |
| Easing time constant | 0.12 s |
| Time between readings | 20.0 ms |

Readings are raised from the game loop, never from another thread. Axes follow the Windows Phone sensor: X to the right, Y toward the top of the screen, Z out of the screen.

## Window

- The game starts in a window, even when its code asks for full screen.
- The back buffer keeps the size the game asked for. When it is taller than the display, the window can be resized.

## Not available on a desktop

- Phone choosers (photo, camera, ringtone): they throw `NotSupportedException`.
- The device media library, phone launchers (they only write a log line), and the marketplace (the game always counts as the full version).
- Multi-touch, vibration and the phone shell events (`Activated`, `Deactivated` and the like never fire).
- Web links: not opened. Set `OpenWebLinks` in DeviceControls.cs to allow it.
