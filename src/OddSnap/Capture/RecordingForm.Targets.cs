using System.Drawing;

namespace OddSnap.Capture;

public sealed partial class RecordingForm
{
    private RecordingCaptureTarget? _captureTarget;
    private bool _preselectedTargetStartQueued;

    /// <summary>
    /// Gets the target selected for the current recording session. Region selection
    /// populates this value when recording starts; monitor/window workflows can set
    /// it up front through <see cref="UsePreselectedTarget"/>.
    /// </summary>
    public RecordingCaptureTarget? CaptureTarget => _captureTarget;

    /// <summary>
    /// Configures the form to start recording a target that was selected before the
    /// recording overlay opened. The existing region-selection path remains unchanged.
    /// </summary>
    public void UsePreselectedTarget(RecordingCaptureTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_state != State.Selecting)
            throw new InvalidOperationException("A capture target can only be assigned before recording starts.");

        var clippedBounds = Rectangle.Intersect(target.Bounds, _virtualBounds);
        if (clippedBounds.Width <= 10 || clippedBounds.Height <= 10)
            throw new ArgumentOutOfRangeException(nameof(target), "The capture target must overlap the virtual desktop by more than 10 pixels.");

        _captureTarget = target.Kind switch
        {
            RecordingCaptureTargetKind.Region => RecordingCaptureTarget.ForRegion(clippedBounds),
            RecordingCaptureTargetKind.Monitor => RecordingCaptureTarget.ForMonitor(clippedBounds, target.DisplayName),
            RecordingCaptureTargetKind.Window => RecordingCaptureTarget.ForWindow(target.WindowHandle, clippedBounds, target.DisplayName),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target.Kind, "Unknown recording capture target kind.")
        };

        _selection = new Rectangle(
            clippedBounds.X - _virtualBounds.X,
            clippedBounds.Y - _virtualBounds.Y,
            clippedBounds.Width,
            clippedBounds.Height);
        _selectionCursor = new Point(_selection.Right, _selection.Bottom);

        if (_preselectedTargetStartQueued)
            return;

        _preselectedTargetStartQueued = true;
        Shown += StartPreselectedTargetAfterShown;
    }

    private void StartPreselectedTargetAfterShown(object? sender, EventArgs e)
    {
        Shown -= StartPreselectedTargetAfterShown;

        if (_state != State.Selecting || _captureTarget is null)
            return;

        BeginInvoke(new Action(() =>
        {
            StartRecording();
            StartWindowChromeTracking();
        }));
    }

    private void EnsureRegionCaptureTarget(Rectangle screenRegion)
    {
        _captureTarget ??= RecordingCaptureTarget.ForRegion(screenRegion);
    }
}
