from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/OddSnap/UI/SnippingResultWindow.xaml.cs"
OLD = "var dialog = new SaveFileDialog"
NEW = "var dialog = new Microsoft.Win32.SaveFileDialog"


def main() -> None:
    if not TARGET.exists():
        raise RuntimeError("SnippingResultWindow.xaml.cs is missing; apply stage 1 first.")

    text = TARGET.read_text(encoding="utf-8")
    if NEW in text:
        print("Snipping launcher repair 2 is already applied.")
        return
    if OLD not in text:
        raise RuntimeError("Expected ambiguous SaveFileDialog construction was not found.")

    TARGET.write_text(text.replace(OLD, NEW, 1), encoding="utf-8")
    print("Snipping launcher repair 2 applied.")


if __name__ == "__main__":
    main()
