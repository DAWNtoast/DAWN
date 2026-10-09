using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Shared material fills; authored illustrations, scene hit targets and dimmers stay untouched.</summary>
    public static class NauticalSurfaceSkin
    {
        public const string CanvasPath = "UISurfaces/nautical_canvas_v1";
        public const string PaperPath = "UISurfaces/journal_parchment_v1";
        private static Sprite canvas;
        private static Sprite paper;
        private static Sprite timber;
        public static Sprite Canvas => Load(ref canvas, CanvasPath);
        public static Sprite Paper => Load(ref paper, PaperPath);

        static NauticalSurfaceSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static bool IsAtmosphere(string name)
        {
            return name.Contains("Overlay") || name.Contains("Tint") || name.Contains("Shade") ||
                   name.Contains("Shadow") || name.Contains("Contact") || name.Contains("Glow") ||
                   name == "Settings Panel" || name == "Achievements Panel" || name == "New Game Confirmation";
        }

        public static bool ApplyPanel(Image image, Color original)
        {
            if (image == null || original.a <= 0.01f || IsAtmosphere(image.name)) return false;
            string name = image.name;
            bool light = original.r + original.g + original.b > 1.65f;
            bool wood = !light && (original.r > original.b * 1.3f || name.Contains("Header") ||
                name.Contains("Ledger") || name.Contains("Plaque") || name.Contains("Settlement") ||
                name.Contains("Footer") || name.Contains("Unified Art Frame") ||
                name == "Left Menu Veil" || name == "Active Dispatch Job");
            // Information panels that used the dark blue nautical canvas now
            // use the same journal parchment as the readable dossier surfaces.
            // Keep wood and light authored surfaces unchanged; scene overlays
            // are filtered above by IsAtmosphere().
            Sprite face = light ? Paper : wood ? Timber : Paper;
            if (face == null) return false;
            image.sprite = face;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = face.rect.width / (wood ? 620f : 340f);
            // Reading surfaces use light parchment paired with dark ink.
            image.color = light ? new Color(1f, 1f, 1f, original.a) :
                wood ? new Color(0.92f, 0.88f, 0.82f, original.a) : new Color(0.96f, 0.91f, 0.81f, original.a);
            if (NeedsFrame(name)) AddFrame(image);
            return true;
        }

        // Resolve only known material surfaces; illustrations and semantic badges
        // keep their authored colors. Stop at the closest opaque surface.
        public static Color ReadableTextColor(Transform parent, Color requested)
        {
            for (Transform current = parent; current != null; current = current.parent)
            {
                Image surface = current.GetComponent<Image>();
                if (surface == null || !surface.enabled || surface.color.a < 0.65f) continue;
                if (surface.sprite == Paper)
                {
                    if (requested.r + requested.g + requested.b < 1f) return requested;
                    if (requested.r > requested.g * 1.5f && requested.g < 0.5f)
                        return new Color(.43f, .12f, .09f, requested.a);
                    if (requested.g > requested.r * 1.35f)
                        return new Color(.12f, .29f, .25f, requested.a);
                    return new Color(.20f, .13f, .08f, requested.a);
                }
                if ((surface.sprite == timber && timber != null) || surface.sprite == WoodButtonSkin.Face)
                {
                    if (requested.g > requested.r * 1.35f)
                        return new Color(.67f, .86f, .78f, requested.a);
                    if (requested.r + requested.g + requested.b < 1.1f)
                        return new Color(.97f, .93f, .83f, requested.a);
                }
                return requested;
            }
            return requested;
        }

        public static bool ApplySpecialButton(Button button, Color original)
        {
            if (button == null || original.a <= 0.01f) return false;
            bool crew = button.name.StartsWith("Dispatch Crew Toggle ", StringComparison.Ordinal);
            if (!crew && !button.name.StartsWith("Add Deck Card ", StringComparison.Ordinal)) return false;
            Image image = button.GetComponent<Image>();
            if (image == null || Paper == null) return false;
            image.sprite = Paper;
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = Paper.rect.width / 280f;
            image.color = Color.white;
            if (crew)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = original.a > 0.7f ? new Color(original.r, original.g, original.b, 1f) : new Color(0.96f, 0.87f, 0.70f, 0.9f);
                colors.highlightedColor = Color.Lerp(colors.normalColor, Color.white, 0.22f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = colors.normalColor * new Color(0.85f, 0.85f, 0.85f, 1f);
                colors.disabledColor = new Color(0.78f, 0.74f, 0.65f, 0.75f);
                button.colors = colors;
                Outline rim = button.gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(0.40f, 0.27f, 0.12f, 0.5f);
                rim.effectDistance = new Vector2(1f, -1f);
            }
            return true;
        }

        public static void ApplyControlPart(Image image)
        {
            if (image == null || image.color.a <= 0.01f) return;
            string name = image.name;
            if (name == "Handle" || name == "Toggle Box" || name == "Background")
            {
                if (WoodButtonSkin.Face == null) return;
                Color original = image.color;
                image.sprite = WoodButtonSkin.Face;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 28f;
                image.color = name == "Handle" ? new Color(1.3f, 1.13f, 0.82f, original.a) : new Color(0.75f, 0.70f, 0.6f, original.a);
            }
            else if (name == "Track" || name == "Fill" || name == "Toggle Check" || name == "Checkmark")
            {
                if (Paper == null) return;
                image.sprite = Paper;
                image.type = Image.Type.Tiled;
                image.pixelsPerUnitMultiplier = Paper.rect.width / 160f;
            }
            else if (name == "Keyword Bubble Pointer") ApplyPanel(image, image.color);
            else if (name.StartsWith("Treasure Chart Paper Edge ", StringComparison.Ordinal) && Paper != null)
            {
                image.sprite = Paper;
                image.type = Image.Type.Simple; // The board's projection component owns the paper corners.
            }
        }

        private static bool NeedsFrame(string name)
        {
            return name == "Settings Card" || name == "Achievements Card" || name == "Confirmation Card" ||
                name == "Warehouse Ledger" || name == "Route Selection" || name == "Voyage Setup" ||
                name == "Active Dispatch Job" || name == "Tutorial Guide" || name == "Guide Panel" ||
                name == "Tutorial Banner" || name == "Tutorial Battle Card";
        }

        private static void AddFrame(Image surface)
        {
            if (WoodButtonSkin.Face == null || surface.transform.Find("Material Timber Rim") != null) return;
            var host = new GameObject("Material Timber Rim", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)host.transform;
            rect.SetParent(surface.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image rim = host.GetComponent<Image>();
            rim.sprite = WoodButtonSkin.Face; rim.type = Image.Type.Sliced;
            rim.fillCenter = false; rim.pixelsPerUnitMultiplier = 28f;
            rim.color = new Color(.78f, .72f, .61f, 1f); rim.raycastTarget = false;
        }

        private static Sprite Timber
        {
            get
            {
                if (timber != null) return timber;
                if (WoodButtonSkin.Face == null) return null;
                Texture2D texture = WoodButtonSkin.Face.texture;
                // Sample only uninterrupted grain, never repeat the decorative button corners.
                timber = Sprite.Create(texture, new Rect(180f, 160f, texture.width - 360f, texture.height - 320f), Vector2.one * 0.5f, 100f);
                timber.name = "UI Timber Grain"; timber.hideFlags = HideFlags.HideAndDontSave;
                return timber;
            }
        }

        private static Sprite Load(ref Sprite cached, string path)
        {
            if (cached != null) return cached;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            cached = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, 100f);
            cached.name = path; cached.hideFlags = HideFlags.HideAndDontSave;
            return cached;
        }

        private static void Release()
        {
            foreach (Sprite sprite in new[] { canvas, paper, timber })
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            canvas = paper = timber = null;
        }
    }
}
