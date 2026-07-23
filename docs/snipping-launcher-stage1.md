# Snipping launcher — stage 1

This stage adds a Windows Snipping Tool-style launch flow on top of the existing OddSnap screenshot and recording engines.

## Entry points

- The configured primary Capture hotkey opens the screenshot launcher. It can be bound directly to PrintScreen.
- The configured GIF/recording hotkey opens the recording launcher. PowerToys can remap Win+Shift+R to that OddSnap hotkey when Windows reserves the original chord.
- Screenshot and recording modes can be switched from the top launcher bar without closing the overlay.

## Screenshot flow

1. Open the screenshot launcher.
2. Choose rectangle, freeform, window, or fullscreen capture.
3. Complete the capture.
4. OddSnap immediately opens a result window with copy, save-as, open-folder, and new-capture actions.

## Recording flow

1. Open the recording launcher.
2. Drag a rectangular region.
3. Press Start recording in the top launcher bar.
4. OddSnap starts the existing region recorder with pause, resume, stop, and discard controls.
5. After encoding, OddSnap immediately opens the result window with playback controls.

## Manual acceptance checks

1. Bind Capture to PrintScreen and confirm the screenshot launcher appears on the monitor containing the pointer.
2. Remap Win+Shift+R through PowerToys to the configured recording hotkey and confirm the recording launcher opens.
3. Switch screenshot/recording modes in-place and verify stale selections are cleared.
4. Verify rectangle, freeform, window, and fullscreen screenshots open in the result window.
5. Verify a recording does not start on mouse release; it starts only after pressing the Start button.
6. Verify pause/resume and audio behavior remain correct.
7. Verify MP4, WebM, MKV, and GIF results open correctly.
8. Verify Esc cancels both launcher modes.
9. Verify mixed-DPI and multi-monitor placement.
