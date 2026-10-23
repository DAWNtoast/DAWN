using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private System.Collections.Generic.IReadOnlyList<CoreBattleCardKeyword> BattleCardKeywords(CoreBattleCard card)
        {
            return CoreBattleCardKeywordText.Build(card, BattleCardEffectTextForCurrentStage(card));
        }

        private string BattleCardTooltipText(CoreBattleCard card, int handIndex)
        {
            var text = new StringBuilder();
            foreach (CoreBattleCardKeyword entry in BattleCardKeywords(card))
            {
                if (text.Length > 0) text.Append("\n\n");
                text.Append(entry.Keyword).Append("：").Append(entry.Meaning);
            }
            return text.ToString();
        }

        private void RefreshBattleCardCosts(RetainedBattleHandCard view, CoreBattleCard card)
        {
            int energy = view.Battle.Stage == NavalBattleStage.Boarding && card.BoardingSpec != null
                ? session.GetBoardingHandEnergyCost(view.Index) : session.GetBattleCardEnergyCost(view.CardId);
            RefreshCardCost(view.Energy, energy.ToString(), card.Energy.ToString());
            RefreshCardCost(view.Powder, session.GetBattleCardPowderCost(view.CardId).ToString(), card.PowderCost.ToString());
            RefreshCardCost(view.Cannonballs, card.CannonballCost.ToString(), card.CannonballCost.ToString());
            RefreshCardCost(view.SpecialCost, CardSpecialCostPresentation.CostText(card, view.Battle, session.Cargo.Wood, session.Cargo.Medicine),
                CardSpecialCostPresentation.CostText(card));
            RefreshCardSupplyCost(view.Button.transform, card, view.Battle);
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
