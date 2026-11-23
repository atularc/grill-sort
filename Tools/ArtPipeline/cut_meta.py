"""Meta / home screen art (2026-11-23): stall decorations, daily-reward and home icons, customer emotes, booster shelf.

Sources (Figma, prompts in Docs/ArtPrompts.md "Meta art v1"): ArtSource/Cartoon/meta/
  deco_<name>.png (one item each), sheet_rewards / sheet_home_icons / sheet_emotes.png (2x2 grids), shelf.png
Writes NEW files to Assets/GrillSort/Art/Meta/ (transparent, trimmed, longest side MAX px; never overwrites unless OVERWRITE=1).
Usage (repo root): python -I Tools/ArtPipeline/cut_meta.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "meta")
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Meta")
OVER = os.environ.get("OVERWRITE") == "1"
EDGE = 4

SINGLES = {"deco_lights": 1024, "deco_plant": 512, "deco_menu": 512, "deco_torch": 512, "deco_smoker": 640, "deco_sign": 512, "shelf": 1024}
SHEETS = {
    "sheet_rewards": ["reward_coins_small", "reward_coins_big", "reward_gift", "reward_chest"],
    "sheet_home_icons": ["icon_decorate", "icon_map", "icon_daily", "icon_trophy"],
    "sheet_emotes": ["emote_heart", "emote_sweat", "emote_angry", "emote_yummy"],
}
SHEET_MAX = 256


def flat(path):
    src = Image.open(path).convert("RGBA")
    f = Image.new("RGBA", src.size, (255, 255, 255, 255)); f.alpha_composite(src)
    return f.convert("RGB")


def finish(item, mx, name):
    s = min(1.0, mx / max(item.size))
    item = item.resize((max(1, round(item.width * s)), max(1, round(item.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (item.width + EDGE * 2, item.height + EDGE * 2), (0, 0, 0, 0))
    canvas.alpha_composite(item, (EDGE, EDGE))
    path = os.path.join(OUT, name + ".png")
    canvas.save(path)
    print(f"wrote {os.path.relpath(path, ROOT)} {canvas.size}")


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, mx in SINGLES.items():
        if os.path.exists(os.path.join(OUT, name + ".png")) and not OVER: print("skip", name); continue
        finish(cc.cut(flat(os.path.join(SRC, name + ".png")), True), mx, name)
    for sheet, names in SHEETS.items():
        im = flat(os.path.join(SRC, sheet + ".png"))
        w, h = im.size
        for i, name in enumerate(names):
            if os.path.exists(os.path.join(OUT, name + ".png")) and not OVER: print("skip", name); continue
            x, y = (i % 2) * w // 2, (i // 2) * h // 2
            finish(cc.cut(im.crop((x, y, x + w // 2, y + h // 2)), True), SHEET_MAX, name)


if __name__ == "__main__":
    main()
