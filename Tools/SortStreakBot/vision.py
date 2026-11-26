"""Reads a Sort Streak board from a phone screenshot (research tool, not shipped).

Board model: one or more wooden shelves; under each shelf hang ropes (one per pot). Each rope holds flowers from
the top (fixed end) down to the free end. Pots sit on the shelf above each rope; the white pot = helper rope.
All coordinates are in the screenshot's own pixels (phone 1080 x 2340).
"""
import numpy as np
from PIL import Image


def load(path):
    if isinstance(path, np.ndarray):
        return path
    return np.asarray(Image.open(path).convert("RGB")).astype(np.int16)


def sat_val(a):
    mx = a.max(axis=-1); mn = a.min(axis=-1)
    v = mx.astype(np.float32)
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0).astype(np.float32)
    return s, v


def is_wood(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    return (r > 170) & (g > 115) & (g < 205) & (b < 150) & (r - b > 60)


def find_shelves(a):
    """Rows where a wide horizontal wood band runs across the screen. Returns [(top, bottom, x0, x1)]."""
    h, w, _ = a.shape
    wood = is_wood(a)
    frac = wood.mean(axis=1)
    rows = np.where(frac > 0.55)[0]
    bands = []
    if len(rows) == 0:
        return bands
    start = prev = rows[0]
    for y in rows[1:]:
        if y - prev > 3:
            bands.append((start, prev)); start = y
        prev = y
    bands.append((start, prev))
    out = []
    for t, b in bands:
        if b - t < 22 or b - t > 90:
            continue
        cols = np.where(wood[(t + b) // 2])[0]
        out.append((t, b, int(cols.min()), int(cols.max())))
    return out


def bg_color(a):
    # background: the far-left strip in the middle of the screen
    h = a.shape[0]
    return np.median(a[h // 3: 2 * h // 3, 2:10].reshape(-1, 3), axis=0)


def find_ropes(a, shelf, bg):
    """Rope x centres = the pegs on the shelf (light knobs breaking the wood band)."""
    t, b, x0, x1 = shelf
    ys = range(t + (b - t) // 3, b - (b - t) // 4)
    wood = is_wood(a[t:b + 1])
    rows = [wood[y - t] for y in ys]
    notwood = ~np.all(rows, axis=0) & np.any(~np.array(rows), axis=0)
    # a peg column: non-wood in most of the middle rows
    frac = 1 - np.array(rows).mean(axis=0)
    on = frac > 0.6
    xs = []
    x = x0 + 8
    while x < x1 - 8:
        if on[x]:
            s0 = x
            while x < x1 - 8 and on[x]:
                x += 1
            if 6 <= x - s0 <= 50:
                xs.append((s0 + x - 1) // 2)
        x += 1
    merged = []
    for x in xs:
        if merged and x - merged[-1][-1] < 70:
            merged[-1].append(x)
        else:
            merged.append([x])
    return [int(round(sum(g) / len(g))) for g in merged]


def flower_mask(a, bg):
    s, v = sat_val(a)
    diff = np.abs(a - bg).sum(axis=-1)
    colored = (s > 0.38) & (v > 70)
    grey = (s < 0.18) & (v < 175) & (v > 50) & (diff > 90)   # mystery flowers (dark grey with a "?")
    return colored | grey


def column_blobs(mask, x, y0, y1, half=62, min_count=38, min_len=70):
    win = mask[y0:y1, max(0, x - half): x + half]
    prof = win.sum(axis=1)
    on = prof > min_count
    blobs = []
    y = 0
    n = len(on)
    while y < n:
        if on[y]:
            s = y
            while y < n and on[y]:
                y += 1
            if y - s >= min_len:
                blobs.append((y0 + s, y0 + y - 1))
        y += 1
    return blobs


def split_blob(t, b, spacing):
    """A tall blob = several touching flowers: split by the expected spacing."""
    L = b - t + 1
    if spacing <= 0:
        return [((t + b) // 2)]
    n = max(1, int(round(L / spacing)))
    if n == 1:
        return [(t + b) // 2]
    step = L / n
    return [int(t + step * (i + .5)) for i in range(n)]


def petal_color(a, cx, cy, r):
    """Mean colour of the petal ring (skips the centre). Returns (rgb, is_grey)."""
    ys, xs = np.ogrid[-r:r + 1, -r:r + 1]
    d = np.sqrt(xs * xs + ys * ys)
    ring = (d > r * 0.45) & (d < r * 0.95)
    y0, x0 = cy - r, cx - r
    patch = a[max(0, y0): y0 + 2 * r + 1, max(0, x0): x0 + 2 * r + 1]
    m = ring[: patch.shape[0], : patch.shape[1]]
    px = patch[m]
    s, v = sat_val(px[None, ...])
    s = s[0]; v = v[0]
    sat_px = px[s > 0.35]
    if len(sat_px) < len(px) * 0.25:
        return px.mean(axis=0), True
    return np.median(sat_px, axis=0), False


def pot_color(a, x, shelf_top):
    patch = a[shelf_top - 95: shelf_top - 20, x - 40: x + 40].reshape(-1, 3)
    s, v = sat_val(patch[None, ...])
    s = s[0]; v = v[0]
    sat_px = patch[s > 0.35]
    if len(sat_px) < len(patch) * 0.2:
        bright = patch[v > 200]
        if len(bright) > len(patch) * 0.3:
            return None, "white"
        return None, "none"
    return np.median(sat_px, axis=0), "color"


def read_board(path, debug=False):
    a = load(path)
    h, w, _ = a.shape
    bg = bg_color(a)
    shelves = find_shelves(a)
    if not shelves:
        return None
    mask = flower_mask(a, bg)
    ropes = []
    for si, sh in enumerate(shelves):
        t, b, x0, x1 = sh
        y_end = shelves[si + 1][0] - 120 if si + 1 < len(shelves) else int(h * 0.80)
        for x in find_ropes(a, sh, bg):
            # rope end: last row near x that differs from background, scanning down
            col = np.abs(a[b:y_end, x - 3:x + 4].mean(axis=1) - bg).sum(axis=1)
            nz = np.where(col > 45)[0]
            rope_end = b + int(nz.max()) if len(nz) else b
            blobs = column_blobs(mask, x, b + 8, rope_end + 10)
            ropes.append({"shelf": si, "x": x, "top": b, "end": rope_end, "blobs": blobs})
    # flower size / spacing from single blobs
    lens = sorted(bb - tt for r in ropes for tt, bb in r["blobs"])
    if lens:
        single = lens[0]
        spacing_guess = single * 1.2
    else:
        single, spacing_guess = 150, 180
    for r in ropes:
        centres = []
        for tt, bb in r["blobs"]:
            centres += split_blob(tt, bb, spacing_guess)
        r["centres"] = centres
    # spacing from neighbouring centres
    gaps = [r["centres"][i + 1] - r["centres"][i] for r in ropes for i in range(len(r["centres"]) - 1)]
    spacing = float(np.median(gaps)) if gaps else spacing_guess
    rad = int(min(single, spacing) * 0.45)
    for r in ropes:
        r["flowers"] = []
        for cy in r["centres"]:
            rgb, grey = petal_color(a, r["x"], cy, rad)
            r["flowers"].append({"y": cy, "rgb": rgb, "grey": grey})
        first = r["top"] + spacing * 0.75
        r["cap"] = max(len(r["flowers"]), int(round((r["end"] - first - spacing * 0.45) / spacing)) + 1)
        r["pot_rgb"], r["pot_kind"] = pot_color(a, r["x"], shelves[r["shelf"]][0])
    return {"shelves": shelves, "ropes": ropes, "spacing": spacing, "size": (w, h), "flower": single}


COLOR_TOL = 70.0


def classify(board):
    """Gives every flower and pot a colour id (clusters by RGB). Grey flowers = '?' (id -2)."""
    protos = []

    def cid(rgb):
        for i, p in enumerate(protos):
            if np.abs(np.asarray(rgb, float) - p).sum() < COLOR_TOL:
                return i
        protos.append(np.asarray(rgb, float))
        return len(protos) - 1

    for r in board["ropes"]:
        for f in r["flowers"]:
            f["c"] = -2 if f["grey"] else cid(f["rgb"])
    for r in board["ropes"]:
        if r["pot_kind"] == "color":
            # pots are painted a bit darker/flatter than petals: match the nearest flower colour by hue
            best, bd = None, 1e9
            pr = np.asarray(r["pot_rgb"], float)
            for i, p in enumerate(protos):
                d = hue_dist(pr, p)
                if d < bd:
                    best, bd = i, d
            r["target"] = best if bd < 25 else cid(pr)
        else:
            r["target"] = -1
    board["protos"] = [p.tolist() for p in protos]
    return board


def hue(rgb):
    r, g, b = [c / 255.0 for c in rgb]
    mx, mn = max(r, g, b), min(r, g, b)
    if mx == mn:
        return 0.0
    if mx == r:
        hh = (g - b) / (mx - mn) % 6
    elif mx == g:
        hh = (b - r) / (mx - mn) + 2
    else:
        hh = (r - g) / (mx - mn) + 4
    return hh * 60


def hue_dist(a, b):
    d = abs(hue(a) - hue(b)) % 360
    return min(d, 360 - d)


def describe(board):
    out = []
    for i, r in enumerate(board["ropes"]):
        fl = " ".join("?" if f["c"] == -2 else str(f["c"]) for f in r["flowers"])
        tgt = "H" if r["target"] < 0 else str(r["target"])
        out.append(f"rope{i} x={r['x']} pot={tgt} cap={r['cap']} [{fl}]")
    return "\n".join(out)


if __name__ == "__main__":
    import sys
    for p in sys.argv[1:]:
        bd = read_board(p)
        if bd is None:
            print(p, "no board"); continue
        classify(bd)
        print(p, "shelves", bd["shelves"], "spacing", round(bd["spacing"]), "flower", bd["flower"])
        print(describe(bd))
