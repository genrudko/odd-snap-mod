from __future__ import annotations

import base64
import subprocess
import tempfile
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PARTS_DIR = ROOT / "tools/snipping_launcher_patch"
MARKER = ROOT / "src/OddSnap/Models/SnippingLauncherMode.cs"


def main() -> None:
    if MARKER.exists():
        print("Snipping launcher stage 1 is already applied.")
        return

    encoded = "".join(
        path.read_text(encoding="utf-8").strip()
        for path in sorted(PARTS_DIR.glob("part*.txt"))
    )
    if not encoded:
        raise RuntimeError("Snipping launcher patch data was not found.")

    patch_bytes = zlib.decompress(base64.b64decode(encoded))
    with tempfile.NamedTemporaryFile(suffix=".patch", delete=False) as patch_file:
        patch_file.write(patch_bytes)
        patch_path = Path(patch_file.name)

    try:
        subprocess.run(
            ["git", "apply", "--whitespace=nowarn", str(patch_path)],
            cwd=ROOT,
            check=True,
        )
    finally:
        patch_path.unlink(missing_ok=True)

    print("Snipping launcher stage 1 applied.")


if __name__ == "__main__":
    main()
