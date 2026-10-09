using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public static class CoreBattleUpgradeBalance
    {
        public const int MaximumLevel = 3;
        public const int NumericalMultiplier = 2;
        public static int CostForNextLevel(int currentLevel) => currentLevel == 0 ? 120 : currentLevel == 1 ? 240 : currentLevel == 2 ? 480 : 0;
    }

    public sealed partial class CoreVoyageSession
    {
        private readonly Dictionary<string, int> cardUpgradeLevels = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> componentBattleTypes = new HashSet<string>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, int> CardUpgradeLevels => new ReadOnlyDictionary<string, int>(cardUpgradeLevels);

        public int GetCardUpgradeLevel(string id) => IsSpecialBattleCard(id) ? SpecialCardLevelForExperience(GetSpecialCardExperience(id)) :
            id != null && cardUpgradeLevels.TryGetValue(id, out int level) ? level : 0;

        public CoreBattleCard GetBattleCard(int index) => index < 0 || index >= cardLibrary.Count ? null :
            IsGuidedBossBattle ? GuidedBossCardDefinition(index) : GetBattleCardAtLevel(index, GetCardUpgradeLevel(cardLibrary[index].Id));

        // Definitions are immutable; only the owning session's chosen level
        // changes. A bounded cache avoids allocating two cards and formatting
        // descriptions on every combat/UI query without sharing player progress.
        private static class UpgradedCardDefinitions
        {
            // Nested initialization runs after the outer partial class's card
            // library is ready, independent of source-file initializer order.
            internal static readonly object Gate = new object();
            internal static readonly CoreBattleCard[,] Cards =
                new CoreBattleCard[cardLibrary.Count, CoreBattleUpgradeBalance.MaximumLevel];
        }

        public CoreBattleCard GetBattleCardAtLevel(int index, int level)
        {
            if (index < 0 || index >= cardLibrary.Count || cardLibrary[index].AllowedStages == NavalBattleStages.None) return null;
            CoreBattleCard original = cardLibrary[index];
            level = Math.Max(0, Math.Min(CoreBattleUpgradeBalance.MaximumLevel, level));
            if (level == 0) return original;
            lock (UpgradedCardDefinitions.Gate)
            {
                CoreBattleCard cached = UpgradedCardDefinitions.Cards[index, level - 1];
                if (cached != null) return cached;
                cached = BuildUpgradedCardDefinition(original, level);
                UpgradedCardDefinitions.Cards[index, level - 1] = cached;
                return cached;
            }
        }

        private static CoreBattleCard BuildUpgradedCardDefinition(CoreBattleCard original, int level)
        {
            int multiplier = CoreBattleUpgradeBalance.NumericalMultiplier;
            CoreBattleCard upgraded = new CoreBattleCard(original.Id, original.Name, original.Type,
                Math.Max(0, original.Energy - (level >= 2 ? 1 : 0)), original.Damage * multiplier,
                original.Block * multiplier, original.Repair * multiplier, original.Draw + (level >= 3 ? 1 : 0),
                original.PowderCost, string.Empty, original.AllowedStages, original.Action, original.RequiredCrew,
                original.ArtId, original.CannonballCost, original.RequiredComponent, level);
            return new CoreBattleCard(upgraded.Id, upgraded.Name, upgraded.Type, upgraded.Energy, upgraded.Damage,
                upgraded.Block, upgraded.Repair, upgraded.Draw, upgraded.PowderCost, DescribeUpgradedBattleCard(upgraded),
                upgraded.AllowedStages, upgraded.Action, upgraded.RequiredCrew, upgraded.ArtId, upgraded.CannonballCost,
                upgraded.RequiredComponent, level);
        }

        public int GetCardUpgradeCost(int index) => index < 0 || index >= cardLibrary.Count || cardLibrary[index].AllowedStages == NavalBattleStages.None || IsSpecialBattleCard(cardLibrary[index].Id) ? 0 :
            CoreBattleUpgradeBalance.CostForNextLevel(GetCardUpgradeLevel(cardLibrary[index].Id));

        public void ConfigureCardUpgrades(IReadOnlyDictionary<string, int> levels)
        {
            if (Phase != CoreDemoPhase.Port) return;
            Dictionary<string, int> copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (levels != null)
                foreach (CoreBattleCard card in cardLibrary)
                    if (card.AllowedStages != NavalBattleStages.None && levels.TryGetValue(card.Id, out int level) && level > 0)
                    {
                        int clamped = Math.Min(CoreBattleUpgradeBalance.MaximumLevel, level);
                        if (IsSpecialBattleCard(card.Id)) RestoreSpecialCardMinimumExperience(card.Id, SpecialCardExperienceForLevel(clamped));
                        else copy[card.Id] = clamped;
                    }
            cardUpgradeLevels.Clear();
            foreach (KeyValuePair<string, int> pair in copy) cardUpgradeLevels[pair.Key] = pair.Value;
            MigrateAcquiredSpecialCardExperience();
        }

        public string BattleCardUpgradeUnavailableReason(int index)
        {
            if (Phase != CoreDemoPhase.Port) return "请在港口强化卡牌";
            CoreBattleCard card = GetBattleCard(index);
            if (card == null) return "卡牌不存在";
            if (IsSpecialBattleCard(card.Id)) return "特殊卡牌仅随来源船员或部件出海航行获得经验，不能使用金币强化";
            if (card.UpgradeLevel >= CoreBattleUpgradeBalance.MaximumLevel) return "已完成三次强化";
            if (!HasBattleCrew(card.RequiredCrew)) return "先携带提供该卡牌的特殊船员";
            if (!HasBattleComponent(card.RequiredComponent)) return "先装备提供该卡牌的部件";
            if (ProfileGold < GetCardUpgradeCost(index)) return "船长金币不足";
            return string.Empty;
        }

        public bool TryUpgradeBattleCard(int index, out string reason)
        {
            reason = BattleCardUpgradeUnavailableReason(index);
            if (reason.Length > 0) return false;
            CoreBattleCard card = GetBattleCard(index);
            int nextLevel = card.UpgradeLevel + 1;
            if (!TrySpendProfileGold(GetCardUpgradeCost(index), "强化「" + card.Name + "」至 " + nextLevel + " 阶"))
            {
                reason = "无法支付本次强化费用";
                return false;
            }
            cardUpgradeLevels[card.Id] = nextLevel;
            reason = card.Name + " 已强化至 " + nextLevel + " 阶，同名卡牌同步生效。";
            return true;
        }

        public void ConfigureComponentBattleCards(IReadOnlyList<string> typeIds)
        {
            if (Phase != CoreDemoPhase.Port) return;
            componentBattleTypes.Clear();
            if (typeIds == null) return;
            foreach (string type in typeIds)
                if (type == "bow" || type == "sail" || type == "cannon" || type == "plating" || type == "instrument")
                    componentBattleTypes.Add(type);
        }

        private bool HasBattleComponent(string type) => string.IsNullOrEmpty(type) ||
            (explicitSpecialCardSources ? activeSpecialComponentIds.Contains(type) : componentBattleTypes.Contains(type));

        public IReadOnlyList<int> GetComponentBattleCards(NavalBattleStage stage)
        {
            List<int> result = new List<int>();
            for (int index = 0; index < cardLibrary.Count; index += 1)
            {
                CoreBattleCard card = cardLibrary[index];
                if (card.RequiredComponent.Length > 0 && card.IsAllowedInStage(stage) && HasBattleComponent(card.RequiredComponent)) result.Add(index);
            }
            return result.AsReadOnly();
        }

        public IReadOnlyList<int> GetBonusBattleCards(NavalBattleStage stage)
        {
            List<int> result = new List<int>(GetCrewBattleCards(stage));
            result.AddRange(GetComponentBattleCards(stage));
            return result.AsReadOnly();
        }

        private static string DescribeUpgradedBattleCard(CoreBattleCard card)
        {
            StringBuilder text = new StringBuilder();
            if (card.Action == NavalBattleCardAction.Cannon) text.Append("350 米内：");
            if (card.Action == NavalBattleCardAction.Sabotage) text.Append("150 米内：");
            if (card.Damage > 0) text.Append(card.AllowedStages == NavalBattleStages.Boarding ? "船员伤害 " : "船体伤害 ").Append(card.Damage).Append("；");
            if (card.Block > 0) text.Append("格挡 +").Append(card.Block).Append("；");
            if (card.Repair > 0) text.Append(card.AllowedStages == NavalBattleStages.Boarding ? "恢复船员 " : card.AllowedStages == NavalBattleStages.All ? "恢复当前阶段生命 " : "修复船体 ").Append(card.Repair).Append("；");
            if (card.MovementMeters > 0)
                text.Append(card.Action == NavalBattleCardAction.Retreat ? "远离 " : "靠近 ").Append(card.MovementMeters).Append(" 米，");
            switch (card.Action)
            {
                case NavalBattleCardAction.Polish: text.Append("本场防御牌额外格挡 +").Append(card.StrategyAmount).Append("，最高 +").Append(card.StrategyLimit).Append("；"); break;
                case NavalBattleCardAction.Load:
                case NavalBattleCardAction.PrecisionLoad: text.Append("下次炮击 +").Append(card.StrategyAmount).Append("，装填最高 +").Append(card.StrategyLimit).Append("；"); break;
                case NavalBattleCardAction.Approach: text.Append("进入冲撞航行；"); break;
                case NavalBattleCardAction.Retreat: text.Append("取消冲撞航行；"); break;
                case NavalBattleCardAction.Turn: text.Append("转向敌舰侧舷并进入冲撞航行；"); break;
                case NavalBattleCardAction.Circle: text.Append("舰艏对准敌舰侧舷，须推进后才能冲撞；"); break;
                case NavalBattleCardAction.Parallel:
                case NavalBattleCardAction.Navigator: text.Append("转为平行，60 米内可接舷；"); break;
                case NavalBattleCardAction.FireRounds: text.Append("烈焰强化 +").Append(card.StrategyAmount).Append(" 发，最高 ").Append(card.StrategyLimit).Append(" 发；命中后灼烧 ").Append(card.BurnDuration).Append(" 回合，每回合 ").Append(card.BurnDamage).Append(" 伤害；"); break;
            }
            if (card.Draw > 0) text.Append("抽 ").Append(card.Draw).Append(" 张当前阶段牌；");
            if (card.PowderCost > 0 || card.CannonballCost > 0)
            {
                text.Append("消耗 ");
                if (card.PowderCost > 0) text.Append(card.PowderCost).Append(" 火药");
                if (card.PowderCost > 0 && card.CannonballCost > 0) text.Append("、");
                if (card.CannonballCost > 0) text.Append(card.CannonballCost).Append(" 炮弹");
                text.Append("；");
            }
            return CoreBattleCardText.FormatEffects(text.ToString().TrimEnd('；') + "。");
        }
    }
}
