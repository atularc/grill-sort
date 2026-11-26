# 5-tier demo levels (designed 2026-11-26)

Built by `Grill Sort > Design Demo Levels (5 tiers)` (Editor/DemoLevels.cs). Each tier is a recipe we chose (Sort Streak model); every kept board is proven winnable by the solver; "bot win" = how often a simple bot that plays sensible random moves wins (lower = harder).

| Demo level | Tier | Tray size | Trays on screen | Orders | Foods | Wrapped | Lock | Par (min moves) | Bot win |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Easy | 4 | 5 | 8 | corn, tomato, sausage, mushroom, pepper | 0 | none | 18 | 100% |
| 2 | Medium | 5 | 5 | 9 | corn, tomato, sausage, mushroom, shrimp, onion | 9 | none | 28~ | 73% |
| 3 | Hard (Hard label, x2 coins) | 5 | 6 | 10 | corn, tomato, sausage, beef, mushroom, shrimp, onion | 17 | spare tray opens on mushroom | 27~ | 10% |
| 4 | Very Hard (Hard label, x2 coins) | 5 | 6 | 11 | corn, pineapple, sausage, beef, mushroom, shrimp, tomato | 18 | customer tray opens on tomato | 34~ | 1% |
| 5 | Super Hard (Hard label, x2 coins) | 6 | 6 | 10 | corn, pineapple, sausage, beef, pepper, zucchini, tomato | 16 | customer tray opens on sausage | 34~ | 4% |

`~` = a solution found by the fallback search (not proven shortest).

## Boards (top of each tray first; `*` = wrapped; `[H]` = spare tray; `lock:x` = opens when x is served)

### Demo level 1: Easy, par 18
On screen:
- [H]: (empty)
- sausage order: sausage, sausage, mushroom, corn
- mushroom order: mushroom, sausage, pepper, sausage
- pepper order: pepper, pepper, mushroom, corn
- [H]: (empty)
Queue (arrives in this order):
- 1. corn order: corn, mushroom, corn, tomato
- 2. tomato order: tomato, corn, corn, corn
- 3. pepper order: pepper, tomato, tomato, pepper
- 4. sausage order: sausage, pepper, pepper, pepper
- 5. corn order: corn, sausage, sausage, sausage

### Demo level 2: Medium, par 28
On screen:
- [H]: (empty)
- sausage order: sausage, sausage*, shrimp, shrimp, corn
- shrimp order: shrimp, shrimp*, sausage, sausage, sausage
- tomato order: tomato, tomato*, mushroom, corn, shrimp
- [H]: (empty)
Queue (arrives in this order):
- 1. mushroom order: mushroom, tomato, tomato*, corn, tomato
- 2. corn order: corn, onion*, mushroom, mushroom, mushroom
- 3. onion order: onion*, corn, tomato, corn, corn
- 4. tomato order: tomato, tomato*, onion, mushroom, onion
- 5. mushroom order: mushroom, mushroom*, corn, tomato, tomato
- 6. corn order: corn, corn, mushroom, mushroom*, onion

### Demo level 3: Hard (Hard), par 27
On screen:
- [H] lock:mushroom: (empty)
- tomato order: tomato*, tomato*, shrimp, shrimp*, mushroom
- mushroom order: mushroom, mushroom*, tomato, tomato, tomato
- shrimp order: shrimp, shrimp, mushroom, beef, mushroom
- beef order: beef, beef, sausage*, shrimp, sausage
- [H]: (empty)
Queue (arrives in this order):
- 1. sausage order: sausage, sausage*, shrimp*, onion, onion
- 2. onion order: onion*, corn, corn*, sausage, corn
- 3. corn order: corn*, corn, shrimp, shrimp*, onion
- 4. shrimp order: shrimp, beef, beef*, beef*, mushroom
- 5. mushroom order: mushroom, mushroom*, beef*, beef, shrimp
- 6. beef order: beef, beef*, onion*, mushroom, mushroom

### Demo level 4: Very Hard (Hard), par 34
On screen:
- [H]: (empty)
- mushroom order: mushroom, mushroom, sausage, corn, beef
- beef order: beef, beef*, mushroom, mushroom, mushroom
- corn order: corn, beef, shrimp*, tomato*, beef
- shrimp order lock:tomato: shrimp, shrimp, corn, corn*, corn
- [H]: (empty)
Queue (arrives in this order):
- 1. tomato order: tomato*, tomato*, pineapple, shrimp, shrimp
- 2. pineapple order: pineapple*, pineapple, tomato, tomato*, sausage
- 3. sausage order: sausage, sausage*, pineapple, mushroom*, pineapple
- 4. mushroom order: mushroom*, mushroom, beef, sausage*, beef
- 5. beef order: beef*, beef*, mushroom, shrimp, shrimp
- 6. shrimp order: shrimp, pineapple*, pineapple, pineapple*, beef
- 7. pineapple order: pineapple*, pineapple, shrimp*, shrimp, mushroom

### Demo level 5: Super Hard (Hard), par 34
On screen:
- [H]: (empty)
- sausage order: sausage*, sausage, beef, beef*, pepper, beef
- beef order: beef, beef, sausage, pepper, sausage, sausage
- pepper order: pepper, sausage*, pineapple, beef, pineapple, zucchini
- pineapple order lock:sausage: pineapple, pineapple*, pepper, pepper, corn, pepper
- [H]: (empty)
Queue (arrives in this order):
- 1. corn order: corn, corn*, tomato, tomato*, sausage, tomato
- 2. tomato order: tomato*, tomato, corn*, zucchini, zucchini, corn
- 3. zucchini order: zucchini*, beef, beef, beef*, tomato, corn
- 4. beef order: beef, beef*, zucchini, zucchini*, pineapple, pineapple
- 5. pineapple order: pineapple, sausage, sausage*, sausage, sausage*, beef
- 6. sausage order: sausage*, pineapple, pineapple*, pineapple, pineapple, pineapple

