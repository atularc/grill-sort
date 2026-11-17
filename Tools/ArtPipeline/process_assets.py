# Turns the AI originals (*-hd folders, on white or black) into transparent game-ready PNGs.
# Run from grill-mockup/source:  python process_assets.py
import os, numpy as np
from PIL import Image, ImageDraw, ImageFilter

R = ".."

def alpha_white(a, thresh=236):
    """Alpha mask: background = near-white region connected to the image border."""
    near = (a.min(axis=2) >= thresh).astype(np.uint8) * 255
    m = Image.fromarray(near, "L").copy()
    W, H = m.size
    seeds = [(x, 0) for x in range(0, W, 8)] + [(x, H - 1) for x in range(0, W, 8)] + \
            [(0, y) for y in range(0, H, 8)] + [(W - 1, y) for y in range(0, H, 8)]
    for s in seeds:
        if m.getpixel(s) == 255: ImageDraw.floodfill(m, s, 128)
    alpha = np.where(np.asarray(m) == 128, 0, 255).astype(np.uint8)
    al = Image.fromarray(alpha, "L").filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    A = np.asarray(al).astype(np.float32) / 255.0
    k = A[..., None]
    with np.errstate(divide="ignore", invalid="ignore"):
        un = (a - (1 - k) * 255.0) / np.maximum(k, 1e-3)   # remove white fringe
    rgb = np.where(k > 0.02, np.clip(un, 0, 255), 0)
    return np.dstack([rgb, A * 255]).astype(np.uint8)

def alpha_black(a):
    """For glowing VFX on black: brightness becomes alpha, colour is un-premultiplied."""
    A = a.max(axis=2) / 255.0
    A = np.where(A < 0.06, 0, (A - 0.06) / 0.94)
    k = np.maximum(A[..., None], 1e-3)
    rgb = np.clip(a / 255.0 / np.maximum(a.max(axis=2, keepdims=True) / 255.0, 1e-3), 0, 1) * 255
    return np.dstack([rgb, A * 255]).astype(np.uint8)

def load(p): return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)

def trim(img, pad=0.04, square=False, size=None):
    img = img.crop(img.getbbox())
    p = int(max(img.size) * pad)
    w, h = img.width + 2 * p, img.height + 2 * p
    if square: w = h = max(w, h)
    c = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    c.paste(img, ((w - img.width) // 2, (h - img.height) // 2), img)
    if size:
        s = size / max(c.size)
        c = c.resize((max(1, round(c.width * s)), max(1, round(c.height * s))), Image.LANCZOS)
    return c

def runs(mask1d, min_gap):
    """Return [start, end) spans of True, merging spans separated by gaps < min_gap."""
    idx = np.flatnonzero(mask1d)
    if not len(idx): return []
    spans, s, prev = [], idx[0], idx[0]
    for i in idx[1:]:
        if i - prev > min_gap: spans.append((s, prev + 1)); s = i
        prev = i
    spans.append((s, prev + 1)); return spans

def split(rgba, min_gap=40):
    """Split a sprite sheet into items: rows by empty horizontal bands, then columns inside each row."""
    A = rgba[..., 3] > 20
    out = []
    for y0, y1 in runs(A.any(axis=1), min_gap):
        for x0, x1 in runs(A[y0:y1].any(axis=0), min_gap):
            out.append(Image.fromarray(rgba[y0:y1, x0:x1], "RGBA"))
    return out

def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True); img.save(path, optimize=True); print("  ", os.path.relpath(path, R), img.size)

# 1. Food: 512px square sprites
print("food")
for f in sorted(os.listdir(f"{R}/food-hd")):
    save(trim(Image.fromarray(alpha_white(load(f"{R}/food-hd/{f}")), "RGBA"), 0.03, True, 512), f"{R}/food/{f}")

# 2. Customers: 512px (higher threshold keeps white fur)
print("customers")
for f in sorted(os.listdir(f"{R}/customers-hd")):
    save(trim(Image.fromarray(alpha_white(load(f"{R}/customers-hd/{f}"), 246), "RGBA"), 0.02, True, 512), f"{R}/customers/{f}")

# 3. Board
print("board")
save(trim(Image.fromarray(alpha_white(load(f"{R}/board-hd/grill.png")), "RGBA"), 0.0), f"{R}/board/grill.png")
save(trim(Image.fromarray(alpha_white(load(f"{R}/board-hd/skewer.png")), "RGBA"), 0.0), f"{R}/board/skewer.png")
save(Image.open(f"{R}/board-hd/background.png").convert("RGB"), f"{R}/board/background.png")

# 4. UI sheets -> single items
print("ui")
SHEETS = {
    "sheet_boosters": ["icon_undo", "icon_hint", "icon_shuffle", "icon_add_skewer"],
    "sheet_icons":    ["icon_coin", "icon_star", "icon_heart", "icon_check"],
    "sheet_system":   ["btn_pause", "btn_settings", "icon_lock", "icon_gift"],
    "sheet_buttons":  ["btn_green", "btn_orange", "btn_red", "btn_tile_yellow"],
    "sheet_panels":   ["bubble_order", "bar_hud", "ribbon_red", "bar_progress"],
    "sheet_words":    ["word_perfect", "word_yummy", "word_sizzling", "word_level_complete"],
}
for sheet, names in SHEETS.items():
    items = split(alpha_white(load(f"{R}/ui-hd/{sheet}.png")))
    items = [i for i in items if i.width * i.height > 2500]
    if len(items) != len(names): print("  !! ", sheet, "found", len(items), "items")
    for img, n in zip(items, names): save(trim(img, 0.02), f"{R}/ui/{n}.png")
save(trim(Image.fromarray(alpha_white(load(f"{R}/ui-hd/popup.png")), "RGBA"), 0.0), f"{R}/ui/popup_panel.png")

print("vfx")
items = [i for i in split(alpha_black(load(f"{R}/ui-hd/sheet_vfx.png")), 30) if i.width * i.height > 2500]
for img, n in zip(items, ["fx_flame", "fx_smoke", "fx_sparkle", "fx_steam"]): save(trim(img, 0.04), f"{R}/vfx/{n}.png")
if len(items) != 4: print("  !! vfx found", len(items))
