"""Turn the raw generated spines in side-view/non-ready-assets/ into game-ready art.

The generator names its output with UUIDs, so file order says nothing about which book is
which - the sequence number only exists as pixels on the spine's number plate. The maps below
were read off the rendered number plates once; they are the whole reason this script exists.

    python tools/stage_new_spines.py

Reads   Assets/Art/side-view/non-ready-assets/<Genre>/<uuid>.png   (flat chroma background)
Writes  Assets/Art/side-view/ready-assets/<genre>_00N_side.png     (transparent, 256x474)
        Assets/Art/side-view/ready-assets/<genre>_side_spritesheet.png  (2560x474)

Nothing the game currently loads is touched - swapping these in is a separate, deliberate step.
"""
import shutil
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prep_spines import build  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets/Art/side-view/non-ready-assets"
DST = ROOT / "Assets/Art/side-view/ready-assets"

# index into the genre folder's alphabetically sorted files -> sequence number on that book's plate
PLATE_ORDER = {
    "Fantasy":   [6, 9, 8, 4, 1, 2, 5, 3, 7, 10],
    "Science":   [8, 4, 10, 3, 7, 9, 1, 5, 2, 6],
    "History":   [1, 7, 8, 2, 5, 4, 10, 3, 9, 6],
    "Biography": [1, 9, 8, 4, 3, 5, 2, 7, 10, 6],
}


def main():
    DST.mkdir(parents=True, exist_ok=True)

    for folder, order in PLATE_ORDER.items():
        genre = folder.lower()
        files = sorted((SRC / folder).glob("*.png"))
        if len(files) != 10:
            raise SystemExit(f"{folder}: expected 10 files, found {len(files)}")
        if sorted(order) != list(range(1, 11)):
            raise SystemExit(f"{folder}: plate order is not a permutation of 1..10: {order}")

        print(f"\n{folder}")
        with tempfile.TemporaryDirectory() as tmp:
            # Rename into sequence order first; prep_spines assigns cell index by sorted filename.
            for path, number in zip(files, order):
                shutil.copyfile(path, Path(tmp) / f"{genre}_{number:03d}_side.png")
            build(tmp, genre, out_dir=DST)


if __name__ == "__main__":
    main()
