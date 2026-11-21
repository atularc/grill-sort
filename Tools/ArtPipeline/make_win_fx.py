"""Win popup celebration texture (2026-11-21): soft sun rays that turn slowly behind the Level Complete panel.

Writes NEW file Assets/GrillSort/Art/Generated/rays.png (512x512, white rays fading out from the centre; the game tints
it warm). Usage (repo root): python -I Tools/ArtPipeline/make_win_fx.py      OVERWRITE=1 to replace.
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated", "rays.png")
SIZE, RAYS = 512, 14


def main():
    if os.path.exists(OUT) and os.environ.get("OVERWRITE") != "1":
        print("skip (exists)", OUT); return
    c = (SIZE - 1) / 2
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    ang = np.arctan2(y - c, x - c)
    r = np.hypot(x - c, y - c) / c
    # rays: wedges a bit narrower than the gaps, soft edges
    wave = (np.cos(ang * RAYS) + 1) / 2
    rays = np.clip((wave - .45) / .2, 0, 1)
    fade = np.clip(1 - r, 0, 1) ** 1.4 * np.clip(r / .12, 0, 1)   # fade out to the rim, hollow centre (the panel covers it)
    a = rays * fade
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))).astype(np.uint8)
    img = np.dstack([np.full((SIZE, SIZE), 255, np.uint8)] * 3 + [a])
    Image.fromarray(img, "RGBA").save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
