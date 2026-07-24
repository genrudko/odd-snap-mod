from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP_CAPTURE = ROOT / "src/OddSnap/App/App.Capture.cs"
RECORDING_FORM = ROOT / "src/OddSnap/Capture/RecordingForm.cs"
RECORDING_TARGETS = ROOT / "src/OddSnap/Capture/RecordingForm.Targets.cs"


def replace_once(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected source fragment not found in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def main() -> None:
    changed = False

    changed |= replace_once(
        APP_CAPTURE,
        """                bool showCursor = settings.ShowCursor;
                var capture = ScreenCapture.CaptureAllScreens(showCursor);
                selectionScreenshot = capture.Bitmap;
                var bounds = capture.Bounds;
                var s = settings;
""",
        """                bool showCursor = settings.ShowCursor;
                Rectangle bounds;
                if (preselectedTarget is null)
                {
                    var capture = ScreenCapture.CaptureAllScreens(showCursor);
                    selectionScreenshot = capture.Bitmap;
                    bounds = capture.Bounds;
                }
                else
                {
                    // A Snipping Launcher selection already supplied the final physical
                    // desktop bounds. Do not show a second frozen screenshot-selection
                    // surface before recording starts.
                    bounds = ScreenCapture.GetVirtualScreenBounds();
                }
                var s = settings;
""",
    )

    changed |= replace_once(
        RECORDING_FORM,
        """        _escapeHook = CaptureEscapeKeyHook.Install(this, CancelFromEscape);
        _selectionAdorner?.Show(this);
    }
""",
        """        _escapeHook = CaptureEscapeKeyHook.Install(this, CancelFromEscape);
        _selectionAdorner?.Show(this);

        // Preselected monitor/window/region workflows must enter recording from this
        // deterministic lifecycle point. Relying on an externally attached Shown
        // handler allowed the form to remain indefinitely in its selection phase.
        if (_preselectedTargetStartQueued)
            StartPreselectedTargetNow();
    }
""",
    )

    changed |= replace_once(
        RECORDING_TARGETS,
        """        if (_preselectedTargetStartQueued)
            return;

        _preselectedTargetStartQueued = true;
        Shown += StartPreselectedTargetAfterShown;
    }

    private void StartPreselectedTargetAfterShown(object? sender, EventArgs e)
    {
        Shown -= StartPreselectedTargetAfterShown;

        if (_state != State.Selecting || _captureTarget is null)
            return;

        BeginInvoke(new Action(() =>
        {
            StartRecording();
            StartWindowChromeTracking();
        }));
    }
""",
        """        _preselectedTargetStartQueued = true;

        // The target is already final, so the selection adorner and crosshair must
        // never be shown for this form. The OnShown lifecycle starts recording.
        _selectionAdorner?.Dispose();
        _selectionAdorner = null;
        Cursor = Cursors.Default;
    }

    private void StartPreselectedTargetNow()
    {
        if (!_preselectedTargetStartQueued ||
            _state != State.Selecting ||
            _captureTarget is null)
        {
            return;
        }

        _preselectedTargetStartQueued = false;
        StartRecording();
        StartWindowChromeTracking();
    }

    internal bool IsRecordingActiveForTests =>
        _state == State.Recording && (_recorder is not null || _videoRecorder is not null);
""",
    )

    print("Preselected recording start repair applied." if changed else "Preselected recording start repair already applied.")


if __name__ == "__main__":
    main()
