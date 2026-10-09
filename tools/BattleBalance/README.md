# Battle balance calibration

Run from the repository root without launching Unity Editor:

~~~powershell
./Tools/BattleBalance/Run.ps1 -Seeds 4 -Output D:/Game/UnityClient/Artifacts/BattleRules20261002/Calibration-v2
~~~

The script compiles current Core sources plus NavalBattleBalanceSimulator using the installed Unity Roslyn compiler and Mono runtime. It requires no network or package restore. EditMode assertions are run separately by the project's Unity test runner.

The matrix contains the actual tutorial boss, a real ordinary encounter from each region, all three route bosses, the ruin-vault boss and all five legendary warships. It uses actual generated nodes and invokes the normal StartBattle entry point only to skip navigation. It never rewrites battle health, hands, RNG or enemy profiles after the fight begins. Tutorial seeds share the authored fixed tutorial map and are repeated checks, not independent geography samples.

Builds:

- Starter: bare sloop, 14 ordinary sailors, no specialist or component cards, quality 1 bow/hull; upgrade 0, I or III.
- Patrol: 18 ordinary sailors and two specialists; upgrade I, II or III.
- Gunship: 26 ordinary sailors and two specialists; upgrade II or III.
- The latter builds carry Gray Gull navigator Evelyn and carpenter Barlow and five actual blue card sources: bow_gilded_mermaid, sail_guild_silk, cannon_chain_repeater, plating_gilded_bulwark and instrument_guild_engine. Their bow/hull quality is 2 and extra component stat bonuses are zero. This preserves the earlier benchmark; it is not a simulated purchasing journey.
- Special card levels restore historical voyage experience (0/120/360/840); they are not bought as base-card upgrades.
- Every fixture begins with 20 water, 20 food, 20 wood, 10 medicine, 25 powder and 40 cannonballs. Voyage attrition and procurement costs are outside this audit.

Both captains obey CanPlayCard and score the current GetBattleCardEnergyCost for the library ID, including per-turn repetition cost. Both current hand and enemy public intent may be read; the draw pile may not. The cannon captain attempts retreat on the structured EnemyWillRam warning. The boarding captain may take the initiative with a legal ram; it prefers a planned free connection when already possible. After enemy-forced boarding, both fight normally. The focused naval deck carries retreat instead of parallel movement so it can actually evade ram threats. No win-rate target is baked into the policy.

runs.csv records every result. FinalStage and BoardingEntry distinguish a true naval kill from a planned, player-ram or enemy-ram melee. summary.md separately reports pure-naval wins and all-sample boarding rate. Overall cannon-policy wins must never be called pure-naval win rate.

Metrics include actual card cost, zero-cost plays, enemy attack turns, ram counts, damage received, minimum hull, surviving hull/crew and ammunition consumed. Final hull may include normal carpenter victory repair; damage and minimum hull remain separate. Ammunition totals sum card costs rather than subtracting ending inventory, so loot cannot hide expenditure. The 64-action and 100-turn watchdogs are only program safety limits; timeout remains a failed sample and is never a game mechanic.

The EditMode regressions check fixture legality, separate ordinary/special crew counts, scenario coverage, deterministic decisions and bounded real outcomes. They do not assert a tuned target win percentage. Interpret four-seed calibration as a small deterministic strategy sample, then validate chosen settings through actual Unity play.

## Ship progression audit (2026-10-03)

`RunShipProgression.ps1 -Seeds 4` compiles an immutable Core snapshot into `UnityClient/Artifacts/ShipProgression20261003/Calibration-baseline`; it never launches Unity or edits Assets. Twelve real ship definitions, upgrades 0/I/II/III paid through the real upgrade API, five fixed deck/policy combinations, three route bosses + ruin boss + five legends produce 8,640 battles. Every bare ship uses full ordinary crew and 74.1 cargo volume.

`-Seeds 32 -Mode Validation -Output <directory>` limits the matrix to sloop/courier/gunship, II/III, default boarding/focused cannon/sustain boarding and five legends. The independent validation seed prefix avoids just repeating baseline seeds.

`-Candidate StarterOnly`, `CrewScale`, or `StarterAndCrewScale` applies clearly labelled experimental changes ONLY to the output CoreSnapshot. StarterOnly sets sloop 55/2/6/13/80/10. CrewScale scales player boarding damage/block/healing by clamp(ordinaryCrew/20,0.3,1.6), rounded AwayFromZero, and the captain evaluates the same effects. None is the unchanged product. The script retains original and compiled source hashes and tool source copies. Never interpret candidate outputs as current product behavior.

The report gives fixed-build win rates, healing, damage, guard absorbed, remaining hull/crew and upgrade costs. Each fight begins fully supplied; these are independent fights, not voyage earnings or a no-resupply gauntlet. Bounded greedy strategies do not prove a failed fight impossible for a skilled player. See `Artifacts/ShipProgression20261003/analysis.md` for findings and limitations.

### Full fleet / crew-reference candidate report

`RunShipProgression.ps1 -Seeds 16 -Mode Crew -Candidate FleetCrewScale -CrewReference 28 -Output <directory>` applies `FleetCandidate.csv` only to output CoreSnapshot and runs all twelve ships with 2/3/6 real specialists, II/III base cards + matched 360/840 specialist XP, sustain/cannon policies and all nine bosses (20,736 battles). `CrewFocus` restricts to starter/merchant/patrol/gunship and five legends with 2/3 specialists. `CrewEarly` uses those four ships with three fresh specialists (0 XP). `Tutorial` checks the authored tutorial using 10/9/7 ordinary crew and correct Broken Anchor source identities at zero XP.

Crew reference remains a snapshot-only parameter. `BuildCrewReport.mjs` exports `generate(root)` to build the full reference28 overview HTML/text and cargo/ration CSV. Report source data checked across all 144 ship/crew/tier/policy combinations. Counts consume real berths and retain understaffing penalties; no component stat/card bonuses are added.

### Production ship-growth verification

Run `./RunProductionVerification.ps1 -Seeds 16` for the production-source check. It copies unmodified Core sources, verifies every copy SHA256, compiles Core separately from the simulator and NUnit fixture, then runs four ships with three fresh/mature specialists, base upgrades II/III, two policies, five legendary enemies, and the tutorial. The policy now reads `GetBattleCardEffects` directly; it never reapplies crew scaling. Output defaults to `Artifacts/ShipProgression20261003/Production-Verification`. Choose a fresh output path for reruns.

Historical candidate runs remain reproducible from their saved `CoreSnapshot` and `Build` files. `RunShipProgression.ps1` rejects candidate transformations once the production effect API exists, preventing accidental second scaling. Production verification is offline Core simulation, not a Unity playthrough.
