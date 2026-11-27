# Grill Sort: level design plan (Sort Streak model)

Written 2026-11-26. Sources: Aaron's Slack thread of 2026-11-25 (art sign-off + next steps), the Sort Streak
recording `sort strek.mp4` (Level 6, 83 s, repo root) and the Sort Streak Level 6 screenshot, and a review of our
generator, solver and the 50 baked levels (`Docs/LevelReport.md`).

Sort Streak (Oakever Games) is a reference for **level design and pacing only**. We never copy its name, art, flowers,
pots, ropes or exact levels.

---

## 1. What Sort Streak does (from the video, Level 6)

| Element | What we saw |
|---|---|
| Board | 6 ropes in one row: 1 white **helper rope** (left, never leaves) + 5 **order ropes**, each coloured like its pot. |
| Order size | **5** pieces per order; every rope holds 5. |
| Orders | **10 pots** in the level ("0/10" shelf). Colours **repeat** in the queue (red was served twice). 6 colours. |
| Start fill | About 22 of 30 slots filled, so ~7-8 free slots of slack. |
| Mystery pieces | About **9 of 22** starting pieces are grey **"?"** pieces. A "?" turns into its real flower when it becomes the free (bottom) piece. New ropes also bring "?" pieces. |
| Serve | 5 of the rope's own colour: flowers burst into the pot, the pot flies to the shelf, the row shifts left, a new rope + pot slides in from the right. |
| Arrivals | The new rope arrives **full** (5 pieces, some "?"). So the slack never grows: the pressure stays the same all level. |
| Free placement | Any piece can sit on any rope as long as it matches the bottom piece; only serving needs the rope's own colour. |
| Feedback | Green column highlight on the drop target, praise words ("Good Luck!", "Nice!"), petal bursts on every drop. |
| Score bar | "IQ" bar with milestones **40 / 90 / 180** above the shelf; it fills as orders are served. |
| Boosters | Undo costs **400 coins** (no free count), Hint x3, Add Rope x1, **Reset** is free. |
| Retention | Reset warns **"You're on a 5-win streak. Restarting will end it!"**, so a win streak exists. |
| Session | ~80 s for Level 6, with a ~10 s think at the end and a Hint used. |

**Why it plays well:** the "?" pieces mean you can't solve the board by looking at it. You plan around what you know,
uncover pieces, and adapt. Full arrivals keep the pressure steady, and colour repeats make the player keep a colour
"parked" on the helper rope for later.

## 2. Why our levels feel basic

1. **Nothing new after Level 1.** Levels 2-50 use the same rules; there is no mechanic to learn or master.
2. **Everything is visible.** With no hidden pieces, a good player solves the board by inspection; there is no discovery.
3. **Difficulty is one straight ramp.** Par climbs 3 -> 37 with almost no breathers and no "Hard level" moments, so
   it feels flat and then tiring. The only knobs are order count, food types and look-alikes.
4. **Order size is 4.** Sort Streak uses 5; longer stacks give the "grab a piece and everything below it" rule more
   interesting choices.
5. **Arrivals are not designed.** Queued trays get the leftover pieces split evenly, so the slack drifts up and down
   and the pressure is accidental.
6. **Par is our only measure.** Par says how long the solution is, not how easy it is to go wrong. A 20-move level
   with no traps is easier than a 12-move level with three.
7. **Only Level 1 is hand-made.** Every mechanic intro needs a hand-made teaching level.

## 3. The level design framework

### 3.1 Board model (match Sort Streak)
- Row = 1 **helper tray** (no customer, never leaves) + 2-5 **customer trays**. Tray capacity = order size.
- **Order size 4 in onboarding (L1-5), 5 from L6.** Aaron already allowed "4 or 5 depending on the level".
- Arriving trays come **full**, so the starting slack is the slack for the whole level.
- Foods may **repeat** in the queue (never two of the same order on screen at once).

### 3.2 Difficulty knobs (one row per level in a level plan table)
| Knob | Easy | Hard |
|---|---|---|
| Customer trays on screen | 2-3 | 5 |
| Orders in the level (queue length) | 2-5 | 10-15 |
| Food types | 2-3 | 7-9 |
| Order size | 4 | 5 |
| **Slack** (empty slots at start) | 8-10 | 4-5 |
| **Mystery pieces** (share of pieces, and how deep they sit) | 0 | 40%+, deep |
| Look-alike foods in one level | 0 | 3 groups |
| Repeated orders in the queue | 0 | 3+ |
| Blockers (see 3.4) | none | 2 kinds mixed |

