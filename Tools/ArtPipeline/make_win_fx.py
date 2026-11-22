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


# ---- v2 (2026-11-22, user: the whitish disc behind the coins "looks bad and low quality"). The old look stretched a
# 128 px disc (jagged rim) and a 256 px glow (banding) to ~900 px. These are drawn at 1024 with float maths and a little
# noise (dither) so the gradients stay smooth after 8-bit + texture compression. No disc: a warm glow + soft rays, like
# the glowing reward in the client's games (Screwdom 3D / FishUp!).
def glow_hd(out):
    S2 = 1024
    rng = np.random.default_rng(5)
    c = (S2 - 1) / 2
    y, x = np.mgrid[0:S2, 0:S2].astype(np.float32)
    r = np.hypot(x - c, y - c) / c
    a = np.clip(1 - r, 0, 1)
    a = a ** 2.2 * .75 + np.exp(-(r / .28) ** 2) * .35          # broad falloff + brighter core
    a = np.clip(a + (rng.random((S2, S2)) - .5) / 255 * 2, 0, 1)
    img = np.dstack([np.full((S2, S2), 255, np.uint8)] * 3 + [(a * 255).astype(np.uint8)])
    Image.fromarray(img, "RGBA").save(out); print("wrote", out)


def rays_hd(out, n=16):
    S2 = 1024
    rng = np.random.default_rng(6)
    c = (S2 - 1) / 2
    y, x = np.mgrid[0:S2, 0:S2].astype(np.float32)
    ang = np.arctan2(y - c, x - c)
    r = np.hypot(x - c, y - c) / c
    wave = (np.cos(ang * n) + 1) / 2
    rays = np.clip((wave - .55) / .3, 0, 1) ** 1.5                 # tapered wedges with soft edges
    fade = np.clip(1 - r, 0, 1) ** 1.2 * np.clip((r - .08) / .25, 0, 1)
    a = np.clip(rays * fade + (rng.random((S2, S2)) - .5) / 255 * 2, 0, 1)
    img = np.dstack([np.full((S2, S2), 255, np.uint8)] * 3 + [(a * 255).astype(np.uint8)])
    Image.fromarray(img, "RGBA").save(out); print("wrote", out)


if __name__ == "__main__":
    g = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated")
    glow_hd(os.path.join(g, "glow_hd.png"))
    rays_hd(os.path.join(g, "rays_hd.png"))
