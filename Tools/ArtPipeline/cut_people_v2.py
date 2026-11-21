"""Customers v2 (Aaron 2026-11-21: "Characters: the style feels a bit off from the rest of the art, and the proportions
vary (some heads are bigger than others)").

1. Style: 8 new Figma generations in the food's style (bold dark-brown outline, cel shading, chunky shapes), same
   people as before. Prompt: Docs/ArtPrompts.md "Customers v2". Sources: ArtSource/Cartoon/peopleV2/people_<name>.png
2. Proportions: after the cut, every person is scaled so the FACE (cheek outline to cheek outline, no ears) has the
   same width (FACE) and moved so the eye line sits at the same height (EYE_Y). Face widths are measured by hand once
   (FACE_PX: automatic attempts hit mouths, mustaches and glasses), eye lines and the face centre come from the pupils.
   FACE/EYE_Y are chosen so the tallest hair (curly's puff) still fits at the top and the shortest bust still reaches
   the counter (the bottom ~18% of the sprite is behind it).
   (Equal pupil distance was tried first: grandpa, grandma and the girl have wide-set eyes, so their heads came out small.)

Writes NEW files: Assets/GrillSort/Art/CustomersPeople_v2/<name>.png (512x512). CustomersPeople/ stays as the backup.
Prints the scale, head top and bust bottom per person.

Usage (repo root): python -I Tools/ArtPipeline/cut_people_v2.py      OVERWRITE=1 to replace.
Knobs: FACE (face width in px of the 512 canvas), EYE_Y (eye line in px from the top).
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "peopleV2")
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "CustomersPeople_v2")
NAMES = "grandpa ponytail redhead curly dad grandma pigtails quiff".split()
SIZE = 512
EDGE = 4   # transparent px kept around the sprite (atlas bleed)
FACE = float(os.environ.get("FACE", "196"))
EYE_Y = float(os.environ.get("EYE_Y", "284")) / SIZE
# face width in px of the cut figure (cut_cartoon.cut of the 1024 source), measured by hand on an overlay 2026-11-21
# (second pass: re-measured on the normalized sprites with a ruler; third pass by eye on the lineup, because how big
# a head LOOKS also depends on hair, ears and shoulders: grandpa +15%, pigtails +12%, curly -6%, quiff -5%)
FACE_PX = {"grandpa": 462, "ponytail": 484, "redhead": 381, "curly": 383, "dad": 403, "grandma": 445, "pigtails": 451, "quiff": 374}


def skin_mask(im):
    """Skin = warm (r > g > b), saturated but not neon (ribbons), not too dark (hair, outline)."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    lum = r * .3 + g * .59 + b * .11
    sat = (r - b) / np.maximum(r, 1)
    return (al > 200) & (r > g) & (g > b) & (r - b > 35) & (lum > 85) & (r > 120) & (sat < .72)


def eye_line(im):
    """Eye line y: the first band of rows (from the top) in the middle of the face with a solid run of eye-white
    pixels. Teeth and mustaches are lower down; hair highlights are thin."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    lum = r * .3 + g * .59 + b * .11
    white = (al > 200) & (lum > 232) & ((np.maximum(np.maximum(r, g), b) - np.minimum(np.minimum(r, g), b)) < 22)
    h, w = white.shape
    rows = white[:, int(w * .22):int(w * .78)].sum(1)
    y0, y1 = int(h * .22), int(h * .7)
    hits = [y for y in range(y0, y1) if rows[y] > w * .025]
    if not hits:
        return h * .42
    run = [hits[0]]
    for y in hits[1:]:
        if y - run[-1] > 3: break
        run.append(y)
    return (run[0] + run[-1]) / 2


# eye line (fraction of the cut figure's height) read by hand where eye_line() is fooled: grandpa's white brows,
# grandma's grey hair highlights (checked on an overlay, 2026-11-21)
EYE_OVERRIDE = {"grandpa": .373, "grandma": .50}


def pupils(im, ey):
    """Centres of the two pupils on the eye line: the two longest dark runs (outlines and glasses frames are thin)."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    h, w = a.shape[:2]
    best = []
    for y in range(int(ey) - 6, int(ey) + 7, 3):
        row = a[y]
        lum = row[:, 0] * .3 + row[:, 1] * .59 + row[:, 2] * .11
        dark = (lum < 95) & (row[:, 3] > 200)
        runs, x = [], int(w * .2)
        while x < int(w * .8):
            if dark[x]:
                x0 = x
                while x < w and dark[x]: x += 1
                runs.append((x - x0, (x0 + x) / 2))
            x += 1
        runs = sorted(runs, reverse=True)[:2]
        if len(runs) == 2 and runs[1][0] > w * .025:
            best.append(sorted(c for _, c in runs))
    if not best:
        return None
    best = np.median(np.array(best), 0)
    return float(best[0]), float(best[1])


def measure(im, name=""):
    """(eye distance px, eye-line y px, face centre x px)."""
    h, w = im.size[1], im.size[0]
    ey = EYE_OVERRIDE[name] * h if name in EYE_OVERRIDE else eye_line(im)
    p = pupils(im, ey)
    if p is None:
        return w * .2, ey, w / 2
    return p[1] - p[0], ey, (p[0] + p[1]) / 2


def main():
    os.makedirs(OUT, exist_ok=True)
    over = os.environ.get("OVERWRITE") == "1"
    for n in NAMES:
        out = os.path.join(OUT, n + ".png")
        if os.path.exists(out) and not over:
            print("skip (exists)", n); continue
        src = Image.open(os.path.join(SRC, f"people_{n}.png")).convert("RGBA")
        flat = Image.new("RGBA", src.size, (255, 255, 255, 255)); flat.alpha_composite(src)   # some downloads are already transparent
        fig = cc.cut(flat.convert("RGB"), False)   # cropped bust, straight bottom
        fw, ey, cx = measure(fig, n)
        s = FACE / FACE_PX[n]
        fig = fig.resize((round(fig.width * s), round(fig.height * s)), Image.LANCZOS)
        ex, eyy = cx * s, ey * s
        canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        x, y = round(SIZE / 2 - ex), round(EYE_Y * SIZE - eyy)
        canvas.paste(fig, (x, y), fig)
        # keep a transparent frame: the sprite atlas extends edge pixels into its padding, so a bust cut off by the
        # bottom edge bled into the sprite packed next to it (navy line above the HUD pills, 2026-11-21). The bottom
        # rows are hidden behind the counter anyway.
        a = np.asarray(canvas).copy()
        a[:EDGE, :, 3] = 0; a[-EDGE:, :, 3] = 0; a[:, :EDGE, 3] = 0; a[:, -EDGE:, 3] = 0
        canvas = Image.fromarray(a, "RGBA")
        canvas.save(out)
        bb = canvas.getbbox()
        print(f"{n:9s} pupils {fw:5.1f}px eye {ey:5.1f} (cut)  ->  x{s:.3f}  head top {bb[1]:3d}  bust bottom {y + fig.height:3d}")


if __name__ == "__main__":
    main()
