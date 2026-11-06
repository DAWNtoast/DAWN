using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        public const int NavalBossRushMaxCells = 5;
        public const int NavalBossRushIntervalTurns = 5;
        private const int NavalBossAllFieldImmunityMask = (1 << 6) - 1;

        private bool NavalBossFieldCycleActive => NavalFieldsActive && Battle.IsBoss &&
            Battle.Profile?.TrainingTarget == null &&
            (!IsGuidedBossBattle || GuidedBossLesson == GuidedBossLesson.FinalBattle);

        // Naval rounds advance even while pursuing. The authored action chain is independent.
        public bool EnemyNavalBossRushThisTurn => NavalBossFieldCycleActive &&
            Battle.PhaseTurn >= NavalBossRushIntervalTurns && Battle.PhaseTurn % NavalBossRushIntervalTurns == 0;

        public int EnemyNavalBossFieldImmunityMaskThisTurn
        {
            get
            {
                if (!NavalBossFieldCycleActive) return 0;
                if (EnemyNavalBossRushThisTurn) return NavalBossAllFieldImmunityMask;
                if (Battle.PhaseTurn < 3 || (Battle.PhaseTurn - 3) % NavalBossRushIntervalTurns != 0 ||
                    !TryFindNavalBossSchool(Battle.Profile, out string school)) return 0;
                switch (school)
                {
                    case "fire": return 1 << (int)CoreNavalFieldKind.Fire;
                    case "arcane": return 1 << (int)CoreNavalFieldKind.Arcane;
                    case "curse": return 1 << (int)CoreNavalFieldKind.Curse;
                    case "tide": case "ordinary": return 1 << (int)CoreNavalFieldKind.Tide;
                    case "thunder": return 1 << (int)CoreNavalFieldKind.Thunder;
                    case "blood": return 1 << (int)CoreNavalFieldKind.Blood;
                    default: return 0;
                }
            }
        }

        public string EnemyNavalBossFieldImmunityDescription
        {
            get
            {
                int mask = EnemyNavalBossFieldImmunityMaskThisTurn;
                if (mask == 0) return string.Empty;
                string field = "所有体系场地";
                if (mask != NavalBossAllFieldImmunityMask)
                    foreach (CoreNavalFieldKind kind in Enum.GetValues(typeof(CoreNavalFieldKind)))
                        if ((mask & (1 << (int)kind)) != 0) field = CoreComponentFieldCards.FieldName(kind) + "场地";
                return "本次敌方回合仅免疫" + field + "的负面效果，保留场地正面收益；已有燃烧、咒印照常结算。";
            }
        }

        private string NavalBossRushDescription => "极速行进：本次敌方回合免疫所有体系场地的负面效果，最多移动 " +
            NavalBossRushMaxCells + " 格，依场地收益与危险选择落点，不与目标重叠；保留场地正面收益，已有燃烧、咒印照常结算。每 " +
            NavalBossRushIntervalTurns + " 个海战回合发动一次。";

        private void ApplyEnemyNavalBossFieldImmunity(CoreNavalFieldShipState ship)
        {
            ship.BossFieldImmunityMask = EnemyNavalBossFieldImmunityMaskThisTurn;
            ship.AllFieldEffectsImmune = ship.BossFieldImmunityMask == NavalBossAllFieldImmunityMask;
        }

        private static void ClearEnemyNavalBossFieldImmunity(CoreNavalFieldShipState ship)
        {
            ship.BossFieldImmunityMask = 0;
            ship.AllFieldEffectsImmune = false;
        }
    }
}
