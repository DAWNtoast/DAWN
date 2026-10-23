using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Call before ConfigureBattleCardPresentation so hover feedback captures
        // the school's actual resting color instead of the old dark panel.
        private void ApplyBattleCardStyle(Button play, CoreBattleCard data, bool available, int index)
        {
            BattleCardPalette palette = CardSchoolArtCatalog.Palette(data.SchoolId);
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
            surface.Configure(data.Type, available, data.SchoolId);

            // Keep a readable paper surface beneath the live description. The
            // independent school frame is added above the content afterward.
            Image paper = CreateImage("Card Description Parchment " + index, play.transform,
                CardFaceLayout.EffectPaperSize, CardFaceLayout.EffectPaperPosition,
                Color.Lerp(Color.white, palette.Parchment, 0.35f), NauticalSurfaceSkin.Paper);
            paper.type = Image.Type.Tiled;
            paper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 280f;
            paper.raycastTarget = false;
            paper.transform.SetSiblingIndex(face.GetSiblingIndex() + 1);

            RectTransform namePlate = CreateCardOrnament("Card Name Plate " + index, play.transform,
                CardFaceLayout.NamePlateSize, CardFaceLayout.NamePlatePosition, CardOrnamentKind.NameRibbon, data.Type, data.SchoolId);
            Text name = CreateText("Card Name " + index, namePlate, CardFaceLayout.NameSize,
                CardFaceLayout.NamePosition, 20, FontStyle.Bold, TextAnchor.MiddleCenter, palette.TitleInk);
            name.text = CardFaceLayout.DisplayName(data);
            name.color = palette.TitleInk;
            name.raycastTarget = false;

            Transform labelTransform = play.transform.Find("Label");
            if (labelTransform != null)
            {
                Text label = labelTransform.GetComponent<Text>();
                label.color = palette.Ink;
                label.raycastTarget = false;
                label.rectTransform.sizeDelta = new Vector2(176f, 96f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -94f);
                label.font = GameTypography.BodyFont;
                label.fontStyle = FontStyle.Normal;
                label.fontSize = 15;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = false;
                label.resizeTextMinSize = 15;
                label.resizeTextMaxSize = 15;
                label.text = BattleCardEffectTextForCurrentStage(data);
                RefreshBattleCardTextLayout(label);
            }
            AddCardSchoolFrame(play.transform, data, available);
            RefreshCardSupplyCost(play.transform, data, session?.Battle);
            // Resource symbols sit over the illustration and outer rim.
            play.transform.Find("Card Energy Cost " + index)?.SetAsLastSibling();
            play.transform.Find("Card Powder Plate " + index)?.SetAsLastSibling();
            play.transform.Find("Card Cannonball Plate " + index)?.SetAsLastSibling();
            play.transform.Find("Card Special Resource Plate " + index)?.SetAsLastSibling();
        }

        private static string BattleHandSummary(CoreBattleCard data)
        {
            // The effective card is the source of truth, including all three upgrades.
            return data.Description;
        }

        private string BattleCardDisplayTextForCurrentStage(CoreBattleCard data, BattleCardPalette palette)
        {
            return BattleCardDisplayText(BattleCardForCurrentStage(data), palette);
        }

        private string BattleCardEffectTextForCurrentStage(CoreBattleCard data)
        {
            return FormatBattleCardDescription(BattleCardForCurrentStage(data)?.Description);
        }

        private CoreBattleCard BattleCardForCurrentStage(CoreBattleCard data)
        {
            if (data == null || session == null || session.Battle == null)
                return data;
            // Keep one authored face in every context. Its numeric slots receive
            // the current quote; the side tooltip only explains visible keywords.
            NavalBattleStage stage = session.Battle.Stage;
            string summary = session.BattleCardFaceDescription(data, stage);
            NavalBattleStages displayStage = stage == NavalBattleStage.Boarding ? NavalBattleStages.Boarding :
                stage == NavalBattleStage.Naval ? NavalBattleStages.Naval : NavalBattleStages.Preparation;
            var display = new CoreBattleCard(data.Id, data.Name, data.Type, data.Energy,
                data.Damage, data.Block, data.Repair, data.Draw, data.PowderCost,
                summary,
                displayStage, data.Action, data.RequiredCrew, data.ArtId, data.CannonballCost,
                data.RequiredComponent, data.UpgradeLevel, data.SchoolId, data.SchoolEffect);
            return display;
        }

        private static string BattleCardDisplayText(CoreBattleCard data, BattleCardPalette palette)
        {
            if (data == null) return string.Empty;
            string title = data.Name + (data.UpgradeLevel > 0 ? " +" + data.UpgradeLevel : string.Empty);
            // Keep the title, category and compact effect paragraphs separate.
            // Card-face layout binds numeric ranges and units when wrapping.
            string effect = FormatBattleCardDescription(BattleHandSummary(data));
            return "<color=#" + ColorUtility.ToHtmlStringRGB(palette.TitleInk) + "><size=20>" + title +
                "</size></color>\n<size=13>" + data.Type + "</size>\n" + effect;
        }

        private static string FormatBattleCardDescription(string value)
        {
            return CardFaceLayout.FormatDescription(value);
        }

        /// <summary>
        /// All cards keep identical regions. Only a field that exceeds its
        /// shared font size shrinks; longer effects never move or resize the art.
        /// </summary>
        private static void RefreshBattleCardTextLayout(Text label)
        {
            if (label == null || label.transform.parent == null) return;
            Transform card = label.transform.parent;
            RectTransform paper = null, art = null, namePlate = null;
            foreach (Transform child in card)
            {
                if (child.name.StartsWith("Card Description Parchment ")) paper = child as RectTransform;
                else if (child.name.StartsWith("Card Art ")) art = child as RectTransform;
                else if (child.name.StartsWith("Card Name Plate ")) namePlate = child as RectTransform;
            }
            label.rectTransform.sizeDelta = CardFaceLayout.EffectSize;
            label.rectTransform.anchoredPosition = CardFaceLayout.EffectPosition;
            label.alignment = TextAnchor.MiddleCenter;
            CardFaceLayout.FitEffect(label);
            if (paper != null)
            {
                paper.sizeDelta = CardFaceLayout.EffectPaperSize;
                paper.anchoredPosition = CardFaceLayout.EffectPaperPosition;
            }
            if (namePlate != null)
            {
                Text name = namePlate.GetComponentInChildren<Text>();
                namePlate.sizeDelta = CardFaceLayout.NamePlateSize;
                namePlate.anchoredPosition = CardFaceLayout.NamePlateLayout(namePlate);
                if (name != null)
                {
                    name.rectTransform.sizeDelta = CardFaceLayout.NameSize;
                    name.rectTransform.anchoredPosition = CardFaceLayout.NamePosition;
                    CardFaceLayout.FitName(name);
                }
            }
            if (art != null)
            {
                CardFaceLayout.FitIllustration(art.GetComponent<RawImage>());
            }
        }
    }
}
