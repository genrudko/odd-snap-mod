from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap.Tests/RecordingSafetyAndAudioMixTests.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
old = 'Assert.Contains("-map 0:v -map "[a]"", args);'
new = 'Assert.Contains("-map 0:v -map \\"[a]\\"", args);'
count = text.count(old)
if count:
    path.write_text(text.replace(old, new), encoding="utf-8")
    print(f"Escaped {count} audio map assertion(s).")
elif new in text:
    print("Audio map assertions already escaped.")
else:
    raise RuntimeError("Audio map assertions were not found")
