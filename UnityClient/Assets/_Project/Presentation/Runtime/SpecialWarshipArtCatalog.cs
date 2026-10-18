using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Complete transparent ship paintings, independent of the player ship atlases.</summary>
    public static class SpecialWarshipArtCatalog
    {
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Vector2> contacts = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> widths = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly HashSet<string> textureNames = BuildTextureNames();
        // Persistent art calibration: x/y are the full-image keel contact; z
        // is the water-effect width relative to the complete transparent frame.
        // Update these twelve rows after inspecting the final exported paintings.
        private static readonly Dictionary<string, Vector3> authoredWaterlines = new Dictionary<string, Vector3>(StringComparer.Ordinal)
        {
            ["boss_fire"] = new Vector3(0.518555f, 0.127930f, 0.385417f),
            ["boss_tide"] = new Vector3(0.516927f, 0.171875f, 0.332682f),
            ["boss_blood"] = new Vector3(0.567708f, 0.133789f, 0.514974f),
            ["boss_thunder"] = new Vector3(0.527018f, 0.141602f, 0.412760f),
            ["boss_curse"] = new Vector3(0.496094f, 0.126953f, 0.356120f),
            ["boss_arcane"] = new Vector3(0.619792f, 0.149414f, 0.516276f),
            ["legendary_fire"] = new Vector3(0.500326f, 0.131836f, 0.436198f),
            ["legendary_tide"] = new Vector3(0.525391f, 0.136719f, 0.374349f),
            ["legendary_blood"] = new Vector3(0.491211f, 0.139648f, 0.302083f),
            ["legendary_thunder"] = new Vector3(0.530273f, 0.129883f, 0.433594f),
            ["legendary_curse"] = new Vector3(0.486003f, 0.131836f, 0.531250f),
            ["legendary_arcane"] = new Vector3(0.479167f, 0.136719f, 0.470703f),
        };
        public static Rect FullUv => new Rect(0f, 0f, 1f, 1f);

        public static string ResourcePath(string encounterId) => CoreRouteBossCatalog.Find(encounterId)?.ArtResourcePath ??
            CoreLegendaryWarships.Find(encounterId)?.ArtResourcePath ?? string.Empty;

        public static bool TryLoad(string encounterId, out Texture2D texture, out Rect uv)
        {
            uv = FullUv;
            string path = ResourcePath(encounterId);
            texture = null;
            if (path.Length == 0) return false;
            if (!textures.TryGetValue(path, out texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>(path);
                // Do not retain a missing import: editor art can arrive while this view is open.
                if (texture == null) return false;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                textures[path] = texture;
            }
            return true;
        }

        public static bool IsSpecialTexture(Texture texture) => TextureKey(texture).Length > 0;

        private static string TextureKey(Texture texture)
        {
            if (texture == null) return string.Empty;
            string name = texture.name;
            return textureNames.Contains(name) ? name : string.Empty;
        }

        private static HashSet<string> BuildTextureNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreSchoolUnlockDefinition school in CoreSchoolUnlockCatalog.All)
            { names.Add("boss_" + school.Id); names.Add("legendary_" + school.Id); }
            return names;
        }

        /// <summary>Art QA can set each full-image water contact and visible hull-width ratio.
        /// Coordinates use Unity's bottom-left normalized texture convention.</summary>
        public static void ConfigureWaterline(string encounterId, Vector2 contactUv, float hullWidthRatio)
        {
            string path = ResourcePath(encounterId);
            if (path.Length == 0) return;
            string key = path.Substring(path.LastIndexOf('/') + 1);
            contacts[key] = new Vector2(Mathf.Clamp01(contactUv.x), Mathf.Clamp01(contactUv.y));
            widths[key] = Mathf.Clamp(hullWidthRatio, .1f, 1f);
        }

        public static void ClearWaterlineOverrides() { contacts.Clear(); widths.Clear(); }

        public static Vector2 HullContactLocal(RawImage image)
        {
            string key = TextureKey(image == null ? null : image.texture);
            if (key.Length == 0) return ShipWaterlineLayout.HullContactLocal(image);
            Vector3 defaults = authoredWaterlines[key];
            Vector2 point = contacts.TryGetValue(key, out Vector2 authored) ? authored : new Vector2(defaults.x, defaults.y);
            Rect uv = image.uvRect;
            return new Vector2(image.rectTransform.rect.width * ((point.x - uv.x) / uv.width - image.rectTransform.pivot.x),
                image.rectTransform.rect.height * ((point.y - uv.y) / uv.height - image.rectTransform.pivot.y));
        }

        public static float HullWidthRatio(Texture texture)
        {
            string key = TextureKey(texture);
            return widths.TryGetValue(key, out float authored) ? authored : key.Length > 0 ? authoredWaterlines[key].z : .93f;
        }

        public static float HullWidthLocal(RawImage image)
        {
            if (image == null) return 0f;
            if (!IsSpecialTexture(image.texture)) return ShipWaterlineLayout.HullWidthLocal(image);
            return image.rectTransform.rect.width * HullWidthRatio(image.texture) /
                Mathf.Max(.00001f, Mathf.Abs(image.uvRect.width));
        }

        public static string RouteCandidatesLabel(CoreRouteDefinition route)
        {
            if (route == null) return string.Empty;
            IReadOnlyList<CoreRouteBoss> bosses = CoreRouteBossCatalog.ForRoute(route.Id);
            return bosses.Count == 2 ? bosses[0].Name + " / " + bosses[1].Name + "（随机一位）" : route.BossName;
        }

        public static void ReleaseLoadedTextures() => textures.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        { textures.Clear(); contacts.Clear(); widths.Clear(); }
    }
}
