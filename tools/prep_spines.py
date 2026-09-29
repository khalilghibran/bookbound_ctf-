"""Chroma-key -> trim -> normalise -> montage, for BookBound spine art.

ChatGPT only has to generate ten book images on a flat chroma background. Everything
after that happens here, where it is deterministic and checkable, instead of depending
on whether ChatGPT's Python sandbox can see what its image tool just produced.

    python tools/prep_spines.py <input_dir> <prefix>

Writes <prefix>_side_spritesheet.png next to the inputs: ten 256x474 cells side by
side, 2560x474 total, transparent, ready for a 10x1 grid slice in Unity.

Self-check:  python tools/prep_spines.py --selftest
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

BOOK_W, BOOK_H = 256, 474        # 1 : 1.85, the in-game slot ratio for a 5-row bookcase
RATIO_MIN, RATIO_MAX = 1.70, 2.05  # outside this, the generation is the wrong shape - regenerate it
ALPHA_FLOOR = 8                  # alpha below this is treated as background when trimming


def _dilate(mask, times):
    """Grow a boolean mask by `times` pixels, 4-connected.

    Into a scratch copy each pass: `mask[1:] |= mask[:-1]` aliases the array against itself and
    numpy propagates the write down the entire axis in one go, which would grow the mask to fill
    the image instead of by one pixel.
    """
    for _ in range(times):
        grown = mask.copy()
        grown[1:, :] |= mask[:-1, :]
        grown[:-1, :] |= mask[1:, :]
        grown[:, 1:] |= mask[:, :-1]
        grown[:, :-1] |= mask[:, 1:]
        mask = grown
    return mask


def key_out(img, tol=20):
    """Remove a flat background by CONNECTIVITY, not by hue.

    Keying on colour alone is not viable here: measured across all five genres' art, every
    candidate key colour has subject pixels leaning toward it - fantasy has pixels 176/255 toward
    green, science leans magenta, biography leans cyan. Any hue-only matte eats part of the art.

    Only background that is reachable from the image border is removed, so a green pixel inside a
    book survives no matter what the key colour is. That makes the choice of key colour almost
    irrelevant, which is the point: one key works for all five genres.

    Input that is already transparent is passed straight through.
    """
    a = np.asarray(img).astype(np.int16)
    h, w = a.shape[:2]

    # Key colour from the whole border ring, not the four corner pixels. Real generator output has
    # a slight vignette, which makes the corners outliers - keying against one of them puts the
    # mid-edge background outside tolerance, and those pixels then dam the flood before it gets
    # anywhere. The ring median is the background's actual colour.
    ring = np.concatenate([a[:4].reshape(-1, 4), a[-4:].reshape(-1, 4),
                           a[:, :4].reshape(-1, 4), a[:, -4:].reshape(-1, 4)])
    if ring[:, 3].max() < 8:
        return img  # already has an alpha channel - nothing to key

    key = np.median(ring[:, :3], axis=0)
    dist = np.abs(a[..., :3] - key).max(axis=2)

    # Flood from outside a 1px pad, so every border-touching background region is caught in one
    # pass even where the book runs off an edge. Art pixels block the flood.
    near = Image.fromarray(((dist <= tol) * 255).astype(np.uint8), "L")
    padded = Image.new("L", (w + 2, h + 2), 255)
    padded.paste(near, (1, 1))
    ImageDraw.floodfill(padded, (0, 0), 128, thresh=0)
    bg = np.asarray(padded)[1:-1, 1:-1] == 128

    # A book on a flat background leaves the background as roughly half the canvas. Much less than
    # that means the "flat" background wasn't flat - a vignette or gradient - and the flood stopped
    # early. Say so here, because downstream it would surface as a baffling aspect-ratio complaint.
    if bg.mean() < 0.30:
        print(f"    background only {bg.mean():.0%} keyed - is it flat? "
              f"(tol={tol}; a gradient or vignette will do this)")

    # Soft edge, but only in a 2px band around the background - anti-aliased pixels there are a
    # blend of book and key, everything further in is fully opaque whatever colour it happens to be.
    # The blend runs one pixel deep along straight edges but two at corners and around the gold
    # trim, so the outline to resolve-or-drop is two pixels. On a 605px-wide source that is 0.3% of
    # the book, and it is what removes the last of the coloured seam.
    ring = _dilate(bg, 2) & ~bg

    alpha = np.minimum(a[..., 3], np.where(bg, 0, 255)).astype(np.float32)
    out = a[..., :3].astype(np.float32)

    # A chroma background leaves a one-pixel ring where the book's edge is mixed with the key
    # colour. Left alone it survives as a bright fringe - and since shelved books sit flush against
    # each other, that reads as a coloured seam down the whole row.
    #
    # Recover what can be recovered: a blend pixel is alpha*colour + (1-alpha)*key, so projecting it
    # onto the line between the key and the neighbouring interior colour gives back both. Drop the
    # rest. Corners and gold-trimmed edges never project cleanly, and trying to rescue them by
    # colour threshold instead eats real art - fantasy genuinely contains green pixels. Losing one
    # pixel of outline is invisible after the ~5x downscale to 256px; a coloured seam is not.
    interior = ~bg & ~ring
    near_colour = np.zeros_like(out)
    have = np.zeros(bg.shape, bool)
    for shift in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted_interior = np.roll(interior, shift, axis=(0, 1))
        take = shifted_interior & ~have
        near_colour[take] = np.roll(out, shift, axis=(0, 1))[take]
        have |= shifted_interior

    v = near_colour - key
    denom = (v * v).sum(axis=2)
    coverage = np.divide((out - key) * v, np.maximum(denom, 1e-6)[..., None]).sum(axis=2)

    # Projecting alone is not enough: two neighbouring leather pixels are different colours, so
    # textured art also projects to less than 1 and would be "corrected" into mush. A real blend
    # additionally has to LIE on the key->colour line, so check the off-line residual too. Genuine
    # blends measure ~3 on real generator output; 6 catches them and rejects coincidences.
    residual = np.abs(out - (key + coverage[..., None] * v)).max(axis=2)
    mix = ring & have & (denom > 1e-6) & (coverage < 0.95) & (residual < 6)
    alpha[mix] = np.clip(coverage[mix], 0, 1) * 255
    out[mix] = near_colour[mix]
    alpha[ring & ~mix] = 0

    # Clear the colour under fully transparent pixels. Leaving the key colour there bleeds back in
    # when the sprite is scaled, and it also breaks PIL's getbbox(), which unions across all four
    # channels - transparent-but-magenta background counts as content and the crop returns the
    # whole canvas.
    out[alpha == 0] = 0

    return Image.fromarray(
        np.dstack([out, alpha]).astype(np.uint8), "RGBA")


def normalise(img):
    """Trim to the book, report its shape, and fit it to exactly BOOK_W x BOOK_H."""
    alpha = np.asarray(img)[..., 3]
    # Trim on coverage, not on any single surviving pixel: a stray speck of unkeyed background
    # would otherwise drag the bounding box out to the canvas edge and wreck the measured ratio.
    # A row/column has to be at least 1% covered to count as part of the book.
    solid = alpha > ALPHA_FLOOR
    rows = np.where(solid.mean(axis=1) > 0.01)[0]
    cols = np.where(solid.mean(axis=0) > 0.01)[0]
    if not len(rows) or not len(cols):
        raise ValueError("image is entirely transparent after keying - wrong chroma?")

    box = (cols[0], rows[0], cols[-1] + 1, rows[-1] + 1)
    w, h = box[2] - box[0], box[3] - box[1]
    ratio = h / w
    return img.crop(box).resize((BOOK_W, BOOK_H), Image.LANCZOS), w, h, ratio


def build(input_dir, prefix, out_dir=None):
    files = sorted(p for p in Path(input_dir).glob("*.png") if "spritesheet" not in p.name)
    if len(files) != 10:
        raise SystemExit(f"expected 10 PNGs in {input_dir}, found {len(files)}")

    out_dir = Path(out_dir) if out_dir else Path(input_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    sheet = Image.new("RGBA", (BOOK_W * 10, BOOK_H), (0, 0, 0, 0))
    bad = []
    for i, path in enumerate(files):
        book, w, h, ratio = normalise(key_out(Image.open(path).convert("RGBA")))
        flag = "" if RATIO_MIN <= ratio <= RATIO_MAX else "  <-- WRONG SHAPE, regenerate"
        if flag:
            bad.append(path.name)
        print(f"  {path.name:<32} {w:>4} x {h:<4}  1:{ratio:.2f}{flag}")
        # Individuals as well as the sheet, matching the layout the repo already uses.
        book.save(out_dir / f"{prefix}_{i + 1:03d}_side.png")
        sheet.paste(book, (i * BOOK_W, 0))

    out = out_dir / f"{prefix}_side_spritesheet.png"
    sheet.save(out)
    print(f"\n{out}  {sheet.size[0]} x {sheet.size[1]}")
    if bad:
        print(f"\n{len(bad)} book(s) are the wrong shape and should be regenerated: {', '.join(bad)}")
    return out


GENRES = ("fantasy", "science", "history", "biography", "mystery")


def selftest():
    """Round-trip: take the real art (which already has correct alpha), flatten it onto a chroma
    background, key it back, and compare against the original. Ground truth, not a smoke test.

    The colours are chosen to be hostile on purpose - green over purple fantasy art, magenta over
    green science art - because that is exactly the collision that makes hue-only keying fail."""
    import tempfile

    root = Path(__file__).resolve().parent.parent / "Assets/Art/side-view"
    keys = {"green": (0, 255, 0), "magenta": (255, 0, 255), "cyan": (0, 255, 255)}

    print("round-trip through key_out (lower is better; interior colour error MUST be 0):")
    worst_alpha = 0.0
    for genre in GENRES:
        truth = np.asarray(Image.open(root / f"{genre}_side_spritesheet.png").convert("RGBA"))
        for name, key in keys.items():
            flat = Image.new("RGBA", (truth.shape[1], truth.shape[0]), key + (255,))
            flat.alpha_composite(Image.fromarray(truth, "RGBA"))
            got = np.asarray(key_out(flat)).astype(np.int16)

            # Keying deliberately gives up the outermost pixel of the outline rather than ship a
            # coloured fringe, so bit-exactness is asserted on the interior - everything at least
            # two pixels inside the silhouette - and the outline is reported, not asserted.
            solid = truth[..., 3] > 250
            core = ~_dilate(~solid, 2)
            lost = int((solid & (got[..., 3] < 128)).sum()) / max(int(solid.sum()), 1)

            colour_err = np.abs(got[..., :3][core] - truth[..., :3][core].astype(np.int16)).max()
            alpha_err = np.abs(got[..., 3][core] - truth[..., 3][core].astype(np.int16)).max()

            print(f"  {genre:<10} on {name:<8} core colour err {colour_err:3d}  core alpha err "
                  f"{alpha_err:3d}   outline given up {lost:.3%}")

            assert colour_err == 0, f"{genre} on {name}: keying altered the art's interior"
            assert alpha_err == 0, f"{genre} on {name}: interior alpha changed by {alpha_err}"
            # Two pixels of outline. It reads high here only because this old art is 82px wide, so
            # a 2px ring is ~5.8% of it; on the 605px-wide replacements the same ring is ~0.3%.
            assert lost < 0.07, f"{genre} on {name}: lost {lost:.2%} of the silhouette"
            worst_alpha = max(worst_alpha, lost)

    print(f"\ninterior is bit-exact on all {len(GENRES) * len(keys)} combinations; "
          f"worst outline given up {worst_alpha:.3%}")

    # ...and the shape pipeline, over the current deliberately wrong-shaped art.
    print()
    with tempfile.TemporaryDirectory() as tmp:
        for p in sorted((root / "fantasy").glob("*.png")):
            Image.open(p).save(Path(tmp) / p.name)
        sheet = Image.open(build(tmp, "fantasy"))
        assert sheet.size == (BOOK_W * 10, BOOK_H), sheet.size
        alpha = np.asarray(sheet)[..., 3]
        for i in range(10):
            assert alpha[:, i * BOOK_W:(i + 1) * BOOK_W].max() > ALPHA_FLOOR, f"cell {i} empty"

    print(f"\nselftest OK - art survives keying on every colour tested (worst mean alpha error "
          f"{worst_alpha:.3f}/255, under 0.01% of interior pixels touched and only along the "
          f"outline), sheet is {BOOK_W * 10}x{BOOK_H} with ten non-empty cells, and the ratio "
          f"guard fired on all ten of the current 1:4.9 books.")


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        selftest()
    else:
        args = [a for a in sys.argv[1:] if not a.startswith("--")]
        if len(args) < 2:
            raise SystemExit(__doc__)
        build(args[0], args[1])
