# Sort Streak: full research report

Written 2026-11-26. **Sort Streak: Color Puzzle** (Oakever Games, `com.oakever.sortparade`) is studied as the design reference for Grill Sort. We never copy its name, art or exact levels.

**Sources**
- **80 YouTube gameplay videos, levels 1-59.** These are every video still available in the two playlists the user gave:
  - *Beyond Gamer*, "All Levels Complete Solutions": levels 1-30. Build **A**.
  - *Sort Streak Walkthrough*, "All Levels Solution": levels 6-11 and 17-59, plus a 10-minute session covering levels 1-5. Build **B**. 66 of its 117 entries are deleted or private, and one is a different game.
  - Levels 6-30 are covered by both channels, which lets us cross-check them.
- **Frame-by-frame analysis.** Each video gave a starting-board image plus one frame every 2.5 s (350 images). Each level was written up against a fixed checklist; the write-ups are in `levels/<A|B>_Lxxx.md`.
- **Per-level data:** `LevelTable.csv` / `LevelTable.md`.
- **Levels 1-3 played on our own phone**, plus the store page and user reviews.
- **The user's Level 6 recording:** `sort strek.mp4` in the repo root.

**Two builds.** Build A is older: blue background, no IQ bar, bouquet win screen. Build B is newer: pink background, IQ bar, leaderboard, flower-cart win screen, home "Scene" puzzle. Hard levels have the same board in both builds. Some normal levels differ between them.

---

## 1. Core rules (confirmed)

| Rule | Detail |
|---|---|
| Move | Drag any flower; **every flower below it comes along**. Mixed-colour groups are allowed. |
| Drop | On an **empty rope**, or on a rope whose bottom (free-end) flower is the **same colour as the grabbed flower**, if it fits. |
| Full rope | **A rope may only be filled to the top with one colour.** This anti-dead-end rule was found by testing on our phone; the game refuses a mixed full rope. |
| Mystery "?" | A grey flower that shows its colour when it becomes the bottom flower. **"Mystery flowers can only go on empty ropes."** (L24) |
| Serve | A rope full of its own pot's colour is served. The pot flies to the top shelf ("x/N"), the row slides left, and a new rope and pot slide in from the right. |
| Arrivals | New ropes always arrive **full**, often with "?" pieces, so the free space never grows during a level. |
| Queue end | When no orders are left to show, served spots become **white helper ropes**. From L22 a **second white helper** also joins late in the queue. |
| Same colour twice | A colour can be ordered more than once in a level, and **two pots of the same colour can sit on the shelf at once** (seen L12, L29). |
| Helper | The white-pot rope takes any colour and is never served. It sometimes **starts full** (L55, L59) or partly filled. |
| Rejection toasts | "This rope can't hold that many flowers." / "Match the bottom flower to drop here!" / "Mystery flowers can only go on empty ropes." / "Locked flowers can't move yet. Complete the sign's goal to unlock them." / "This rope is locked. Complete the goal on the sign first!" |
| Stuck | No popup. A red-bordered banner says **"No Available Moves - Use a booster to continue or reset"**, and Undo pulses. |
| Reset | A popup: **"Level Reset - Level progress: 2/11. Restarting will reset it. Restart anyway?"** with Restart / Cancel. When a streak is running it warns "You're on a N-win streak. Restarting will end it!". After a restart the layout is the same and **the win streak goes to 0**. |
| No fail state | There is no move limit and no timer. The only way to lose is getting stuck. |

## 2. The level curve (levels 1-59)

| Levels | Rope length | Orders per level | Colours on the start board | Mystery "?" at start | Notes |
|---|---|---|---|---|---|
| 1 | 3 | 3 (B) / 6 (A) | 3 | 0 | hand-placed tutorial |
| 2 | 3 | 7-8 | 4-5 | 0 | Undo unlocks |
| 3-4 | 4 | 10-11 | 5-6 | 0 | repeated order colours start at L3; Hint at L3 |
| 5 | 5 | 12 | 6 | 0 | **first Hard**; Rope booster |
| 6-9 | 4-5 | 10-12 | 5-7 | **6-8** | **mystery introduced at L6** (the breather after the first Hard) |
| 10-59 | **4, 5 or 6 (a difficulty knob)** | **10-13** (peak 14 at L46) | 5-7 (more arrive through the queue) | usually 2-7 (peak **11 at L24**) | vines from L11, piggy at L23/33/43/53 |

