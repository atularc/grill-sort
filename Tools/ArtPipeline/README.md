# Art pipeline scripts

Python 3 + Pillow + numpy. These are copies of the scripts used to make the game art. They expect the
**art-lab folder layout** at `D:\me\Projects Games\grill-mockup` (run them from its `source/` folder):

| Script | What it does |
|---|---|
| `process_assets.py` | Cuts `food-hd/`, `customers-hd/`, `board-hd/`, `ui-hd/` sheets into transparent sprites (white flood-fill, edge soften, fringe removal, sheet split; VFX on black use brightness as alpha) |
| `cut_stages.py` | Cuts `food-raw-hd/` and `food-medium-hd/` into `food-raw/`, `food-medium/` and builds the 3-stage sheet |
| `compose.py` | Builds the v1 mockup (`Grill-Sort-Mockup.png`). The layout numbers in CLAUDE.md come from here |
| `compose_v2.py` | v2 mockup (white outline, rejected) |
| `compose_variants.py` | v3 shadow (recommended) / v4 lane / v5 glow mockups |
| `compose_theme.py` | Theme mockups + `layer_front.png`; add `calm` for blurred back scenes |
| `lineup.py` | Food lineup sheet |

Fonts are read from `source/fonts/` (Fredoka). After cutting new art, copy the results into
`Assets/GrillSort/Art/` following the folder structure in CLAUDE.md. Never overwrite existing art unless asked.
