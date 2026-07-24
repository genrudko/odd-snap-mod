from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP_SETTINGS = ROOT / "src/OddSnap/Models/AppSettings.cs"
RECORDING_FORM = ROOT / "src/OddSnap/Capture/RecordingForm.cs"
RECORDING_TOOLBAR = ROOT / "src/OddSnap/Capture/RecordingToolbarForm.cs"
APP_CAPTURE = ROOT / "src/OddSnap/App/App.Capture.cs"
SETTINGS_XAML = ROOT / "src/OddSnap/UI/SettingsWindow.xaml"
SETTINGS_CODE = ROOT / "src/OddSnap/UI/SettingsWindow.RecordingToolbarOpacity.cs"
TESTS = ROOT / "src/OddSnap.Tests/RecordingToolbarOpacityTests.cs"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8").replace("\r\n", "\n")


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8")


def insert_after(path: Path, marker: str, addition: str, sentinel: str) -> bool:
    text = read(path)
    if sentinel in text:
        return False
    index = text.find(marker)
    if index < 0:
        raise RuntimeError(f"Marker not found in {path}: {marker!r}")
    index += len(marker)
    write(path, text[:index] + addition + text[index:])
    return True


def replace_required(path: Path, old: str, new: str, sentinel: str) -> bool:
    text = read(path)
    if sentinel in text:
        return False
    if old not in text:
        raise RuntimeError(f"Fragment not found in {path}: {old!r}")
    write(path, text.replace(old, new, 1))
    return True


def write_if_changed(path: Path, content: str) -> bool:
    normalized = content.replace("\r\n", "\n")
    if path.exists() and read(path) == normalized:
        return False
    write(path, normalized)
    return True


def patch_xaml() -> bool:
    text = read(SETTINGS_XAML)
    if "FadeRecordingToolbarWhenIdleCheck" in text:
        return False

    anchor = 'Checked="RecordShowCursorCheck_Changed" Unchecked="RecordShowCursorCheck_Changed"/>'
    anchor_end = text.find(anchor)
    if anchor_end < 0:
        raise RuntimeError("RecordShowCursorCheck anchor not found in SettingsWindow.xaml")
    anchor_end += len(anchor)

    close_match = re.search(r"\n(?P<grid>\s*</Grid>)\n(?P<stack>\s*</StackPanel>)", text[anchor_end:])
    if close_match is None:
        raise RuntimeError("Recording settings card closing tags were not found")

    close_start = anchor_end + close_match.start()
    close_end = anchor_end + close_match.end()
    grid_close = close_match.group("grid")
    stack_close = close_match.group("stack")

    rows = """
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
                                            Width="150" Minimum="20" Maximum="100"
                                            TickFrequency="5" SmallChange="5" LargeChange="10"
                                            IsSnapToTickEnabled="True" IsMoveToPointEnabled="True"
                                            VerticalAlignment="Center"
                                            AutomationProperties.Name="Idle recording control opacity"
                                            ToolTip="Choose the recording control opacity while idle."
                                            ValueChanged="RecordingToolbarIdleOpacitySlider_ValueChanged"/>
                                    <TextBlock x:Name="RecordingToolbarIdleOpacityValueText"
                                               Width="48" Margin="10,0,0,0"
                                               VerticalAlignment="Center" TextAlignment="Right"
                                               Foreground="{DynamicResource ThemeTextSecondaryBrush}"
                                               FontFamily="Segoe UI Variable Text" FontSize="12"/>
                                </StackPanel>
                            </Grid>"""

    replacement = f"\n{grid_close}\n{rows}\n{stack_close}"
    write(SETTINGS_XAML, text[:close_start] + replacement + text[close_end:])
    return True


