using System.Drawing;
using System.Windows.Forms;

namespace OddSnap.Capture;

public sealed partial class RegionOverlayForm
{
    internal int HandleToolbarSurfaceMouseMove(Point screenPoint)
    {
        if (IsDisposed || Disposing)
            return -1;

        var clientPoint = PointToClient(screenPoint);
        int button = GetToolbarButtonAt(clientPoint);
        if (button != _hoveredButton)
        {
            _hoveredButton = button;
            UpdateToolbarTooltip(clientPoint);
            RefreshToolbar();
            UpdateToolbarSurfaceOnly();
        }

        return button;
    }

    internal bool HandleToolbarSurfaceMouseDown(Point screenPoint, MouseButtons button)
    {
        if (IsDisposed || Disposing)
            return false;

        if (button == MouseButtons.Right)
        {
            Cancel();
            return true;
        }

        if (button != MouseButtons.Left)
            return false;

        var clientPoint = PointToClient(screenPoint);
        int index = GetToolbarButtonAt(clientPoint);
        if (index < 0)
            return false;

        CloseCaptureMagnifier();

        if (index == BtnCount - 1)
        {
            Cancel();
            return true;
        }

        if (ColorButtonIndex >= 0 && index == ColorButtonIndex)
        {
            ToggleColorPicker();
            return true;
        }

        if (_moreButtonIndex >= 0 && index == _moreButtonIndex)
        {
            if (_flyoutOpen)
                CloseMoreToolsDropdown();
            else
                ShowMoreToolsDropdown();
            return true;
        }

        if (index < _mainBarTools.Length)
        {
            ActivateToolbarItem(_mainBarTools[index]);
            return true;
        }

        return false;
    }

    internal void HandleToolbarSurfaceMouseLeave()
    {
        if (IsDisposed || Disposing)
            return;

        HandleToolbarSurfaceMouseMove(System.Windows.Forms.Cursor.Position);
    }
}
