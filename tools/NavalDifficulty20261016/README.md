# Naval difficulty audit

Run `RunSimulation.ps1 -RunTag <unique> -Build starter|mixed|P|H|M -Seeds 30`.
`P_thunder`, `H_thunder`, `M_thunder`, and corresponding `_blood` names use the
same ship tiers with their actual school unlock level and two real crew sources.

The default `SourceRoot` reads the frozen pre-adjustment source under
`Artifacts/BattleBalanceBaseline20261016_PreNaval/Source`. To evaluate current
production, pass `-SourceRoot D:/Game`. Each run copies and hashes its own Core
source and harness; use a new tag for each experiment. Output is isolated under
`Artifacts/NavalDifficulty20261016/<tag>`.

Only sailing to an encounter is bypassed by invoking the existing StartBattle
entry. Preparation, shuffle, current hand, energy, real artillery range, armor,
enemy movement/ram, field effects, supplies and end-turn resolution all use
production Core. The captain plans with public legal movement APIs, commits
each real path and selects a legal heading. It never writes geometry, health,
enemy parameters, RNG or hands, never inspects future draws, and never requests
boarding or player ram. An enemy-forced transition to boarding is reported
separately and is not a naval victory. Enemy ram itself retains real resolution.

The fixed captain prefers cannon damage, useful shield/repair, energy/draw,
and movement into a three-to-four-cell firing band. It uses current public
intent only. Tide shields are valued only when paid and likely to absorb an
attack; tide damage quotes already include consumed resources. Thunder setup
and blood payment/healing receive explicit scores. These are reproducible
reasonable policies, not optimal human play or an estimate of a complete voyage.

`deck.csv` exports actual phase cards and caps; `fixture.txt` exports ship,
crew and finite supplies. `runs.csv` records each outcome, moves, cannon plays,
hull loss/repair and costs. `summary.csv` separates sinking, death, forced
boarding and watchdog outcomes. `actions.csv` retains the first three seeds
per enemy, including real coordinates, headings, action points, intent, cards
and remaining supplies. Quoted real cannon costs are counted before victory
loot can replenish inventory. Use source and harness hashes when comparing runs.

Starter uses the true twelve-card default deck and no special crew or upgrades.
Mixed keeps the default deck and upgrades only chain shot, barrage and evasive
maneuver by one rank (360 gold). Mature fixtures use ten legal school/support
cards per ship-combat phase with real upgrade caps; source crew remain in their
actual allowed stages and do not grant fictional naval cards or bonuses.

These are independent battles at departure hull and curse zero. Map travel,
previous fight attrition and boarding follow-up are outside this audit. A victory
rate here must not be described as full-map completion probability.
