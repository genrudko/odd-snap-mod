using System.Drawing;

namespace OddSnap.Capture;

/// <summary>
/// Describes what the recording workflow is expected to capture.
/// Region and monitor targets are currently backed by fixed desktop bounds.
/// Window targets additionally preserve the native HWND so the capture source
/// can later move from coordinate-based recording to Windows Graphics Capture
/// without changing the selection UI contract again.
/// </summary>
public sealed record RecordingCaptureTarget
{
    private RecordingCaptureTarget(
        RecordingCaptureTargetKind kind,
        Rectangle bounds,
        nint windowHandle,
        string? displayName)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Capture bounds must have a positive width and height.");

        if (kind == RecordingCaptureTargetKind.Window && windowHandle == nint.Zero)
            throw new ArgumentException("A window recording target requires a non-zero window handle.", nameof(windowHandle));

        Kind = kind;
        Bounds = bounds;
        WindowHandle = windowHandle;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }

    public RecordingCaptureTargetKind Kind { get; }

    /// <summary>
    /// Physical desktop coordinates for the selected target at selection time.
    /// For a window target these bounds also provide the safe coordinate-based
    /// fallback used before native HWND capture is enabled.
    /// </summary>
    public Rectangle Bounds { get; }

    /// <summary>
    /// Native HWND for a window target; zero for region and monitor targets.
    /// </summary>
    public nint WindowHandle { get; }

    /// <summary>
    /// Optional user-facing target name, such as a monitor device name or
    /// the selected window title.
    /// </summary>
    public string? DisplayName { get; }

    public static RecordingCaptureTarget ForRegion(Rectangle bounds) =>
        new(RecordingCaptureTargetKind.Region, bounds, nint.Zero, null);

    public static RecordingCaptureTarget ForMonitor(Rectangle bounds, string? displayName = null) =>
        new(RecordingCaptureTargetKind.Monitor, bounds, nint.Zero, displayName);

    public static RecordingCaptureTarget ForWindow(nint windowHandle, Rectangle bounds, string? displayName = null) =>
        new(RecordingCaptureTargetKind.Window, bounds, windowHandle, displayName);
}

public enum RecordingCaptureTargetKind
{
    Region,
    Monitor,
    Window,
}
