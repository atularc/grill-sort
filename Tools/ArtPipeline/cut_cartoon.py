"""Cut the cartoon food / people art (ArtSource/Cartoon/single/*.png, one 1024 image per item) into game sprites.

Usage (from the repo root):  python -I Tools/ArtPipeline/cut_cartoon.py
Needs Pillow + numpy only. Set OVERWRITE=1 to replace existing sprites.
STYLE=FINAL cuts the game's food (style B, shine toned down) into Art/FoodFinal/Grilled/ + Generated/ShadowsFinal/.
STYLE=C cuts style C (style B sources + grill pass) into Art/FoodCartoonC/ + Generated/ShadowsCartoonC/.
STYLE=B cuts only the food style B set (SEAR=1 adds the rejected sear pass; off by default) (flatter cartoon, 2026-11-18): ArtSource/Cartoon/singleB/ ->
Art/FoodCartoonB/Grilled/ + Generated/ShadowsCartoonB/. Style A (default) is the first cartoon set.

- White background removed by growing the white region from the image border.
- Enclosed background gaps (e.g. a ring's centre) must be listed in HOLES; there is no automatic hole finder.
- People: busts are cut straight across, so white clothing touching the cut line is kept.
- Stray specks are dropped (keeps the biggest blob + big parts).
- Output: 512x512, centred, ~3% padding (same as the old Food/ and Customers/ sprites).
The first 2x2 sheets (ArtSource/Cartoon/sheet_*.png) were the style test; the singles replaced them for quality.
"""
import os
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "ArtSource", "Cartoon")
STYLE_FINAL = os.environ.get("STYLE", "").upper() == "FINAL"   # style B sources + less shine = the game's food
STYLE_C = os.environ.get("STYLE", "").upper() == "C"   # style B sources + grill pass
STYLE_B = os.environ.get("STYLE", "").upper() == "B" or STYLE_C or STYLE_FINAL
SINGLE = os.path.join(SRC, "singleB" if STYLE_B else "single")
FOOD_OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "FoodFinal" if STYLE_FINAL else "FoodCartoonC" if STYLE_C else "FoodCartoonB" if STYLE_B else "FoodCartoon", "Grilled")
PEOPLE_OUT = os.path.join(ROOT, "Assets", "GrillSort", "Art", "CustomersPeople")

FOODS = "bacon beef broccoli chicken corn halloumi meatball mushroom onion pepper pineapple potato salmon sausage shrimp squid tomato zucchini".split()
PEOPLE = "grandpa ponytail redhead curly dad grandma pigtails quiff".split()
# enclosed background gaps the automatic cut can't tell from white clothing: (x, y) in the 1024 source
HOLES = {}
SIZE, PAD = 512, 0.03


def grow(seed, allowed):
    reg = seed & allowed
    while True:
        g = reg.copy()
        g[1:, :] |= reg[:-1, :]; g[:-1, :] |= reg[1:, :]; g[:, 1:] |= reg[:, :-1]; g[:, :-1] |= reg[:, 1:]
        g &= allowed
        if (g == reg).all():
            return reg
        reg = g


def components(mask):
    """Label 4-connected components; returns list of (area, pixel index arrays)."""
    h, w = mask.shape
    seen = np.zeros_like(mask)
    out = []
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if seen[y0, x0]:
            continue
        q = deque([(y0, x0)]); seen[y0, x0] = True; pts = []
        while q:
            y, x = q.popleft(); pts.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True; q.append((ny, nx))
        out.append(pts)
    return out


def cut(cell, food, holes=()):
    a = np.asarray(cell.convert("RGB")).astype(np.int16)
    white = a.min(axis=2) > 232
    border = np.zeros_like(white)
    border[0, :] = border[-1, :] = border[:, 0] = border[:, -1] = True
    if food:
        bg = grow(border, white)
    else:
        # busts are cut straight across: everything below the cut line is background, and white
        # clothing touching the cut line must not be flooded from below
        rows = np.nonzero((~white).sum(axis=1) > 40)[0]
        yb = rows.max() if len(rows) else white.shape[0] - 1
        allowed = white.copy(); allowed[yb:, :] = False
        border[yb:, :] = False
        bg = grow(border, allowed)
        bg[yb + 1:, :] = True
    for x, y in holes:
        seed = np.zeros_like(white); seed[y, x] = True
        bg |= grow(seed, white)
    if False:  # automatic hole finder: off (it ate white highlights); list real holes in HOLES instead
        pure = (a.min(axis=2) > 244) & ~bg
        for pts in components(pure[::2, ::2]):
            if len(pts) > 600:  # in half-res pixels
                ys, xs = np.array(pts).T * 2
                seed = np.zeros_like(white); seed[ys, xs] = True
                bg |= grow(seed, white)
    fg = ~bg
    # drop leaked neighbour bits: keep blobs at least 4% of the biggest
    small = fg[::2, ::2]
    comps = sorted(components(small), key=len, reverse=True)
    keep = np.zeros_like(small)
    for pts in comps:
        if len(pts) >= 0.04 * len(comps[0]):
            ys, xs = np.array(pts).T; keep[ys, xs] = True
    keep = np.repeat(np.repeat(keep, 2, axis=0), 2, axis=1)[: fg.shape[0], : fg.shape[1]]
    fg &= np.asarray(Image.fromarray(keep.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(5))) > 0
    alpha = Image.fromarray(fg.astype(np.uint8) * 255)
    alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.7))
    # un-premultiply the white fringe on soft edges
    rgb = a.astype(np.float32)
    al = np.asarray(alpha).astype(np.float32)[..., None] / 255.0
    rgb = np.where(al > 0.05, (rgb - 255 * (1 - al)) / np.maximum(al, 0.05), rgb)
    out = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).convert("RGBA")
    out.putalpha(alpha)
    return out.crop(out.getbbox())


