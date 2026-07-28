using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OddSnap.Native;

namespace OddSnap.Capture;

internal sealed class RecordingToolbarForm : Form
{
    private const byte ActiveAlpha = 255;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(850);
    private static readonly TimeSpan DiscardConfirmationWindow = TimeSpan.FromSeconds(2.5);

    private readonly RecordingForm _owner;
    private readonly System.Windows.Forms.Timer _fadeTimer;
    private Bitmap? _surface;
    private Graphics? _surfaceGraphics;
    private int _hoveredButton = -1;
    private DateTime _lastInteractionUtc = DateTime.UtcNow;
    private byte _surfaceAlpha = ActiveAlpha;
    private bool _pointerInside;
    private bool _dragging;
    private int _pressedButton = -1;
    private DateTime _discardArmedUntilUtc = DateTime.MinValue;
    private Point _dragOffset;
    private Point? _manualOffsetInMonitor;

    public RecordingToolbarForm(RecordingForm owner)
    {
        _owner = owner;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = owner.TopMost;
        StartPosition = FormStartPosition.Manual;

        _fadeTimer = new System.Windows.Forms.Timer { Interval = 35 };
        _fadeTimer.Tick += (_, _) => UpdateIdleOpacity();
        _fadeTimer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= User32.WS_EX_TOOLWINDOW;
            cp.ExStyle |= User32.WS_EX_NOACTIVATE;
            cp.ExStyle |= User32.WS_EX_LAYERED;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CaptureWindowExclusion.Apply(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        WakeSurface();
        UpdateSurface();
    }

    public void ApplyAutomaticBounds(Rectangle requestedBounds)
    {
        if (requestedBounds.Width <= 0 || requestedBounds.Height <= 0 || IsDisposed)
            return;

        var workArea = ResolveWorkArea(requestedBounds);
        Rectangle target;
        if (_manualOffsetInMonitor is { } offset)
        {
            target = new Rectangle(
                workArea.Left + offset.X,
                workArea.Top + offset.Y,
                requestedBounds.Width,
                requestedBounds.Height);
        }
        else
        {
            target = requestedBounds;
        }

        target = ClampToWorkArea(target, workArea);
        if (Bounds != target)
            Bounds = target;
    }

    public void UpdateSurface()
    {
        var sz = Size;
        if (sz.Width <= 0 || sz.Height <= 0 || IsDisposed || !IsHandleCreated)
            return;

        if (_surface == null || _surface.Width != sz.Width || _surface.Height != sz.Height)
        {
            _surfaceGraphics?.Dispose();
            _surface?.Dispose();
            _surface = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppPArgb);
            _surfaceGraphics = Graphics.FromImage(_surface);
        }

        var g = _surfaceGraphics!;
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        _owner.PaintRecordingToolbarTo(g, new Rectangle(Point.Empty, sz), _hoveredButton, IsDiscardArmed);
        g.Flush(FlushIntention.Sync);

        var screenPt = new User32.POINT { X = Left, Y = Top };
        var size = new User32.SIZE { cx = sz.Width, cy = sz.Height };
        var srcPt = new User32.POINT { X = 0, Y = 0 };
        var blend = new User32.BLENDFUNCTION
        {
            BlendOp = 0,
            BlendFlags = 0,
            SourceConstantAlpha = _surfaceAlpha,
            AlphaFormat = 1
        };

        IntPtr hdcScreen = User32.GetDC(IntPtr.Zero);
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBmp = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcMem = User32.CreateCompatibleDC(hdcScreen);
            hBmp = _surface.GetHbitmap(Color.FromArgb(0));
            hOld = User32.SelectObject(hdcMem, hBmp);
            User32.UpdateLayeredWindow(Handle, hdcScreen, ref screenPt, ref size,
                hdcMem, ref srcPt, 0, ref blend, 2);
        }
        finally
        {
            if (hdcMem != IntPtr.Zero && hOld != IntPtr.Zero)
                User32.SelectObject(hdcMem, hOld);
            if (hBmp != IntPtr.Zero)
                User32.DeleteObject(hBmp);
            if (hdcMem != IntPtr.Zero)
                User32.DeleteDC(hdcMem);
            User32.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _pointerInside = true;
        WakeSurface();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _pointerInside = true;
        WakeSurface();

        if (_dragging)
        {
            MoveByPointer();
            return;
        }

        int previous = _hoveredButton;
        _hoveredButton = GetButtonAt(e.Location);
        Cursor = _hoveredButton >= 0 ? Cursors.Hand : Cursors.SizeAll;
        if (_hoveredButton != previous)
            UpdateSurface();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragging)
            return;

        _pointerInside = false;
        if (_hoveredButton != -1)
        {
            _hoveredButton = -1;
            UpdateSurface();
        }
        Cursor = Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        WakeSurface();
        int button = GetButtonAt(e.Location);
        if (button >= 0)
        {
            if (button != 2)
                DisarmDiscardConfirmation();

            _pressedButton = button;
            _hoveredButton = button;
            Capture = true;
            UpdateSurface();
            return;
        }

        DisarmDiscardConfirmation();
        _dragging = true;
        _dragOffset = e.Location;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (_pressedButton >= 0)
        {
            int pressedButton = _pressedButton;
            _pressedButton = -1;
            Capture = false;

            if (GetButtonAt(e.Location) == pressedButton)
                ExecuteToolbarButton(pressedButton);
            else
                UpdateSurface();

            return;
        }

        if (!_dragging)
            return;

