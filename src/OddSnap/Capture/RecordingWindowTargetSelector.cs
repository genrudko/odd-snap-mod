using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace OddSnap.Capture;

/// <summary>
/// Selects a visible top-level window for recording. The first implementation keeps
/// the existing rectangle-based recorder and captures the selected window's physical
/// extended-frame bounds at selection time. The HWND is preserved in the target so
/// native Windows Graphics Capture can replace the fixed-bounds backend later.
/// </summary>
public static class RecordingWindowTargetSelector
{
    public static RecordingCaptureTarget? SelectWindowAt(Point initialScreenPoint)
    {
        RecordingCaptureTarget? selectedTarget = null;
        Exception? pickerError = null;

        void ShowPicker()
        {
            try
            {
                using var dpiScope = new PerMonitorDpiScope();
                using var picker = new WindowRecordingPickerForm(initialScreenPoint);
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
                Name = "OddSnap window recording picker"
            };
            pickerThread.SetApartmentState(ApartmentState.STA);
            pickerThread.Start();
            pickerThread.Join();
        }

        if (pickerError is not null)
            throw new InvalidOperationException("OddSnap could not open the window recording picker.", pickerError);

        return selectedTarget;
    }

    private sealed class WindowRecordingPickerForm : Form
    {
        private readonly Rectangle _virtualBounds;
        private readonly IReadOnlyList<WindowCandidate> _windows;
        private WindowCandidate? _hoveredWindow;

        public WindowRecordingPickerForm(Point initialScreenPoint)
        {
            _virtualBounds = NativeMethods.GetPhysicalVirtualScreen();
            _windows = NativeMethods.EnumerateRecordableWindows(_virtualBounds);
            _hoveredWindow = FindCandidate(initialScreenPoint);

            AutoScaleMode = AutoScaleMode.None;
            Bounds = _virtualBounds;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.Black;
            Opacity = 0.48;
            Cursor = Cursors.Hand;

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

            if (_hoveredWindow is not null)
            {
                var localBounds = ToLocal(_hoveredWindow.Bounds);
                using var fill = new SolidBrush(Color.FromArgb(56, 38, 126, 255));
                using var border = new Pen(Color.FromArgb(255, 90, 170, 255), 6f)
                {
                    Alignment = PenAlignment.Inset,
                    LineJoin = LineJoin.Round
                };
                e.Graphics.FillRectangle(fill, localBounds);
                e.Graphics.DrawRectangle(border, localBounds);
                DrawWindowLabel(e.Graphics, _hoveredWindow, localBounds);
            }

            DrawInstruction(e.Graphics, _windows.Count == 0);
        }

