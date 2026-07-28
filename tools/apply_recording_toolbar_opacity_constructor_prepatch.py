from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/RecordingForm.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

sentinel = "int recordingToolbarIdleOpacityPercent = 55)"
if sentinel in text:
    print("Recording opacity constructor already patched.")
else:
    marker = "bool showMagnifier = false)"
    if marker not in text:
        raise RuntimeError("RecordingForm showMagnifier constructor marker was not found.")
    text = text.replace(
        marker,
        "bool showMagnifier = false,\n"
        "                          bool fadeRecordingToolbarWhenIdle = true,\n"
        "                          int recordingToolbarIdleOpacityPercent = 55)",
        1,
    )
    path.write_text(text, encoding="utf-8")
    print("Recording opacity constructor patched.")