# --- style B "sear": the AI draws grill marks as flat ink lines; this burns them in (2026-11-18, user: "no burned effect").
# Finds the dark stripes inside the food, softens their edges into a charred core, browns a wide caramel band
# around them, lightly toasts pale areas and the rim. Cartoon look kept (no texture added).
def blur(m, r):
    return np.asarray(Image.fromarray((np.clip(m,0,1)*255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32)/255
def sear(im, halo=0.75, rim=0.15, toast=0.35):
    a=np.asarray(im.convert("RGBA")).astype(np.float32)/255; rgb=a[...,:3]; al=a[...,3]
    inner=np.asarray(Image.fromarray((al>0.5).astype(np.uint8)*255).filter(ImageFilter.MinFilter(15)))>0
    lum=rgb@np.array([.3,.59,.11]); 
    stripe=(lum<0.30)&inner&(rgb[...,0]>=rgb[...,2]-0.02)
    # keep only big blobs (stripes), drop small dark details
    st=blur(stripe.astype(np.float32),1.0)>0.5
    near=blur(np.asarray(Image.fromarray(st.astype(np.uint8)*255).filter(ImageFilter.MaxFilter(13)))/255.0,11)
    w=np.clip(near*1.5,0,1)*halo*al
    # browned band right next to the stripe is strongest, fades out
    w=np.where(st,0,w)
    w=w*np.clip(lum/0.4,0.35,1.0)  # already-dark food (beef, meatball) browns less
    burnt=rgb*np.array([0.62,0.42,0.24])  # caramel/char multiply
    out=rgb*(1-w[...,None])+burnt*w[...,None]
    # soft charred core: deepen stripes, slight warm edge glow
    # soften the stripe edge so it reads as a sear, not an ink line: blend stripe into the browned band
    soft=blur(st.astype(np.float32),2.2)
    core=rgb*np.array([0.42,0.25,0.14])
    out=out*(1-soft[...,None])+np.minimum(core,np.array([0.30,0.15,0.07]))*soft[...,None]
    # gentle toast on pale/light areas (halloumi, potato, zucchini centre)
    light=np.clip((lum-0.6)/0.3,0,1)*inner*toast
    out=out*(1-light[...,None])+out*np.array([0.93,0.80,0.6])*light[...,None]
    # toasted rim: darken near silhouette edge a bit
    edge=1-blur(al>0.5, 18); r=np.clip(edge*2,0,1)*rim*al
    out=out*(1-r[...,None])+out*np.array([0.7,0.5,0.32])*r[...,None]
    res=np.dstack([np.clip(out,0,1),al]); return Image.fromarray((res*255).astype(np.uint8),"RGBA")

# --- style C "grill" pass (2026-11-18): style B + a grilled look without darkening. Only THIN dark bands count as
# grill marks (big dark areas like zucchini skin / beef crust are excluded). Adds an amber caramel glow around the
# marks at the pixel's own brightness, a chocolate-red mark colour instead of flat ink, and a glossy pressed-in lip.
def shift(m, dx, dy):
    o=np.zeros_like(m); h,w=m.shape
    o[max(dy,0):h+min(dy,0), max(dx,0):w+min(dx,0)] = m[max(-dy,0):h+min(-dy,0), max(-dx,0):w+min(-dx,0)]; return o
def grill(im, amber=0.75, bevel=0.75):
    a=np.asarray(im.convert("RGBA")).astype(np.float32)/255; rgb=a[...,:3]; al=a[...,3]
    inner=np.asarray(Image.fromarray((al>0.5).astype(np.uint8)*255).filter(ImageFilter.MinFilter(15)))>0
    lum=rgb@np.array([.3,.59,.11])
    dark=(lum<0.30)&inner&(rgb[...,0]>=rgb[...,2]-0.02)
    # grill marks are THIN dark bands: remove big dark areas (zucchini skin, beef crust) with a morphological opening
    D=Image.fromarray(dark.astype(np.uint8)*255)
    big=np.asarray(D.filter(ImageFilter.MinFilter(31)).filter(ImageFilter.MaxFilter(35)))>0
    big=np.asarray(Image.fromarray(big.astype(np.uint8)*255).filter(ImageFilter.MaxFilter(5)))>0
    st=blur((dark&~big).astype(np.float32),1.0)>0.5
    # drop tiny dark bits (outlines of details, droplets): keep only long blobs
    st=np.asarray(Image.fromarray(st.astype(np.uint8)*255).filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5)))>0
    stf=st.astype(np.float32)
    # 1) caramel glow: hue-tint toward amber around marks, keeping brightness (no darkening)
    near=blur(np.asarray(Image.fromarray(st.astype(np.uint8)*255).filter(ImageFilter.MaxFilter(11)))/255.0,9)
    w=np.clip(near*1.4,0,1)*(1-stf)*amber*al*inner
    amberc=np.array([1.0,0.62,0.18])
    tint=amberc*np.maximum(lum,0.35)[...,None]*1.25   # amber at the pixel's own brightness
    out=rgb*(1-w[...,None])+np.clip(tint,0,1)*w[...,None]
    # 2) mark colour: dark chocolate-red core instead of flat ink, warm at the edges
    soft=blur(stf,1.6); core=blur(np.asarray(Image.fromarray(st.astype(np.uint8)*255).filter(ImageFilter.MinFilter(5)))/255.0,1.5)
    edgec=np.array([0.45,0.20,0.07]); corec=np.array([0.20,0.08,0.03])
    markc=edgec*(1-core[...,None])+corec*core[...,None]
    out=out*(1-soft[...,None])+markc*soft[...,None]
    # 3) pressed-in bevel: bright glossy lip on the upper side of each mark, soft shade below
    lip=np.clip(shift(stf,0,-4)-stf,0,1); lip=blur(lip,1.3)*bevel*inner
    out=out+(np.array([1.0,0.92,0.75])-out)*lip[...,None]*0.65
    res=np.dstack([np.clip(out,0,1),al]); return Image.fromarray((res*255).astype(np.uint8),"RGBA")

