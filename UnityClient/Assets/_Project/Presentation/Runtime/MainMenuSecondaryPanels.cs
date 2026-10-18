using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class MainMenuController
    {
        private static readonly Color SecondaryPanelMutedInk = new Color(.44f, .36f, .27f, 1f);
        private const string NewGameConfirmationMessage = "现有航程、教学进度、金币、贵重藏品及陈列记录将被重置。\n游戏设置将保留。";
        // Apply after prefab binding and procedural authoring so both entry paths
        // share the market's paper, controls and typography without rebuilding views.
        private void StyleMenuSettings()
        {
            RectTransform card = settingsPanel.Find("Settings Card") as RectTransform;
            if (card == null) return;
            StyleSecondaryPaper(settingsPanel, card, new Vector2(980f, 780f));
            StyleMenuHeading(card, 320f, 278f, 240f);
            Text subtitle = card.Find("Modal Subtitle")?.GetComponent<Text>();
            if (subtitle != null)
            {
                subtitle.text = string.Empty;
                subtitle.gameObject.SetActive(false);
            }
            card.Find("Settings Hint")?.gameObject.SetActive(false);
            StyleMenuVolume(card, masterSlider, masterValue, "主音量", 183f);
            StyleMenuVolume(card, musicSlider, musicValue, "背景音乐", 83f);
            StyleMenuVolume(card, effectsSlider, effectsValue, "战斗音效", -17f);

            SetMenuRect((RectTransform)fullScreenToggle.transform, new Vector2(756f, 60f), new Vector2(0f, -153f));
            Image box = fullScreenToggle.transform.Find("Background")?.GetComponent<Image>();
            if (box != null)
            {
                SetMenuRect(box.rectTransform, new Vector2(32f, 32f), new Vector2(-362f, 0f));
                box.overrideSprite = null;
                box.sprite = null;
                box.color = new Color(.97f, .94f, .85f, 1f);
                Outline edge = box.GetComponent<Outline>() ?? box.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(.46f, .34f, .20f, 1f);
                edge.effectDistance = new Vector2(1f, -1f);
                Image check = fullScreenToggle.graphic as Image;
                if (check != null)
                {
                    SetMenuRect(check.rectTransform, new Vector2(22f, 22f), Vector2.zero);
                    check.overrideSprite = null;
                    check.sprite = masterSlider.handleRect.GetComponent<Image>().sprite;
                    check.color = Color.white;
                }
                fullScreenToggle.colors = ColorBlock.defaultColorBlock;
            }
            Text fullScreenLabel = fullScreenToggle.GetComponentInChildren<Text>(true);
            StyleMenuText(fullScreenLabel, new Vector2(670f, 42f), new Vector2(32f, 0f), 23, FontStyle.Normal, TextAnchor.MiddleLeft);
            MenuRule(card, "Settings Footer Divider", new Vector2(784f, 1f), new Vector2(0f, -235f));
            StyleMenuAction(card.Find("Back From Settings")?.GetComponent<Button>(), new Vector2(330f, 56f), new Vector2(-211f, -298f), false);
            StyleMenuAction(card.Find("Apply Settings")?.GetComponent<Button>(), new Vector2(330f, 56f), new Vector2(211f, -298f), true);
        }

        private void StyleMenuAchievements()
        {
            RectTransform card = achievementsPanel.Find("Achievements Card") as RectTransform;
            if (card == null) return;
            StyleSecondaryPaper(achievementsPanel, card, new Vector2(1180f, 960f));
            StyleMenuHeading(card, 434f, 395f, 326f);
            StyleMenuText(achievementSummary, new Vector2(880f, 32f), new Vector2(0f, 351f), 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            achievementSummary.color = SecondaryPanelMutedInk;
            SetMenuRect((RectTransform)achievementCategory.transform, new Vector2(380f, 44f), new Vector2(-330f, 284f));
            StyleMenuDropdown(achievementCategory);
            for (int i = 0; i < achievementStateButtons.Count; i++)
            {
                Button filter = achievementStateButtons[i];
                StyleMenuAction(filter, new Vector2(200f, 44f), new Vector2(-5f + i * 210f, 284f), false);
                MarketShopSkin.SetMode(filter, i == achievementStateFilter);
            }
            for (int i = 0; i < achievementRows.Count; i++)
            {
                MainMenuAchievementRowView row = achievementRows[i];
                SetMenuRect(row.Root, new Vector2(1040f, 68f), new Vector2(0f, 217f - i * 76f));
                row.Face.sprite = row.Face.overrideSprite = null;
                row.Face.raycastTarget = false;
                SetMenuRect(row.Icon.rectTransform, new Vector2(60f, 60f), new Vector2(-477f, 0f));
                StyleMenuText(row.Title, new Vector2(710f, 32f), new Vector2(-68f, 15f), 24, FontStyle.Bold, TextAnchor.MiddleLeft);
                StyleMenuText(row.Description, new Vector2(710f, 28f), new Vector2(-68f, -17f), 18, FontStyle.Normal, TextAnchor.MiddleLeft);
                row.Description.color = SecondaryPanelMutedInk;
                StyleMenuText(row.State, new Vector2(162f, 28f), new Vector2(420f, 14f), 18, FontStyle.Normal, TextAnchor.MiddleRight);
                StyleMenuText(row.Progress, new Vector2(162f, 26f), new Vector2(420f, -16f), 18, FontStyle.Normal, TextAnchor.MiddleRight);
                row.Progress.color = SecondaryPanelMutedInk;
                MenuRule(row.Root, "Achievement Ledger Rule", new Vector2(1020f, 1f), new Vector2(0f, -37f));
            }
            StyleMenuAction(achievementPreviousPage, new Vector2(190f, 40f), new Vector2(-290f, -382f), false);
            StyleMenuAction(achievementNextPage, new Vector2(190f, 40f), new Vector2(290f, -382f), false);
            StyleMenuText(achievementPageLabel, new Vector2(220f, 36f), new Vector2(0f, -382f), 21, FontStyle.Normal, TextAnchor.MiddleCenter);
            StyleMenuAction(card.Find("Back From Achievements")?.GetComponent<Button>(), new Vector2(320f, 40f), new Vector2(0f, -429f), true);
            if (achievementEmpty != null) achievementEmpty.color = SecondaryPanelMutedInk;
        }

        private void StyleMenuNewGameConfirmation()
        {
            RectTransform card = confirmationPanel.Find("Confirmation Card") as RectTransform;
            if (card == null) return;
            StyleSecondaryPaper(confirmationPanel, card, new Vector2(860f, 440f));
            Text eyebrow = card.Find("Confirmation Eyebrow")?.GetComponent<Text>();
            StyleMenuText(eyebrow, new Vector2(716f, 26f), new Vector2(0f, 164f), 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (eyebrow != null) eyebrow.color = SecondaryPanelMutedInk;
            StyleMenuText(card.Find("Confirmation Title")?.GetComponent<Text>(), new Vector2(716f, 54f),
                new Vector2(0f, 116f), 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            MenuRule(card, "Confirmation Heading Divider", new Vector2(716f, 1f), new Vector2(0f, 74f));
            Text body = card.Find("Confirmation Body")?.GetComponent<Text>();
            StyleMenuText(body, new Vector2(716f, 96f), new Vector2(0f, -2f), 23, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (body != null)
            {
                body.text = NewGameConfirmationMessage;
                body.lineSpacing = 1.35f;
            }
            MenuRule(card, "Confirmation Footer Divider", new Vector2(716f, 1f), new Vector2(0f, -84f));
            StyleMenuAction(card.Find("Cancel New Game")?.GetComponent<Button>(), new Vector2(330f, 56f), new Vector2(-193f, -144f), false);
            StyleMenuAction(card.Find("Confirm New Game")?.GetComponent<Button>(), new Vector2(330f, 56f), new Vector2(193f, -144f), true);
        }

        private static void StyleSecondaryPaper(RectTransform backdrop, RectTransform card, Vector2 size)
        {
            Image shade = backdrop.GetComponent<Image>();
            if (shade != null)
            {
                shade.sprite = shade.overrideSprite = null;
                shade.color = new Color(.025f, .035f, .055f, .68f);
            }
            SetMenuRect(card, size, Vector2.zero);
            MarketShopSkin.ApplyPanel(card.GetComponent<Image>());
        }

        private static void StyleMenuHeading(RectTransform card, float eyebrowY, float titleY, float lineY)
        {
            Text eyebrow = card.Find("Modal Eyebrow")?.GetComponent<Text>();
            StyleMenuText(eyebrow, new Vector2(880f, 26f), new Vector2(0f, eyebrowY), 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (eyebrow != null) eyebrow.color = SecondaryPanelMutedInk;
            StyleMenuText(card.Find("Modal Title")?.GetComponent<Text>(), new Vector2(880f, 54f), new Vector2(0f, titleY), 36, FontStyle.Bold, TextAnchor.MiddleCenter);
            MenuRule(card, "Modal Divider", new Vector2(card.sizeDelta.x - 144f, 1f), new Vector2(0f, lineY));
        }

        private static void StyleMenuVolume(RectTransform card, Slider slider, Text value, string label, float y)
        {
            if (slider == null) return;
            Text caption = card.Find(slider.name + " Label")?.GetComponent<Text>();
            StyleMenuText(caption, new Vector2(580f, 34f), new Vector2(-88f, y), 23, FontStyle.Normal, TextAnchor.MiddleLeft);
            if (caption != null) caption.text = label;
            StyleMenuText(value, new Vector2(120f, 34f), new Vector2(318f, y), 22, FontStyle.Normal, TextAnchor.MiddleRight);
            SetMenuRect((RectTransform)slider.transform, new Vector2(756f, 40f), new Vector2(0f, y - 40f));
            SetMenuRect(slider.transform.Find("Background") as RectTransform, new Vector2(756f, 7f), Vector2.zero);
            SetMenuRect(slider.transform.Find("Fill Area") as RectTransform, new Vector2(756f, 7f), Vector2.zero);
            SetMenuRect(slider.transform.Find("Handle Slide Area") as RectTransform, new Vector2(756f, 0f), Vector2.zero);
            if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(30f, 32f);
            Image hit = slider.GetComponent<Image>() ?? slider.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            MarketShopSkin.ApplySlider(slider);
        }

        private static void StyleMenuDropdown(Dropdown dropdown)
        {
            Image face = dropdown.GetComponent<Image>();
            face.sprite = face.overrideSprite = null;
            face.color = new Color(.96f, .93f, .85f, 1f);
            Outline edge = face.GetComponent<Outline>() ?? face.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(.46f, .34f, .20f, .75f);
            edge.effectDistance = new Vector2(1f, -1f);
            dropdown.colors = ColorBlock.defaultColorBlock;
            StyleMenuText(dropdown.captionText, new Vector2(314f, 40f), new Vector2(-12f, 0f), 20, FontStyle.Normal, TextAnchor.MiddleLeft);
            Text arrow = dropdown.transform.Find("Category Arrow")?.GetComponent<Text>();
            StyleMenuText(arrow, new Vector2(28f, 40f), new Vector2(164f, 0f), 18, FontStyle.Normal, TextAnchor.MiddleCenter);
            foreach (Image image in dropdown.template.GetComponentsInChildren<Image>(true))
            {
                image.sprite = image.overrideSprite = null;
                image.color = image.name == "Selected" ? new Color(.60f, .43f, .21f, 1f) : new Color(.96f, .93f, .85f, 1f);
            }
            foreach (Text text in dropdown.template.GetComponentsInChildren<Text>(true)) text.color = MarketShopSkin.Ink;
        }

        private static void StyleMenuAction(Button button, Vector2 size, Vector2 position, bool primary)
        {
            if (button == null) return;
            SetMenuRect((RectTransform)button.transform, size, position);
            Text label = button.GetComponentInChildren<Text>(true);
            StyleMenuText(label, size - new Vector2(24f, 6f), Vector2.zero, 23, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (primary) MarketShopSkin.SetMode(button, true);
            else MarketShopSkin.ApplyLightButton(button);
        }

        private static void StyleMenuText(Text text, Vector2 size, Vector2 position, int fontSize, FontStyle style, TextAnchor alignment)
        {
            if (text == null) return;
            SetMenuRect(text.rectTransform, size, position);
            GameTypography.Apply(text, fontSize, style);
            text.color = MarketShopSkin.Ink;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void MenuRule(RectTransform parent, string name, Vector2 size, Vector2 position)
        {
            Image line = parent.Find(name)?.GetComponent<Image>();
            if (line == null) line = CreateRect(name, parent, size, position).gameObject.AddComponent<Image>();
            SetMenuRect(line.rectTransform, size, position);
            line.sprite = line.overrideSprite = null;
            line.color = new Color(.60f, .47f, .30f, .55f);
            line.raycastTarget = false;
        }

        private static void SetMenuRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