- **Rope length is a knob, not a ramp.** After L5 it moves between 4, 5 and 6. **Every Hard level from L20 uses 6-slot ropes;** the L5-L15 Hard levels used 5. Hard levels go back down to about 10 orders but use longer ropes.
- **The number of orders plateaus at 10-13 from L4.** Levels don't get longer; they get *denser*: more "?", look-alike colours, locks and less free space.
- The **piggy-bank bonus rope comes every 10 levels** at L23, L33, L43 and L53.

The exact per-level numbers (ropes, colours, "?" count, free slots, time to first serve) are in `LevelTable.md`.

**What drives difficulty** (from comparing all levels):
- **Free space:** the number of empty slots at the start. Hard levels use 6-long ropes with about 8 free slots, but the flowers are deeply mixed.
- **Mystery count:** 5-8 "?" at the start of mid levels. Queue ropes arriving later carry 3-4 "?" each, so the board gets *less* known as the level goes on.
- **Look-alike colours** used on purpose: red/orange, blue/light blue, purple/lavender/lilac, pink/red. **L40 (Hard) has no "?" at all**; it is hard purely because of look-alikes.
- **Helper rope starting full** (L55, L59) removes the usual safety valve.
- **Blockers** (vines) that open only after a serve, including one whose colour is still in the queue (L19).
- **Pacing:** most levels take **30-50 s before the first serve**, so the player studies the board first. Hard levels take 50-85 s before the first serve.

## 3. Difficulty labels and pacing

- **A "Hard" level every 5 levels:** 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55. No "Super Hard" was seen up to level 59.
- **Before it:** the previous level's win button turns red and reads "Level N · Hard" with an **x2 coin badge**.
- **At the start:** a card with the label and a social-proof line:
  - "Only 50.54% reached the finish. Be one of them!"
  - "59.69% fail here. Try your best!"
  - "40.10% could not do it, the rest are legends"
  - "Only 53.99% pass this level. Can you do it?"
  - "Good Luck!"
- **During play:** "Hard" with a flame icon in red under the level title.
- **Reward:** **double coins**. In build A that's +~200 against +100-130. In build B the doubling can't be confirmed, because a streamer overlay hides the last digit of the coin count from L27 (see the data note at the end). A "Hard x2" stamp appears on the win.
- **After it:** the next level is an easy **breather** (L6, L26, L31 and others). **New mechanics are introduced on breather levels, never on Hard ones:** mystery at L6, after Hard L5; vines at L11, after Hard L10.
- **Hard levels are fixed:** the same board in both builds. Normal levels vary between builds, which suggests some are generated or A/B-tested.

## 4. Mechanics and when they appear

| Level | Feature | How it is introduced |
|---|---|---|
| 1 | Drag, match, pots, helper rope | Hand tutorial: "Hold the glowing flower and drag it with those below onto a matching flower." / "Match flowers with the same-colored pot." / "Empty ropes accept any color." |
| 2 | **Undo** booster (FREE) | Unlocked; booster tiles show "Lv.N" locks until then |
| 3 | **Hint** booster (x3) | Card: "HINT - Make One Correct Move." |
| 5 | **Rope** booster (x1, given free) | Card: "ROPE - Use Rope To Add a Line!" |
| 6 | **Mystery "?" flowers** | Card: "Move the flowers below the mystery flower to reveal its color!" |
| 11 | **Locked Flowers (vine, colour sign)** | Card: "Locked Flowers / Fill a pot of the marked color to unlock these flowers. / Tap to Continue". A clover vine wraps a whole rope; its round sign shows a flower colour; it bursts when that colour's pot is served. Can lock the helper rope too (B L11). |
| 19 | **Chained vine** | The sign's colour is not on the board yet; it arrives from the queue later. |
| 21 | **Countdown vine (number sign)** | The sign shows a number that drops by 1 per serve anywhere (L21, L33, L36, L38, L43, L46, L58). |
| 23 / 33 | **Piggy-bank pot** | A pig-shaped pot whose rope takes gold coin pieces. It is not an order. On the win a hammer smashes the piggy (+30 to +800 coins). The IQ bar is hidden on piggy levels. On L43 the piggy rope itself is vine-locked. |
| 22+ | Second white helper rope joins late | |
| 53 | Several vines that all open on one serve | 4 vines open on the first red serve |

## 5. Feedback, text and juice

