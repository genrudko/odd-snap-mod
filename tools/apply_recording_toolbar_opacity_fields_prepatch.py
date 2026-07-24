from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/RecordingForm.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

sentinel = "private readonly bool _fadeRecordingToolbarWhenIdle;"
if sentinel in text:
    print("Recording toolbar opacity fields already patched.")
else:
    marker = "    private readonly bool _showMagnifier;"
    if marker not in text:
        raise RuntimeError("RecordingForm showMagnifier field marker was not found.")

    fields = (
        marker
        + "\n    private readonly bool _fadeRecordingToolbarWhenIdle;"
        + "\n    private readonly int _recordingToolbarIdleOpacityPercent;"
    )
    text = text.replace(marker, fields, 1)
    path.write_text(text, encoding="utf-8")
    print("Recording toolbar opacity fields patched.")