        private void HandleMouseMove(object? sender, MouseEventArgs e)
        {
            var candidate = FindCandidate(PointToScreen(e.Location));
            if (candidate?.Handle == _hoveredWindow?.Handle)
                return;

            _hoveredWindow = candidate;
            Invalidate();
        }

        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _hoveredWindow = FindCandidate(PointToScreen(e.Location));
            ConfirmSelection();
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
                return;
            }

            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                ConfirmSelection();
                e.Handled = true;
            }
        }

        private void ConfirmSelection()
        {
            if (_hoveredWindow is null)
                return;

            SelectedTarget = RecordingCaptureTarget.ForWindow(
                _hoveredWindow.Handle,
                _hoveredWindow.Bounds,
                _hoveredWindow.Title);
            DialogResult = DialogResult.OK;
            Close();
        }

        private WindowCandidate? FindCandidate(Point screenPoint)
        {
            return _windows.FirstOrDefault(candidate => candidate.Bounds.Contains(screenPoint));
        }

        private Rectangle ToLocal(Rectangle screenBounds) => new(
            screenBounds.X - _virtualBounds.X,
            screenBounds.Y - _virtualBounds.Y,
            screenBounds.Width,
            screenBounds.Height);

        private static void DrawWindowLabel(Graphics graphics, WindowCandidate window, Rectangle bounds)
        {
            string title = string.IsNullOrWhiteSpace(window.Title) ? "WINDOW" : window.Title;
            string subtitle = $"{bounds.Width} × {bounds.Height}";

            float scale = Math.Clamp(Math.Min(bounds.Width / 1280f, bounds.Height / 720f), 0.72f, 1.25f);
            using var titleFont = new Font("Segoe UI Semibold", 22f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            using var subtitleFont = new Font("Segoe UI", 14f * scale, FontStyle.Regular, GraphicsUnit.Pixel);

            var titleSize = graphics.MeasureString(title, titleFont, Math.Max(160, bounds.Width - 56));
            var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
            float panelWidth = Math.Min(bounds.Width - 24f, Math.Max(220f, Math.Max(titleSize.Width, subtitleSize.Width) + 40f * scale));
            float panelHeight = titleSize.Height + subtitleSize.Height + 28f * scale;
            float panelX = bounds.Left + (bounds.Width - panelWidth) / 2f;
            float panelY = bounds.Top + (bounds.Height - panelHeight) / 2f;
            var panel = new RectangleF(panelX, panelY, panelWidth, panelHeight);

            using var panelBrush = new SolidBrush(Color.FromArgb(226, 18, 25, 36));
            using var textBrush = new SolidBrush(Color.White);
            using var path = RoundedRectangle(panel, 12f * scale);
            graphics.FillPath(panelBrush, path);
            graphics.DrawString(title, titleFont, textBrush,
                new RectangleF(panelX + 20f * scale, panelY + 8f * scale, panelWidth - 40f * scale, titleSize.Height));
            graphics.DrawString(subtitle, subtitleFont, textBrush,
                panelX + 20f * scale, panelY + titleSize.Height + 8f * scale);
        }

        private void DrawInstruction(Graphics graphics, bool noWindows)
        {
            string instruction = noWindows
                ? "No recordable windows found · Esc to cancel"
                : "Select a window to record · Click to confirm · Esc to cancel";
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

    internal sealed record WindowCandidate(nint Handle, Rectangle Bounds, string Title);

    private sealed class PerMonitorDpiScope : IDisposable
    {
        private readonly nint _previousContext;

        public PerMonitorDpiScope()
        {
            _previousContext = NativeMethods.SetThreadDpiAwarenessContext(
                NativeMethods.DpiAwarenessContextPerMonitorAwareV2);
        }

        public void Dispose()
        {
            if (_previousContext != nint.Zero)
                NativeMethods.SetThreadDpiAwarenessContext(_previousContext);
        }
    }

    private static class NativeMethods
    {
        internal static readonly nint DpiAwarenessContextPerMonitorAwareV2 = new(-4);

        private const int GwlExStyle = -20;
        private const long WsExToolWindow = 0x00000080L;
        private const uint DwmwaExtendedFrameBounds = 9;
        private const uint DwmwaCloaked = 14;
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;

        internal static Rectangle GetPhysicalVirtualScreen() => new(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));

        internal static IReadOnlyList<WindowCandidate> EnumerateRecordableWindows(Rectangle virtualBounds)
        {
            var result = new List<WindowCandidate>();
            int currentProcessId = Environment.ProcessId;

            EnumWindows((window, parameter) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window))
                    return true;

                GetWindowThreadProcessId(window, out uint processId);
                if (processId == currentProcessId || processId == 0)
                    return true;

                long exStyle = GetWindowLongPtr(window, GwlExStyle).ToInt64();
                if ((exStyle & WsExToolWindow) != 0)
                    return true;

                if (DwmGetWindowAttributeUInt(window, DwmwaCloaked, out uint cloaked, sizeof(uint)) == 0 && cloaked != 0)
                    return true;

                int titleLength = GetWindowTextLength(window);
                if (titleLength <= 0)
                    return true;

                var titleBuilder = new StringBuilder(titleLength + 1);
                GetWindowText(window, titleBuilder, titleBuilder.Capacity);
                string title = titleBuilder.ToString().Trim();
                if (title.Length == 0)
                    return true;

                if (!TryGetExtendedFrameBounds(window, out var bounds))
                    return true;

                bounds = Rectangle.Intersect(bounds, virtualBounds);
                if (bounds.Width < 80 || bounds.Height < 60)
                    return true;

                result.Add(new WindowCandidate(window, bounds, title));
                return true;
            }, nint.Zero);

            return result;
        }

        private static bool TryGetExtendedFrameBounds(nint window, out Rectangle bounds)
        {
            if (DwmGetWindowAttributeRect(window, DwmwaExtendedFrameBounds, out Rect rect, Marshal.SizeOf<Rect>()) != 0)
            {
                if (!GetWindowRect(window, out rect))
                {
                    bounds = Rectangle.Empty;
                    return false;
                }
            }

            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return bounds.Width > 0 && bounds.Height > 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(nint window, nint parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(nint window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(nint window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern nint GetWindowLongPtr64(nint window, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern nint GetWindowLong32(nint window, int index);

        private static nint GetWindowLongPtr(nint window, int index) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(nint window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(nint window, StringBuilder text, int maxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(nint window, out Rect rect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        internal static extern nint SetThreadDpiAwarenessContext(nint dpiContext);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmGetWindowAttributeRect(nint window, uint attribute, out Rect value, int valueSize);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmGetWindowAttributeUInt(nint window, uint attribute, out uint value, int valueSize);
    }
}
