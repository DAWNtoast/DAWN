using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Readable scene labels without rectangular plates over the artwork.</summary>
    public static class UiLabelBacking
    {
        public static void Apply(Text label, bool keepAccent = false)
        {
            if (label == null) return;
            // Retire plates already saved in authored previews without moving the
            // label or changing the scene's existing interaction hierarchy.
            Transform previous = label.transform.parent == null ? null :
                label.transform.parent.Find(label.name + " Backing");
            Image backing = previous == null ? null : previous.GetComponent<Image>();
            if (backing != null)
            {
                backing.enabled = false;
                backing.raycastTarget = false;
            }
            if (!keepAccent) label.color = new Color(.96f, .92f, .82f, 1f);

            Outline outline = label.GetComponent<Outline>();
            if (outline == null) outline = label.gameObject.AddComponent<Outline>();
            foreach (Shadow effect in label.GetComponents<Shadow>())
                if (effect != outline) effect.enabled = false;
            outline.enabled = true;
            outline.effectColor = new Color(.025f, .018f, .012f, .8f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
