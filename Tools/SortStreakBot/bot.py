"""Plays Sort Streak on a USB-connected Android phone to study its level design (research tool, not shipped).

Loop: screenshot -> read board (vision.py) -> plan a move (short look-ahead search) -> swipe -> repeat.
Screens without a board are saved to runs/<run>/screens/ and handled by handle_other_screen().
Every level start is saved as an image, and every level is logged to runs/<run>/levels.jsonl.

Usage: python -I bot.py [max_levels]
"""
import io, json, os, subprocess, sys, time, hashlib
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vision as V

ADB = r"C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
RUN = os.path.join(HERE, "runs", time.strftime("%Y%m%d-%H%M%S"))
os.makedirs(os.path.join(RUN, "levels"), exist_ok=True)
os.makedirs(os.path.join(RUN, "screens"), exist_ok=True)
LOG = open(os.path.join(RUN, "log.txt"), "a", encoding="utf-8")


def log(*a):
    s = time.strftime("%H:%M:%S ") + " ".join(str(x) for x in a)
    print(s, flush=True)
    LOG.write(s + "\n"); LOG.flush()


def adb(*args, binary=False):
    r = subprocess.run([ADB, *args], capture_output=True, timeout=60)
    return r.stdout if binary else r.stdout.decode("utf-8", "ignore")


def shot(path):
    data = adb("exec-out", "screencap", "-p", binary=True)
    with open(path, "wb") as f:
        f.write(data)
    return path


def grab():
    """Raw screencap (no PNG): header + RGBA bytes -> int16 RGB array."""
    data = adb("exec-out", "screencap", binary=True)
    w, h = int.from_bytes(data[0:4], "little"), int.from_bytes(data[4:8], "little")
    hdr = len(data) - w * h * 4
    a = np.frombuffer(data, dtype=np.uint8, offset=hdr).reshape(h, w, 4)[..., :3]
    return a.astype(np.int16)


def save(a, path):
    Image.fromarray(a.astype(np.uint8)).save(path)


def swipe(x0, y0, x1, y1, ms=450):
    adb("shell", "input", "swipe", str(int(x0)), str(int(y0)), str(int(x1)), str(int(y1)), str(ms))


def tap(x, y):
    adb("shell", "input", "tap", str(int(x)), str(int(y)))


# ---------------------------------------------------------------- model
def to_state(board):
    return tuple((tuple(f["c"] for f in r["flowers"]), r["cap"], r["target"]) for r in board["ropes"])


def key(state):
    return hash(tuple((st, t) for st, c, t in state))


def moves(state, banned):
    out = []
    for a, (st, ca, ta) in enumerate(state):
        n = len(st)
        for i in range(n):
            c = st[i]
            if c == -2:
                continue
            g = n - i
            for b, (sb, cb, tb) in enumerate(state):
                if b == a or len(sb) + g > cb:
                    continue
                if sb and sb[-1] != c:
                    continue
                if len(sb) + g == cb and any(x != c for x in sb + st[i:]):
                    continue   # Sort Streak: a rope may only be filled to the top with one colour
                if i == 0 and not sb and not (tb >= 0 and all(x == tb for x in st)):
                    continue   # whole rope onto an empty rope only when that serves it
                if (a, i, b, st) in banned:
                    continue
                out.append((a, i, b))
    return out


def apply(state, m):
    a, i, b = m
    st = list(state)
    sa, ca, ta = st[a]; sb, cb, tb = st[b]
    group = sa[i:]
    st[a] = (sa[:i], ca, ta)
    st[b] = (sb + group, cb, tb)
    rev = 1 if st[a][0] and st[a][0][-1] == -2 else 0
    keep, served = [], 0
    for r in st:
        s0, c0, t0 = r
        if t0 >= 0 and len(s0) == c0 and all(x == t0 for x in s0):
            served += 1
        else:
            keep.append(r)
    return tuple(keep), served, rev


def score(state):
    s = 0.0
    for st, cap, t in state:
        if t >= 0:
            run = 0
            for x in st:
                if x == t:
                    run += 1
                else:
                    break
            s += run * 12 - (len(st) - run) * 6 + st.count(t) * 3
        for j in range(1, len(st)):
            if st[j] == st[j - 1] and st[j] >= 0:
                s += 4
        s -= st.count(-2) * 2
        s += (cap - len(st)) * 1.5
    return s


