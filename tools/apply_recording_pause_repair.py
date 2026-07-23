from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GIF_RECORDER = ROOT / "src/OddSnap/Capture/GifRecorder.cs"


def main() -> None:
    text = GIF_RECORDER.read_text(encoding="utf-8")
    obsolete_start_time = "        _startTime = DateTime.UtcNow;\n"

    if obsolete_start_time not in text:
        print("GIF pause repair already applied.")
        return

    GIF_RECORDER.write_text(text.replace(obsolete_start_time, "", 1), encoding="utf-8")
    print("Removed obsolete GIF recorder start-time assignment.")


if __name__ == "__main__":
    main()
