using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Activated naval doctrines and successful-use budgets, independent of card copies and ranks.</summary>
    public sealed class CoreSpecialCardBattleState
    {
        private readonly HashSet<string> active = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> played = new HashSet<string>(StringComparer.Ordinal);
        public bool IsActive(string school) => school != null && active.Contains(school);
        public bool HasPlayed(string school) => school != null && played.Contains(school);
        internal void Activate(string school) => active.Add(school);
        internal bool Record(string school) => played.Add(school);
        internal void BeginTurn() => played.Clear();
        internal void Clear() { active.Clear(); played.Clear(); }
    }

    public sealed partial class CoreBattleState
    {
        public CoreSpecialCardBattleState SpecialCards { get; } = new CoreSpecialCardBattleState();
    }

    public static class CoreSpecialCardRules
    {
        public static bool IsDoctrine(CoreBattleCard card) => DoctrineSchool(card?.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None).Length > 0;
        public static string DoctrineSchool(CoreSchoolMechanic mechanic)
        {
            switch (mechanic)
            {
                case CoreSchoolMechanic.SpecialFireDoctrine: return "fire";
                case CoreSchoolMechanic.SpecialArcaneDoctrine: return "arcane";
                case CoreSchoolMechanic.SpecialCurseDoctrine: return "curse";
                case CoreSchoolMechanic.SpecialTideDoctrine: return "tide";
                case CoreSchoolMechanic.SpecialThunderDoctrine: return "thunder";
                case CoreSchoolMechanic.SpecialBloodDoctrine: return "blood";
                default: return string.Empty;
            }
        }

        public static bool WaivesBloodCost(CoreBattleCard card, CoreBattleState battle) =>
            card?.SchoolId == "blood" && !IsDoctrine(card) && battle?.Stage == NavalBattleStage.Naval &&
            battle.SpecialCards.IsActive("blood") && !battle.SpecialCards.HasPlayed("blood");

        /// <summary>The same read-only life cost is used for validation, payment and the live resource badge.</summary>
        public static int HealthCost(CoreBattleCard card, CoreBattleState battle = null)
        {
            if (card == null || WaivesBloodCost(card, battle)) return 0;
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            if (spec != null) return spec.HullCost;
            CoreSchoolCardEffect effect = card.SchoolEffect;
            return effect != null && (effect.Mechanic == CoreSchoolMechanic.BloodRisk || effect.Mechanic == CoreSchoolMechanic.BloodWard)
                ? effect.Threshold : 0;
        }

        public static string CostDescription(string text, CoreBattleCard card, CoreBattleState battle)
        {
            if (!WaivesBloodCost(card, battle)) return text;
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            return spec != null && spec.HullCost > 0
                ? text.Replace("支付 " + spec.HullCost + " 点船体", "支付 0 点船体（血契特权）")
                    .Replace("支付 " + spec.HullCost + " 船体", "支付 0 船体（血契特权）") : text;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        private void ApplySpecialCardDoctrine(CoreBattleCard card)
        {
            if (!SchoolBattleActive(NavalBattleStage.Naval) || card == null) return;
            string doctrine = CoreSpecialCardRules.DoctrineSchool(card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None);
            if (doctrine.Length > 0)
            {
                Battle.SpecialCards.Activate(doctrine);
                return;
            }
            string school = card.SchoolId;
            if (string.IsNullOrEmpty(school) || school == "thunder" && !IsThunderAttack(card)) return;
            // Record successful cards even before activation: installing/replaying a doctrine cannot reset a turn's budget.
            if (!Battle.SpecialCards.Record(school) || !Battle.SpecialCards.IsActive(school)) return;
            CoreSchoolBattleState state = Battle.Schools;
            switch (school)
            {
                case "fire": ApplySchoolBurn(3, 2); break;
                case "arcane": state.Mana = Math.Min(state.ManaLimit, SchoolSum(state.Mana, 1)); break;
                case "curse":
                    if (SchoolEnemyHealth <= 0) break;
                    state.CurseMarks = Math.Max(state.CurseMarks, Math.Min(6, SchoolSum(state.CurseMarks, 1)));
                    state.CurseDamagePerMark = Math.Max(state.CurseDamagePerMark, 2);
                    state.CurseTurns = Math.Max(state.CurseTurns, 2);
                    break;
                case "tide": state.TidePower = Math.Min(state.TideLimit, SchoolSum(state.TidePower, 3)); break;
                case "thunder": state.ThunderChain = Math.Min(state.ThunderLimit, SchoolSum(state.ThunderChain, 1)); break;
                // Blood's benefit was already quoted and paid before this successful-use record.
            }
        }
    }
}
