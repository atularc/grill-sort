# Cuts the raw and medium-grilled food originals (food-raw-hd, food-medium-hd) into 512px game sprites
# (food-raw, food-medium) and builds Grill-Food-Stages.png (raw -> medium -> grilled for every food).
import os, numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
R = ".."
def cut(src, thresh=236):
    a = np.asarray(Image.open(src).convert("RGB")).astype(np.float32)
    near = (a.min(axis=2) >= thresh).astype(np.uint8) * 255
    m = Image.fromarray(near, "L").copy(); W, H = m.size
    for s in [(x, 0) for x in range(0, W, 8)] + [(x, H - 1) for x in range(0, W, 8)] + [(0, y) for y in range(0, H, 8)] + [(W - 1, y) for y in range(0, H, 8)]:
        if m.getpixel(s) == 255: ImageDraw.floodfill(m, s, 128)
    alpha = np.where(np.asarray(m) == 128, 0, 255).astype(np.uint8)
    A = np.asarray(Image.fromarray(alpha, "L").filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
    k = A[..., None]
    with np.errstate(divide="ignore", invalid="ignore"):
        rgb = np.where(k > 0.02, np.clip((a - (1 - k) * 255) / np.maximum(k, 1e-3), 0, 255), 0)
    img = Image.fromarray(np.dstack([rgb, A * 255]).astype(np.uint8), "RGBA"); img = img.crop(img.getbbox())
    s = int(max(img.size) * 1.06); c = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    c.paste(img, ((s - img.width) // 2, (s - img.height) // 2), img)
    return c.resize((512, 512), Image.LANCZOS)
for stage in ["raw", "medium"]:
    os.makedirs(f"{R}/food-{stage}", exist_ok=True)
    for f in sorted(os.listdir(f"{R}/food-{stage}-hd")):
        cut(f"{R}/food-{stage}-hd/{f}").save(f"{R}/food-{stage}/{f}", optimize=True)
    print(stage, len(os.listdir(f"{R}/food-{stage}")))
# stages sheet
F = ImageFont.truetype("fonts/Fredoka-Bold.ttf", 30)
names = sorted(f[:-4] for f in os.listdir(f"{R}/food")); per = 3; cw = 190; gw = cw * 3 + 40; rh = 250
S = Image.new("RGBA", (per * gw + 20, 60 + (len(names) + per - 1) // per * rh), (252, 236, 214, 255)); d = ImageDraw.Draw(S)
for k in range(per):
    for j, t in enumerate(["Fresh", "Medium", "Grilled"]):
        d.text((20 + k * gw + j * cw + cw // 2, 30), t, font=F, fill=(150, 90, 50), anchor="mm")
for i, n in enumerate(names):
    x = 20 + (i % per) * gw; y = 60 + (i // per) * rh
    d.rounded_rectangle((x, y, x + cw * 3, y + rh - 14), 24, fill=(255, 248, 238))
    for j, folder in enumerate(["food-raw", "food-medium", "food"]):
        S.alpha_composite(Image.open(f"{R}/{folder}/{n}.png").resize((170, 170), Image.LANCZOS), (x + j * cw + 10, y + 8))
    d.text((x + cw * 1.5, y + rh - 40), n.capitalize(), font=F, fill=(92, 46, 18), anchor="mm")
S.convert("RGB").save(f"{R}/Grill-Food-Stages.png", optimize=True); print("sheet", S.size)