def plan(state, seen, banned, depth=6, beam=160):
    """Beam search: value = serves (big), reveals, board score; dead ends heavily penalised."""
    first = moves(state, banned)
    if not first:
        return None
    frontier = []
    for m in first:
        s1, sv, rv = apply(state, m)
        g = sv * 1000 + rv * 60 - (400 if key(s1) in seen else 0)
        frontier.append((g, s1, m))
    best = {}
    for d in range(depth):
        scored = []
        for g, st, m0 in frontier:
            ms = moves(st, ())
            dead = (not ms) and len(st) > 0 and any(t >= 0 for _, _, t in st)
            val = g + score(st) - (6000 if dead else 0)
            if val > best.get(m0, -1e18):
                best[m0] = val
            if d < depth - 1:
                for m in ms:
                    s2, sv, rv = apply(st, m)
                    scored.append((g + (sv * 1000 + rv * 60) * (0.97 ** (d + 1)), s2, m0))
        if not scored:
            break
        scored.sort(key=lambda x: x[0] + score(x[1]), reverse=True)
        uniq, ks = [], set()
        for it in scored:
            k = key(it[1])
            if k in ks:
                continue
            ks.add(k); uniq.append(it)
            if len(uniq) >= beam:
                break
        frontier = uniq
    return max(best, key=best.get)


# ---------------------------------------------------------------- screens
def big_blob(mask, min_px):
    """A real button: many rows where the colour runs > 380 px wide."""
    rows = np.where(mask.sum(axis=1) > 380)[0]
    if len(rows) < 50:
        return None
    ys, xs = np.where(mask[rows])
    return int(np.median(xs)), int(np.median(rows))


_back_presses = [0]