### 3.3 Difficulty score (more than par)
The baker measures every candidate with the solver and scores it:
- **Par**: the shortest solution (we already have this).
- **Trap rate**: the share of legal first moves (and moves after each serve) that lead to an unwinnable board.
- **Random-play win rate**: 200 playouts by a "sensible but naive" bot; a low win rate means the level needs thought.
- **Hidden-info risk**: how often the naive bot loses because of what a "?" turned out to be. Too high = the level
  feels unfair (luck), too low = the "?" doesn't matter.

`Difficulty = a*par + b*trapRate + c*(1 - botWinRate)`, with weights tuned once on hand-played levels.
Each level in the plan gets a **target band**; the generator keeps the candidate inside the band.
**Fairness rule:** every level must be solvable with full knowledge (proved by the solver), and the naive bot must
win at least ~15% of runs with hidden info, so a careful player never loses to bad luck alone.

### 3.4 Mechanics roadmap (one new thing at a time, each with a hand-made teaching level)
| Intro level | Mechanic | Rule | Theme look |
|---|---|---|---|
| L1-3 | Basics | drag, match the bottom food, serve | (exists) |
| L3 | Queue | first new customer slides in | (exists, needs a tip) |
| L6 | Order size 5 | trays and orders grow to 5 | taller trays |
| **L8** | **Mystery piece** | hidden until it becomes the free (bottom) piece | piece wrapped in **foil** with a "?" |
| **L22** | **Locked tray** | an extra tray with a padlock + counter, opens after N orders | chained grill with a number |
| **L32** | **Frozen piece** | can't be grabbed, and pins the pieces above it; thaws when the tray next to it is served | ice cube around the food |

Locked and Frozen are our own additions (not seen in the video), so they make our game better than the reference.

### 3.5 Pacing: a sawtooth, not a ramp
In every block of 10 levels: steady ramp, a **Hard** level at x5, a **Super Hard** level at x0, and the level after
each one is a **breather** (~30% easier). A new mechanic is never introduced on a Hard level.

| Level | Role | Trays | Orders | Size | Foods | Slack | Mystery | Notes |
|---|---|---|---|---|---|---|---|---|
| 1 | tutorial | 2 | 2 | 4 | 2 | 8 | 0 | hand-made (exists) |
| 2 | tutorial | 3 | 3 | 4 | 3 | 8 | 0 | hand-made |
| 3 | teach queue | 3 | 4 | 4 | 3 | 7 | 0 | hand-made, tip "new customer!" |
| 4-5 | ramp | 4 | 5-6 | 4 | 4 | 7 | 0 | |
| 6 | teach size 5 | 4 | 6 | 5 | 4 | 8 | 0 | breather-level difficulty |
| 7 | ramp | 4 | 7 | 5 | 5 | 7 | 0 | |
| 8 | **teach mystery** | 4 | 6 | 5 | 4 | 7 | 2-3 | hand-made, tip |
| 9 | ramp | 4 | 8 | 5 | 5 | 6 | 20% | |
| 10 | **Hard** | 5 | 9 | 5 | 5 | 6 | 25% | badge, bonus coins |
| 11 | breather | 4 | 7 | 5 | 5 | 7 | 15% | |
| 12-14 | ramp | 5 | 8-9 | 5 | 6 | 6 | 25-30% | |
| 15 | **Hard** | 5 | 10 | 5 | 6 | 5 | 30% | |
| 16 | breather | 5 | 8 | 5 | 6 | 7 | 20% | look-alikes start (1 group) |
| 17-19 | ramp | 5 | 9-10 | 5 | 6-7 | 6 | 30% | |
| 20 | **Super Hard** | 5 | 11 | 5 | 7 | 5 | 35% | |
| 21 | breather | 5 | 9 | 5 | 6 | 7 | 25% | |
| 22 | teach locked tray | 5 | 9 | 5 | 6 | 5 (+lock) | 25% | hand-made |
| 23-29 | same pattern | 5 | 10-12 | 5 | 7-8 | 5-6 | 30-40% | Hard at 25, Super Hard at 30 |
| 31-40 | same pattern | 5 | 11-13 | 5 | 8 | 4-6 | 35-45% | frozen at 32, Hard 35, Super Hard 40 |
| 41-50 | mix everything | 5 | 12-15 | 5 | 8-9 | 4-5 | 40-50% | Hard 45, Super Hard 50 |
| 51+ | endless | 5 | 12-15 | 5 | 8-10 | 4-6 | 40-50% | the same 10-level sawtooth repeats, mechanics mixed by seed |