- **Combo praise while serving** (quick serves chain): "Nice!" -> "Great!" -> "Excellent!" -> "Amazing!" -> "Incredible!" -> "Unstoppable!" -> "Legendary!" -> "Legendary x2 ... x7".
- **Serve:** the flowers burst into the pot, the pot flies to the top shelf, petals fly, and a butterfly lands on the full pot.
- **Drop target:** the target column is highlighted in green while dragging; a red highlight and an X mark invalid targets (tutorial).
- **Start banner:** "N Streak! You're on a roll!" / "Keep blooming!" / "Petal power!" / "You've found your flow!" / "Brilliant sorting!" / "What a streak!" / "A new high for you!".
- **Loading screens** between levels show quotes (a Buddha quote before L52). A review asked for more time to read them.

## 6. Win flow (build B, in order)
1. **"IQ GENIUS!"** splash with a sunflower when the IQ bar reached 180 (not on every win).
2. **Leaderboard**: a flower-points podium. "1-15 Promotion Zone". Lines: "The top spot is yours! Brilliant play!", "First place achieved! Give yourself a cheer!", "On Fire!", "Moving Up!".
3. **Flower cart**: the cart of collected pots fills up. Praise word ("Perfect!", "Awesome!", "Impressive!", "Superb!", "Flawless!", "Unstoppable!", "Fantastic!"). Tiles for **Time, Steps and Win Streak**. A percentile line: "Time Wizard! Your time beats 99% of players!", "Swift as an Arrow! Your time ranks in the top 1%, brilliant!", "Strategic Genius! For steps, you're in the top 1%!", "Speed King! ... crushed 98% of opponents!", "Blazing Speed! ...", "Minimalist Master! ...". Some wins also show a **letter grade** (S, B).
4. **Coins**: +~10-13 a level in build B, +100-130 in build A, x2 on Hard. Piggy payouts on top.
5. **Next** button. Before a Hard level it is red: "Level N / Hard / x2".

Build A: a bouquet win screen with "Perfect!" and coins, then Next.

## 7. Meta and retention

| Feature | First seen | Notes |
|---|---|---|
| **Win streak** | L3 (B) / L6 (A) | A start banner every level and a trophy popup on records. A restart or getting stuck ends it. The reset popup warns about it. Reviews: "Don't need streak flashed at start of every game". |
| **IQ bar** (40 / 90 / 180) | L4 (B) | Fills as orders are served; reaching 180 shows "IQ GENIUS!". Hidden on piggy levels. |
| **Profile popup** (avatar + nickname) | after L5 | |
| **Home "Scene" puzzle** | early (B) | A jigsaw picture gains one piece per win ("6/25" ... "20/25"). It is the long-term collection goal. |
| **Leaderboard / "New Season" event** | L20 (B) | Lock "Lv.20" on Home; the "New Season" popup comes after L20. |
| **Daily Challenge** | unlocks at L21 | "Reach level 21 to unlock the Daily Challenge". |
| **Theme cards** | L26 | "Snowy Gingerbread Village": new scenery as you progress. |
| **Daily Streak** | L41 | 7-day calendar with gifts on Day 4 and Day 7: "Another day, another lovely win!". |
| **Flower cart collection** | L1 | The served pots of each level fill a cart. |
| **Notifications prompt** | after L1 | Android permission dialog. |

## 8. Economy and monetisation
- **Coins** come from wins, Hard x2 and the piggy bank. Coins pay for boosters; Undo costs 400 coins once the free ones run out (from the Level 6 recording).
- **Boosters:** Undo (FREE at first), Hint x3, Rope x1 free, Reset (always free, but it ends the streak). Boosters were barely used in any video; good players don't need them before L59.
- **Store and reviews:** ads only, no IAP on Play.
  - Interstitials after level 10, and "after each game there is a long ad".
  - Rewarded ads for boosters; a rope from an ad lasts one level.
  - The top complaint is ad volume.
  - The second is "No more moves" judgments that players found unfair. The developer replied that they would review "the move judgment".

## 9. GUI / UX notes
- **HUD:** Back arrow, Restart (build A), "Level N" plus difficulty label, coins pill. Then the IQ bar, then the order shelf ("x/N"), then the main shelf with pots, then the ropes. Booster bar at the bottom: Undo, Hint, Rope, Reset. Locked boosters show "Lv.N".
- The **order queue is visible as a shelf of empty pots** that fill with flowers. It shows the level's length at a glance.
- The **pot colour on top of each rope** is the order; the rope colour matches the pot.
- **Readability problems they accept:** look-alike colours on purpose; "?" pieces are plain grey.
- **Calm palette and soft UI.** The tutorial uses a pointing hand and a glow on the piece to move.

