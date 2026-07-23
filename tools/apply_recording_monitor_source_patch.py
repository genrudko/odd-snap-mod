from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def replace_once(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected source fragment not found in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def replace_if_present(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if old not in text:
        return False
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def main() -> None:
    changed = False

    app_settings = ROOT / "src/OddSnap/Models/AppSettings.cs"
    changed |= replace_once(
        app_settings,
        '        new("_record",        "Record",             ToolGlyphs.RecordGlyph, null, 2),\n',
        '        new("_record",        "Record area",        ToolGlyphs.RecordGlyph, null, 2),\n'
        '        new("_recordMonitor", "Record monitor",     ToolGlyphs.FullscreenGlyph, null, 2),\n',
    )
    changed |= replace_once(
        app_settings,
        '        new("_recordMonitor", "Record monitor",     ToolGlyphs.FullscreenGlyph, null, 2),\n',
        '        new("_recordMonitor", "Record monitor",     ToolGlyphs.FullscreenGlyph, null, 2),\n'
        '        new("_recordWindow",  "Record window",      ToolGlyphs.ActiveWindowGlyph, null, 2),\n',
    )

    app_capture = ROOT / "src/OddSnap/App/App.Capture.cs"
    changed |= replace_once(
        app_capture,
        "    private void LaunchGifRecording()\n",
        "    private void LaunchGifRecording(RecordingCaptureTarget? preselectedTarget = null)\n",
    )
    changed |= replace_once(
        app_capture,
        "                    _settingsService!.Settings.ShowCaptureMagnifier);\n                selectionScreenshot = null;\n",
        "                    _settingsService!.Settings.ShowCaptureMagnifier);\n"
        "                if (preselectedTarget is not null)\n"
        "                    form.UsePreselectedTarget(preselectedTarget);\n"
        "                selectionScreenshot = null;\n",
    )
    changed |= replace_once(
        app_capture,
        "                break;\n            default:\n                ResetCapturing();\n",
        "                break;\n"
        "            case \"_recordMonitor\":\n"
        "                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));\n"
        "                break;\n"
        "            default:\n"
        "                ResetCapturing();\n",
    )
    changed |= replace_once(
        app_capture,
        "            case \"_recordMonitor\":\n"
        "                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));\n"
        "                break;\n",
        "            case \"_recordMonitor\":\n"
        "                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));\n"
        "                break;\n"
        "            case \"_recordWindow\":\n"
        "            {\n"
        "                var windowTarget = RecordingWindowTargetSelector.SelectWindowAt(System.Windows.Forms.Cursor.Position);\n"
        "                if (windowTarget is not null)\n"
        "                    LaunchGifRecording(windowTarget);\n"
        "                else\n"
        "                    ResetCapturing();\n"
        "                break;\n"
        "            }\n",
    )

    duplicate_monitor_case = (
        "            case \"_recordWindow\":\n"
        "                var windowTarget = RecordingWindowTargetSelector.SelectWindowAt(System.Windows.Forms.Cursor.Position);\n"
        "                if (windowTarget is not null)\n"
        "                    LaunchGifRecording(windowTarget);\n"
        "                else\n"
        "                    ResetCapturing();\n"
        "                break;\n"
        "            case \"_recordMonitor\":\n"
        "                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));\n"
        "                break;\n"
    )
    corrected_window_case = (
        "            case \"_recordWindow\":\n"
        "            {\n"
        "                var windowTarget = RecordingWindowTargetSelector.SelectWindowAt(System.Windows.Forms.Cursor.Position);\n"
        "                if (windowTarget is not null)\n"
        "                    LaunchGifRecording(windowTarget);\n"
        "                else\n"
        "                    ResetCapturing();\n"
        "                break;\n"
        "            }\n"
    )
    changed |= replace_if_present(app_capture, duplicate_monitor_case, corrected_window_case)

    recording_lifecycle = ROOT / "src/OddSnap/Capture/RecordingForm.Recording.cs"
    changed |= replace_once(
        recording_lifecycle,
        "            _selection.Width, _selection.Height);\n\n        if (_format == Models.RecordingFormat.GIF)\n",
        "            _selection.Width, _selection.Height);\n\n"
        "        EnsureRegionCaptureTarget(screenRegion);\n\n"
        "        if (_format == Models.RecordingFormat.GIF)\n",
    )

    window_selector = ROOT / "src/OddSnap/Capture/RecordingWindowTargetSelector.cs"
    changed |= replace_once(
        window_selector,
        "            nint.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);\n",
        "            IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);\n",
    )

    if changed:
        print("Recording target source patch applied.")
    else:
        print("Recording target source patch already applied.")


if __name__ == "__main__":
    main()