def other_screen(path):
    """No board on screen: press the main button (blue Next / green / orange), or Back as a last resort."""
    a = V.load(path)
    h, w, _ = a.shape
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    s, v = V.sat_val(a)
    lower = slice(h // 2, h - 60)
    blue = (b > 200) & (r < 90) & (g > 110) & (g < 190)
    green = (g > 150) & (g > r + 40) & (g > b + 40) & (s > .45)
    orange = (r > 220) & (g > 110) & (g < 190) & (b < 90)
    for name, m in (("blue", blue), ("green", green), ("orange", orange)):
        pt = big_blob(m[lower], 9000)
        if pt:
            tap(pt[0], pt[1] + h // 2)
            _back_presses[0] = 0
            return name
    # Android notification permission dialog: "Don't allow" sits near the bottom on a dark sheet
    if a[h - 300:h - 100, w // 4: 3 * w // 4].mean() < 60:
        tap(w // 2, h - 157)
        return "dialog-bottom"
    if _back_presses[0] < 2:
        _back_presses[0] += 1
        adb("shell", "input", "keyevent", "4")
        return "back"
    return None


def stable_shot(prefix):
    p1 = shot(prefix + "_a.png")
    time.sleep(0.5)
    p2 = shot(prefix + "_b.png")
    a1, a2 = V.load(p1), V.load(p2)
    if a1.shape == a2.shape and np.abs(a1 - a2).mean() < 1.5:
        return p2
    time.sleep(0.8)
    return shot(prefix + "_c.png")


# ---------------------------------------------------------------- main loop
def read(a):
    bd = V.read_board(a)
    if bd is None or not bd["ropes"]:
        return None, None
    V.classify(bd)
    return bd, to_state(bd)


def settled():
    """Two quick grabs that read the same board = animations finished."""
    a1 = grab(); b1, s1 = read(a1)
    for _ in range(6):
        time.sleep(0.25)
        a2 = grab(); b2, s2 = read(a2)
        if s1 is not None and s2 is not None and key(s1) == key(s2):
            return a2, b2, s2
        if s1 is None and s2 is None:
            return a2, None, None
        a1, b1, s1 = a2, b2, s2
    return a1, b1, s1


def main(max_levels):
    level_i = 0
    seen, banned = set(), set()
    level_moves = 0
    level_info = None
    prev_key, prev_move, prev_stack = None, None, None
    no_board = 0
    level_cap = None
    refusals = 0
    stuck_count = 0
    t_level = time.time()
    while level_i < max_levels:
        a, board, state = settled()
        if board is None:
            no_board += 1
            dst = os.path.join(RUN, "screens", f"{int(time.time()*10)}.png")
            save(a, dst)
            what = other_screen(a)
            log("no board", os.path.basename(dst), "->", what)
            if level_info:
                level_info["moves"] = level_moves
                level_info["seconds"] = round(time.time() - t_level)
                with open(os.path.join(RUN, "levels.jsonl"), "a") as f:
                    f.write(json.dumps(level_info) + chr(10))
                level_info = None
                level_cap = None
            if not what and no_board >= 3:
                log("STOP: unknown screen, needs a look:", dst)
                return
            time.sleep(0.8 if what else 1.5)
            prev_key = prev_move = None
            continue
        no_board = 0
        if level_cap is not None:
            state = tuple((st, max(level_cap, len(st)), t) for st, _, t in state)
        if level_info is None:
            level_i += 1
            seen, banned, level_moves, t_level = set(), set(), 0, time.time()
            caps = [c for _, c, _ in state]
            level_cap = max(set(caps), key=caps.count)
            state = tuple((st, max(level_cap, len(st)), t) for st, _, t in state)
            refusals = stuck_count = 0
            save(a, os.path.join(RUN, "levels", f"L{level_i:03d}.png"))
            level_info = {"n": level_i, "ropes": len(state), "caps": [c for _, c, _ in state],
                          "helpers": sum(1 for _, _, t in state if t < 0),
                          "pieces": sum(len(st) for st, _, _ in state),
                          "mystery": sum(st.count(-2) for st, _, _ in state),
                          "colors": len(board["protos"]), "shelves": len(board["shelves"]),
                          "board": V.describe(board)}
            log(f"LEVEL #{level_i}", json.dumps({k: v for k, v in level_info.items() if k != "board"}))
        k = key(state)
        if prev_move is not None and k == prev_key:
            banned.add((prev_move[0], prev_move[1], prev_move[2], prev_stack))
            log("move refused, banned", prev_move)
            refusals += 1
        else:
            refusals = 0
        if refusals >= 4:
            stuck_count += 1
            dst = os.path.join(RUN, "screens", f"stuck_{int(time.time()*10)}.png")
            save(a, dst)
            if level_info:
                level_info["stuck"] = stuck_count
            if stuck_count % 3 != 0:
                log(f"STUCK -> Undo (stuck #{stuck_count})", os.path.basename(dst))
                tap(200, 2010); time.sleep(1.0)
                refusals, prev_move = 0, None
                continue
            log(f"STUCK -> Reset + Restart (stuck #{stuck_count})", os.path.basename(dst))
            tap(877, 2030); time.sleep(1.4)
            save(grab(), os.path.join(RUN, "screens", f"resetpopup_{int(time.time()*10)}.png"))
            tap(540, 1266); time.sleep(3.0)
            seen, banned, refusals, prev_move = set(), set(), 0, None
            continue
        seen.add(k)
        m = plan(state, seen, banned)
        if m is None:
            log("no move found (stuck?)")
            refusals = 99
            prev_move = None
            continue
        # play this move and, while the outcome is certain (no serve, nothing revealed), the next ones too
        blind = 0
        cur = state
        offs = None
        for r in board["ropes"]:
            if r["flowers"]:
                offs = r["flowers"][0]["y"] - r["top"]; break
        if offs is None:
            offs = board["spacing"] * .75
        while True:
            i0, i1, i2 = m
            ra, rb = board["ropes"][i0], board["ropes"][i2]
            y0 = ra["top"] + offs + i1 * board["spacing"]
            nb = len(cur[i2][0])
            y1 = rb["top"] + offs + max(0, nb - 1) * board["spacing"]
            swipe(ra["x"], y0, rb["x"], y1, 180)
            level_moves += 1
            nxt, served, rev = apply(cur, m)
            prev_key, prev_move, prev_stack = key(cur), m, cur[i0][0]
            if served or rev or blind >= 6 or len(nxt) != len(cur):
                time.sleep(1.3 if served else 0.3)
                break
            blind += 1
            seen.add(key(nxt))
            cur = nxt
            time.sleep(0.32)
            m = plan(cur, seen, banned)
            if m is None:
                break
        if level_moves > 300:
            log("too many moves, stopping for a look")
            save(a, os.path.join(RUN, "screens", f"long_{int(time.time())}.png"))
            return


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 5)