def main() -> None:
    changed = False

    changed |= insert_after(
        APP_SETTINGS,
        "    public string? DesktopAudioDeviceId { get; set; }",
        "\n    public bool FadeRecordingToolbarWhenIdle { get; set; } = true;"
        "\n    public int RecordingToolbarIdleOpacityPercent { get; set; } = 55;",
        "FadeRecordingToolbarWhenIdle",
    )

    changed |= insert_after(
        RECORDING_FORM,
        "    private readonly bool _showMagnifier;",
        "\n    private readonly bool _fadeRecordingToolbarWhenIdle;"
        "\n    private readonly int _recordingToolbarIdleOpacityPercent;",
        "_fadeRecordingToolbarWhenIdle",
    )

    changed |= replace_required(
        RECORDING_FORM,
        "                          bool showMagnifier = false)",
        "                          bool showMagnifier = false,\n"
        "                          bool fadeRecordingToolbarWhenIdle = true,\n"
        "                          int recordingToolbarIdleOpacityPercent = 55)",
        "int recordingToolbarIdleOpacityPercent = 55)",
    )

    changed |= insert_after(
        RECORDING_FORM,
        "        _showMagnifier = showMagnifier;",
        "\n        _fadeRecordingToolbarWhenIdle = fadeRecordingToolbarWhenIdle;"
        "\n        _recordingToolbarIdleOpacityPercent = Math.Clamp(recordingToolbarIdleOpacityPercent, 20, 100);",
        "_recordingToolbarIdleOpacityPercent = Math.Clamp",
    )

    changed |= insert_after(
        RECORDING_FORM,
        "    }\n\n    protected override CreateParams CreateParams",
        "\n\n    internal bool FadeRecordingToolbarWhenIdle => _fadeRecordingToolbarWhenIdle;"
        "\n\n    internal int RecordingToolbarIdleOpacityPercent => _recordingToolbarIdleOpacityPercent;",
        "internal bool FadeRecordingToolbarWhenIdle",
    )

    changed |= replace_required(
        APP_CAPTURE,
        "                    _settingsService!.Settings.ShowCaptureMagnifier);",
        "                    s.ShowCaptureMagnifier,\n"
        "                    s.FadeRecordingToolbarWhenIdle,\n"
        "                    s.RecordingToolbarIdleOpacityPercent);",
        "s.RecordingToolbarIdleOpacityPercent);",
    )

    toolbar_text = read(RECORDING_TOOLBAR)
    if "private const byte IdleAlpha = 140;\n" in toolbar_text:
        toolbar_text = toolbar_text.replace("    private const byte IdleAlpha = 140;\n", "", 1)
        write(RECORDING_TOOLBAR, toolbar_text)
        changed = True

    changed |= insert_after(
        RECORDING_TOOLBAR,
        "    private void UpdateIdleOpacity()",
        """    internal static byte ResolveIdleAlpha(bool fadeWhenIdle, int opacityPercent)
    {
        if (!fadeWhenIdle)
            return ActiveAlpha;

        int clampedPercent = Math.Clamp(opacityPercent, 20, 100);
        return (byte)Math.Round(
            ActiveAlpha * (clampedPercent / 100d),
            MidpointRounding.AwayFromZero);
    }

""",
        "internal static byte ResolveIdleAlpha",
    )

    changed |= replace_required(
        RECORDING_TOOLBAR,
        "            : IdleAlpha;",
        "            : ResolveIdleAlpha(\n"
        "                _owner.FadeRecordingToolbarWhenIdle,\n"
        "                _owner.RecordingToolbarIdleOpacityPercent);",
        "_owner.RecordingToolbarIdleOpacityPercent);",
    )

    changed |= patch_xaml()

    changed |= write_if_changed(
        SETTINGS_CODE,
        """using System.Windows;
using System.Windows.Controls;

namespace OddSnap.UI;

public partial class SettingsWindow
{
    private bool _suppressRecordingToolbarOpacityPreferenceChange;

    private void RecordingToolbarOpacitySettings_Loaded(object sender, RoutedEventArgs e) =>
        LoadRecordingToolbarOpacitySettings();

    private void LoadRecordingToolbarOpacitySettings()
    {
        int opacityPercent = Math.Clamp(
            _settingsService.Settings.RecordingToolbarIdleOpacityPercent, 20, 100);
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
                int restored = Math.Clamp(value, 20, 100);
                RecordingToolbarIdleOpacitySlider.Value = restored;
                RecordingToolbarIdleOpacityValueText.Text = $"{restored}%";
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
            try { _settingsService.Save(); }
            catch (Exception rollbackEx) { AppDiagnostics.LogError($"{diagnosticKey}-rollback", rollbackEx); }

            _suppressRecordingToolbarOpacityPreferenceChange = true;
            try { restoreUi(previous); }
            finally { _suppressRecordingToolbarOpacityPreferenceChange = false; }

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
        TESTS,
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
        bool fadeWhenIdle, int opacityPercent, byte expectedAlpha)
    {
        Assert.Equal(
            expectedAlpha,
            RecordingToolbarForm.ResolveIdleAlpha(fadeWhenIdle, opacityPercent));
    }
}
""",
    )

    print("Recording toolbar opacity settings repair applied." if changed
          else "Recording toolbar opacity settings repair already applied.")


if __name__ == "__main__":
    main()