These numbers are the **starting draft**. The baker will move them once the difficulty score exists.

### 3.5b Designed, not random (user 2026-11-26)
Levels 1-50 are **designed**, not rolled by a random generator:
1. Each level has a **recipe**: its role (teach / ramp / Hard / Super Hard / breather) and the one idea it is about.
2. The idea is built from named **patterns**, placed on purpose:
   - **Buried order:** the food a customer wants sits deep under other food, so the player must dig it out in order.
   - **Parking:** a food has no free tray yet, so the player must park it on the helper tray and remember it.
   - **Repeat order:** the same food is ordered twice, so serving the first one early is a trap.
   - **Hidden key:** a "?" hides the piece that unlocks the board, so the player must uncover it first.
   - **Late arrival:** a queued tray brings the missing piece, so the player must keep space for it.
   - **Look-alike decoy:** a similar-looking food (corn vs. pineapple) is placed where a quick glance misleads.
3. The layout is built in the **level editor** from the recipe; the solver proves it can be won and gives par,
   trap rate and bot win rate.
4. If the numbers miss the level's target, the layout is edited, not re-rolled.
Only levels past 50 (endless) use the generator, and it reuses the same patterns.

### 3.6 Aaron's 5-level tier build (his next step)
- **No new branch.** It's the same game with a switch (`tierDemo`) that plays 5 levels picked from the real curve:
  **Easy** (~L4), **Medium** (~L9), **Hard** (L10), **Very Hard** (~L19), **Super Hard** (L20).
- On screen they are "Level 1-5", each with its tier name on the start banner ("Level 3: Hard").
- Home screen and Daily Rewards are off (Remote Config default), so it opens straight into Level 1.
- The switch is on only for that APK (`Grill Sort - 5 Tier Demo.apk`), shared on the Slack thread like the
  earlier builds. The normal build keeps the full 50-level game.

### 3.7 Korean BBQ skewers (Aaron: good market signals)
New cartoon food in the `FoodFinal` style, used in the early levels: e.g. **tteok** (rice cake), **eomuk** (fish cake),
**dakkochi** (chicken + leek), **sotteok** (sausage + rice cake), **bulgogi** beef, **galbi**, **garlic**,
**green chili**. About 8-10 Figma images. They count as new foods, so the look-alike groups need updating.

---

## 4. Task list (Trello cards)

Lists: **Aaron feedback build**, **Level design system**, **Mechanics**, **Content and tuning**, **Decisions**.
Each `###` below is one card; its bullets are the checklist.

### List: Aaron feedback build (next build to Aaron)
### Finished skewer detaches from the grill and flies up
- The completed skewer lifts off the grill as one object (not a separate small skewer popping in)
- The same skewer tilts, shrinks and flies into the customer's bubble
- Replace the current `ServeSkewer` pop in `GameController.cs`
### Order bubbles: stop the order skewers overlapping
- Smaller skewer in the order bubble (`SkewerStack`), or a narrower bubble with 5 customers
- Check 2-5 customers on 9:19.5, 16:9 and 3:4
### UI buttons: tone down the gloss once more
- New soft set (`UI_Soft_v2`) from `soften_ui.py` with a stronger setting; keep `UI_Soft` as backup
- Before/after image for Aaron
### Remote Config: Home screen and Daily Rewards off for the first test
- Pick the SDK with iKame (Firebase Remote Config is likely)
- `homeScreen` and the daily popup read the remote value, defaulting to off
- Game opens straight into the level, like Sort Streak
### Korean BBQ skewer art set
- 8-10 Figma images (tteok, eomuk, dakkochi, sotteok, bulgogi, galbi, garlic, green chili); say the credit cost first
- Cut with the `cut_cartoon.py` pipeline + shadows; new folder, nothing overwritten
- Use them in the early levels (L1-10)
### 5-level tier demo build
- `tierDemo` switch (no new branch): plays the Easy, Medium, Hard, Very Hard, Super Hard levels as "Level 1-5"
- Tier name on the start banner ("Level 3: Hard")
- Home + Daily Rewards off, opens straight into Level 1
- Build `Grill Sort - 5 Tier Demo.apk`, share on the Slack thread with a short note
### Send Aaron an update on Slack
- What changed from his notes, plus the 5-tier demo APK

