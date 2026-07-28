using OddSnap.Capture;
using Xunit;

namespace OddSnap.Tests;

public sealed class RecordingSafetyAndAudioMixTests
{
    [Fact]
    public void DiscardRequiresASecondClickInsideTheConfirmationWindow()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Arm,
            RecordingToolbarForm.ResolveDiscardClick(now, DateTime.MinValue));
        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Discard,
            RecordingToolbarForm.ResolveDiscardClick(now, now.AddSeconds(1)));
        Assert.Equal(
            RecordingToolbarForm.DiscardClickDecision.Arm,
            RecordingToolbarForm.ResolveDiscardClick(now, now.AddMilliseconds(-1)));
    }

    [Fact]
    public void DualAudioMuxNormalizesBothInputsBeforeMixing()
    {
        string args = VideoRecorder.BuildMuxArguments(
            "video.mp4",
            new[] { "desktop.wav", "mic.wav" },
            "muxed.mp4",
            "aac",
            12.5);

        Assert.Contains("[1:a]aresample=48000:async=1:first_pts=0", args);
        Assert.Contains("[2:a]aresample=48000:async=1:first_pts=0", args);
        Assert.Contains("sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo", args);
        Assert.Contains("[desktop][mic]amix=inputs=2", args);
        Assert.Contains("normalize=0", args);
        Assert.Contains("alimiter=limit=0.95", args);
        Assert.Contains("-map 0:v -map \"[a]\"", args);
    }

    [Fact]
    public void SingleAudioMuxStillUsesTheDirectSafePath()
    {
        string args = VideoRecorder.BuildMuxArguments(
            "video.mp4",
            new[] { "desktop.wav" },
            "muxed.mp4",
            "aac",
            8);

        Assert.DoesNotContain("amix=", args);
        Assert.Contains("[1:a]apad,atrim=0:8[a]", args);
        Assert.Contains("-map 0:v -map \"[a]\"", args);
    }
}
