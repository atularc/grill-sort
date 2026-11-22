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

**REWORKED 2026-11-19 after Aaron played the demo (2026-11-18):** "we're still following Sort Streak for the core, only with a new theme. We don't want it to play like Water Sort ... The main action is still dragging pieces from the bottom across the trays, so the 5th grill isn't needed." And: "each customer's order should line up with a row, so players need to sort the matching items into that row to serve it." (Sort Streak: Oakever Games, `com.oakever.sortparade`; flowers on ropes, completed groups go into the matching pot. Never copy it.)

**Aaron 2026-11-19 (after our space question + the Sort Streak video in `WhatsApp Video 2026-11-19 at 09.53.46.mp4`):** "follow it closely ... each customer tray holds the number of pieces in that customer's order (e.g. 4 or 5 depending on the level, and later levels can ask for more), plus 2 helper trays without customers, one on each side, used only to move pieces around. The helper trays look clearly different." And: "could we drag from the bottom like in Sort Streak?"

```
State: helper | customer trays (2-5) | helper. Each tray holds exactly P pieces (P = order size: 4, later 5).
       Food HANGS from the top of each tray (slot 0 at the top); the bottom piece is the free end.
       Customer k stands right above the k-th customer tray; helper trays have no customer (steel frame).
Move:  drag any piece; every piece BELOW it comes along. Valid if the whole group fits and the target tray is
       empty or its bottom piece is the same food ("Match the bottom food to drop here!" /
       "This tray can't hold that many!"). Tap-tap still works.
Done:  a customer tray holding its P pieces (nothing else) is served; it empties and stays as free space.
Win:   every order served.
```
- Logic is unchanged from a normal stack (index 0 = fixed end); only the drawing is flipped (`GrillView.SlotY`).
- `MoveRule` stays a setting (`AnyWithAbove` default = "the grabbed piece and everything toward the free end").

**Customers & orders:** one customer above each customer tray, all visible at once (2–5). The bubble shows the food for the tray below. "Orders x / y".

**Cooking stages (we have Raw → Medium → Grilled art for all 18 foods). How to use them is OPEN; ask the user.** Default plan:
- During play the food shows **Grilled**. This is what the client approved in the mockup.
- Optional flag `cookOnComplete`: pieces show **Raw** while sorting. When a skewer completes it cooks Raw → Medium → Grilled (~0.35 s per step, sizzle + steam + sparkle), then it's served.

**Boosters** (bottom bar, yellow tiles with red count badges): **Undo**, **Hint**, **Shuffle**, **Extra tray (+)** (an empty tray without a customer; Sort Streak has "Add a Rope").

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
- **Layout:** one row of trays, max 5 orders (+1 extra tray), each customer centred above their tray (`StageLayout`).
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

**Aaron's art sign-off feedback (2026-11-18), grill marks:** "too strong (see the corn) ... softer and lighter, like in our mock. Some items don't need grill marks at all." Reference he named for overall style: **Skewer Jam: Food Game** (iKame's own game, `com.ig.skewer.jam.food.sort`): thin, light, same-hue marks; corn, shrimp, peppers, tomatoes shown without marks.
- `FoodFinal_v3/Grilled/` (2026-11-19) = `FoodFinal_v2` with the marks softened: thinner, lighter, tinted with the food's own colour. Marks removed on corn, shrimp, tomato, broccoli (our pick, Aaron only named corn; the user wanted the pepper to keep its marks). Alpha unchanged, so `Generated/ShadowsFinal/` still fits. Script: `Tools/ArtPipeline/soften_marks.py` (knobs `STRENGTH`, `THIN`, `NO_MARKS`). Comparison: `Docs/Mockups/Food-Grill-Marks-Before-After.png`. The game still uses `FoodFinal_v2` until `GrillSortSetup.FoodDir` is switched.

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