# --- FINAL food (2026-11-18): Aaron chose style B and asked to "tone down the shine so they look more natural and
# tasty". Big white specular streaks are pulled toward the surrounding food colour (measured over a wide area, so
# large streaks are caught too), leaving a faint sheen. Pale foods keep their white parts (only spots much brighter
# AND paler than their surroundings count).
def matte(im, k=0.85):
    """Tone down glossy specular highlights: near-white, low-saturation spots inside the food are pulled toward the
    surrounding food colour (a large median of the non-highlight pixels), keeping a faint soft sheen."""
    a=np.asarray(im.convert("RGBA")).astype(np.float32)/255; rgb=a[...,:3]; al=a[...,3]
    mx=rgb.max(-1); mn=rgb.min(-1); sat=(mx-mn)/np.maximum(mx,1e-3); lum=rgb@np.array([.3,.59,.11])
    inner=np.asarray(Image.fromarray((al>0.5).astype(np.uint8)*255).filter(ImageFilter.MinFilter(7)))>0
    # highlight = bright and much paler than its surroundings
    base=Image.fromarray((rgb*255).astype(np.uint8))
    small=base.resize((128,128),Image.BILINEAR)   # median over a wide area (~45 px at 512) so big streaks are measured against the food, not themselves
    surround=np.asarray(small.filter(ImageFilter.MedianFilter(11)).resize(base.size,Image.BILINEAR)).astype(np.float32)/255
    slum=surround@np.array([.3,.59,.11])
    hl=np.clip(((lum-slum)-0.08)/0.16,0,1)*np.clip((0.6-sat)/0.35,0,1)*inner
    hl=np.asarray(Image.fromarray((hl*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(2.5))).astype(np.float32)/255
    w=np.clip(hl,0,1)*k
    target=surround*1.08  # slightly lighter than surroundings = soft sheen, not a white glare
    out=rgb*(1-w[...,None])+np.clip(target,0,1)*w[...,None]
    res=np.dstack([np.clip(out,0,1),al]); return Image.fromarray((res*255).astype(np.uint8),"RGBA")

def place(item):
    inner = int(SIZE * (1 - 2 * PAD))
    s = inner / max(item.size)
    item = item.resize((max(1, round(item.width * s)), max(1, round(item.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(item, ((SIZE - item.width) // 2, (SIZE - item.height) // 2))
    return canvas


def run(names, prefix, outdir, food):
    os.makedirs(outdir, exist_ok=True)
    for name in names:
        path = os.path.join(outdir, name + ".png")
        if os.path.exists(path) and os.environ.get("OVERWRITE") != "1":
            print("skip (exists)", path); continue
        src = Image.open(os.path.join(SINGLE, f"{prefix}_{name}.png"))
        out = place(cut(src, food, HOLES.get(name, ())))
        if STYLE_B and food and os.environ.get("SEAR") == "1":  # sear pass: tried 2026-11-18, user: "just got darker", reverted
            out = sear(out)
        if STYLE_C and food:
            out = grill(out)
        if STYLE_FINAL and food:
            out = matte(out)
        out.save(path)
        print("wrote", os.path.relpath(path, ROOT))


def bake_shadows():
    """v3 deep shadow, same recipe as bake_game_fx.py, into Generated/ShadowsCartoon/."""
    out = os.path.join(ROOT, "Assets", "GrillSort", "Art", "Generated", "ShadowsFinal" if STYLE_FINAL else "ShadowsCartoonC" if STYLE_C else "ShadowsCartoonB" if STYLE_B else "ShadowsCartoon")
    os.makedirs(out, exist_ok=True)
    pad = 100
    for f in sorted(os.listdir(FOOD_OUT)):
        if not f.endswith(".png"):
            continue
        im = Image.open(os.path.join(FOOD_OUT, f)).convert("RGBA")
        base = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
        base.alpha_composite(im, (pad, pad))
        a = base.getchannel("A").filter(ImageFilter.MaxFilter(27)).filter(ImageFilter.GaussianBlur(30))
        sh = Image.merge("RGBA", (Image.new("L", base.size, 12), Image.new("L", base.size, 5),
                                  Image.new("L", base.size, 0), a.point(lambda v: int(v * 0.95))))
        sh.resize((256, 256), Image.LANCZOS).save(os.path.join(out, f))
    print("shadows ->", os.path.relpath(out, ROOT))


def board():
    """Figma grill + skewer (ArtSource/Cartoon/grill_v1.png, skewer_v1.png) -> exact game sizes, plus the
    v3 darkened grill (brightness x0.62, saturation x0.7) for non-complete skewers."""
    from PIL import ImageEnhance
    art = os.path.join(ROOT, "Assets", "GrillSort", "Art")
    for src, dst, size in (("grill_v1.png", "Board/grill_cartoon.png", (373, 1818)),
                           ("skewer_v1.png", "Board/skewer_cartoon.png", (97, 1869))):
        item = cut(Image.open(os.path.join(SRC, src)), True)
        item = item.resize(size, Image.LANCZOS)
        item.save(os.path.join(art, dst)); print("wrote", dst, "from", src)
        if "grill" in dst:
            a = item.getchannel("A")
            dark = ImageEnhance.Brightness(ImageEnhance.Color(item.convert("RGB")).enhance(0.7)).enhance(0.62).convert("RGBA")
            dark.putalpha(a)
            dark.save(os.path.join(art, "Generated", "grill_cartoon_dark.png")); print("wrote Generated/grill_cartoon_dark.png")



def final_v2(t=0.5):
    """FoodFinal_v2 (2026-11-18): user: v1 removed the gloss completely; Aaron only said "a bit too glossy".
    v2 = 50/50 blend of the original style B (FoodCartoonB) and v1 (FoodFinal): half the shine. Shadows: reuse ShadowsFinal
    (same silhouettes). Usage: STYLE=FINAL2 python -I Tools/ArtPipeline/cut_cartoon.py"""
    art = os.path.join(ROOT, "Assets", "GrillSort", "Art")
    out = os.path.join(art, "FoodFinal_v2", "Grilled"); os.makedirs(out, exist_ok=True)
    for name in FOODS:
        path = os.path.join(out, name + ".png")
        if os.path.exists(path) and os.environ.get("OVERWRITE") != "1":
            print("skip (exists)", path); continue
        a = Image.open(os.path.join(art, "FoodCartoonB", "Grilled", name + ".png")).convert("RGBA")
        b = Image.open(os.path.join(art, "FoodFinal", "Grilled", name + ".png")).convert("RGBA")
        Image.blend(a, b, t).save(path); print("wrote", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    if os.environ.get("STYLE", "").upper() == "FINAL2":
        final_v2(); raise SystemExit
    run(FOODS, "food", FOOD_OUT, True)
    if STYLE_B:
        bake_shadows(); raise SystemExit
    run(PEOPLE, "people", PEOPLE_OUT, False)
    bake_shadows()
    board()
