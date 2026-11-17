# Readability variants of the mockup (assets untouched). Usage: python compose_variants.py shadow|lane|glow
# Run from grill-mockup/source:  python compose.py
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

R = ".."
W, H = 1080, 1920
FONT = "fonts/Fredoka-Bold.ttf"
def font(s): return ImageFont.truetype(FONT, s)
def A(path): return Image.open(f"{R}/{path}").convert("RGBA")

def fit(im, w=None, h=None):
    s = (w / im.width) if w else (h / im.height)
    return im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS)

def put(canvas, im, cx, cy, w=None, h=None, opacity=1.0):
    im = fit(im, w, h)
    if opacity < 1:
        a = im.getchannel("A").point(lambda v: int(v * opacity)); im.putalpha(a)
    canvas.alpha_composite(im, (round(cx - im.width / 2), round(cy - im.height / 2)))
    return im

def shadow(canvas, box, blur, alpha):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(box, fill=(20, 8, 0, alpha))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))

def rshadow(canvas, box, r, blur, alpha):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).rounded_rectangle(box, r, fill=(20, 8, 0, alpha))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(blur)))

def glow(canvas, cx, cy, rx, ry, color, alpha):
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=color + (alpha,))
    canvas.alpha_composite(layer.filter(ImageFilter.GaussianBlur(rx * 0.35)))

def text(canvas, xy, s, size, fill=(255, 255, 255), stroke=(78, 38, 16), sw=6, anchor="mm"):
    d = ImageDraw.Draw(canvas)
    d.text(xy, s, font=font(size), fill=fill, stroke_width=sw, stroke_fill=stroke, anchor=anchor)

def badge(canvas, cx, cy, n, color=(232, 52, 52)):
    d = ImageDraw.Draw(canvas)
    d.ellipse((cx - 30, cy - 30, cx + 30, cy + 30), fill=(255, 255, 255))
    d.ellipse((cx - 24, cy - 24, cx + 24, cy + 24), fill=color)
    text(canvas, (cx, cy - 1), str(n), 32, sw=0)

