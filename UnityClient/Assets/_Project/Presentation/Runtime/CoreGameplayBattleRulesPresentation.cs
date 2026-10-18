using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string BattleCardTooltipText(CoreBattleCard card, int handIndex)
        {
            string text = CardKeywordHelp(card);
            if (card != null && session?.Battle != null && handIndex >= 0 && handIndex < session.Battle.Hand.Count)
            {
                string reason = session.CardUnavailableReason(handIndex);
                if (!string.IsNullOrEmpty(reason)) text += "\n\n<color=#6B251C>当前不能使用</color>\n" + reason;
            }
            return text;
        }

        private string CardKeywordHelp(CoreBattleCard card)
        {
            if (card?.BoardingSpec != null) return BoardingCardKeywordHelp(card);
            if (card?.SchoolEffect != null) return SchoolCardKeywordHelp(card);
            if (card == null || session?.Battle == null || session.Battle.Stage != NavalBattleStage.Boarding ||
                !card.IsAllowedInStage(NavalBattleStage.Boarding) || card.Damage <= 0 && card.Repair <= 0)
                return string.Empty;

            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            var text = new StringBuilder();
            text.Append("卡牌效果不受人数影响。\n")
                .Append("当前士气 ").Append(session.Morale).Append(" · 接舷暴击率 ")
                .Append(session.BoardingCriticalChancePercent).Append("%（暴击伤害 ×2）。");
            if (card.Damage > 0)
                text.Append("\n\n<color=#6B251C>白刃</color>\n船员伤害 ").Append(effects.Damage).Append("。");
            if (card.Repair > 0)
            {
                text.Append("\n\n<color=#234921>治疗</color>\n有药完整 ").Append(effects.FullRepair).Append("。\n")
                    .Append("无药保底 ").Append(effects.FallbackRepair).Append("，不随强化增加。\n");
                if (effects.HasMedicineBoost)
                    text.Append("随船药品可提供完整治疗。\n");
                else text.Append("无随船药品。\n");
                text.Append("治疗上限 ").Append(effects.Repair);
                if (effects.IsRepairPreview)
                    text.Append("；治疗预览不消耗药品。");
                else
                {
                    int missing = Mathf.Max(0, session.Battle.PlayerMaxBoardingHealth - session.Battle.PlayerBoardingHealth);
                    text.Append("，当前缺血 ").Append(missing).Append("；\n本次实际恢复 ").Append(effects.ActualRepair)
                        .Append("（取治疗上限与缺血量的较小值）。\n")
                        .Append(effects.MedicineCost > 0 ? "药品 -1。" : "不耗药。");
                }
                text.Append("\n仅完整治疗超出保底时消耗 1 药品；满血或保底可补满不耗药。");
            }
            // This wider calculation tooltip keeps the full explanatory prose;
            // compact wording and card-face wrapping belong only to the card.
            return CoreBattleCardText.FormatEffects(text.ToString());
        }

        private void RefreshBattleCardCosts(RetainedBattleHandCard view, CoreBattleCard card)
        {
            int energy = view.Battle.Stage == NavalBattleStage.Boarding && card.BoardingSpec != null
                ? session.GetBoardingHandEnergyCost(view.Index) : session.GetBattleCardEnergyCost(view.CardId);
            RefreshCardCost(view.Energy, energy.ToString(), card.Energy.ToString());
            RefreshCardCost(view.Powder, session.GetBattleCardPowderCost(view.CardId).ToString(), card.PowderCost.ToString());
            RefreshCardCost(view.Cannonballs, card.CannonballCost.ToString(), card.CannonballCost.ToString());
            RefreshCardCost(view.SpecialCost, CardSpecialCostPresentation.CostText(card, view.Battle, session.Cargo.Wood),
                CardSpecialCostPresentation.CostText(card));
        }

        private string BattleCrewName(CoreBattleState battle, bool player)
        {
            string name = player ? session.SelectedShip.Name : battle.EnemyName;
            int ordinary = player ? battle.PlayerOrdinaryCrew : battle.EnemyOrdinaryCrew;
            int maximum = player ? battle.PlayerMaxBoardingHealth : battle.EnemyMaxBoardingHealth;
            bool attacking = player != battle.BoardingInitiatedByEnemy;
            string role = battle.Stage == NavalBattleStage.Boarding ? (attacking ? " · 登船队" : " · 守备队") : string.Empty;
            return name + role + "\n<size=15>普通水手 " + ordinary + " 人 · 接舷生命上限 " + maximum + "</size>";
        }

        private string BattleActiveBuffText(CoreBattleState battle)
        {
            string status = battle.Stage == NavalBattleStage.Boarding
                ? "普通水手 " + battle.PlayerOrdinaryCrew + " 人" +
                    "\n船员生命 " + battle.PlayerBoardingHealth + "/" + battle.PlayerMaxBoardingHealth +
                    "\n当前船员格挡 " + battle.PlayerBlock
                : "甲板强化 +" + battle.PlayerDeckBuff +
                    "\n装填加伤 +" + session.PlayerLoadedShotDamageBonus +
                    "\n烈焰炮弹 " + battle.PlayerFireRounds + " 发";
            return status + "\n牌堆 " + battle.DrawPile.Count + " · 弃牌 " + battle.DiscardPile.Count;
        }

        private static void ConfigureEnemyIntentLayout(RectTransform battleRoot, RectTransform hud, Text label, Image icon, bool ram)
        {
            if (battleRoot == null || hud == null || label == null || icon == null) return;
            // The body spans the full panel width; the small glyph occupies
            // only the title line so longer ram plans keep a readable minimum.
            label.fontSize = BattleDecisionFontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = BattleDecisionFontSize;
            hud.sizeDelta = new Vector2(BattleDecisionPanelWidth, BattleDecisionInfoHeight);
            icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
            icon.rectTransform.anchoredPosition = new Vector2(-96f, 43f);
            Text glyph = icon.GetComponentInChildren<Text>();
            if (glyph != null) { glyph.fontSize = 16; glyph.rectTransform.sizeDelta = new Vector2(24f, 24f); }
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.sizeDelta = new Vector2(212f, 104f);
            label.rectTransform.anchoredPosition = Vector2.zero;
        }

    }
}
