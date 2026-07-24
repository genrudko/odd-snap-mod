from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/OddSnap/App/App.Capture.cs"

OLD = """                if (!TryPostToAppDispatcher(
                        () => LaunchGifRecording(RecordingCaptureTarget.ForRegion(screenRegion), openResultWindow: true),
                        DispatcherPriority.Background,
                        \"capture.snipping-recording-post\"))
                {
                    ResetCapturingWithoutUiRestore();
                }
"""

NEW = """                // LaunchGifRecording only creates the dedicated recording STA thread.
                // Calling it directly here avoids losing the handoff when the overlay
                // closes before a queued dispatcher callback gets a chance to run.
                LaunchGifRecording(
                    RecordingCaptureTarget.ForRegion(screenRegion),
                    openResultWindow: true);
"""


def main() -> None:
    text = TARGET.read_text(encoding="utf-8")
    if NEW in text:
        print("Snipping recording handoff repair already applied.")
        return
    if OLD not in text:
        raise RuntimeError("Expected snipping recording dispatcher handoff was not found.")
    TARGET.write_text(text.replace(OLD, NEW, 1), encoding="utf-8")
    print("Snipping recording handoff repair applied.")


if __name__ == "__main__":
    main()
