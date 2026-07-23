using System.Drawing;
using System.Runtime.InteropServices;

namespace OddSnap.Capture;

public sealed partial class RecordingForm
{
    private Rectangle _trackedWindowScreenBounds;
    private bool _trackedWindowChromeVisible = true;
    private System.Windows.Forms.Timer? _windowChromeTrackingTimer;

    private void StartWindowChromeTracking()
    {
        if (_captureTarget is null ||
            _captureTarget.Kind != RecordingCaptureTargetKind.Window ||
            _captureTarget.WindowHandle == nint.Zero)
        {
            return;
        }

        UpdateTrackedWindowChrome();

        if (_windowChromeTrackingTimer is null)
        {
            _windowChromeTrackingTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _windowChromeTrackingTimer.Tick += (_, _) =>
            {
                if (_state != State.Recording)
                {
                    _windowChromeTrackingTimer?.Stop();
                    return;
                }

                UpdateTrackedWindowChrome();
            };

            Disposed += (_, _) =>
            {
                _windowChromeTrackingTimer?.Stop();
                _windowChromeTrackingTimer?.Dispose();
                _windowChromeTrackingTimer = null;
            };
        }

        _windowChromeTrackingTimer.Start();
    }

    private void UpdateTrackedWindowChrome()
    {
        if (_captureTarget is null ||
            _captureTarget.Kind != RecordingCaptureTargetKind.Window ||
            _captureTarget.WindowHandle == nint.Zero ||
            _state != State.Recording)
        {
            return;
        }

        if (!TryGetTrackedWindowBounds(_captureTarget.WindowHandle, out var windowBounds))
        {
            SetTrackedWindowChromeVisible(false);
            return;
        }

        bool boundsChanged = windowBounds != _trackedWindowScreenBounds;
        _trackedWindowScreenBounds = windowBounds;

        if (boundsChanged)
        {
            _recordRegion = new Rectangle(
                windowBounds.X - _virtualBounds.X,
                windowBounds.Y - _virtualBounds.Y,
                windowBounds.Width,
                windowBounds.Height);

            CalcToolbarLayout();
            _recordingBorderForm?.SetRecordingScreenBounds(windowBounds);

            var toolbarBounds = GetRecordingToolbarScreenBounds();
            if (_recordingToolbarForm is not null && !toolbarBounds.IsEmpty)
                _recordingToolbarForm.Bounds = toolbarBounds;
        }

        SetTrackedWindowChromeVisible(true);

        if (boundsChanged)
        {
            _recordingBorderForm?.UpdateSurface();
            _recordingToolbarForm?.UpdateSurface();
        }
    }

    private void SetTrackedWindowChromeVisible(bool visible)
    {
        if (_trackedWindowChromeVisible == visible)
            return;

        _trackedWindowChromeVisible = visible;

        if (_recordingBorderForm is not null && !_recordingBorderForm.IsDisposed)
            _recordingBorderForm.Visible = visible;

        if (_recordingToolbarForm is not null && !_recordingToolbarForm.IsDisposed)
            _recordingToolbarForm.Visible = visible;
    }

    private static bool TryGetTrackedWindowBounds(nint windowHandle, out Rectangle bounds)
    {
        bounds = Rectangle.Empty;

        if (!NativeWindowTracking.IsWindow(windowHandle) ||
            !NativeWindowTracking.IsWindowVisible(windowHandle) ||
            NativeWindowTracking.IsIconic(windowHandle))
        {
            return false;
        }

        if (NativeWindowTracking.DwmGetWindowAttributeUInt(
                windowHandle,
                NativeWindowTracking.DwmwaCloaked,
                out uint cloaked,
                sizeof(uint)) == 0 &&
            cloaked != 0)
        {
            return false;
        }

        NativeWindowTracking.Rect rect;
        if (NativeWindowTracking.DwmGetWindowAttributeRect(
                windowHandle,
                NativeWindowTracking.DwmwaExtendedFrameBounds,
                out rect,
                Marshal.SizeOf<NativeWindowTracking.Rect>()) != 0 &&
            !NativeWindowTracking.GetWindowRect(windowHandle, out rect))
        {
            return false;
        }

        bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static class NativeWindowTracking
    {
        internal const uint DwmwaExtendedFrameBounds = 9;
        internal const uint DwmwaCloaked = 14;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(nint windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint windowHandle, out Rect rect);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        internal static extern int DwmGetWindowAttributeRect(
            nint windowHandle,
            uint attribute,
            out Rect value,
            int valueSize);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        internal static extern int DwmGetWindowAttributeUInt(
            nint windowHandle,
            uint attribute,
            out uint value,
            int valueSize);
    }
}
