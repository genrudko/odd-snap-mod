using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using OddSnap.Helpers;
using OddSnap.Services;

namespace OddSnap.UI;

public partial class SnippingResultWindow : Window
{
    private readonly Bitmap? _imageBitmap;
    private readonly string? _filePath;
    private readonly bool _isVideo;
    private readonly DispatcherTimer _positionTimer;
    private bool _mediaPlaying;
    private bool _sliderDragging;
    private TimeSpan _mediaDuration;

    public event Action? NewScreenshotRequested;
    public event Action? NewRecordingRequested;

    public SnippingResultWindow(Bitmap image, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        _imageBitmap = image;
        _filePath = filePath;

        InitializeComponent();
        _positionTimer = CreatePositionTimer();
        InitializeWindowChrome();
        ResultImage.Source = BitmapPerf.ToBitmapSource(image);
        TitleText.Text = string.IsNullOrWhiteSpace(filePath)
            ? "Screenshot"
            : Path.GetFileName(filePath);
        StatusText.Text = $"{image.Width} × {image.Height}";
        OpenFolderButton.IsEnabled = HasExistingFile();
    }

    public SnippingResultWindow(string filePath, Bitmap? previewFrame = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
        _isVideo = !Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase);
        _imageBitmap = previewFrame;

        InitializeComponent();
        _positionTimer = CreatePositionTimer();
        InitializeWindowChrome();
        TitleText.Text = Path.GetFileName(filePath);
        OpenFolderButton.IsEnabled = HasExistingFile();

