from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/OddSnap/Capture/RegionOverlayForm.Input.Tools.cs"

OLD = """                if (isRecordingLauncher && _selectionRect.Width > 2 && _selectionRect.Height > 2)
                {
                    _autoDetectRect = Rectangle.Empty;
                    _autoDetectActive = false;
                    _hasSelection = true;
                    RefreshToolbar();
                    Invalidate(InflateForRepaint(_selectionRect, 12));
                }
                else if (isRecordingLauncher)
                {
                    _hasSelection = false;
                    Invalidate();
                }
"""

NEW = """                if (isRecordingLauncher && _selectionRect.Width > 2 && _selectionRect.Height > 2)
                {
                    _autoDetectRect = Rectangle.Empty;
                    _autoDetectActive = false;
                    _hasSelection = true;
                    CalcToolbar();
                    PositionToolbarForm();
                    RefreshToolbar();
                    UpdateToolbarSurfaceOnly();
                    Invalidate(InflateForRepaint(_selectionRect, 12));
                }
                else if (isRecordingLauncher)
                {
                    _hasSelection = false;
                    CalcToolbar();
                    PositionToolbarForm();
                    RefreshToolbar();
                    UpdateToolbarSurfaceOnly();
                    Invalidate();
                }
"""


def main() -> None:
    text = TARGET.read_text(encoding="utf-8")
    if NEW in text:
        print("Snipping launcher recording toolbar repair already applied.")
        return
    if OLD not in text:
        raise RuntimeError("Expected recording launcher selection block was not found.")
    TARGET.write_text(text.replace(OLD, NEW, 1), encoding="utf-8")
    print("Snipping launcher recording toolbar repair applied.")


if __name__ == "__main__":
    main()
