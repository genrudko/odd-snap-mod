from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/RecordingToolbarForm.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

sentinel = "internal static byte ResolveIdleAlpha"
if sentinel in text:
    print("Recording toolbar opacity resolver already patched.")
else:
    marker = "    private void UpdateIdleOpacity()"
    if marker not in text:
        raise RuntimeError("RecordingToolbarForm UpdateIdleOpacity marker was not found.")

    method = """    internal static byte ResolveIdleAlpha(bool fadeWhenIdle, int opacityPercent)
    {
        if (!fadeWhenIdle)
            return ActiveAlpha;

        int clampedPercent = Math.Clamp(opacityPercent, 20, 100);
        return (byte)Math.Round(
            ActiveAlpha * (clampedPercent / 100d),
            MidpointRounding.AwayFromZero);
    }

"""
    text = text.replace(marker, method + marker, 1)
    path.write_text(text, encoding="utf-8")
    print("Recording toolbar opacity resolver patched.")
