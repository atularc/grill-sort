# Builds Assets/GrillSort/Art/Generated/card_cream.png: a 9-slice rounded cream card with the same brown rim and colours
# as UI_Soft/Panels/bar_hud.png (2026-11-23, Home stall card: the HUD pill's round ends left no room for 3 lines).
# Slice borders in GrillSortSetup: 72 on every side.
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "../../Assets/GrillSort/Art/Generated/card_cream.png")
S, SS = 240, 4                  # final size, supersample
R, RIM = 66, 22                 # corner radius, rim thickness (final px)


def rr(size, box, radius, fill):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=fill)
    return m


W = S * SS
outer = rr((W, W), (6 * SS, 4 * SS, W - 6 * SS, W - 10 * SS), R * SS, 255)
inner_box = (6 * SS + RIM * SS, 4 * SS + RIM * SS * .8, W - 6 * SS - RIM * SS, W - 10 * SS - RIM * SS * 1.15)
inner = rr((W, W), inner_box, (R - RIM) * SS, 255)
shadow = rr((W, W), (6 * SS, 12 * SS, W - 6 * SS, W - 2 * SS), R * SS, 70).filter(ImageFilter.GaussianBlur(5 * SS))

ys = np.linspace(0, 1, W)[:, None] * np.ones((1, W))
rim = np.zeros((W, W, 3))
for c, (top, bot) in enumerate([(172, 119), (78, 39), (42, 11)]):
    rim[..., c] = top + (bot - top) * ys
cream = np.zeros((W, W, 3))
for c, (top, bot) in enumerate([(254, 254), (244, 234), (210, 192)]):
    cream[..., c] = top + (bot - top) * ys
# soft warm shade along the inside bottom edge, like the pill
inner_a = np.array(inner) / 255.0
edge = np.array(inner.filter(ImageFilter.GaussianBlur(9 * SS))) / 255.0
shade = np.clip((inner_a - edge) * 2.2, 0, 1)[..., None] * np.clip((ys[..., None] - .45) / .4, 0, 1)
cream = cream * (1 - shade * .35) + np.array([232, 184, 134]) * shade * .35

rgb = rim * (1 - inner_a[..., None]) + cream * inner_a[..., None]
a_card = np.array(outer) / 255.0
a_sh = np.array(shadow) / 255.0
alpha = a_card + a_sh * (1 - a_card)
rgb = (rgb * a_card[..., None]) / np.maximum(alpha[..., None], 1e-6)   # shadow is black
img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8), "RGBA")
img = img.resize((S, S), Image.LANCZOS)
if os.path.exists(OUT) and not os.environ.get("OVERWRITE"):
    raise SystemExit("exists: " + OUT)
img.save(OUT)
print("saved", OUT, img.size)
