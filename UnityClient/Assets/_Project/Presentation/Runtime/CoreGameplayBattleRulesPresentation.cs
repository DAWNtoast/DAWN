using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string BattleRepeatCostRule = "同名牌每次成功打出后费用 +1，副本共享；下回合或切换阶段恢复基础费用。";

        private string BattleCardTooltipText(CoreBattleCard card, int handIndex)
        {
            int cardIndex = session.Battle.Hand[handIndex];
            int actual = session.GetBattleCardEnergyCost(cardIndex);
            int used = Mathf.Max(0, actual - card.Energy);
            string unavailable = session.CardUnavailableReason(handIndex);
            return (unavailable.Length > 0 ? "<color=#6B251C>" + FormatBattleCardDescription(unavailable) + "</color>\n\n" : string.Empty) +
                FormatBattleCardDescription("当前能量 " + actual + "＝基础 " + card.Energy + "＋本回合已用 " + used + " 次") +
                "\n" + FormatBattleCardDescription(BattleRepeatCostRule) + "\n\n" +
                FormatBattleCardDescription(session.BattleCardDescription(card)) + "\n\n" + CardKeywordHelp(card) +
                (session.Battle.Stage == NavalBattleStage.Boarding && card.Repair > 0 ?
                    "\n" + FormatBattleCardDescription("药品：基础治疗 +50%（向上取整）；只在增疗有效时耗 1，满血或基础可补满不耗。") : string.Empty);
        }

        private void RefreshBattleCardCost(Text badge, Text surcharge, int cardIndex, CoreBattleCard card)
        {
            int actual = session.GetBattleCardEnergyCost(cardIndex);
            int increase = Mathf.Max(0, actual - card.Energy);
            badge.text = actual.ToString();
            surcharge.text = "本回合同名已用 " + increase + " 次 · +" + increase + " 费";
            surcharge.transform.parent.gameObject.SetActive(increase > 0);
        }

        private string BattleCrewName(CoreBattleState battle, bool player)
        {
            string name = player ? session.SelectedShip.Name : battle.EnemyName;
            int ordinary = player ? battle.PlayerOrdinaryCrew : battle.EnemyOrdinaryCrew;
            int maximum = player ? battle.PlayerMaxBoardingHealth : battle.EnemyMaxBoardingHealth;
            bool attacking = player != battle.BoardingInitiatedByEnemy;
            string role = battle.Stage == NavalBattleStage.Boarding ? (attacking ? " · 登船队" : " · 守备队") : string.Empty;
            return name + role + "\n<size=" + GameReadability.FontSize(18) + ">普通水手 " + ordinary + " 人 · 接舷生命上限 " + maximum + "</size>";
        }

        private static string BattleActiveBuffText(CoreBattleState battle)
        {
            string status = battle.Stage == NavalBattleStage.Boarding
                ? "普通水手 " + battle.PlayerOrdinaryCrew + " 人 · 卡牌效果 " +
                    (CoreBattleCardEffects.BoardingScaleFor(battle.PlayerOrdinaryCrew) * 100m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                    "%\n船员生命 " + battle.PlayerBoardingHealth + "/" + battle.PlayerMaxBoardingHealth +
                    "\n当前船员格挡 " + battle.PlayerBlock
                : "甲板强化 +" + battle.PlayerDeckBuff + "\n装填加伤 +" + battle.PlayerLoadedShot + "\n烈焰炮弹 " + battle.PlayerFireRounds + " 发";
            return status + "\n牌堆 " + battle.DrawPile.Count + " · 弃牌 " + battle.DiscardPile.Count;
        }

        private static void ConfigureEnemyIntentLayout(RectTransform battleRoot, RectTransform hud, Text label, Image icon, bool ram)
        {
            if (battleRoot == null || hud == null || label == null || icon == null) return;
            CoreGameplayDemoController controller = battleRoot.GetComponentInParent<CoreGameplayDemoController>();
            bool boarding = controller != null && controller.Session?.Battle?.Stage == NavalBattleStage.Boarding;
            // Place intent beside the enemy defense band, away from the name
            // board. Naval warnings grow upward; their bottom stays clear of
            // ram/board actions and their left edge stays beyond the ship art.
            GameReadability.Body(label, 22);
            float width = boarding ? 230f : 210f;
            float height = boarding ? 104f : 150f + 60f * (GameReadability.Scale - 1f);
            hud.sizeDelta = new Vector2(width, height);
            hud.anchoredPosition = boarding ? new Vector2(735f, -186f) : new Vector2(780f, -82f + height * .5f);
            icon.rectTransform.sizeDelta = new Vector2(30f, 30f);
            icon.rectTransform.anchoredPosition = new Vector2(-width * .5f + 21f, height * .5f - 21f);
            Text glyph = icon.GetComponentInChildren<Text>();
            if (glyph != null) { GameReadability.Body(glyph, 18); glyph.rectTransform.sizeDelta = new Vector2(30f, 30f); }
            // The narrow side column stays clear of the ships and card fan.
            // Keep the complete warning available through wheel/scrollbar input.
            ReadableParagraph(label, hud, new Vector2(width - 12f, height - 44f), new Vector2(0f, -19f), 22, true);
        }

    }
}
