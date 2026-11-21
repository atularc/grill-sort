"""Tutorial hand from the user's pick (2026-11-21): ArtSource/Cartoon/ui/tutorial_hand_pngtree.png (Pngtree "hand click
cartoon for game", 3500x3500, transparent). It has a yellow glow baked around it; the client's FishUp! hand has none,
so the glow is removed (pixels clearly more yellow than the white/grey glove are dropped).

Writes Assets/GrillSort/Art/UI/Icons/icon_hand.png + UI_Soft/Icons/icon_hand.png (512x512, 4 px clear edge) and prints
the fingertip pivot for GrillSortSetup.HandPivot. The generated gloves stay as icon_hand_v1..v5.png.
LICENSE: Pngtree assets usually need a premium licence (or attribution) for commercial use - check before release.

Usage (repo root): python -I Tools/ArtPipeline/cut_hand_pngtree.py
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "ui", "tutorial_hand_pngtree.png")
OUTS = [os.path.join(ROOT, "Assets", "GrillSort", "Art", d, "Icons", "icon_hand.png") for d in ("UI", "UI_Soft")]
SIZE, PAD = 512, 0.03


def main():
    im = Image.open(SRC).convert("RGBA")
    a = np.asarray(im).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    yellow = (r + g) / 2 - b                     # glove: ~0 (white/grey); glow: strongly positive
    keep = np.clip((32 - yellow) / 18, 0, 1)
    m = Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(9)).filter(ImageFilter.GaussianBlur(2))
    al = al * np.asarray(m).astype(np.float32) / 255
    out = Image.fromarray(np.dstack([a[..., :3], al]).clip(0, 255).astype(np.uint8), "RGBA")
    out = out.crop(out.getbbox())
    inner = int(SIZE * (1 - 2 * PAD))
    s = inner / max(out.size)
    out = out.resize((round(out.width * s), round(out.height * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(out, ((SIZE - out.width) // 2, (SIZE - out.height) // 2))
    c = np.asarray(canvas).copy()
    c[:4, :, 3] = 0; c[-4:, :, 3] = 0; c[:, :4, 3] = 0; c[:, -4:, 3] = 0
    canvas = Image.fromarray(c, "RGBA")
    for o in OUTS:
        canvas.save(o); print("wrote", os.path.relpath(o, ROOT))
    alm = c[..., 3] > 128
    ys, xs = np.nonzero(alm)
    k = int(np.argmin(xs + ys))
    x, y = xs[k] + 12, ys[k] + 12
    print(f"fingertip px ({x},{y}) -> pivot ({x / SIZE:.3f}, {1 - y / SIZE:.3f})")


if __name__ == "__main__":
    main()
