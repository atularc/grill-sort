# CustomersPeople_v4 = CustomersPeople_v3 with every bust reaching the bottom of the sprite (user 2026-11-23: "people
# are in the air"). Some busts (grandma, pigtails) were cut 20-30 px above the sprite bottom, so they ended above the
# counter. The clothes are extended DOWN by repeating a full-width row from just above the cut, so the eye line and the
# face size from v3 stay the same. The extension is hidden behind the counter in game (CustomerView.BodyY).
# A 4 px transparent edge is kept at the bottom (atlas bleed), like v3.
import glob
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "../../Assets/GrillSort/Art/CustomersPeople_v3")
DST = os.path.join(HERE, "../../Assets/GrillSort/Art/CustomersPeople_v4")
EDGE = 4          # transparent rows kept at the bottom
SAMPLE_UP = 10    # take the row this far above the cut (clear of the cut's rounded/soft edge)

os.makedirs(DST, exist_ok=True)
for p in sorted(glob.glob(os.path.join(SRC, "*.png"))):
    out = os.path.join(DST, os.path.basename(p))
    if os.path.exists(out) and not os.environ.get("OVERWRITE"):
        print("exists", out)
        continue
    a = np.array(Image.open(p).convert("RGBA"))
    h = a.shape[0]
    rows = np.where((a[..., 3] > 128).any(axis=1))[0]
    bottom = rows[-1]                      # last opaque row
    target = h - 1 - EDGE                  # extend down to here
    if bottom < target:
        src_row = a[bottom - SAMPLE_UP].copy()
        for y in range(bottom - SAMPLE_UP + 1, target + 1):
            a[y] = src_row
        a[target + 1:] = 0
    Image.fromarray(a).save(out)
    print(os.path.basename(p), "cut at", bottom, "->", target)
