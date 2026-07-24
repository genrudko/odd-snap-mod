using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class SnippingOverlayHandoffTests
{
    [Fact]
    public void RecordingHandoffHidesAndClosesLauncherBeforeNextFormStarts()
    {
        Exception? failure = null;
        bool eventRaised = false;
        bool overlayHiddenBeforeEvent = false;
        bool overlayClosed = false;
        Rectangle receivedSelection = Rectangle.Empty;
        using var finished = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            System.Windows.Forms.Timer? watchdog = null;
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

                form.RecordingRegionSelected += selection =>
                {
                    eventRaised = true;
                    overlayHiddenBeforeEvent = !form.Visible;
                    receivedSelection = selection;
                };
                form.FormClosed += (_, _) =>
                {
                    overlayClosed = true;
                    finished.Set();
                };
                form.Shown += (_, _) =>
                {
                    form.BeginInvoke(new Action(() =>
                        form.TriggerSnippingRecordingHandoffForTests(
                            new Rectangle(120, 90, 320, 240))));
                };

                var deadline = DateTime.UtcNow.AddSeconds(5);
                watchdog = new System.Windows.Forms.Timer { Interval = 50 };
                watchdog.Tick += (_, _) =>
                {
                    if (DateTime.UtcNow < deadline)
                        return;

                    watchdog.Stop();
                    form.Close();
                };
                watchdog.Start();

                Application.Run(form);
            }
            catch (Exception ex)
            {
                failure = ex;
                finished.Set();
            }
            finally
            {
                watchdog?.Stop();
                watchdog?.Dispose();
            }
        })
        {
            IsBackground = true,
            Name = "OddSnap snipping overlay handoff test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(10)), "The launcher overlay did not close after recording handoff.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();

        Assert.True(eventRaised, "The recording selection event was not raised.");
        Assert.True(overlayHiddenBeforeEvent, "The launcher was still visible when the recording handoff event fired.");
        Assert.True(overlayClosed, "The launcher overlay did not reach FormClosed.");
        Assert.Equal(new Rectangle(120, 90, 320, 240), receivedSelection);
    }
}
