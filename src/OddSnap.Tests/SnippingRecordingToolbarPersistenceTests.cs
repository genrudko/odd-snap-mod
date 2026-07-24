using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class SnippingRecordingToolbarPersistenceTests
{
    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RecordingLauncherToolbarRemainsVisibleDuringAndAfterRegionDrag()
    {
        Exception? failure = null;
        bool visibleBeforeDrag = false;
        bool visibleDuringDrag = false;
        bool visibleAfterDrag = false;
        bool hasStartAction = false;
        using var finished = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                var bounds = new Rectangle(0, 0, 900, 650);
                using var screenshot = new Bitmap(bounds.Width, bounds.Height);
                using var form = new RegionOverlayForm(
                    screenshot,
                    bounds,
                    CaptureMode.Rectangle,
                    WindowDetectionMode.Off,
                    CenterSelectionAspectRatio.Free,
                    SnippingLauncherMode.Recording);

                form.Shown += (_, _) => form.BeginInvoke(new Action(() =>
                {
                    var toolbar = GetToolbar(form);
                    visibleBeforeDrag = toolbar?.Visible == true;

                    InvokeMouse(form, "OnMouseDown", MouseButtons.Left, 140, 180);
                    visibleDuringDrag = toolbar?.Visible == true;

                    InvokeMouse(form, "OnMouseMove", MouseButtons.Left, 560, 410);
                    InvokeMouse(form, "OnMouseUp", MouseButtons.Left, 560, 410);

                    toolbar = GetToolbar(form);
                    visibleAfterDrag = toolbar?.Visible == true;
                    hasStartAction = GetTools(form).Any(tool => tool.Id == "_snipStart");
                    form.Close();
                }));

                form.FormClosed += (_, _) => finished.Set();
                Application.Run(form);
            }
            catch (Exception ex)
            {
                failure = ex;
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "OddSnap recording launcher persistent toolbar test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(12)), "Persistent toolbar test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        Assert.True(visibleBeforeDrag, "The recording launcher toolbar was not visible before selection.");
        Assert.True(visibleDuringDrag, "The recording launcher toolbar was hidden when selection began.");
        Assert.True(visibleAfterDrag, "The recording launcher toolbar was not visible after selection.");
        Assert.True(hasStartAction, "The Start recording action was not added after selection.");
    }

    private static Form? GetToolbar(RegionOverlayForm form) =>
        typeof(RegionOverlayForm).GetField("_toolbarForm", InstancePrivate)?.GetValue(form) as Form;

    private static ToolDef[] GetTools(RegionOverlayForm form) =>
        typeof(RegionOverlayForm).GetField("_mainBarTools", InstancePrivate)?.GetValue(form) as ToolDef[]
        ?? Array.Empty<ToolDef>();

    private static void InvokeMouse(
        RegionOverlayForm form,
        string methodName,
        MouseButtons button,
        int x,
        int y)
    {
        var method = typeof(RegionOverlayForm).GetMethod(methodName, InstancePrivate)
            ?? throw new MissingMethodException(typeof(RegionOverlayForm).FullName, methodName);
        method.Invoke(form, [new MouseEventArgs(button, 1, x, y, 0)]);
    }
}
