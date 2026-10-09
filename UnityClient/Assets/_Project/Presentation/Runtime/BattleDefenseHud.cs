using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // Presentation state only. Guard is expendable; the ship's permanent armor
    // is not an extra HP pool and must never be consumed by this view.
    public sealed class BattleDefenseHud : MonoBehaviour
    {
        private static Sprite fillSprite;
        public static Sprite FillSprite
        {
            get
            {
                if (fillSprite == null)
                {
                    // UGUI ignores Image.fillAmount when no sprite is assigned.
                    fillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f, 1f);
                    fillSprite.name = "Battle Solid Fill";
                    fillSprite.hideFlags = HideFlags.HideAndDontSave;
                }
                return fillSprite;
            }
        }
        public float DisplayedHull { get; private set; }
        public float DisplayedGuard { get; private set; }
        public int PermanentArmor { get; private set; }
        public int MaximumHull { get; private set; }
        public Image ChipBar { get; private set; }
        public Image HullBar { get; private set; }
        private Image topGuard, bottomGuard;
        private Image[] frame;
        private Text hullLabel, defenseLabel;
        private int guardCapacity;

        public void Configure(Image hull, Image chip, Image top, Image bottom,
            Image[] armorFrame, Text hp, Text defense, int current, int maximum, int guard, int armor)
        {
            HullBar = hull; ChipBar = chip; topGuard = top; bottomGuard = bottom;
            frame = armorFrame; hullLabel = hp; defenseLabel = defense;
            MaximumHull = Mathf.Max(1, maximum); PermanentArmor = Mathf.Max(0, armor);
            BattleHealthSegmentsGraphic.Attach(hull, MaximumHull);
            guardCapacity = Mathf.Max(1, guard);
            SetHull(current, current);
            SetGuard(guard);
        }

        public void SetHull(float value, float trailingValue)
        {
            DisplayedHull = Mathf.Clamp(value, 0f, MaximumHull);
            if (HullBar != null) HullBar.fillAmount = DisplayedHull / MaximumHull;
            if (ChipBar != null) ChipBar.fillAmount = Mathf.Clamp01(trailingValue / MaximumHull);
            if (hullLabel != null) hullLabel.text = Mathf.CeilToInt(DisplayedHull) + " / " + MaximumHull;
        }

        public void SetGuard(float value)
        {
            DisplayedGuard = Mathf.Max(0f, value);
            guardCapacity = Mathf.Max(guardCapacity, Mathf.CeilToInt(value));
            float fill = Mathf.Clamp01(DisplayedGuard / guardCapacity);
            if (topGuard != null) topGuard.fillAmount = fill;
            if (bottomGuard != null) bottomGuard.fillAmount = fill;
            bool protectedHull = PermanentArmor > 0 || DisplayedGuard > 0.01f;
            if (frame != null)
                foreach (Image edge in frame)
                    if (edge != null) edge.color = new Color(0.92f, 0.97f, 1f, protectedHull ? 0.95f : 0.12f);
            if (defenseLabel != null)
            {
                string armor = PermanentArmor > 0 ? "装甲 " + PermanentArmor + "  ·  " : string.Empty;
                defenseLabel.text = armor + (DisplayedGuard > 0.01f ? "格挡 " + Mathf.CeilToInt(DisplayedGuard) : "无格挡");
                defenseLabel.color = protectedHull ? new Color(0.96f, 0.98f, 1f) : new Color(0.71f, 0.77f, 0.78f);
            }
        }
    }
}