# ---------- background (cover-fit 1024x1536 -> 1080x1920)
bg = A("board/background.png")
s = max(W / bg.width, H / bg.height)
bg = bg.resize((round(bg.width * s), round(bg.height * s)), Image.LANCZOS)
bg = bg.crop(((bg.width - W) // 2, 0, (bg.width - W) // 2 + W, H))
C = bg.copy()
COUNTER_Y = round(528 * s)  # top of the counter in the background

# ---------- customers (standing behind the counter) + order bubbles
food = {n: A(f"food/{n}.png") for n in
        ["salmon", "sausage", "pineapple", "squid", "beef", "zucchini", "shrimp", "bacon", "potato", "halloumi"]}
CUST = [(150, "fox", "salmon", False), (410, "bear", "sausage", True),
        (670, "kitty", "pineapple", False), (930, "panda", "squid", False)]
bubble = A("ui/bubble_order.png"); check = A("ui/icon_check.png")
for cx, who, want, done in CUST:
    put(C, A(f"customers/{who}.png"), cx, COUNTER_Y - 80, w=250)
C.alpha_composite(bg.crop((0, COUNTER_Y, W, H)), (0, COUNTER_Y))  # counter in front of customers
for cx, who, want, done in CUST:
    by = COUNTER_Y - 290
    shadow(C, (cx - 80, by + 40, cx + 80, by + 70), 10, 70)
    put(C, bubble, cx, by, w=196)
    put(C, food[want], cx, by - 8, w=112)
    if done:
        put(C, check, cx + 80, by - 46, w=62)

# ---------- HUD
put(C, A("ui/btn_pause.png"), 92, 106, w=112)
put(C, A("ui/bar_hud.png"), 540, 100, w=300)
text(C, (540, 98), "Level 12", 52, fill=(92, 46, 18), sw=0)
put(C, A("ui/bar_hud.png"), 900, 100, w=250)
put(C, A("ui/icon_coin.png"), 790, 98, w=86)
text(C, (918, 98), "1,250", 44, fill=(92, 46, 18), sw=0)
pb = put(C, A("ui/bar_progress.png"), 540, 186, w=330)
text(C, (540, 184), "Orders 1 / 4", 30, sw=4, stroke=(40, 70, 20))

# ---------- grills
grill, skewer = A("board/grill.png"), A("board/skewer.png")
GX = [140, 340, 540, 740, 940]
GTOP, GH = 862, 770
GW = round(grill.width * GH / grill.height)
SLOT = [1515, 1352, 1189, 1026]  # bottom -> top
LAY = [["salmon", "pineapple", "beef"],
       ["sausage", "sausage", "sausage", "sausage"],
       ["beef", "zucchini", "salmon", "squid"],
       ["pineapple", "shrimp", "bacon"],
       ["squid", "zucchini"]]
SIZE = {"sausage": 170, "salmon": 150, "pineapple": 150, "beef": 148, "zucchini": 150,
        "squid": 160, "shrimp": 156, "bacon": 156, "potato": 146, "halloumi": 150}
DONE = 1
for i, gx in enumerate(GX):
    rshadow(C, (gx - GW / 2 - 6, GTOP + 18, gx + GW / 2 + 10, GTOP + GH + 22), 18, 14, 150)
import sys
MODE = sys.argv[1]
OUT = {"shadow": "v3-shadow", "lane": "v4-lane", "glow": "v5-glow"}[MODE]

def calm(im, bright, color):
    a = im.getchannel("A")
    im = ImageEnhance.Color(im.convert("RGB")).enhance(color)
    im = ImageEnhance.Brightness(im).enhance(bright).convert("RGBA"); im.putalpha(a); return im

def solid(size, rgb, alpha):
    return Image.merge("RGBA", (Image.new("L", size, rgb[0]), Image.new("L", size, rgb[1]), Image.new("L", size, rgb[2]), alpha))

def food_fx(im, w, sh_alpha, sh_off, sh_blur, sh_grow, halo=None):
    im = fit(im, w); pad = 40
    base = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
    base.alpha_composite(im, (pad, pad)); a = base.getchannel("A")
    out = Image.new("RGBA", base.size, (0, 0, 0, 0))
    if halo:
        col, ha, grow, blur = halo
        h = a.filter(ImageFilter.MaxFilter(grow)).filter(ImageFilter.GaussianBlur(blur)).point(lambda v: int(v * ha))
        out.alpha_composite(solid(base.size, col, h))
    sh = a.filter(ImageFilter.MaxFilter(sh_grow)).filter(ImageFilter.GaussianBlur(sh_blur)).point(lambda v: int(v * sh_alpha))
    out.alpha_composite(solid(base.size, (12, 5, 0), sh), sh_off)
    rgb = ImageEnhance.Contrast(ImageEnhance.Color(base.convert("RGB")).enhance(1.1)).enhance(1.06).convert("RGBA"); rgb.putalpha(a)
    out.alpha_composite(rgb); return out

if MODE == "shadow":   # food floats above the grate with a deep soft shadow
    grill_c = calm(grill, 0.62, 0.7); SC = 1.08
    fx = dict(sh_alpha=0.95, sh_off=(8, 18), sh_blur=10, sh_grow=9)
elif MODE == "lane":   # a dark smoky lane down the skewer; coals glow only at the sides
    grill_c = calm(grill, 0.78, 0.85); SC = 1.06
    fx = dict(sh_alpha=0.8, sh_off=(4, 12), sh_blur=8, sh_grow=7)
else:                  # cool grey coals + warm sizzling glow behind each food
    grill_c = calm(grill, 0.5, 0.35); SC = 1.06
    fx = dict(sh_alpha=0.7, sh_off=(4, 12), sh_blur=8, sh_grow=5, halo=((255, 176, 70), 0.75, 21, 16))

for i, gx in enumerate(GX):
    if i == DONE:
        glow(C, gx, GTOP + GH / 2, GW * 0.95, GH * 0.58, (255, 205, 70), 200)
    put(C, grill_c if i != DONE else grill, gx, GTOP + GH / 2, h=GH)
    if MODE == "lane":
        lw = GW * 0.36
        layer = Image.new("RGBA", C.size, (0, 0, 0, 0))
        ImageDraw.Draw(layer).rounded_rectangle((gx - lw, GTOP + 50, gx + lw, GTOP + GH - 50), 40, fill=(14, 7, 4, 185))
        C.alpha_composite(layer.filter(ImageFilter.GaussianBlur(14)))
    put(C, skewer, gx, GTOP + GH / 2 - 6, h=GH - 20)
    for j, n in enumerate(LAY[i]):
        f = food_fx(food[n], round(SIZE[n] * SC), **fx); y = SLOT[j]
        C.alpha_composite(f, (round(gx - f.width / 2), round(y - f.height / 2)))

# heat haze: soft steam over a few hot pieces
steam, spark, flame = A("vfx/fx_steam.png"), A("vfx/fx_sparkle.png"), A("vfx/fx_flame.png")
for gx, y in [(140, 1100), (540, 940), (740, 1110)]:
    put(C, steam, gx + 18, y, h=110, opacity=0.45)

# completed skewer: steam, sparkles, word
gx = GX[DONE]
for dx, y, hh in [(-34, 930, 150), (30, 900, 170)]:
    put(C, steam, gx + dx, y, h=hh, opacity=0.8)
for x, y, sz in [(gx - 92, 1000, 70), (gx + 96, 1150, 56), (gx - 88, 1420, 60), (gx + 92, 1560, 74), (gx + 60, 880, 46)]:
    put(C, spark, x, y, w=sz)
put(C, A("ui/word_perfect.png"), gx + 30, GTOP + GH - 4, w=300)


# ---------- boosters
tile = A("ui/btn_tile_yellow.png")
BOOST = [("icon_undo", 3), ("icon_hint", 2), ("icon_shuffle", 1), ("icon_add_skewer", 1)]
for k, (ic, n) in enumerate(BOOST):
    x = 240 + k * 200; y = 1790
    rshadow(C, (x - 78, y - 60, x + 84, y + 92), 30, 12, 110)
    put(C, tile, x, y, w=170)
    put(C, A(f"ui/{ic}.png"), x, y - 6, w=104)
    badge(C, x + 66, y + 58, n)

C.convert("RGB").save(f"{R}/Grill-Sort-Mockup-{OUT}.png", optimize=True)
print("saved")
