using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum NavalCommandButtonKind { Ram, Board, Sail, Clear, EndTurn }

    /// <summary>Visual-only skin for the compact naval command rail. Existing callbacks and hit bounds stay on the Button.</summary>
    [DisallowMultipleComponent]
    public sealed class NavalCommandButtonSkin : MonoBehaviour
    {
        private Button button;
        private Text label;
        private Image face;
        private NavalCommandIconGraphic icon, arrow;
        private NavalCommandButtonKind kind;
        private bool? lastAvailable;

        public static void Apply(Button target, NavalCommandButtonKind style)
        {
            if (target == null) return;
            NavalCommandButtonSkin skin = target.GetComponent<NavalCommandButtonSkin>();
            if (skin == null) skin = target.gameObject.AddComponent<NavalCommandButtonSkin>();
            skin.Configure(target, style);
        }

        private void Configure(Button target, NavalCommandButtonKind style)
        {
            button = target;
            kind = style;
            label = target.GetComponentInChildren<Text>(true);
            NauticalButtonMotion motion = target.GetComponent<NauticalButtonMotion>();
            if (motion != null) motion.enabled = false;
            foreach (BaseMeshEffect effect in target.GetComponents<BaseMeshEffect>()) effect.enabled = false;
            Image legacy = target.GetComponent<Image>();
            if (legacy != null)
            {
                legacy.sprite = null;
                legacy.color = Color.clear;
                legacy.raycastTarget = true;
            }
            if (face == null)
            {
                RectTransform rect = Child("Naval Command Face");
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
                face = rect.GetComponent<Image>();
                if (face == null) face = rect.gameObject.AddComponent<Image>();
            }
            face.raycastTarget = false;
            WoodButtonSkin.ApplyAction(target,
                kind == NavalCommandButtonKind.EndTurn ? WoodButtonRole.Primary : WoodButtonRole.Secondary, face: face);
            ConfigureLabel();
            ConfigureIcons();
            lastAvailable = null;
            RefreshForeground();
        }

        private void ConfigureLabel()
        {
            if (label == null) return;
            foreach (Shadow effect in label.GetComponents<Shadow>()) effect.enabled = false;
            Transform backing = label.transform.parent.Find(label.name + " Backing");
            if (backing != null) backing.gameObject.SetActive(false);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.alignment = kind == NavalCommandButtonKind.Sail ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            Vector2 size = ((RectTransform)button.transform).rect.size;
            bool tactic = kind == NavalCommandButtonKind.Ram || kind == NavalCommandButtonKind.Board;
            GameTypography.Apply(label,
                kind == NavalCommandButtonKind.EndTurn ? 24 : kind == NavalCommandButtonKind.Clear ? 18 : 21,
                FontStyle.Normal);
            rect.sizeDelta = tactic ? new Vector2(size.x - 16f, 30f) :
                kind == NavalCommandButtonKind.Sail ? new Vector2(size.x - 58f, size.y - 8f) :
                kind == NavalCommandButtonKind.EndTurn ? new Vector2(140f, size.y - 8f) : size - new Vector2(8f, 8f);
            rect.anchoredPosition = tactic ? new Vector2(0f, -17f) :
                kind == NavalCommandButtonKind.Sail ? new Vector2(20f, 0f) :
                kind == NavalCommandButtonKind.EndTurn ? new Vector2(8f, 0f) : Vector2.zero;
        }

        private void ConfigureIcons()
        {
            if (kind != NavalCommandButtonKind.Clear)
            {
                if (icon == null)
                {
                    RectTransform rect = Child("Naval Command Icon");
                    icon = rect.GetComponent<NavalCommandIconGraphic>();
                    if (icon == null) icon = rect.gameObject.AddComponent<NavalCommandIconGraphic>();
                }
                icon.gameObject.SetActive(true);
                icon.raycastTarget = false;
                icon.Configure(kind == NavalCommandButtonKind.Ram ? NavalCommandIconKind.Swords :
                    kind == NavalCommandButtonKind.Board ? NavalCommandIconKind.Anchor :
                    kind == NavalCommandButtonKind.Sail ? NavalCommandIconKind.Navigation : NavalCommandIconKind.Hourglass);
                bool tactic = kind == NavalCommandButtonKind.Ram || kind == NavalCommandButtonKind.Board;
                icon.rectTransform.sizeDelta = Vector2.one * (tactic ? 24f : 22f);
                icon.rectTransform.anchoredPosition = tactic ? new Vector2(0f, 16f) :
                    kind == NavalCommandButtonKind.Sail ? new Vector2(-((RectTransform)button.transform).rect.width * .5f + 25f, 0f) :
                    new Vector2(-67f, 0f);
            }
            else if (icon != null) icon.gameObject.SetActive(false);
            if (kind == NavalCommandButtonKind.EndTurn)
            {
                if (arrow == null)
                {
                    RectTransform rect = Child("Naval Command Chevron");
                    arrow = rect.GetComponent<NavalCommandIconGraphic>();
                    if (arrow == null) arrow = rect.gameObject.AddComponent<NavalCommandIconGraphic>();
                }
                arrow.gameObject.SetActive(true);
                arrow.raycastTarget = false;
                arrow.Configure(NavalCommandIconKind.Chevron);
                arrow.rectTransform.sizeDelta = Vector2.one * 16f;
                arrow.rectTransform.anchoredPosition = new Vector2(((RectTransform)button.transform).rect.width * .5f - 21f, 0f);
            }
            else if (arrow != null) arrow.gameObject.SetActive(false);
        }

        private RectTransform Child(string childName)
        {
            RectTransform rect = transform.Find(childName) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(transform, false);
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            return rect;
        }

        private void LateUpdate() => RefreshForeground();

        private void RefreshForeground()
        {
            if (button == null) return;
            bool available = button.IsInteractable();
            if (lastAvailable == available) return;
            lastAvailable = available;
            if (label != null) label.color = available ? WoodButtonSkin.Ink : WoodButtonSkin.MutedInk;
            Color ink = !available ? new Color(.49f, .55f, .51f, 1f) :
                kind == NavalCommandButtonKind.EndTurn ? new Color(.92f, .79f, .53f, 1f) : new Color(.79f, .67f, .45f, 1f);
            if (icon != null) icon.color = ink;
            if (arrow != null) arrow.color = ink * new Color(1f, 1f, 1f, .75f);
        }

    }
}
