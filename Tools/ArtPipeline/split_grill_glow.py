"""Split the red coal glow out of the grill art into its own layer (Aaron, 2026-11-18: "split the red glow under the
coals into its own layer and add a flickering tween, so it looks like real fire").

For each grill sprite (lit + dim) writes two NEW files into Assets/GrillSort/Art/Generated/ (sources untouched):
  <name>_base.png  the grill with the glow between the coals burned down to dark embers
  <name>_glow.png  only the glow (same size, transparent elsewhere); the game puts it on top and flickers it

Usage (repo root): python -I Tools/ArtPipeline/split_grill_glow.py      OVERWRITE=1 to replace existing files.
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")
OUT = os.path.join(ART, "Generated")
SOURCES = {
    "grill_cartoon": os.path.join(ART, "Board", "grill_cartoon.png"),          # completed grill (full brightness)
    "grill_cartoon_dim": os.path.join(ART, "Generated", "grill_cartoon_dim.png"),  # normal grill
}
# coal bed inside the red enamel frame, in px of the 373x1818 source (the frame is the same red, so stay inside it)
INNER = (54, 56, 319, 1743)   # x0, y0, x1, y1


def split(path):
    im = np.asarray(Image.open(path).convert("RGBA")).astype(np.float32)
    rgb, a = im[..., :3], im[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    h, w = a.shape
    inside = np.zeros((h, w), bool)
    x0, y0, x1, y1 = INNER
    inside[y0:y1, x0:x1] = True
    # glow = warm pixels (red clearly above blue); coals and chrome bars are grey/blue
    wgt = np.clip((r - b - 40) / 90, 0, 1) * inside * (a > 0)
    wgt = np.asarray(Image.fromarray((wgt * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255
    wgt *= inside

    ember = np.stack([r * .30 + 10, g * .10 + 4, b * .10 + 6], -1)            # dark smouldering red
    base = rgb * (1 - wgt[..., None]) + ember * wgt[..., None]
    glow = np.dstack([rgb, wgt * 255])
    return (Image.fromarray(np.dstack([base, a]).clip(0, 255).astype(np.uint8), "RGBA"),
            Image.fromarray(glow.clip(0, 255).astype(np.uint8), "RGBA"))


def main():
    over = os.environ.get("OVERWRITE") == "1"
    for name, src in SOURCES.items():
        outs = {f"{name}_base.png": None, f"{name}_glow.png": None}
        if not over and all(os.path.exists(os.path.join(OUT, k)) for k in outs):
            print("skip (exists)", name); continue
        base, glow = split(src)
        base.save(os.path.join(OUT, f"{name}_base.png"))
        glow.save(os.path.join(OUT, f"{name}_glow.png"))
        print("wrote", f"{name}_base.png", f"{name}_glow.png")


if __name__ == "__main__":
    main()
