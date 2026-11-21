"""Soften the grill marks on the game food (Aaron, 2026-11-18: "marks feel a bit too strong (see the corn);
make them softer and lighter, like in our mock. Some items don't need grill marks at all").

Reads  Assets/GrillSort/Art/FoodFinal_v2/Grilled/*.png   (never modified)
Writes Assets/GrillSort/Art/FoodFinal_v3/Grilled/*.png   (new set; alpha is unchanged, so Generated/ShadowsFinal still fits)

How:
1. Find the marks: thick dark-brown bands inside the food (thin outlines are removed by a morphological opening).
2. Fill them in with the colour around them (diffusion inpaint), so we know what the food looks like underneath.
3. Paint back a thinner, softer mark: the food's own colour darkened a little with a warm caramel tint, blurred edges.
   Foods in NO_MARKS keep only step 2 (no marks at all).

Usage (repo root): python -I Tools/ArtPipeline/soften_marks.py      OVERWRITE=1 to replace existing v3 files.
Knobs: STRENGTH (how dark the new mark is, 0..1), THIN (px eroded from each side of the old mark).
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "GrillSort", "Art", "FoodFinal_v2", "Grilled")
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "FoodFinal_v3", "Grilled")
FOODS = "bacon beef broccoli chicken corn halloumi meatball mushroom onion pepper pineapple potato salmon sausage shrimp squid tomato zucchini".split()
# no grill marks (like the reference: corn, shrimp, peppers and tomatoes are shown clean)
NO_MARKS = set(os.environ.get("NO_MARKS", "corn shrimp tomato broccoli").split())
STRENGTH = float(os.environ.get("STRENGTH", "0.55"))   # 0 = invisible, 1 = as dark as the old marks
THIN = int(os.environ.get("THIN", "6"))
DARK_LUM_DEFAULT = 68                                 # old marks are lum 25-65; food bodies are brighter
DARK_LUM = {"meatball": 45}                         # per-food override where the food itself is dark
ANGLE = {"meatball": 145}                            # per-food stripe angle where the automatic pick fails


def shift(m, dy, dx):
    o = np.zeros_like(m)
    h, w = m.shape[:2]
    o[max(dy, 0):h + min(dy, 0), max(dx, 0):w + min(dx, 0)] = m[max(-dy, 0):h + min(-dy, 0), max(-dx, 0):w + min(-dx, 0)]
    return o


def erode(m, r):
    for _ in range(r):
        m = m & shift(m, 1, 0) & shift(m, -1, 0) & shift(m, 0, 1) & shift(m, 0, -1)
    return m


def dilate(m, r):
    for _ in range(r):
        m = m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)
    return m


def _line(ang, length):
    t = np.deg2rad(ang)
    pts = {(int(round(k * np.sin(t))), int(round(k * np.cos(t)))) for k in range(-(length // 2), length // 2 + 1)}
    return sorted(pts)


def line_open(m, ang, length):
    """Morphological opening with a straight line: keeps only parts of `m` that contain a `length` px segment at `ang`."""
    pts = _line(ang, length)
    er = m.copy()
    for dy, dx in pts:
        er &= shift(m, dy, dx)
    di = np.zeros_like(m)
    for dy, dx in pts:
        di |= shift(er, -dy, -dx)
    return di & m


def find_marks(dark, inner, name=""):
    """Grill marks = parallel dark bands: long along one angle, at most ~46 px across. Picks the angle per food."""
    core = dilate(erode(dark, 4), 4) & inner          # drop thin lines (outlines, details)
    best = None
    for ang in ([ANGLE[name]] if name in ANGLE else range(0, 180, 5)):
        thick = line_open(core, ang + 90, 48)          # too wide across the band = shading blob, not a mark
        cand = line_open(core & ~dilate(thick, 3), ang, 56)
        score = int(cand.sum())
        if best is None or score > best[0]:
            best = (score, ang, cand)
    _, ang, marks = best
    # stripes broken by highlights / juice drops: extend each found stripe along its own line and take the dark pixels on it
    pts = _line(ang, 140)
    lane = np.zeros_like(marks)
    for dy, dx in pts:
        lane |= shift(erode(marks, 3), dy, dx)
    marks |= dilate(lane, 3) & dark & dilate(inner, 7)   # reach the stripe ends next to the outline
    return marks, ang


def blur(a, r):
    """Blur a 0..1 mask (8-bit precision is plenty for a soft edge)."""
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im).astype(np.float32) / 255


def gauss(a, r):
    """Float gaussian blur (separable), for 2-D or 3-D arrays."""
    k = np.arange(-3 * r, 3 * r + 1, dtype=np.float32)
    k = np.exp(-k * k / (2 * r * r)); k /= k.sum()
    p = len(k) // 2
    def along(x, axis):
        pad = [(0, 0)] * x.ndim; pad[axis] = (p, p)
        x = np.pad(x, pad, mode="edge")
        return sum(k[i] * np.take(x, range(i, i + x.shape[axis] - 2 * p), axis=axis) for i in range(len(k)))
    return along(along(a, 0), 1)


def inpaint(rgb, hole, solid):
    """Fill `hole` with a smooth blend of the food colour around it (normalised blur, fine scale first)."""
    known = (solid & ~hole).astype(np.float32)
    out = rgb.copy()
    todo = hole.copy()
    for r in (5, 9, 16, 28):
        w = gauss(known, r)
        c = gauss(rgb * known[..., None], r) / np.maximum(w, 1e-4)[..., None]
        ok = todo & (w > 0.12)
        out[ok] = c[ok]
        todo &= ~ok
    out[todo] = c[todo]
    return out


def process(name):
    im = np.asarray(Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3], im[..., 3]
    solid = a > 200
    lum = rgb @ np.array([.299, .587, .114], np.float32)
    dark = solid & (lum < DARK_LUM.get(name, DARK_LUM_DEFAULT)) & (rgb[..., 0] >= rgb[..., 2])
    inner = erode(solid, 16)                       # stay off the outline (it is ~12 px thick)
    marks, _ = find_marks(dark, inner, name)
    marks = erode(dilate(marks, 7), 7) & solid      # close the shiny spots inside the old marks
    # the mark's bevel / anti-aliased edge is a lighter brown: take everything clearly darker than the food next to it
    near = dilate(marks, 9) & dilate(inner, 7)
    ref = blur(np.where(near & ~marks, lum, 0) / 255, 12) / np.maximum(blur((near & ~marks).astype(np.float32), 12), 1e-3) * 255
    edge = near & (lum < ref * 0.85)
    hole = dilate(marks | edge, 2) & solid
    base = inpaint(rgb, hole, solid)

    out = rgb.copy()
    soft = blur(hole.astype(np.float32), 1.2)
    out = out * (1 - soft[..., None]) + base * soft[..., None]
    if name not in NO_MARKS:
        core = erode(marks, THIN)
        wgt = blur(core.astype(np.float32), 1.1) * STRENGTH
        tint = base * np.array([.62, .50, .42], np.float32) + np.array([18, 6, 0], np.float32)  # warm caramel, same hue family
        out = out * (1 - wgt[..., None]) + tint * wgt[..., None]
    res = np.dstack([np.clip(out, 0, 255), a]).astype(np.uint8)
    return Image.fromarray(res, "RGBA"), int(marks.sum())


def main():
    os.makedirs(OUT, exist_ok=True)
    over = os.environ.get("OVERWRITE") == "1"
    for n in FOODS:
        dst = os.path.join(OUT, n + ".png")
        if os.path.exists(dst) and not over:
            print("skip (exists)", n); continue
        img, px = process(n)
        img.save(dst)
        print(f"{n:10s} marks {px:6d}px  {'removed' if n in NO_MARKS else 'softened'}")


if __name__ == "__main__":
    main()
