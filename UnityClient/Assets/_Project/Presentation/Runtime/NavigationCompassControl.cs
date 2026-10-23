using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Reuses the dashboard's heading buttons as eight independently clickable compass wedges.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class NavigationCompassControl : MonoBehaviour, ICanvasRaycastFilter
    {
        public const string ResourcePath = "NavigationUIV3/navigation_compass_v3";
        private const string ArtworkName = "Navigation Compass Artwork";
        private const string SectorName = "Navigation Compass Sector";
        private static readonly string[] Directions =
            { "North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest" };
        [SerializeField] private Vector2 compassCenter;
        [SerializeField] private float compassDiameter = 280f;
        private readonly Button[] buttons = new Button[8];
        private readonly NavigationCompassSectorGraphic[] sectors = new NavigationCompassSectorGraphic[8];
        private bool applying;
        private int navigationAvailability = -1;
        private bool IsHeadingTarget => name.StartsWith("Heading ", System.StringComparison.Ordinal);

        public static NavigationCompassControl Attach(RectTransform panel)
        {
            if (panel == null) return null;
            NavigationCompassControl control = panel.GetComponent<NavigationCompassControl>();
            if (control == null) control = panel.gameObject.AddComponent<NavigationCompassControl>();
            control.Apply();
            return control;
        }

        public static void Layout(RectTransform panel, Vector2 center, float diameter)
        {
            if (panel == null) return;
            NavigationCompassControl control = panel.GetComponent<NavigationCompassControl>();
            if (control == null) control = panel.gameObject.AddComponent<NavigationCompassControl>();
            control.compassCenter = center;
            control.compassDiameter = Mathf.Max(100f, diameter);
            control.Apply();
        }

        private void OnEnable() { if (!IsHeadingTarget) Apply(); }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!IsHeadingTarget) return true;
            NavigationCompassSectorGraphic sector = DirectChild(transform, SectorName)?.GetComponent<NavigationCompassSectorGraphic>();
            return sector != null && sector.IsRaycastLocationValid(screenPoint, eventCamera);
        }

        private void LateUpdate()
        {
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            if (IsHeadingTarget) return;
            // Selection belongs to WoodButtonRoleState; disabled state belongs
            // to Button. Observe both without replacing any gameplay callback.
            for (int index = 0; index < buttons.Length; index++) RefreshState(index);
            ApplyKeyboardNavigation(transform as RectTransform);
        }

        private void Apply()
        {
            if (applying || IsHeadingTarget) return;
            var panel = transform as RectTransform;
            if (panel == null) return;
            applying = true;
            try
            {
                RawImage artwork = DirectChild(panel, ArtworkName)?.GetComponent<RawImage>();
                if (artwork == null)
                {
                    var go = new GameObject(ArtworkName, typeof(RectTransform), typeof(RawImage));
                    go.layer = panel.gameObject.layer;
                    go.transform.SetParent(panel, false);
                    artwork = go.GetComponent<RawImage>();
                }
                artwork.texture = Resources.Load<Texture2D>(ResourcePath);
                artwork.color = Color.white;
                artwork.raycastTarget = false;
                Place(artwork.rectTransform, compassCenter, Vector2.one * compassDiameter);
                artwork.transform.SetAsFirstSibling();

                Text oldCenter = DirectChild(panel, "Compass")?.GetComponent<Text>();
                if (oldCenter != null) { oldCenter.enabled = false; oldCenter.raycastTarget = false; }
                for (int index = 0; index < Directions.Length; index++)
                {
                    RectTransform root = DirectChild(panel, "Heading " + Directions[index]);
                    Button button = root == null ? null : root.GetComponent<Button>();
                    buttons[index] = button;
                    if (button == null) continue;
                    // Tutorial guards are siblings of the visible sector. A
                    // filter on the button root also clips those transparent
                    // guards, so a disabled neighbour cannot cover the east key.
                    if (root.GetComponent<NavigationCompassControl>() == null)
                        root.gameObject.AddComponent<NavigationCompassControl>();
                    NauticalButtonMotion motion = root.GetComponent<NauticalButtonMotion>();
                    if (motion != null) motion.enabled = false;
                    Place(root, compassCenter, Vector2.one * compassDiameter);
                    // A rectangle image cannot participate in hit testing once
                    // all eight roots share the compass bounds.
                    Image rectangle = root.GetComponent<Image>();
                    if (rectangle != null) { rectangle.enabled = false; rectangle.raycastTarget = false; }
                    NavigationCompassSectorGraphic sector = DirectChild(root, SectorName)?.GetComponent<NavigationCompassSectorGraphic>();
                    if (sector == null)
                    {
                        var go = new GameObject(SectorName, typeof(RectTransform), typeof(NavigationCompassSectorGraphic));
                        go.layer = root.gameObject.layer;
                        go.transform.SetParent(root, false);
                        sector = go.GetComponent<NavigationCompassSectorGraphic>();
                    }
                    sectors[index] = sector;
                    Place(sector.rectTransform, Vector2.zero, Vector2.one * compassDiameter);
                    sector.transform.SetAsFirstSibling();
                    sector.sprite = WoodButtonSkin.Face;
                    sector.color = Color.white;
                    sector.raycastTarget = true;
                    if (button.targetGraphic != sector)
                    {
                        button.targetGraphic = sector;
                        Color tint = button.IsInteractable() || button.transition == Selectable.Transition.None
                            ? button.colors.normalColor : button.colors.disabledColor;
                        sector.canvasRenderer.SetColor(tint * button.colors.colorMultiplier);
                    }
                    Text label = DirectChild(root, "Label")?.GetComponent<Text>();
                    if (label != null)
                    {
                        float angle = (90f - index * 45f) * Mathf.Deg2Rad;
                        Vector2 labelCenter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (compassDiameter * .335f);
                        Place(label.rectTransform, labelCenter, new Vector2(compassDiameter * .24f, compassDiameter * .15f));
                        label.fontSize = Mathf.Clamp(Mathf.RoundToInt(compassDiameter * .079f), 18, 25);
                        label.resizeTextForBestFit = true;
                        label.resizeTextMaxSize = label.fontSize;
                        label.resizeTextMinSize = Mathf.Max(16, label.fontSize - 3);
                        label.alignment = TextAnchor.MiddleCenter;
                        label.horizontalOverflow = HorizontalWrapMode.Overflow;
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        label.raycastTarget = false;
                    }
                    RefreshState(index);
                }
                ApplyKeyboardNavigation(panel, true);
            }
            finally { applying = false; }
        }

        private void RefreshState(int index)
        {
            Button button = buttons[index];
            NavigationCompassSectorGraphic sector = sectors[index];
            if (button == null || sector == null) return;
            WoodButtonRoleState role = button.GetComponent<WoodButtonRoleState>();
            bool selected = role != null && role.IsSelected;
            sector.Configure(90f - index * 45f, selected);
            Text label = DirectChild(button.transform, "Label")?.GetComponent<Text>();
            if (label == null) return;
            // Tutorial locks intentionally preserve the ordinary brightness.
            bool visiblyDisabled = !button.IsInteractable() && button.transition != Selectable.Transition.None;
            Color ink = visiblyDisabled ? new Color(.56f, .52f, .43f, .85f) :
                selected ? new Color(1f, .88f, .48f, 1f) : WoodButtonSkin.Ink;
            if (label.color != ink) label.color = ink;
        }

        private void ApplyKeyboardNavigation(RectTransform panel, bool force = false)
        {
            Selectable details = DirectChild(panel, "Forecast Details")?.GetComponent<Selectable>();
            Selectable back = DirectChild(panel, "Safe Return")?.GetComponent<Selectable>();
            int availability = 0;
            for (int index = 0; index < buttons.Length; index++)
                if (CanNavigateTo(buttons[index])) availability |= 1 << index;
            if (CanNavigateTo(details)) availability |= 1 << 8;
            if (CanNavigateTo(back)) availability |= 1 << 9;
            if (!force && navigationAvailability == availability) return;
            navigationAvailability = availability;
            for (int index = 0; index < buttons.Length; index++)
            {
                if (buttons[index] == null) continue;
                // Automatic navigation sees coincident button centres. Explicit
                // navigation must also skip tutorial-locked controls: Unity's
                // explicit-navigation path does not check IsInteractable.
                Navigation navigation = buttons[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnRight = NextAvailableHeading(index, 1);
                navigation.selectOnLeft = NextAvailableHeading(index, -1);
                navigation.selectOnUp = HeadingTowards(index, 0, index <= 4 ? -1 : 1, details);
                navigation.selectOnDown = HeadingTowards(index, 4, index < 4 ? 1 : -1, back);
                buttons[index].navigation = navigation;
            }
        }

        private Selectable NextAvailableHeading(int start, int step)
        {
            for (int offset = 1; offset < buttons.Length; offset++)
            {
                Button candidate = buttons[(start + step * offset + buttons.Length) % buttons.Length];
                if (CanNavigateTo(candidate)) return candidate;
            }
            return null;
        }

        private Selectable HeadingTowards(int start, int end, int step, Selectable fallback)
        {
            int index = start;
            while (index != end)
            {
                index = (index + step + buttons.Length) % buttons.Length;
                if (CanNavigateTo(buttons[index])) return buttons[index];
            }
            return CanNavigateTo(fallback) ? fallback : null;
        }

        private static bool CanNavigateTo(Selectable control) =>
            control != null && control.isActiveAndEnabled && control.IsInteractable();

        private static RectTransform DirectChild(Transform parent, string name) =>
            parent == null ? null : parent.Find(name) as RectTransform;

        private static void Place(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }
    }
}
