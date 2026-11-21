"""Split each grill into two layers so the red glow sits UNDER the coals (Aaron 2026-11-21: "the red glow under the
coals isn't on its own layer yet. Could you split it out and add a gentle flicker animation?").

The earlier split (split_grill_glow.py) only lifted the brightest orange spots and drew them ON TOP of the coals; the
red glow itself stayed painted into the grill. This one cuts the grill in two:
  <name>_bed.png    the whole coal bed (glow + everything) inside the frame, opaque. Drawn first; the game tints it
                    darker/brighter to flicker.
  <name>_front.png  the grill with every warm pixel inside the frame made transparent: frame, coals, bars, outlines.
                    Drawn on top, so the glow shows through the gaps between the coals.
Bed + front at full brightness look exactly like the source art.

Sources (untouched): Board/grill_cartoon.png (completed grill), Generated/grill_cartoon_dim.png (normal grill).
Also writes grill_helper_front.png (the dim front with the frame turned steel, like make_helper_grill.py), and
  <name>_hot_a.png / _hot_b.png  (user, same day: "the flame glow ... still feels like before"): a brighter, yellower
                    copy of the glow, each covering different random patches of the bed (A and B add up to the whole
                    bed). Drawn over the bed and under the front; the game pulses A and B out of step, so the embers
                    shimmer patch by patch instead of the whole bed just dimming.
All outputs are NEW files in Assets/GrillSort/Art/Generated/.

Usage (repo root): python -I Tools/ArtPipeline/split_grill_layers.py      OVERWRITE=1 to replace existing files.
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")
OUT = os.path.join(ART, "Generated")
SOURCES = {
    "grill_cartoon": os.path.join(ART, "Board", "grill_cartoon.png"),
    "grill_cartoon_dim": os.path.join(ART, "Generated", "grill_cartoon_dim.png"),
}
INNER = (54, 56, 319, 1743)   # coal bed inside the red enamel frame (373x1818 source)


def load(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)


def save(arr, name):
    Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    print("wrote", name)


def inside_mask(h, w):
    m = np.zeros((h, w), bool)
    x0, y0, x1, y1 = INNER
    m[y0:y1, x0:x1] = True
    return m


def split(path):
    im = load(path)
    rgb, a = im[..., :3], im[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    inside = inside_mask(*a.shape)
    # warm = glow (dark red -> orange -> yellow). Coals, bars and outlines are grey/blue/black, so red - blue is low.
    warm = np.clip((r - b - 28) / 45, 0, 1) * inside
    warm = np.asarray(Image.fromarray((warm * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(.7))).astype(np.float32) / 255
    warm *= inside
    front = np.dstack([rgb, a * (1 - warm)])
    bed = np.dstack([rgb, a * inside])
    return bed, front


def steel(front):
    """Red enamel frame -> brushed steel (same recipe as make_helper_grill.py)."""
    rgb, a = front[..., :3], front[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    frame = ~inside_mask(*a.shape)
    lum = np.clip(r * .5 + g * .3 + b * .2, 0, 255)
    st = np.stack([lum * .82 + 20, lum * .9 + 26, lum * 1.0 + 34], -1)
    k = (np.clip((r - np.maximum(g, b) - 25) / 40, 0, 1) * frame)[..., None]
    return np.dstack([rgb * (1 - k) + st * k, a])


def hot_layers(bed, seed):
    """Two shimmer layers: the bed's glow brightened toward yellow, masked by soft random patches (A = n, B = 1 - n)."""
    rgb, a = bed[..., :3], bed[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    warm = np.clip((r - b - 28) / 45, 0, 1)
    hot = np.stack([np.clip(r * 1.15 + 40, 0, 255), np.clip(g * 1.55 + 45, 0, 255), np.clip(b * 1.1 + 10, 0, 255)], -1)
    rng = np.random.default_rng(seed)
    h, w = a.shape
    n = rng.random((h // 24 + 2, w // 24 + 2)).astype(np.float32)
    n = np.asarray(Image.fromarray((n * 255).astype(np.uint8), "L").resize((w, h), Image.BICUBIC).filter(ImageFilter.GaussianBlur(10))).astype(np.float32) / 255
    n = np.clip((n - n.min()) / max(1e-3, n.max() - n.min()), 0, 1)
    n = np.clip((n - .5) * 2.2 + .5, 0, 1)   # more contrast: clear patches
    out = []
    for m in (n, 1 - n):
        out.append(np.dstack([hot, a * warm * m]))
    return out


def main():
    over = os.environ.get("OVERWRITE") == "1"
    for name, src in SOURCES.items():
        outs = [f"{name}_bed.png", f"{name}_front.png"]
        if not over and all(os.path.exists(os.path.join(OUT, o)) for o in outs):
            print("skip (exists)", name); continue
        bed, front = split(src)
        save(bed, outs[0]); save(front, outs[1])
        if name == "grill_cartoon_dim":
            save(steel(front), "grill_helper_front.png")
    for i, name in enumerate(SOURCES):
        outs = [f"{name}_hot_a.png", f"{name}_hot_b.png"]
        if not over and all(os.path.exists(os.path.join(OUT, o)) for o in outs):
            print("skip (exists)", outs); continue
        ha, hb = hot_layers(load(os.path.join(OUT, f"{name}_bed.png")), 7 + i)
        save(ha, outs[0]); save(hb, outs[1])


if __name__ == "__main__":
    main()
