#if UNITY_EDITOR
using System;
using System.Linq;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class TransparentBackingCapture
    {
        // The caller creates an isolated production Settlement editor preview.
        // Seeking its presentation controllers never confirms or grants rewards.
        private static bool ArrangeSettlement(GameObject host, string variant)
        {
            if (variant != "SettlementCaption" && variant != "SettlementProtection" &&
                variant != "SettlementChest") return false;
            if (Application.isPlaying)
                throw new InvalidOperationException("Settlement captures require a stopped editor.");
            if (host == null || !host.activeInHierarchy)
                throw new InvalidOperationException("An active settlement preview host is required.");

            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null)
                throw new InvalidOperationException("The settlement preview has no production controller/session.");
            decimal goldBefore = controller.Session.ProfileGold;
            var phaseBefore = controller.Session.Phase;
            var reveal = host.GetComponentsInChildren<SettlementRevealController>(true).SingleOrDefault();
            var sequence = host.GetComponentsInChildren<SettlementChestSequenceController>(true).SingleOrDefault();
            if (reveal == null || sequence == null)
                throw new InvalidOperationException("Settlement preview did not create its reveal and chest controllers.");

            reveal.CompleteImmediately();
            if (variant == "SettlementChest")
            {
                // Rewards finish at least .75 seconds before the .55-second fade.
                // Capture the closing hold: every icon and exact amount is landed,
                // while the collected strip is still at its full production alpha.
                float elapsed = sequence.Duration - SettlementChestSequenceController.FadeDuration - .15f;
                sequence.SetElapsed(Mathf.Max(0f, elapsed));
                if (sequence.IsComplete || sequence.Phase != SettlementChestSequencePhase.Closing)
                    throw new InvalidOperationException("Chest capture did not reach the visible collected-reward hold.");
                RequireSettlementBacking(host, "Settlement Collected Shade");
                var amounts = host.GetComponentsInChildren<Text>(false)
                    .Where(text => text.name.StartsWith("Settlement Chest Item Amount ", StringComparison.Ordinal))
                    .ToArray();
                if (amounts.Length == 0 || amounts.Any(text => string.IsNullOrEmpty(text.text) ||
                    text.text == "+0" || SettlementCaptureAlpha(text.transform) < .99f))
                    throw new InvalidOperationException("The collected strip must show fully revealed nonzero reward amounts.");
            }
            else
            {
                sequence.CompleteImmediately();
                RequireSettlementBacking(host, variant == "SettlementCaption"
                    ? "Settlement Harbor Caption Shade" : "Settlement Protection Tint");
            }

            if (controller.Session.ProfileGold != goldBefore || controller.Session.Phase != phaseBefore)
                throw new InvalidOperationException("Settlement presentation unexpectedly changed gameplay state.");
            Canvas.ForceUpdateCanvases();
            return true;
        }

        private static void RequireSettlementBacking(GameObject host, string name)
        {
            Image image = host.GetComponentsInChildren<Image>(true).SingleOrDefault(item => item.name == name);
            if (image == null || !image.enabled || !image.gameObject.activeInHierarchy || image.color.a <= .01f ||
                SettlementCaptureAlpha(image.transform) < .99f)
                throw new InvalidOperationException("Settlement backing is not visible: " + name);
            if (image.sprite != null || image.overrideSprite != null)
                throw new InvalidOperationException("Settlement backing no longer uses a plain rectangle: " + name);
        }

        private static float SettlementCaptureAlpha(Transform target)
        {
            float alpha = 1f;
            for (Transform at = target; at != null; at = at.parent)
            {
                CanvasGroup group = at.GetComponent<CanvasGroup>();
                if (group != null) alpha *= group.alpha;
            }
            return alpha;
        }
    }
}
#endif
