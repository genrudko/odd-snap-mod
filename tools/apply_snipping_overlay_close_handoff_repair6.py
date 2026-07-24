from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
INPUT_HELPERS = ROOT / "src/OddSnap/Capture/RegionOverlayForm.Input.Helpers.cs"
APP_CAPTURE = ROOT / "src/OddSnap/App/App.Capture.cs"
TARGETS = ROOT / "src/OddSnap/Capture/RecordingForm.Targets.cs"
START_TESTS = ROOT / "src/OddSnap.Tests/SnippingRecordingStartTests.cs"


def replace_once(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected fragment was not found in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def main() -> None:
    changed = False

    changed |= replace_once(
        INPUT_HELPERS,
        """        if (tool.Id == \"_snipStart\")
        {
            if (_snippingLauncherMode == SnippingLauncherMode.Recording &&
                _selectionRect.Width > 2 && _selectionRect.Height > 2)
            {
                RecordingRegionSelected?.Invoke(_selectionRect);
            }
            return;
        }
""",
        """        if (tool.Id == \"_snipStart\")
        {
            if (_snippingLauncherMode == SnippingLauncherMode.Recording &&
                _selectionRect.Width > 2 && _selectionRect.Height > 2)
            {
                BeginSnippingRecordingHandoff(_selectionRect);
            }
            return;
        }
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """        Bitmap? screenshot = null;
        bool captureFlowHandedOff = false;
        try
""",
        """        Bitmap? screenshot = null;
        bool captureFlowHandedOff = false;
        RecordingCaptureTarget? pendingRecordingTarget = null;
        try
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """            overlay.RecordingRegionSelected += sel =>
            {
                captureFlowHandedOff = true;
                overlay.Hide();
                overlay.Close();
                var screenRegion = new Rectangle(
                    bounds.X + sel.X,
                    bounds.Y + sel.Y,
                    sel.Width,
                    sel.Height);

                // LaunchGifRecording only creates the dedicated recording STA thread.
                // Calling it directly here avoids losing the handoff when the overlay
                // closes before a queued dispatcher callback gets a chance to run.
                LaunchGifRecording(
                    RecordingCaptureTarget.ForRegion(screenRegion),
                    openResultWindow: true);
            };
""",
        """            overlay.RecordingRegionSelected += sel =>
            {
                captureFlowHandedOff = true;
                var screenRegion = new Rectangle(
                    bounds.X + sel.X,
                    bounds.Y + sel.Y,
                    sel.Width,
                    sel.Height);
                pendingRecordingTarget = RecordingCaptureTarget.ForRegion(screenRegion);
            };
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """            overlay.FormClosed += (_, _) =>
            {
                screenshot?.Dispose();
                if (captureFlowHandedOff)
                    return;
                screenshot = null;

                var mode = overlay.CurrentMode;
""",
        """            overlay.FormClosed += (_, _) =>
            {
                screenshot?.Dispose();
                screenshot = null;

                // The old launcher and its owned layered windows are now fully closed.
                // Only at this point may the dedicated recording form be created.
                if (pendingRecordingTarget is not null)
                {
                    LaunchGifRecording(pendingRecordingTarget, openResultWindow: true);
                    return;
                }

                if (captureFlowHandedOff)
                    return;

                var mode = overlay.CurrentMode;
""",
    )

    changed |= replace_once(
        TARGETS,
        """    internal bool IsRecordingActiveForTests =>
        _state == State.Recording && (_recorder is not null || _videoRecorder is not null);
""",
        """    internal bool IsRecordingActiveForTests =>
        _state == State.Recording && (_recorder is not null || _videoRecorder is not null);

    internal bool IsRecordingChromeVisibleForTests =>
        _recordingBorderForm?.Visible == true && _recordingToolbarForm?.Visible == true;
""",
    )

    changed |= replace_once(
        START_TESTS,
        """                    if (form.IsRecordingActiveForTests)
                    {
                        started = true;
""",
        """                    if (form.IsRecordingActiveForTests && form.IsRecordingChromeVisibleForTests)
                    {
                        started = true;
""",
    )

    print("Sequential snipping overlay close/recording handoff repair applied." if changed
          else "Sequential snipping overlay close/recording handoff repair already applied.")


if __name__ == "__main__":
    main()
