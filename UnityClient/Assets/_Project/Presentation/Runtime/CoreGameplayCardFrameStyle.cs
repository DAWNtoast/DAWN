using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform CreateCollectionCardFace(Transform parent, CoreBattleCard data,
            string prefix, string artName, string nameName, float scale = 0.5f,
            string effectDescription = null, string effectName = null)
        {
            BattleCardPalette palette = CardSchoolArtCatalog.Palette(data.SchoolId);
            RectTransform face = CreateRect(prefix + " Face", parent, CardFaceLayout.CardSize, Vector2.zero);
            face.localScale = Vector3.one * scale;
            Image backing = face.gameObject.AddComponent<Image>();
            backing.color = palette.Surface;
            backing.raycastTarget = false;
            RectTransform surface = CreateRect(prefix + " Surface", face, Vector2.zero, Vector2.zero);
            surface.anchorMin = Vector2.zero;
            surface.anchorMax = Vector2.one;
            surface.offsetMin = Vector2.one * 2f;
            surface.offsetMax = -Vector2.one * 2f;
            surface.gameObject.AddComponent<BattleCardSurfaceGraphic>().Configure(data.Type, true, data.SchoolId);

            CreateCardIllustration(artName, face, data);
            Image paper = CreateImage(prefix + " Effect Paper", face, CardFaceLayout.EffectPaperSize,
                CardFaceLayout.EffectPaperPosition, Color.Lerp(Color.white, palette.Parchment, 0.35f), NauticalSurfaceSkin.Paper);
            paper.type = Image.Type.Tiled;
            paper.pixelsPerUnitMultiplier = NauticalSurfaceSkin.Paper == null ? 1f : NauticalSurfaceSkin.Paper.rect.width / 280f;
            paper.raycastTarget = false;
            RectTransform ribbon = CreateCardOrnament(prefix + " Name Ribbon", face,
                CardFaceLayout.NamePlateSize, CardFaceLayout.NamePlatePosition, CardOrnamentKind.NameRibbon, data.Type, data.SchoolId);
            Text title = CreateText(nameName, ribbon, CardFaceLayout.NameSize, CardFaceLayout.NamePosition,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, palette.TitleInk);
            title.text = CardFaceLayout.DisplayName(data);
            title.color = palette.TitleInk;
            CardFaceLayout.FitName(title);
            Text effect = CreateText(effectName ?? prefix + " Effect", face, CardFaceLayout.EffectSize, CardFaceLayout.EffectPosition,
                15, FontStyle.Normal, TextAnchor.MiddleCenter, palette.Ink);
            effect.text = FormatBattleCardDescription(effectDescription ??
                (session != null ? SpecialCardPreviewEffectDescription(data) : data.Description));
            effect.color = palette.Ink;
            CardFaceLayout.FitEffect(effect);
            AddCardSchoolFrame(face, data, true);
            CreateCardCostIcon(prefix + " Energy Icon", prefix + " Energy Cost", face,
                CardFaceLayout.EnergyIconSize, CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Energy),
                CardOrnamentKind.Energy, data.Type, data.Energy);
            CreateCardResourceCostIcons(face, data, prefix + " Powder Icon", prefix + " Powder Cost",
                prefix + " Cannonball Icon", prefix + " Cannonball Cost", out _, out _);
            CreateCardSpecialCostIcon(face, data, prefix + " Special Resource Icon", prefix + " Special Resource Cost");
            return face;
        }

        private RawImage CreateCardIllustration(string name, Transform parent, CoreBattleCard data)
        {
            Color backingColor = Color.Lerp(Color.black, CardSchoolArtCatalog.Palette(data.SchoolId).Surface, .28f);
            Image backing = CreateImage("Card Illustration Backdrop " + name, parent, CardFaceLayout.ArtSize,
                CardFaceLayout.ArtPosition, backingColor);
            backing.raycastTarget = false;
            RawImage picture = CreateRawImage(name, parent, CardFaceLayout.ArtSize,
                CardFaceLayout.ArtPosition, CardSchoolArtCatalog.Illustration(data, art),
                CardSchoolArtCatalog.IllustrationUv(data, art));
            picture.raycastTarget = false;
            CardFaceLayout.FitIllustration(picture);
            return picture;
        }

        private void CreateCardResourceCostIcons(Transform parent, CoreBattleCard data,
            string powderName, string powderValueName, string cannonballName, string cannonballValueName,
            out Text powder, out Text cannonballs)
        {
            // Read the same effective definition as PlayCard, including tutorial overrides.
            // Each resource has its own badge; quantities must never be added together.
            powder = data.PowderCost > 0
                ? CreateCardCostIcon(powderName, powderValueName, parent, CardFaceLayout.PowderIconSize,
                    CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Gunpowder), CardOrnamentKind.Gunpowder, data.Type, data.PowderCost)
                : null;
            cannonballs = data.CannonballCost > 0
                ? CreateCardCostIcon(cannonballName, cannonballValueName, parent, CardFaceLayout.CannonballIconSize,
                    CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Cannonball), CardOrnamentKind.Cannonball, data.Type, data.CannonballCost)
                : null;
        }

        private CardSchoolFrameGraphic AddCardSchoolFrame(Transform card, CoreBattleCard data, bool available)
        {
            return AddCardSchoolFrame(card, data.SchoolId, available, data.PowderCost,
                data.CannonballCost, CardSpecialCostPresentation.ResourceType(data));
        }

        private CardSchoolFrameGraphic AddCardSchoolFrame(Transform card, string schoolId, bool available,
            int powderCost = 0, int cannonballCost = 0, string specialType = "")
        {
            Rect bounds = CardSchoolArtCatalog.FrameBounds(schoolId, powderCost, cannonballCost, specialType);
            RectTransform frame = CreateRect("Card School Frame", card, bounds.size, bounds.center);
            CardSchoolFrameGraphic graphic = frame.gameObject.AddComponent<CardSchoolFrameGraphic>();
            graphic.Configure(schoolId, available, powderCost, cannonballCost, specialType);
            // Small optical offsets belong to the complete authored frame.
            // Initial creation and retained refresh share this calibration.
            foreach (RawImage illustration in card.GetComponentsInChildren<RawImage>(true))
                CardFaceLayout.FitIllustration(illustration);
            foreach (CardOrnamentGraphic ornament in card.GetComponentsInChildren<CardOrnamentGraphic>(true))
                if (ornament.Kind == CardOrnamentKind.NameRibbon)
                {
                    ornament.rectTransform.anchoredPosition = CardFaceLayout.NamePlateLayout(ornament.rectTransform);
                    foreach (Text title in ornament.GetComponentsInChildren<Text>(true)) CardFaceLayout.FitName(title);
                }
            Image body = card.GetComponent<Image>();
            if (body != null) body.color = Color.clear;
            foreach (Image paper in card.GetComponentsInChildren<Image>(true))
                if (paper.name.EndsWith(" Effect Paper", System.StringComparison.Ordinal) ||
                    paper.name.StartsWith("Card Description Parchment ", System.StringComparison.Ordinal) ||
                    paper.name.StartsWith("Workshop Card Effect Paper ", System.StringComparison.Ordinal))
                    paper.enabled = false;
            // This outline belonged to the previous rectangular card rim.
            // Keep live text outlines; only remove the obsolete body outline.
            foreach (Outline outline in card.GetComponents<Outline>()) outline.enabled = false;
            // Authored name/description paper is opaque. Its live labels must
            // render above the whole frame while illustration remains beneath it.
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
            {
                Transform layer = text.transform;
                while (layer.parent != null && layer.parent != card) layer = layer.parent;
                if (layer.parent == card) layer.SetAsLastSibling();
            }
            // The visible corner emblems extend beyond the content rectangle.
            // Keep those parts of a selectable card within the same button.
            Button button = card.GetComponentInParent<Button>();
            if (button != null && button.image != null)
            {
                button.image.color = Color.clear;
                RectTransform target = button.image.rectTransform;
                Vector3[] corners = new Vector3[4];
                frame.GetWorldCorners(corners);
                Vector2 minimum = target.InverseTransformPoint(corners[0]);
                Vector2 maximum = minimum;
                for (int index = 1; index < corners.Length; index++)
                {
                    Vector2 point = target.InverseTransformPoint(corners[index]);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                Rect hit = target.rect;
                button.image.raycastPadding = new Vector4(
                    Mathf.Min(0f, minimum.x - hit.xMin), Mathf.Min(0f, minimum.y - hit.yMin),
                    Mathf.Min(0f, hit.xMax - maximum.x), Mathf.Min(0f, hit.yMax - maximum.y));
                button.GetComponent<BattleCardHoverFeedback>()?.RefreshFrameHitArea();
            }
            return graphic;
        }

        private Text CreateCardSpecialCostIcon(Transform parent, CoreBattleCard data, string name,
            string valueName, CoreBattleState battle = null)
        {
            if (CardSpecialCostPresentation.ResourceType(data).Length == 0) return null;
            Text number = CreateCardCostIcon(name, valueName, parent, CardFaceLayout.SpecialCostIconSize,
                CardSchoolArtCatalog.CostPosition(data, CardSpecialCostPresentation.OrnamentKind(data)),
                CardSpecialCostPresentation.OrnamentKind(data), data.Type, 0);
            number.text = CardSpecialCostPresentation.CostText(data, battle, battle == null ? (int?)null : session.Cargo.Wood);
            return number;
        }

        private RectTransform CreateCardOrnament(string name, Transform parent, Vector2 size,
            Vector2 position, CardOrnamentKind kind, string cardType, string schoolId = null)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            CardOrnamentGraphic graphic = rect.gameObject.AddComponent<CardOrnamentGraphic>();
            graphic.Configure(kind, cardType, schoolId);
            graphic.raycastTarget = false;
            return rect;
        }

        private Text CreateCardCostIcon(string name, string valueName, Transform parent, Vector2 size,
            Vector2 position, CardOrnamentKind kind, string cardType, int value, int fontSize = 24)
        {
            RectTransform icon = CreateRect(name, parent, size, position);
            icon.gameObject.AddComponent<CardOrnamentGraphic>().ConfigureCostLabel(kind, cardType);
            Text number = CreateText(valueName, icon, size - new Vector2(10f, 8f), Vector2.zero,
                fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            // The ornament is the reading surface; do not inherit parchment ink
            // from the enclosing card through ReadableTextColor.
            number.color = Color.white;
            number.raycastTarget = false;
            number.text = value.ToString();
            number.horizontalOverflow = HorizontalWrapMode.Overflow;
            number.verticalOverflow = VerticalWrapMode.Overflow;
            number.resizeTextForBestFit = false;
            Outline outline = number.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.08f, 0.10f, 0.14f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            return number;
        }
    }
}
