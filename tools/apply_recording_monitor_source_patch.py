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


def write_text_if_changed(path: Path, content: str) -> bool:
    normalized = content.replace("\r\n", "\n")
    if path.exists() and path.read_text(encoding="utf-8").replace("\r\n", "\n") == normalized:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(normalized, encoding="utf-8")
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

    pause_clock = ROOT / "src/OddSnap/Capture/RecordingPauseClock.cs"
    changed |= write_text_if_changed(
        pause_clock,
        """using System.Diagnostics;

namespace OddSnap.Capture;

/// <summary>
/// Tracks active recording time while excluding every paused interval. The same
/// clock is shared by GIF and video recorders so frame scheduling, toolbar time,
/// maximum duration, and audio trimming all use one pause-aware timeline.
/// </summary>
internal sealed class RecordingPauseClock
{
    private readonly object _sync = new();
    private long _startTimestamp;
    private long _pauseStartedTimestamp;
    private long _accumulatedPausedTicks;
    private bool _isPaused;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
                return _isPaused;
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_sync)
            {
                if (_startTimestamp == 0)
                    return TimeSpan.Zero;

                long now = Stopwatch.GetTimestamp();
                long pausedTicks = _accumulatedPausedTicks;
                if (_isPaused && _pauseStartedTimestamp != 0)
                    pausedTicks += Math.Max(0, now - _pauseStartedTimestamp);

                long activeTicks = Math.Max(0, now - _startTimestamp - pausedTicks);
                return TimeSpan.FromSeconds(activeTicks / (double)Stopwatch.Frequency);
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _pauseStartedTimestamp = 0;
            _accumulatedPausedTicks = 0;
            _isPaused = false;
            Monitor.PulseAll(_sync);
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_startTimestamp == 0 || _isPaused)
                return;

            _pauseStartedTimestamp = Stopwatch.GetTimestamp();
            _isPaused = true;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (!_isPaused)
            {
                Monitor.PulseAll(_sync);
                return;
            }

            long now = Stopwatch.GetTimestamp();
            if (_pauseStartedTimestamp != 0)
                _accumulatedPausedTicks += Math.Max(0, now - _pauseStartedTimestamp);

            _pauseStartedTimestamp = 0;
            _isPaused = false;
            Monitor.PulseAll(_sync);
        }
    }

    public void WaitWhilePaused(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            while (_isPaused && !cancellationToken.IsCancellationRequested)
                Monitor.Wait(_sync, 100);
        }
    }
}
""",
    )

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
    changed |= replace_once(
        recording_lifecycle,
        "        int tw = UiChrome.ScaleInt(320), th = WindowsDockRenderer.SurfaceHeight;\n"
        "        _toolbarRect = GetSmartRecordingToolbarRect(\n"
        "            _recordRegion,\n"
        "            new Rectangle(0, 0, Width, Height),\n",
        "        int tw = UiChrome.ScaleInt(360), th = WindowsDockRenderer.SurfaceHeight;\n"
        "        _toolbarRect = GetSmartRecordingToolbarRect(\n"
        "            _recordRegion,\n"
        "            GetRecordingToolbarPlacementBounds(),\n",
    )
    changed |= replace_once(
        recording_lifecycle,
        "    internal static Rectangle GetSmartRecordingToolbarRect(\n",
        """    private Rectangle GetRecordingToolbarPlacementBounds()
    {
        var fullVirtualClientBounds = new Rectangle(0, 0, Width, Height);
        if (_captureTarget is null)
            return fullVirtualClientBounds;

        Rectangle placementScreenBounds;
        switch (_captureTarget.Kind)
        {
            case RecordingCaptureTargetKind.Monitor:
                placementScreenBounds = _captureTarget.Bounds;
                break;

            case RecordingCaptureTargetKind.Window:
            {
                var currentWindowBounds = !_trackedWindowScreenBounds.IsEmpty
                    ? _trackedWindowScreenBounds
                    : _captureTarget.Bounds;
                try
                {
                    placementScreenBounds = Screen.FromRectangle(currentWindowBounds).WorkingArea;
                }
                catch
                {
                    placementScreenBounds = currentWindowBounds;
                }
                break;
            }

            default:
                return fullVirtualClientBounds;
        }

        var clipped = Rectangle.Intersect(placementScreenBounds, _virtualBounds);
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return fullVirtualClientBounds;

        return new Rectangle(
            clipped.X - _virtualBounds.X,
            clipped.Y - _virtualBounds.Y,
            clipped.Width,
            clipped.Height);
    }

    internal static Rectangle GetSmartRecordingToolbarRect(
""",
    )

    recording_form = ROOT / "src/OddSnap/Capture/RecordingForm.cs"
    changed |= replace_once(
        recording_form,
        """        var elapsed = _recorder?.Elapsed ?? _videoRecorder?.Elapsed ?? TimeSpan.Zero;

        float dotX = bounds.X + 16;
        float dotY = bounds.Y + bounds.Height / 2f - 5;
        bool dotVisible = (int)(elapsed.TotalMilliseconds / 500) % 2 == 0;
        if (dotVisible)
            g.FillEllipse(_dotBrush, dotX, dotY, 10, 10);
        g.DrawEllipse(_ringPen, dotX, dotY, 10, 10);

        string time = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        var stopButton = GetRecordingToolbarStopButton(bounds);
        var discardButton = GetRecordingToolbarDiscardButton(bounds);
        var timeRect = new RectangleF(dotX + 18, bounds.Y, stopButton.X - (dotX + 24), bounds.Height);
        using (var timeFormat = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(time, _timeFont, _timeBrush, timeRect, timeFormat);

        DrawIconBtn(g, stopButton, "stopSquare", hoveredButton == 0,
            UiChrome.SurfaceTextPrimary, active: false);
        DrawIconBtn(g, discardButton, "close", hoveredButton == 1,
            UiChrome.SurfaceTextPrimary, active: false);
""",
        """        var elapsed = _recorder?.Elapsed ?? _videoRecorder?.Elapsed ?? TimeSpan.Zero;
        bool paused = IsRecordingPaused;

        float dotX = bounds.X + 16;
        float dotY = bounds.Y + bounds.Height / 2f - 5;
        bool dotVisible = paused || (int)(elapsed.TotalMilliseconds / 500) % 2 == 0;
        if (dotVisible)
            g.FillEllipse(_dotBrush, dotX, dotY, 10, 10);
        g.DrawEllipse(_ringPen, dotX, dotY, 10, 10);

        string time = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        var pauseButton = GetRecordingToolbarPauseButton(bounds);
        var stopButton = GetRecordingToolbarStopButton(bounds);
        var discardButton = GetRecordingToolbarDiscardButton(bounds);
        var timeRect = new RectangleF(dotX + 18, bounds.Y, pauseButton.X - (dotX + 24), bounds.Height);
        using (var timeFormat = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(time, _timeFont, _timeBrush, timeRect, timeFormat);

        DrawIconBtn(g, pauseButton, paused ? "_recordResume" : "_recordPause", hoveredButton == 0,
            UiChrome.SurfaceTextPrimary, active: paused);
        DrawIconBtn(g, stopButton, "stopSquare", hoveredButton == 1,
            UiChrome.SurfaceTextPrimary, active: false);
        DrawIconBtn(g, discardButton, "close", hoveredButton == 2,
            UiChrome.SurfaceTextPrimary, active: false);
""",
    )
    changed |= replace_once(
        recording_form,
        """    internal static Rectangle GetRecordingToolbarStopButton(Rectangle toolbarBounds)
    {
        var discardButton = GetRecordingToolbarDiscardButton(toolbarBounds);
        return new Rectangle(discardButton.X - WindowsDockRenderer.ButtonSpacing - WindowsDockRenderer.IconButtonSize,
            discardButton.Y, WindowsDockRenderer.IconButtonSize, WindowsDockRenderer.IconButtonSize);
    }

    internal void RequestToolbarStop() => StopRecording();

    internal void RequestToolbarDiscard() => DiscardRecording();
""",
        """    internal static Rectangle GetRecordingToolbarStopButton(Rectangle toolbarBounds)
    {
        var discardButton = GetRecordingToolbarDiscardButton(toolbarBounds);
        return new Rectangle(discardButton.X - WindowsDockRenderer.ButtonSpacing - WindowsDockRenderer.IconButtonSize,
            discardButton.Y, WindowsDockRenderer.IconButtonSize, WindowsDockRenderer.IconButtonSize);
    }

    internal static Rectangle GetRecordingToolbarPauseButton(Rectangle toolbarBounds)
    {
        var stopButton = GetRecordingToolbarStopButton(toolbarBounds);
        return new Rectangle(stopButton.X - WindowsDockRenderer.ButtonSpacing - WindowsDockRenderer.IconButtonSize,
            stopButton.Y, WindowsDockRenderer.IconButtonSize, WindowsDockRenderer.IconButtonSize);
    }

    internal bool IsRecordingPaused =>
        _recorder?.IsPaused == true || _videoRecorder?.IsPaused == true;

    internal void RequestToolbarTogglePause()
    {
        if (_state != State.Recording)
            return;

        if (IsRecordingPaused)
        {
            _recorder?.Resume();
            _videoRecorder?.Resume();
        }
        else
        {
            _recorder?.Pause();
            _videoRecorder?.Pause();
        }

        _recordingToolbarForm?.UpdateSurface();
    }

    internal void RequestToolbarStop() => StopRecording();

    internal void RequestToolbarDiscard() => DiscardRecording();
""",
    )

    recording_toolbar = ROOT / "src/OddSnap/Capture/RecordingToolbarForm.cs"
    changed |= replace_once(
        recording_toolbar,
        """        int previous = _hoveredButton;
        _hoveredButton = RecordingForm.GetRecordingToolbarStopButton(ClientRectangle).Contains(e.Location) ? 0
            : RecordingForm.GetRecordingToolbarDiscardButton(ClientRectangle).Contains(e.Location) ? 1
            : -1;
""",
        """        int previous = _hoveredButton;
        _hoveredButton = RecordingForm.GetRecordingToolbarPauseButton(ClientRectangle).Contains(e.Location) ? 0
            : RecordingForm.GetRecordingToolbarStopButton(ClientRectangle).Contains(e.Location) ? 1
            : RecordingForm.GetRecordingToolbarDiscardButton(ClientRectangle).Contains(e.Location) ? 2
            : -1;
""",
    )
    changed |= replace_once(
        recording_toolbar,
        """        if (RecordingForm.GetRecordingToolbarStopButton(ClientRectangle).Contains(e.Location))
            _owner.RequestToolbarStop();
        else if (RecordingForm.GetRecordingToolbarDiscardButton(ClientRectangle).Contains(e.Location))
            _owner.RequestToolbarDiscard();
""",
        """        if (RecordingForm.GetRecordingToolbarPauseButton(ClientRectangle).Contains(e.Location))
            _owner.RequestToolbarTogglePause();
        else if (RecordingForm.GetRecordingToolbarStopButton(ClientRectangle).Contains(e.Location))
            _owner.RequestToolbarStop();
        else if (RecordingForm.GetRecordingToolbarDiscardButton(ClientRectangle).Contains(e.Location))
            _owner.RequestToolbarDiscard();
""",
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
        """    private DateTime _startTime;
    private TimeSpan _recordedDuration = TimeSpan.Zero;
    private bool _isPaused;
    private bool _disposed;
    private readonly object _pauseLock = new();
""",
        """    private readonly RecordingPauseClock _pauseClock = new();
    private TimeSpan _recordedDuration = TimeSpan.Zero;
    private bool _disposed;
""",
    )
    changed |= replace_once(
        video_recorder,
        """    public TimeSpan Elapsed => DateTime.UtcNow - _startTime;
    public bool IsRecording => _captureThread?.IsAlive == true;
    public bool IsPaused => _isPaused;
""",
        """    public TimeSpan Elapsed => _pauseClock.Elapsed;
    public bool IsRecording => _captureThread?.IsAlive == true;
    public bool IsPaused => _pauseClock.IsPaused;
""",
    )
    changed |= replace_once(
        video_recorder,
        """        _initialCaptureDelayMs = Math.Max(0, initialCaptureDelayMs);
        _startTime = DateTime.UtcNow;
""",
        """        _initialCaptureDelayMs = Math.Max(0, initialCaptureDelayMs);
        _recordedDuration = TimeSpan.Zero;
""",
    )
    changed |= replace_if_present(
        video_recorder,
        "                try { writer?.Write(e.Buffer, 0, e.BytesRecorded); } catch { }\n",
        "                try\n"
        "                {\n"
        "                    if (!_pauseClock.IsPaused)\n"
        "                        writer?.Write(e.Buffer, 0, e.BytesRecorded);\n"
        "                }\n"
        "                catch { }\n",
    )
    changed |= replace_if_present(
        video_recorder,
        "                try { writer?.Write(e.Buffer, 0, e.BytesRecorded); } catch { }\n",
        "                try\n"
        "                {\n"
        "                    if (!_pauseClock.IsPaused)\n"
        "                        writer?.Write(e.Buffer, 0, e.BytesRecorded);\n"
        "                }\n"
        "                catch { }\n",
    )
    changed |= replace_once(
        video_recorder,
        """    public void Pause()
    {
        lock (_pauseLock) _isPaused = true;
    }

    public void Resume()
    {
        lock (_pauseLock)
        {
            _isPaused = false;
            Monitor.PulseAll(_pauseLock);
        }
    }
""",
        """    public void Pause() => _pauseClock.Pause();

    public void Resume() => _pauseClock.Resume();
""",
    )
    changed |= replace_once(
        video_recorder,
        """    private void CaptureLoop()
    {
        var ct = _cts.Token;
        using var frameCapturer = RecordingFrameSourceFactory.Create(_region, _showCursor, _captureTarget);
        byte[]? captureBuffer = null;
        byte[]? lastFrameBuffer = null;
        int lastFrameByteCount = 0;
        double frameIntervalTicks = (double)Stopwatch.Frequency / _fps;

        if (_initialCaptureDelayMs > 0)
        {
            try { Thread.Sleep(_initialCaptureDelayMs); }
            catch (ThreadInterruptedException) { return; }
        }

        long activeStartTicks = Stopwatch.GetTimestamp();
        while (!ct.IsCancellationRequested)
        {
            var activeElapsed = Stopwatch.GetElapsedTime(activeStartTicks);
            if (activeElapsed.TotalMilliseconds >= _maxDurationMs)
                break;

            // Pause support
            lock (_pauseLock)
            {
                while (_isPaused && !ct.IsCancellationRequested)
                    Monitor.Wait(_pauseLock, 100);
            }
            if (ct.IsCancellationRequested) break;

            WaitForNextFrameSlot(activeStartTicks, frameIntervalTicks, ct);
            if (ct.IsCancellationRequested)
                break;

            bool capturedFrame = false;
            try
            {
                captureBuffer = frameCapturer.CaptureToBuffer(captureBuffer);
                int byteCount = captureBuffer.Length;
                if (lastFrameBuffer == null || lastFrameBuffer.Length != byteCount)
                    lastFrameBuffer = new byte[byteCount];

                WriteFrame(captureBuffer, byteCount);
                Buffer.BlockCopy(captureBuffer, 0, lastFrameBuffer, 0, byteCount);
                lastFrameByteCount = byteCount;
                capturedFrame = true;

                CapturePreviewFrame(frameCapturer);
                Interlocked.Increment(ref _capturedFrameCount);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                Interlocked.Increment(ref _droppedFrameCount);
            }

            if (!capturedFrame && lastFrameBuffer == null)
                continue;

            int targetFrameCount = GetExpectedFrameCount(Stopwatch.GetElapsedTime(activeStartTicks), _fps);
            DuplicateLastFrameUntil(lastFrameBuffer, lastFrameByteCount, targetFrameCount);
        }

        _recordedDuration = Stopwatch.GetElapsedTime(activeStartTicks);
        if (lastFrameBuffer != null && lastFrameByteCount > 0)
        {
            int targetFrameCount = GetExpectedFrameCount(_recordedDuration, _fps);
            DuplicateLastFrameUntil(lastFrameBuffer, lastFrameByteCount, targetFrameCount);
        }
    }

    private void WaitForNextFrameSlot(long activeStartTicks, double frameIntervalTicks, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            long nextDueTicks = activeStartTicks + (long)Math.Round(_frameCount * frameIntervalTicks);
            long nowTicks = Stopwatch.GetTimestamp();
            long remainingTicks = nextDueTicks - nowTicks;
            if (remainingTicks <= 0)
                break;

            int sleepMs = (int)Math.Min(20, remainingTicks * 1000 / Stopwatch.Frequency);
            if (sleepMs <= 1)
            {
                Thread.Yield();
                continue;
            }

            try { Thread.Sleep(sleepMs); }
            catch (ThreadInterruptedException) { break; }
        }
    }
""",
        """    private void CaptureLoop()
    {
        var ct = _cts.Token;
        using var frameCapturer = RecordingFrameSourceFactory.Create(_region, _showCursor, _captureTarget);
        byte[]? captureBuffer = null;
        byte[]? lastFrameBuffer = null;
        int lastFrameByteCount = 0;

        if (_initialCaptureDelayMs > 0)
        {
            try { Thread.Sleep(_initialCaptureDelayMs); }
            catch (ThreadInterruptedException) { return; }
        }

        _pauseClock.Start();
        while (!ct.IsCancellationRequested)
        {
            _pauseClock.WaitWhilePaused(ct);
            if (ct.IsCancellationRequested)
                break;

            var activeElapsed = _pauseClock.Elapsed;
            if (activeElapsed.TotalMilliseconds >= _maxDurationMs)
                break;

            WaitForNextFrameSlot(ct);
            if (ct.IsCancellationRequested)
                break;

            bool capturedFrame = false;
            try
            {
                captureBuffer = frameCapturer.CaptureToBuffer(captureBuffer);
                int byteCount = captureBuffer.Length;
                if (lastFrameBuffer == null || lastFrameBuffer.Length != byteCount)
                    lastFrameBuffer = new byte[byteCount];

                WriteFrame(captureBuffer, byteCount);
                Buffer.BlockCopy(captureBuffer, 0, lastFrameBuffer, 0, byteCount);
                lastFrameByteCount = byteCount;
                capturedFrame = true;

                CapturePreviewFrame(frameCapturer);
                Interlocked.Increment(ref _capturedFrameCount);
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                Interlocked.Increment(ref _droppedFrameCount);
            }

            if (!capturedFrame && lastFrameBuffer == null)
                continue;

            int targetFrameCount = GetExpectedFrameCount(_pauseClock.Elapsed, _fps);
            DuplicateLastFrameUntil(lastFrameBuffer, lastFrameByteCount, targetFrameCount);
        }

        _recordedDuration = _pauseClock.Elapsed;
        if (lastFrameBuffer != null && lastFrameByteCount > 0)
        {
            int targetFrameCount = GetExpectedFrameCount(_recordedDuration, _fps);
            DuplicateLastFrameUntil(lastFrameBuffer, lastFrameByteCount, targetFrameCount);
        }
    }

    private void WaitForNextFrameSlot(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _pauseClock.WaitWhilePaused(ct);
            if (ct.IsCancellationRequested)
                break;

            double nextDueSeconds = _frameCount / (double)_fps;
            double remainingMs = (nextDueSeconds - _pauseClock.Elapsed.TotalSeconds) * 1000d;
            if (remainingMs <= 0d)
                break;

            int sleepMs = (int)Math.Min(20d, Math.Ceiling(remainingMs));
            if (sleepMs <= 1)
            {
                Thread.Yield();
                continue;
            }

            try { Thread.Sleep(sleepMs); }
            catch (ThreadInterruptedException) { break; }
        }
    }
""",
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
    for _ in range(3):
        changed |= replace_if_present(
            video_recorder,
            "        lock (_pauseLock) { _isPaused = false; Monitor.PulseAll(_pauseLock); }\n",
            "        _pauseClock.Resume();\n",
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
        """    private DateTime _startTime;
    private bool _disposed;
""",
        """    private readonly RecordingPauseClock _pauseClock = new();
    private bool _disposed;
""",
    )
    changed |= replace_once(
        gif_recorder,
        """    public int FrameCount => _frameCount;
    public TimeSpan Elapsed => DateTime.UtcNow - _startTime;
    public bool IsRecording => _captureThread?.IsAlive == true;
""",
        """    public int FrameCount => _frameCount;
    public TimeSpan Elapsed => _pauseClock.Elapsed;
    public bool IsRecording => _captureThread?.IsAlive == true;
    public bool IsPaused => _pauseClock.IsPaused;
""",
    )
    changed |= replace_once(
        gif_recorder,
        """        _initialCaptureDelayMs = Math.Max(0, initialCaptureDelayMs);
        _startTime = DateTime.UtcNow;
""",
        """        _initialCaptureDelayMs = Math.Max(0, initialCaptureDelayMs);
""",
    )
    changed |= replace_once(
        gif_recorder,
        "            using var frameCapturer = ScreenCapture.CreateRecordingFrameCapturer(_region, _showCursor);\n",
        "            using var frameCapturer = RecordingFrameSourceFactory.Create(_region, _showCursor, _captureTarget);\n",
    )
    changed |= replace_once(
        gif_recorder,
        """            while (!ct.IsCancellationRequested)
            {
                // Auto-stop at max duration
                if ((DateTime.UtcNow - _startTime).TotalMilliseconds >= _maxDurationMs)
                    break;

                var sw = Stopwatch.StartNew();
""",
        """            _pauseClock.Start();
            while (!ct.IsCancellationRequested)
            {
                _pauseClock.WaitWhilePaused(ct);
                if (ct.IsCancellationRequested)
                    break;

                // Auto-stop at max active duration. Paused time is excluded.
                if (_pauseClock.Elapsed.TotalMilliseconds >= _maxDurationMs)
                    break;

                var sw = Stopwatch.StartNew();
""",
    )
    changed |= replace_once(
        gif_recorder,
        """    /// <summary>Stops recording and encodes frames to GIF. Uses FFmpeg if available (10-50x faster).</summary>
    public string StopAndEncode(string outputPath)
""",
        """    public void Pause() => _pauseClock.Pause();

    public void Resume() => _pauseClock.Resume();

    /// <summary>Stops recording and encodes frames to GIF. Uses FFmpeg if available (10-50x faster).</summary>
    public string StopAndEncode(string outputPath)
""",
    )
    for marker in (
        "    public string StopAndEncode(string outputPath)\n    {\n        _cts.Cancel();\n",
        "    public void Discard()\n    {\n        _cts.Cancel();\n",
        "    public void Dispose()\n    {\n        if (_disposed) return;\n        _disposed = true;\n        _cts.Cancel();\n",
    ):
        if marker in gif_recorder.read_text(encoding="utf-8"):
            replacement = marker + "        _pauseClock.Resume();\n"
            changed |= replace_once(gif_recorder, marker, replacement)

    icon_data = ROOT / "src/OddSnap/Helpers/RecordingTargetIconData.cs"
    icon_text = icon_data.read_text(encoding="utf-8")
    if '["_recordPause"]' not in icon_text:
        icon_marker = "        };\n\n    internal static bool TryGetIcon"
        if icon_marker not in icon_text:
            raise RuntimeError("Could not locate the recording icon dictionary terminator.")
        icon_entries = """            [\"_recordPause\"] = (
                \"M6 4.5A1.5 1.5 0 0 1 7.5 3h1A1.5 1.5 0 0 1 10 4.5v11A1.5 1.5 0 0 1 8.5 17h-1A1.5 1.5 0 0 1 6 15.5v-11Zm1.5-.5a.5.5 0 0 0-.5.5v11c0 .28.22.5.5.5h1a.5.5 0 0 0 .5-.5v-11a.5.5 0 0 0-.5-.5h-1Zm4.5.5A1.5 1.5 0 0 1 13.5 3h1A1.5 1.5 0 0 1 16 4.5v11a1.5 1.5 0 0 1-1.5 1.5h-1a1.5 1.5 0 0 1-1.5-1.5v-11Zm1.5-.5a.5.5 0 0 0-.5.5v11c0 .28.22.5.5.5h1a.5.5 0 0 0 .5-.5v-11a.5.5 0 0 0-.5-.5h-1Z\",
                \"M6 4.5A1.5 1.5 0 0 1 7.5 3h1A1.5 1.5 0 0 1 10 4.5v11A1.5 1.5 0 0 1 8.5 17h-1A1.5 1.5 0 0 1 6 15.5v-11Zm6 0A1.5 1.5 0 0 1 13.5 3h1A1.5 1.5 0 0 1 16 4.5v11a1.5 1.5 0 0 1-1.5 1.5h-1a1.5 1.5 0 0 1-1.5-1.5v-11Z\"),
            [\"_recordResume\"] = (
                \"M7.43 3.3A1.5 1.5 0 0 0 5 4.47v11.06a1.5 1.5 0 0 0 2.43 1.18l7.37-5.53a1.48 1.48 0 0 0 0-2.36L7.43 3.3Zm-.82.81c.12-.08.28-.08.4.01l7.37 5.53c.2.15.2.45 0 .6l-7.37 5.53a.5.5 0 0 1-.8-.4V4.48c0-.16.08-.3.2-.37Z\",
                \"M5 4.47a1.5 1.5 0 0 1 2.43-1.18l7.37 5.53a1.48 1.48 0 0 1 0 2.36l-7.37 5.53A1.5 1.5 0 0 1 5 15.53V4.47Z\")
"""
        insert_at = icon_text.index(icon_marker)
        prefix = icon_text[:insert_at]
        suffix = icon_text[insert_at:]
        last_non_whitespace = len(prefix.rstrip()) - 1
        if last_non_whitespace < 0 or prefix[last_non_whitespace] != ")":
            raise RuntimeError("Could not locate the final recording icon entry.")
        prefix = prefix[:last_non_whitespace + 1] + "," + prefix[last_non_whitespace + 1:]
        icon_text = prefix + icon_entries + suffix
        icon_data.write_text(icon_text, encoding="utf-8")
        changed = True

    window_selector = ROOT / "src/OddSnap/Capture/RecordingWindowTargetSelector.cs"
    changed |= replace_once(
        window_selector,
        "            nint.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);\n",
        "            IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);\n",
    )

    if changed:
        print("Recording target, native window capture, toolbar placement, and pause patch applied.")
    else:
        print("Recording target, native window capture, toolbar placement, and pause patch already applied.")


if __name__ == "__main__":
    main()
