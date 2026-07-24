from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "src/OddSnap/Capture/RegionOverlayForm.Input.Tools.cs"

VALID_SELECTION_OLD = """                    _hasSelection = true;
                    CalcToolbar();
                    PositionToolbarForm();
                    RefreshToolbar();
                    UpdateToolbarSurfaceOnly();
                    Invalidate(InflateForRepaint(_selectionRect, 12));
"""

VALID_SELECTION_NEW = """                    _hasSelection = true;
                    CalcToolbar();
                    // Mouse-down hides the separate layered toolbar while the user drags.
                    // Recreate/show it after mouse-up so the newly-added Start button is usable.
                    EnsureToolbarReady();
                    Invalidate(InflateForRepaint(_selectionRect, 12));
"""

INVALID_SELECTION_OLD = """                    _hasSelection = false;
                    CalcToolbar();
                    PositionToolbarForm();
                    RefreshToolbar();
                    UpdateToolbarSurfaceOnly();
                    Invalidate();
"""

INVALID_SELECTION_NEW = """                    _hasSelection = false;
                    CalcToolbar();
                    EnsureToolbarReady();
                    Invalidate();
"""


def replace_once(text: str, old: str, new: str, label: str) -> tuple[str, bool]:
    if new in text:
        return text, False
    if old not in text:
        raise RuntimeError(f"Expected {label} fragment was not found in {TARGET}.")
    return text.replace(old, new, 1), True


def main() -> None:
    text = TARGET.read_text(encoding="utf-8")
    text, changed_valid = replace_once(
        text,
        VALID_SELECTION_OLD,
        VALID_SELECTION_NEW,
        "valid recording selection",
    )
    text, changed_invalid = replace_once(
        text,
        INVALID_SELECTION_OLD,
        INVALID_SELECTION_NEW,
        "invalid recording selection",
    )

    if changed_valid or changed_invalid:
        TARGET.write_text(text, encoding="utf-8")
        print("Recording launcher toolbar restore repair applied.")
    else:
        print("Recording launcher toolbar restore repair already applied.")


if __name__ == "__main__":
    main()
