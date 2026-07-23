using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OddSnap.Capture;

/// <summary>
/// Resolves concrete recording targets from the current Windows desktop layout.
/// Monitor selection is performed in physical desktop pixels so mixed-DPI monitor
/// layouts use the same coordinate space as the recording engine.
/// </summary>
public static class RecordingCaptureTargetSelector
{
    public static IReadOnlyList<RecordingCaptureTarget> GetMonitorTargets()
    {
        using var dpiScope = DpiAwarenessScope.EnterPerMonitorV2();
        return GetOrderedMonitors()
            .Select((monitor, index) => RecordingCaptureTarget.ForMonitor(
                monitor.Bounds,
                BuildMonitorDisplayName(monitor, index + 1)))
            .ToArray();
    }

    /// <summary>
    /// Opens an interactive monitor picker, initially highlighting the monitor that
    /// contains <paramref name="screenPoint"/>. Moving the pointer changes the target;
    /// a left click confirms it and only then does the recording workflow continue.
    /// </summary>
    public static RecordingCaptureTarget GetMonitorTargetAt(Point screenPoint)
    {
        RecordingCaptureTarget? selectedTarget = null;
        Exception? pickerError = null;

        void ShowPicker()
        {
            try
            {
                using var dpiScope = DpiAwarenessScope.EnterPerMonitorV2();
                using var picker = new MonitorRecordingPickerForm(screenPoint);
                picker.ShowDialog();
                selectedTarget = picker.SelectedTarget;
            }
            catch (Exception ex)
            {
                pickerError = ex;
            }
        }

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            ShowPicker();
        }
        else
        {
            var pickerThread = new Thread(ShowPicker)
            {
                IsBackground = true,
                Name = "OddSnap monitor recording picker"
            };
            pickerThread.SetApartmentState(ApartmentState.STA);
            pickerThread.Start();
            pickerThread.Join();
        }

        if (pickerError is not null)
            throw new InvalidOperationException("OddSnap could not open the monitor recording picker.", pickerError);

        if (selectedTarget is not null)
            return selectedTarget;

