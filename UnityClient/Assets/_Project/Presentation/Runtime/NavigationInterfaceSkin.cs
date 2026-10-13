using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Local voyage-dashboard materials; unrelated screens keep their own surface skins.</summary>
    public static class NavigationInterfaceSkin
    {
        public const string TitleStripName = "Navigation Map Title Strip";
        public const string PanelFrameResourcePath = "NavigationUIV3/navigation_panel_frame_v3";
        private static readonly Color SeaBacking = new Color(6f / 255f, 18f / 255f, 24f / 255f, 1f);
        private static readonly Color PaperInk = new Color(.20f, .13f, .075f, 1f);
        private static Sprite panelFrame;

        static NavigationInterfaceSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        private static Sprite PanelFrame
        {
            get
            {
                if (panelFrame != null) return panelFrame;
                Texture2D texture = Resources.Load<Texture2D>(PanelFrameResourcePath);
                if (texture == null) return WoodButtonSkin.Face;
                // The transparent source margin is outside the painted frame.
                // Crop through Sprite UVs; never alter the authored source PNG.
                panelFrame = Sprite.Create(texture, new Rect(32, 32, texture.width - 64, texture.height - 64),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 64f);
                panelFrame.name = "Navigation UI V3 Panel Frame";
                panelFrame.hideFlags = HideFlags.HideAndDontSave;
                return panelFrame;
            }
        }

        public static void ApplyDashboard(RectTransform content)
        {
            if (content == null) return;
            RectTransform chart = Child(content, "Navigation Chart");
            RectTransform controls = Child(content, "Navigation Controls");
            RectTransform lookout = Child(content, "Forward View Panel");
            if (chart != null)
            {
                Image surface = chart.GetComponent<Image>();
                if (surface != null)
                {
                    surface.sprite = surface.overrideSprite = null;
                    surface.type = Image.Type.Simple;
                    surface.color = SeaBacking;
                    surface.raycastTarget = false;
                }
                Image strip = EnsureImage(chart, TitleStripName);
                strip.sprite = NauticalSurfaceSkin.Paper;
                strip.overrideSprite = null;
                strip.type = Image.Type.Tiled;
                strip.pixelsPerUnitMultiplier = strip.sprite == null ? 1f : strip.sprite.rect.width / 760f;
                strip.color = new Color(1f, .97f, .90f, 1f);
                strip.raycastTarget = false;
                RectTransform rect = strip.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = new Vector2(chart.rect.width - 24f, 58f);
                rect.anchoredPosition = new Vector2(0f, chart.rect.height * .5f - 41f);
                strip.transform.SetAsFirstSibling();
                RawImage emblem = Child(chart, "Navigation Map Title Compass")?.GetComponent<RawImage>();
                if (emblem == null)
                {
                    var go = new GameObject("Navigation Map Title Compass", typeof(RectTransform), typeof(RawImage));
                    go.layer = chart.gameObject.layer;
                    go.transform.SetParent(chart, false);
                    emblem = go.GetComponent<RawImage>();
                }
                emblem.texture = Resources.Load<Texture2D>(NavigationCompassControl.ResourcePath);
                emblem.raycastTarget = false;
                emblem.rectTransform.anchorMin = emblem.rectTransform.anchorMax = emblem.rectTransform.pivot = Vector2.one * .5f;
                emblem.rectTransform.sizeDelta = new Vector2(34f, 34f);
                emblem.rectTransform.anchoredPosition = new Vector2(-chart.rect.width * .5f + 40f, chart.rect.height * .5f - 41f);
                SetText(chart, "Map Title", PaperInk);
                SetText(Child(chart, "Voyage Supply Endurance"), "Water Food Endurance", PaperInk);
                SetText(chart, "Map Course Hint", new Color(.94f, .88f, .72f, 1f));
                ApplyFrame(chart);
            }
            if (controls != null)
            {
                Image surface = controls.GetComponent<Image>();
                if (surface != null)
                {
                    surface.sprite = NauticalSurfaceSkin.Paper;
                    surface.overrideSprite = null;
                    surface.type = Image.Type.Tiled;
                    surface.pixelsPerUnitMultiplier = surface.sprite == null ? 1f : surface.sprite.rect.width / 920f;
                    surface.color = new Color(1f, .98f, .93f, 1f);
                    surface.raycastTarget = false;
                }
                SetText(controls, "Navigation Title", PaperInk);
                SetText(controls, "Distance Label", PaperInk);
                SetText(controls, "Forecast", PaperInk);
                ApplyRole(controls, "Execute Sail", WoodButtonRole.Primary);
                ApplyRole(controls, "Safe Return", WoodButtonRole.Secondary);
                ApplyRole(controls, "Forecast Details", WoodButtonRole.Secondary);
                ApplyFrame(controls);
            }
            if (lookout != null) ApplyFrame(lookout);
        }

        private static void ApplyFrame(RectTransform panel)
        {
            Image frame = EnsureImage(panel, panel.name + " Timber Frame");
            frame.sprite = PanelFrame;
            frame.overrideSprite = null;
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = panelFrame == null ? 14f : 8f;
            frame.color = Color.white;
            frame.raycastTarget = false;
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            frame.transform.SetAsLastSibling();
            // Some older saved panels retain the shared material's extra rim.
            Transform oldRim = panel.Find("Material Timber Rim");
            if (oldRim != null) oldRim.gameObject.SetActive(false);
        }

        private static void ApplyRole(Transform panel, string name, WoodButtonRole role)
        {
            Button button = Child(panel, name)?.GetComponent<Button>();
            if (button != null) WoodButtonSkin.SetRole(button, role);
        }

        private static void SetText(Transform panel, string name, Color color)
        {
            Text text = Child(panel, name)?.GetComponent<Text>();
            if (text != null) { text.color = color; text.raycastTarget = false; }
        }

        private static RectTransform Child(Transform parent, string name)
        {
            if (parent == null) return null;
            // A live Render retires old panels before their deferred destruction.
            // Apply the skin to the new active panel, just as the layout does.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name && child.gameObject.activeSelf) return child as RectTransform;
            }
            return null;
        }

        private static Image EnsureImage(RectTransform parent, string name)
        {
            RectTransform existing = Child(parent, name);
            if (existing != null)
                return existing.GetComponent<Image>() ?? existing.gameObject.AddComponent<Image>();
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void Release()
        {
            if (panelFrame == null) return;
            if (Application.isPlaying) Object.Destroy(panelFrame); else Object.DestroyImmediate(panelFrame);
            panelFrame = null;
        }
    }
}
