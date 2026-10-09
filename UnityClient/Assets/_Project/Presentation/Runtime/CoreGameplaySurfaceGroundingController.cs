using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Small contact shadows belong to the supporting surface, not to the
        // hoverable object. Hover must never move the object's contact point.
        private void CreateSurfaceContact(string name, Transform parent, Vector2 position, Vector2 size)
        {
            for (int layer = 0; layer < 3; layer++)
            {
                Image shadow = CreateImage(name + " " + layer, parent,
                    size * (1f - layer * 0.19f), position,
                    new Color(0.035f, 0.018f, 0.007f, 0.09f + layer * 0.045f), circleSprite);
                shadow.raycastTarget = false;
            }
        }

        private static void ConfigureGroundedObjectHover(Button button, Image glow, Graphic art, Text label, Color accent)
        {
            button.transition = Selectable.Transition.None;
            if (art != null) button.targetGraphic = art;
            Color originalLabel = label == null ? Color.white : label.color;
            glow.color = Color.clear;
            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                if (!button.interactable) return;
                // Keep the physical art and its contact shadow stable. The old
                // pointer treatment painted a coloured floor ring and brightened
                // the illustration, which read as a detached glow on the room.
                glow.color = Color.clear;
                if (label != null) label.color = new Color(1f, 0.91f, 0.64f);
            });
            trigger.triggers.Add(enter);
            EventTrigger.Entry leave = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            leave.callback.AddListener(_ =>
            {
                glow.color = Color.clear;
                if (label != null) label.color = originalLabel;
            });
            trigger.triggers.Add(leave);
        }
    }
}
