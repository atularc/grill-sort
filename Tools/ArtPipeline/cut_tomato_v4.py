"""Grilled tomato v4 (Aaron 2026-11-21: "Tomato: it looks a bit too juicy right now, not quite like a grilled tomato yet").

New Figma generation (prompt: Docs/ArtPrompts.md "Grilled tomato v4"): ArtSource/Cartoon/singleB/food_tomato_grilled_v4.png
Cut exactly like the rest of the game food (cut_cartoon.py: cut + place, half of the matte() shine pass like FoodFinal_v2)
and its own deep shadow (same recipe as bake_shadows()).

Writes NEW files (v3 tomato + ShadowsFinal/tomato stay):
  Assets/GrillSort/Art/FoodFinal_v4/Grilled/tomato.png
  Assets/GrillSort/Art/Generated/ShadowsFinal_v4/tomato.png
GrillSortSetup reads a food from FoodFinal_v4 when the file exists there (FoodOverrideDir).

Usage (repo root): python -I Tools/ArtPipeline/cut_tomato_v4.py      OVERWRITE=1 to replace.
"""
import os
import sys

from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402  (module code only runs under __main__)

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "singleB", "food_tomato_grilled_v4.png")
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "FoodFinal_v4", "Grilled", "tomato.png")
SHADOW = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated", "ShadowsFinal_v4", "tomato.png")


def shadow(im):
    pad = 100
    base = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    base.alpha_composite(im, (pad, pad))
    a = base.getchannel("A").filter(ImageFilter.MaxFilter(27)).filter(ImageFilter.GaussianBlur(30))
    sh = Image.merge("RGBA", (Image.new("L", base.size, 12), Image.new("L", base.size, 5),
                              Image.new("L", base.size, 0), a.point(lambda v: int(v * 0.95))))
    return sh.resize((256, 256), Image.LANCZOS)


def main():
    if os.path.exists(OUT) and os.environ.get("OVERWRITE") != "1":
        print("skip (exists)", OUT); return
    raw = cc.place(cc.cut(Image.open(SRC), True))
    out = Image.blend(raw, cc.matte(raw), .5)   # half the shine, like FoodFinal_v2
    for p in (OUT, SHADOW):
        os.makedirs(os.path.dirname(p), exist_ok=True)
    out.save(OUT); print("wrote", os.path.relpath(OUT, ROOT))
    shadow(out).save(SHADOW); print("wrote", os.path.relpath(SHADOW, ROOT))


if __name__ == "__main__":
    main()
