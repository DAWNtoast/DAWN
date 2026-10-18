using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // UI Cancel, card gestures and the scene controller can see the same key.
    // Share consumption so their execution order cannot dismiss two layers.
    internal static class UiEscapeInput
    {
        private static int consumedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConsumption() => consumedFrame = -1;

        public static bool IsPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public static bool TryConsume()
        {
            if (consumedFrame == Time.frameCount) return false;
            consumedFrame = Time.frameCount;
            return true;
        }

        public static bool TryCloseDropdown(Transform root)
        {
            if (root == null) return false;
            foreach (Dropdown dropdown in root.GetComponentsInChildren<Dropdown>())
            {
                // uGUI retains the list during its cancel fade, including when
                // EventSystem processed Cancel before the controller's Update.
                Transform list = dropdown.transform.Find("Dropdown List");
                if (list == null || !list.gameObject.activeInHierarchy) continue;
                dropdown.Hide();
                return true;
            }
            return false;
        }
    }
}
