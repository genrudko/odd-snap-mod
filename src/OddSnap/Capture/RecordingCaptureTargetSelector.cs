using System.Drawing;
using System.Windows.Forms;

namespace OddSnap.Capture;

/// <summary>
/// Resolves concrete recording targets from the current Windows desktop layout.
/// The selector intentionally keeps monitor discovery separate from the recording
/// engine so the overlay UI can stay small and the existing recorder can continue
/// consuming fixed desktop bounds.
/// </summary>
public static class RecordingCaptureTargetSelector
{
    /// <summary>
    /// Returns all physical monitors in a stable visual order: top-to-bottom,
    /// then left-to-right. Bounds are physical virtual-desktop coordinates.
    /// </summary>
    public static IReadOnlyList<RecordingCaptureTarget> GetMonitorTargets()
    {
        return Screen.AllScreens
            .OrderBy(screen => screen.Bounds.Top)
            .ThenBy(screen => screen.Bounds.Left)
            .Select((screen, index) => RecordingCaptureTarget.ForMonitor(
                screen.Bounds,
                BuildMonitorDisplayName(screen, index + 1)))
            .ToArray();
    }

    /// <summary>
    /// Resolves the monitor containing the supplied physical screen point.
    /// Windows chooses the nearest monitor when the point lies outside every
    /// monitor, matching <see cref="Screen.FromPoint(Point)"/> semantics.
    /// </summary>
    public static RecordingCaptureTarget GetMonitorTargetAt(Point screenPoint)
    {
        var screen = Screen.FromPoint(screenPoint);
        var orderedScreens = Screen.AllScreens
            .OrderBy(candidate => candidate.Bounds.Top)
            .ThenBy(candidate => candidate.Bounds.Left)
            .ToArray();

        int ordinal = Array.FindIndex(
            orderedScreens,
            candidate => string.Equals(candidate.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase));

        return RecordingCaptureTarget.ForMonitor(
            screen.Bounds,
            BuildMonitorDisplayName(screen, ordinal >= 0 ? ordinal + 1 : 1));
    }

    private static string BuildMonitorDisplayName(Screen screen, int ordinal)
    {
        string resolution = $"{screen.Bounds.Width} x {screen.Bounds.Height}";
        string primary = screen.Primary ? " · Primary" : string.Empty;
        return $"Monitor {ordinal} · {resolution}{primary}";
    }
}
