using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreSchoolUnlockDefinition
    {
        public CoreSchoolUnlockDefinition(string id, string name, int level)
        { Id = id; Name = name; RequiredLevel = level; }
        public string Id { get; }
        public string Name { get; }
        public int RequiredLevel { get; }
        public int BasicCardCount
        {
            get
            {
                int count = 0;
                // Ship-stage cards retain their historic definitions; the new
                // boarding collection replaces four legacy choices with fifteen.
                foreach (CoreBattleCard card in CoreSchoolCardCatalog.All)
                    if (card.SchoolId == Id && card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 &&
                        card.AllowedStages != NavalBattleStages.Boarding && card.AllowedStages != NavalBattleStages.None) count++;
                foreach (CoreBattleCard card in CoreBoardingCardCatalog.All)
                    if (card.SchoolId == Id) count++;
                return count;
            }
        }
    }

    /// <summary>School names and historic grant levels retained for profile migration.</summary>
    public static class CoreSchoolUnlockCatalog
    {
        private static readonly IReadOnlyList<CoreSchoolUnlockDefinition> all = Array.AsReadOnly(new[]
        {
            new CoreSchoolUnlockDefinition("fire", "火焰", 2),
            new CoreSchoolUnlockDefinition("arcane", "秘术", 3),
            new CoreSchoolUnlockDefinition("curse", "诅咒", 4),
            new CoreSchoolUnlockDefinition("tide", "潮汐", 5),
            new CoreSchoolUnlockDefinition("thunder", "雷霆", 6),
            new CoreSchoolUnlockDefinition("blood", "血契", 7),
        });
        public static IReadOnlyList<CoreSchoolUnlockDefinition> All => all;
        public static CoreSchoolUnlockDefinition Find(string id)
        {
            foreach (CoreSchoolUnlockDefinition school in all)
                if (string.Equals(school.Id, id, StringComparison.Ordinal)) return school;
            return null;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        public int GetSchoolUnlockLevel(string schoolId) => CoreSchoolUnlockCatalog.Find(schoolId)?.RequiredLevel ?? 1;

        public bool IsBattleCardUnlocked(CoreBattleCard card) => GetBattleCardUnlockReason(card).Length == 0;

        public string GetBattleCardUnlockReason(CoreBattleCard card)
        {
            if (card == null) return "卡牌不存在";
            if (card.AllowedStages == NavalBattleStages.None) return "卡牌已停用";
            // Sources retain independent recruitment/equipment progression.
            if (card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0 || string.IsNullOrEmpty(card.SchoolId))
                return string.Empty;
            if (HasPackCard(card)) return string.Empty;
            CoreSchoolUnlockDefinition school = CoreSchoolUnlockCatalog.Find(card.SchoolId);
            if (school == null) return "卡牌体系配置无效";
            return "尚未拥有此卡牌，可通过打开" + school.Name + "卡包或普通卡包获得。";
        }

        public bool CanSelectRoute(int routeIndex, out string reason)
        {
            reason = routeIndex < 0 || routeIndex >= routes.Count ? "航道不存在。" :
                Phase != CoreDemoPhase.Port ? "只能在港口选择航道。" :
                PirateLevel < routes[routeIndex].UnlockLevel ? "海盗等级达到 " + routes[routeIndex].UnlockLevel + " 后开放" + routes[routeIndex].Name + "。" : string.Empty;
            return reason.Length == 0;
        }

        public bool CanStartOrdinaryVoyage(out string reason)
        {
            if (!CanStartWithDepartureCrew(out reason)) return false;
            if (SelectedTreasureMap != null)
            { reason = string.Empty; return true; }
            if (!CanSelectDestinationPort(SelectedDestinationPort, out reason)) return false;
            if (GuidedTutorialLeg > 0) return true;
            return CanSelectRoute(SelectedRouteIndex, out reason);
        }
    }
}

