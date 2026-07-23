using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OddSnap.Capture;

/// <summary>
/// Resolves concrete recording targets from the current Windows desktop layout.
/// Monitor recording uses a dedicated full-desktop picker so the target is confirmed
/// explicitly instead of silently using whichever monitor contains the cursor.
/// </summary>
public static class RecordingCaptureTargetSelector
{
    public static IReadOnlyList<RecordingCaptureTarget> GetMonitorTargets()
    {
        return GetOrderedScreens()
            .Select((screen, index) => RecordingCaptureTarget.ForMonitor(
                screen.Bounds,
                BuildMonitorDisplayName(screen, index + 1)))
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

        return selectedTarget ?? CreateTargetForScreen(Screen.FromPoint(screenPoint));
    }

    private static Screen[] GetOrderedScreens() =>
        Screen.AllScreens
            .OrderBy(screen => screen.Bounds.Top)
            .ThenBy(screen => screen.Bounds.Left)
            .ToArray();

    private static RecordingCaptureTarget CreateTargetForScreen(Screen screen)
    {
        var orderedScreens = GetOrderedScreens();
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

    private sealed class MonitorRecordingPickerForm : Form
    {
        private readonly Rectangle _virtualBounds;
        private readonly Screen[] _screens;
        private Screen _hoveredScreen;

        public MonitorRecordingPickerForm(Point initialScreenPoint)
        {
            _virtualBounds = SystemInformation.VirtualScreen;
            _screens = GetOrderedScreens();
            _hoveredScreen = Screen.FromPoint(initialScreenPoint);

            AutoScaleMode = AutoScaleMode.None;
            Bounds = _virtualBounds;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.Black;
            Opacity = 0.58;
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

            foreach (var screen in _screens)
            {
                var localBounds = ToLocal(screen.Bounds);
                bool active = string.Equals(
                    screen.DeviceName,
                    _hoveredScreen.DeviceName,
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

                DrawMonitorLabel(e.Graphics, screen, localBounds, active);
            }

            DrawInstruction(e.Graphics);
        }

        private void HandleMouseMove(object? sender, MouseEventArgs e)
        {
            var screenPoint = PointToScreen(e.Location);
            var candidate = Screen.FromPoint(screenPoint);
            if (string.Equals(candidate.DeviceName, _hoveredScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                return;

            _hoveredScreen = candidate;
            Invalidate();
        }

        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _hoveredScreen = Screen.FromPoint(PointToScreen(e.Location));
            SelectedTarget = CreateTargetForScreen(_hoveredScreen);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                SelectedTarget = CreateTargetForScreen(_hoveredScreen);
                DialogResult = DialogResult.OK;
                Close();
                e.Handled = true;
            }
        }

        private Rectangle ToLocal(Rectangle screenBounds) => new(
            screenBounds.X - _virtualBounds.X,
            screenBounds.Y - _virtualBounds.Y,
            screenBounds.Width,
            screenBounds.Height);

        private void DrawMonitorLabel(Graphics graphics, Screen screen, Rectangle bounds, bool active)
        {
            int ordinal = Array.FindIndex(
                _screens,
                candidate => string.Equals(candidate.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase)) + 1;

            string title = $"MONITOR {ordinal}";
            string subtitle = $"{screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? " · PRIMARY" : string.Empty)}";

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
            const string instruction = "Select a monitor to record · Click to confirm";
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
}
