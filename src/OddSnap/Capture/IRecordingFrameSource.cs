using System.Drawing;
using OddSnap.Services;

namespace OddSnap.Capture;

/// <summary>
/// Produces fixed-size BGRA frames for the existing GIF and FFmpeg encoders.
/// Desktop-region and native-window capture sources share this contract so the
/// recording lifecycle and output pipeline do not need target-specific branches.
/// </summary>
internal interface IRecordingFrameSource : IDisposable
{
    int BufferByteCount { get; }

    byte[] CaptureToBuffer(byte[]? buffer);

    Bitmap CaptureBitmap();

    Bitmap CloneCurrentFrame();
}

internal static class RecordingFrameSourceFactory
{
    public static IRecordingFrameSource Create(
        Rectangle region,
        bool includeCursor,
        RecordingCaptureTarget? target)
    {
        if (target is not null &&
            target.Kind == RecordingCaptureTargetKind.Window &&
            target.WindowHandle != nint.Zero &&
            WindowsGraphicsCaptureFrameSource.IsSupported)
        {
            try
            {
                return new WindowsGraphicsCaptureFrameSource(
                    target.WindowHandle,
                    new Size(region.Width, region.Height),
                    includeCursor);
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogWarning(
                    "recording.native-window-capture",
                    $"Native window capture could not start; using fixed desktop bounds instead. {ex.Message}",
                    ex);
            }
        }

        return ScreenCapture.CreateRecordingFrameCapturer(region, includeCursor);
    }
}
