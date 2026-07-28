from pathlib import Path

path = Path(__file__).resolve().parent / "apply_recording_safety_audio_repair11.py"
text = path.read_text(encoding="utf-8")
old = 'RECORDING_UI = ROOT / "src/OddSnap/Capture/RecordingForm.Recording.cs"'
new = 'RECORDING_UI = ROOT / "src/OddSnap/Capture/RecordingForm.cs"'
if old in text:
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    print("Recording toolbar paint target corrected.")
elif new in text:
    print("Recording toolbar paint target already correct.")
else:
    raise RuntimeError("Recording UI path declaration was not found")
