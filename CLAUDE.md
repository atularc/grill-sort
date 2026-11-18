# Grill Sort — project brief for Claude

Read this first in every session. It holds everything decided so far about the game, the art, and how to build it.
If the user says **"build the prototype with levels using all the assets we have"**, follow
[Build the prototype](#build-the-prototype-step-by-step) at the end of this file.

---

## 1. What the game is

A casual mobile **BBQ skewer sort puzzle** (Water Sort core) for **Google Play and the App Store**, monetized with **ads + IAP**.

- Cute animal **customers** stand behind a food-stall counter at the top, each with an **order bubble** showing one food.
- Below the counter are **grills**. Each grill holds one **wooden skewer**; each skewer holds a stack of **food pieces** (capacity **4**).
- The player moves food between skewers until every skewer holds **4 of the same food**.
- A finished skewer **sizzles, glows, shows "Perfect!"** and is **served** to the customer who ordered it (✓ on the bubble, customer cheers, leaves, next customer steps up). The order counter in the HUD goes up ("Orders 1 / 4").
- The level is won when all orders are served (= all food sorted).

**Client:** Aaron (DucNA), cc Thảo Nguyễn, via Slack. Aaron chose this BBQ theme (2026-11-17) after rejecting the earlier animal/rope theme. He wanted a screenshot with real final-quality art first: done and loved (see `Docs/Mockups/`).
**Reference game Aaron named:** "Grill Sort Master" (Water Sort style). It is someone else's game: **never copy its name, art or levels.**
**Game name:** NOT decided yet. "Grill Sort" is only the working title. Logo and app icon wait on the name.

## 2. Core rules

```
State: N skewers, each a bottom→top stack of food ids, capacity C = 4.
Move:  tap a source skewer (picks up its movable group), tap a target skewer.
       Valid if target is empty, OR target's top food == moving food AND target has room.
       Moves as many pieces of the group as fit.
Done:  a skewer holding C identical pieces is complete → locked, cooked/served, freed.
Win:   all orders served / every non-empty skewer complete.
```

**DECIDED by Aaron (2026-11-18): `AnyWithAbove`.** "Follow Sort Streak closely: players can grab any piece, and everything above it comes along, same as your prototype." (Sort Streak is another reference game: same rule as ours, never copy it.)
- The move takes the whole hanging stack, which must fit on the target. The target must be empty or have the grabbed piece's food on top.
- The rule stays a setting (`enum MoveRule { AnyWithAbove, TopGroup, SingleTop }`, default `AnyWithAbove`). The solver, generator and bake all use the active rule.

**Customers & orders:** 3–4 customers visible. Each wants one food; a completed skewer of that food serves them. Orders = number of food types in the level (one skewer each), shown as "Orders x / y".

**Cooking stages (we have Raw → Medium → Grilled art for all 18 foods). How to use them is OPEN; ask the user.** Default plan:
- During play the food shows **Grilled**. This is what the client approved in the mockup.
- Optional flag `cookOnComplete`: pieces show **Raw** while sorting. When a skewer completes it cooks Raw → Medium → Grilled (~0.35 s per step, sizzle + steam + sparkle), then it's served.

**Boosters** (bottom bar, yellow tiles with red count badges): **Undo**, **Hint**, **Shuffle**, **Extra skewer (+)**.

**Fail/stuck:** detect when no valid moves remain → offer Undo / Shuffle / Extra skewer (rewarded ad) / Retry.

## 3. Levels & difficulty (user requirements)

- **Endless levels with rising difficulty.** Every level must be **really solvable**: verify with a solver at bake time.
- Bake ~**2000 levels** to a data file and verify each one. Past level 2000 the deterministic generator keeps going with the same seed scheme.
- **Every level should make the player think more**: no flat plateau. (In Dangle Pals, difficulty flattened after ~level 126 and the user wanted it raised all the way.)
- Keep the **`limitToTenLevels` boolean** (user asked for it in the last project): when ticked, only 10 levels can be played (demo/review builds).
- Difficulty knobs: food types (3 → 8+), skewers, empty skewers (2 → 1), mixing depth, minimum solution length (par), and how many look-alike foods share a level.
- **Look-alike groups.** Don't mix many of these early on:
  - yellow: corn, pineapple, potato, halloumi
  - brown-red: beef, sausage, meatball, bacon
  - orange: salmon, shrimp, squid
  - green: pepper, zucchini, broccoli
- Early levels need tips. In the last project the user found the rules confusing at first, so levels 1–3 got hand-holding and a free idle hint on levels ≤ 10.
- **Layout:** the mockup fits **5 grills** across a 1080-wide portrait screen. For more skewers, use two rows or narrower grills. Design levels to fit.
- **Reference implementation** (rope rules, but the solver, generator, bake/verify and difficulty tiers carry over): the old Dangle Pals Unity project at https://github.com/atularc/animals-sort.
  - `Assets/DanglePals/Scripts/Core/`: `Puzzle.cs` (solver: `Solve(out exhausted)`, `Replays`), `LevelGenerator.cs`, `LevelBank.cs`, `Mulberry32.cs` (seeded RNG).
  - `Assets/DanglePals/Scripts/Game/`: `GameController.cs` (idle hint, dead-end detection, `limitToTenLevels`), `GameArt.cs`, `Fx.cs`, `Ease.cs`, `AudioManager.cs`, `Haptics.cs`, `SaveData.cs`.
  - `Assets/DanglePals/Scripts/UI/`: `SafeArea.cs`, `CanvasAspect.cs`, `PressScale.cs`, `UiKit.cs`.
  - `Assets/DanglePals/Scripts/Services/`: `IAdsService` / `IIapService` placeholders.
  - `Assets/DanglePals/Editor/`: `DanglePalsLevels.cs` (bake & verify), `DanglePalsAutoPlay.cs`, `UiBuilder.cs` (scene-authored UI), `DanglePalsSetup.cs`, `DanglePalsBuild.cs`.
  - Adapt them to the Water Sort rules. Don't copy the rope logic blindly.

## 4. User's standing rules for the Unity build

- **Everything lives on the Canvas, always present in the scene. Nothing is created or loaded at runtime** (user rule, 2026-11-17).
  - The whole game (background, customers, every grill + skewer + 4 food slots, VFX images, flying pieces, HUD, boosters, tips, toast, every popup) is a uGUI object **authored into `Game.unity` by the Editor builder** ("Grill Sort > Run Full Setup").
  - Runtime code may only **toggle (SetActive / alpha), re-skin (swap sprites/text) and tween** those existing objects. No `Instantiate`, no `new GameObject`, no `AddComponent`, no `Resources.Load`, no Addressables, no runtime-built prefabs or particle systems.
  - Need more of something (e.g. more grills)? Raise the count in the builder and rebuild the scene; don't spawn.
  - The only `Resources` folder is the one TextMeshPro itself needs (`Assets/TextMesh Pro/Resources`, TMP Essentials). Our own assets never go in `Resources`.
- **No `Resources` folder.** Reference assets through a ScriptableObject (e.g. `GameArt`) wired in the Editor.
- **Safe area must be handled** (notches / home bar) with a SafeArea component on the UI root.
- **Smallest build size.** Strip unused packages, compress textures (ASTC on mobile), and sprite-atlas the UI and food.
- **Juice matters** ("pleasing to ears and eyes"): tweens on every move, a pick-up lift, a landing bounce, sizzle/steam/sparkle VFX, the serve animation, sounds, haptics.
- **New scenery unlocks as you play** (theme worlds, see §6).
- **Ads + IAP** behind interfaces (`IAdsService`, `IIapService`) with placeholder implementations until real IDs exist.
- Use a uGUI Canvas + TextMeshPro for UI (never OnGUI). URP 2D.
- Don't commit or push unless the user asks. **Never commit keystores.**

## 5. Art direction

**Aaron's feedback on the mockups (2026-11-18):**
- He liked the UI and the backgrounds of the **Wood** and **Beach** mockups. Both can go in the real game.
- "Real assets" meant the actual in-game assets, not realistic food. The current food is **too realistic**.
- Food must go back to **cartoon style** like their own mock: still juicy and appetizing, with **clear grill marks**.
- Customers should be **people** (like their demo), not animals. Human characters connect better with this audience and work better in food-game ads.
- Once the art direction is locked he wants a **build**.

- **Food (new direction):** **cartoon** grilled food, juicy and appetizing, bold clear grill marks, simple readable shapes, slight top-down 3/4 view. The current `Food/` set is the rejected stylized-realistic version: keep it, but save the cartoon set as new files.
- **Customers (new direction):** cartoon **people** (diverse, friendly), head and upper body only (cut at the chest), drawn *behind* the counter. The animal set in `Customers/` is rejected.
- *(Old, rejected)* Food: stylized-realistic. Customers: chubby 3D vinyl-toy baby animals.
- **UI:** glossy, bubbly 3D casual-game style, thick rounded shapes, subtle dark outline. Fredoka font.
- **Readability (important).** In the first mockup the user noticed the food blending into the glowing grills. The approved fix is **v3 "deep shadow"**:
  - Draw **non-complete grills darker**: brightness ×0.62, saturation ×0.7.
  - Give **each food a deep soft drop shadow**: color (12,5,0), alpha ~0.95, offset (8,18) px at 1080p, blur ~10, spread ~9.
  - Make food slightly richer: saturation ×1.1, contrast ×1.06, scale ×1.08.
  - A **completed** grill stays full brightness with a warm golden glow (255,205,70).
  - **The user rejected a white outline around food. Never use outlines/halos.**
- **Backgrounds:** the user found the themed backgrounds **overwhelming**. Prefer the `background-calm` versions (the scene behind the counter is blurred and toned down). Counters and patterns can be toned down further if needed.

## 6. Themes / worlds

Same layout for every theme; only the background changes.

| Theme | Folder | Counter top (px in 1024×1536 source) | Notes |
|---|---|---|---|
| Wood stall (default) | `Backgrounds/Wood` | 528 | The approved mockup look |
| Beach sunset shack | `Backgrounds/Beach` | 500 | Bright and fresh |
| Neon night market | `Backgrounds/NightMarket` | 520 | Most striking |
| Snowy winter market | `Backgrounds/Winter` | 538 | Christmas / seasonal event |
| Mint food truck | `Backgrounds/FoodTruck` | 527 | Cleanest, food pops most |

Suggested unlock order: Wood → Beach → Food Truck → Night Market; Winter as a holiday event.

Layering per theme:
1. `background.png` (or `background-calm.png`), full screen.
2. Customers.
3. `layer_front.png`, the same image with everything above the counter top transparent, so customers stand *behind* the counter.
4. Grills, skewers, food, UI.

Backgrounds are 1024×1536 and **cover-fit** to the screen (1080×1920 → scale 1.25, crop sides).

## 7. Reference layout (from the approved mockup, 1080×1920 portrait)

Positions are centers in px from the top-left, at 1080×1920. Convert to anchors / safe area in Unity.

- **HUD (top):**
  - Pause button (92,106) w112.
  - Level pill `bar_hud` (540,100) w300 with "Level 12" (Fredoka Bold 52, brown 92,46,18).
  - Coin pill `bar_hud` (900,100) w250, `icon_coin` at (790,98) w86, "1,250" (44).
  - `bar_progress` (540,186) w330 with "Orders 1 / 4" (30, white with dark-green stroke).
- **Customers:** 4 slots at x = 150, 410, 670, 930, width 250, center y = counterY − 80 (counterY = counterTop × 1.25 ≈ 660 for Wood).
  - Order bubble `bubble_order` w196 at y = counterY − 290, with the food icon w112 inside (y − 8).
  - Served: `icon_check` w62 at (+80, −46) from the bubble center.
- **Grills:** 5 at x = 140, 340, 540, 740, 940; top y 862, height 770 (width ≈ 158, from `grill.png` aspect 373×1818). Each has a soft dark rounded shadow behind it.
  - Skewer height ≈ grill height − 20, centered 6 px above the grill center.
  - **Food slots (bottom→top) y = 1515, 1352, 1189, 1026**, so 4 per skewer, spaced 163.
  - Food widths (px):

    | Food | Width | Food | Width | Food | Width |
    |---|---|---|---|---|---|
    | sausage | 170 | chicken | 150 | shrimp | 156 |
    | meatball | 146 | corn | 160 | tomato | 140 |
    | mushroom | 150 | broccoli | 156 | pepper | 156 |
    | onion | 150 | pineapple | 150 | beef | 148 |
    | zucchini | 150 | salmon | 150 | squid | 160 |
    | potato | 146 | bacon | 156 | halloumi | 150 |
- **Completed skewer:** golden glow behind the grill, 2 steam wisps above, 4–5 sparkles around, `word_perfect` w300 just below the grill.
- **Boosters:** 4 yellow tiles `btn_tile_yellow` w170 at x = 240, 440, 640, 840, y 1790. Icon w104 inside. Red count badge (r30) at (+66, +58).

## 8. Asset catalog

### Folder structure
```
CLAUDE.md                     this brief
Assets/GrillSort/
  Art/
    Food/Grilled|Medium|Raw/  18 foods each (512x512)
    Customers/                fox, bear, kitty, panda
    Board/                    grill, skewer
    Backgrounds/<Theme>/      Wood, Beach, NightMarket, Winter, FoodTruck
    UI/Icons|Buttons|Panels|Words/
    VFX/                      flame, smoke, sparkle, steam
  Fonts/                      Fredoka-Bold.ttf, Fredoka-SemiBold.ttf
  Editor/ArtImportSettings.cs sprite import defaults for Art/
  Art/Generated/              baked helpers: food Shadows/, grill_dark, glow, soft_rect, badge, bar_fill/track, rounded
  Audio/                      placeholder synthesized SFX (pick, drop, invalid, sizzle, serve, coin, win, click)
  Scripts/Core/               pure C#: Board (+ MoveRule), Solver, LevelGenerator, LevelData, FoodCatalog, Mulberry32
  Scripts/Game/               GameController, GrillView, CustomerView, GameArt, LevelBank, Tween, SaveData, AudioManager, Haptics
  Scripts/UI/                 GameUi (scene refs), StageLayout, SafeArea, CanvasAspect, PressScale
  Scripts/Services/           IAdsService / IIapService + placeholders
  Editor/                     GrillSortSetup (Run Full Setup), GrillSortLevels (Bake And Verify), UiKit
  Data/                       GameArt.asset, LevelBank.asset (30 baked levels)
  Atlases/                    UI + Food sprite atlases (ASTC on mobile)
  Scenes/Game.unity           the only build scene
Assets/Scenes/SampleScene     old template scene (not in the build)
ArtSource/                    full-size AI originals on white (Food/Grilled|Medium|Raw, Customers, Board, UI sheets)
Docs/Mockups/                 approved screenshots + Themes/
Docs/ArtPrompts.md            exact prompt for every asset
Tools/ArtPipeline/            Python cut/compose scripts (see README there)
```
**The playable prototype is built (2026-11-17).** See [Current state](#current-state-of-the-prototype) at the end.

All art is in `Assets/GrillSort/Art/` as transparent PNGs, imported as **Sprites** by `Assets/GrillSort/Editor/ArtImportSettings.cs`.
Full-size originals (white background, uncut) are in `ArtSource/` outside Assets, for re-cutting.

**Food:** 18 types × 3 stages, 512×512, centered, with ~3% padding. The same file names are used in every stage folder.
- `Food/Grilled/` (main game look)
- `Food/Medium/` (half-cooked)
- `Food/Raw/` (fresh)
- Names: `bacon beef broccoli chicken corn halloumi meatball mushroom onion pepper pineapple potato salmon sausage shrimp squid tomato zucchini`
- Medium/Raw are separate generations, so their shape is close to the grilled piece but not identical. Hide the swap with a puff of steam.

**Cartoon set (new direction, 2026-11-18):**
- `FoodCartoon/Grilled/`: all 18 foods, same names, 512×512. Grilled stage only; no Raw/Medium yet.
- **`FoodCartoonB/Grilled/` = food style B (2026-11-18).** The user found style A still a bit realistic (painted texture, specks, fibres), so all 18 were regenerated flatter: no texture, smooth cel shading, bold outline, thick clean grill stripes, glossy highlights and juice drops. Sources `ArtSource/Cartoon/singleB/`, shadows `Generated/ShadowsCartoonB/`, cut with `STYLE=B python -I Tools/ArtPipeline/cut_cartoon.py`. A **sear pass** (`SEAR=1`, `sear()` in that script) was tried to burn the grill marks in; the user said it **only looked darker** and it was reverted (off by default). A Figma prompt for charred marks drifted back to realistic texture (`ArtSource/Cartoon/v2test/charred_chicken_rejected.png`). **All sets are kept for the client to choose** (`Docs/Mockups/Food-Style-A-B-C.png`, `-ingame.png`).
- **`FoodCartoonC/Grilled/` = food style C** = style B + a "grilled" pass (`STYLE=C`, `grill()` in `cut_cartoon.py`): amber caramel glow around the marks at the same brightness, chocolate-red marks instead of flat ink, a glossy pressed-in lip. Only thin dark bands count as marks. The user asked for a grilled look **without darkening** and to keep B unchanged.
  - In game: `GameArt.foods` (A), `foodsB` (B), `foodsC` (C); code reads `art.Foods`. Pause menu button "Food Style: A/B/C" cycles live (`SaveData.FoodStyle`, default **B**). Once Aaron picks, drop the other sets, their atlases (`FoodB`, `FoodC`) and the button.
- `CustomersPeople/`: 8 people, 512×512: `grandpa ponytail redhead curly dad grandma pigtails quiff`.
- `Board/grill_cartoon.png` (373×1818, glossy red-enamel tray, cartoon coals, chrome bars), `Board/skewer_cartoon.png` (97×1869), `Generated/grill_cartoon_dark.png` (v3 darkened), `Generated/ShadowsCartoon/`.
- **Every item is its own 1024 Figma generation** in `ArtSource/Cartoon/single/` (`food_<name>.png`, `people_<name>.png`); grill/skewer are `ArtSource/Cartoon/grill_v1.png`, `skewer_v1.png`.
  - The 2×2 sheets (`sheet_*.png`) were only the style test and came out too soft.
  - All of it is cut by `Tools/ArtPipeline/cut_cartoon.py` (sprites + shadows + grill/skewer). It never overwrites unless `OVERWRITE=1`. There is no automatic hole finder (it ate white highlights): list real background gaps in `HOLES`.
- Prompts: end of `Docs/ArtPrompts.md`. For singles, use the same subject text with "one single …, filling most of the image". Lineup: `Docs/Mockups/Cartoon-Set-Lineup.png`.
- **In the game** via `GrillSortSetup` (`FoodDir`, `ShadowDir`, `CustomerDir`, `CustomerNames`). The old realistic `Food/`, animal `Customers/` and `grill.png`/`skewer.png` stay unused, except Raw/Medium food for the optional `cookOnComplete`.
- Fire: 5 small `fx_flame` sprites per grill (`GrillView.FlameSpots`, in the gaps between food slots), flickering in `GameController.LateUpdate`.
- Figma account is the **Starter (free) plan**: generations use its monthly AI credits. ~54 images were used on 2026-11-18 (33 style A + 21 style B incl. 3 tests).

**Aaron's level-screen feedback (2026-11-18): he chose food style B.** Done the same day, every change as NEW files (originals kept as backups):
1. Grill area too dark → `Generated/grill_cartoon_dim.png` (brightness ×0.86, saturation ×0.9, coals lifted) replaces `grill_cartoon_dark.png` as the non-complete grill; grill shadow alpha .59 → .42; Wood counter front → `Backgrounds/Wood/layer_front_bright.png` (planks +30%). Script: `Tools/ArtPipeline/brighten_board.py`. A theme uses `layer_front_bright.png` when it exists.
2. Fire sparks removed (the small `fx_flame` sprites between the food). Add back later as particles.
3. Food too glossy → `FoodFinal_v2/Grilled/` = half the shine (50/50 blend of `FoodCartoonB` and `FoodFinal`). `FoodFinal/` (v1) removed the shine completely, and the user said that went too far. Shadows: `Generated/ShadowsFinal/`. Script: `cut_cartoon.py` `STYLE=FINAL` / `STYLE=FINAL2`.
- The A/B/C pause switch is gone; the game uses only `FoodFinal_v2`. A (`FoodCartoon`), B (`FoodCartoonB`), C (`FoodCartoonC`) stay on disk, unused.
- Level-1 tip now matches AnyWithAbove: "Tap any food to pick it up with the food above it".
- Images: `Docs/Mockups/Level-Screen-Before-After.png`, `Level-Screen-Final.png`.

**Customers, old animal set (rejected)** (`Customers/`, 512×512): `fox` (rainbow scarf), `bear` (blue bandana), `kitty` (grey tabby, pink bow), `panda` (green scarf). All face the viewer, smiling. No mood variants yet.

**Board** (`Board/`):
- `grill.png` (373×1818): tall cast-iron charcoal tray with glowing coals and chrome bars, empty. One per skewer.
- `skewer.png` (97×1869): bamboo skewer, ring handle on top, pointed tip.

**Backgrounds** (`Backgrounds/<Theme>/`):
- `background.png` (1024×1536)
- `layer_front.png` (counter + table only)
- `background-calm.png` (all themes except Wood; Wood was never judged too busy)

**UI:**
- `UI/Icons/`: `icon_undo`, `icon_hint` (bulb), `icon_shuffle`, `icon_add_skewer`, `icon_coin`, `icon_star`, `icon_heart`, `icon_check`, `icon_lock`, `icon_gift`
- `UI/Buttons/`: `btn_green` / `btn_orange` / `btn_red` (wide blank pills, 9-slice them), `btn_tile_yellow` (booster tile), `btn_pause`, `btn_settings` (round blue)
- `UI/Panels/`: `bubble_order` (white speech bubble, tail down), `bar_hud` (cream pill, brown outline), `bar_progress` (frame with a green half-fill: use the frame and mask the fill), `ribbon_red` (title banner), `popup_panel` (Level Complete window: wood frame, cream inside, red ribbon header, checkered trim)
- `UI/Words/`: `word_perfect`, `word_yummy`, `word_sizzling`, `word_level_complete` (pre-rendered bubbly text)
- Text everywhere else uses `Assets/GrillSort/Fonts/Fredoka-Bold.ttf` / `Fredoka-SemiBold.ttf` (make TMP font assets).

**VFX** (`VFX/`): `fx_flame`, `fx_smoke`, `fx_sparkle`, `fx_steam`. These were cut from black (brightness → alpha). Use them as particle textures (additive or alpha blend).

**Docs:**
- `Docs/Mockups/`: approved screenshots.
  - `Grill-Sort-Mockup.png` (v1)
  - `-v2` (white outline, rejected)
  - `-v3-shadow` (**recommended**)
  - `-v4-lane`, `-v5-glow` (not chosen)
  - `Grill-Food-Lineup.png`, `Grill-Food-Stages.png`
  - `Themes/<Theme>-mockup.png` / `-mockup-calm.png`, `All-Themes.png`, `All-Themes-Calm.png`
- `Docs/ArtPrompts.md`: the **exact prompt** used for every asset.

**Not made yet:**
- Game name, logo, app icon
- Home / level-map screen, loading screen
- Shop art (coin packs, No Ads, bundles)
- Customer moods (waiting, impatient, happy), more customers
- Close (X) button, sound/music toggles, tutorial hand, locked-grill look
- Store screenshots and feature graphic
- **All sounds and music** (sizzle, pick, drop, serve, coins, win, BGM)

## 9. Making more art (same quality)

- **Tool:** Figma MCP `generate_image`, model `gpt-image-2.5-flare`, file "Grill Sort - Food Art" key `cjHcELnFveS7axiqp8anVh`. Call `create_session` with that file key first.
  - It uses the user's paid **Figma AI credits**. Say roughly how many images a batch needs before a big batch.
  - The user prefers this generator ("do the same way you did for the original ones") over Weave image-editing.
- **Prompt recipes.** Copy from `Docs/ArtPrompts.md` and only swap the subject.
  - Food: `Premium mobile game food asset: one single <food + state description>, seen from a slight top-down three-quarter view. <details>. Appetizing, mouth-watering, high detail, polished stylized-realistic look like top casual cooking games. Soft bright studio lighting with gentle rim light. Centered, fully visible, isolated on a plain solid pure white background, no plate, no skewer, no text, no shadow.`
  - UI: put several items on one sheet ("Sprite sheet of 4 separate … arranged in a clean 2 by 2 grid with lots of empty white space … items not touching") to save credits, then split.
  - VFX: same, on a **pure black** background.
- **Cutting.** `Tools/ArtPipeline/` holds the Python (PIL + numpy) scripts:
  - `process_assets.py`: white flood-fill from the border, edge soften, un-premultiply the white fringe, trim, pad. Splits sheets by empty bands. For black-background VFX, brightness becomes alpha.
  - `cut_stages.py`: cuts the cooking stages.
  - `compose*.py`: mockup builders, the source of the layout numbers above.
  - The scripts expect the art-lab layout at `D:\me\Projects Games\grill-mockup` (food-hd/, food/, ui-hd/ …). Run them there, then copy the results into `Assets/GrillSort/Art/`.
- **Rules:**
  - Always check results (pale items on a dark background, sheet splits) and redo bad ones (e.g. "exactly ONE single half … no other pieces").
  - **Save new art as new files. Never overwrite existing art unless the user asks.**

## 10. Tech setup

- **Unity 6000.3.17f1** (Unity 6.3), **2D URP** template. Android + iOS modules installed on this PC. Portrait.
- Drive the Editor with the **Unity CLI** (`unity` command; `unity command …` through the project's `com.unity.pipeline` package). Prefer it over hand-editing scene files. Note: the old UnityMCP server refused connections.
- Repo: https://github.com/atularc/grill-sort (**public**), branch `main`. Uses the standard Unity `.gitignore`.
- Old project for reference code: https://github.com/atularc/animals-sort.
- Debug APKs built on this PC (JDK and Android SDK through Unity's modules). iOS builds need a Mac or cloud Mac.

---

## Build the prototype (step by step)

Goal: a playable portrait prototype that looks like `Docs/Mockups/Grill-Sort-Mockup-v3-shadow.png`, with verified, rising-difficulty levels.

1. **Data**
   - `FoodType` (id + 3 sprites: raw/medium/grilled + width).
   - `GameArt` ScriptableObject referencing every sprite (food, customers, board, UI, VFX, themes).
   - `ThemeDef` (background, calm background, front layer, counter top).
   - `LevelData` (skewer stacks, capacity, food set, par).
2. **Core logic** (pure C#, unit-testable):
   - `Board` state, move validation for both `MoveRule`s, apply/undo, completion detection.
   - `Solver` (BFS/IDA* with state hashing), returning solvable + par.
   - `LevelGenerator` (seeded; difficulty tiers from §3; look-alike limits).
3. **Bake & verify** (Editor menu "Grill Sort > Bake And Verify Levels"):
   - Generate 2000 levels, solve each, reject unsolvable ones, write `levels.bytes` plus a report (par per level, showing rising difficulty).
   - Beyond 2000, generate at runtime with the same scheme.
4. **Scene builder** (Editor menu "Grill Sort > Run Full Setup"). Authors the Game scene:
   - Camera and background layers per theme.
   - Customer row, grill slots, HUD, booster bar.
   - Level Complete popup and SafeArea.
   - All UI made in the Editor, not at runtime.
5. **Gameplay:**
   - Tap to pick (lift + highlight), tap to drop (arc tween + bounce).
   - Invalid move shake.
   - Completion: glow, steam, sparkles, "Perfect!", then serve to the matching customer (✓, cheer, next customer).
   - Win popup with stars/coins, then next level.
   - Undo / Hint (solver's next move) / Shuffle / Extra skewer.
   - Stuck detection.
   - `limitToTenLevels`.
   - Save progress (PlayerPrefs is fine for the prototype).
6. **Look:** apply the §5 v3 readability rules (darker grills, deep food shadows). Use the calm backgrounds.
7. **Verify:**
   - Enter Play mode with the Unity CLI and take screenshots. Compare with the v3 mockup.
   - Auto-play a few levels using the solver.
   - Zero console errors.
   - Report the difficulty curve from the bake report.

---

## Current state of the prototype

Built 2026-11-17. Playable in `Assets/GrillSort/Scenes/Game.unity` (portrait, 1080×1920 design).

- **Rebuild everything:** "Grill Sort > Run Full Setup" (textures, TMP font + outline materials, GameArt, atlases, the whole scene, player settings). Safe to re-run; it rebuilds `Game.unity` from scratch, so **never hand-edit the scene**: change `GrillSortSetup.cs` instead.
- **Levels:** "Grill Sort > Bake And Verify Levels" bakes **30 levels** into `Data/LevelBank.asset`, re-solves and replays each, and writes `Docs/LevelReport.md`. Tiers live in `LevelGenerator.TierFor`. Past level 30 the same seeded generator runs on device (smaller solver budget).
  - **Solver** (`Solver.cs`): A* on a compact, order-independent state (3 ulongs, own hash table, no per-node allocations). Heuristic = runs of identical pieces − food types left (+1/+2 when no move can merge yet). It is admissible, so par is the true minimum. Works for all three rules. A weighted fallback (`~` par) only runs when the node budget runs out.
  - **Rebaked for `AnyWithAbove` (2026-11-18):** 30 levels in **~44 s**, 0 failures, no `~` pars. Par rises 3, 6, 7, 8 … 27 (+1 per level; a repeat only where the tier drops an empty skewer or adds a food/look-alikes). Target pars live in `LevelGenerator.TargetPars`.
  - True par per food count (AnyWithAbove): 3 foods ≈ 7, 4 ≈ 10, 5 ≈ 14, 6 ≈ 17, 7 ≈ 20, 8 ≈ 24 (max ≈ 29). With 9 grills (8 foods + 1 empty), par tops out near 27, so levels past 30 hover at 24–27. To keep climbing, more grills are needed (two rows).
  - `NextMove` (Hint) takes ~15 ms in the Editor on level 30. Levels past 30 are generated on a worker thread (`LevelBank.Prefetch`, ~0.5 s each in the Editor) while the previous level is played.
- **Move rules** (`GameController.moveRule`): `AnyWithAbove` (**default, Aaron's decision 2026-11-18**: grab any piece, everything above comes along), `TopGroup` (classic Water Sort), `SingleTop`. Levels are baked for `GrillSortLevels.BakeRule` (= `AnyWithAbove`). If the game's rule doesn't match the bank, levels are generated on the device instead (slower).
- **Cooking stages:** not used yet. Play shows Grilled; `cookOnComplete` (Raw while sorting, Raw → Medium → Grilled on completion) is off. The user will decide how to use Raw/Medium later.
- **Testing:** tick `Use Test Level` on the `Game` object and type a number in `Test Level` to start there (progress isn't saved). Changing the number during Play mode loads that level at once. Run Full Setup resets it to off.
- **Other flags on GameController:** `cookOnComplete`, `limitToTenLevels`, `idleHintSeconds`, `verboseLog`.
- **Gameplay done:** tap to lift/drop with arc flight + bounce, invalid shake, completion glow/steam/sparkles/"Perfect!", serve flight to the customer (✓, cheer, next customer arrives), Orders bar, win popup (stars vs par, coins, x2 rewarded ad), Undo / Hint / Shuffle / Extra skewer boosters (counts saved; at 0 a rewarded ad gives +1), stuck popup, pause (sound/vibration), tips on levels 1–3, free idle hint on levels ≤ 10, theme unlock toast (Wood 1, Beach 11, Food Truck 21, Night Market 31, Winter 41), PlayerPrefs save.
- **Grill look:** cartoon red-enamel grills 180 wide with food at ×1.0 so pieces sit on the grate; dimmed grill (`grill_cartoon_dim`, lighter than v3 per Aaron) + deep food shadows; flickering ember glow behind each grill; faint smoke wisps above the coals. The small flames between the food were removed (Aaron).
- **Atlases** are deleted and rebuilt on every Run Full Setup, so they always match the art folders in `GrillSortSetup`.
- **Verify loop:** set Game view to 1080×1920 (`PlayModeWindow.SetCustomRenderingResolution`), enter Play mode, `GameController.StartAutoPlayLevels(n)` plays with solver moves (`AutoPlayLog` holds the results), capture with `unity command capture game_view`. If you're auto-playing while the user clicks in the Game view, the two collide.
- **Not done yet:** real sounds/music (placeholders are synthesized by `Tools/ArtPipeline/bake_game_fx.py`), stripping unused packages, home/level-map screen, shop, real ads/IAP SDKs, iOS haptics.
