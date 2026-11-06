using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Local paper, walnut and brass styling for the market shop drawer.</summary>
    public static class MarketShopSkin
    {
        public const string PaperResourcePath = "PortFacilities/Market/market_shop_panel_v3";
        // The 56px nine-slice includes 8px of quiet paper beyond the painted rim.
        public const float PaperReadingBorder = 48f;
        public static readonly Color Ink = new Color(.25f, .18f, .12f, 1f);
        public static readonly Color MutedInk = new Color(.48f, .40f, .31f, 1f);
        private static readonly Color LightFace = new Color(.94f, .91f, .84f, 1f);
        private static Sprite paper, controlFace, diamond, checkmark;
        private static Texture2D controlTexture, diamondTexture, checkmarkTexture;

        static MarketShopSkin()
        {
            Application.quitting += Release;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Release;
#endif
        }

        public static Sprite Paper
        {
            get
            {
                if (paper != null) return paper;
                Texture2D texture = Resources.Load<Texture2D>(PaperResourcePath);
                if (texture == null) return null;
                // The authored texture includes an ivory margin outside the wood.
                // Trim that margin in sprite space so the wood is the outer edge.
                const float outerMargin = 8f;
                paper = Sprite.Create(texture, new Rect(outerMargin, outerMargin,
                    texture.width - outerMargin * 2f, texture.height - outerMargin * 2f),
                    Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * (64f - outerMargin));
                paper.name = PaperResourcePath;
                paper.hideFlags = HideFlags.HideAndDontSave;
                return paper;
            }
        }

        public static void ApplyPanel(Image image)
        {
            if (image == null) return;
            Sprite face = Paper;
            if (face == null) return;
            ApplyFace(image, face, true);
            image.pixelsPerUnitMultiplier = 2f;
            // The caller owns hit testing, including the opaque drawer backing.
        }

        public static void ReleaseGameplayPaperReference()
        {
            // Let the existing return-to-menu UnloadUnusedAssets pass collect the
            // borrowed paper texture without invalidating a still-visible Image.
            if (paper != null) paper.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            paper = null;
        }

        // Inventory tiles and multi-field choices use dark ink on the ledger surface.
        public static void ApplyChoiceRow(Button button, bool selected = false)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            ClearWoodRole(button);
            ApplyFace(image, ControlFace, true);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            Color normal = selected ? new Color(.95f, .86f, .67f, 1f) : LightFace;
            button.colors = LightColors(normal);
            SetLabelColor(button, Ink);
        }

        public static void ApplyInput(InputField input)
        {
            if (input == null) return;
            Image image = input.GetComponent<Image>();
            if (image == null) return;
            ApplyFace(image, ControlFace, true);
            input.targetGraphic = image;
            input.transition = Selectable.Transition.ColorTint;
            input.colors = LightColors(new Color(.98f, .96f, .90f, 1f));
            if (input.textComponent != null) input.textComponent.color = Ink;
            if (input.placeholder is Text placeholder) placeholder.color = MutedInk;
        }

        public static void ApplyToggle(Toggle toggle)
        {
            if (toggle == null) return;
            Image box = toggle.transform.Find("Background")?.GetComponent<Image>();
            if (box != null)
            {
                ApplyFace(box, ControlFace, true);
                Outline outline = box.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                toggle.targetGraphic = box;
                toggle.transition = Selectable.Transition.ColorTint;
                toggle.colors = LightColors(LightFace);
            }
            if (toggle.graphic is Image check)
            {
                ApplyFace(check, Checkmark, false);
                check.color = Ink;
                check.raycastTarget = false;
            }
        }

        public static void ApplyDropdown(Dropdown dropdown)
        {
            if (dropdown == null) return;
            Image face = dropdown.GetComponent<Image>();
            if (face != null)
            {
                ApplyFace(face, ControlFace, true);
                Outline outline = face.GetComponent<Outline>();
                if (outline != null) outline.enabled = false;
                dropdown.targetGraphic = face;
                dropdown.transition = Selectable.Transition.ColorTint;
                dropdown.colors = LightColors(LightFace);
            }
            if (dropdown.captionText != null) dropdown.captionText.color = Ink;
            if (dropdown.template == null) return;
            Image paperFace = dropdown.template.GetComponent<Image>();
            if (paperFace != null) ApplyFace(paperFace, ControlFace, true);
            foreach (Toggle item in dropdown.template.GetComponentsInChildren<Toggle>(true))
            {
                if (item.targetGraphic is Image itemFace)
                {
                    ApplyFace(itemFace, ControlFace, true);
                    item.transition = Selectable.Transition.ColorTint;
                    item.colors = LightColors(LightFace);
                }
                if (item.graphic is Image check)
                {
                    ApplyFace(check, Checkmark, false);
                    check.color = Ink;
                    check.raycastTarget = false;
                    check.rectTransform.sizeDelta = new Vector2(20f, 20f);
                }
            }
            foreach (Text label in dropdown.template.GetComponentsInChildren<Text>(true)) label.color = Ink;
        }

        public static void ApplyCheckboxRow(Button button)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;
            ClearWoodRole(button);
            ApplyFace(image, null, false);
            image.color = Color.clear;
            Text label = button.GetComponentInChildren<Text>(true);
            button.targetGraphic = label != null ? (Graphic)label : image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.35f, 1.25f, 1.12f, 1f);
            colors.pressedColor = new Color(.75f, .68f, .58f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, .45f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            SetLabelColor(button, Ink);
        }

        public static void ApplySlider(Slider slider)
        {
            if (slider == null) return;
            Image track = slider.transform.Find("Background")?.GetComponent<Image>();
            if (track != null)
            {
                ApplyFace(track, null, false);
                track.color = new Color(.76f, .64f, .43f, 1f);
            }
            Image fill = slider.fillRect == null ? null : slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                ApplyFace(fill, null, false);
                fill.color = new Color(.35f, .25f, .16f, 1f);
            }
            Image handle = slider.handleRect == null ? null : slider.handleRect.GetComponent<Image>();
            if (handle == null) return;
            ApplyFace(handle, Diamond, false);
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.12f, 1.08f, .95f, 1f);
            colors.pressedColor = new Color(.80f, .73f, .60f, 1f);
            colors.disabledColor = new Color(.75f, .72f, .66f, .65f);
            colors.fadeDuration = .1f;
            slider.colors = colors;
            // Keep the authored hit box, handle anchors, range and callbacks.
        }

        private static void ApplyFace(Image image, Sprite face, bool sliced)
        {
            image.overrideSprite = null;
            image.sprite = face;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.fillCenter = true;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = Color.white;
        }

        private static void ClearWoodRole(Button button)
        {
            WoodButtonRoleState role = button.GetComponent<WoodButtonRoleState>();
            if (role == null) return;
            role.Select(false);
            role.enabled = false;
            // NauticalButtonMotion changes only scale, so retain its pointer feedback.
        }

        private static void SetLabelColor(Button button, Color color)
        {
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                label.color = color;
                label.raycastTarget = false;
            }
        }

        private static ColorBlock LightColors(Color normal)
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = normal;
            colors.highlightedColor = colors.selectedColor = Color.Lerp(normal, Color.white, .45f);
            colors.pressedColor = new Color(.82f, .75f, .63f, 1f);
            colors.disabledColor = new Color(.83f, .81f, .75f, .70f);
            colors.fadeDuration = .1f;
            return colors;
        }

        private static Sprite ControlFace
        {
            get
            {
                if (controlFace == null)
                    controlFace = CreateGeometrySprite("Market Light Control", false, out controlTexture);
                return controlFace;
            }
        }

        private static Sprite Diamond
        {
            get
            {
                if (diamond == null)
                    diamond = CreateGeometrySprite("Market Brass Diamond", true, out diamondTexture);
                return diamond;
            }
        }

        private static Sprite Checkmark
        {
            get
            {
                if (checkmark != null) return checkmark;
                const int size = 64;
                checkmarkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Paper Control Checkmark", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[size * size];
                Vector2 a = new Vector2(10f, 31f), b = new Vector2(26f, 16f), c = new Vector2(54f, 49f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + .5f, y + .5f);
                    float distance = Mathf.Min(SegmentDistance(point, a, b), SegmentDistance(point, b, c));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(4.5f - distance));
                }
                checkmarkTexture.SetPixels32(pixels);
                checkmarkTexture.Apply(false, true);
                checkmark = Sprite.Create(checkmarkTexture, new Rect(0f, 0f, size, size), Vector2.one * .5f, 100f);
                checkmark.name = "Paper Control Checkmark";
                checkmark.hideFlags = HideFlags.HideAndDontSave;
                return checkmark;
            }
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            return Vector2.Distance(point, a + segment * Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude));
        }

        private static Sprite CreateGeometrySprite(string name, bool isDiamond, out Texture2D texture)
        {
            const int size = 32;
            const float half = size * .5f, radius = 4f;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            Color edge = new Color(.42f, .30f, .18f, 1f);
            Color centre = isDiamond ? new Color(.82f, .66f, .39f, 1f) : Color.white;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x + .5f - half), dy = Mathf.Abs(y + .5f - half);
                float distance;
                if (isDiamond) distance = (dx + dy - (half - 1f)) * .7071068f;
                else
                {
                    Vector2 q = new Vector2(dx, dy) - Vector2.one * (half - radius);
                    distance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude +
                        Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                }
                Color pixel = Color.Lerp(centre, edge, Mathf.Clamp01(distance + 1.8f));
                pixel.a = Mathf.Clamp01(.5f - distance);
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f,
                100f, 0, SpriteMeshType.FullRect, isDiamond ? Vector4.zero : Vector4.one * 6f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Release()
        {
            Destroy(paper); paper = null;
            Destroy(controlFace); controlFace = null;
            Destroy(diamond); diamond = null;
            Destroy(checkmark); checkmark = null;
            Destroy(controlTexture); controlTexture = null;
            Destroy(diamondTexture); diamondTexture = null;
            Destroy(checkmarkTexture); checkmarkTexture = null;
        }

        private static void Destroy(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Object.Destroy(asset);
            else Object.DestroyImmediate(asset);
        }
    }
}
