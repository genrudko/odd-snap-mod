from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LIFECYCLE = ROOT / "src/OddSnap/Capture/RegionOverlayForm.Lifecycle.cs"


def main() -> None:
    text = LIFECYCLE.read_text(encoding="utf-8")
    old = """    private void HideToolbarForCaptureTool()
    {
        if (ToolDef.IsCaptureTool(_mode))
            HideToolbarImmediately();
    }
"""
    new = """    private void HideToolbarForCaptureTool()
    {
        // The recording launcher must keep its command bar visible while the user
        // draws the region. Hiding a separate layered ToolbarForm and trying to
        // resurrect it on MouseUp proved unreliable on real multi-monitor systems.
        // Keeping it alive also matches the Windows Snipping Tool interaction model:
        // the bar remains available and simply gains the Start action afterwards.
        if (IsSnippingLauncher && _snippingLauncherMode == SnippingLauncherMode.Recording)
            return;

        if (ToolDef.IsCaptureTool(_mode))
            HideToolbarImmediately();
    }
"""
    if new in text:
        print("Persistent recording launcher toolbar repair already applied.")
        return
    if old not in text:
        raise RuntimeError("HideToolbarForCaptureTool source fragment was not found.")
    LIFECYCLE.write_text(text.replace(old, new, 1), encoding="utf-8")
    print("Persistent recording launcher toolbar repair applied.")


if __name__ == "__main__":
    main()
