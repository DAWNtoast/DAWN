using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>One card layout. Content changes font size, never its illustration or regions.</summary>
    public static class CardFaceLayout
    {
        public static readonly Vector2 CardSize = new Vector2(206f, 370f);
        // Visual backgrounds bleed underneath the authored frame; text keeps
        // its narrower reading region and never inherits this extra width.
        public static readonly Vector2 ArtSize = new Vector2(206f, 252f);
        public static readonly Vector2 ArtPosition = new Vector2(0f, 59.2202f);
        // The complete square source extends underneath the authored frame.
        // Natural edge coverage removes gaps without cropping or stretching UVs.
        public static readonly Rect IllustrationBounds = new Rect(-109.14093f, -44f, 216f, 216f);
        public static readonly Vector2 NamePlateSize = new Vector2(206f, 34f);
        public static readonly Vector2 NamePlatePosition = new Vector2(0f, -54.7798f);
        public static readonly Vector2 NameSize = new Vector2(174f, 26f);
        public static readonly Vector2 NamePosition = Vector2.zero;
        public static readonly Vector2 EffectSize = new Vector2(176f, 94f);
        public static readonly Vector2 EffectPosition = new Vector2(0f, -118.7798f);
        public static readonly Vector2 EffectPaperSize = new Vector2(206f, 124f);
        public static readonly Vector2 EffectPaperPosition = new Vector2(0f, -120.7798f);
        // The authored frame includes the three corner emblems. These rectangles
        // position only their live numbers; card art and click targets stay fixed.
        public static readonly Vector2 FrameSize = new Vector2(280f, 480f);
        public static readonly Rect VisualBounds = new Rect(-140f, -240f, 280f, 480f);
        public static readonly Rect DefaultFrameBounds = new Rect(-137f, -236.7202f, 272f, 459.4404f);
        public static readonly Vector2 HandSize = new Vector2(1120f, 340f);
        public static readonly Vector2 HandPosition = new Vector2(0f, -270f);
        public const float CollectionScale = .9f;
        public const float DeckPreviewScale = 1f;
        public const float WorkshopCollectionScale = .535f;
        public static readonly Vector2 EnergyIconSize = new Vector2(52f, 52f);
        public static readonly Vector2 EnergyIconPosition = new Vector2(-102.75f, 177.9f);
        public static readonly Vector2 PowderIconSize = new Vector2(40f, 40f);
        public static readonly Vector2 PowderIconPosition = new Vector2(102.59f, -186f);
        public static readonly Vector2 CannonballIconSize = new Vector2(40f, 40f);
        public static readonly Vector2 CannonballIconPosition = new Vector2(-102.89f, -185.15f);
        public static readonly Vector2 SpecialCostIconSize = new Vector2(52f, 52f);
        public static readonly Vector2 SpecialCostIconPosition = new Vector2(101.46f, 177.9f);
        public static readonly Vector2 RepeatCostPosition = new Vector2(-40f, 195f);
        public const string ReferenceName = "领航员 · 抢占航线+3";
        public const int EffectFontSize = 16;
        private static string referenceEffect;
        private static readonly Regex UsageConditions = new Regex(
            @"<b>[\s\S]*?</b>|(?:平行且)?距离不超过[ \u00a0]*[0-9]+(?:\.[0-9]+)?[ \u00a0]*米" +
            @"|[0-9]+(?:\.[0-9]+)?[ \u00a0]*米(?:以内|以外|内|外)" +
            @"|须(?:再次推进|推进后)才能冲撞|再次推进后(?=可冲撞)", RegexOptions.CultureInvariant);

        public static string DisplayName(CoreBattleCard card) => card.Name +
            (card.UpgradeLevel > 0 ? "+" + card.UpgradeLevel : string.Empty);

        public static string ReferenceEffect
        {
            get
            {
                if (referenceEffect != null) return referenceEffect;
                for (int i = 0; i < CoreVoyageSession.CardLibrary.Count; i++)
                    if (CoreVoyageSession.CardLibrary[i].Id == "card_full_sail_turn")
                    {
                        CoreBattleCard card = new CoreVoyageSession().GetBattleCardAtLevel(i, 3);
                        referenceEffect = FormatDescription(card.Description);
                        return referenceEffect;
                    }
                throw new System.InvalidOperationException("Missing typography reference card: card_full_sail_turn");
            }
        }

        public static void FitName(Text text)
        {
            if (text == null) return;
            text.font = GameTypography.InterfaceFont;
            text.fontStyle = FontStyle.Normal;
            CardRibbonTitleEffect ribbon = text.GetComponent<CardRibbonTitleEffect>();
            if (ribbon == null) ribbon = text.gameObject.AddComponent<CardRibbonTitleEffect>();
            CardSchoolFrameGraphic frame = text.transform.parent?.parent?.GetComponentInChildren<CardSchoolFrameGraphic>();
            ribbon.ConfigureForFrame(frame != null && frame.HasAuthoredTexture ? frame.mainTexture.name : null);
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1f;
            int maximum = 32;
            while (maximum > 1 && !ribbon.GeneratedTextFits(text, ReferenceName, maximum)) maximum--;
            text.fontSize = maximum;
            while (text.fontSize > 1 && !ribbon.GeneratedTextFits(text)) text.fontSize--;
            text.resizeTextMinSize = text.fontSize;
            text.resizeTextMaxSize = maximum;
        }
        public static void FitEffect(Text text)
        {
            if (text == null || text.font == null) return;
            string source = CardDescriptionText.SourceFor(text);
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            for (int size = EffectFontSize; size >= 1; size--)
            {
                text.fontSize = size;
                text.text = CardDescriptionText.Wrap(text, source, size);
                if (Fits(text, text.text, size, false) && text.preferredHeight <= text.rectTransform.rect.height) break;
            }
            text.resizeTextMinSize = text.fontSize;
            text.resizeTextMaxSize = EffectFontSize;
            CardDescriptionText.RememberLayout(text, source);
        }

        public static string FormatDescription(string value)
        {
            string description = CardDescriptionText.Compact(value);
            // Emphasize prerequisites, not movement amounts or resulting damage.
            // Existing bold spans are preserved so formatting remains idempotent.
            return UsageConditions.Replace(description, match => match.Value.StartsWith("<b>", System.StringComparison.Ordinal)
                ? match.Value : "<b>" + match.Value + "</b>");
        }

        private static bool Fits(Text text, string content, int size, bool singleLine)
        {
            // Measure in the unscaled reference card so a collection thumbnail
            // uses precisely the same typography as its full-size battle card.
            Vector2 bounds = text.rectTransform.rect.size;
            TextGenerationSettings settings = text.GetGenerationSettings(bounds);
            settings.scaleFactor = 1f;
            settings.fontSize = size;
            settings.resizeTextForBestFit = false;
            settings.horizontalOverflow = singleLine ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.lineSpacing = 1f;
            TextGenerator generator = text.cachedTextGeneratorForLayout;
            float height = generator.GetPreferredHeight(content, settings);
            if (height > bounds.y) return false;
            return !singleLine || generator.GetPreferredWidth(content, settings) <= bounds.x;
        }

        public static Vector2 IllustrationSize(Texture texture, Rect uv)
            => IllustrationSize(texture, uv, IllustrationBounds);

        public static Vector2 IllustrationSize(Texture texture, Rect uv, Rect target)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 || uv.width <= 0f || uv.height <= 0f)
                return target.size;
            Vector2 source = new Vector2(texture.width * uv.width, texture.height * uv.height);
            float scale = Mathf.Min(target.width / source.x, target.height / source.y);
            return source * scale;
        }

        public static void FitIllustration(RawImage image)
        {
            if (image == null) return;
            Rect target = IllustrationLayout(image);
            image.rectTransform.sizeDelta = IllustrationSize(image.texture, image.uvRect, target);
            image.rectTransform.anchoredPosition = target.center;
        }

        public static Rect IllustrationLayout(RawImage image)
        {
            CardSchoolFrameGraphic frame = image != null && image.transform.parent != null
                ? image.transform.parent.GetComponentInChildren<CardSchoolFrameGraphic>() : null;
            return frame != null && frame.HasAuthoredTexture
                ? CardSchoolArtCatalog.IllustrationBoundsForFrame(frame.mainTexture.name)
                : IllustrationBounds;
        }

        public static Vector2 NamePlateLayout(RectTransform namePlate)
        {
            CardSchoolFrameGraphic frame = namePlate != null && namePlate.parent != null
                ? namePlate.parent.GetComponentInChildren<CardSchoolFrameGraphic>() : null;
            return frame != null && frame.HasAuthoredTexture
                ? CardSchoolArtCatalog.NamePlatePositionForFrame(frame.mainTexture.name) : NamePlatePosition;
        }

        public static Rect IllustrationUv(Rect source) => source;
    }
}
