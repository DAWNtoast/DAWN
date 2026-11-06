using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// An authored naval boss action. This is a decision value, never persistent
    /// combat state; previews must not pay its resources or apply its effects.
    /// Damage coefficients are evaluated against the resources available after
    /// the next enemy start and movement, by the shared preview/execution caller.
    /// </summary>
    public enum CoreNavalBossFieldAction { None, Place, Extend, Expand }

    public sealed class CoreNavalBossMove
    {
        public string Name, SchoolId, NextName, StageName, NextStageName;
        public int CycleStep, CycleLength;
        public bool AllowRam;
        public int Burn, Marks, PiercingDamage;
        public int ManaGain, ManaLimitGain, ManaDamagePerPoint;
        public int TidalGuard, TideDamagePercent;
        public int ChainGain, ChainFiveBonus, ChainTenBonus;
        public int BloodCost, BloodDamageBonus, LifeStealPercent;
        public CoreNavalFieldKind? FieldKind;
        public CoreNavalBossFieldAction FieldAction;
        public int FieldTurns, FieldExtendTurns, FieldExpansionCells;
        public bool SpendMana, SpendTide, SpendChain;
        public bool PlaceThunderField => FieldAction == CoreNavalBossFieldAction.Place && FieldKind == CoreNavalFieldKind.Thunder;
        public int ThunderFieldTurns => PlaceThunderField ? FieldTurns : 0;
    }

    public sealed partial class CoreBattleState
    {
        // Scalar battle-only state is copied by the existing preview memberwise
        // copy. Only damage actually absorbed by the marked portion
        // of EnemyBlock increases EnemyTidePower; there is no resource cap.
        public int EnemyTidePower;
        public int EnemyTidalGuard;
        public int EnemyNavalBossActionsCompleted;
    }

    public sealed partial class CoreVoyageSession
    {
        // Percentages below are authored independently of player upgrades. There
        // is no PhaseTurn/4 damage ramp; arcane growth is explicit mana capacity.
        private bool TryBuildNavalBossPlan(float distance, out EnemyBattlePlan plan)
        {
            plan = default(EnemyBattlePlan);
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval ||
                IsGuidedBossBattle || !TryFindNavalBossSchool(Battle.Profile, out string school)) return false;

            CoreBattleProfile profile = Battle.Profile;
            int length = school == "arcane" || school == "thunder" ? 7 : school == "tide" || school == "ordinary" ? 5 : 6;
            int step = Math.Max(0, Battle.EnemyNavalBossActionsCompleted) % length;
            int damage = Math.Max(0, profile.CannonDamage);
            int guard = Math.Max(0, profile.Guard);
            var move = new CoreNavalBossMove { SchoolId = school, CycleStep = step + 1 };
            plan.BossMove = move;
            plan.Action = EnemyBattleAction.Guard;

            switch (school)
            {
                case "fire":
                    // The first chart precedes the fire school's protection cards.
                    // Keep placement, extension and expansion, with two fewer turns of exposure.
                    bool earlyFire = ReferenceEquals(profile, CoreBattleBalance.FirstRouteBoss);
                    NameNavalBossMove(move, step,
                        new[] { "火场封航", "续焰添薪", "焰沿扩散", "引火压舷", "爆燃齐射", "炉舱回整" },
                        new[] { "布场", "续场", "扩域", "施压", "爆发", "回整" });
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Fire, CoreNavalBossFieldAction.Place); if (earlyFire) move.FieldTurns = 2; plan.Attack = NavalBossPercent(damage, 20); plan.Guard = guard; }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Fire, CoreNavalBossFieldAction.Extend, earlyFire ? 1 : 2); plan.Attack = NavalBossPercent(damage, 35); move.Burn = Math.Max(1, NavalBossPercent(damage, 5)); }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Fire, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 45); move.Burn = Math.Max(1, NavalBossPercent(damage, 8)); }
                    else if (step == 3) { plan.Attack = NavalBossPercent(damage, 65); move.Burn = Math.Max(1, NavalBossPercent(damage, 10)); }
                    else if (step == 4) { plan.Attack = NavalBossPercent(damage, 110); move.Burn = Math.Max(1, NavalBossPercent(damage, 15)); }
                    else plan.Guard = NavalBossPercent(guard, 50);
                    break;
                case "arcane":
                    NameNavalBossMove(move, step,
                        new[] { "折射蓄能", "星阵续航", "镜域展开", "星轨试射", "聚束校准", "聚束释放", "星帆回整" },
                        new[] { "布场", "续场", "扩域", "施压", "蓄力", "爆发", "回整" });
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Arcane, CoreNavalBossFieldAction.Place); plan.Guard = SchoolSum(guard, NavalBossPercent(damage, 30)); move.ManaLimitGain = profile.BossKind == CoreBossKind.Legendary ? 2 : 1; }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Arcane, CoreNavalBossFieldAction.Extend, 3); plan.Guard = NavalBossPercent(guard, 70); move.ManaGain = 1; }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Arcane, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 35); }
                    else if (step == 3) { plan.Attack = NavalBossPercent(damage, 50); move.ManaGain = 1; }
                    else if (step == 4) { BossField(move, CoreNavalFieldKind.Arcane, CoreNavalBossFieldAction.Extend); plan.Attack = NavalBossPercent(damage, 45); }
                    else if (step == 5) { plan.Attack = NavalBossPercent(damage, 60); move.SpendMana = true; move.ManaDamagePerPoint = Math.Max(1, NavalBossPercent(damage, 20)); }
                    else { plan.Attack = NavalBossPercent(damage, 20); plan.Guard = NavalBossPercent(guard, 50); }
                    break;
                case "curse":
                    NameNavalBossMove(move, step,
                        new[] { "禁域缚魂", "幽界漫延", "咒域续契", "续印咒击", "蚀甲穿魂", "静默换防" },
                        new[] { "布场", "扩域", "续场", "施压", "爆发", "回整" });
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Curse, CoreNavalBossFieldAction.Place); plan.Attack = NavalBossPercent(damage, 15); plan.Guard = guard; move.Marks = Math.Max(1, NavalBossPercent(damage, 4)); }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Curse, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 35); move.Marks = Math.Max(1, NavalBossPercent(damage, 4)); }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Curse, CoreNavalBossFieldAction.Extend, 3); plan.Guard = guard; }
                    else if (step == 3) { plan.Attack = NavalBossPercent(damage, 45); move.Marks = Math.Max(1, NavalBossPercent(damage, 6)); }
                    else if (step == 4) { plan.Attack = NavalBossPercent(damage, 90); move.PiercingDamage = NavalBossPercent(damage, 25); }
                    else { plan.Attack = NavalBossPercent(damage, 20); plan.Guard = NavalBossPercent(guard, 50); }
                    break;
                case "tide":
                    NameNavalBossMove(move, step,
                        new[] { "引潮壁垒", "长潮固守", "水脉扩张", "退潮反击", "撤盾回整" },
                        new[] { "布场", "续场", "扩域", "爆发", "回整" });
                    bool earlyTide = damage <= 15;
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Place); plan.Guard = earlyTide ? SchoolSum(damage, guard) : SchoolSum(NavalBossPercent(damage, 150), NavalBossPercent(guard, 150)); move.TidalGuard = plan.Guard; }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Extend); plan.Attack = NavalBossPercent(damage, 35); plan.Guard = guard; move.TidalGuard = guard; }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 45); plan.Guard = guard; move.TidalGuard = guard; }
                    else if (step == 3) { plan.Attack = NavalBossPercent(damage, earlyTide ? 70 : 80); move.SpendTide = true; move.TideDamagePercent = earlyTide ? 25 : 35; }
                    else plan.Attack = NavalBossPercent(damage, 25);
                    break;
                case "thunder":
                    NameNavalBossMove(move, step,
                        new[] { "雷栅布阵", "电网蔓延", "雷场续能", "电弧试射", "终雷蓄势", "连锁终雷", "余电回整" },
                        new[] { "布场", "扩域", "续场", "施压", "蓄力", "爆发", "回整" });
                    move.ChainGain = NavalBossThunderChainGain(step);
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Thunder, CoreNavalBossFieldAction.Place); plan.Attack = NavalBossPercent(damage, 20); plan.Guard = SchoolSum(guard, NavalBossPercent(damage, 25)); }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Thunder, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 35); }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Thunder, CoreNavalBossFieldAction.Extend); plan.Attack = NavalBossPercent(damage, 35); }
                    else if (step == 3) plan.Attack = NavalBossPercent(damage, 55);
                    else if (step == 4) plan.Guard = guard;
                    else if (step == 5) { plan.Attack = NavalBossPercent(damage, 75); move.ChainFiveBonus = NavalBossPercent(damage, 40); move.ChainTenBonus = NavalBossPercent(damage, 80); move.SpendChain = true; }
                    else plan.Attack = NavalBossPercent(damage, 25);
                    break;
                case "blood":
                    NameNavalBossMove(move, step,
                        new[] { "血池仪式", "献血重炮", "血契续池", "血潮漫溢", "偿血炮击", "赤帆回整" },
                        new[] { "布场", "献血", "续场", "扩域", "爆发", "回整" });
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Blood, CoreNavalBossFieldAction.Place); plan.Guard = SchoolSum(guard, NavalBossPercent(damage, 30)); }
                    else if (step == 1) { plan.Attack = NavalBossPercent(damage, 45); move.BloodCost = NavalBossBloodActionCost(profile, step); move.BloodDamageBonus = NavalBossPercent(damage, 35); }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Blood, CoreNavalBossFieldAction.Extend, 3); plan.Guard = guard; }
                    else if (step == 3) { BossField(move, CoreNavalFieldKind.Blood, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 45); move.LifeStealPercent = 25; }
                    else if (step == 4) { plan.Attack = NavalBossPercent(damage, 110); move.LifeStealPercent = 50; }
                    else { plan.Attack = NavalBossPercent(damage, 25); plan.Guard = NavalBossPercent(guard, 50); }
                    break;
                default:
                    NameNavalBossMove(move, step,
                        new[] { "宝库潮锁", "锁海扩界", "潮锁续阵", "守藏齐射", "炮舱换防" },
                        new[] { "布场", "扩域", "续场", "爆发", "回整" });
                    if (step == 0) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Place); plan.Guard = SchoolSum(NavalBossPercent(guard, 200), NavalBossPercent(damage, 40)); }
                    else if (step == 1) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Expand); plan.Attack = NavalBossPercent(damage, 45); }
                    else if (step == 2) { BossField(move, CoreNavalFieldKind.Tide, CoreNavalBossFieldAction.Extend); plan.Attack = NavalBossPercent(damage, 40); plan.Guard = guard; }
                    else if (step == 3) plan.Attack = NavalBossPercent(damage, 120);
                    else plan.Attack = NavalBossPercent(damage, 30);
                    break;
            }

            if (plan.Attack > 0 || move.PiercingDamage > 0) plan.Action = EnemyBattleAction.Cannon;
            // Setup commits a visible field at the current firing position. Arcane
            // and blood keep their beneficial terrain while the target is in range.
            float requested = plan.Action == EnemyBattleAction.Cannon
                ? profile.Archetype == CoreBattleArchetype.Artillery ? 60f :
                    profile.Archetype == CoreBattleArchetype.Raider ? 150f : 100f
                : 90f;
            bool avoidField = NavalBossShouldAvoidOccupiedField();
            bool holdField = NavalBossShouldHoldOccupiedField(distance);
            plan.Move = avoidField ? Math.Max(NavalGridCellMeters, requested) :
                distance > 60f && !move.FieldKind.HasValue && !holdField ? NavalApproachMovement(distance, requested) : 0f;
            return true;
        }

        private static bool TryFindNavalBossSchool(CoreBattleProfile profile, out string school)
        {
            school = null;
            if (profile == null || profile.TrainingTarget != null) return false;
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All)
                if (ReferenceEquals(profile, boss.Profile)) { school = boss.SchoolId; return true; }
            foreach (CoreLegendaryWarship boss in CoreLegendaryWarships.All)
                if (ReferenceEquals(profile, boss.Profile)) { school = boss.SchoolId; return true; }
            if (!ReferenceEquals(profile, CoreBattleBalance.ThirdTreasureBoss)) return false;
            school = "ordinary";
            return true;
        }

        private static void NameNavalBossMove(CoreNavalBossMove move, int step, string[] names, string[] stages)
        {
            move.CycleLength = names.Length;
            move.Name = names[step];
            move.StageName = stages[step];
            move.NextName = names[(step + 1) % names.Length];
            move.NextStageName = stages[(step + 1) % stages.Length];
            move.AllowRam = step == names.Length - 1;
        }

        private static int NavalBossThunderChainGain(int step) => step == 3 || step == 4 ? 2 : step == 6 ? 0 : 1;

        private int NavalBossExpectedChainGain()
        {
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval || IsGuidedBossBattle ||
                !TryFindNavalBossSchool(Battle.Profile, out string school) || school != "thunder") return 0;
            return NavalBossThunderChainGain(Math.Max(0, Battle.EnemyNavalBossActionsCompleted) % 7);
        }

        private static int NavalBossBloodActionCost(CoreBattleProfile profile, int step) =>
            step == 1 ? Math.Max(1, NavalBossPercent(Math.Max(0, profile.CannonDamage), 12)) : 0;

        private int NavalBossExpectedBloodCost()
        {
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval || IsGuidedBossBattle ||
                !TryFindNavalBossSchool(Battle.Profile, out string school) || school != "blood") return 0;
            return NavalBossBloodActionCost(Battle.Profile, Math.Max(0, Battle.EnemyNavalBossActionsCompleted) % 6);
        }

        private static void BossField(CoreNavalBossMove move, CoreNavalFieldKind kind,
            CoreNavalBossFieldAction action, int extend = 2)
        {
            move.FieldKind = kind;
            move.FieldAction = action;
            move.FieldTurns = kind == CoreNavalFieldKind.Thunder ? 5 : 3;
            move.FieldExtendTurns = action == CoreNavalBossFieldAction.Extend ? extend : 0;
            move.FieldExpansionCells = action == CoreNavalBossFieldAction.Expand ? 8 : 0;
        }

        private static int NavalBossPercent(int value, int percent) =>
            (int)Math.Min(int.MaxValue, ((long)Math.Max(0, value) * Math.Max(0, percent) + 50L) / 100L);
    }
}
