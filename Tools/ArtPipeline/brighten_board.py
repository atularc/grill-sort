"""Client feedback 2026-11-18 (Aaron): "the bottom part feels a bit dark, brighten it up a little".
Writes NEW files (originals stay):
- Generated/grill_cartoon_dim.png: the non-complete grill, dimmed much less than the v3 grill_cartoon_dark.png
  (brightness x0.86 / saturation x0.9 instead of x0.62 / x0.7) with the coal blacks lifted a little.
- Backgrounds/Wood/layer_front_bright.png: the wood planks below the counter cloth lifted ~30% with a soft ramp.
Usage (repo root): python -I Tools/ArtPipeline/brighten_board.py
"""
import os
import numpy as np
from PIL import Image, ImageEnhance

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")


def grill():
    item = Image.open(os.path.join(ART, "Board", "grill_cartoon.png")).convert("RGBA")
    a = item.getchannel("A")
    rgb = ImageEnhance.Brightness(ImageEnhance.Color(item.convert("RGB")).enhance(0.9)).enhance(0.86)
    x = np.asarray(rgb).astype(np.float32) / 255
    x = x ** 0.88  # lift the black coals a little so the tray doesn't read as a dark hole
    out = Image.fromarray((np.clip(x, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
    out.putalpha(a)
    out.save(os.path.join(ART, "Generated", "grill_cartoon_dim.png"))
    print("wrote Generated/grill_cartoon_dim.png")


def wood_front(start=680, ramp=120, gain=1.3):
    p = os.path.join(ART, "Backgrounds", "Wood")
    im = Image.open(os.path.join(p, "layer_front.png")).convert("RGBA")
    x = np.asarray(im).astype(np.float32)
    h = x.shape[0]
    t = np.clip((np.arange(h) - start) / ramp, 0, 1)[:, None, None]  # 0 above the cloth, 1 on the planks
    rgb = x[..., :3] / 255
    lifted = 1 - (1 - np.clip(rgb * gain, 0, 1)) * 0.94  # gain + a touch of lift in the shadows
    lifted *= np.array([1.0, 0.98, 0.94])  # keep it warm
    x[..., :3] = (rgb * (1 - t) + lifted * t) * 255
    Image.fromarray(np.clip(x, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(p, "layer_front_bright.png"))
    print("wrote Backgrounds/Wood/layer_front_bright.png")


if __name__ == "__main__":
    grill()
    wood_front()
