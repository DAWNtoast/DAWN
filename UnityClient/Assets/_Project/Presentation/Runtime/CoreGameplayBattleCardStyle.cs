using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Call before ConfigureBattleCardPresentation so hover feedback captures
        // the category's actual resting color instead of the old dark panel.
        private void ApplyBattleCardStyle(Button play, CoreBattleCard data, bool available, int index)
        {
            BattleCardPalette palette = BattleCardStylePalette.ForType(data.Type);
            Image backing = play.GetComponent<Image>();
            backing.color = palette.Surface;
            backing.sprite = null;
            backing.type = Image.Type.Simple;
            play.targetGraphic = backing;

            // Selectable multiplies its state tint by Image.color. A dark tint
            // here would square the surface color and make every type black.
            ColorBlock states = play.colors;
            states.normalColor = Color.white;
            states.highlightedColor = Color.white;
            states.selectedColor = Color.white;
            states.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            states.disabledColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            states.colorMultiplier = 1f;
            play.colors = states;

            Outline outline = play.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = available ? Color.Lerp(palette.Accent, palette.Surface, .5f) : Color.Lerp(palette.Accent, palette.Surface, 0.4f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            RectTransform face = CreateRect("Card Type Surface " + index, play.transform, Vector2.zero, Vector2.zero);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.offsetMin = new Vector2(2f, 2f);
            face.offsetMax = new Vector2(-2f, -2f);
            face.SetAsFirstSibling();
            BattleCardSurfaceGraphic surface = face.gameObject.AddComponent<BattleCardSurfaceGraphic>();
            surface.Configure(data.Type, available);

            // Preserve each category's authored border, but use real paper fibers
            // beneath the description instead of a nearly uniform procedural fill.
            Image paper = CreateImage("Card Description Parchment " + index, play.transform,
                new Vector2(178f, 96f), new Vector2(0f, -93f),
                Color.Lerp(Color.white, palette.Parchment, 0.35f), NauticalSurfaceSkin.Paper);
            paper.type = Image.Type.Tiled;
            paper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 280f;
            paper.raycastTarget = false;
            paper.transform.SetSiblingIndex(face.GetSiblingIndex() + 1);

            Transform labelTransform = play.transform.Find("Label");
            if (labelTransform != null)
            {
                Text label = labelTransform.GetComponent<Text>();
                label.color = palette.Ink;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(176f, 96f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -94f);
                label.font = GameTypography.BodyFont;
                label.fontSize = GameReadability.FontSize(22);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = false;
                label.resizeTextMinSize = 15;
                label.resizeTextMaxSize = 15;
                label.text = BattleCardDisplayTextForCurrentStage(data, palette);
                RefreshBattleCardTextLayout(label);
            }

            Transform accent = play.transform.Find("Card Accent " + index);
            if (accent != null) accent.GetComponent<Image>().color = palette.Accent;
            Transform energy = play.transform.Find("Card Energy Cost " + index);
            if (energy != null)
            {
                Image badge = energy.GetComponent<Image>();
                badge.sprite = NauticalSurfaceSkin.Paper;
                badge.type = Image.Type.Tiled;
                badge.color = Color.Lerp(palette.Parchment, palette.Accent, 0.3f);
                Text cost = energy.GetComponentInChildren<Text>();
                if (cost != null)
                {
                    cost.color = palette.Ink;
                    GameReadability.Body(cost, 22);
                    float side = 40f * GameReadability.Scale;
                    badge.rectTransform.sizeDelta = new Vector2(side, side);
                    badge.rectTransform.anchoredPosition = new Vector2(-98f + side * .5f, 144f - side * .5f);
                    cost.rectTransform.sizeDelta = new Vector2(side - 4f, side - 4f);
                }
            }

            // The small ammunition label also needs an opaque light plate when
            // drawn over a busy illustration rather than over the old black UI.
            Transform powder = play.transform.Find("Card Powder Cost " + index);
            if (powder != null)
            {
                RectTransform plate = CreateFixedPanel("Card Powder Plate " + index, play.transform,
                    new Vector2(74f, 62f), new Vector2(57f, 114f), palette.Parchment);
                plate.GetComponent<Image>().raycastTarget = false;
                plate.SetSiblingIndex(powder.GetSiblingIndex());
                powder.GetComponent<Text>().color = palette.TitleInk;
                powder.GetComponent<Text>().raycastTarget = false;
            }
        }

        private static string BattleHandSummary(CoreBattleCard data)
        {
            // The effective card is the source of truth, including all three upgrades.
            return data.Description;
        }

        private string BattleCardDisplayTextForCurrentStage(CoreBattleCard data, BattleCardPalette palette)
        {
            if (data == null || session == null || session.Battle == null || session.Battle.Stage != NavalBattleStage.Boarding)
                return BattleCardDisplayText(data, palette);
            // The immutable library card remains the upgraded base definition.
            // Only this display copy receives the same actual effects as PlayCard.
            string summary = session.BattleCardEffectDescription(data, NavalBattleStage.Boarding);
            if (data.Repair > 0)
            {
                CoreBattleCardEffects healing = session.GetBattleCardEffects(data, NavalBattleStage.Boarding);
                summary = summary.Replace("恢复船员 " + healing.Repair, "本次恢复 " + healing.ActualRepair);
                summary += "\n" + (healing.ActualRepair == 0 ?
                    (session.Battle.PlayerBoardingHealth >= session.Battle.PlayerMaxBoardingHealth ? "满血不耗药；" : "未恢复，不耗药；") +
                    (healing.HasMedicineBoost ? "有药上限 " + healing.Repair + "，" : string.Empty) + "无药 " + healing.BaseRepair + "。"
                    : (healing.MedicineCost > 0 ? "药品 -1" : "不耗药") + "；无药基础 " + healing.BaseRepair + "。");
            }
            var display = new CoreBattleCard(data.Id, data.Name, data.Type, data.Energy,
                data.Damage, data.Block, data.Repair, data.Draw, data.PowderCost,
                "实际：" + summary,
                data.AllowedStages, data.Action, data.RequiredCrew, data.ArtId, data.CannonballCost,
                data.RequiredComponent, data.UpgradeLevel);
            return BattleCardDisplayText(display, palette);
        }

        private static string BattleCardDisplayText(CoreBattleCard data, BattleCardPalette palette)
        {
            if (data == null) return string.Empty;
            string title = data.Name + (data.UpgradeLevel > 0 ? " +" + data.UpgradeLevel : string.Empty);
            // Keep the card's three semantic fields on separate visual rows. A non-breaking
            // space keeps ranges such as "60 米" together when the paper panel is narrow.
            string effect = FormatBattleCardDescription(BattleHandSummary(data));
            return "<color=#" + ColorUtility.ToHtmlStringRGB(palette.TitleInk) + "><size=" + GameReadability.FontSize(32) + ">" + title +
                "</size></color>\n<size=" + GameReadability.FontSize(18) + ">" + data.Type + "</size>\n" + effect;
        }

        private static string KeepCardUnitTogether(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace(" 米", "\u00A0米");
        }

        private static string FormatBattleCardDescription(string value)
        {
            return KeepCardUnitTogether(CoreBattleCardText.FormatEffects(value));
        }

        /// <summary>
        /// Keep every effect readable at the same font size. Longer descriptions
        /// borrow space from the illustration instead of clipping or shrinking.
        /// Also used when an existing hand card's live healing/cost text changes.
        /// </summary>
        private static void RefreshBattleCardTextLayout(Text label)
        {
            if (label == null || label.transform.parent == null) return;
            Button button = label.GetComponentInParent<Button>();
            if (button == null) return;
            Transform card = button.transform;
            RectTransform paper = null, art = null, repeat = null;
            foreach (Transform child in card)
            {
                if (child.name.StartsWith("Card Description Parchment ")) paper = child as RectTransform;
                else if (child.name.StartsWith("Card Art ")) art = child as RectTransform;
                else if (child.name.StartsWith("Card Repeat Cost Plate ")) repeat = child as RectTransform;
            }
            float repeatHeight = 0f;
            if (repeat != null)
            {
                Text repeated = repeat.GetComponentInChildren<Text>();
                GameReadability.Body(repeated, 18);
                repeated.rectTransform.sizeDelta = new Vector2(176f, 100f);
                repeatHeight = repeat.gameObject.activeSelf ? Mathf.Min(106f, MeasureCardTextHeight(repeated) + 4f) : 0f;
                repeat.sizeDelta = new Vector2(182f, Mathf.Max(28f, repeatHeight));
                repeat.anchoredPosition = new Vector2(0f, -142f + repeat.rect.height * .5f);
                repeated.rectTransform.sizeDelta = new Vector2(176f, repeat.rect.height);
            }
            float bottom = -142f + repeatHeight;
            float height = 222f - repeatHeight;
            ReadableParagraph(label, card, new Vector2(184f, height), new Vector2(0f, bottom + height * .5f), 22, false).transform.SetSiblingIndex(3);
            if (paper != null)
            {
                paper.sizeDelta = new Vector2(190f, 226f);
                paper.anchoredPosition = new Vector2(0f, -31f);
            }
            if (art != null)
            {
                art.sizeDelta = new Vector2(38f, 38f);
                art.anchoredPosition = new Vector2(0f, 113f);
            }
            foreach (Transform child in card)
                if (child.name.StartsWith("Card Powder Cost "))
                {
                    Text powder = child.GetComponent<Text>();
                    GameReadability.Body(powder, 18);
                    powder.text = powder.text.Replace("火药 ", "火药\n");
                    powder.rectTransform.sizeDelta = new Vector2(74f, 62f);
                    powder.rectTransform.anchoredPosition = new Vector2(57f, 114f);
                }

        }
    }
}
