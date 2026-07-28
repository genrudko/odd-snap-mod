from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/OddSnap/Capture/VideoRecorder.cs"
text = path.read_text(encoding="utf-8").replace("\r\n", "\n")

if "const string normalizeAudio" in text:
    print("Dual-audio mux arguments already normalized.")
else:
    old = r'''        return $"-y -i \"{videoPath}\" -i \"{audioFiles[0]}\" -i \"{audioFiles[1]}\" " +
               $"-filter_complex \"[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0,apad,atrim=0:{duration}[a]\" " +
               $"-c:v copy -c:a {audioCodec} -map 0:v -map \"[a]\"{muxerArgs} \"{tempOut}\"";
'''
    new = r'''        const string normalizeAudio = "aresample=48000:async=1:first_pts=0,aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo";
        return $"-y -i \"{videoPath}\" -i \"{audioFiles[0]}\" -i \"{audioFiles[1]}\" " +
               $"-filter_complex \"[1:a]{normalizeAudio},volume=0.75[desktop];" +
               $"[2:a]{normalizeAudio},volume=0.75[mic];" +
               $"[desktop][mic]amix=inputs=2:duration=longest:dropout_transition=0:normalize=0," +
               $"alimiter=limit=0.95,apad,atrim=0:{duration}[a]\" " +
               $"-c:v copy -c:a {audioCodec} -map 0:v -map \"[a]\"{muxerArgs} \"{tempOut}\"";
'''
    if old not in text:
        raise RuntimeError("Dual-audio BuildMuxArguments block was not found")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")
    print("Dual-audio mux arguments normalized.")
