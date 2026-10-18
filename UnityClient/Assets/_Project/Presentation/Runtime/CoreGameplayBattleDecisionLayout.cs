using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float BattleDecisionColumnX = 735f;
        private const float BattleDecisionPanelWidth = 230f;
        private const float BattleDecisionInfoHeight = 120f;
        private const float BattleDecisionButtonHeight = 44f;
        private const float BattleDecisionGap = 8f;
        private const int BattleDecisionFontSize = 18;

        // Apply after construction and retained refresh, never during an animation.
        // Keep the stable direct-child paths used by card callbacks and defense FX.
        private void ApplyBattleDecisionLayout(RectTransform root, CoreBattleState battle)
        {
            if (root == null || battle == null) return;
            RefreshBattleSchoolHud(root, battle);
            // The hand scales within its own region; fixed decision columns
            // must not jump as drawing or playing cards changes that scale.
            const float cardTop = -136f;
            float infoTopY = cardTop - BattleDecisionInfoHeight * .5f;
            float infoBottomY = infoTopY - BattleDecisionInfoHeight - BattleDecisionGap;
            float buttonY = infoBottomY - BattleDecisionInfoHeight * .5f - BattleDecisionGap - BattleDecisionButtonHeight * .5f;
            RectTransform resources = root.Find("Player Resource HUD") as RectTransform;
            if (resources != null)
            {
                SetBattleDecisionPanel(resources, true, infoTopY, BattleDecisionInfoHeight);
                SetBattleDecisionText(resources, "Player Resource Title", new Vector2(212f, 26f),
                    new Vector2(0f, 36f), BattleDecisionFontSize);
                SetBattleDecisionText(resources, "Player Ammunition", new Vector2(212f, 40f),
                    new Vector2(0f, -35f), BattleDecisionFontSize);
                foreach (string glyph in new[] { "能", "药" })
                {
                    Text value = resources.Find("Resource " + glyph + " Value")?.GetComponent<Text>();
                    if (value != null)
                    {
                        value.fontSize = BattleDecisionFontSize;
                        value.rectTransform.sizeDelta = new Vector2(glyph == "能" ? 56f : 52f, 38f);
                        value.rectTransform.anchoredPosition = new Vector2(glyph == "能" ? -24f : 84f, -4f);
                    }
                }
                // Resource symbols are set when the stage view is built. A
                // retained refresh keeps this stage and only changes numbers.
            }

            RectTransform buffs = root.Find("Battle Buff Panel") as RectTransform;
            if (buffs != null)
            {
                SetBattleDecisionPanel(buffs, true, infoBottomY, BattleDecisionInfoHeight);
                SetBattleDecisionText(buffs, "Battle Active Buffs", new Vector2(212f, 104f), Vector2.zero, BattleDecisionFontSize);
            }
            RectTransform retreat = root.Find("Retreat Battle") as RectTransform ?? root.Find("Emergency Battle Return") as RectTransform;
            if (retreat != null) SetBattleDecisionPanel(retreat, true, buttonY, BattleDecisionButtonHeight);
            foreach (RectTransform panel in new[] { resources, buffs, retreat })
                SetBattleDecisionTypography(panel);

            RectTransform intent = root.Find("Enemy Intent HUD") as RectTransform;
            if (intent != null) SetBattleDecisionPanel(intent, false, infoTopY, BattleDecisionInfoHeight);
            RectTransform enemyResources = root.Find("Enemy Resource HUD") as RectTransform;
            if (enemyResources != null)
            {
                SetBattleDecisionPanel(enemyResources, false, infoBottomY, BattleDecisionInfoHeight);
                SetBattleDecisionText(enemyResources, "Enemy Resource Title", new Vector2(212f, 26f), new Vector2(0f, 36f), BattleDecisionFontSize);
                SetBattleDecisionText(enemyResources, "Enemy Burning", new Vector2(212f, 26f), new Vector2(0f, -36f), BattleDecisionFontSize);
                SetBattleDecisionTypography(enemyResources);
            }
            RectTransform endTurn = root.Find("End Battle Turn") as RectTransform;
            if (endTurn != null)
            {
                SetBattleDecisionPanel(endTurn, false, buttonY, BattleDecisionButtonHeight);
                SetBattleDecisionTypography(endTurn);
            }
            // Stack the naval actions above the right-hand panels. The inset
            // leaves the distant enemy's hull bar clear on their left.
            SetBattleSpecialAction(root, "Battle Ram", "Battle Ram Condition", 6f);
            SetBattleSpecialAction(root, "Battle Board", "Battle Board Condition", -68f);
            RectTransform curse = root.Find("Battle Curse Panel") as RectTransform;
            if (curse != null) curse.gameObject.SetActive(session.CurseCount > 0);
            RefreshBoardingEnemyHud(root, battle);
            if (battle.Stage != NavalBattleStage.Boarding) ApplyNavalTacticalHudLayout(root, battle);
            RefreshBattleStatusHud(root, battle);
            RefreshCompactBattleHud(root, battle);
        }

        private static void SetBattleDecisionPanel(RectTransform panel, bool player, float y, float height)
        {
            panel.sizeDelta = new Vector2(BattleDecisionPanelWidth, height);
            panel.anchoredPosition = new Vector2(player ? -BattleDecisionColumnX : BattleDecisionColumnX, y);
            Text buttonLabel = panel.Find("Label")?.GetComponent<Text>();
            if (buttonLabel != null)
            {
                buttonLabel.rectTransform.sizeDelta = panel.sizeDelta - new Vector2(24f, 10f);
                buttonLabel.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private static void SetBattleDecisionTypography(RectTransform panel)
        {
            if (panel == null) return;
            foreach (Text label in panel.GetComponentsInChildren<Text>(true))
            {
                label.fontSize = BattleDecisionFontSize;
                label.resizeTextForBestFit = false;
            }
        }

        private static void SetBattleSpecialAction(RectTransform root, string buttonName, string hintName, float y)
        {
            RectTransform button = root.Find(buttonName) as RectTransform;
            if (button == null) return;
            button.sizeDelta = new Vector2(176f, 44f);
            button.anchoredPosition = new Vector2(762f, y);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 16;
                label.resizeTextForBestFit = false;
                label.rectTransform.sizeDelta = button.sizeDelta - new Vector2(24f, 10f);
                label.rectTransform.anchoredPosition = Vector2.zero;
            }
            SetBattleDecisionText(root, hintName, new Vector2(176f, 28f), new Vector2(762f, y - 37f), 12);
        }

        private static void SetBattleDecisionText(Transform parent, string name, Vector2 size, Vector2 position, int fontSize)
        {
            Text label = parent.Find(name)?.GetComponent<Text>();
            if (label == null) return;
            label.rectTransform.sizeDelta = size;
            label.rectTransform.anchoredPosition = position;
            label.fontSize = fontSize;
            label.color = Ink;
            label.raycastTarget = false;
        }
    }
}
