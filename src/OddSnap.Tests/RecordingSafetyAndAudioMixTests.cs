using NAudio.Wave;
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

    [Fact]
    public void SilentWaveIsNotAcceptedAsARecordingAudioSource()
    {
        string path = Path.Combine(Path.GetTempPath(), $"oddsnap-silent-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(path, new WaveFormat(48_000, 16, 1)))
                writer.Write(new byte[48_000 * 2 / 10]);

            Assert.False(VideoRecorder.HasMeaningfulAudio(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void WaveWithMicrophoneLikeSignalIsAcceptedAsARecordingAudioSource()
    {
        string path = Path.Combine(Path.GetTempPath(), $"oddsnap-signal-{Guid.NewGuid():N}.wav");
        try
        {
            var data = new byte[48_000 * 2 / 10];
            for (int i = 0; i + 1 < data.Length; i += 2)
            {
                short sample = (short)(Math.Sin(i / 16d) * 2_000);
                data[i] = (byte)(sample & 0xff);
                data[i + 1] = (byte)((sample >> 8) & 0xff);
            }

            using (var writer = new WaveFileWriter(path, new WaveFormat(48_000, 16, 1)))
                writer.Write(data);

            Assert.True(VideoRecorder.HasMeaningfulAudio(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
