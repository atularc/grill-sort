"""Softer, less glossy UI (Aaron 2026-11-21: "some UI elements could be polished to make the shine softer or less
glossy. Still 3D cartoon, just a softer look").

Same idea as the food's matte() in cut_cartoon.py (FoodFinal: shine toned down): bright, pale specular streaks are
pulled part of the way toward the colour around them, so the gloss turns into a soft sheen. The shapes, outlines,
shading and depth stay, only the glare goes down. White symbols (pause bars, gear, tick, plus; files in SYMBOLS) are
protected: they are thick solid white shapes, while the glare streaks are thinner and fade out.

Writes a complete NEW set to Assets/GrillSort/Art/UI_Soft/<Buttons|Icons|Panels|Words>/ (softened where listed in
STRENGTH, copied unchanged otherwise). Art/UI/ stays as the backup. The game reads UI_Soft (GrillSortSetup.UiDir).

Usage (repo root): python -I Tools/ArtPipeline/soften_ui.py      OVERWRITE=1 to replace existing files.
"""
import os
import shutil

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "GrillSort", "Art", "UI")
DST = os.path.join(ROOT, "Assets", "GrillSort", "Art", "UI_Soft")
# how much of the shine to take away (0 = keep, 1 = remove). "Softer", not flat: about half.
STRENGTH = {
    "Buttons/btn_green.png": .80, "Buttons/btn_orange.png": .80, "Buttons/btn_red.png": .80,
    "Buttons/btn_tile_yellow.png": .80, "Buttons/btn_pause.png": .80, "Buttons/btn_settings.png": .80,
    "Panels/ribbon_red.png": .75, "Panels/bar_hud.png": .65,
    "Icons/icon_coin.png": .70, "Icons/icon_star.png": .70, "Icons/icon_heart.png": .75, "Icons/icon_gift.png": .70,
    "Icons/icon_check.png": .75, "Icons/icon_lock.png": .70, "Icons/icon_hint.png": .65, "Icons/icon_undo.png": .70,
    "Icons/icon_shuffle.png": .70, "Icons/icon_add_skewer.png": .70,
}
SYMBOLS = {"Buttons/btn_pause.png", "Buttons/btn_settings.png", "Icons/icon_check.png", "Icons/icon_add_skewer.png"}
LUMA = np.array([.3, .59, .11], np.float32)


def soften(im, k, symbols=False):
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255
    rgb, al = a[..., :3], a[..., 3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    lum = rgb @ LUMA
    inner = np.asarray(Image.fromarray((al > .5).astype(np.uint8) * 255).filter(ImageFilter.MinFilter(7))) > 0
    base = Image.fromarray((rgb * 255).astype(np.uint8))
    small = base.resize((64, max(8, round(64 * base.height / base.width))), Image.BILINEAR)
    surround = np.asarray(small.filter(ImageFilter.MedianFilter(9)).resize(base.size, Image.BILINEAR)).astype(np.float32) / 255
    slum = surround @ LUMA
    hl = np.clip(((lum - slum) - .06) / .16, 0, 1) * np.clip((.65 - sat) / .4, 0, 1) * inner
    if symbols:
        # protect solid white symbols: white areas that survive a thick erosion (a pause bar or a tick does, the
        # thinner glare crescent on the rim doesn't), grown back to their full shape
        white = ((lum > .86) & (sat < .12)).astype(np.uint8) * 255
        e = max(5, round(min(im.size) * .045)) | 1
        core = Image.fromarray(white).filter(ImageFilter.MinFilter(e)).filter(ImageFilter.MaxFilter(e + 8))
        protect = np.asarray(core.filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255
        hl *= 1 - protect
    hl = np.asarray(Image.fromarray((hl * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(2.5))).astype(np.float32) / 255
    w = np.clip(hl, 0, 1) * k
    target = np.clip(surround * 1.12, 0, 1)   # a bit lighter than around it: soft sheen, not a white glare
    out = rgb * (1 - w[..., None]) + target * w[..., None]
    return Image.fromarray((np.dstack([np.clip(out, 0, 1), al]) * 255).astype(np.uint8), "RGBA")


def main():
    over = os.environ.get("OVERWRITE") == "1"
    for dirpath, _, files in os.walk(SRC):
        for f in files:
            if not f.endswith(".png"):
                continue
            rel = os.path.relpath(os.path.join(dirpath, f), SRC).replace("\\", "/")
            out = os.path.join(DST, rel)
            if os.path.exists(out) and not over:
                continue
            os.makedirs(os.path.dirname(out), exist_ok=True)
            if rel in STRENGTH:
                soften(Image.open(os.path.join(SRC, rel)), STRENGTH[rel], rel in SYMBOLS).save(out)
                print("softened", rel)
            else:
                shutil.copyfile(os.path.join(SRC, rel), out)
                print("copied  ", rel)
    # the Orders bar fill is baked by bake_game_fx.py into Generated/: softened copy next to it
    gen = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated")
    out = os.path.join(gen, "bar_fill_soft.png")
    if over or not os.path.exists(out):
        soften(Image.open(os.path.join(gen, "bar_fill.png")), .75).save(out)
        print("softened Generated/bar_fill.png -> bar_fill_soft.png")


if __name__ == "__main__":
    main()
