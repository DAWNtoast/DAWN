using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Anchored layout templates. Put columns inside a full-screen content root;
    /// backgrounds and modal blockers can use Stretch to cover their entire parent.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class UiLayout : MonoBehaviour
    {
        [SerializeField] private bool respectSafeArea = true;
        [SerializeField] private float maximumAspectRatio;
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform target;
        private RectTransform previousParent;
        private Camera previousCamera;
        private Rect previousViewport, previousSafeArea, previousParentRect;
        private bool previousLocalCoordinates, previousRespectSafeArea;
        private float previousMaximumAspectRatio;
        private bool applied;

        public readonly struct Columns
        {
            public RectTransform Root { get; }
            public IReadOnlyList<RectTransform> Regions { get; }
            internal Columns(RectTransform root, RectTransform[] regions)
            {
                Root = root;
                Regions = Array.AsReadOnly(regions);
            }
        }

        public static void Stretch(RectTransform rect, RectOffset padding = null)
        {
            if (rect == null) throw new ArgumentNullException(nameof(rect));
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding == null ? Vector2.zero : new Vector2(padding.left, padding.bottom);
            rect.offsetMax = padding == null ? Vector2.zero : new Vector2(-padding.right, -padding.top);
        }

        public static RectTransform CreateFullScreen(string name, Transform parent,
            bool respectSafeArea = true, float maxAspectRatio = 0f)
        {
            RectTransform root = UiKit.CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            UiLayout layout = root.gameObject.AddComponent<UiLayout>();
            layout.Configure(respectSafeArea, maxAspectRatio);
            return root;
        }

        public static Columns CreateTwoColumn(string name, Transform parent, float leftWeight = 1f,
            float rightWeight = 1f, float spacing = UiTheme.Gutter, RectOffset padding = null)
        {
            return CreateColumns(name, parent, new[] { "Left", "Right" },
                new[] { leftWeight, rightWeight }, spacing, padding);
        }

        public static Columns CreateThreeColumn(string name, Transform parent, float leftWeight = 1f,
            float centerWeight = 1f, float rightWeight = 1f, float spacing = UiTheme.Gutter, RectOffset padding = null)
        {
            return CreateColumns(name, parent, new[] { "Left", "Center", "Right" },
                new[] { leftWeight, centerWeight, rightWeight }, spacing, padding);
        }

        public static Columns CreateSidebarDetail(string name, Transform parent, float sidebarWidth = 320f,
            float spacing = UiTheme.Gutter, RectOffset padding = null)
        {
            if (!PositiveFinite(sidebarWidth)) throw new ArgumentOutOfRangeException(nameof(sidebarWidth));
            Columns columns = CreateColumns(name, parent, new[] { "Sidebar", "Detail" },
                new[] { 1f, 1f }, spacing, padding);
            LayoutElement sidebar = columns.Regions[0].GetComponent<LayoutElement>();
            sidebar.flexibleWidth = 0f;
            sidebar.preferredWidth = sidebarWidth;
            // On narrow parents the layout can shrink both regions instead of
            // forcing a fixed sidebar beyond the screen boundary.
            columns.Regions[1].GetComponent<LayoutElement>().preferredWidth = sidebarWidth;
            return columns;
        }

        private static Columns CreateColumns(string name, Transform parent, string[] roles,
            float[] weights, float spacing, RectOffset padding)
        {
            if (!Finite(spacing) || spacing < 0f) throw new ArgumentOutOfRangeException(nameof(spacing));
            foreach (float weight in weights)
                if (!PositiveFinite(weight)) throw new ArgumentOutOfRangeException(nameof(weights));
            RectTransform root = UiKit.CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Stretch(root);
            HorizontalLayoutGroup group = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.padding = padding == null ? new RectOffset() :
                new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            var regions = new RectTransform[roles.Length];
            for (int index = 0; index < roles.Length; index++)
            {
                regions[index] = UiKit.CreateRect(name + " " + roles[index], root, Vector2.zero, Vector2.zero);
                LayoutElement sizing = regions[index].gameObject.AddComponent<LayoutElement>();
                sizing.minWidth = sizing.minHeight = 0f;
                sizing.preferredWidth = sizing.preferredHeight = 0f;
                sizing.flexibleWidth = weights[index];
                sizing.flexibleHeight = 1f;
            }
            return new Columns(root, regions);
        }

        /// <summary>
        /// Normalized anchors inside a viewport expressed in the same coordinates
        /// as safeArea. An aspect cap limits width only and centers the content in
        /// the safe region; zero means the full available width. Invalid/zero-size
        /// viewports return full anchors until the next valid resize.
        /// </summary>
        public static Rect CalculateAnchors(Rect viewport, Rect safeArea, bool respectSafeArea = true,
            float maxAspectRatio = 0f)
        {
            if (!ValidRect(viewport)) return new Rect(0f, 0f, 1f, 1f);
            Rect content = viewport;
            if (respectSafeArea && ValidRect(safeArea))
            {
                float left = Mathf.Clamp(safeArea.xMin, viewport.xMin, viewport.xMax);
                float right = Mathf.Clamp(safeArea.xMax, viewport.xMin, viewport.xMax);
                float bottom = Mathf.Clamp(safeArea.yMin, viewport.yMin, viewport.yMax);
                float top = Mathf.Clamp(safeArea.yMax, viewport.yMin, viewport.yMax);
                content = Rect.MinMaxRect(left, bottom, right, top);
            }
            if (PositiveFinite(maxAspectRatio) && content.width > content.height * maxAspectRatio)
            {
                float width = content.height * maxAspectRatio;
                content.x += (content.width - width) * .5f;
                content.width = width;
            }
            return new Rect((content.xMin - viewport.xMin) / viewport.width,
                (content.yMin - viewport.yMin) / viewport.height,
                content.width / viewport.width, content.height / viewport.height);
        }

        public void Configure(bool useSafeArea, float maxAspectRatio = 0f)
        {
            respectSafeArea = useSafeArea;
            maximumAspectRatio = PositiveFinite(maxAspectRatio) ? maxAspectRatio : 0f;
            applied = false;
            Refresh();
        }

        private void OnEnable() { applied = false; Refresh(); }
        private void OnTransformParentChanged() { applied = false; }
        private void LateUpdate() { Refresh(); }

        /// <summary>Refreshes only when the parent, camera viewport or safe-area inputs change.</summary>
        public void Refresh()
        {
            if (target == null) target = GetComponent<RectTransform>();
            RectTransform parent = target.parent as RectTransform;
            if (parent == null || !ValidRect(parent.rect)) return;
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas == null ? null : canvas.rootCanvas;
            Camera camera = rootCanvas != null && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? rootCanvas.worldCamera : null;
            // Unattached editor previews, world-space canvases and camera canvases
            // without an assigned camera use parent coordinates, never Screen pixels.
            bool local = rootCanvas == null || rootCanvas.renderMode == RenderMode.WorldSpace ||
                (rootCanvas.renderMode == RenderMode.ScreenSpaceCamera && camera == null);
            Rect viewport = parent.rect;
            Rect safeArea = viewport;
            if (!local)
            {
                parent.GetWorldCorners(corners);
                Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 max = min;
                for (int index = 1; index < corners.Length; index++)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                viewport = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                // Render textures have no device safe-area cutout. Unity ignores
                // camera.rect/pixelRect when rendering to a texture, so a retained
                // screen viewport must not crop the offscreen parent layout.
                safeArea = camera != null && camera.targetTexture != null ? viewport : Screen.safeArea;
            }
            if (!ValidRect(viewport)) return;
            if (applied && previousParent == parent && previousCamera == camera && previousViewport == viewport &&
                previousSafeArea == safeArea && previousParentRect == parent.rect && previousLocalCoordinates == local &&
                previousRespectSafeArea == respectSafeArea && previousMaximumAspectRatio == maximumAspectRatio) return;

            Rect anchors = CalculateAnchors(viewport, safeArea, !local && respectSafeArea, maximumAspectRatio);
            if (!local)
            {
                Vector2 screenMin = viewport.min + Vector2.Scale(viewport.size, anchors.min);
                Vector2 screenMax = viewport.min + Vector2.Scale(viewport.size, anchors.max);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMin, camera, out Vector2 min) ||
                    !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenMax, camera, out Vector2 max)) return;
                anchors = Rect.MinMaxRect((min.x - parent.rect.xMin) / parent.rect.width,
                    (min.y - parent.rect.yMin) / parent.rect.height,
                    (max.x - parent.rect.xMin) / parent.rect.width, (max.y - parent.rect.yMin) / parent.rect.height);
            }
            target.anchorMin = anchors.min;
            target.anchorMax = anchors.max;
            target.offsetMin = target.offsetMax = Vector2.zero;
            previousParent = parent;
            previousCamera = camera;
            previousViewport = viewport;
            previousSafeArea = safeArea;
            previousParentRect = parent.rect;
            previousLocalCoordinates = local;
            previousRespectSafeArea = respectSafeArea;
            previousMaximumAspectRatio = maximumAspectRatio;
            applied = true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool PositiveFinite(float value) => Finite(value) && value > 0f;
        private static bool ValidRect(Rect rect) => PositiveFinite(rect.width) && PositiveFinite(rect.height) &&
            Finite(rect.x) && Finite(rect.y) && Finite(rect.xMax) && Finite(rect.yMax);
    }
}
