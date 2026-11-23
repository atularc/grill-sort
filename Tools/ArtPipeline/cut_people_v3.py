"""Customers v3 = the same 8 people with three moods (2026-11-23, review: the customers never react).

Each person is ONE Figma generation of an expression sheet (waiting | delighted | impatient side by side, prompt:
Docs/ArtPrompts.md "Customers v3 moods"), so the three faces are the same drawing. Sources:
ArtSource/Cartoon/peopleV3/moods_<name>.png (1536x1024).

1. The sheet is split into its three figures at the empty white columns, each is cut like the v2 busts
   (cut_cartoon.cut, straight bottom).
2. One scale per person (from the waiting face), so the three moods stay the same size. Every mood is placed with its
   straight bottom edge on the same line and its bust centred, so swapping moods doesn't make the person jump.
3. Across people the FACE is normalised like v2: every face the same width (FACE) and every eye line at EYE_Y.
   Face width = the widest skin run across the cheeks (measured on the waiting face); FACE_FIX nudges it by eye.

Writes NEW files: Assets/GrillSort/Art/CustomersPeople_v3/<name>.png (waiting), <name>_happy.png, <name>_angry.png
(512x512, 4 px transparent edge for the atlas). CustomersPeople_v2/ stays as the backup.
Usage (repo root): python -I Tools/ArtPipeline/cut_people_v3.py      OVERWRITE=1 to replace.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cut_cartoon as cc  # noqa: E402
import cut_people_v2 as v2  # noqa: E402

ROOT = cc.ROOT
SRC = os.path.join(ROOT, "ArtSource", "Cartoon", "peopleV3")
OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "CustomersPeople_v3")
NAMES = v2.NAMES
MOODS = ["", "_happy", "_angry"]
SIZE, EDGE = 512, 4
FACE = float(os.environ.get("FACE", "215"))
EYE_Y = float(os.environ.get("EYE_Y", "276")) / SIZE
# by-eye corrections of the measured face width (how big a head LOOKS also depends on hair, ears, shoulders)
FACE_FIX = {"grandpa": .92, "grandma": .94}   # look a little small next to the others
# hand-measured on a 50 px grid over the cut waiting figure (2026-11-23): automatic face widths hit beards, hair over
# the cheeks and darker skin. Cheek outline to cheek outline (no ears), and the pupils' height, in px of the cut figure.
FACE_PX = {"grandpa": 355, "ponytail": 338, "redhead": 322, "curly": 348, "dad": 354, "grandma": 385, "pigtails": 302, "quiff": 322}
EYE_PX = {"grandpa": 244, "ponytail": 374, "redhead": 322, "curly": 426, "dad": 317, "grandma": 385, "pigtails": 281, "quiff": 317}


def split3(sheet):
    """Three figures: split at the two widest white column bands between them."""
    a = np.asarray(sheet.convert("RGB")).astype(np.int16)
    ink = (a.min(axis=2) < 232).sum(axis=0) > 2
    w = len(ink)
    cuts = []
    for lo, hi in ((w * .2, w * .47), (w * .53, w * .8)):
        best, bx, x = 0, int((lo + hi) / 2), int(lo)
        while x < hi:
            if not ink[x]:
                x0 = x
                while x < hi and not ink[x]: x += 1
                if x - x0 > best: best, bx = x - x0, (x0 + x) // 2
            x += 1
        cuts.append(bx)
    xs = [0] + cuts + [w]
    return [sheet.crop((xs[i], 0, xs[i + 1], sheet.height)) for i in range(3)]


def skin_mask(im):
    """v2's skin mask, plus darker skin tones (curly): warm, opaque, not outline-dark, not saturated like ribbons."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    lum = r * .3 + g * .59 + b * .11
    dark_skin = (al > 200) & (r > g) & (g > b) & (r - b > 30) & (lum > 60) & (r > 95) & ((r - b) / np.maximum(r, 1) < .75)
    return v2.skin_mask(im) | dark_skin


def face_width(fig, eye_y):
    """Widest run of skin pixels on the rows between the eyes and the mouth."""
    skin = skin_mask(fig)
    h, w = skin.shape
    best = 0
    for y in range(int(eye_y), min(h, int(eye_y + h * .16)), 2):
        row = skin[y]
        x, cx = 0, w // 2
        # the run through the middle of the face (bridged over small gaps: eyes, glasses)
        xs = np.nonzero(row)[0]
        if len(xs) == 0: continue
        runs, start = [], xs[0]
        for a, b in zip(xs[:-1], xs[1:]):
            if b - a > 18: runs.append((start, a)); start = b
        runs.append((start, xs[-1]))
        for a, b in runs:
            if a <= cx + w * .15 and b >= cx - w * .15: best = max(best, b - a)
    return best


def main():
    os.makedirs(OUT, exist_ok=True)
    over = os.environ.get("OVERWRITE") == "1"
    for n in NAMES:
        if os.path.exists(os.path.join(OUT, n + ".png")) and not over:
            print("skip (exists)", n); continue
        src = Image.open(os.path.join(SRC, f"moods_{n}.png")).convert("RGBA")
        sheet = Image.new("RGBA", src.size, (255, 255, 255, 255)); sheet.alpha_composite(src)   # some downloads are already transparent
        sheet = sheet.convert("RGB")
        figs = [cc.cut(f, False) for f in split3(sheet)]
        base = figs[0]
        ey = EYE_PX[n] if n in EYE_PX else v2.eye_line(base)
        fw = (FACE_PX[n] if n in FACE_PX else face_width(base, ey)) * FACE_FIX.get(n, 1.0)
        s = FACE / max(fw, 1)
        # where the waiting face's eye line lands; every mood shares the bust bottom line
        y_bottom = round(EYE_Y * SIZE + (base.height - ey) * s)
        for mood, fig in zip(MOODS, figs):
            f = fig.resize((round(fig.width * s), round(fig.height * s)), Image.LANCZOS)
            canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
            # centre on the straight bottom edge (the bust), not the hair
            a = np.asarray(f)[..., 3]
            cols = np.nonzero(a[-3] > 128)[0]
            cx = (cols[0] + cols[-1]) / 2 if len(cols) else f.width / 2
            canvas.paste(f, (round(SIZE / 2 - cx), y_bottom - f.height), f)
            arr = np.asarray(canvas).copy()
            arr[:EDGE, :, 3] = 0; arr[-EDGE:, :, 3] = 0; arr[:, :EDGE, 3] = 0; arr[:, -EDGE:, 3] = 0
            Image.fromarray(arr, "RGBA").save(os.path.join(OUT, n + mood + ".png"))
        bb = Image.open(os.path.join(OUT, n + ".png")).getbbox()
        print(f"{n:9s} face {fw:5.0f}px  x{s:.3f}  head top {bb[1]:3d}  bust bottom {y_bottom:3d}")


if __name__ == "__main__":
    main()
