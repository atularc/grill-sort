# Bakes the helper sprites + placeholder SFX the Unity game uses. Reads/writes inside the Unity project
# (no art-lab needed). Run from the repo root:  python Tools/ArtPipeline/bake_game_fx.py
# Never touches existing art: everything goes to Assets/GrillSort/Art/Generated and Assets/GrillSort/Audio.
import os, wave, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

ART = "Assets/GrillSort/Art"
OUT = f"{ART}/Generated"
AUD = "Assets/GrillSort/Audio"
os.makedirs(f"{OUT}/Shadows", exist_ok=True)
os.makedirs(AUD, exist_ok=True)

FOODS = "bacon beef broccoli chicken corn halloumi meatball mushroom onion pepper pineapple potato salmon sausage shrimp squid tomato zucchini".split()

def solid(size, rgb, alpha):
    return Image.merge("RGBA", (Image.new("L", size, rgb[0]), Image.new("L", size, rgb[1]), Image.new("L", size, rgb[2]), alpha))

# ---- v3 "deep shadow": food is ~170 px on a 1080 screen, sprites are 512 -> factor ~3
PAD = 100  # shadow canvas = 512 + 2*PAD = 712 (Unity sizes it foodW * 712/512)
for n in FOODS:
    for stage in ("Grilled", "Raw"):
        im = Image.open(f"{ART}/Food/{stage}/{n}.png").convert("RGBA")
        base = Image.new("RGBA", (im.width + PAD * 2, im.height + PAD * 2), (0, 0, 0, 0))
        base.alpha_composite(im, (PAD, PAD))
        a = base.getchannel("A").filter(ImageFilter.MaxFilter(27)).filter(ImageFilter.GaussianBlur(30))
        sh = solid(base.size, (12, 5, 0), a.point(lambda v: int(v * 0.95)))
        suffix = "" if stage == "Grilled" else "_raw"
        sh.resize((256, 256), Image.LANCZOS).save(f"{OUT}/Shadows/{n}{suffix}.png")

# ---- darker grill (brightness x0.62, saturation x0.7)
g = Image.open(f"{ART}/Board/grill.png").convert("RGBA"); ga = g.getchannel("A")
gd = ImageEnhance.Brightness(ImageEnhance.Color(g.convert("RGB")).enhance(0.7)).enhance(0.62).convert("RGBA"); gd.putalpha(ga)
gd.save(f"{OUT}/grill_dark.png")

# ---- soft radial glow (white, tinted in Unity)
S = 256; y, x = np.mgrid[0:S, 0:S]; r = np.sqrt((x - S / 2 + .5) ** 2 + (y - S / 2 + .5) ** 2) / (S / 2)
al = np.clip(1 - r, 0, 1) ** 1.6
Image.merge("RGBA", [Image.new("L", (S, S), 255)] * 3 + [Image.fromarray((al * 255).astype(np.uint8))]).save(f"{OUT}/glow.png")

# ---- soft rounded shadow (grill / tiles)
W, H, P = 160, 400, 40
sh = Image.new("RGBA", (W + 2 * P, H + 2 * P), (0, 0, 0, 0))
ImageDraw.Draw(sh).rounded_rectangle((P, P, P + W, P + H), 26, fill=(20, 8, 0, 255))
sh.filter(ImageFilter.GaussianBlur(16)).save(f"{OUT}/soft_rect.png")

# ---- red count badge (white ring, red disc) and plain white disc
B = 128
bd = Image.new("RGBA", (B, B), (0, 0, 0, 0)); d = ImageDraw.Draw(bd)
d.ellipse((2, 2, B - 3, B - 3), fill=(255, 255, 255, 255)); d.ellipse((14, 14, B - 15, B - 15), fill=(232, 52, 52, 255))
bd.save(f"{OUT}/badge.png")
wd = Image.new("RGBA", (B, B), (0, 0, 0, 0)); ImageDraw.Draw(wd).ellipse((2, 2, B - 3, B - 3), fill=(255, 255, 255, 255))
wd.save(f"{OUT}/disc.png")
# rounded white panel for tips / dim cards (9-sliced in Unity, border 48)
rp = Image.new("RGBA", (160, 160), (0, 0, 0, 0)); ImageDraw.Draw(rp).rounded_rectangle((0, 0, 159, 159), 48, fill=(255, 255, 255, 255))
rp.save(f"{OUT}/rounded.png")

