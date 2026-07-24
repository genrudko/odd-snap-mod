from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/RecordingForm.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

sentinel = "internal bool FadeRecordingToolbarWhenIdle"
if sentinel in text:
    print("Recording toolbar opacity properties already patched.")
else:
    marker = "    protected override CreateParams CreateParams"
    if marker not in text:
        raise RuntimeError("RecordingForm CreateParams marker was not found.")

    properties = """    internal bool FadeRecordingToolbarWhenIdle => _fadeRecordingToolbarWhenIdle;

    internal int RecordingToolbarIdleOpacityPercent => _recordingToolbarIdleOpacityPercent;

"""
    text = text.replace(marker, properties + marker, 1)
    path.write_text(text, encoding="utf-8")
    print("Recording toolbar opacity properties patched.")
