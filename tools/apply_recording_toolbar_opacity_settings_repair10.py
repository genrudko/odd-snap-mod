from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP_SETTINGS = ROOT / "src/OddSnap/Models/AppSettings.cs"
RECORDING_FORM = ROOT / "src/OddSnap/Capture/RecordingForm.cs"
RECORDING_TOOLBAR = ROOT / "src/OddSnap/Capture/RecordingToolbarForm.cs"
APP_CAPTURE = ROOT / "src/OddSnap/App/App.Capture.cs"
SETTINGS_XAML = ROOT / "src/OddSnap/UI/SettingsWindow.xaml"
SETTINGS_OPACITY_CODE = ROOT / "src/OddSnap/UI/SettingsWindow.RecordingToolbarOpacity.cs"
OPACITY_TESTS = ROOT / "src/OddSnap.Tests/RecordingToolbarOpacityTests.cs"


def replace_once(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if new in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected fragment was not found in {path}: {old!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    return True


def write_if_changed(path: Path, content: str) -> bool:
    normalized = content.replace("\r\n", "\n")
    if path.exists() and path.read_text(encoding="utf-8").replace("\r\n", "\n") == normalized:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(normalized, encoding="utf-8")
    return True


def main() -> None:
    changed = False

    changed |= replace_once(
        APP_SETTINGS,
        """    public string? DesktopAudioDeviceId { get; set; }

    // Toolbar customization: which tools appear in the dock
""",
        """    public string? DesktopAudioDeviceId { get; set; }
    public bool FadeRecordingToolbarWhenIdle { get; set; } = true;
    public int RecordingToolbarIdleOpacityPercent { get; set; } = 55;

    // Toolbar customization: which tools appear in the dock
""",
    )

    changed |= replace_once(
        RECORDING_FORM,
        """    private readonly bool _showMagnifier;
    private readonly CaptureMagnifierHelper? _magHelper;
""",
        """    private readonly bool _showMagnifier;
    private readonly bool _fadeRecordingToolbarWhenIdle;
    private readonly int _recordingToolbarIdleOpacityPercent;
    private readonly CaptureMagnifierHelper? _magHelper;
""",
    )

    changed |= replace_once(
        RECORDING_FORM,
        """                          bool recordDesktop = false, string? desktopDeviceId = null,
                          bool showMagnifier = false)
""",
        """                          bool recordDesktop = false, string? desktopDeviceId = null,
                          bool showMagnifier = false,
                          bool fadeRecordingToolbarWhenIdle = true,
                          int recordingToolbarIdleOpacityPercent = 55)
""",
    )

    changed |= replace_once(
        RECORDING_FORM,
        """        _desktopDeviceId = desktopDeviceId;
        _showMagnifier = showMagnifier;
        if (_showMagnifier && screenshot is not null)
""",
        """        _desktopDeviceId = desktopDeviceId;
        _showMagnifier = showMagnifier;
        _fadeRecordingToolbarWhenIdle = fadeRecordingToolbarWhenIdle;
        _recordingToolbarIdleOpacityPercent = Math.Clamp(recordingToolbarIdleOpacityPercent, 20, 100);
        if (_showMagnifier && screenshot is not null)
""",
    )

    changed |= replace_once(
        RECORDING_FORM,
        """    protected override CreateParams CreateParams
""",
        """    internal bool FadeRecordingToolbarWhenIdle => _fadeRecordingToolbarWhenIdle;

    internal int RecordingToolbarIdleOpacityPercent => _recordingToolbarIdleOpacityPercent;

    protected override CreateParams CreateParams
""",
    )

    changed |= replace_once(
        APP_CAPTURE,
        """                form = new RecordingForm(selectionScreenshot, bounds, fps, savePath, fmt, maxH,
                    showCursor, recMic, s.MicrophoneDeviceId, recDesktop, s.DesktopAudioDeviceId,
                    _settingsService!.Settings.ShowCaptureMagnifier);
""",
        """                form = new RecordingForm(selectionScreenshot, bounds, fps, savePath, fmt, maxH,
                    showCursor, recMic, s.MicrophoneDeviceId, recDesktop, s.DesktopAudioDeviceId,
                    s.ShowCaptureMagnifier,
                    s.FadeRecordingToolbarWhenIdle,
                    s.RecordingToolbarIdleOpacityPercent);
""",
    )

    changed |= replace_once(
        RECORDING_TOOLBAR,
        """    private const byte ActiveAlpha = 255;
    private const byte IdleAlpha = 140;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(850);
""",
        """    private const byte ActiveAlpha = 255;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(850);
""",
    )

    changed |= replace_once(
        RECORDING_TOOLBAR,
        """    private void UpdateIdleOpacity()
    {
""",
        """    internal static byte ResolveIdleAlpha(bool fadeWhenIdle, int opacityPercent)
    {
        if (!fadeWhenIdle)
            return ActiveAlpha;

        int clampedPercent = Math.Clamp(opacityPercent, 20, 100);
        return (byte)Math.Round(
            ActiveAlpha * (clampedPercent / 100d),
            MidpointRounding.AwayFromZero);
    }

    private void UpdateIdleOpacity()
    {
""",
    )

    changed |= replace_once(
        RECORDING_TOOLBAR,
        """        byte target = _pointerInside || _dragging || DateTime.UtcNow - _lastInteractionUtc < IdleDelay
            ? ActiveAlpha
            : IdleAlpha;
""",
        """        byte target = _pointerInside || _dragging || DateTime.UtcNow - _lastInteractionUtc < IdleDelay
            ? ActiveAlpha
            : ResolveIdleAlpha(
                _owner.FadeRecordingToolbarWhenIdle,
                _owner.RecordingToolbarIdleOpacityPercent);
""",
    )

    changed |= replace_once(
        SETTINGS_XAML,
        """                                <CheckBox x:Name="RecordShowCursorCheck" Grid.Column="2" Content="" FontSize="13" VerticalAlignment="Center"
                                          AutomationProperties.Name="Show cursor in recordings"
                                          ToolTip="Include pointer movement in recorded output."
                                          Checked="RecordShowCursorCheck_Changed" Unchecked="RecordShowCursorCheck_Changed"/>
                            </Grid>
                        </StackPanel>
""",
        """                                <CheckBox x:Name="RecordShowCursorCheck" Grid.Column="2" Content="" FontSize="13" VerticalAlignment="Center"
                                          AutomationProperties.Name="Show cursor in recordings"
                                          ToolTip="Include pointer movement in recorded output."
                                          Checked="RecordShowCursorCheck_Changed" Unchecked="RecordShowCursorCheck_Changed"/>
                            </Grid>
                            <Border Height="1" Background="{DynamicResource ThemeSeparatorBrush}" Margin="0,9,0,9"/>
                            <Grid Style="{StaticResource SettingRow}"
                                  Loaded="RecordingToolbarOpacitySettings_Loaded">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <Border Style="{StaticResource SettingIconFrame}">
                                    <TextBlock Text="&#xE7C9;" Style="{StaticResource SettingIconGlyph}"/>
                                </Border>
                                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                    <TextBlock Text="Fade recording controls when idle" Style="{StaticResource SettingTitle}"/>
                                    <TextBlock Text="Reduce the control panel opacity when the pointer is away." Style="{StaticResource SettingDescription}"/>
                                </StackPanel>
                                <CheckBox x:Name="FadeRecordingToolbarWhenIdleCheck" Grid.Column="2" Content="" FontSize="13" VerticalAlignment="Center"
                                          AutomationProperties.Name="Fade recording controls when idle"
                                          ToolTip="Reduce the control panel opacity when the pointer is away."
                                          Checked="FadeRecordingToolbarWhenIdleCheck_Changed"
                                          Unchecked="FadeRecordingToolbarWhenIdleCheck_Changed"/>
                            </Grid>
                            <Border Height="1" Background="{DynamicResource ThemeSeparatorBrush}" Margin="0,9,0,9"/>
                            <Grid Style="{StaticResource SettingRow}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <Border Style="{StaticResource SettingIconFrame}">
                                    <TextBlock Text="&#xE706;" Style="{StaticResource SettingIconGlyph}"/>
                                </Border>
                                <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                    <TextBlock Text="Idle control opacity" Style="{StaticResource SettingTitle}"/>
                                    <TextBlock Text="Opacity after the pointer leaves the recording controls." Style="{StaticResource SettingDescription}"/>
                                </StackPanel>
                                <StackPanel Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                                    <Slider x:Name="RecordingToolbarIdleOpacitySlider"
                                            Width="150"
                                            Minimum="20"
                                            Maximum="100"
                                            TickFrequency="5"
                                            SmallChange="5"
                                            LargeChange="10"
                                            IsSnapToTickEnabled="True"
                                            IsMoveToPointEnabled="True"
                                            VerticalAlignment="Center"
                                            AutomationProperties.Name="Idle recording control opacity"
                                            ToolTip="Choose the recording control opacity while idle."
                                            ValueChanged="RecordingToolbarIdleOpacitySlider_ValueChanged"/>
                                    <TextBlock x:Name="RecordingToolbarIdleOpacityValueText"
                                               Width="48"
                                               Margin="10,0,0,0"
                                               VerticalAlignment="Center"
                                               TextAlignment="Right"
                                               Foreground="{DynamicResource ThemeTextSecondaryBrush}"
                                               FontFamily="Segoe UI Variable Text"
                                               FontSize="12"/>
                                </StackPanel>
                            </Grid>
                        </StackPanel>
""",
    )

    changed |= write_if_changed(
        SETTINGS_OPACITY_CODE,
        """using System.Windows;
using System.Windows.Controls;

namespace OddSnap.UI;

public partial class SettingsWindow
{
    private bool _suppressRecordingToolbarOpacityPreferenceChange;

    private void RecordingToolbarOpacitySettings_Loaded(object sender, RoutedEventArgs e)
    {
        LoadRecordingToolbarOpacitySettings();
    }

    private void LoadRecordingToolbarOpacitySettings()
    {
        int opacityPercent = Math.Clamp(
            _settingsService.Settings.RecordingToolbarIdleOpacityPercent,
            20,
            100);
        bool fadeWhenIdle = _settingsService.Settings.FadeRecordingToolbarWhenIdle;

        _suppressRecordingToolbarOpacityPreferenceChange = true;
        try
        {
            FadeRecordingToolbarWhenIdleCheck.IsChecked = fadeWhenIdle;
            RecordingToolbarIdleOpacitySlider.Value = opacityPercent;
            RecordingToolbarIdleOpacitySlider.IsEnabled = fadeWhenIdle;
            RecordingToolbarIdleOpacityValueText.Text = $"{opacityPercent}%";
        }
        finally
        {
            _suppressRecordingToolbarOpacityPreferenceChange = false;
        }
    }

    private void FadeRecordingToolbarWhenIdleCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppressRecordingToolbarOpacityPreferenceChange)
            return;

        bool previous = _settingsService.Settings.FadeRecordingToolbarWhenIdle;
        bool current = FadeRecordingToolbarWhenIdleCheck.IsChecked == true;

        SaveRecordingToolbarOpacityPreference(
            "settings.recording-toolbar-fade",
            "Recording control fade",
            previous,
            current,
            value => _settingsService.Settings.FadeRecordingToolbarWhenIdle = value,
            value =>
            {
                FadeRecordingToolbarWhenIdleCheck.IsChecked = value;
                RecordingToolbarIdleOpacitySlider.IsEnabled = value;
            },
            () => RecordingToolbarIdleOpacitySlider.IsEnabled = current);
    }

    private void RecordingToolbarIdleOpacitySlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (RecordingToolbarIdleOpacityValueText is null)
            return;

        int current = Math.Clamp((int)Math.Round(e.NewValue), 20, 100);
        RecordingToolbarIdleOpacityValueText.Text = $"{current}%";

        if (!IsLoaded || _suppressRecordingToolbarOpacityPreferenceChange)
            return;

        int previous = _settingsService.Settings.RecordingToolbarIdleOpacityPercent;
        SaveRecordingToolbarOpacityPreference(
            "settings.recording-toolbar-opacity",
            "Recording control opacity",
            previous,
            current,
            value => _settingsService.Settings.RecordingToolbarIdleOpacityPercent = value,
            value =>
            {
                RecordingToolbarIdleOpacitySlider.Value = Math.Clamp(value, 20, 100);
                RecordingToolbarIdleOpacityValueText.Text = $"{Math.Clamp(value, 20, 100)}%";
            });
    }

    private void SaveRecordingToolbarOpacityPreference<T>(
        string diagnosticKey,
        string label,
        T previous,
        T current,
        Action<T> setValue,
        Action<T> restoreUi,
        Action? applyCurrentUi = null)
    {
        try
        {
            setValue(current);
            applyCurrentUi?.Invoke();
            _settingsService.Save();
            SetRecordingToolbarOpacityStatus(string.Empty);
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogError(diagnosticKey, ex);
            setValue(previous);
            try
            {
                _settingsService.Save();
            }
            catch (Exception rollbackEx)
            {
                AppDiagnostics.LogError($"{diagnosticKey}-rollback", rollbackEx);
            }

            _suppressRecordingToolbarOpacityPreferenceChange = true;
            try
            {
                restoreUi(previous);
            }
            finally
            {
                _suppressRecordingToolbarOpacityPreferenceChange = false;
            }

            SetRecordingToolbarOpacityStatus($"{label} change was not saved. Previous setting restored.");
            ToastWindow.ShowError(
                $"{label} failed",
                $"The previous recording setting was restored. Check Settings -> Recording and try again.\n{ex.Message}");
        }
    }

    private void SetRecordingToolbarOpacityStatus(string message)
    {
        RecordingPreferenceStatusText.Text = message;
        RecordingPreferenceStatusText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
""",
    )

    changed |= write_if_changed(
        OPACITY_TESTS,
        """using OddSnap.Capture;
using OddSnap.Models;
using Xunit;

namespace OddSnap.Tests;

public sealed class RecordingToolbarOpacityTests
{
    [Fact]
    public void AppSettingsUseReadableIdleToolbarDefaults()
    {
        var settings = new AppSettings();

        Assert.True(settings.FadeRecordingToolbarWhenIdle);
        Assert.Equal(55, settings.RecordingToolbarIdleOpacityPercent);
    }

    [Theory]
    [InlineData(false, 20, 255)]
    [InlineData(true, 20, 51)]
    [InlineData(true, 55, 140)]
    [InlineData(true, 100, 255)]
    [InlineData(true, 5, 51)]
    [InlineData(true, 140, 255)]
    public void ResolveIdleAlphaHonorsToggleAndClampsRange(
        bool fadeWhenIdle,
        int opacityPercent,
        byte expectedAlpha)
    {
        Assert.Equal(
            expectedAlpha,
            RecordingToolbarForm.ResolveIdleAlpha(fadeWhenIdle, opacityPercent));
    }
}
""",
    )

    print(
        "Recording toolbar opacity settings repair applied."
        if changed
        else "Recording toolbar opacity settings repair already applied."
    )


if __name__ == "__main__":
    main()
