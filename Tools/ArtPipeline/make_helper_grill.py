"""Helper-tray grill (Aaron 2026-11-19: "2 helper trays without customers ... The helper trays look clearly different
from the customer trays, so players know they're just for space").

Reads the split grill art (split_grill_glow.py) and writes NEW files into Assets/GrillSort/Art/Generated/:
  grill_helper_base.png   same grill with the red enamel frame turned brushed steel (cool grey-blue)
The coal glow layer (grill_cartoon_dim_glow.png) is reused on top, so helpers still look hot.

Usage (repo root): python -I Tools/ArtPipeline/make_helper_grill.py      OVERWRITE=1 to replace.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GEN = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated")
SRC = os.path.join(GEN, "grill_cartoon_dim_base.png")
OUT = os.path.join(GEN, "grill_helper_base.png")
INNER = (54, 56, 319, 1743)   # coal bed inside the frame (same as split_grill_glow.py)


def main():
    if os.path.exists(OUT) and os.environ.get("OVERWRITE") != "1":
        print("skip (exists)", OUT); return
    im = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3], im[..., 3]
    h, w = a.shape
    frame = np.ones((h, w), bool)
    x0, y0, x1, y1 = INNER
    frame[y0:y1, x0:x1] = False
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    red = frame & (r > g + 25) & (r > b + 25)                   # the red enamel (keeps chrome bolts and dark outline)
    lum = np.clip(r * .5 + g * .3 + b * .2, 0, 255)
    steel = np.stack([lum * .82 + 20, lum * .9 + 26, lum * 1.0 + 34], -1)   # brushed steel, slightly blue
    k = np.clip((r - np.maximum(g, b) - 25) / 40, 0, 1)[..., None] * red[..., None]
    out = rgb * (1 - k) + steel * k
    Image.fromarray(np.dstack([out, a]).clip(0, 255).astype(np.uint8), "RGBA").save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
