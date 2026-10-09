# Early voyage combat audit

Run from `D:/Game` without launching Unity:

```powershell
& 'D:/Game/tools/EarlyBattleAudit/Run.ps1' -Seeds 32 -Output 'D:/Game/UnityClient/Artifacts/EarlyBattleAudit20260909'
```

This separate audit leaves the existing third-chart matrix and fixed captain unchanged.
It compiles current pure Core code plus `NavalBattleBalanceSimulator` and invokes that
simulator's existing private `BestCard` method. No copied or retuned decision scoring is used.

The 22 combinations cover:

- Chart I: the default unupgraded sloop, no hired specialist or rare component cards,
  against the authored coral ambush, random ambush and route boss, using both tactics.
- Chart II: merchant and patrol ships, each at card tier 0 and tier 1, with the explicitly
  assumed recruited Evelyn and Barlow but no rare component cards, against the authored
  customs ship and random ambush, using both tactics.

Every battle starts with the actual new-session 8 powder and 10 cannonballs. Hull and
all other cargo also remain at the normal initial values. The audit skips travel to
the encounter, invokes the normal combat entry point and then changes neither hands,
enemy hull, RNG nor combat resources. The first-chart empty specialist list matches a
fresh front-end save's recruitment flags; it does not inherit the older standalone
Core session's six default profession flags.

Each result is an independent single battle, not a whole voyage. A successful boss
battle on ten cannonballs does not imply that earlier fights and travel are free.
The generated report states fixture assumptions and includes mean/max ammunition
costs over all outcomes, plus win rate, winning turns and surviving health.

The 2026-09-09 sample ran 704 battles (32 seeds per combination): all won, no watchdog.
The first route boss's cannon tactic averaged 8.4 balls and reached the full starting
10-ball budget in some seeds. That is a provisioning constraint for consecutive
cannon fights, not evidence requiring a single-battle stat change. No balance table
was modified by this audit.
