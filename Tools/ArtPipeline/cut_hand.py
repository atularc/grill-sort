"""Tutorial hand (2026-11-21, user: "they have a hand in the tutorial ... that hand for sure").

Source: ArtSource/Cartoon/ui/tutorial_hand.png (Figma, prompt in Docs/ArtPrompts.md "Tutorial hand").
Cut like the rest of the art (cut_cartoon.cut + place) into 512x512 with a 4 px transparent edge (atlas bleed), and
prints the fingertip as a sprite pivot (0..1) for GrillSortSetup (HandPivot).
Writes NEW files: Assets/GrillSort/Art/UI/Icons/icon_hand.png (+ the same in UI_Soft/Icons, which the game reads).

Usage (repo root): python -I Tools/ArtPipeline/cut_hand.py      OVERWRITE=1 to replace.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402

ROOT = cc.ROOT
# v2 (user: "not as good as the video reference"): soft glossy 3D white glove like the client's FishUp! tutorial,
# no outline. v1 (outlined cartoon glove) is kept as icon_hand_v1.png.
# v5 (user, comparing with the FishUp! hand: chunky mitten, short thick finger, small cuff): generated flat, then
# rotated TILT degrees so the finger points up-left and the cuff sits low on the right, like the reference.
# Older: v4 = icon_hand_v4.png (thin, steep), v3 sideways, v2 glossy.
TILT = -25
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "ui", "tutorial_hand_v5.png")
OUTS = [os.path.join(ROOT, "Assets", "GrillSort", "Art", d, "Icons", "icon_hand.png") for d in ("UI", "UI_Soft")]


def main():
    if all(os.path.exists(o) for o in OUTS) and os.environ.get("OVERWRITE") != "1":
        print("skip (exists)"); return
    raw = Image.open(SRC).convert("RGB")
    # white glove on white: flood-fill only the near-pure-white background (the glove's shading is light grey-blue)
    a0 = np.asarray(raw).astype(np.int16)
    bg_white = a0.min(axis=2) > 247
    border = np.zeros_like(bg_white); border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    bg = cc.grow(border, bg_white)
    fg = ~bg
    from PIL import ImageFilter
    alpha = Image.fromarray(fg.astype(np.uint8) * 255).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.8))
    cut = raw.convert("RGBA"); cut.putalpha(alpha)
    cut = cut.crop(cut.getbbox()).rotate(TILT, expand=True, resample=Image.BICUBIC)
    im = cc.place(cut.crop(cut.getbbox()))
    a = np.asarray(im).copy()
    e = 4
    a[:e, :, 3] = 0; a[-e:, :, 3] = 0; a[:, :e, 3] = 0; a[:, -e:, 3] = 0
    im = Image.fromarray(a, "RGBA")
    for o in OUTS:
        im.save(o); print("wrote", os.path.relpath(o, ROOT))
    # fingertip: the opaque pixel furthest toward the top-left (smallest x + y), nudged a little inside the edge
    al = a[..., 3] > 128
    ys, xs = np.nonzero(al)
    k = int(np.argmin(xs + ys))
    x, y = xs[k] + 10, ys[k] + 10
    print(f"fingertip px ({x:.0f},{y}) -> pivot ({x / 512:.3f}, {1 - y / 512:.3f})")


if __name__ == "__main__":
    main()
