using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>School identity is authored data, independent of a card's mechanical type.</summary>
    public static class CardSchoolArtCatalog
    {
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        [Serializable] private sealed class FrameLayoutFile { public FrameLayoutEntry[] entries; }
        [Serializable] private sealed class FrameLayoutRectangle
        {
            public float x, y, width, height;
            public Rect Bounds => new Rect(x, y, width, height);
        }
        [Serializable] private sealed class FrameCostPoint
        {
            public float x, y;
            public Vector2 Position => new Vector2(x, y);
        }
        [Serializable] private sealed class FrameLayoutEntry
        {
            public string id;
            public FrameLayoutRectangle rect;
            public FrameCostPoint energyPosition, powderPosition, cannonballPosition, specialPosition;
            public FrameLayoutRectangle illustrationRect;
            public FrameCostPoint namePlatePosition;
            public CardRibbonTitleLayout titleCurve;
        }
        private static readonly Dictionary<string, FrameLayoutEntry> FrameLayouts = new Dictionary<string, FrameLayoutEntry>(StringComparer.Ordinal);
        private static bool frameLayoutsLoaded;
        public static readonly string[] SchoolIds = { "fire", "arcane", "curse", "tide", "thunder", "blood" };

        public static string NormalizeSchool(string schoolId)
        {
            foreach (string id in SchoolIds)
                if (string.Equals(schoolId, id, StringComparison.Ordinal)) return id;
            return "neutral";
        }

        public static string FrameVariant(int powderCost, int cannonballCost) =>
            powderCost > 0 ? (cannonballCost > 0 ? "both" : "powder") : (cannonballCost > 0 ? "cannonball" : "ap");

        public static string FrameResourcePath(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "") =>
            "CardSchools/Frames/Integrated/frame-" + NormalizeSchool(schoolId) + "-" + FrameVariant(powderCost, cannonballCost) +
            IntegratedSpecialSuffix(specialType);

        // New supply symbols are drawn at runtime over the existing frame.
        private static string IntegratedSpecialSuffix(string specialType) =>
            string.IsNullOrEmpty(specialType) || specialType == "wood" || specialType == "tide"
                ? string.Empty : "-" + specialType;

        public static string FrameResourcePath(CoreBattleCard card) =>
            FrameResourcePath(card.SchoolId, card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card));

        public static Texture2D Frame(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "") =>
            Load(FrameResourcePath(schoolId, powderCost, cannonballCost, specialType));

        public static Texture2D Frame(CoreBattleCard card) => Load(FrameResourcePath(card));

        public static Rect FrameBounds(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "")
        {
            return FrameLayout(schoolId, powderCost, cannonballCost, specialType)?.rect.Bounds ?? CardFaceLayout.DefaultFrameBounds;
        }

        private static FrameLayoutEntry FrameLayout(string schoolId, int powderCost, int cannonballCost, string specialType)
        {
            EnsureFrameLayouts();
            string id = "frame-" + NormalizeSchool(schoolId) + "-" + FrameVariant(powderCost, cannonballCost) +
                IntegratedSpecialSuffix(specialType);
            return FrameLayouts.TryGetValue(id, out FrameLayoutEntry layoutEntry) ? layoutEntry : null;
        }

        private static void EnsureFrameLayouts()
        {
            if (!frameLayoutsLoaded)
            {
                frameLayoutsLoaded = true;
                TextAsset file = Resources.Load<TextAsset>("CardSchools/Frames/Integrated/frame-layouts");
                if (file != null)
                {
                    FrameLayoutFile layout = JsonUtility.FromJson<FrameLayoutFile>(file.text);
                    if (layout?.entries != null)
                        foreach (FrameLayoutEntry entry in layout.entries)
                            if (!string.IsNullOrEmpty(entry.id) && entry.rect != null && entry.rect.width > 0f && entry.rect.height > 0f)
                                FrameLayouts[entry.id] = entry;
                }
            }
        }

        public static Rect IllustrationBoundsForFrame(string frameId)
        {
            EnsureFrameLayouts();
            return !string.IsNullOrEmpty(frameId) && FrameLayouts.TryGetValue(frameId, out FrameLayoutEntry entry) &&
                entry.illustrationRect != null && entry.illustrationRect.width > 0f && entry.illustrationRect.height > 0f
                ? entry.illustrationRect.Bounds : CardFaceLayout.IllustrationBounds;
        }

        public static Vector2 NamePlatePositionForFrame(string frameId)
        {
            EnsureFrameLayouts();
            return !string.IsNullOrEmpty(frameId) && FrameLayouts.TryGetValue(frameId, out FrameLayoutEntry entry)
                ? CostCorner(entry.namePlatePosition, CardFaceLayout.NamePlatePosition) : CardFaceLayout.NamePlatePosition;
        }

        public static CardRibbonTitleLayout TitleLayoutForFrame(string frameId)
        {
            EnsureFrameLayouts();
            return !string.IsNullOrEmpty(frameId) && FrameLayouts.TryGetValue(frameId, out FrameLayoutEntry entry) &&
                entry.titleCurve != null && entry.titleCurve.halfWidth > 0f && entry.titleCurve.halfThickness > 0f
                ? entry.titleCurve : CardRibbonTitleLayout.Default;
        }

        public static Rect FrameBounds(CoreBattleCard card) => FrameBounds(card.SchoolId,
            card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card));

        public static Vector2 CostPosition(CoreBattleCard card, CardOrnamentKind kind)
        {
            FrameLayoutEntry layout = FrameLayout(card.SchoolId, card.PowderCost, card.CannonballCost,
                CardSpecialCostPresentation.ResourceType(card));
            switch (kind)
            {
                case CardOrnamentKind.Energy: return CostCorner(layout?.energyPosition, CardFaceLayout.EnergyIconPosition);
                case CardOrnamentKind.Gunpowder: return CostCorner(layout?.powderPosition, CardFaceLayout.PowderIconPosition);
                case CardOrnamentKind.Cannonball: return CostCorner(layout?.cannonballPosition, CardFaceLayout.CannonballIconPosition);
                default: return CostCorner(layout?.specialPosition, CardFaceLayout.SpecialCostIconPosition);
            }
        }

        private static Vector2 CostCorner(FrameCostPoint point, Vector2 fallback)
        {
            // JsonUtility can instantiate a missing nested object with zeroed
            // fields. The card center is never a valid corner calibration.
            return point == null || point.Position == Vector2.zero ? fallback : point.Position;
        }

        public static bool HasSchoolIllustration(CoreBattleCard card) => card != null &&
            !string.IsNullOrEmpty(card.ArtId) && card.ArtId.StartsWith("card_school_", StringComparison.Ordinal);

        public static string WesternIllustrationPath(CoreBattleCard card)
        {
            if (card == null || string.IsNullOrEmpty(card.Id)) return null;
            // Boarding additions deliberately reuse the complete existing image.
            // Legacy cards retain their per-card Western replacement first.
            if (card.BoardingSpec != null && card.Id.StartsWith("card_boarding_", StringComparison.Ordinal) &&
                !HasSchoolIllustration(card) && !string.IsNullOrEmpty(card.ArtId))
                return "CardArt/Western/" + card.ArtId;
            // New starter utilities deliberately reuse complete existing art.
            // Keep their identities distinct and avoid falling back to an atlas crop.
            string illustration = card.Id == "card_energy_order" ? "card_brace" :
                card.Id == "card_tactical_draw" ? "card_full_sail_turn" : card.Id;
            return "CardArt/Western/" + illustration;
        }

        public static string DedicatedIllustrationPath(CoreBattleCard card)
        {
            string western = WesternIllustrationPath(card);
            if (western != null && Load(western) != null) return western;
            return HasSchoolIllustration(card) ? "CardSchools/Art/" + card.ArtId : western;
        }

        public static bool HasDedicatedArt(CoreBattleCard card) =>
            card != null && IndividualIllustration(card) != null;

        private static Texture2D IndividualIllustration(CoreBattleCard card)
        {
            if (card == null) return null;
            string western = WesternIllustrationPath(card);
            Texture2D replacement = western == null ? null : Load(western);
            if (replacement != null) return replacement;
            return HasSchoolIllustration(card) ? Load("CardSchools/Art/" + card.ArtId) : null;
        }

        public static Texture2D Illustration(CoreBattleCard card, LegacyDemoArtCatalog legacy)
        {
            // CardId replacements separate old cards that shared one ArtId. Keep the
            // atlas fallback for compatible older resources, but expose it to audits.
            Texture2D individual = IndividualIllustration(card);
            if (individual != null || HasSchoolIllustration(card)) return individual;
            return card == null ? null : legacy?.CardSheet;
        }

        public static Rect IllustrationUv(CoreBattleCard card, LegacyDemoArtCatalog legacy)
        {
            Texture2D texture = IndividualIllustration(card);
            if (texture == null)
                return card != null && !HasSchoolIllustration(card) && legacy != null
                    ? legacy.CardUv(card.ArtId) : new Rect(0f, 0f, 1f, 1f);
            // Each illustration is the complete authored composition. Aspect
            // fitting belongs to its display rectangle, never to cropped UVs.
            return new Rect(0f, 0f, 1f, 1f);
        }

        public static BattleCardPalette Palette(string schoolId)
        {
            switch (NormalizeSchool(schoolId))
            {
                case "fire": return Make("#653520", "#E9A240", "#F0DCB8", "#352316", "#763718");
                case "arcane": return Make("#433456", "#BBA1E8", "#E7DCEF", "#2B223B", "#554073");
                case "curse": return Make("#384635", "#B2C37A", "#DFE2C8", "#273122", "#3B5432");
                case "tide": return Make("#24565C", "#87CFCB", "#D7E9E0", "#1F3535", "#275D63");
                case "thunder": return Make("#354665", "#E8D57C", "#E8E5CE", "#273244", "#384B71");
                case "blood": return Make("#592B35", "#D8938F", "#EBD9D0", "#3B252A", "#743849");
                default: return Make("#795236", "#CEAA73", "#EADDC3", "#382B20", "#65472F");
            }
        }

        public static void ReleaseLoadedTextures()
        {
            Textures.Clear();
            FrameLayouts.Clear();
            frameLayoutsLoaded = false;
        }

        private static Texture2D Load(string path)
        {
            if (Textures.TryGetValue(path, out Texture2D texture)) return texture;
            texture = Resources.Load<Texture2D>(path);
            Textures.Add(path, texture);
            return texture;
        }

        private static BattleCardPalette Make(string surface, string accent, string parchment, string ink, string title)
        {
            Color Parse(string value) { ColorUtility.TryParseHtmlString(value, out Color result); return result; }
            return new BattleCardPalette(BattleCardSurfaceMaterial.Timber, Parse(surface), Parse(accent), Parse(parchment), Parse(ink), Parse(title));
        }
    }
}