### List: Level design system
### Order size 5 from L6 + full arrivals
- Tray capacity = order size per level (4 in L1-5, 5 later)
- Queued trays arrive full; slack is fixed per level
- Colours may repeat in the queue (never twice on screen)
- Check the solver speed with 5 x 6 trays
### Level plan table
- One row per level: role (tutorial / ramp / Hard / Super Hard / breather / teach), knobs from 3.2, target band
- Editor-only data read by the baker (nothing loaded at runtime)
### Difficulty score in the baker
- Trap rate, random-play bot win rate, hidden-info risk, par
- Pick the candidate inside the level's target band
- Report: difficulty chart per level in `Docs/LevelReport.md`
### Sawtooth pacing + Hard / Super Hard badges
- Hard at x5, Super Hard at x0, breather after each
- Start banner with the badge, more coins on win
### Hand-made teaching levels
- L2, L3 (queue), L6 (size 5), L8 (mystery), plus one per new mechanic
- Tip text + tutorial hand for each
### Level editor window (Unity)
- Edit a level's trays, orders and "?" by hand, Verify (solver), Play this level
- Save into the level bank
### Design levels 1-50 from recipes
- A recipe per level: role + one idea built from the patterns in 3.5b
- Build each in the level editor; solver check; edit until it hits its target
- 0 failures, report reviewed, autoplay L1-50 at par
### Endless levels past 50
- The same 10-level sawtooth with mechanics mixed by seed, generated on the device

### List: Mechanics
### Mystery piece (foil-wrapped "?")
- Board: hidden flag per piece, revealed when it becomes the free (bottom) piece
- Solver proves solvable with full knowledge; fairness check with hidden info (bot win rate)
- Art: foil wrap with "?" (1 Figma image); reveal animation (foil peels, sparkle, sound)
- Hint never reveals a hidden piece's food
### Locked tray
- Extra tray with a padlock and a counter; opens after N orders served
### Frozen piece
- Ice cube; can't be grabbed and pins the pieces above it; thaws when the tray next to it is served
### Win streak
- Count wins in a row; Restart / quitting warns "you'll lose your streak"
- Streak reward (coins or a free booster) at 3 / 5 / 10
### Chef score bar (like the IQ bar)
- Bar with 3 milestones that fills as orders are served; sets the stars / coins on the win screen

### List: Content and tuning
### Analytics events for tuning
- level_start / level_win / level_stuck / restart, moves, time, boosters used, "?" revealed
### Playtest pass on a real phone
- Play L1-20, note where it feels flat or unfair, adjust the plan table
### Difficulty tuning round 2
- Use the playtest + analytics to move the target bands

### List: Decisions (made by us, 2026-11-26)
### Orders are 4 pieces in L1-5, 5 pieces from L6
### Mystery piece = food wrapped in foil with a "?"
### Locked tray (L22) and Frozen piece (L32) are in
### Undo stays a free count + rewarded ad at 0 (kinder than Sort Streak's 400 coins, and earns ad views)
### Firebase Remote Config + Firebase Analytics
### Korean BBQ foods lead L1-10, then mix with the current foods
### The 5-tier demo keeps 2 empty helper trays from the start (2026-11-27)
- Aaron (2026-11-27): "The 2 empty grills show up a bit early compared to Sort Streak." Sort Streak starts with 1 white
  helper and brings a 2nd one through the queue (from ~L22).
- Tried on the demo: with full trays (no spare slot) the generator can't add room to make up for a missing helper.
  Easy fell from a 99.7% bot win rate to 2-35%; Hard / Very Hard had no winnable board in 50 tries. Aaron had just
  approved the demo difficulty, so the demo keeps 2 helpers.
- The late helper is built (Board, Solver and the game handle an empty helper tray waiting in the queue;
  `DemoLevels.LateHelper` / `WithLateHelper` put it mid-queue). Use it in the full level set with boards designed for
  1 helper from the start, and bring the 2nd one in late from ~L22 like Sort Streak.