        if (_isVideo && File.Exists(filePath))
        {
            ResultImage.Visibility = Visibility.Collapsed;
            ResultMedia.Visibility = Visibility.Visible;
            PlayPauseButton.Visibility = Visibility.Visible;
            PositionSlider.Visibility = Visibility.Visible;
            ResultMedia.Source = new Uri(filePath, UriKind.Absolute);
            ResultMedia.Play();
            _mediaPlaying = true;
            StatusText.Text = "Opening video…";
        }
        else if (previewFrame is not null)
        {
            ResultImage.Source = BitmapPerf.ToBitmapSource(previewFrame);
            StatusText.Text = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        }
        else
        {
            ResultImage.Visibility = Visibility.Collapsed;
            EmptyMessage.Visibility = Visibility.Visible;
            StatusText.Text = "Saved";
        }
    }

    private void InitializeWindowChrome()
    {
        OddSnapWindowChrome.ApplyRoundedCorners(this, 12);
        SourceInitialized += (_, _) => OddSnapWindowChrome.ApplyRoundedCorners(this, 12);
        Closed += (_, _) =>
        {
            _positionTimer.Stop();
            try { ResultMedia.Stop(); } catch { }
            _imageBitmap?.Dispose();
        };
    }

    private DispatcherTimer CreatePositionTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => UpdateMediaPosition();
        return timer;
    }

    private bool HasExistingFile() =>
        !string.IsNullOrWhiteSpace(_filePath) && File.Exists(_filePath);

    private void NewScreenshot_Click(object sender, RoutedEventArgs e)
    {
        NewScreenshotRequested?.Invoke();
        Close();
    }

    private void NewRecording_Click(object sender, RoutedEventArgs e)
    {
        NewRecordingRequested?.Invoke();
        Close();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_imageBitmap is not null && !_isVideo)
            {
                ClipboardService.CopyToClipboard(_imageBitmap, _filePath);
                StatusText.Text = "Image copied";
            }
            else if (HasExistingFile())
            {
                ClipboardService.CopyFilesToClipboard(_filePath!);
                StatusText.Text = "File copied";
            }
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Copy failed", ex.Message, _filePath);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var extension = HasExistingFile()
                ? Path.GetExtension(_filePath!)
                : ".png";
            var dialog = new SaveFileDialog
            {
                FileName = HasExistingFile() ? Path.GetFileName(_filePath!) : "Screenshot.png",
                DefaultExt = extension,
                Filter = BuildSaveFilter(extension)
            };
            if (dialog.ShowDialog(this) != true)
                return;

            if (HasExistingFile())
            {
                File.Copy(_filePath!, dialog.FileName, overwrite: true);
            }
            else if (_imageBitmap is not null)
            {
                SaveBitmap(dialog.FileName, _imageBitmap);
            }
            else
            {
                throw new InvalidOperationException("No result is available to save.");
            }

            StatusText.Text = $"Saved as {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Save failed", ex.Message, _filePath);
        }
    }

    private static string BuildSaveFilter(string extension) => extension.ToLowerInvariant() switch
    {
        ".mp4" => "MP4 video (*.mp4)|*.mp4|All files (*.*)|*.*",
        ".webm" => "WebM video (*.webm)|*.webm|All files (*.*)|*.*",
        ".mkv" => "Matroska video (*.mkv)|*.mkv|All files (*.*)|*.*",
        ".gif" => "GIF image (*.gif)|*.gif|All files (*.*)|*.*",
        ".jpg" or ".jpeg" => "JPEG image (*.jpg)|*.jpg|PNG image (*.png)|*.png|All files (*.*)|*.*",
        ".bmp" => "Bitmap image (*.bmp)|*.bmp|PNG image (*.png)|*.png|All files (*.*)|*.*",
        _ => "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|Bitmap image (*.bmp)|*.bmp|All files (*.*)|*.*"
    };

    private static void SaveBitmap(string path, Bitmap bitmap)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg")
            bitmap.Save(path, ImageFormat.Jpeg);
        else if (extension == ".bmp")
            bitmap.Save(path, ImageFormat.Bmp);
        else
            bitmap.Save(path, ImageFormat.Png);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!HasExistingFile())
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ToastWindow.ShowError("Open failed", ex.Message, _filePath);
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (!_isVideo)
            return;

        if (_mediaPlaying)
        {
            ResultMedia.Pause();
            _mediaPlaying = false;
            PlayPauseButton.Content = "Play";
        }
        else
        {
            ResultMedia.Play();
            _mediaPlaying = true;
            PlayPauseButton.Content = "Pause";
        }
    }

    private void ResultMedia_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (ResultMedia.NaturalDuration.HasTimeSpan)
        {
            _mediaDuration = ResultMedia.NaturalDuration.TimeSpan;
            PositionSlider.Maximum = Math.Max(0.001, _mediaDuration.TotalSeconds);
        }
        _positionTimer.Start();
        StatusText.Text = FormatTime(ResultMedia.Position) + " / " + FormatTime(_mediaDuration);
    }

    private void ResultMedia_MediaEnded(object sender, RoutedEventArgs e)
    {
        ResultMedia.Position = TimeSpan.Zero;
        ResultMedia.Pause();
        _mediaPlaying = false;
        PlayPauseButton.Content = "Play";
        UpdateMediaPosition();
    }

    private void ResultMedia_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        _positionTimer.Stop();
        ResultMedia.Visibility = Visibility.Collapsed;
        PositionSlider.Visibility = Visibility.Collapsed;
        PlayPauseButton.Visibility = Visibility.Collapsed;
        if (_imageBitmap is not null)
        {
            ResultImage.Source = BitmapPerf.ToBitmapSource(_imageBitmap);
            ResultImage.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyMessage.Visibility = Visibility.Visible;
        }
        StatusText.Text = "Playback unavailable";
        AppDiagnostics.LogWarning("snipping-result.playback", e.ErrorException?.Message ?? "Media playback failed", e.ErrorException);
    }

    private void UpdateMediaPosition()
    {
        if (!_isVideo || _sliderDragging)
            return;
        PositionSlider.Value = ResultMedia.Position.TotalSeconds;
        StatusText.Text = FormatTime(ResultMedia.Position) + " / " + FormatTime(_mediaDuration);
    }

    private static string FormatTime(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss")
            : value.ToString(@"m\:ss");

    private void PositionSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _sliderDragging = true;
    }

    private void PositionSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isVideo)
            return;
        ResultMedia.Position = TimeSpan.FromSeconds(PositionSlider.Value);
        _sliderDragging = false;
        UpdateMediaPosition();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }
        try { DragMove(); } catch { }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        else if (e.Key == Key.Space && _isVideo)
            PlayPause_Click(sender, e);
    }
}
