"""Cut the cartoon food / people art (ArtSource/Cartoon/single/*.png, one 1024 image per item) into game sprites.

Usage (from the repo root):  python -I Tools/ArtPipeline/cut_cartoon.py
Needs Pillow + numpy only. Set OVERWRITE=1 to replace existing sprites.

- White background removed by growing the white region from the image border.
- Enclosed background gaps (e.g. a ring's centre) must be listed in HOLES; there is no automatic hole finder.
- People: busts are cut straight across, so white clothing touching the cut line is kept.
- Stray specks are dropped (keeps the biggest blob + big parts).
- Output: 512x512, centred, ~3% padding (same as the old Food/ and Customers/ sprites).
The first 2x2 sheets (ArtSource/Cartoon/sheet_*.png) were the style test; the singles replaced them for quality.
"""
import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Cartoon")
SINGLE = os.path.join(SRC, "single")
FOOD_OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "FoodCartoon", "Grilled")
PEOPLE_OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "CustomersPeople")

FOODS = "bacon beef broccoli chicken corn halloumi meatball mushroom onion pepper pineapple potato salmon sausage shrimp squid tomato zucchini".split()
PEOPLE = "grandpa ponytail redhead curly dad grandma pigtails quiff".split()
# enclosed background gaps the automatic cut can't tell from white clothing: (x, y) in the 1024 source
HOLES = {}
SIZE, PAD = 512, 0.03


def grow(seed, allowed):
    reg = seed & allowed
    while True:
        g = reg.copy()
        g[1:, :] |= reg[:-1, :]; g[:-1, :] |= reg[1:, :]; g[:, 1:] |= reg[:, :-1]; g[:, :-1] |= reg[:, 1:]
        g &= allowed
        if (g == reg).all():
            return reg
        reg = g


def components(mask):
    """Label 4-connected components; returns list of (area, pixel index arrays)."""
    h, w = mask.shape
    seen = np.zeros_like(mask)
    out = []
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; pts = []
        while q:
            y, x = q.popleft(); pts.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True; q.append((ny, nx))
        out.append(pts)
    return out


def cut(cell, food, holes=()):
    a = np.asarray(cell.convert("RGB")).astype(np.int16)
    white = a.min(axis=2) > 232
    border = np.zeros_like(white)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    if food:
        bg = grow(border, white)
    else:
        # busts are cut straight across: everything below the cut line is background, and white
        # clothing touching the cut line must not be flooded from below
        rows = np.nonzero((~white).sum(axis=1) > 40)[0]
        yb = rows.max() if len(rows) else white.shape[0] - 1
        allowed = white.copy(); allowed[yb:, :] = False
        border[yb:, :] = False
        bg = grow(border, allowed)
        bg[yb + 1:, :] = True
    for x, y in holes:
        seed = np.zeros_like(white); seed[y, x] = True
        bg |= grow(seed, white)
    if False:  # automatic hole finder: off (it ate white highlights); list real holes in HOLES instead
        pure = (a.min(axis=2) > 244) & ~bg
        for pts in components(pure[::2, ::2]):
            if len(pts) > 600:  # in half-res pixels
                ys, xs = np.array(pts).T * 2
                seed = np.zeros_like(white); seed[ys, xs] = True
                bg |= grow(seed, white)
    fg = ~bg
    # drop leaked neighbour bits: keep blobs at least 4% of the biggest
    small = fg[::2, ::2]
    comps = sorted(components(small), key=len, reverse=True)
    keep = np.zeros_like(small)
    for pts in comps:
        if len(pts) >= 0.04 * len(comps[0]):
            ys, xs = np.array(pts).T; keep[ys, xs] = True
    keep = np.repeat(np.repeat(keep, 2, axis=0), 2, axis=1)[: fg.shape[0], : fg.shape[1]]
    fg &= np.asarray(Image.fromarray(keep.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(5))) > 0
    alpha = Image.fromarray(fg.astype(np.uint8) * 255)
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    # un-premultiply the white fringe on soft edges
    rgb = a.astype(np.float32)
    al = np.asarray(alpha).astype(np.float32)[..., None] / 255.0
    rgb = np.where(al > 0.05, (rgb - 255 * (1 - al)) / np.maximum(al, 0.05), rgb)
    out = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert("RGBA")
    out.putalpha(alpha)
    return out.crop(out.getbbox())


def place(item):
    inner = int(SIZE * (1 - 2 * PAD))
    s = inner / max(item.size)
    item = item.resize((max(1, round(item.width * s)), max(1, round(item.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(item, ((SIZE - item.width) // 2, (SIZE - item.height) // 2))
    return canvas


def run(names, prefix, outdir, food):
    os.makedirs(outdir, exist_ok=True)
    for name in names:
        path = os.path.join(outdir, name + ".png")
        if os.path.exists(path) and os.environ.get("OVERWRITE") != "1":
            print("skip (exists)", path); continue
        src = Image.open(os.path.join(SINGLE, f"{prefix}_{name}.png"))
        place(cut(src, food, HOLES.get(name, ()))).save(path)
        print("wrote", os.path.relpath(path, ROOT))


def bake_shadows():
    """v3 deep shadow, same recipe as bake_game_fx.py, into Generated/ShadowsCartoon/."""
    out = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated", "ShadowsCartoon")
    os.makedirs(out, exist_ok=True)
    pad = 100
    for f in sorted(os.listdir(FOOD_OUT)):
        if not f.endswith(".png"):
            continue
        im = Image.open(os.path.join(FOOD_OUT, f)).convert("RGBA")
        base = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
        base.alpha_composite(im, (pad, pad))
        a = base.getchannel("A").filter(ImageFilter.MaxFilter(27)).filter(ImageFilter.GaussianBlur(30))
        sh = Image.merge("RGBA", (Image.new("L", base.size, 12), Image.new("L", base.size, 5),
                                  Image.new("L", base.size, 0), a.point(lambda v: int(v * 0.95))))
        sh.resize((256, 256), Image.LANCZOS).save(os.path.join(out, f))
    print("shadows ->", os.path.relpath(out, ROOT))


def board():
    """Figma grill + skewer (ArtSource/Cartoon/grill_v1.png, skewer_v1.png) -> exact game sizes, plus the
    v3 darkened grill (brightness x0.62, saturation x0.7) for non-complete skewers."""
    from PIL import ImageEnhance
    art = os.path.join(ROOT, "Assets", "GrillSort", "Art")
    for src, dst, size in (("grill_v1.png", "Board/grill_cartoon.png", (373, 1818)),
                           ("skewer_v1.png", "Board/skewer_cartoon.png", (97, 1869))):
        item = cut(Image.open(os.path.join(SRC, src)), True)
        item = item.resize(size, Image.LANCZOS)
        item.save(os.path.join(art, dst)); print("wrote", dst, "from", src)
        if "grill" in dst:
            a = item.getchannel("A")
            dark = ImageEnhance.Brightness(ImageEnhance.Color(item.convert("RGB")).enhance(0.7)).enhance(0.62).convert("RGBA")
            dark.putalpha(a)
            dark.save(os.path.join(art, "Generated", "grill_cartoon_dark.png")); print("wrote Generated/grill_cartoon_dark.png")


if __name__ == "__main__":
    run(FOODS, "food", FOOD_OUT, True)
    run(PEOPLE, "people", PEOPLE_OUT, False)
    bake_shadows()
    board()
