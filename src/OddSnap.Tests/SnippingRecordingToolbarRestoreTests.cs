using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class SnippingRecordingToolbarRestoreTests
{
    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void CompletingRecordingSelectionShowsToolbarWithStartAction()
    {
        Exception? failure = null;
        bool toolbarRestored = false;
        using var finished = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            System.Windows.Forms.Timer? readinessTimer = null;
            try
            {
                var bounds = new Rectangle(0, 0, 800, 600);
                using var screenshot = new Bitmap(bounds.Width, bounds.Height);
                using var form = new RegionOverlayForm(
                    screenshot,
                    bounds,
                    CaptureMode.Rectangle,
                    WindowDetectionMode.Off,
                    CenterSelectionAspectRatio.Free,
                    SnippingLauncherMode.Recording);

                form.Shown += (_, _) =>
                {
                    // Wait for the separate toolbar window to be created before
                    // synthesizing the drag. Starting immediately from Shown races
                    // QueueToolbarReady on hosted Windows runners.
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    readinessTimer = new System.Windows.Forms.Timer { Interval = 25 };
                    readinessTimer.Tick += (_, _) =>
                    {
                        var toolbar = GetToolbar(form);
                        if (toolbar?.Visible != true)
                        {
                            if (DateTime.UtcNow >= deadline)
                            {
                                readinessTimer.Stop();
                                form.Close();
                            }
                            return;
                        }

                        readinessTimer.Stop();
                        InvokeMouse(form, "OnMouseDown", MouseButtons.Left, 120, 160);
                        InvokeMouse(form, "OnMouseMove", MouseButtons.Left, 520, 380);
                        InvokeMouse(form, "OnMouseUp", MouseButtons.Left, 520, 380);

                        toolbar = GetToolbar(form);
                        toolbarRestored = toolbar?.Visible == true &&
                            GetTools(form).Any(tool => tool.Id == "_snipStart");
                        form.Close();
                    };
                    readinessTimer.Start();
                };

                form.FormClosed += (_, _) => finished.Set();
                Application.Run(form);
            }
            catch (Exception ex)
            {
                failure = ex;
                finished.Set();
            }
            finally
            {
                readinessTimer?.Stop();
                readinessTimer?.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "OddSnap recording launcher toolbar restore test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(12)), "The recording selection toolbar test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.True(toolbarRestored, "The recording launcher toolbar stayed hidden or did not expose Start recording after mouse-up.");
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
