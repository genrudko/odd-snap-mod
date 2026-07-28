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
            System.Windows.Forms.Timer? watchdog = null;
            System.Threading.Timer? hardStop = null;
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

                // Hosted Windows runners occasionally fail to deliver Shown or the
                // WinForms timer tick promptly. Keep a non-UI watchdog so the test
                // cannot strand its STA message loop indefinitely.
                hardStop = new System.Threading.Timer(
                    _ =>
                    {
                        try
                        {
                            if (!form.IsDisposed && form.IsHandleCreated)
                                form.BeginInvoke(new Action(form.Close));
                        }
                        catch (InvalidOperationException) { }
                        catch (ObjectDisposedException) { }
                    },
                    null,
                    TimeSpan.FromSeconds(10),
                    Timeout.InfiniteTimeSpan);

                form.Shown += (_, _) =>
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        InvokeMouse(form, "OnMouseDown", MouseButtons.Left, 120, 160);
                        InvokeMouse(form, "OnMouseMove", MouseButtons.Left, 520, 380);
                        InvokeMouse(form, "OnMouseUp", MouseButtons.Left, 520, 380);

                        var deadline = DateTime.UtcNow.AddSeconds(6);
                        watchdog = new System.Windows.Forms.Timer { Interval = 50 };
                        watchdog.Tick += (_, _) =>
                        {
                            var toolbarField = typeof(RegionOverlayForm).GetField("_toolbarForm", InstancePrivate);
                            var toolsField = typeof(RegionOverlayForm).GetField("_mainBarTools", InstancePrivate);
                            var toolbar = toolbarField?.GetValue(form) as Form;
                            var tools = toolsField?.GetValue(form) as ToolDef[];

                            if (toolbar?.Visible == true && tools?.Any(tool => tool.Id == "_snipStart") == true)
                            {
                                toolbarRestored = true;
                                watchdog.Stop();
                                form.Close();
                                return;
                            }

                            if (DateTime.UtcNow >= deadline)
                            {
                                watchdog.Stop();
                                form.Close();
                            }
                        };
                        watchdog.Start();
                    }));
                };

                Application.Run(form);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                watchdog?.Stop();
                watchdog?.Dispose();
                hardStop?.Dispose();
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "OddSnap recording launcher toolbar restore test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(20)), "The recording selection toolbar test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.True(toolbarRestored, "The recording launcher toolbar stayed hidden or did not expose Start recording after mouse-up.");
    }

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
