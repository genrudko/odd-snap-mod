from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RECORDING_FORM = ROOT / "src/OddSnap/Capture/RecordingForm.cs"
RECORDING_UI = ROOT / "src/OddSnap/Capture/RecordingForm.Recording.cs"
TOOLBAR = ROOT / "src/OddSnap/Capture/RecordingToolbarForm.cs"
VIDEO_RECORDER = ROOT / "src/OddSnap/Capture/VideoRecorder.cs"
TESTS = ROOT / "src/OddSnap.Tests/RecordingSafetyAndAudioMixTests.cs"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8").replace("\r\n", "\n")


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8")


def replace_once(path: Path, old: str, new: str, sentinel: str | None = None) -> bool:
    text = read(path)
    if sentinel and sentinel in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected fragment not found in {path}: {old!r}")
    write(path, text.replace(old, new, 1))
    return True


def insert_after(path: Path, anchor: str, addition: str, sentinel: str) -> bool:
    text = read(path)
    if sentinel in text:
        return False
    if anchor not in text:
        raise RuntimeError(f"Anchor not found in {path}: {anchor!r}")
    write(path, text.replace(anchor, anchor + addition, 1))
    return True


def write_if_changed(path: Path, content: str) -> bool:
    normalized = content.replace("\r\n", "\n")
    if path.exists() and read(path) == normalized:
        return False
    write(path, normalized)
    return True


