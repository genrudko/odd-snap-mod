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

        _preselectedTargetStartQueued = true;

        // The target is already final, so the selection adorner and crosshair must
        // never be shown for this form. The OnShown lifecycle starts recording.
        _selectionAdorner?.Dispose();
        _selectionAdorner = null;
        Cursor = Cursors.Default;
    }

    private void StartPreselectedTargetNow()
    {
        if (!_preselectedTargetStartQueued ||
            _state != State.Selecting ||
            _captureTarget is null)
        {
            return;
        }

        _preselectedTargetStartQueued = false;
        StartRecording();
        StartWindowChromeTracking();
    }

    internal bool IsRecordingActiveForTests =>
        _state == State.Recording && (_recorder is not null || _videoRecorder is not null);

    internal bool IsRecordingChromeVisibleForTests =>
        _recordingBorderForm?.Visible == true && _recordingToolbarForm?.Visible == true;

    private void EnsureRegionCaptureTarget(Rectangle screenRegion)
    {
        _captureTarget ??= RecordingCaptureTarget.ForRegion(screenRegion);
    }
}