**Update 2026-11-21, Aaron's combined feedback (not committed yet).** Aaron also said to keep working on parts the sort mechanic won't touch (UI screens, food/customer animations, win/lose flow) while they review. Everything below was saved as NEW files, with the old ones kept:
1. **Tomato** "too juicy, not grilled": `FoodFinal_v4/Grilled/tomato.png` (new Figma image: wrinkled, split skin, roasted patches, soft marks, no drops) + `Generated/ShadowsFinal_v4/`. `GrillSortSetup.FoodOverrideDir`: any food found there replaces the `FoodDir` one.
2. **Characters** "style off, head sizes vary": `CustomersPeople_v2/` = the same 8 people redrawn in the food's style (bold outline, cel shading) and normalised to one face width and eye line (`cut_people_v2.py`, `FACE_PX` hand-measured). `CustomerView.BodyY` 80 → 118 to match. Old set = `CustomersPeople/`. Sprites keep a 4 px transparent edge (atlas bleed).
3. **Table + curtain smaller**: `Backgrounds/<Theme>/layer_front_compact.png` (band squashed to 62%, `compact_counter.py`); `ThemeDef.counterBottom` / `StageLayout.counterBottom` put the trays just under the curtain.
4. **Fire glow on its own layer + gentle flicker**: `split_grill_layers.py` → `grill_cartoon*_bed.png` (glowing coal bed, drawn first) + `*_front.png` (frame/coals/bars with the glow cut out, drawn on top) + `grill_helper_front.png`. The flicker tints the bed's brightness (.74–1, slow). The 11-18 split (`*_base`/`*_glow`) is unused.
5. **Orders are skewers**: the bubble shows a small tilted skewer with the order's pieces (`SkewerStack`, bubble 224 wide). On completion the pieces slide together on the tray, pop onto one small skewer (`GameUi.serveSkewer`), and it flies up, tilting and shrinking into the bubble (`GameController.ServeSkewer`, which replaced `ServePiece`).
6. **Softer UI shine**: `Art/UI_Soft/` (glare pulled toward the base colour, white symbols protected, `soften_ui.py`) + `Generated/bar_fill_soft.png`. The builder's `S("UI/...")` reads `UiDir`. `Art/UI/` is the backup.
- 9-sliced sprites (borders dict + `bar_track`) are imported as **Full Rect**: the atlas trimmed their margins, and sliced images then showed neighbours' colours as lines above the HUD pills.
- **Win popup polish** (user, same day): warm glow + slowly turning sun rays behind (`Generated/rays.png`, `make_win_fx.py`), stars in an arc that pop into recessed sockets (an unearned star leaves an empty socket), sparkles, coins in a pill that count up, bigger buttons with an AD tag, everything pops in one after the other, popup dim .66 → .76. The ribbon title is centred on the red band (81.5 px from the top of `popup_panel`). The user tried "Level Complete!" above the panel and preferred it inside: keep it inside.
- **Loading screen v2** (user, after the client's FishUp! video; v1, a theme background with grill/sign stickers and text letters, was rejected as "too basic"): `GameUi.loading`, built by `BuildLoading`, visible from the first frame.
  - Art: ONE full-screen scene `Loading/keyart_default.png` (cover-fit) = **v3b**, the user's pick: a bright sky-blue beach with sun rays, a cute red grill of skewers on a wooden stand, light sand, and palm leaves and an awning framing it. It was modelled on the client's FishUp! loading screen. Rejected: v1 painterly ("too realistic"), v2 flat sunset ("overwhelming"), v2 calm ("faded out"), v3a platter. Use bright colours with ONE strong centerpiece, never wash colours out.
  - Logo: the logo image `Loading/logo.png`, a "Grill Sort" placeholder to regenerate once the name is final. `make_loading_art.clear_trapped_white` removes the white background trapped between the two words.
  - Mascot: `Loading/mascot.png`, a sausage chef.
  - Sequence: the logo drops in with a squash, then the mascot pops up, bounces and waves; rays turn, steam and embers rise off the painted grill (children of the background, normalised coords), sparkles twinkle, the bar fills on the table. Then the mascot hops off, the logo pops away, and it fades.
  - `_loadingScreen` holds the level intro, customers and tips until it lifts.
  - Also shown **between levels** like FishUp! (`loadingBetweenLevels`, on by default): after Next it fades in over the finished level, the next level is set up behind it, then a shorter run plays (`loadingBetweenSeconds` 0.7 s bar, animations ×`loadingBetweenSpeed` 2). That is about 1.7 s in total, against FishUp!'s ~2.5 s, because our levels load instantly; set the bar to ~1.9 s and the speed to 1 to match them exactly. It's skipped during autoplay. It's the natural slot for interstitial ads later.
- A finished tray is moved to the front (`SetAsLastSibling`) so its glow, sparkles and "Perfect!" draw over the trays to its right.
- **Tutorial hand + banner** (matched to the client's FishUp! tutorial).
  - Hand: the user's pick, the Pngtree "hand click cartoon for game" (`ArtSource/Cartoon/ui/tutorial_hand_pngtree.png`). Its baked yellow glow is removed by `Tools/ArtPipeline/cut_hand_pngtree.py` → `icon_hand.png`, with the pivot at the fingertip (0.078, 0.9), `HandSize` 250. **Licence:** Pngtree assets usually need a premium licence for commercial use, so check before release, or ask iKame for FishUp!'s own hand. The five generated gloves (v1–v5) were all rejected and are kept as `icon_hand_v1..v5.png`; don't generate more. Lives in `HandLayer` above the board and under the popups.
  - Edges: `HandScale` checks the hand's position every frame and turns it (mirrors it) only while its body would leave the right edge, so it's never cut off on either side. `HandLoop` moves it onto the piece and presses twice, then carries a see-through copy of the piece (`handGhost`) to the tray and lets go, on repeat.
  - When it shows: levels ≤ `tutorialLevels` (2) after 0.9 s idle, looping until the player moves; also for the idle hint (≤ 10) and the Hint booster.
  - Banner: tips and toasts are a white pill with bold navy text ("Drag food to fill each tray!"). On tutorial levels the banner stays up the whole level; a toast replaces it briefly, then it comes back.
  - `LoadLevel` closes the win/stuck popups and resets their pop-in scales, so a reload during the win animation can't leave a stuck popup without "Next".
- **Logo** = option 1, the two-tone design (orange "Grill", turquoise "Sort", navy outline). `logo_v1.png` (yellow/red) is the backup; options 2 and 3 are in `ArtSource/Cartoon/ui/logo_opt*.png`.
- **Ember shimmer:** `*_hot_a/_hot_b.png` (`split_grill_layers.py`) = brighter patches of the bed, `GrillView.coalHotA/B`, pulsed out of step over the breathing bed.
- `Generated/bar_track_clean.png`: `bar_track.png` had a flat ghost of the old fill (two-tone inside); used by the Orders bar and the loading bar.
- **App icon** (user pick): the "Grill Sort" text logo (`Loading/logo.png`) on the loading screen's bright sky, built by `compose_icon.py v4_text_logo` → `ArtSource/Icon/icon_game_v4_text_logo.png` → `Assets/GrillSort/Icon/app_icon.png`. Never put it on a plain-colour background. The earlier "three trays" icon is `icon_game_v1_three_trays.png`. `GrillSortSetup.SetAppIcon` sets the default/iOS icon and the Android legacy + round icons. The adaptive icon uses `app_icon_adaptive_bg.png` (icon at 70% on a blurred copy).
- **Win screen** = FishUp!-style "Well Done!" (`BuildWinPopup`), full screen over the dimmed level with no panel, stars or logo. It has "Well Done!" (TMP), a glowing coin bubble with rays and sparkles, a multiplier bar x2 x3 x5 x3 x2 with the sausage mascot as the sliding pointer, then Claim and "Claim xN" (an ad; the pointer stops on press). Coins fly into a coin pill that counts up, then the between-levels loading screen plays. Bar and buttons are both 820 wide. Button rows use measuring HorizontalLayoutGroups so the coin never touches the number. The dim is 0.95 because Linear colour space makes 0.8 look like 0.5. FishUp!'s extras (Add Slot bar, locked levels, ad banner) are left out.
- **Settings panel** (was Pause): red ✕, three toggle tiles (sound / music / vibration; off = grey tile + red slash), Restore Purchases (`IIapService.Restore`, placeholder), Restart + Continue. Icons are cut from one Figma sheet by `cut_settings_icons.py`.
- **Audio** (all CC0, sources and licences in `ArtSource/Audio/LICENSES.md`; built by `Tools/ArtPipeline/make_audio.py` → `Audio/SFX`, `Audio/Music`):
  - Effects: Kenney packs; steel-drum jingles for win, serve and no-moves.
  - Pick and drop are synthesized soft pops: the user found the pluck and thud versions bad. A dragged stack plays one pop.
  - The sizzle and the sizzle ambience are synthesized too.
  - `AudioManager` has 8 voices, each with its own pitch, plus a music source and an ambience source with fades.
  - Music (user pick): "Which Brand of Mustard Shall I Buy?" (CC0) → `Audio/Music/bgm_main.ogg`. It's cut at 123.15 s, where it repeats its opening, just before the file's fade-out, and the seam is crossfaded so it loops with no dip. Volume 0.30 (user: "lower the music a little"). Music button = On/Off. The other candidate tracks stay in `ArtSource/Audio/Music`, outside the build.
  - The constant sizzle ambience is OFF (`AudioManager.ambienceOn`, user: "not good").
- **iKame splash** (2026-11-22, client reference in `Downloads/Ikame sfx and splasg screen refernce`): Unity's own startup splash (Player Settings > Splash Image), NOT a Canvas screen. Logo = `Loading/iKame Flame Mascot Logo.png` (set by the user, 2 s), Unity logo off, background = `Loading/splash_bg.png` (purple 51,17,140 with a soft lighter glow), no blur, no overlay. Run Full Setup doesn't touch it.
- **iKame-style taps** (2026-11-22): measured on the client's FishUp!, Skewer Jam, Screwdom 3D and Mahjong recordings and rebuilt in `make_audio.py` (`tock`, `ikame_click`). The UI click is a warm double "tok-tok" 50 ms apart; pick and drop are a wooden tock (~420-470 Hz) with a crisp edge. A dropped stack plays one tock per piece, 0.12 s apart, stepping up in pitch (`GameController.DropSound`). The recordings can't be used directly (music under every sound, WhatsApp compression); the exact files would have to come from iKame.
, `-Customers-Tomato.png`. Prompts: end of `Docs/ArtPrompts.md` (9 Figma images used).

**Update 2026-11-19 (not committed yet):**
- **Core = Sort Streak** (see §2): helper | customer trays | helper, tray size = order size (4, then 5 from level 24), food hangs from the top, drag a piece + everything below it, drop on a matching bottom piece. Helper trays: `Generated/grill_helper_base.png` (steel frame, `Tools/ArtPipeline/make_helper_grill.py`), `GrillView.helper`. Up to 8 grills authored (`LevelGenerator.MaxGrills` = 5 customers + 2 helpers + 1 Extra tray).
- **Solver** (`Solver.cs`): ordered trays with targets, h = unsettled runs (+1 if no reducing move), admissible/consistent, up to 8 trays x 7 slots. ~1-20 ms per level.
- **Levels:** 50 baked, 0 failures, 0.2 s. Tiers in `LevelGenerator.Tiers` (start level, customers, pieces, look-alikes, par range): L1 2x4, L2 3x4, L4 4x4, L9 5x4, L16 5x4 + look-alikes, L24 5x5, L38 5x5 + more look-alikes. Par 4 -> 24 by L41, then 23-25.
- **Fire:** coal glow split out of the grill art (`Generated/grill_cartoon*_base.png` + `_glow.png`, `split_grill_glow.py`), flickers in `GameController.LateUpdate`. **Smoke:** 4 light wisps per grill along its length.
- **Backgrounds:** Beach for levels 1-50 (Aaron wants the demo's Beach), then Food Truck 51, Winter 61, Wood 71, Night Market 81.
- **Food:** `FoodFinal_v3` (soft marks; Aaron: "look much better now").
- Verified in the Editor: autoplay L1-3 and L40 at par, drag / invalid drop + toast tested by sending drag events (the CLI's simulated pointer doesn't reach the UI), zero console errors. Not yet tried with a real finger on a phone.

Built 2026-11-17. Playable in `Assets/GrillSort/Scenes/Game.unity` (portrait, 1080×1920 design).

- **Rebuild everything:** "Grill Sort > Run Full Setup" (textures, TMP font + outline materials, GameArt, atlases, the whole scene, player settings). Safe to re-run; it rebuilds `Game.unity` from scratch, so structural changes belong in `GrillSortSetup.cs`. **The user hand-tunes UI layout in the Inspector** (2026-11-22: "I will fix it myself, make sure you don't mess it up when you run the full setup"). `LayoutKeeper` (Editor) protects those tweaks:
  - Before each rebuild it saves unsaved edits, diffs the scene against `Data/LayoutBuilt.json` (the last build), and stores hand-changed props in `Data/LayoutOverrides.json`. Tracked: position, size, scale, rotation, anchors, pivot, colour, TMP font size, layout spacing.
  - After the rebuild it re-applies them.
  - Never delete `LayoutOverrides.json` or run "Grill Sort > Forget Manual Layout Tweaks" unless the user asks.
  - Renaming or re-parenting a UI object changes its path, which drops that object's tweak, so tell the user when a change does that.
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
