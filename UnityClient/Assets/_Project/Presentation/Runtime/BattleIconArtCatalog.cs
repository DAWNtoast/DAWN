using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Authored combat symbols shared by the HUD, statuses, intents and header.</summary>
    public static class BattleIconArtCatalog
    {
        public const string ResourcePath = "BattleIcons/battle_icons_v1";
        public const int AtlasWidth = 2048;
        public const int AtlasHeight = 1536;
        private static Texture2D texture;

        /// <summary>Retire the shared lookup on returning to the menu, without unloading live UI art.</summary>
        public static void ReleaseLoadedTextures() => texture = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();

        // Source order is fixed by outputs/art-redraw-20261024/icons/manifest.json.
        // Each cell retains the complete 256px transparent canvas, including padding.
        private static readonly string[] ids =
        {
            "energy", "action_point", "mana", "tide", "thunder_chain", "curse_mark", "blood", "powder",
            "cannonball", "planks", "medicine", "gold", "food", "draw_pile", "discard_pile", "supplies",
            "cannon", "melee", "ram", "guard", "repair", "empower", "curse", "move",
            "wait", "fire", "load", "morale", "armor", "attack", "healing", "weakness",
            "arcane", "thunder", "anchor", "slow", "immunity", "seal", "draw", "settings", "deck"
        };
        public static IReadOnlyList<string> AssetIds { get; } = Array.AsReadOnly(ids);
        public static Texture2D Texture => texture != null ? texture : (texture = Resources.Load<Texture2D>(ResourcePath));

        // The unused last atlas cell contains a small white utility swatch. Existing
        // source/trigger marks sample it, so they stay crisp and retain their colors.
        public static Vector2 SolidUv => Texture != null
            ? new Vector2(2032.5f / AtlasWidth, 15.5f / AtlasHeight) : Vector2.zero;

        public static bool TryGetUv(string assetId, out Rect uv)
        {
            int cell = Array.IndexOf(ids, assetId);
            if (cell < 0) { uv = default; return false; }
            uv = new Rect((cell % 8) / 8f, 1f - (cell / 8 + 1) / 6f, 1f / 8f, 1f / 6f);
            return true;
        }

        public static string CommandAssetId(BattleCommandIconKind kind)
        {
            switch (kind)
            {
                case BattleCommandIconKind.Cannon: return "cannon";
                case BattleCommandIconKind.Melee: return "melee";
                case BattleCommandIconKind.Ram: return "ram";
                case BattleCommandIconKind.Guard: return "guard";
                case BattleCommandIconKind.Immunity: return "immunity";
                case BattleCommandIconKind.Repair: return "repair";
                case BattleCommandIconKind.Empower: return "empower";
                case BattleCommandIconKind.Curse: return "curse";
                case BattleCommandIconKind.Move: return "move";
                case BattleCommandIconKind.Wait: return "wait";
                case BattleCommandIconKind.Fire: return "fire";
                case BattleCommandIconKind.Energy: return "energy";
                case BattleCommandIconKind.ActionPoint: return "action_point";
                case BattleCommandIconKind.Mana: return "mana";
                case BattleCommandIconKind.TidePower: return "tide";
                case BattleCommandIconKind.ThunderChain: return "thunder_chain";
                case BattleCommandIconKind.CurseMark: return "curse_mark";
                case BattleCommandIconKind.Blood: return "blood";
                case BattleCommandIconKind.Powder: return "powder";
                case BattleCommandIconKind.Cannonball: return "cannonball";
                case BattleCommandIconKind.Planks: return "planks";
                case BattleCommandIconKind.Medicine: return "medicine";
                case BattleCommandIconKind.Gold: return "gold";
                case BattleCommandIconKind.Food: return "food";
                case BattleCommandIconKind.DrawPile: return "draw_pile";
                case BattleCommandIconKind.DiscardPile: return "discard_pile";
                case BattleCommandIconKind.Load: return "load";
                case BattleCommandIconKind.Morale: return "morale";
                case BattleCommandIconKind.Supplies: return "supplies";
                default: return null;
            }
        }

        public static string StatusAssetId(CoreBattleStatusKind kind)
        {
            switch (kind)
            {
                case CoreBattleStatusKind.Guard: return "guard";
                case CoreBattleStatusKind.Armor: return "armor";
                case CoreBattleStatusKind.Attack: return "attack";
                case CoreBattleStatusKind.Fire: return "fire";
                case CoreBattleStatusKind.Retaliation: return "melee";
                case CoreBattleStatusKind.Healing: return "healing";
                case CoreBattleStatusKind.Curse: return "curse";
                case CoreBattleStatusKind.Weakness: return "weakness";
                case CoreBattleStatusKind.Tide: return "tide";
                case CoreBattleStatusKind.Arcane: return "arcane";
                case CoreBattleStatusKind.Thunder: return "thunder";
                case CoreBattleStatusKind.Blood: return "blood";
                case CoreBattleStatusKind.Delayed: return "wait";
                case CoreBattleStatusKind.Anchor: return "anchor";
                case CoreBattleStatusKind.Slow: return "slow";
                case CoreBattleStatusKind.Immunity: return "immunity";
                case CoreBattleStatusKind.Seal: return "seal";
                case CoreBattleStatusKind.Draw: return "draw";
                default: return null;
            }
        }

        public static string HeaderAssetId(bool settings) => settings ? "settings" : "deck";

        internal static bool Draw(VertexHelper mesh, Rect bounds, string assetId, Color tint)
        {
            if (Texture == null || !TryGetUv(assetId, out Rect uv)) return false;
            float side = Mathf.Min(bounds.width, bounds.height);
            if (side <= 0f) return true;
            Vector2 min = bounds.center - Vector2.one * (side * .5f);
            Vector2 max = bounds.center + Vector2.one * (side * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(min.x, min.y), tint, new Vector2(uv.xMin, uv.yMin));
            mesh.AddVert(new Vector3(min.x, max.y), tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(new Vector3(max.x, max.y), tint, new Vector2(uv.xMax, uv.yMax));
            mesh.AddVert(new Vector3(max.x, min.y), tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
            return true;
        }
    }
}
