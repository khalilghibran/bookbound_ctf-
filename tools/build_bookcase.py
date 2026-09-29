"""Rebuild the bookcase sprite with five equal shelf compartments.

Image models cannot reliably count shelves, and they don't need to: a bookcase is a
repeating structure, so the 5-row version is the existing 4-row sprite's own slices
re-stacked. Same pixels, so the art style matches exactly, and the geometry is exact
instead of approximate - this also prints the ShelfGeometry row rects for free.

    python tools/build_bookcase.py

Measured from the source (612x408): cornice 0-45, boards 9px at y=107/181/254/325,
openings ~65px, base 370-407, interior x 117-495.
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "Assets/Art/ChatGPT_Image_Aug_2__2026__11_42_42_AM-removebg-preview.png"
DST = ROOT / "Assets/Art/bookcase_5row.png"

CORNICE = (0, 46)      # transparent top margin + top cornice
BOARD = (107, 116)     # one shelf board, 9px
OPENING = (116, 181)   # one compartment's back panel, 65px
BASE = (370, 408)      # base plinth + bottom margin

ROWS = 5
SLOTS_PER_ROW = 10
INTERIOR_X0, INTERIOR_X1 = 117, 495

# Openings are stretched from 65px to 70px so that one slot (interior_width/10 = 37.8px)
# against a 70px opening lands at 1 : 1.85 - the ratio the spine art is being redrawn to.
OPENING_H = 70


def slice_of(img, span, height=None):
    part = img.crop((0, span[0], img.width, span[1]))
    if height is not None and part.height != height:
        part = part.resize((img.width, height), Image.LANCZOS)
    return part


def main():
    src = Image.open(SRC).convert("RGBA")
    cornice = slice_of(src, CORNICE)
    board = slice_of(src, BOARD)
    opening = slice_of(src, OPENING, OPENING_H)
    base = slice_of(src, BASE)

    unit_h = opening.height + board.height
    height = cornice.height + ROWS * unit_h + base.height
    out = Image.new("RGBA", (src.width, height), (0, 0, 0, 0))

    y = 0
    out.paste(cornice, (0, y)); y += cornice.height
    opening_tops = []
    for _ in range(ROWS):
        opening_tops.append(y)
        out.paste(opening, (0, y)); y += opening.height
        out.paste(board, (0, y)); y += board.height
    out.paste(base, (0, y))

    out.save(DST)
    print(f"{DST.relative_to(ROOT)}  {out.width} x {out.height}  (aspect {out.width / height:.4f})")

    slot_w = (INTERIOR_X1 - INTERIOR_X0) / SLOTS_PER_ROW
    print(f"slot {slot_w:.1f} x {OPENING_H} px  ->  1 : {OPENING_H / slot_w:.2f}\n")

    print("ShelfGeometry row rects (normalised, bottom-left origin, top row first):")
    x = INTERIOR_X0 / src.width
    w = (INTERIOR_X1 - INTERIOR_X0) / src.width
    for i, top in enumerate(opening_tops):
        y_norm = (height - (top + OPENING_H)) / height
        print(f"  new ShelfGeometry.NormalizedRect({x:.4f}f, {y_norm:.4f}f, "
              f"{w:.4f}f, {OPENING_H / height:.4f}f), // Row {i}")

    # The whole point is five identical compartments - assert it rather than trust the loop.
    pitches = [b - a for a, b in zip(opening_tops, opening_tops[1:])]
    assert len(opening_tops) == ROWS, opening_tops
    assert len(set(pitches)) == 1, f"uneven row pitch: {pitches}"
    print(f"\nOK: {ROWS} compartments, identical {OPENING_H}px openings, even {pitches[0]}px pitch")


if __name__ == "__main__":
    main()