def main() -> None:
    changed = False

    # Escape during an active recording is a safe stop-and-save action.
    changed |= replace_once(
        RECORDING_FORM,
        """        if (_state == State.Recording)
        {
            DiscardRecording();
            return;
        }
""",
        """        if (_state == State.Recording)
        {
            StopRecording();
            return;
        }
""",
        """        if (_state == State.Recording)
        {
            StopRecording();
            return;
        }
""",
    )

    # Make the destructive toolbar action a two-step, mouse-up-confirmed operation.
    changed |= insert_after(
        TOOLBAR,
        "    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(850);",
        "\n    private static readonly TimeSpan DiscardConfirmationWindow = TimeSpan.FromSeconds(2.5);",
        "DiscardConfirmationWindow",
    )
    changed |= insert_after(
        TOOLBAR,
        "    private bool _dragging;",
        "\n    private int _pressedButton = -1;"
        "\n    private DateTime _discardArmedUntilUtc = DateTime.MinValue;",
        "_discardArmedUntilUtc",
    )
    changed |= replace_once(
        TOOLBAR,
        "        _owner.PaintRecordingToolbarTo(g, new Rectangle(Point.Empty, sz), _hoveredButton);",
        "        _owner.PaintRecordingToolbarTo(g, new Rectangle(Point.Empty, sz), _hoveredButton, IsDiscardArmed);",
        "PaintRecordingToolbarTo(g, new Rectangle(Point.Empty, sz), _hoveredButton, IsDiscardArmed)",
    )

    old_mouse_down = """    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        WakeSurface();
        int button = GetButtonAt(e.Location);
        if (button == 0)
        {
            _owner.RequestToolbarTogglePause();
            return;
        }
        if (button == 1)
        {
            _owner.RequestToolbarStop();
            return;
        }
        if (button == 2)
        {
            _owner.RequestToolbarDiscard();
            return;
        }

        _dragging = true;
        _dragOffset = e.Location;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }
"""
    new_mouse_down = """    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        WakeSurface();
        int button = GetButtonAt(e.Location);
        if (button >= 0)
        {
            if (button != 2)
                DisarmDiscardConfirmation();

            _pressedButton = button;
            _hoveredButton = button;
            Capture = true;
            UpdateSurface();
            return;
        }

        DisarmDiscardConfirmation();
        _dragging = true;
        _dragOffset = e.Location;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }
"""
    changed |= replace_once(TOOLBAR, old_mouse_down, new_mouse_down, "_pressedButton = button;")

    old_mouse_up = """    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_dragging)
            return;

        MoveByPointer();
        _dragging = false;
        Capture = false;
        _pointerInside = ClientRectangle.Contains(PointToClient(MousePosition));
        Cursor = _pointerInside ? Cursors.SizeAll : Cursors.Default;
        WakeSurface();
    }
"""
    new_mouse_up = """    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (_pressedButton >= 0)
        {
            int pressedButton = _pressedButton;
            _pressedButton = -1;
            Capture = false;

            if (GetButtonAt(e.Location) == pressedButton)
                ExecuteToolbarButton(pressedButton);
            else
                UpdateSurface();

            return;
        }

        if (!_dragging)
            return;

        MoveByPointer();
        _dragging = false;
        Capture = false;
        _pointerInside = ClientRectangle.Contains(PointToClient(MousePosition));
        Cursor = _pointerInside ? Cursors.SizeAll : Cursors.Default;
        WakeSurface();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture || _pressedButton < 0)
            return;

        _pressedButton = -1;
        UpdateSurface();
    }

    private void ExecuteToolbarButton(int button)
    {
        switch (button)
        {
            case 0:
                DisarmDiscardConfirmation();
                _owner.RequestToolbarTogglePause();
                break;

            case 1:
                DisarmDiscardConfirmation();
                _owner.RequestToolbarStop();
                break;

            case 2:
            {
                var nowUtc = DateTime.UtcNow;
                if (ResolveDiscardClick(nowUtc, _discardArmedUntilUtc) == DiscardClickDecision.Discard)
                {
                    _discardArmedUntilUtc = DateTime.MinValue;
                    _owner.RequestToolbarDiscard();
                }
                else
                {
                    _discardArmedUntilUtc = nowUtc + DiscardConfirmationWindow;
                    _lastInteractionUtc = nowUtc;
                    UpdateSurface();
                }
                break;
            }
        }
    }

    private void DisarmDiscardConfirmation()
    {
        if (_discardArmedUntilUtc == DateTime.MinValue)
            return;

        _discardArmedUntilUtc = DateTime.MinValue;
        UpdateSurface();
    }

    internal enum DiscardClickDecision
    {
        Arm,
        Discard
    }

    internal static DiscardClickDecision ResolveDiscardClick(DateTime nowUtc, DateTime armedUntilUtc)
        => nowUtc <= armedUntilUtc ? DiscardClickDecision.Discard : DiscardClickDecision.Arm;

    private bool IsDiscardArmed => DateTime.UtcNow <= _discardArmedUntilUtc;
"""
    changed |= replace_once(TOOLBAR, old_mouse_up, new_mouse_up, "DiscardClickDecision ResolveDiscardClick")

    changed |= replace_once(
        TOOLBAR,
        """        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            _owner.RequestToolbarDiscard();
            return true;
        }
""",
        """        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            _owner.RequestToolbarStop();
            return true;
        }
""",
        "_owner.RequestToolbarStop();\n            return true;",
    )

    changed |= replace_once(
        TOOLBAR,
        """    private void UpdateIdleOpacity()
    {
        if (IsDisposed || !IsHandleCreated || !Visible)
            return;

        byte target = _pointerInside || _dragging || DateTime.UtcNow - _lastInteractionUtc < IdleDelay
""",
        """    private void UpdateIdleOpacity()
    {
        if (IsDisposed || !IsHandleCreated || !Visible)
            return;

        var nowUtc = DateTime.UtcNow;
        if (_discardArmedUntilUtc != DateTime.MinValue && nowUtc > _discardArmedUntilUtc)
        {
            _discardArmedUntilUtc = DateTime.MinValue;
            UpdateSurface();
        }

        byte target = _pointerInside || _dragging || nowUtc - _lastInteractionUtc < IdleDelay
""",
        "nowUtc > _discardArmedUntilUtc",
    )

    # The toolbar visibly explains the second destructive click.
    changed |= replace_once(
        RECORDING_UI,
        "    internal void PaintRecordingToolbarTo(Graphics g, Rectangle bounds, int hoveredButton)",
        "    internal void PaintRecordingToolbarTo(Graphics g, Rectangle bounds, int hoveredButton, bool discardArmed)",
        "bool discardArmed)",
    )
    changed |= replace_once(
        RECORDING_UI,
        """        string time = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        var pauseButton = GetRecordingToolbarPauseButton(bounds);
        var stopButton = GetRecordingToolbarStopButton(bounds);
        var discardButton = GetRecordingToolbarDiscardButton(bounds);
        var timeRect = new RectangleF(dotX + 18, bounds.Y, pauseButton.X - (dotX + 24), bounds.Height);
        using (var timeFormat = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(time, _timeFont, _timeBrush, timeRect, timeFormat);
""",
        """        string time = discardArmed
            ? "Click × again to discard"
            : $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
        var pauseButton = GetRecordingToolbarPauseButton(bounds);
        var stopButton = GetRecordingToolbarStopButton(bounds);
        var discardButton = GetRecordingToolbarDiscardButton(bounds);
        var timeRect = new RectangleF(dotX + 18, bounds.Y, pauseButton.X - (dotX + 24), bounds.Height);
        using (var timeFormat = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            g.DrawString(time, discardArmed ? _hintFont : _timeFont, _timeBrush, timeRect, timeFormat);
""",
        '"Click × again to discard"',
    )
    changed |= replace_once(
        RECORDING_UI,
        """        DrawIconBtn(g, discardButton, "close", hoveredButton == 2,
            UiChrome.SurfaceTextPrimary, active: false);
""",
        """        DrawIconBtn(g, discardButton, "close", hoveredButton == 2,
            discardArmed ? Color.FromArgb(255, 239, 68, 68) : UiChrome.SurfaceTextPrimary,
            active: discardArmed);
""",
        "discardArmed ? Color.FromArgb(255, 239, 68, 68)",
    )

    # Explicitly normalize the two audio sources before amix.
    changed |= replace_once(
        VIDEO_RECORDER,
        """        return $"-y -i \"{videoPath}\" -i \"{audioFiles[0]}\" -i \"{audioFiles[1]}\" " +
               $"-filter_complex \"[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0,apad,atrim=0:{duration}[a]\" " +
               $"-c:v copy -c:a {audioCodec} -map 0:v -map \"[a]\"{muxerArgs} \"{tempOut}\"";
""",
        """        const string normalizeAudio = "aresample=48000:async=1:first_pts=0,aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo";
        return $"-y -i \"{videoPath}\" -i \"{audioFiles[0]}\" -i \"{audioFiles[1]}\" " +
               $"-filter_complex \"[1:a]{normalizeAudio},volume=0.75[desktop];" +
               $"[2:a]{normalizeAudio},volume=0.75[mic];" +
               $"[desktop][mic]amix=inputs=2:duration=longest:dropout_transition=0:normalize=0," +
               $"alimiter=limit=0.95,apad,atrim=0:{duration}[a]\" " +
               $"-c:v copy -c:a {audioCodec} -map 0:v -map \"[a]\"{muxerArgs} \"{tempOut}\"";
""",
        "const string normalizeAudio",
    )

    old_mux = """            string args = BuildMuxArguments(videoPath, audioFiles, tempOut, audioCodec, targetDurationSeconds);
            var result = RunProcessWithLimitedOutput(ffmpegPath, args, timeoutMs: 30_000);

            if (!result.TimedOut && result.ExitCode == 0 && HasNonEmptyFile(tempOut))
            {
                File.Delete(videoPath);
                File.Move(tempOut, videoPath);
                return true;
            }
            else
            {
                // Mux failed — keep the original video without audio
                AppDiagnostics.LogWarning(
                    "recording.mux-audio",
                    result.TimedOut
                        ? $"Audio mux timed out for {Path.GetFileName(videoPath)}."
                        : $"Audio mux failed for {Path.GetFileName(videoPath)}. FFmpeg exit={result.ExitCode}. {result.StdErr}");
                TryDeleteRecordingTempFile(tempOut, "failed mux output");
            }
"""
    new_mux = """            string args = BuildMuxArguments(videoPath, audioFiles, tempOut, audioCodec, targetDurationSeconds);
            var result = RunProcessWithLimitedOutput(ffmpegPath, args, timeoutMs: 30_000);

            if (TryPromoteMuxedOutput(videoPath, tempOut, result))
                return true;

            AppDiagnostics.LogWarning(
                "recording.mux-audio",
                result.TimedOut
                    ? $"Audio mux timed out for {Path.GetFileName(videoPath)}."
                    : $"Audio mux failed for {Path.GetFileName(videoPath)}. FFmpeg exit={result.ExitCode}. {result.StdErr}");
            TryDeleteRecordingTempFile(tempOut, "failed mux output");

            // A failed two-source mix must never silently remove every audio track.
            // Retry each captured source independently and keep the first valid result.
            if (audioFiles.Count > 1)
            {
                foreach (var fallbackAudio in audioFiles)
                {
                    string fallbackArgs = BuildMuxArguments(
                        videoPath,
                        new[] { fallbackAudio },
                        tempOut,
                        audioCodec,
                        targetDurationSeconds);
                    var fallbackResult = RunProcessWithLimitedOutput(ffmpegPath, fallbackArgs, timeoutMs: 30_000);
                    if (TryPromoteMuxedOutput(videoPath, tempOut, fallbackResult))
                    {
                        AppDiagnostics.LogWarning(
                            "recording.mux-audio-fallback",
                            $"Mixed audio failed; preserved {Path.GetFileName(fallbackAudio)} as the recording audio track.");
                        return true;
                    }

                    TryDeleteRecordingTempFile(tempOut, "failed fallback mux output");
                }
            }
"""
    changed |= replace_once(VIDEO_RECORDER, old_mux, new_mux, "recording.mux-audio-fallback")

    changed |= insert_after(
        VIDEO_RECORDER,
        """    private static void StopCaptureAndWait(IWaveIn? capture, int timeoutMs = 5_000)
""",
        """    private static bool TryPromoteMuxedOutput(
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

""",
        "private static bool TryPromoteMuxedOutput",
    )

    changed |= write_if_changed(
        TESTS,
        """using OddSnap.Capture;
using Xunit;

namespace OddSnap.Tests;

public sealed class RecordingSafetyAndAudioMixTests
{
    [Fact]
    public void DiscardRequiresASecondClickInsideTheConfirmationWindow()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Arm,
            RecordingToolbarForm.ResolveDiscardClick(now, DateTime.MinValue));
        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Discard,
            RecordingToolbarForm.ResolveDiscardClick(now, now.AddSeconds(1)));
        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Arm,
            RecordingToolbarForm.ResolveDiscardClick(now, now.AddMilliseconds(-1)));
    }

    [Fact]
    public void DualAudioMuxNormalizesBothInputsBeforeMixing()
    {
        string args = VideoRecorder.BuildMuxArguments(
            "video.mp4",
            new[] { "desktop.wav", "mic.wav" },
            "muxed.mp4",
            "aac",
            12.5);

        Assert.Contains("[1:a]aresample=48000:async=1:first_pts=0", args);
        Assert.Contains("[2:a]aresample=48000:async=1:first_pts=0", args);
        Assert.Contains("sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo", args);
        Assert.Contains("[desktop][mic]amix=inputs=2", args);
        Assert.Contains("normalize=0", args);
        Assert.Contains("alimiter=limit=0.95", args);
        Assert.Contains("-map 0:v -map \"[a]\"", args);
    }

    [Fact]
    public void SingleAudioMuxStillUsesTheDirectSafePath()
    {
        string args = VideoRecorder.BuildMuxArguments(
            "video.mp4",
            new[] { "desktop.wav" },
            "muxed.mp4",
            "aac",
            8);

        Assert.DoesNotContain("amix=", args);
        Assert.Contains("[1:a]apad,atrim=0:8[a]", args);
        Assert.Contains("-map 0:v -map \"[a]\"", args);
    }
}
""",
    )

    print("Recording safety and dual-audio repair applied." if changed else "Recording safety and dual-audio repair already applied.")


if __name__ == "__main__":
    main()
