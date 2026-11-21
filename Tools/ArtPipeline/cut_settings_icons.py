"""Settings panel icons (2026-11-21, user: a settings panel like FishUp!'s, matching our theme).
Source: ArtSource/Cartoon/ui/settings_icons_sheet.png (one Figma 2x2 sheet: speaker, music note, vibrating phone,
red close button). Each quadrant is cut like the rest of the UI art (cut_cartoon.cut, 4 px clear edge) into NEW files
Assets/GrillSort/Art/UI/Icons/ and UI_Soft/Icons/: icon_sound, icon_music, icon_vibrate, btn_close.
Usage (repo root): python -I Tools/ArtPipeline/cut_settings_icons.py
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "ui", "settings_icons_sheet.png")
NAMES = ["icon_sound", "icon_music", "icon_vibrate", "btn_close"]   # TL, TR, BL, BR


def main():
    sheet = Image.open(SRC).convert("RGBA")
    flat = Image.new("RGBA", sheet.size, (255, 255, 255, 255)); flat.alpha_composite(sheet)
    w, h = flat.size
    for i, n in enumerate(NAMES):
        x0, y0 = (i % 2) * w // 2, (i // 2) * h // 2
        cell = flat.crop((x0, y0, x0 + w // 2, y0 + h // 2)).convert("RGB")
        im = cc.cut(cell, True)
        s = 400 / max(im.size)
        im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
        c = Image.new("RGBA", (im.width + 8, im.height + 8), (0, 0, 0, 0)); c.alpha_composite(im, (4, 4))
        for d in ("UI", "UI_Soft"):
            p = os.path.join(ROOT, "Assets", "GrillSort", "Art", d, "Icons", n + ".png")
            c.save(p)
        print("wrote", n, c.size)


if __name__ == "__main__":
    main()