        MoveByPointer();
        _dragging = false;
        Capture = false;
        _pointerInside = ClientRectangle.Contains(PointToClient(MousePosition));
        Cursor = _pointerInside ? Cursors.SizeAll : Cursors.Default;
        WakeSurface();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture || _pressedButton < 0)
            return;

        _pressedButton = -1;
        UpdateSurface();
    }

    private void ExecuteToolbarButton(int button)
    {
        switch (button)
        {
            case 0:
                DisarmDiscardConfirmation();
                _owner.RequestToolbarTogglePause();
                break;

            case 1:
                DisarmDiscardConfirmation();
                _owner.RequestToolbarStop();
                break;

            case 2:
            {
                var nowUtc = DateTime.UtcNow;
                if (ResolveDiscardClick(nowUtc, _discardArmedUntilUtc) == DiscardClickDecision.Discard)
                {
                    _discardArmedUntilUtc = DateTime.MinValue;
                    _owner.RequestToolbarDiscard();
                }
                else
                {
                    _discardArmedUntilUtc = nowUtc + DiscardConfirmationWindow;
                    _lastInteractionUtc = nowUtc;
                    UpdateSurface();
                }
                break;
            }
        }
    }

    private void DisarmDiscardConfirmation()
    {
        if (_discardArmedUntilUtc == DateTime.MinValue)
            return;

        _discardArmedUntilUtc = DateTime.MinValue;
        UpdateSurface();
    }

    internal enum DiscardClickDecision
    {
        Arm,
        Discard
    }

    internal static DiscardClickDecision ResolveDiscardClick(DateTime nowUtc, DateTime armedUntilUtc)
        => nowUtc <= armedUntilUtc ? DiscardClickDecision.Discard : DiscardClickDecision.Arm;

    private bool IsDiscardArmed => DateTime.UtcNow <= _discardArmedUntilUtc;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if ((keyData & Keys.KeyCode) == Keys.Escape)
        {
            _owner.RequestToolbarStop();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private int GetButtonAt(Point location) =>
        RecordingForm.GetRecordingToolbarPauseButton(ClientRectangle).Contains(location) ? 0
        : RecordingForm.GetRecordingToolbarStopButton(ClientRectangle).Contains(location) ? 1
        : RecordingForm.GetRecordingToolbarDiscardButton(ClientRectangle).Contains(location) ? 2
        : -1;

    private void MoveByPointer()
    {
        if (!_dragging)
            return;

        var pointer = MousePosition;
        var workArea = ResolveWorkArea(new Rectangle(pointer, new Size(1, 1)));
        var requested = new Rectangle(
            pointer.X - _dragOffset.X,
            pointer.Y - _dragOffset.Y,
            Width,
            Height);
        var clamped = ClampToWorkArea(requested, workArea);

        if (Bounds != clamped)
            Bounds = clamped;

        _manualOffsetInMonitor = new Point(
            clamped.Left - workArea.Left,
            clamped.Top - workArea.Top);
        UpdateSurface();
    }

    private void WakeSurface()
    {
        _lastInteractionUtc = DateTime.UtcNow;
        if (_surfaceAlpha == ActiveAlpha)
            return;

        _surfaceAlpha = ActiveAlpha;
        UpdateSurface();
    }

    internal static byte ResolveIdleAlpha(bool fadeWhenIdle, int opacityPercent)
    {
        if (!fadeWhenIdle)
            return ActiveAlpha;

        int clampedPercent = Math.Clamp(opacityPercent, 20, 100);
        return (byte)Math.Round(
            ActiveAlpha * (clampedPercent / 100d),
            MidpointRounding.AwayFromZero);
    }

    private void UpdateIdleOpacity()
    {
        if (IsDisposed || !IsHandleCreated || !Visible)
            return;

        var nowUtc = DateTime.UtcNow;
        if (_discardArmedUntilUtc != DateTime.MinValue && nowUtc > _discardArmedUntilUtc)
        {
            _discardArmedUntilUtc = DateTime.MinValue;
            UpdateSurface();
        }

        byte target = _pointerInside || _dragging || nowUtc - _lastInteractionUtc < IdleDelay
            ? ActiveAlpha
            : ResolveIdleAlpha(
                _owner.FadeRecordingToolbarWhenIdle,
                _owner.RecordingToolbarIdleOpacityPercent);
        if (_surfaceAlpha == target)
            return;

        int delta = target > _surfaceAlpha ? 24 : -18;
        int next = _surfaceAlpha + delta;
        _surfaceAlpha = target > _surfaceAlpha
            ? (byte)Math.Min(target, next)
            : (byte)Math.Max(target, next);
        UpdateSurface();
    }

    private static Rectangle ResolveWorkArea(Rectangle bounds)
    {
        try
        {
            return Screen.FromRectangle(bounds).WorkingArea;
        }
        catch
        {
            return SystemInformation.VirtualScreen;
        }
    }

    private static Rectangle ClampToWorkArea(Rectangle bounds, Rectangle workArea)
    {
        int maxX = Math.Max(workArea.Left, workArea.Right - bounds.Width);
        int maxY = Math.Max(workArea.Top, workArea.Bottom - bounds.Height);
        return new Rectangle(
            Math.Clamp(bounds.Left, workArea.Left, maxX),
            Math.Clamp(bounds.Top, workArea.Top, maxY),
            bounds.Width,
            bounds.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
            if (IsHandleCreated)
                CaptureWindowExclusion.Unregister(Handle);
            _surfaceGraphics?.Dispose();
            _surface?.Dispose();
        }

        base.Dispose(disposing);
    }
}
