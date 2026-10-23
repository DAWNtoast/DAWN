using System;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private bool UsesFireFallback(CoreBattleCard card) => card != null && card.SchoolId == "fire" &&
            card.PowderCost > Cargo.Powder && card.SchoolEffect != null &&
            (card.SchoolEffect.FallbackDamage > 0 || card.SchoolEffect.FallbackBlock > 0);

        public CoreBattleCard GetSupplyAdjustedBattleCard(int cardIndex) => GetSupplyAdjustedBattleCard(GetBattleCard(cardIndex));

        internal CoreBattleCard GetSupplyAdjustedBattleCard(CoreBattleCard card)
        {
            if (card == null || Phase != CoreDemoPhase.Battle || Battle == null) return card;
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card.Id)?.Mechanic == CoreNavalExpansionMechanic.TideAnchor && Cargo.Wood < 1)
                return new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy, 0, 2, 0, 0, 0,
                    "无木板：获得 2 普通格挡，不产生潮势，不获得定锚。", card.AllowedStages, card.Action,
                    card.RequiredCrew, card.ArtId, card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId);
            if (BActive)
            {
                card = CoreBoardingCardCatalog.ForBoarding(card);
                if (card.BoardingSpec != null) return card;
            }
            int damage = card.Damage, block = card.Block;
            bool fireFallback = UsesFireFallback(card), tideFallback = card.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard && Cargo.Wood < card.SchoolEffect.WoodCost;
            if (fireFallback)
            {
                damage = card.SchoolEffect.FallbackDamage;
                block = card.SchoolEffect.FallbackBlock;
            }
            else if (tideFallback)
                block = Math.Min(2, block);
            if (!fireFallback && !tideFallback) return card;
            int movement = tideFallback ? card.SchoolEffect.Movement : 0;
            CoreSchoolCardEffect fallbackEffect = movement == 0 ? null : new CoreSchoolCardEffect(
                CoreSchoolMechanic.None, movement: movement, upgrades: CoreSchoolUpgradeFields.None);
            string fallbackText = (fireFallback ? "火药不足，仅基础效果：" : "木板不足，仅基础效果：") +
                (movement > 0 ? "靠近 {movement} 米并转为平行；" : movement < 0 ? "远离 {movement} 米；" : string.Empty) +
                (damage > 0 ? "伤害 {damage}；" : string.Empty) +
                (block > 0 ? "格挡 +{block}；" : string.Empty) +
                (card.Draw > 0 ? "抽 {draw} 张；" : string.Empty) +
                (fireFallback ? "不触发火焰附加效果。" : "不积累潮势。");
            return new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy, damage, block,
                card.Repair, card.Draw, fireFallback ? 0 : card.PowderCost, fallbackText, card.AllowedStages,
                card.Action, card.RequiredCrew, card.ArtId, card.CannonballCost, card.RequiredComponent,
                card.UpgradeLevel, card.SchoolId, fallbackEffect);
        }

        public int GetBattleCardPowderCost(int cardIndex)
        {
            CoreBattleCard card = GetBattleCard(cardIndex);
            if (BActive && card?.BoardingSpec != null)
                return BState.FreePowderUses > 0 ? 0 : BRequiredPowder(card);
            return card == null || UsesFireFallback(card) ? 0 : card.PowderCost;
        }

        public int GetBattleCardWoodCost(int cardIndex)
        {
            CoreBattleCard card = GetBattleCard(cardIndex);
            if (card == null) return 0;
            CoreNavalExpansionCardSpec expansion = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            if (expansion != null) return Cargo.Wood >= expansion.WoodCost ? expansion.WoodCost : 0;
            NavalBattleStage stage = Battle == null ? NavalBattleStage.Naval : Battle.Stage;
            return GetBattleCardEffects(card, stage).WoodCost + TideWoodCost(card);
        }

        private int TideWoodCost(CoreBattleCard card) => card?.SchoolEffect?.Mechanic == CoreSchoolMechanic.TideWard &&
            Cargo.Wood >= card.SchoolEffect.WoodCost ? card.SchoolEffect.WoodCost : 0;

        private string BattleCardUseIdentity(CoreBattleCard card) => BActive && card?.BoardingSpec != null
            ? BIdentity(card) : card?.Id ?? string.Empty;

        public int GetBattleCardEnergyRecovery(int cardIndex)
        {
            CoreBattleCard card = GetBattleCard(cardIndex);
            return CoreBattleUpgradeBalance.EnergyRecovery(card);
        }

        private static bool IsThunderAttack(CoreBattleCard card) => card != null && card.SchoolId == "thunder" && card.Damage > 0;

        private void PayMana(int amount)
        {
            int temporary = Math.Min(amount, Battle.Schools.TemporaryMana);
            Battle.Schools.TemporaryMana -= temporary;
            Battle.Schools.Mana -= amount - temporary;
        }

        // Expire the previous enemy turn's reserve before the enemy can build a new one.
        private void EndSchoolPlayerWindow()
        {
            Battle.Schools.TidePower = 0;
            Battle.Schools.TemporaryMana = 0;
        }

        private bool ApplySchoolCurseTick()
        {
            CoreSchoolBattleState s = Battle.Schools;
            if (Battle.Stage == NavalBattleStage.Preparation || s.CurseMarks <= 0) return false;
            int quoted = SchoolSum(s.CurseMarks, s.CurseNextTickBonus);
            quoted = SchoolSum(quoted, NavalFieldCurseBonus(false, s.CurseMarks));
            s.CurseNextTickBonus = 0;
            int damage = DamageSchoolEnemy(quoted);
            Battle.LastAction += " 附咒回合末结算：" + s.CurseMarks + " 层，伤害 " + damage + "。";
            s.CurseMarks -= 1;
            s.CurseTurns = s.CurseMarks;
            s.CurseDamagePerMark = 0;
            return ResolveBattleOutcome();
        }

        private void RecordPaidTideGuard(int absorbed)
        {
            CoreSchoolBattleState s = Battle.Schools;
            int before = SchoolSum(Battle.PlayerBlock, absorbed);
            int paidBefore = Math.Min(before, s.TidePaidBlock);
            int ordinaryBefore = before - paidBefore;
            int paidAbsorbed = Math.Min(paidBefore, Math.Max(0, absorbed - ordinaryBefore));
            s.TidePaidBlock = Math.Max(0, s.TidePaidBlock - paidAbsorbed);
            s.TidePower = Math.Min(s.TideLimit, SchoolSum(s.TidePower, paidAbsorbed));
        }
    }
}
