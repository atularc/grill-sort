"""Make the counter (table) and the curtain/valance under it smaller (Aaron 2026-11-21: "Table and curtain: these are
mainly visual, so they could be a bit smaller to free up space for the layout").

For every theme, squashes the band from the counter top down to the bottom of the curtain to SCALE of its height.
The counter top line stays where it is (customers don't move); the floor below moves up and the freed rows at the
bottom are filled by stretching the last floor rows (they sit under the booster bar).

Writes NEW files: Assets/GrillSort/Art/Backgrounds/<Theme>/layer_front_compact.png (source untouched: the bright
front where one exists, else layer_front.png). Prints the new band bottom for GrillSortSetup (Theme(..., bottom)).

Usage (repo root): python -I Tools/ArtPipeline/compact_counter.py      OVERWRITE=1 to replace, SCALE=0.62 default.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BG = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Backgrounds")
SCALE = float(os.environ.get("SCALE", "0.62"))
# counter top, band bottom (bottom of the curtain / front panel), px in the 1024x1536 source (measured 2026-11-21)
BANDS = {
    "Beach": (500, 692),
    "FoodTruck": (527, 655),
    "Winter": (538, 658),
    "Wood": (528, 664),
    "NightMarket": (520, 645),
}


def main():
    over = os.environ.get("OVERWRITE") == "1"
    for theme, (top, bottom) in BANDS.items():
        d = os.path.join(BG, theme)
        out = os.path.join(d, "layer_front_compact.png")
        src = os.path.join(d, "layer_front_bright.png")
        if not os.path.exists(src):
            src = os.path.join(d, "layer_front.png")
        new_h = round((bottom - top) * SCALE)
        print(f"{theme}: band {top}-{bottom} -> {top}-{top + new_h}  (Theme bottom = {top + new_h})")
        if os.path.exists(out) and not over:
            print("  skip (exists)"); continue
        im = Image.open(src).convert("RGBA")
        w, h = im.size
        a = np.asarray(im)
        band = im.crop((0, top, w, bottom)).resize((w, new_h), Image.LANCZOS)
        rest = a[bottom:]
        pad = (bottom - top) - new_h
        fill = np.repeat(rest[-1:], pad, axis=0)        # stretch the last floor row into the freed space
        res = np.concatenate([a[:top], np.asarray(band), rest, fill], 0)
        assert res.shape[0] == h
        Image.fromarray(res, "RGBA").save(out)
        print("  wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main()