# ---- progress bar: empty track + green fill pill cut from bar_progress
bp = Image.open(f"{ART}/UI/Panels/bar_progress.png").convert("RGBA"); arr = np.array(bp).astype(int)
R_, G_, B_ = arr[..., 0], arr[..., 1], arr[..., 2]
green = (G_ > R_ + 30) & (G_ > B_ + 30) & (arr[..., 3] > 0)
gm = Image.fromarray((green * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))
ys, xs = np.nonzero(np.array(gm))
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
fill = bp.crop((x0, y0, x1 + 1, y1 + 1)); fa = np.array(fill); m = np.array(gm.crop((x0, y0, x1 + 1, y1 + 1))) > 0
fa[..., 3] = np.where(m, fa[..., 3], 0); Image.fromarray(fa).save(f"{OUT}/bar_fill.png")
# paint the green area with the dark interior colour sampled from the right half
interior = arr[(y0 + y1) // 2, x1 + (bp.width - x1) // 2]
tr = arr.copy(); mm = np.array(gm) > 0
for c in range(3): tr[..., c] = np.where(mm, interior[c], tr[..., c])
Image.fromarray(tr.astype(np.uint8)).save(f"{OUT}/bar_track.png")
print("fill rect", x0, y0, x1, y1, "of", bp.size)

# ---- placeholder SFX (synthesized, short, soft). Replace with real sounds later.
SR = 44100
def save(name, sig):
    sig = np.clip(sig, -1, 1); data = (sig * 32000).astype(np.int16)
    with wave.open(f"{AUD}/{name}.wav", "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
def t(d): return np.arange(int(SR * d)) / SR
def env(n, a=0.005, rel=None):
    e = np.ones(n); k = max(1, int(SR * a)); e[:k] = np.linspace(0, 1, k)
    if rel: e *= np.exp(-np.arange(n) / (SR * rel))
    return e
rng = np.random.default_rng(7)
def tone(f0, f1, d, rel, shape="sine"):
    tt = t(d); f = np.linspace(f0, f1, len(tt)); ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) if shape == "sine" else np.sign(np.sin(ph)) * 0.4
    return s * env(len(tt), rel=rel)
def seq(parts, total):
    out = np.zeros(int(SR * total))
    for delay, sig in parts:
        i = int(SR * delay); n = min(len(sig), len(out) - i); out[i:i + n] += sig[:n]
    return out
def lowpass(x, k):
    return np.convolve(x, np.ones(k) / k, mode="same")
save("pick", 0.5 * tone(520, 900, 0.12, 0.04))
save("drop", 0.6 * tone(260, 140, 0.16, 0.05) + 0.15 * lowpass(rng.standard_normal(int(SR * .16)), 6) * env(int(SR * .16), rel=0.02))
save("invalid", 0.35 * tone(180, 150, 0.22, 0.08, "square"))
n = int(SR * 1.0); hiss = rng.standard_normal(n) - lowpass(rng.standard_normal(n), 3)
crack = (rng.random(n) > 0.9993) * rng.standard_normal(n) * 2
save("sizzle", 0.22 * (hiss + lowpass(crack, 4)) * env(n, 0.03) * np.linspace(1, 0, n) ** 0.5)
save("serve", seq([(i * 0.07, 0.35 * tone(f, f, 0.35, 0.12)) for i, f in enumerate([660, 880, 1320])], 0.6))
save("coin", 0.4 * np.concatenate([tone(1320, 1320, 0.06, 0.03), tone(1760, 1760, 0.22, 0.08)]))
save("win", seq([(i * 0.11, 0.3 * tone(f, f, 0.5, 0.2)) for i, f in enumerate([523, 659, 784, 1046, 1318])], 1.1))
save("click", 0.4 * tone(800, 600, 0.05, 0.015))
print("done")
