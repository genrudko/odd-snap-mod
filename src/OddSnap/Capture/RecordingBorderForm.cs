using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using OddSnap.Helpers;
using OddSnap.Native;

namespace OddSnap.Capture;

internal sealed class RecordingBorderForm : Form
{
    private Rectangle _recordingScreenBounds;
    private readonly int _pad;
    private Bitmap? _surface;
    private Graphics? _surfaceGraphics;

    public RecordingBorderForm(Rectangle recordingScreenBounds)
    {
        _recordingScreenBounds = recordingScreenBounds;
        _pad = UiChrome.ScaleInt(10);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Bounds = Rectangle.Inflate(recordingScreenBounds, _pad, _pad);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= User32.WS_EX_TOOLWINDOW;
            cp.ExStyle |= User32.WS_EX_NOACTIVATE;
            cp.ExStyle |= User32.WS_EX_LAYERED;
            cp.ExStyle |= User32.WS_EX_TRANSPARENT;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        CaptureWindowExclusion.Apply(this);
    }

    public void SetRecordingScreenBounds(Rectangle recordingScreenBounds)
    {
        if (recordingScreenBounds.Width <= 0 || recordingScreenBounds.Height <= 0)
            return;

        if (_recordingScreenBounds == recordingScreenBounds)
            return;

        _recordingScreenBounds = recordingScreenBounds;
        Bounds = Rectangle.Inflate(recordingScreenBounds, _pad, _pad);
        UpdateSurface();
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
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float lineWidth = UiChrome.ScaleFloat(2f);
        float inset = Math.Max(2f, UiChrome.ScaleFloat(3f));
        var borderRect = new RectangleF(
            _pad - inset,
            _pad - inset,
            _recordingScreenBounds.Width + inset * 2,
            _recordingScreenBounds.Height + inset * 2);

        using (var shadowPen = new Pen(Color.FromArgb(80, 0, 0, 0), lineWidth + 2f)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = new[] { 4f, 3f },
            LineJoin = LineJoin.Miter
        })
        {
            g.DrawRectangle(shadowPen, borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height);
        }

        using (var borderPen = new Pen(Color.FromArgb(230, 239, 68, 68), lineWidth)
        {
            DashStyle = DashStyle.Dash,
            DashPattern = new[] { 4f, 3f },
            LineJoin = LineJoin.Miter
        })
        {
            g.DrawRectangle(borderPen, borderRect.X, borderRect.Y, borderRect.Width, borderRect.Height);
        }

        float corner = UiChrome.ScaleFloat(7f);
        using var cornerBrush = new SolidBrush(Color.FromArgb(235, 239, 68, 68));
        FillCorner(g, cornerBrush, borderRect.Left, borderRect.Top, corner);
        FillCorner(g, cornerBrush, borderRect.Right, borderRect.Top, corner);
        FillCorner(g, cornerBrush, borderRect.Left, borderRect.Bottom, corner);
        FillCorner(g, cornerBrush, borderRect.Right, borderRect.Bottom, corner);

        g.Flush(FlushIntention.Sync);
        UpdateLayeredWindowSurface(sz);
    }

    private void UpdateLayeredWindowSurface(Size sz)
    {
        var screenPt = new User32.POINT { X = Left, Y = Top };
        var size = new User32.SIZE { cx = sz.Width, cy = sz.Height };
        var srcPt = new User32.POINT { X = 0, Y = 0 };
        var blend = new User32.BLENDFUNCTION
        {
            BlendOp = 0,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = 1
        };

        IntPtr hdcScreen = User32.GetDC(IntPtr.Zero);
        IntPtr hdcMem = IntPtr.Zero;
        IntPtr hBmp = IntPtr.Zero;
        IntPtr hOld = IntPtr.Zero;

        try
        {
            hdcMem = User32.CreateCompatibleDC(hdcScreen);
            hBmp = _surface!.GetHbitmap(Color.FromArgb(0));
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

    private static void FillCorner(Graphics g, Brush brush, float x, float y, float size)
        => g.FillRectangle(brush, x - size / 2f, y - size / 2f, size, size);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (IsHandleCreated)
                CaptureWindowExclusion.Unregister(Handle);
            _surfaceGraphics?.Dispose();
            _surface?.Dispose();
        }

        base.Dispose(disposing);
    }
}
