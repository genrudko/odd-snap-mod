from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/OddSnap/UI/SnippingResultWindow.xaml.cs"
OLD = "private void Window_KeyDown(object sender, KeyEventArgs e)"
NEW = "private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)"


def main() -> None:
    if not TARGET.exists():
        raise RuntimeError("SnippingResultWindow.xaml.cs is missing; apply stage 1 first.")

    text = TARGET.read_text(encoding="utf-8")
    if NEW in text:
        print("Snipping launcher repair 1 is already applied.")
        return
    if OLD not in text:
        raise RuntimeError("Expected ambiguous KeyEventArgs signature was not found.")

    TARGET.write_text(text.replace(OLD, NEW, 1), encoding="utf-8")
    print("Snipping launcher repair 1 applied.")


if __name__ == "__main__":
    main()