        using var fallbackDpiScope = DpiAwarenessScope.EnterPerMonitorV2();
        return CreateTargetForMonitor(FindMonitorAt(screenPoint, GetOrderedMonitors()));
    }

    private static MonitorDescriptor[] GetOrderedMonitors()
    {
        var monitors = new List<MonitorDescriptor>();

        NativeMethods.EnumDisplayMonitors(
            nint.Zero,
            nint.Zero,
            (monitorHandle, _, _, _) =>
            {
                var info = new NativeMethods.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };

                if (!NativeMethods.GetMonitorInfo(monitorHandle, ref info))
                    return true;

                var bounds = Rectangle.FromLTRB(
                    info.rcMonitor.Left,
                    info.rcMonitor.Top,
                    info.rcMonitor.Right,
                    info.rcMonitor.Bottom);

                monitors.Add(new MonitorDescriptor(
                    monitorHandle,
                    bounds,
                    info.szDevice,
                    (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));

                return true;
            },
            nint.Zero);

        if (monitors.Count == 0)
        {
            monitors.AddRange(Screen.AllScreens.Select(screen => new MonitorDescriptor(
                nint.Zero,
                screen.Bounds,
                screen.DeviceName,
                screen.Primary)));
        }

        return monitors
            .OrderBy(monitor => monitor.Bounds.Top)
            .ThenBy(monitor => monitor.Bounds.Left)
            .ToArray();
    }

    private static MonitorDescriptor FindMonitorAt(Point point, IReadOnlyList<MonitorDescriptor> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (monitor.Bounds.Contains(point))
                return monitor;
        }

        return monitors
            .OrderBy(monitor => DistanceSquaredToRectangle(point, monitor.Bounds))
            .First();
    }

    private static long DistanceSquaredToRectangle(Point point, Rectangle bounds)
    {
        int closestX = Math.Clamp(point.X, bounds.Left, bounds.Right - 1);
        int closestY = Math.Clamp(point.Y, bounds.Top, bounds.Bottom - 1);
        long dx = point.X - closestX;
        long dy = point.Y - closestY;
        return dx * dx + dy * dy;
    }

    private static RecordingCaptureTarget CreateTargetForMonitor(MonitorDescriptor monitor)
    {
        var orderedMonitors = GetOrderedMonitors();
        int ordinal = Array.FindIndex(
            orderedMonitors,
            candidate => string.Equals(candidate.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));

        return RecordingCaptureTarget.ForMonitor(
            monitor.Bounds,
            BuildMonitorDisplayName(monitor, ordinal >= 0 ? ordinal + 1 : 1));
    }

    private static string BuildMonitorDisplayName(MonitorDescriptor monitor, int ordinal)
    {
        string resolution = $"{monitor.Bounds.Width} x {monitor.Bounds.Height}";
        string primary = monitor.Primary ? " · Primary" : string.Empty;
        return $"Monitor {ordinal} · {resolution}{primary}";
    }

    private sealed record MonitorDescriptor(
        nint Handle,
        Rectangle Bounds,
        string DeviceName,
        bool Primary);

    private sealed class MonitorRecordingPickerForm : Form
    {
        private readonly Rectangle _virtualBounds;
        private readonly MonitorDescriptor[] _monitors;
        private MonitorDescriptor _hoveredMonitor;

        public MonitorRecordingPickerForm(Point initialScreenPoint)
        {
            _monitors = GetOrderedMonitors();
            _virtualBounds = Rectangle.Union(_monitors[0].Bounds, _monitors[0].Bounds);
            foreach (var monitor in _monitors.Skip(1))
                _virtualBounds = Rectangle.Union(_virtualBounds, monitor.Bounds);

            _hoveredMonitor = FindMonitorAt(initialScreenPoint, _monitors);

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.Black;
            Opacity = 0.58;
            Cursor = Cursors.Hand;

            SetBounds(
                _virtualBounds.X,
                _virtualBounds.Y,
                _virtualBounds.Width,
                _virtualBounds.Height,
                BoundsSpecified.All);

            MouseMove += HandleMouseMove;
            MouseDown += HandleMouseDown;
            KeyDown += HandleKeyDown;
            Shown += (_, _) =>
            {
                Activate();
                Focus();
                Invalidate();
            };
        }

        public RecordingCaptureTarget? SelectedTarget { get; private set; }

        protected override bool ShowWithoutActivation => false;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            foreach (var monitor in _monitors)
            {
                var localBounds = ToLocal(monitor.Bounds);
                bool active = string.Equals(
                    monitor.DeviceName,
                    _hoveredMonitor.DeviceName,
                    StringComparison.OrdinalIgnoreCase);

                using var fill = new SolidBrush(active
                    ? Color.FromArgb(72, 38, 126, 255)
                    : Color.FromArgb(24, 255, 255, 255));
                e.Graphics.FillRectangle(fill, localBounds);

                using var border = new Pen(
                    active ? Color.FromArgb(255, 90, 170, 255) : Color.FromArgb(145, 220, 220, 220),
                    active ? 6f : 2f)
                {
                    Alignment = PenAlignment.Inset,
                    LineJoin = LineJoin.Round
                };
                e.Graphics.DrawRectangle(border, localBounds);

                DrawMonitorLabel(e.Graphics, monitor, localBounds, active);
            }

            DrawInstruction(e.Graphics);
        }

        private void HandleMouseMove(object? sender, MouseEventArgs e)
        {
            var screenPoint = new Point(e.X + _virtualBounds.X, e.Y + _virtualBounds.Y);
            var candidate = FindMonitorAt(screenPoint, _monitors);
            if (string.Equals(candidate.DeviceName, _hoveredMonitor.DeviceName, StringComparison.OrdinalIgnoreCase))
                return;

            _hoveredMonitor = candidate;
            Invalidate();
        }

        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            var screenPoint = new Point(e.X + _virtualBounds.X, e.Y + _virtualBounds.Y);
            _hoveredMonitor = FindMonitorAt(screenPoint, _monitors);
            SelectedTarget = CreateTargetForMonitor(_hoveredMonitor);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                SelectedTarget = CreateTargetForMonitor(_hoveredMonitor);
                DialogResult = DialogResult.OK;
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
            }
        }

        private Rectangle ToLocal(Rectangle monitorBounds) => new(
            monitorBounds.X - _virtualBounds.X,
            monitorBounds.Y - _virtualBounds.Y,
            monitorBounds.Width,
            monitorBounds.Height);

        private void DrawMonitorLabel(Graphics graphics, MonitorDescriptor monitor, Rectangle bounds, bool active)
        {
            int ordinal = Array.FindIndex(
                _monitors,
                candidate => string.Equals(candidate.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)) + 1;

            string title = $"MONITOR {ordinal}";
            string subtitle = $"{monitor.Bounds.Width} × {monitor.Bounds.Height}{(monitor.Primary ? " · PRIMARY" : string.Empty)}";

            float scale = Math.Clamp(Math.Min(bounds.Width / 1920f, bounds.Height / 1080f), 0.75f, 1.5f);
            using var titleFont = new Font("Segoe UI Semibold", 24f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            using var subtitleFont = new Font("Segoe UI", 15f * scale, FontStyle.Regular, GraphicsUnit.Pixel);

            var titleSize = graphics.MeasureString(title, titleFont);
            var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
            float panelWidth = Math.Max(titleSize.Width, subtitleSize.Width) + 42f * scale;
            float panelHeight = titleSize.Height + subtitleSize.Height + 30f * scale;
            float panelX = bounds.Left + (bounds.Width - panelWidth) / 2f;
            float panelY = bounds.Top + (bounds.Height - panelHeight) / 2f;
            var panel = new RectangleF(panelX, panelY, panelWidth, panelHeight);

            using var panelBrush = new SolidBrush(active
                ? Color.FromArgb(225, 18, 25, 36)
                : Color.FromArgb(185, 28, 28, 30));
            using var path = RoundedRectangle(panel, 14f * scale);
            graphics.FillPath(panelBrush, path);

            using var textBrush = new SolidBrush(Color.White);
            graphics.DrawString(title, titleFont, textBrush, panelX + 21f * scale, panelY + 10f * scale);
            graphics.DrawString(subtitle, subtitleFont, textBrush, panelX + 21f * scale, panelY + titleSize.Height + 10f * scale);
        }

        private void DrawInstruction(Graphics graphics)
        {
            const string instruction = "Select a monitor to record · Click to confirm · Esc to cancel";
            using var font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = graphics.MeasureString(instruction, font);
            var panel = new RectangleF(
                (ClientSize.Width - size.Width) / 2f - 22f,
                24f,
                size.Width + 44f,
                size.Height + 20f);

            using var panelBrush = new SolidBrush(Color.FromArgb(230, 18, 25, 36));
            using var textBrush = new SolidBrush(Color.White);
            using var path = RoundedRectangle(panel, 12f);
            graphics.FillPath(panelBrush, path);
            graphics.DrawString(instruction, font, textBrush, panel.X + 22f, panel.Y + 10f);
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private sealed class DpiAwarenessScope : IDisposable
    {
        private readonly nint _previousContext;

        private DpiAwarenessScope(nint previousContext)
        {
            _previousContext = previousContext;
        }

        public static DpiAwarenessScope EnterPerMonitorV2()
        {
            nint previous = NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            return new DpiAwarenessScope(previous);
        }

        public void Dispose()
        {
            if (_previousContext != nint.Zero)
                NativeMethods.SetThreadDpiAwarenessContext(_previousContext);
        }
    }

    private static class NativeMethods
    {
        internal const uint MONITORINFOF_PRIMARY = 0x00000001;
        internal static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

        internal delegate bool MonitorEnumProc(
            nint monitorHandle,
            nint monitorDeviceContext,
            nint monitorRectangle,
            nint userData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(
            nint deviceContext,
            nint clipRectangle,
            MonitorEnumProc callback,
            nint userData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(
            nint monitorHandle,
            ref MONITORINFOEX monitorInfo);

        [DllImport("user32.dll")]
        internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }
    }
}
