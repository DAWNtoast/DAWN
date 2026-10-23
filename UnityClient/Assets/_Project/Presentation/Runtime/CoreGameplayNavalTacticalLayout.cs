using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void ApplyNavalTacticalHudLayout(RectTransform root, CoreBattleState battle)
        {
            if (root == null || battle == null || battle.Stage == NavalBattleStage.Boarding) return;

            foreach (bool player in new[] { true, false })
            {
                string side = player ? "Player" : "Enemy";
                Vector2 position = new Vector2(player ? -735f : 735f, 410f);
                SetNavalHudRect(root.Find(side + " Battle Name Board") as RectTransform,
                    new Vector2(340f, 70f), position);
                Text name = SetNavalHudText(root, side + " Battle Name", new Vector2(328f, 62f), position, 20);
                if (name != null)
                {
                    name.resizeTextForBestFit = true;
                    name.resizeTextMinSize = 16;
                    name.resizeTextMaxSize = 20;
                }
            }

            RectTransform resources = SetNavalHudPanel(root, "Player Resource HUD", true, -193f, 152f);
            SetNavalHudText(resources, "Player Resource Title", new Vector2(212f, 24f), new Vector2(0f, 58f), 17);
            SetNavalHudText(resources, "Player Ammunition", new Vector2(212f, 24f), new Vector2(0f, -10f), 16);
            ArrangeNavalResourceRow(resources, "能", -54f, 24f);
            ArrangeNavalResourceRow(resources, "药", 54f, 24f);

            RectTransform buffs = SetNavalHudPanel(root, "Battle Buff Panel", true, -336f, 116f);
            SetNavalHudText(buffs, "Battle Active Buffs", new Vector2(212f, 100f), Vector2.zero, 17);
            string retreatName = root.Find("Emergency Battle Return") != null ? "Emergency Battle Return" : "Retreat Battle";
            SetNavalHudPanel(root, retreatName, true, -420f, 40f, 16);

            RectTransform intent = SetNavalHudPanel(root, "Enemy Intent HUD", false, -218f, 106f);
            Text intentText = SetNavalHudText(intent, "Enemy Intent Text", new Vector2(208f, 90f), Vector2.zero, 17);
            if (intentText != null)
            {
                intentText.resizeTextForBestFit = true;
                intentText.resizeTextMinSize = 14;
                intentText.resizeTextMaxSize = 17;
            }
            RectTransform intentIcon = intent == null ? null : intent.Find("Enemy Intent Icon") as RectTransform;
            SetNavalHudRect(intentIcon, new Vector2(24f, 24f), new Vector2(-96f, 37f));
            SetNavalHudText(intentIcon, "Enemy Intent Glyph", new Vector2(24f, 24f), Vector2.zero, 15);

            RectTransform enemyResources = SetNavalHudPanel(root, "Enemy Resource HUD", false, -336f, 116f);
            SetNavalHudText(enemyResources, "Enemy Resource Title", new Vector2(212f, 26f), new Vector2(0f, 36f), 17);
            SetNavalHudText(enemyResources, "Enemy Burning", new Vector2(212f, 24f), new Vector2(0f, -38f), 16);
            ArrangeNavalResourceRow(enemyResources, "回", -54f);
            ArrangeNavalResourceRow(enemyResources, "盾", 54f);
            SetNavalHudPanel(root, "End Battle Turn", false, -420f, 40f, 18);

            // All action text remains upright and outside the hand's width.
            // A disabled action retains its actual current eligibility reason.
            SetNavalAction(root, "Battle Ram", "Battle Ram Condition", 40f, -5f, 42f);
            SetNavalAction(root, "Battle Board", "Battle Board Condition", -56f, -117f, 48f);

            RectTransform school = root.Find("Battle School Status Panel") as RectTransform;
            if (school != null)
            {
                Text status = SetNavalHudText(school, "Battle School Status", new Vector2(208f, 300f), Vector2.zero, 16);
                if (status != null)
                {
                    status.alignment = TextAnchor.UpperLeft;
                    float height = Mathf.Clamp(status.preferredHeight + 18f, 120f, 320f);
                    SetNavalHudRect(school, new Vector2(230f, height), new Vector2(-735f, -109f + height * .5f));
                    status.rectTransform.sizeDelta = new Vector2(208f, height - 18f);
                }
            }

            RectTransform curse = root.Find("Battle Curse Panel") as RectTransform;
            SetNavalHudRect(curse, new Vector2(230f, 70f), new Vector2(-735f, 310f));
            SetNavalHudText(curse, "Battle Curse Status", new Vector2(212f, 60f), Vector2.zero, 14);

            RefreshNavalActionHud(root, battle);
        }

        private static RectTransform SetNavalHudPanel(RectTransform root, string name, bool player,
            float y, float height, int buttonFontSize = 17)
        {
            RectTransform panel = root.Find(name) as RectTransform;
            if (panel == null) return null;
            SetNavalHudRect(panel, new Vector2(230f, height), new Vector2(player ? -735f : 735f, y));
            SetNavalHudText(panel, "Label", panel.sizeDelta - new Vector2(20f, 8f), Vector2.zero, buttonFontSize);
            return panel;
        }

        private static void SetNavalAction(RectTransform root, string buttonName, string hintName,
            float buttonY, float hintY, float hintHeight)
        {
            RectTransform button = root.Find(buttonName) as RectTransform;
            if (button == null) return;
            SetNavalHudRect(button, new Vector2(220f, 40f), new Vector2(735f, buttonY));
            SetNavalHudText(button, "Label", new Vector2(208f, 34f), Vector2.zero, 15);
            Text hint = SetNavalHudText(root, hintName, new Vector2(212f, hintHeight), new Vector2(735f, hintY), 13);
            if (hint != null)
            {
                hint.resizeTextForBestFit = true;
                hint.resizeTextMinSize = 12;
                hint.resizeTextMaxSize = 13;
                UiLabelBacking.Apply(hint);
            }
        }

        private static void ArrangeNavalResourceRow(RectTransform panel, string glyph, float x, float y = -2f)
        {
            if (panel == null) return;
            RectTransform icon = panel.Find("Resource " + glyph + " Icon") as RectTransform;
            SetNavalHudRect(icon, new Vector2(34f, 34f), new Vector2(x - 22f, y));
            SetNavalHudText(icon, "Resource " + glyph + " Glyph", new Vector2(30f, 30f), Vector2.zero, 15);
            Text amount = SetNavalHudText(panel, "Resource " + glyph + " Value",
                new Vector2(56f, 34f), new Vector2(x + 27f, y), 18);
            if (amount != null) amount.alignment = TextAnchor.MiddleLeft;
        }

        private static Text SetNavalHudText(Transform parent, string name, Vector2 size, Vector2 position, int fontSize)
        {
            if (parent == null) return null;
            Text label = parent.Find(name)?.GetComponent<Text>();
            if (label == null) return null;
            SetNavalHudRect(label.rectTransform, size, position);
            label.fontSize = fontSize;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static void SetNavalHudRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.identity;
        }
    }
}
