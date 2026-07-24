using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class SnippingRecordingStartTests
{
    [Fact]
    public void PreselectedRegionStartsWithoutSecondSelectionInteraction()
    {
        Exception? failure = null;
        bool started = false;
        using var finished = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            System.Windows.Forms.Timer? watchdog = null;
            string? outputPath = null;
            try
            {
                var virtualBounds = SystemInformation.VirtualScreen;
                if (virtualBounds.Width < 120 || virtualBounds.Height < 120)
                    virtualBounds = new Rectangle(0, 0, 800, 600);

                var region = new Rectangle(
                    virtualBounds.Left + 20,
                    virtualBounds.Top + 20,
                    64,
                    64);
                outputPath = Path.Combine(
                    Path.GetTempPath(),
                    $"oddsnap_preselected_start_{Guid.NewGuid():N}.gif");

                using var form = new RecordingForm(
                    screenshot: null,
                    virtualBounds,
                    fps: 5,
                    savePath: outputPath,
                    format: RecordingFormat.GIF,
                    showCursor: false,
                    recordMic: false,
                    recordDesktop: false,
                    showMagnifier: false);

                form.UsePreselectedTarget(RecordingCaptureTarget.ForRegion(region));

                var deadline = DateTime.UtcNow.AddSeconds(8);
                watchdog = new System.Windows.Forms.Timer { Interval = 50 };
                watchdog.Tick += (_, _) =>
                {
                    if (form.IsRecordingActiveForTests)
                    {
                        started = true;
                        watchdog.Stop();
                        form.RequestToolbarDiscard();
                        return;
                    }

                    if (DateTime.UtcNow >= deadline)
                    {
                        watchdog.Stop();
                        form.Close();
                    }
                };
                watchdog.Start();

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
                if (outputPath is not null)
                {
                    try
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                    }
                    catch
                    {
                        // Best-effort cleanup only.
                    }
                }
                finished.Set();
            }
        })
        {
            IsBackground = true,
            Name = "OddSnap preselected recording start smoke test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(finished.Wait(TimeSpan.FromSeconds(15)), "The recording form smoke test timed out.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.True(started, "The preselected recording form never entered the recording state.");
    }
}
