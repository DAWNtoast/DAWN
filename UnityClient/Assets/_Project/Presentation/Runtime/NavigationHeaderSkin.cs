using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Walnut and brass presentation for the voyage header, retaining its live readouts and controls.</summary>
    public static class NavigationHeaderSkin
    {
        public const string HeaderResourcePath = "NavigationUIV3/navigation_header_v3";
        public const string CompassResourcePath = "NavigationUIV3/navigation_compass_v3";
        private const string CompassName = "Voyage Header Compass";
        private static readonly Color HeadingInk = new Color(1f, .88f, .61f, 1f);
        private static readonly Color ReadoutInk = new Color(.97f, .91f, .78f, 1f);
        private static readonly Color Brass = new Color(.78f, .57f, .29f, 1f);
        private static Sprite headerFace;
        private static Sprite compassFace;

        static NavigationHeaderSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static void Apply(RectTransform header)
        {
            // Port and battle surfaces must retain their existing regional artwork.
            if (header == null || header.name != "Voyage Header") return;
            Image backing = header.GetComponent<Image>();
            if (backing != null)
            {
                backing.color = new Color(.085f, .058f, .039f, 1f);
                backing.raycastTarget = false;
            }

            Image frame = Child(header, "Voyage Header Frame")?.GetComponent<Image>();
            if (headerFace == null) headerFace = LoadSprite(HeaderResourcePath, "Navigation Walnut Header", true);
            if (frame != null && headerFace != null)
            {
                frame.sprite = headerFace;
                frame.type = Image.Type.Sliced;
                frame.fillCenter = true;
                frame.preserveAspect = false;
                frame.pixelsPerUnitMultiplier = headerFace.border.x / 8f;
                frame.color = Color.white;
                frame.raycastTarget = false;
            }

            if (compassFace == null) compassFace = LoadSprite(CompassResourcePath, "Navigation Brass Compass", false);
            if (compassFace != null)
            {
                RectTransform compass = Child(header, CompassName);
                if (compass == null)
                {
                    var decoration = new GameObject(CompassName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    decoration.layer = header.gameObject.layer;
                    compass = decoration.GetComponent<RectTransform>();
                    compass.SetParent(header, false);
                }
                Image image = compass.GetComponent<Image>();
                image.sprite = compassFace;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
                // The compass decorates the frame without introducing a new hit target.
                compass.SetSiblingIndex(frame == null ? 0 : frame.transform.GetSiblingIndex() + 1);
            }

            StyleText(Child(header, "Voyage Header Title"), 29, HeadingInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            StyleText(Child(header, "Voyage Header Status"), 18, ReadoutInk, FontStyle.Normal, TextAnchor.MiddleLeft);
            RectTransform ribbon = Child(header, "Lookout Time Ribbon");
            StyleText(Child(ribbon, "Lookout Time Readout"), 20, ReadoutInk, FontStyle.Normal, TextAnchor.MiddleCenter);
            StyleText(Child(ribbon, "Lookout Time Legend"), 12, new Color(.82f, .77f, .66f, 1f), FontStyle.Normal, TextAnchor.MiddleCenter);
            foreach (string name in new[] { "Voyage Cargo", "Voyage Settings" })
            {
                RectTransform control = Child(header, name);
                if (control == null) continue;
                StyleText(Child(control, "Label"), 22, ReadoutInk, FontStyle.Normal, TextAnchor.MiddleCenter);
                // Reuse the original selectable, callbacks and disabled appearance.
                foreach (Outline edge in control.GetComponents<Outline>())
                {
                    if (edge is WoodButtonRoleState) continue;
                    edge.effectColor = new Color(Brass.r, Brass.g, Brass.b, .46f);
                    edge.effectDistance = new Vector2(1f, -1f);
                }
            }
            ApplyLayout(header);
        }

        public static void ApplyLayout(RectTransform header)
        {
            if (header == null || header.name != "Voyage Header") return;
            const float informationLeft = 128f, informationWidth = 520f;
            const float rightMargin = 30f, settingsWidth = 128f, cargoWidth = 144f, actionGap = 16f;
            float informationRight = informationLeft + informationWidth;
            // The Expand canvas guarantees at least the 1920px design width.
            // Anchor the timeline between the fixed left/right groups rather
            // than reading header.rect during a CanvasScaler dimensions event:
            // that rect can still contain the previous render target's width.
            const float clockWidth = 720f;
            float clockCenterOffset = (informationRight - rightMargin - settingsWidth - actionGap - cargoWidth) * .5f;

            HeaderBox(Child(header, "Voyage Header Title"), new Vector2(informationWidth, 32f),
                0f, informationLeft + informationWidth * .5f, 17f);
            HeaderBox(Child(header, "Voyage Header Status"), new Vector2(informationWidth, 24f),
                0f, informationLeft + informationWidth * .5f, -23f);
            HeaderBox(Child(header, "Voyage Cargo"), new Vector2(cargoWidth, 64f),
                1f, -rightMargin - settingsWidth - actionGap - cargoWidth * .5f, 0f);
            HeaderBox(Child(header, "Voyage Settings"), new Vector2(settingsWidth, 64f),
                1f, -rightMargin - settingsWidth * .5f, 0f);
            LayoutButtonCaption(Child(header, "Voyage Cargo"));
            LayoutButtonCaption(Child(header, "Voyage Settings"));

            // All information stays in the central 70px band. The taller tutorial
            // header retains its objective/instruction space and its existing height.
            float compassSize = header.rect.height > PhaseSurfaceLayout.HeaderHeight + 1f ? 68f : 88f;
            HeaderBox(Child(header, CompassName), Vector2.one * compassSize, 0f, 64f, 0f);
            RectTransform ribbon = Child(header, "Lookout Time Ribbon");
            HeaderBox(ribbon, new Vector2(clockWidth, 68f), .5f, clockCenterOffset, 0f);
            HeaderBox(Child(ribbon, "Lookout Time Readout"), new Vector2(clockWidth - 20f, 24f), .5f, 0f, 22f);
            HeaderBox(Child(ribbon, "Lookout Time Legend"), new Vector2(clockWidth - 20f, 16f), .5f, 0f, -26f);
            RectTransform track = Child(ribbon, "Lookout Time Track");
            if (track != null)
            {
                // Preserve the timeline's hour/marker geometry and its live state;
                // changing the outer header never replaces its Graphic component.
                HeaderBox(track, new Vector2(clockWidth / .4f, 116f), .5f, 0f, -3f);
                track.localScale = Vector3.one * .4f;
            }
        }

        private static void LayoutButtonCaption(RectTransform control)
        {
            if (control == null) return;
            HeaderBox(Child(control, "Label"), control.sizeDelta - new Vector2(24f, 8f), .5f, 0f, 0f);
        }

        private static void StyleText(RectTransform rect, int size, Color color, FontStyle style, TextAnchor alignment)
        {
            Text text = rect == null ? null : rect.GetComponent<Text>();
            if (text == null) return;
            text.color = color;
            text.fontSize = text.resizeTextMaxSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size - 3);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void HeaderBox(RectTransform rect, Vector2 size, float anchorX, float x, float y)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(anchorX, .5f);
            rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static RectTransform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name && child.gameObject.activeSelf) return child as RectTransform;
            }
            return null;
        }

        private static Sprite LoadSprite(string path, string name, bool sliced)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            float border = sliced ? Mathf.Clamp(texture.height * .12f, 8f, 96f) : 0f;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Release()
        {
            ReleaseSprite(ref headerFace);
            ReleaseSprite(ref compassFace);
        }

        private static void ReleaseSprite(ref Sprite sprite)
        {
            if (sprite == null) return;
            if (Application.isPlaying) Object.Destroy(sprite);
            else Object.DestroyImmediate(sprite);
            sprite = null;
        }
    }
}
