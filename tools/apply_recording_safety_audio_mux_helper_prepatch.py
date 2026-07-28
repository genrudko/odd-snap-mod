from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/VideoRecorder.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

if "private static bool TryPromoteMuxedOutput" in text:
    print("Audio mux fallback helper already present.")
else:
    anchor = "    private static void StopCaptureAndWait(IWaveIn? capture, int timeoutMs = 5_000)\n"
    helper = """    private static bool TryPromoteMuxedOutput(
        string videoPath,
        string tempOut,
        ProcessCaptureResult result)
    {
        if (result.TimedOut || result.ExitCode != 0 || !HasNonEmptyFile(tempOut))
            return false;

        File.Delete(videoPath);
        File.Move(tempOut, videoPath);
        return true;
    }

"""
    if anchor not in text:
        raise RuntimeError("StopCaptureAndWait declaration was not found")
    path.write_text(text.replace(anchor, helper + anchor, 1), encoding="utf-8")
    print("Audio mux fallback helper inserted before StopCaptureAndWait.")
