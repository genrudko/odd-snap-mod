using OddSnap.Capture;
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
