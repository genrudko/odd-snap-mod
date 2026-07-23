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


def keep_single_occurrence(path: Path, fragment: str) -> bool:
    text = path.read_text(encoding="utf-8")
    first = text.find(fragment)
    if first < 0:
        return False

    changed = False
    search_from = first + len(fragment)
    while True:
        duplicate = text.find(fragment, search_from)
        if duplicate < 0:
            break
        text = text[:duplicate] + text[duplicate + len(fragment):]
        changed = True
        search_from = first + len(fragment)

    if changed:
        path.write_text(text, encoding="utf-8")
    return changed


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

    monitor_case = (
        "            case \"_recordMonitor\":\n"
        "                LaunchGifRecording(RecordingCaptureTargetSelector.GetMonitorTargetAt(System.Windows.Forms.Cursor.Position));\n"
        "                break;\n"
    )
    window_case = (
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
    unbraced_window_case = (
        "            case \"_recordWindow\":\n"
        "                var windowTarget = RecordingWindowTargetSelector.SelectWindowAt(System.Windows.Forms.Cursor.Position);\n"
        "                if (windowTarget is not null)\n"
        "                    LaunchGifRecording(windowTarget);\n"
        "                else\n"
        "                    ResetCapturing();\n"
        "                break;\n"
    )

    changed |= replace_if_present(app_capture, unbraced_window_case, window_case)

    capture_text = app_capture.read_text(encoding="utf-8")
    if monitor_case not in capture_text:
        insertion_point = (
            "            default:\n"
            "                ResetCapturing();\n"
            "                break;\n"
        )
        if insertion_point not in capture_text:
            raise RuntimeError("Could not locate toolbar action switch default block.")
        capture_text = capture_text.replace(
            insertion_point,
            monitor_case + window_case + insertion_point,
            1,
        )
        app_capture.write_text(capture_text, encoding="utf-8")
        changed = True
    elif window_case not in capture_text:
        capture_text = capture_text.replace(monitor_case, monitor_case + window_case, 1)
        app_capture.write_text(capture_text, encoding="utf-8")
        changed = True

    changed |= keep_single_occurrence(app_capture, monitor_case)
    changed |= keep_single_occurrence(app_capture, window_case)

    recording_lifecycle = ROOT / "src/OddSnap/Capture/RecordingForm.Recording.cs"
    changed |= replace_once(
        recording_lifecycle,
        "            _selection.Width, _selection.Height);\n\n        if (_format == Models.RecordingFormat.GIF)\n",
        "            _selection.Width, _selection.Height);\n\n"
        "        EnsureRegionCaptureTarget(screenRegion);\n\n"
        "        if (_format == Models.RecordingFormat.GIF)\n",
    )
    changed |= replace_once(
        recording_lifecycle,
        "            _recorder = new GifRecorder(screenRegion, _fps, _maxDuration, _showCursor);\n",
        "            _recorder = new GifRecorder(screenRegion, _fps, _maxDuration, _showCursor, _captureTarget);\n",
    )
    changed |= replace_once(
        recording_lifecycle,
        "            _videoRecorder = new VideoRecorder(screenRegion, vfmt, _fps, _maxDuration, _maxHeight,\n"
        "                _showCursor, _recordMic, _micDeviceId, _recordDesktop, _desktopDeviceId);\n",
        "            _videoRecorder = new VideoRecorder(screenRegion, vfmt, _fps, _maxDuration, _maxHeight,\n"
        "                _showCursor, _recordMic, _micDeviceId, _recordDesktop, _desktopDeviceId, _captureTarget);\n",
    )

    screen_capture = ROOT / "src/OddSnap/Capture/ScreenCapture.cs"
    changed |= replace_once(
        screen_capture,
        "    internal sealed class RecordingFrameCapturer : IDisposable\n",
        "    internal sealed class RecordingFrameCapturer : IRecordingFrameSource\n",
    )

    video_recorder = ROOT / "src/OddSnap/Capture/VideoRecorder.cs"
    changed |= replace_once(
        video_recorder,
        "    private readonly string? _desktopDeviceId;\n"
        "    private readonly CancellationTokenSource _cts = new();\n",
        "    private readonly string? _desktopDeviceId;\n"
        "    private readonly RecordingCaptureTarget? _captureTarget;\n"
        "    private readonly CancellationTokenSource _cts = new();\n",
    )
    changed |= replace_once(
        video_recorder,
        "                         bool recordMic = false, string? micDeviceId = null,\n"
        "                         bool recordDesktop = false, string? desktopDeviceId = null)\n",
        "                         bool recordMic = false, string? micDeviceId = null,\n"
        "                         bool recordDesktop = false, string? desktopDeviceId = null,\n"
        "                         RecordingCaptureTarget? captureTarget = null)\n",
    )
    changed |= replace_once(
        video_recorder,
        "        _desktopDeviceId = desktopDeviceId;\n"
        "    }\n",
        "        _desktopDeviceId = desktopDeviceId;\n"
        "        _captureTarget = captureTarget;\n"
        "    }\n",
    )
    changed |= replace_once(
        video_recorder,
        "        using var frameCapturer = ScreenCapture.CreateRecordingFrameCapturer(_region, _showCursor);\n",
        "        using var frameCapturer = RecordingFrameSourceFactory.Create(_region, _showCursor, _captureTarget);\n",
    )
    changed |= replace_once(
        video_recorder,
        "    private void CapturePreviewFrame(ScreenCapture.RecordingFrameCapturer frameCapturer)\n",
        "    private void CapturePreviewFrame(IRecordingFrameSource frameCapturer)\n",
    )

    gif_recorder = ROOT / "src/OddSnap/Capture/GifRecorder.cs"
    changed |= replace_once(
        gif_recorder,
        "    private readonly bool _showCursor;\n"
        "    private readonly string _tempDir;\n",
        "    private readonly bool _showCursor;\n"
        "    private readonly RecordingCaptureTarget? _captureTarget;\n"
        "    private readonly string _tempDir;\n",
    )
    changed |= replace_once(
        gif_recorder,
        "    public GifRecorder(Rectangle region, int fps = 15, int maxDurationSeconds = 30, bool showCursor = false)\n",
        "    public GifRecorder(Rectangle region, int fps = 15, int maxDurationSeconds = 30, bool showCursor = false,\n"
        "        RecordingCaptureTarget? captureTarget = null)\n",
    )
    changed |= replace_once(
        gif_recorder,
        "        _showCursor = showCursor;\n"
        "        _tempDir = Path.Combine(Path.GetTempPath(), $\"oddsnap_gif_{Guid.NewGuid():N}\");\n",
        "        _showCursor = showCursor;\n"
        "        _captureTarget = captureTarget;\n"
        "        _tempDir = Path.Combine(Path.GetTempPath(), $\"oddsnap_gif_{Guid.NewGuid():N}\");\n",
    )
    changed |= replace_once(
        gif_recorder,
        "            using var frameCapturer = ScreenCapture.CreateRecordingFrameCapturer(_region, _showCursor);\n",
        "            using var frameCapturer = RecordingFrameSourceFactory.Create(_region, _showCursor, _captureTarget);\n",
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
