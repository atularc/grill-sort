# 5-tier demo levels (designed 2026-11-26)

Built by `Grill Sort > Design Demo Levels (5 tiers)` (Editor/DemoLevels.cs). Each tier is a recipe we chose (Sort Streak model); every kept board is proven winnable by the solver; "bot win" = how often a simple bot that plays sensible random moves wins (lower = harder).

| Demo level | Tier | Tray size | Trays on screen | Orders | Foods | Wrapped | Lock | Par (min moves) | Bot win |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Easy | 4 | 5 | 8 | corn, tomato, sausage, mushroom, pepper | 0 | none | 18 | 78% |
| 2 | Medium | 5 | 5 | 9 | corn, tomato, sausage, mushroom, shrimp, onion | 9 | none | 28~ | 41% |
| 3 | Hard (Hard label, x2 coins) | 5 | 6 | 10 | corn, tomato, sausage, beef, mushroom, shrimp, onion | 17 | none | 31~ | 14% |
| 4 | Very Hard (Hard label, x2 coins) | 5 | 6 | 11 | corn, pineapple, sausage, beef, mushroom, shrimp, tomato | 18 | spare tray, opens on beef | 37~ | 3% |
| 5 | Super Hard (Hard label, x2 coins) | 6 | 6 | 10 | corn, pineapple, sausage, beef, pepper, zucchini, tomato | 17 | none | 40~ | 2% |

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

### Demo level 3: Hard (Hard), par 31
On screen:
- [H]: (empty)
- mushroom order: mushroom, corn, onion, onion, onion
- onion order: onion*, shrimp, shrimp, mushroom, mushroom
- shrimp order: shrimp, shrimp, onion, mushroom, sausage
- sausage order: sausage*, sausage*, tomato*, shrimp*, tomato
- [H]: (empty)
Queue (arrives in this order):
- 1. tomato order: tomato, tomato*, corn, corn*, sausage
- 2. corn order: corn*, corn, mushroom*, tomato, sausage
- 3. beef order: beef, beef, shrimp*, corn*, shrimp
- 4. shrimp order: shrimp, shrimp, beef*, beef*, beef
- 5. mushroom order: mushroom, corn*, corn, corn*, shrimp
- 6. corn order: corn*, mushroom, mushroom, mushroom*, mushroom

### Demo level 4: Very Hard (Hard), par 37
On screen:
- [H]: (empty)
- shrimp order: shrimp, pineapple*, corn*, pineapple, pineapple
- pineapple order: pineapple*, pineapple, shrimp, shrimp, shrimp
- tomato order: tomato, tomato, sausage, shrimp, sausage
- sausage order lock:beef: sausage, tomato, tomato*, mushroom, tomato
- [H]: (empty)
Queue (arrives in this order):
- 1. beef order: beef, beef, mushroom*, sausage*, mushroom
- 2. mushroom order: mushroom, corn*, beef*, beef, beef
- 3. corn order: corn*, corn, tomato, tomato*, mushroom
- 4. tomato order: tomato*, corn*, beef, corn, beef
- 5. beef order: beef, beef*, tomato, sausage*, tomato
- 6. sausage order: sausage*, sausage, corn*, beef, corn
- 7. corn order: corn*, corn, sausage*, sausage, sausage

### Demo level 5: Super Hard (Hard), par 40
On screen:
- [H]: (empty)
- beef order: beef, tomato, tomato, tomato*, pepper, tomato
- tomato order: tomato, beef, beef, sausage*, beef*, sausage
- sausage order: sausage, beef, tomato, pepper*, beef, zucchini
- zucchini order: zucchini, sausage, pineapple, sausage, pineapple*, sausage
- [H]: (empty)
Queue (arrives in this order):
- 1. pineapple order: pineapple, pineapple, zucchini*, zucchini, zucchini*, pepper
- 2. pepper order: pepper, pepper*, zucchini*, corn, corn, corn
- 3. corn order: corn*, sausage, sausage*, pepper, sausage, sausage
- 4. sausage order: sausage, sausage, tomato*, tomato, corn*, tomato
- 5. tomato order: tomato, beef, beef, beef*, beef*, beef
- 6. beef order: beef, corn*, tomato, tomato*, pineapple, pineapple

