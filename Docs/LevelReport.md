# Grill Sort level report

Move rule: **AnyWithAbove**. Capacity 4. Every level was solved by the A* solver (admissible heuristic, so par is the true minimum unless marked `~`), and the solution was replayed on a fresh board to confirm it ends with every order served.

| Level | Foods | Empty | Skewers | Look-alike pairs | Target par | Par (min moves) | Also solvable with other rule | Bar |
|---|---|---|---|---|---|---|---|---|
| 1 | 2 | 1 | 3 | 0 | 3 | 3 | yes | ### |
| 2 | 3 | 2 | 5 | 0 | 6 | 6 | yes | ###### |
| 3 | 3 | 2 | 5 | 0 | 7 | 7 | yes | ####### |
| 4 | 4 | 2 | 6 | 0 | 8 | 8 | yes | ######## |
| 5 | 4 | 2 | 6 | 0 | 9 | 9 | yes | ######### |
| 6 | 4 | 2 | 6 | 0 | 10 | 10 | yes | ########## |
| 7 | 4 | 1 | 5 | 0 | 11 | 11 | yes | ########### |
| 8 | 5 | 2 | 7 | 0 | 12 | 12 | yes | ############ |
| 9 | 5 | 2 | 7 | 0 | 13 | 13 | yes | ############# |
| 10 | 5 | 1 | 6 | 0 | 14 | 14 | yes | ############## |
| 11 | 5 | 1 | 6 | 1 | 15 | 15 | no | ############### |
| 12 | 6 | 2 | 8 | 0 | 15 | 15 | yes | ############### |
| 13 | 6 | 2 | 8 | 1 | 16 | 16 | yes | ################ |
| 14 | 6 | 1 | 7 | 1 | 17 | 17 | no | ################# |
| 15 | 6 | 1 | 7 | 1 | 18 | 18 | yes | ################## |
| 16 | 6 | 1 | 7 | 2 | 19 | 19 | yes | ################### |
| 17 | 7 | 2 | 9 | 1 | 19 | 19 | yes | ################### |
| 18 | 7 | 2 | 9 | 1 | 20 | 20 | yes | #################### |
| 19 | 7 | 1 | 8 | 2 | 21 | 21 | yes | ##################### |
| 20 | 7 | 1 | 8 | 3 | 22 | 22 | yes | ###################### |
| 21 | 7 | 1 | 8 | 3 | 22 | 22 | yes | ###################### |
| 22 | 7 | 1 | 8 | 1 | 23 | 23 | no | ####################### |
| 23 | 8 | 1 | 9 | 2 | 23 | 23 | no | ####################### |
| 24 | 8 | 1 | 9 | 5 | 24 | 24 | no | ######################## |
| 25 | 8 | 1 | 9 | 4 | 24 | 24 | no | ######################## |
| 26 | 8 | 1 | 9 | 3 | 25 | 25 | yes | ######################### |
| 27 | 8 | 1 | 9 | 5 | 25 | 25 | yes | ######################### |
| 28 | 8 | 1 | 9 | 2 | 26 | 26 | yes | ########################## |
| 29 | 8 | 1 | 9 | 2 | 26 | 26 | no | ########################## |
| 30 | 8 | 1 | 9 | 3 | 27 | 27 | no | ########################### |

Baked 30 levels in 43.6 s, 0 failures. Levels past 30 are generated at runtime with the same seeded scheme (and still solver-checked).
`~` = par from the weighted fallback search (an upper bound, not proven minimal).
