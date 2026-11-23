"""Raw food stage + three clearer grilled foods (2026-11-23, review: cooking stages as the hook; squid, bacon and beef read
as uncooked in play).

1. Raw (cooking stages): 18 new Figma generations in the game food's style, one per food, shaped like the grilled piece
   (prompts: Docs/ArtPrompts.md "Raw food v1"). Sources: ArtSource/Cartoon/raw/food_<name>_raw.png
   -> Assets/GrillSort/Art/FoodRaw_v1/Raw/<name>.png + Generated/ShadowsRaw_v1/<name>.png
2. Grilled v5 (squid, bacon, beef): ArtSource/Cartoon/grilledV5/food_<name>_grilled_v5.png
   -> Assets/GrillSort/Art/FoodFinal_v5/Grilled/<name>.png + Generated/ShadowsFinal_v5/<name>.png
   Same recipe as the game food: cut + place, half the matte() shine pass (like FoodFinal_v2), then soften_marks
   (thinner, lighter, same-hue marks like FoodFinal_v3).

Never overwrites unless OVERWRITE=1. Old sets stay as they are.
Usage (repo root): python -I Tools/ArtPipeline/cut_food_v5.py [raw|grilled]
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402
import cut_tomato_v4 as tv  # noqa: E402  (shadow recipe)
import soften_marks as sm  # noqa: E402

ROOT = cc.ROOT
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")
RAW_SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "raw")
V5_SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "grilledV5")
RAW_OUT, RAW_SH = os.path.join(ART, "FoodRaw_v1", "Raw"), os.path.join(ART, "Generated", "ShadowsRaw_v1")
V5_OUT, V5_SH = os.path.join(ART, "FoodFinal_v5", "Grilled"), os.path.join(ART, "Generated", "ShadowsFinal_v5")
V5 = ["squid", "bacon", "beef"]
OVER = os.environ.get("OVERWRITE") == "1"


def cut_food(src):
    item = cc.place(cc.cut(Image.open(src), True))
    return Image.blend(item, cc.matte(item), .5)   # half the shine, like FoodFinal_v2


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    print("wrote", os.path.relpath(path, ROOT))


def raw():
    for n in cc.FOODS:
        out = os.path.join(RAW_OUT, n + ".png")
        if os.path.exists(out) and not OVER:
            print("skip (exists)", n); continue
        img = cut_food(os.path.join(RAW_SRC, f"food_{n}_raw.png"))
        save(img, out)
        save(tv.shadow(img), os.path.join(RAW_SH, n + ".png"))


def grilled():
    tmp = os.path.join(ROOT, "ArtSource", "Cartoon", "grilledV5", "cut")   # pre-soften cut (kept for re-tuning)
    for n in V5:
        out = os.path.join(V5_OUT, n + ".png")
        if os.path.exists(out) and not OVER:
            print("skip (exists)", n); continue
        save(cut_food(os.path.join(V5_SRC, f"food_{n}_grilled_v5.png")), os.path.join(tmp, n + ".png"))
        sm.SRC = tmp
        img, px = sm.process(n)
        print(f"  {n}: marks {px}px softened")
        save(img, out)
        save(tv.shadow(img), os.path.join(V5_SH, n + ".png"))


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    if what in ("raw", "all"): raw()
    if what in ("grilled", "all"): grilled()
