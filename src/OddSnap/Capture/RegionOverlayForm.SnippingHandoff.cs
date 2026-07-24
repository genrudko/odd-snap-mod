using System.Drawing;

namespace OddSnap.Capture;

public sealed partial class RegionOverlayForm
{
    private bool _snippingRecordingHandoffStarted;

    /// <summary>
    /// Ends the launcher overlay before the recording form is created. This method is
    /// intentionally invoked from the toolbar mouse handler, so it first removes every
    /// visible selection surface and only then posts Close() back to the overlay loop.
    /// The application starts recording from FormClosed, after the old topmost HWNDs
    /// have been destroyed.
    /// </summary>
    private void BeginSnippingRecordingHandoff(Rectangle selection)
    {
        if (_snippingRecordingHandoffStarted ||
            _snippingLauncherMode != SnippingLauncherMode.Recording ||
            selection.Width <= 2 || selection.Height <= 2)
        {
            return;
        }

        _snippingRecordingHandoffStarted = true;
        _allowDeactivation = true;

        // Remove the white selection frame and all auxiliary launcher windows before
        // notifying the application. The user must never see the old overlay while the
        // recording form is being initialized.
        _hasSelection = false;
        _hasDragged = false;
        _isSelecting = false;
        _selectionRect = Rectangle.Empty;
        _lastSelectionRect = Rectangle.Empty;
        _autoDetectRect = Rectangle.Empty;
        _autoDetectActive = false;
        CloseSelectionAdorner();
        CloseCaptureMagnifier();
        CloseMagWindow();
        ClearCrosshairGuides();
        _toolbarForm?.Hide();
        Hide();

        RecordingRegionSelected?.Invoke(selection);

        if (IsDisposed || Disposing)
            return;

        try
        {
            if (IsHandleCreated)
                BeginInvoke(new Action(Close));
            else
                Close();
        }
        catch (InvalidOperationException)
        {
            if (!IsDisposed && !Disposing)
                Close();
        }
    }

    internal void TriggerSnippingRecordingHandoffForTests(Rectangle selection)
        => BeginSnippingRecordingHandoff(selection);
}
