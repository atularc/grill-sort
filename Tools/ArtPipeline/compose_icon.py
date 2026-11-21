"""App icon versions composed from the game's REAL art (2026-11-21, user: "make logo which matches our game and theme";
the earlier generated icons were generic glossy 3D and didn't look like our game).

Uses the in-game sprites as they appear in play: lit red-enamel grill trays (grill_cartoon_bed + _front), the bamboo
skewer, FoodFinal_v3 (+v4 tomato) with their deep shadows, the calm Beach background + teal curtain, CustomersPeople_v2,
the order bubble and sparkles. No text (like Sort Streak's icon).

Writes ArtSource/Icon/icon_game_<v>.png (1024x1024, square, full-bleed; the store rounds the corners).
Usage (repo root): python -I Tools/ArtPipeline/compose_icon.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")
OUT = os.path.join(ROOT, "ArtSource", "Icon")
S = 1024
# food slot centres / width in the 373x1818 grill source (game: slots 163 apart at grill height 770 -> x2.36)
K = 1818 / 770
SLOT_Y = [909 - (268 - 163 * i) * K for i in range(4)]
FOOD_W = {"sausage": 170, "corn": 160, "tomato": 140, "pepper": 156, "chicken": 150, "mushroom": 150, "shrimp": 156}


def load(rel):
    return Image.open(os.path.join(ART, rel)).convert("RGBA")


def food(name):
    d = "FoodFinal_v4/Grilled" if os.path.exists(os.path.join(ART, "FoodFinal_v4/Grilled", name + ".png")) else "FoodFinal_v3/Grilled"
    sd = "Generated/ShadowsFinal_v4" if d.endswith("v4/Grilled") else "Generated/ShadowsFinal"
    return load(f"{d}/{name}.png"), load(f"{sd}/{name}.png")


def tray(foods, lit=True, slots=4):
    """One grill tray exactly like in game (glow bed, front with coals, skewer, food with deep shadows), source px."""
    bed = load("Generated/grill_cartoon_bed.png" if lit else "Generated/grill_cartoon_dim_bed.png")
    front = load("Generated/grill_cartoon_front.png" if lit else "Generated/grill_cartoon_dim_front.png")
    w, h = front.size
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    c.alpha_composite(bed); c.alpha_composite(front)
    sk = load("Board/skewer_cartoon.png")
    skh = int(h * (750 / 770)); skw = int(skh * 97 / 1869)
    sk = sk.resize((skw, skh), Image.LANCZOS)
    c.alpha_composite(sk, ((w - skw) // 2, (h - skh) // 2 - int(6 * K)))
    for i, n in enumerate(foods[:slots]):
        f, sh = food(n)
        fw = int(FOOD_W.get(n, 150) * K)
        f = f.resize((fw, fw), Image.LANCZOS)
        sw = int(fw * 712 / 512)
        sh = sh.resize((sw, sw), Image.LANCZOS)
        cy = int(SLOT_Y[i])
        c.alpha_composite(sh, (w // 2 - sw // 2 + int(8 * K), cy - sw // 2 + int(18 * K)))
        c.alpha_composite(f, (w // 2 - fw // 2, cy - fw // 2))
    return c


def glow(size, color, alpha):
    g = load("Generated/glow.png").resize(size, Image.LANCZOS)
    a = np.asarray(g).astype(np.float32)
    a[..., 0], a[..., 1], a[..., 2] = color
    a[..., 3] *= alpha
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")


def beach_bg():
    bg = load("Backgrounds/Beach/background-calm.png")
    bg = bg.crop((0, 120, 1024, 1144)).resize((S, S), Image.LANCZOS)
    return ImageEnhance.Color(bg).enhance(1.15)


def curtain_strip(height):
    """The teal-and-white curtain under the counter (compact front layer), as a strip across the top."""
    fr = load("Backgrounds/Beach/layer_front_compact.png")
    band = fr.crop((0, 500, 1024, 625))
    return band.resize((S, height), Image.LANCZOS)


def sparkles(img, spots):
    sp = load("VFX/fx_sparkle.png")
    for x, y, w in spots:
        s = sp.resize((w, w), Image.LANCZOS)
        img.alpha_composite(s, (x - w // 2, y - w // 2))


def v1_three_trays():
    """Three sorted trays side by side under the teal curtain, warm glow behind (= the game in one look)."""
    c = beach_bg()
    c.alpha_composite(glow((1300, 1100), (255, 205, 70), .75), (-138, 80))
    sets = [["corn"] * 4, ["sausage"] * 4, ["tomato"] * 4]
    k = 1.02 * S / 1818 * 1.35
    tw, th = int(373 * k), int(1818 * k)
    gap = 26
    x0 = (S - (3 * tw + 2 * gap)) // 2
    for i, fs in enumerate(sets):
        t = tray(fs).resize((tw, th), Image.LANCZOS)
        c.alpha_composite(t, (x0 + i * (tw + gap), 120))
    c.alpha_composite(curtain_strip(150), (0, -18))
    sparkles(c, [(150, 330, 90), (880, 420, 70), (520, 980, 60)])
    return c


def v2_customer():
    """A customer behind the counter waiting for her order, the finished tray right below her."""
    c = beach_bg()
    cu = load("CustomersPeople_v2/curly.png").resize((620, 620), Image.LANCZOS)
    c.alpha_composite(cu, (40, 40))
    # order bubble with a little sausage skewer
    b = load("UI_Soft/Panels/bubble_order.png").resize((430, 260), Image.LANCZOS)
    c.alpha_composite(b, (560, 70))
    f, _ = food("sausage")
    f = f.resize((130, 130), Image.LANCZOS)
    for i in range(3):
        c.alpha_composite(f, (610 + i * 95, 120 - i * 8))
    # counter + curtain over her chest
    fr = load("Backgrounds/Beach/layer_front_compact.png").crop((0, 470, 1024, 1094)).resize((S, 620), Image.LANCZOS)
    c.alpha_composite(fr, (0, 520))
    c.alpha_composite(glow((900, 700), (255, 205, 70), .8), (62, 520))
    k = S / 1818 * 1.6
    t = tray(["sausage"] * 4).resize((int(373 * k), int(1818 * k)), Image.LANCZOS)
    t = t.rotate(-90, expand=True)                       # lying across the counter like it's being served
    c.alpha_composite(t, ((S - t.width) // 2, 640))
    sparkles(c, [(870, 600, 90), (120, 700, 70), (930, 930, 60)])
    return c


def v3_big_tray():
    """One big finished tray, tilted, glowing gold, sparkles (the 'Perfect!' moment), on the beach."""
    c = beach_bg()
    c.alpha_composite(glow((1200, 1200), (255, 205, 70), .9), (-88, -60))
    k = S / 1818 * 1.25
    t = tray(["sausage", "pepper", "sausage", "pepper"]).resize((int(373 * k), int(1818 * k)), Image.LANCZOS)
    t = tray(["sausage"] * 4).resize((int(373 * k), int(1818 * k)), Image.LANCZOS)
    t = t.rotate(-28, expand=True, resample=Image.BICUBIC)
    c.alpha_composite(t, ((S - t.width) // 2 + 30, (S - t.height) // 2 + 40))
    sparkles(c, [(190, 230, 110), (820, 260, 80), (850, 820, 100), (230, 860, 70)])
    return c


def v4_text_logo():
    """The "Grill Sort" text logo (grill-lid topper, the one used on the loading screen) on the loading screen's bright
    sky and sea (user pick 2026-11-21: the icon should be the Grill Sort text logo, no plain-colour background)."""
    key = load("Loading/keyart_default.png")
    bg = key.crop((0, 0, 1024, 1024))
    bg = ImageEnhance.Color(bg).enhance(1.05)
    c = bg.copy()
    c.alpha_composite(glow((1100, 1100), (255, 245, 210), .55), (-38, -40))   # soft light behind the logo
    logo = load("Loading/logo.png")
    lw = 900
    logo = logo.resize((lw, round(logo.height * lw / logo.width)), Image.LANCZOS)
    c.alpha_composite(logo, ((S - lw) // 2, (S - logo.height) // 2 + 10))
    return c


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn in (("v1_three_trays", v1_three_trays), ("v2_customer", v2_customer), ("v3_big_tray", v3_big_tray), ("v4_text_logo", v4_text_logo)):
        img = fn().convert("RGB")
        p = os.path.join(OUT, f"icon_game_{name}.png")
        img.save(p)
        print("wrote", os.path.relpath(p, ROOT))


if __name__ == "__main__":
    main()
