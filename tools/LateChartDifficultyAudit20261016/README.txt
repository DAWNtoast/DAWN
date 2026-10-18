Late-chart combat difficulty audit (2026-10-16)

This directory is an offline validation tool. It snapshots the current production
Core C# sources and CrewNarrativeCatalog, compiles against Unity's local Mono
runtime, and writes all output under Artifacts/LateChartDifficultyAudit20261016.
It does not modify production balance, saves, global card definitions or enemy
profiles. No external services are required.

Scope: independent full-health boarding combat, starting from a real encounter,
real preparation completion and successful TryBoard. Navigation and naval damage
are bypassed, curse is zero, medicine and powder are finite. These are not full
voyage completion probabilities, treasure completion probabilities, or chained
battle probabilities. Treasure legendary ships use the same production encounter
factory and boarding scripts as those injected into the three current treasure
maps. The old Moon Vault encounter is a separate optional challenge on chart 3.

REFERENCE FIXTURES (not system-deck recommendations)
B0: ship 4, 14 ordinary + Tam/Isha, 76 HP. Original 12-card starter deck. Only
    cutlass/thrust/guard +1, special cards +0. Medicine/powder 4 each.
B1: ship 2, 20 ordinary + Tam/Isha, 100 HP. Original 12-card starter deck.
    All eligible base cards +2 (real caps), specials 120 XP (+1), supplies 6.
B2: ship 7, 26 ordinary + Tam/Isha, 124 HP. Original 12-card starter deck.
    All eligible base cards +2 (real caps), specials 360 XP (+2), supplies 8.
B3: ship 7, 26 ordinary + Tam/Isha, 124 HP. Original 12-card starter deck.
    All eligible base cards +3 (real caps), specials 840 XP (+3), supplies 8.
B4: ship 7, 26 ordinary + Tam/Isha, 124 HP. Legal 10-card active deck: two each
    boarding_hook, cutlass_raid, crew_bandage, energy_order, tactical_draw.
    All eligible base cards +3 (real caps), specials 840 XP (+3), supplies 8.
All builds use level 5, no component cards and no equipment multipliers. The two
actual specialist cards are fire_05 and blood_07; sources are explicit. Every
run exports the actual normalized cards, copies, levels, current effects and XP.

The basic reference policies use visible hand and current intent, without future
draws, future RNG, rollback/clone gameplay, or enemy future turns. These policies
do not fairly value arbitrary resource/conditional school decks.
Screen50 runs B0-B3 used policy v1: burn did not contribute to danger urgency.
B4Screen50 and all Validation200 runs use policy v2: current hostile burn counts
for danger urgency, while block value still excludes damage it cannot prevent.
This policy fix does not change the game combat rules or enemy values.

Reference screening seeds: 0-49, balanced policy. Independent validation seeds:
200-399, each of balanced/conservative/aggressive. Seed prefix:
LATE-CHART-AUDIT-20261016-. B2/B3/B4Validation200 each contain 5400 battles.
Examples:
  ./tools/LateChartDifficultyAudit20261016/RunSimulation.ps1 -RunTag Example \
    -Scenario B2 -Seeds 200 -StartSeed 200 -PolicySet all

Outputs include source-hashes.csv, copied CoreSnapshot and harness, baseline.txt,
run-settings.txt, encounters.csv, starter-deck.csv, runs.csv, summary.csv, and
first-three-seed action/turn traces for every enemy/policy combination.
The obsolete candidate CSV argument is rejected by the audit executable; all
reported encounters retain production stats and scripts.

System-deck analysis requested after the reference run is maintained separately
from the basic-deck results. Do not describe B0-B4 as completed system builds.

SYSTEM-DECK FIXTURES
Use -School fire|arcane|curse|tide|thunder|blood with growth build B1/B2/B3.
The exact ten base cards and two crew IDs are read from the JSON block in
Artifacts/LateChartDifficultyAudit20261016/school-spec.txt. Each run freezes that
document, a deterministic TSV, SystemPolicy.cs and the harness into Build, and
exports their SHA256 values in harness-hashes.csv.
School B1: patrol frigate, 20 ordinary + two specialist crew, 100 HP, base target
  +2 and source XP120 (+1).
School B2: heavy gunship, 26 ordinary + two specialists, 124 HP, base target +2,
  source XP360 (+2).
School B3: same heavy gunship and crew, base target +3, source XP840 (+3).
Actual upgrades respect every real maximum; fixed utility cards do not become
stronger. Each school uses its two real source crew identities and capabilities,
eight medicine, and 16 powder for fire only (zero powder for other schools).
Pirate level is max(5, the school's true unlock level): thunder 7, blood 10.
The actual starter-deck.csv and source key/identity/upgrade assertions verify
what entered battle. No component card sources or equipment boosts are present.

SystemPolicy is separately reviewed to score public resource generation and
spending, real card quotes, visible incoming damage, current hand combinations,
burn/self-burn, delayed damage, reflection and life costs. It never clones or
rolls back game state, inspects draw-pile order, uses the seed, or reads future
RNG/enemy turns. All outcomes still use production PlayCard/EndBattleTurn.
The first-three-seed action traces include card keys, the visible hand, energy,
and before/after mana, marks, tide, chain, self-burn, enemy burn and delayed damage.
Full run rows include finite medicine/powder consumption and exhaustion.

SystemsScreen30: all six schools x B1/B2/B3 x nine targets x 30 seeds, balanced
policy, seeds0-29. This is exploratory screening, not the final estimate. The
matrix stops if the harness, school specification or policy changes during a
run. Do not average school win rates to obscure a losing school.

One deck-only revision followed screening: curse and tide v2 use ordinary energy
support and more focused resource consumers instead of spending their scarce
school resources on too many competing utilities. Exact old decks remain in
school-spec-v1.txt and original run snapshots. The SystemPolicy weights/rules did
not change. SystemsV2Screen30 repeats just those two schools at seeds0-29 to
verify legal deck construction and actual resource behavior. It is still only
screening and must not be presented as an independent success-rate estimate.

SystemsValidation200 is the completed frozen six-school, three-build,
nine-encounter main validation matrix: independent seeds200-399, balanced policy,
32400 battles and zero timeouts. Every run is a separate full-health fight. The matrix also rejects any
change to any production Core source or CrewNarrativeCatalog between scenarios.
Do not compare conditional school strengths as if deck construction, crew-source
cards and this fixed policy were identical; report each tested deck separately.

AuditMatrix.ps1 verifies all 18 groups have nine rows and share identical
production, harness, policy and specification hashes. SystemsValidation200-audit.txt
records the successful check. SystemsValidation200-verified.csv preserves the
validated results and SystemsValidation200-confidence.csv adds per-row Wilson
95% sampling intervals. These quantify only randomness under this fixed deck and
policy, not variation among human players or whole-voyage success.

Examples:
  ./tools/LateChartDifficultyAudit20261016/RunSystemMatrix.ps1 -RunPrefix Check \
    -Seeds 30 -Schools fire,arcane -Builds B1,B2,B3
  ./tools/LateChartDifficultyAudit20261016/RunSimulation.ps1 -RunTag OneSchool \
    -Scenario B2 -School thunder -Seeds 200 -StartSeed 600 -PolicySet all
