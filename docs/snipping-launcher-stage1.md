# Snipping launcher — stage 1

This stage adds a Windows Snipping Tool-style launch flow on top of the existing OddSnap screenshot and recording engines.

## Entry points

- The configured primary **Capture** hotkey opens the screenshot launcher. It can be bound directly to `PrintScreen`.
- The configured **GIF / recording** hotkey opens the recording launcher.
- PowerToys Keyboard Manager can remap `Win + Shift + R` to the configured OddSnap recording hotkey when Windows reserves the original chord.
- Screenshot and recording modes can be switched from the top launcher bar without closing the overlay.

## Screenshot flow

1. Open the screenshot launcher.
2. Choose rectangle, freeform, window, or fullscreen capture.
3. Complete the capture.
4. OddSnap immediately opens a result window with copy, save-as, open-folder, and new-capture actions.

## Recording flow

1. Open the recording launcher.
2. Drag a rectangular region.
3. Press **Start recording** in the top launcher bar.
4. OddSnap starts the existing region recorder with pause, resume, stop, and discard controls.
5. The recording controls can be dragged by any non-button area and remain inside the active monitor working area.
6. After a short idle delay the controls fade to a translucent state; moving the pointer over them restores full opacity.
7. A manually chosen toolbar position is preserved when a tracked window moves and is transferred to the equivalent position when the target changes monitors.
8. After encoding, OddSnap immediately opens the result window with playback controls.

## Suggested PowerToys mapping

Keep OddSnap's recording hotkey on a technically safe chord, for example `Ctrl + Alt + Shift + F12`, and configure PowerToys Keyboard Manager as follows:

```text
Win + Shift + R  →  Ctrl + Alt + Shift + F12
```

OddSnap remains responsible for the capture workflow; PowerToys only translates the system shortcut.

## Manual acceptance checks

1. Bind Capture to `PrintScreen` and confirm the screenshot launcher appears on the monitor containing the pointer.
2. Remap `Win + Shift + R` through PowerToys to the configured recording hotkey and confirm the recording launcher opens.
3. Switch screenshot/recording modes in-place and verify stale selections are cleared.
4. Verify rectangle, freeform, window, and fullscreen screenshots open in the result window.
5. Verify a recording does not start on mouse release; the Start recording button must appear after a valid region is selected.
6. Drag the recording controls from the timer area and verify that pause, stop, and discard still work.
7. Leave the pointer away from the controls and verify that they fade; hover them and verify that full opacity returns.
8. Move a tracked recording window between monitors and verify that a manually positioned toolbar remains inside the destination monitor.
9. Verify pause/resume and audio behavior remain correct.
10. Verify MP4, WebM, MKV, and GIF results open correctly.
11. Verify `Esc` cancels both launcher modes.
12. Verify mixed-DPI and multi-monitor placement.