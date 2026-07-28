from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VIDEO_RECORDER = ROOT / "src/OddSnap/Capture/VideoRecorder.cs"
TESTS = ROOT / "src/OddSnap.Tests/RecordingSafetyAndAudioMixTests.cs"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8").replace("\r\n", "\n")


def write(path: Path, text: str) -> None:
    path.write_text(text.replace("\r\n", "\n"), encoding="utf-8")


def replace_once(path: Path, old: str, new: str, sentinel: str) -> bool:
    text = read(path)
    if sentinel in text:
        return False
    if old not in text:
        raise RuntimeError(f"Expected fragment not found in {path}: {old!r}")
    write(path, text.replace(old, new, 1))
    return True


def main() -> None:
    changed = False

    changed |= replace_once(
        VIDEO_RECORDER,
        "    private WaveInEvent? _micCapture;\n",
        "    private WasapiCapture? _micCapture;\n",
        "private WasapiCapture? _micCapture;",
    )

    old_mic = '''    private void StartMicrophoneAudioCapture(string dir, string outputPath)
    {
        string wavPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(outputPath) + "_mic.wav");
        WaveInEvent? capture = null;
        WaveFileWriter? writer = null;
        EventHandler<WaveInEventArgs>? dataAvailableHandler = null;
        bool started = false;

        try
        {
            int micDevice = ResolveMicDeviceNumber(_micDeviceId);
            capture = new WaveInEvent
            {
                DeviceNumber = micDevice,
                WaveFormat = new WaveFormat(44100, 16, 1)
            };
            writer = new WaveFileWriter(wavPath, capture.WaveFormat);
            dataAvailableHandler = (_, e) =>
            {
                try
                {
                    if (!_pauseClock.IsPaused)
                        writer?.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch { }
            };
            capture.DataAvailable += dataAvailableHandler;
            capture.StartRecording();

            _micWavPath = wavPath;
            _micCapture = capture;
            _micWriter = writer;
            _micDataAvailableHandler = dataAvailableHandler;
            started = true;
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogWarning(
                "recording.audio-start",
                $"Microphone audio capture did not start: {ex.Message}",
                ex);
        }
        finally
        {
            if (!started)
            {
                if (capture is not null && dataAvailableHandler is not null)
                {
                    try { capture.DataAvailable -= dataAvailableHandler; } catch { }
                }

                try { writer?.Dispose(); } catch { }
                try { capture?.Dispose(); } catch { }
                TryDeleteRecordingTempFile(wavPath, "failed microphone audio startup");
            }
        }
    }

    private static int ResolveMicDeviceNumber(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId)) return 0;
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            if (caps.ProductName.Contains(deviceId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }
'''

    new_mic = '''    private void StartMicrophoneAudioCapture(string dir, string outputPath)
    {
        string wavPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(outputPath) + "_mic.wav");
        WasapiCapture? capture = null;
        WaveFileWriter? writer = null;
        EventHandler<WaveInEventArgs>? dataAvailableHandler = null;
        bool started = false;

        try
        {
            capture = CreateMicrophoneCapture(_micDeviceId);
            writer = new WaveFileWriter(wavPath, capture.WaveFormat);
            dataAvailableHandler = (_, e) =>
            {
                try
                {
                    if (!_pauseClock.IsPaused)
                        writer?.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch { }
            };
            capture.DataAvailable += dataAvailableHandler;
            capture.StartRecording();

            _micWavPath = wavPath;
            _micCapture = capture;
            _micWriter = writer;
            _micDataAvailableHandler = dataAvailableHandler;
            started = true;

            AppDiagnostics.LogInfo(
                "recording.audio-start",
                $"Microphone capture started through WASAPI: {capture.WaveFormat}.");
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogWarning(
                "recording.audio-start",
                $"Microphone audio capture did not start: {ex.Message}",
                ex);
        }
        finally
        {
            if (!started)
            {
                if (capture is not null && dataAvailableHandler is not null)
                {
                    try { capture.DataAvailable -= dataAvailableHandler; } catch { }
                }

                try { writer?.Dispose(); } catch { }
                try { capture?.Dispose(); } catch { }
                TryDeleteRecordingTempFile(wavPath, "failed microphone audio startup");
            }
        }
    }

    private static WasapiCapture CreateMicrophoneCapture(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return new WasapiCapture();

        using var enumerator = new MMDeviceEnumerator();
        try
        {
            return new WasapiCapture(enumerator.GetDevice(deviceId));
        }
        catch (Exception exactMatchError)
        {
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var device in devices)
            {
                if (string.Equals(device.ID, deviceId, StringComparison.OrdinalIgnoreCase) ||
                    device.FriendlyName.Contains(deviceId, StringComparison.OrdinalIgnoreCase) ||
                    deviceId.Contains(device.FriendlyName, StringComparison.OrdinalIgnoreCase))
                {
                    AppDiagnostics.LogWarning(
                        "recording.audio-device",
                        $"Microphone endpoint was resolved by friendly name after exact ID lookup failed: {device.FriendlyName}. " +
                        exactMatchError.Message);
                    return new WasapiCapture(device);
                }
            }

            AppDiagnostics.LogWarning(
                "recording.audio-device",
                $"Configured microphone endpoint was not found; using the default capture endpoint. {exactMatchError.Message}");
            return new WasapiCapture();
        }
    }
'''

    changed |= replace_once(
        VIDEO_RECORDER,
        old_mic,
        new_mic,
        "private static WasapiCapture CreateMicrophoneCapture",
    )

    old_signal = '''    private static bool HasMeaningfulAudio(string path)
    {
        try { return File.Exists(path) && new FileInfo(path).Length > 44; }
        catch { return false; }
    }
'''

    new_signal = '''    internal static bool HasMeaningfulAudio(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= 44)
                return false;

            using var reader = new AudioFileReader(path);
            var samples = new float[8192];
            int read;
            while ((read = reader.Read(samples, 0, samples.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    if (Math.Abs(samples[i]) >= 0.00001f)
                        return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            AppDiagnostics.LogWarning(
                "recording.audio-signal-check",
                $"Could not inspect captured audio {Path.GetFileName(path)}: {ex.Message}",
                ex);
            return false;
        }
    }
'''

    changed |= replace_once(
        VIDEO_RECORDER,
        old_signal,
        new_signal,
        "recording.audio-signal-check",
    )

    tests = read(TESTS)
    if "SilentWaveIsNotAcceptedAsARecordingAudioSource" not in tests:
        tests = tests.replace(
            "using OddSnap.Capture;\n",
            "using NAudio.Wave;\nusing OddSnap.Capture;\n",
            1,
        )
        insertion = '''
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
'''
        closing = tests.rfind("}\n")
        if closing < 0:
            raise RuntimeError("Test class closing brace not found")
        tests = tests[:closing] + insertion + tests[closing:]
        write(TESTS, tests)
        changed = True

    print("WASAPI microphone and signal-aware audio repair applied." if changed else "WASAPI microphone and signal-aware audio repair already applied.")


if __name__ == "__main__":
    main()
