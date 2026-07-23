using System.Drawing;
using OddSnap.Capture;
using OddSnap.Models;
using OddSnap.UI;

namespace OddSnap;

public partial class App
{
    private void ShowSnippingImageResult(Bitmap image, string? filePath)
    {
        try
        {
            var window = new SnippingResultWindow(image, filePath);
            HookSnippingResultWindow(window);
            window.Show();
            window.Activate();
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private void ShowSnippingMediaResult(string filePath, Bitmap? previewFrame)
    {
        try
        {
            var window = new SnippingResultWindow(filePath, previewFrame);
            HookSnippingResultWindow(window);
            window.Show();
            window.Activate();
        }
        catch
        {
            previewFrame?.Dispose();
            throw;
        }
    }

    private void HookSnippingResultWindow(SnippingResultWindow window)
    {
        window.NewScreenshotRequested += () => StartSnippingLauncher(SnippingLauncherMode.Screenshot);
        window.NewRecordingRequested += () => StartSnippingLauncher(SnippingLauncherMode.Recording);
    }

    private void StartSnippingLauncher(SnippingLauncherMode mode)
    {
        if (Interlocked.CompareExchange(ref _isCapturing, 1, 0) != 0)
            return;

        HideSettingsForCapture();
        LaunchOverlay(
            CaptureMode.Rectangle,
            snippingLauncherMode: mode);
    }
}