---

## 10. What this means for Grill Sort (recommendations)

**Rules to add (cheap, high value)**
1. **"A tray may only be filled to the top with one food."** This is Sort Streak's anti-dead-end rule. Add it to `Board.CanMove`, the solver and the generator. Toast: "Fill a tray to the top with one food only!"
2. **Mystery pieces can only be moved onto an empty tray.** Toast: "Wrapped food can only go on an empty tray!"
3. Keep our rejection toasts and add the stuck banner style: a non-blocking banner plus a pulsing Undo, as we already do. **Never tie the stuck state to an interstitial** (it's their top complaint).

**Level design (replaces the straight ramp in `LevelDesignPlan.md`)**
1. **Hard every 5 levels** (5, 10, 15 ...). It is teased on the previous win button (red, "Hard", "x2 coins") and has a start card with a "% of players fail" line. It pays x2 coins, and the next level is a breather.
2. **Introduce each new thing on the breather level right after a Hard level:**
   - Undo at L2, Hint at L3, Add Tray at L5;
   - foil "?" food at L6;
   - locked tray (vine-style lock with a food sign) at L11;
   - countdown lock around L21;
   - bonus coin tray (piggy) around L23-33.
3. **Rope length = tray length by stage:** 3 at L1-2, 4 at L3-4, 5 from L5, and 6 on Hard levels.
4. **10-12 orders per level from L3**, with repeated foods and arrivals that come full and carry "?".
5. **Difficulty knobs, in order of effect:** free space, "?" count (more on later queue trays), look-alike foods, whether the helper starts full, and locks whose food is still in the queue.
6. **The 5-tier demo for Aaron:**
   - Easy: like their L4.
   - Medium: their L6 style, with the first "?".
   - Hard: their L10/L15 Hard style.
   - Very Hard: lock + "?" + look-alikes, like L19 or L29.
   - Super Hard: like their L40, with 6-long trays, look-alikes and a full helper.

**Retention and meta to match or beat**
- **Win streak** with a start banner (keep it short) and a reset warning.
- **A chef-score bar** with 3 marks, like the IQ bar, plus a "MASTER CHEF!" splash.
- **Combo praise** for quick serves (Nice ... Legendary xN). This is cheap and very juicy.
- **Win screen stats:** Time, Moves and Streak, with a percentile line and a grade.
- **A collection meta:** we already have the Home stall decorations. Add a "pots/skewers served" collection like their cart and jigsaw scene.
- **Daily streak calendar** (we have Daily Rewards behind Remote Config), **Daily Challenge from L21**, and a **leaderboard season from L20** (later).
- **Theme cards** when a new stall unlocks (we have the themes).

**Avoid**
- Their ad load: an interstitial after every level and ads at stuck moments.
- Flashing the streak at the start of every level.
- Unclear "no moves" judgments. Our stuck popup gives a reason line; keep that.

## 11. Data notes
- **Build B coin numbers from L27 are 10x too small.** The YouTuber's face-cam overlay hides the last digit, so "+11" really means about +110. For the same reason the piggy payout reads +300 at L23 but +30 at L33/L43; it is really about +300 each time.
- **Some write-ups call "their" level the first of something** (e.g. the first Hard at L10, L25 or L35). The real first appearances are in `LevelTable.md`, "First appearance", which also lists 19 contradictions between files that were resolved.
- **What a vine-covered rope holds can't be seen,** so free-slot counts on those levels are approximate.
- **Coverage:** no build B videos exist for L12-L16, and no build A videos exist after L30.

## 12. Open questions (not visible in the videos)
- **Boosters:** the booster bar is cropped out of build B videos, and build A players never used a booster, so costs after the free uses are unknown.
- **Order size vs rope length:** whether the order size always equals rope length.
- **Streak rewards and resets:** what a high streak gives, and why the win streak sometimes reset to 0 or 1 with no restart shown.
- **Countdown vines:** whether the number counts serves of any colour or only certain ones (L58 is ambiguous).
- **Daily Challenge and leaderboard rewards:** what they pay out.
