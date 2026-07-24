using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class SnippingRecordingTargetToolbarTests
{
    private static readonly BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void RecordingLauncherExposesAreaWindowAndMonitorTargets()
    {
        Exception? failure = null;
        bool toolbarReady = false;
        string? activeToolId = null;
        string[] toolIds = Array.Empty<string>();
        var requestedActions = new List<string>();
        using var finished = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            System.Windows.Forms.Timer? readinessTimer = null;
            try
            {
                var bounds = new Rectangle(0, 0, 1000, 700);
                using var screenshot = new Bitmap(bounds.Width, bounds.Height);
                using var form = new RegionOverlayForm(
                    screenshot,
                    bounds,
                    CaptureMode.Rectangle,
                    WindowDetectionMode.Off,
                    CenterSelectionAspectRatio.Free,
                    SnippingLauncherMode.Recording);

                form.ToolbarActionRequested += requestedActions.Add;
                form.Shown += (_, _) =>
                {
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
                        toolbarReady = true;
                        toolIds = GetTools(form).Select(tool => tool.Id).ToArray();
                        activeToolId = GetActiveToolId(form);

                        ClickToolbarAction(form, "_recordWindow");
                        ClickToolbarAction(form, "_recordMonitor");
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
            Name = "OddSnap recording target toolbar test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(12)), "Recording target toolbar test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        Assert.True(toolbarReady, "The recording launcher toolbar did not become visible.");
        Assert.Contains("_snipArea", toolIds);
        Assert.Contains("_recordWindow", toolIds);
        Assert.Contains("_recordMonitor", toolIds);
        Assert.Equal("_snipArea", activeToolId);
        Assert.Equal(new[] { "_recordWindow", "_recordMonitor" }, requestedActions);
    }

    private static Form? GetToolbar(RegionOverlayForm form) =>
        typeof(RegionOverlayForm).GetField("_toolbarForm", InstancePrivate)?.GetValue(form) as Form;

    private static ToolDef[] GetTools(RegionOverlayForm form) =>
        typeof(RegionOverlayForm).GetField("_mainBarTools", InstancePrivate)?.GetValue(form) as ToolDef[]
        ?? Array.Empty<ToolDef>();

    private static string? GetActiveToolId(RegionOverlayForm form) =>
        typeof(RegionOverlayForm).GetField("_activeToolId", InstancePrivate)?.GetValue(form) as string;

    private static void ClickToolbarAction(RegionOverlayForm form, string toolId)
    {
        var ids = typeof(RegionOverlayForm).GetField("_toolbarToolIds", InstancePrivate)?.GetValue(form) as string[]
            ?? throw new InvalidOperationException("Toolbar IDs were not initialized.");
        var buttons = typeof(RegionOverlayForm).GetField("_toolbarButtons", InstancePrivate)?.GetValue(form) as Rectangle[]
            ?? throw new InvalidOperationException("Toolbar bounds were not initialized.");

        int index = Array.FindIndex(ids, id => string.Equals(id, toolId, StringComparison.Ordinal));
        if (index < 0)
            throw new InvalidOperationException($"Toolbar action {toolId} was not found.");

        var button = buttons[index];
        var screenPoint = form.PointToScreen(new Point(button.Left + button.Width / 2, button.Top + button.Height / 2));
        Assert.True(form.HandleToolbarSurfaceMouseDown(screenPoint, MouseButtons.Left));
    }
}
