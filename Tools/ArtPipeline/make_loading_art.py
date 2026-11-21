"""Loading screen art + clean progress-bar track (2026-11-21, user: the loading screen is "too basic" next to the
client's other game, and the bar "has weird effects").

Writes NEW files (sources untouched). v2 adds Loading/keyart_default.png (painted full-screen scene), Loading/logo.png
(the "Grill Sort" logo image, a placeholder until the name is final) and Loading/mascot.png (sausage chef mascot):
  Assets/GrillSort/Art/Loading/hero_grill.png   theme-neutral hero: a sizzling BBQ grill loaded with skewers (Figma,
                                                ArtSource/Cartoon/ui/loading_hero_grill.png), shown over any theme
  Assets/GrillSort/Art/Loading/logo_plate.png   blank wooden sign board for the logo (Figma, loading_logo_plate.png);
                                                the title stays TEXT on top of it, since the game name isn't final
  Assets/GrillSort/Art/Generated/bar_track_clean.png
        bar_track.png had a flat "ghost" where the old fill was cut out (left ~55%): the left end cap is rebuilt by
        mirroring the right one and the straight middle is a clean column repeated. Used by the loading bar and the
        Orders bar.
Prompts: Docs/ArtPrompts.md "Loading screen". Usage (repo root): python -I Tools/ArtPipeline/make_loading_art.py
OVERWRITE=1 to replace.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "ui")
ART = os.path.join(ROOT, "Assets", "GrillSort", "Art")
OUT = os.path.join(ART, "Loading")
OVER = os.environ.get("OVERWRITE") == "1"


def cut_to(src, out, max_side, edge=4):
    if os.path.exists(out) and not OVER:
        print("skip (exists)", out); return
    raw = Image.open(src).convert("RGBA")
    flat = Image.new("RGBA", raw.size, (255, 255, 255, 255)); flat.alpha_composite(raw)   # some downloads are already transparent
    im = cc.cut(flat.convert("RGB"), True)
    s = max_side / max(im.size)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (im.width + edge * 2, im.height + edge * 2), (0, 0, 0, 0))
    canvas.alpha_composite(im, (edge, edge))
    canvas.save(out)
    print("wrote", os.path.relpath(out, ROOT), canvas.size)


def clean_track():
    out = os.path.join(ART, "Generated", "bar_track_clean.png")
    if os.path.exists(out) and not OVER:
        print("skip (exists)", out); return
    a = np.asarray(Image.open(os.path.join(ART, "Generated", "bar_track.png")).convert("RGBA"))
    h, w = a.shape[:2]
    flat = np.array([118, 49, 31, 255])
    row = a[40]
    ghost_end = max(x for x in range(w // 2 + 200) if (row[x] == flat).all())   # last flat column on the channel's top row
    cap = 130
    res = a.copy()
    res[:, :cap] = a[:, w - cap:][:, ::-1]                    # left end cap = mirrored right end cap
    clean = a[:, ghost_end + 60]                               # a clean column of the straight middle
    res[:, cap:ghost_end + 60] = clean[:, None, :]       # up to the source column itself: no seam
    Image.fromarray(res, "RGBA").save(out)
    print("wrote", os.path.relpath(out, ROOT), "ghost ended at x =", ghost_end)


def main():
    os.makedirs(OUT, exist_ok=True)
    cut_to(os.path.join(SRC, "loading_hero_grill.png"), os.path.join(OUT, "hero_grill.png"), 900)
    cut_to(os.path.join(SRC, "loading_logo_plate.png"), os.path.join(OUT, "logo_plate.png"), 1000)
    clean_track()
    # v2 (user: "still not able to match the level"): a painted full-screen key art, a real logo image and a mascot
    keyart()
    # logo v1 (yellow/red, user: didn't like the colour and style) is kept as logo_v1.png; the game uses option 1,
    # the two-tone orange "Grill" + turquoise "Sort" with a navy outline (FishUp style), picked 2026-11-21
    cut_to(os.path.join(SRC, "loading_logo_v1.png"), os.path.join(OUT, "logo_v1.png"), 1000)
    clear_trapped_white(os.path.join(OUT, "logo_v1.png"))
    # option 1 (two-tone on a flat light-blue slab: user, "plain blue background") kept as logo_opt1.png
    cut_to(os.path.join(SRC, "logo_opt1_twotone.png"), os.path.join(OUT, "logo_opt1.png"), 1000)
    clear_trapped_white(os.path.join(OUT, "logo_opt1.png"))
    # final (picked 2026-11-21): same colours, built like the client's FishUp! logo: tight navy outline hugging the
    # letters, no backing panel, a grill-lid + skewers topper behind the top
    cut_logo(os.path.join(SRC, "logo_v3_grilltopper.png"), os.path.join(OUT, "logo.png"), 1000)
    cut_to(os.path.join(SRC, "mascot_sausage_v1.png"), os.path.join(OUT, "mascot.png"), 520)


def cut_logo(src, out, max_side, edge=4):
    """Cut a logo without eating its white inner strokes: only the near-pure-white background reachable from the border
    goes, plus big pure-white pockets (> 2500 px) trapped between letters (clear_trapped_white also removed white
    stroke fringes, which left pink halos)."""
    from PIL import ImageFilter
    if os.path.exists(out) and not OVER:
        print("skip (exists)", out); return
    raw = Image.open(src).convert("RGBA")
    flat = Image.new("RGBA", raw.size, (255, 255, 255, 255)); flat.alpha_composite(raw)
    rgb = flat.convert("RGB"); a = np.asarray(rgb).astype(np.int16)
    white = a.min(axis=2) > 246
    border = np.zeros_like(white); border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    bg = cc.grow(border, white)
    for pts in cc.components(white & ~bg):
        if len(pts) > 2500:
            ys, xs = np.array(pts).T; bg[ys, xs] = True
    al = Image.fromarray((~bg).astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(.7))
    im = rgb.convert("RGBA"); im.putalpha(al); im = im.crop(im.getbbox())
    s = max_side / max(im.size)
    im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (im.width + edge * 2, im.height + edge * 2), (0, 0, 0, 0))
    canvas.alpha_composite(im, (edge, edge))
    canvas.save(out)
    print("wrote", os.path.relpath(out, ROOT), canvas.size)


def clear_trapped_white(path, min_px=1200):
    """The white background trapped between "Grill" and "Sort" can't be reached by the cut's flood fill from the
    border, so it stayed as a white blob (user 2026-11-21). Removes enclosed near-white areas bigger than min_px; the
    letters' shine highlights are much smaller and stay."""
    from PIL import ImageFilter
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).copy()
    white = (a[..., :3].min(-1) > 236) & (a[..., 3] > 0)
    removed = 0
    for pts in cc.components(white):
        if len(pts) < min_px:
            continue
        ys, xs = np.array(pts).T
        a[ys, xs, 3] = 0
        removed += len(pts)
    # soften the new edge (the white fringe next to the hole)
    hole = Image.fromarray(((a[..., 3] == 0) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
    fringe = (np.asarray(hole) > 0) & (a[..., :3].min(-1) > 200)
    a[fringe, 3] = 0
    Image.fromarray(a, "RGBA").save(path)
    print("cleared", removed, "trapped white px in", os.path.basename(path))


def keyart():
    """Full-screen loading scene. v1 painterly ("too realistic"), v2 flat sunset (and its calm pass: "faded out"). v3 was
    made from the client's FishUp! loading screen as reference (bright sky-blue backdrop, light sand, ONE food centerpiece,
    framing leaves, soft glossy cartoon). The user picked v3b (the red grill). Used as is, no colour pass.
    Writes Loading/keyart_default.png; keeps keyart_v1.png (painterly) as a backup."""
    out = os.path.join(OUT, "keyart_default.png")
    if os.path.exists(out) and not OVER:
        print("skip (exists)", out); return
    Image.open(os.path.join(SRC, "loading_keyart_v3b.png")).convert("RGB").save(out)
    print("wrote", os.path.relpath(out, ROOT))


if __name__ == "__main__":
    main()
