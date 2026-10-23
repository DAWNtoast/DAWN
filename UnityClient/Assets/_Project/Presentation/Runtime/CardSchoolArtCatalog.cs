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
            public FrameCostPoint energyPosition, powderPosition, cannonballPosition, specialPosition, supplyPosition;
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

        public static string FrameResourcePath(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "", bool supplyCost = false, string supplyType = "") =>
            "CardSchools/Frames/Integrated/" + FrameId(schoolId, powderCost, cannonballCost, specialType, supplyCost, supplyType);

        private static string FrameId(string schoolId, int powderCost, int cannonballCost, string specialType, bool supplyCost, string supplyType) =>
            "frame-" + NormalizeSchool(schoolId) + "-" + FrameVariant(powderCost, cannonballCost) +
            IntegratedSpecialSuffix(specialType) + (HasBakedSupplyIcon(supplyType)
                ? "-" + supplyType : supplyCost || !string.IsNullOrEmpty(supplyType) ? "-supply" : string.Empty);

        private static bool HasBakedSupplyIcon(string supplyType) => supplyType == "wood" || supplyType == "medicine";

        public static bool HasBakedSupplyIcon(CoreBattleCard card, NavalBattleStage? stage = null) =>
            HasBakedSupplyIcon(CardSupplyCostPresentation.ResourceType(card, stage));

        // Supply emblems are painted into each complete school/resource frame.
        // Untyped legacy callers can still request the shared supply recess.
        private static string IntegratedSpecialSuffix(string specialType) =>
            string.IsNullOrEmpty(specialType) || specialType == "wood"
                ? string.Empty : "-" + specialType;

        public static string FrameResourcePath(CoreBattleCard card, NavalBattleStage? stage = null) =>
            FrameResourcePath(card.SchoolId, card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card),
                supplyType: CardSupplyCostPresentation.ResourceType(card, stage));

        public static Texture2D Frame(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "", bool supplyCost = false, string supplyType = "") =>
            Load(FrameResourcePath(schoolId, powderCost, cannonballCost, specialType, supplyCost, supplyType));

        public static Texture2D Frame(CoreBattleCard card, NavalBattleStage? stage = null) => Load(FrameResourcePath(card, stage));

        public static Rect FrameBounds(string schoolId, int powderCost = 0, int cannonballCost = 0, string specialType = "", bool supplyCost = false, string supplyType = "")
        {
            return FrameLayout(schoolId, powderCost, cannonballCost, specialType, supplyCost, supplyType)?.rect.Bounds ?? CardFaceLayout.DefaultFrameBounds;
        }

        private static FrameLayoutEntry FrameLayout(string schoolId, int powderCost, int cannonballCost, string specialType, bool supplyCost = false, string supplyType = "")
        {
            EnsureFrameLayouts();
            string id = FrameId(schoolId, powderCost, cannonballCost, specialType, supplyCost, supplyType);
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

        public static Rect FrameBounds(CoreBattleCard card, NavalBattleStage? stage = null) => FrameBounds(card.SchoolId,
            card.PowderCost, card.CannonballCost, CardSpecialCostPresentation.ResourceType(card),
            supplyType: CardSupplyCostPresentation.ResourceType(card, stage));

        public static Vector2 SupplyPosition(CoreBattleCard card, NavalBattleStage? stage = null)
        {
            FrameLayoutEntry layout = FrameLayout(card.SchoolId, card.PowderCost, card.CannonballCost,
                CardSpecialCostPresentation.ResourceType(card), supplyType: CardSupplyCostPresentation.ResourceType(card, stage));
            return CostCorner(layout?.supplyPosition, new Vector2(101f, 108f));
        }

        public static Vector2 CostPosition(CoreBattleCard card, CardOrnamentKind kind, NavalBattleStage? stage = null)
        {
            FrameLayoutEntry layout = FrameLayout(card.SchoolId, card.PowderCost, card.CannonballCost,
                CardSpecialCostPresentation.ResourceType(card), supplyType: CardSupplyCostPresentation.ResourceType(card, stage));
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
            return "CardArt/Western/" + IllustrationIdentity(card);
        }

        private static string IllustrationIdentity(CoreBattleCard card) =>
            card.BoardingSpec != null && !card.Id.StartsWith("card_special_", StringComparison.Ordinal) &&
            CoreBoardingCardCatalog.IsLegacyAlias(card)
                ? "card_boarding_" + card.BoardingSpec.Key : card.Id;

        // Presentation-only corrections preserve ArtId: source-card rules use its
        // historical suffix, and existing save identities must remain unchanged.
        private static string ReviewedIllustrationPath(string cardId)
        {
            switch (cardId)
            {
                case "card_boarding_basic_01": return "CardArt/Western/card_crew_thrust";
                case "card_boarding_basic_04":
                case "card_boarding_basic_05":
                case "card_boarding_basic_12": return "CardArt/Western/card_crew_guard";
                case "card_boarding_basic_06": return "CardArt/Western/card_crew_bandage";
                case "card_boarding_basic_07": return "CardArt/Western/card_doctor_sutures";
                case "card_boarding_basic_08": return "CardArt/Western/card_tactical_draw";
                case "card_boarding_basic_09": return "CardArt/Western/card_energy_order";
                case "card_boarding_basic_10": return "CardArt/Western/card_crew_rally";
                case "card_boarding_fire_06":
                case "card_boarding_fire_11": return "CardSchools/Art/card_school_fire_b08";
                case "card_boarding_arcane_07": return "CardSchools/Art/card_school_arcane_b03";
                case "card_boarding_arcane_13": return "CardSchools/Art/card_school_arcane_b09";
                case "card_boarding_arcane_15": return "CardSchools/Art/card_school_arcane_e05";
                case "card_school_fire_n04": return "CardSchools/Art/card_school_fire_b02";
                case "card_school_fire_n05": return "CardSchools/Art/card_school_fire_e02";
                case "card_special_plating_moonsilver_skin": return "CardSchools/Art/card_school_arcane_b05";
                case "card_boarding_tide_03":
                case "card_boarding_tide_15": return "CardSchools/Art/card_school_tide_b08";
                case "card_boarding_tide_04": return "CardSchools/Art/card_school_tide_b07";
                case "card_boarding_tide_08":
                case "card_boarding_tide_09": return "CardSchools/Art/card_school_tide_b10";
                case "card_boarding_thunder_01": return "CardSchools/Art/card_school_thunder_b08";
                case "card_boarding_thunder_03":
                case "card_boarding_thunder_05": return "CardSchools/Art/card_school_thunder_b07";
                case "card_boarding_thunder_04":
                case "card_boarding_thunder_14":
                case "card_boarding_thunder_15": return "CardSchools/Art/card_school_thunder_b10";
                case "card_boarding_blood_10": return "CardSchools/Art/card_school_blood_b07";
                case "card_boarding_blood_12": return "CardSchools/Art/card_school_blood_e04";
                case "card_boarding_blood_13": return "CardSchools/Art/card_school_blood_b10";
                case "card_school_tide_n03": return "CardSchools/Art/card_school_tide_b01";
                case "card_school_tide_n06": return "CardSchools/Art/card_school_tide_l01";
                case "card_school_thunder_n03": return "CardSchools/Art/card_school_thunder_b01";
                case "card_school_thunder_n05": return "CardSchools/Art/card_school_thunder_c02";
                case "card_school_thunder_n06": return "CardSchools/Art/card_school_thunder_b06";
                case "card_special_plating_fogiron_carapace": return "CardSchools/Art/card_school_blood_b05";
                case "card_special_instrument_allseeing_sextant": return "CardSchools/Art/card_school_blood_b01";
                default: return null;
            }
        }

        public static string DedicatedIllustrationPath(CoreBattleCard card)
        {
            string western = WesternIllustrationPath(card);
            if (western != null && Load(western) != null) return western;
            if (card == null) return null;
            string identity = IllustrationIdentity(card);
            string reviewed = ReviewedIllustrationPath(identity);
            if (reviewed != null && Load(reviewed) != null) return reviewed;
            CoreBattleCard fallbackCard = identity != card.Id ? CoreBoardingCardCatalog.Find(identity) ?? card : card;
            // Keep the former complete-image fallbacks available for older packages.
            string reusedId = card.Id == "card_energy_order" ? "card_brace" :
                card.Id == "card_tactical_draw" ? "card_full_sail_turn" :
                fallbackCard.BoardingSpec != null && !HasSchoolIllustration(fallbackCard) ? fallbackCard.ArtId : null;
            string reused = string.IsNullOrEmpty(reusedId) ? null : "CardArt/Western/" + reusedId;
            if (reused != null && Load(reused) != null) return reused;
            return HasSchoolIllustration(fallbackCard) ? "CardSchools/Art/" + fallbackCard.ArtId : western;
        }

        public static bool HasDedicatedArt(CoreBattleCard card) =>
            card != null && IndividualIllustration(card) != null;

        private static Texture2D IndividualIllustration(CoreBattleCard card)
        {
            if (card == null) return null;
            string path = DedicatedIllustrationPath(card);
            return path == null ? null : Load(path);
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
