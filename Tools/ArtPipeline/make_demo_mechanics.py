"""Art for the demo mechanics (2026-11-26), drawn in code in the cartoon style (bold dark outline, cel shading).

  Generated/foil_wrap.png   512x512  food wrapped in crumpled foil with a "?" (Sort Streak's mystery flower, BBQ style)
  Generated/lock_chains.png 373x1818 chains wrapped around a tray (Sort Streak's locked-flowers vine, BBQ style)

Run: python -I Tools/ArtPipeline/make_demo_mechanics.py   (never overwrites unless OVERWRITE=1)
"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "GrillSort", "Art", "Generated")
OUTLINE = (52, 48, 58, 255)


def save(img, name):
    p = os.path.join(OUT, name)
    if os.path.exists(p) and os.environ.get("OVERWRITE") != "1":
        print("exists, skipped:", name)
        return
    img.save(p)
    print("wrote", name)


def foil():
    S = 4 * 512  # draw big, scale down for smooth edges
    rnd = random.Random(11)
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    cx, cy, R = S / 2, S / 2 + 20, S * 0.40
    # lumpy round silhouette (crumpled ball)
    pts = []
    n = 26
    for i in range(n):
        a = 2 * math.pi * i / n
        r = R * (1 + rnd.uniform(-0.06, 0.06))
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r * 0.92))
    d = ImageDraw.Draw(img)
    d.polygon(pts, fill=OUTLINE)
    inner = [(cx + (x - cx) * 0.955, cy + (y - cy) * 0.955) for x, y in pts]
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).polygon(inner, fill=255)
    # base silver with vertical light gradient
    base = Image.new("RGBA", (S, S))
    bp = base.load()
    for y in range(S):
        t = y / S
        c = int(232 - 70 * t)
        for x in range(0, S):
            bp[x, y] = (c - 6, c, min(255, c + 10), 255)
    # crumple facets: random triangles lighter / darker
    fac = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fac)
    grid = []
    step = S // 11
    for gy in range(-1, 13):
        row = []
        for gx in range(-1, 13):
            row.append((gx * step + rnd.uniform(-step * .35, step * .35), gy * step + rnd.uniform(-step * .35, step * .35)))
        grid.append(row)
    for gy in range(len(grid) - 1):
        for gx in range(len(grid[0]) - 1):
            a, b, c2, e = grid[gy][gx], grid[gy][gx + 1], grid[gy + 1][gx], grid[gy + 1][gx + 1]
            for tri in ((a, b, c2), (b, e, c2)):
                v = rnd.choice([-38, -22, -10, 0, 14, 30, 44])
                col = (255, 255, 255, v * 3) if v > 0 else (40, 46, 60, -v * 3)
                fd.polygon(tri, fill=col)
                fd.line(list(tri) + [tri[0]], fill=(90, 96, 110, 70), width=6)
    base = Image.alpha_composite(base, fac)
    # rim shadow + top-left shine
    shade = Image.new("L", (S, S), 0)
    ImageDraw.Draw(shade).ellipse([cx - R * 1.3, cy - R * 0.2, cx + R * 1.3, cy + R * 1.6], fill=110)
    shade = shade.filter(ImageFilter.GaussianBlur(S * 0.06))
    dark = Image.new("RGBA", (S, S), (30, 34, 48, 255))
    base = Image.composite(dark, base, shade.point(lambda v: int(v * 0.55)))
    shine = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shine).ellipse([cx - R * .62, cy - R * .78, cx - R * .05, cy - R * .38], fill=(255, 255, 255, 170))
    shine = shine.filter(ImageFilter.GaussianBlur(S * 0.018))
    base = Image.alpha_composite(base, shine)
    img.paste(base, (0, 0), mask)
    # twisted top knot (a wrap's gathered end)
    d = ImageDraw.Draw(img)
    kx, ky = cx + R * 0.05, cy - R * 0.98
    knot = [(kx - R * .2, ky + R * .12), (kx - R * .28, ky - R * .2), (kx - R * .05, ky - R * .1),
            (kx + R * .08, ky - R * .26), (kx + R * .24, ky - R * .06), (kx + R * .18, ky + R * .14)]
    d.polygon(knot, fill=OUTLINE)
    kin = [(kx + (x - kx) * .82, ky + (y - ky) * .82 + 6) for x, y in knot]
    d.polygon(kin, fill=(214, 220, 232, 255))
    # big "?" with a dark outline
    font = None
    for f in ("C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/segoeuib.ttf"):
        if os.path.exists(f):
            font = ImageFont.truetype(f, int(R * 1.15)); break
    txt = "?"
    bb = d.textbbox((0, 0), txt, font=font)
    tx = cx - (bb[2] - bb[0]) / 2 - bb[0]
    ty = cy - (bb[3] - bb[1]) / 2 - bb[1] + R * 0.04
    d.text((tx, ty), txt, font=font, fill=(255, 255, 255, 255), stroke_width=int(S * 0.022), stroke_fill=OUTLINE)
    img = img.resize((512, 512), Image.LANCZOS)
    save(img, "foil_wrap.png")


def link(d, x, y, w, h, ang, face):
    """One chain link (oval ring) at (x, y), rotated by ang (deg). face=True: seen flat (hole), else edge-on."""
    def rot(px, py):
        a = math.radians(ang)
        return (x + px * math.cos(a) - py * math.sin(a), y + px * math.sin(a) + py * math.cos(a))
    def oval(rw, rh, n=40):
        return [rot(rw * math.cos(2 * math.pi * i / n), rh * math.sin(2 * math.pi * i / n)) for i in range(n)]
    if face:
        d.polygon(oval(w / 2 + 12, h / 2 + 12), fill=OUTLINE)
        d.polygon(oval(w / 2, h / 2), fill=(176, 182, 196, 255))
        d.polygon(oval(w / 2 - 7, h / 2 - 9), fill=(214, 220, 232, 255))
        d.polygon(oval(w / 2 - 34, h / 2 - 36), fill=OUTLINE)
        d.polygon(oval(w / 2 - 44, h / 2 - 46), fill=(0, 0, 0, 0))
    else:
        d.polygon(oval(w / 2 + 12, 34), fill=OUTLINE)
        d.polygon(oval(w / 2, 22), fill=(150, 156, 172, 255))
        d.polygon(oval(w / 2 - 10, 9), fill=(222, 228, 240, 255))


def chains():
    W, H = 373 * 2, 1818 * 2
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # three chain bands wrapped across the tray, slightly slanted, alternating direction
    bands = [(0.14, -12), (0.38, 12), (0.64, -12), (0.88, 12)]
    lw, lh = 360, 230
    for fy, ang in bands:
        y0 = H * fy
        n = 4
        for i in range(n):
            t = i / (n - 1)
            x = -30 + t * (W + 60)
            y = y0 + (t - .5) * math.tan(math.radians(ang)) * W
            link(d, x, y, lw, lh, ang, i % 2 == 0)
    img = img.resize((373, 1818), Image.LANCZOS)
    save(img, "lock_chains_v2.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    foil()
    chains()
