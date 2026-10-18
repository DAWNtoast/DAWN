using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void ArrangeBattleHand(RectTransform hand)
        {
            // Both the naval chart and boarding deck stay clear above the hand.
            // Resting cards tuck below the viewport; inspection reveals the face.
            hand.sizeDelta = new Vector2(1120f, 200f);
            hand.anchoredPosition = new Vector2(0f, -350f);
            int count = retainedBattleHandViews.Count;
            if (count == 0) return;

            const float scale = .60f;
            float halfSpread = Mathf.Min(18f, (count - 1) * 4.5f);
            float outerWidth = scale * (CardFaceLayout.FrameSize.x * Mathf.Cos(halfSpread * Mathf.Deg2Rad) +
                CardFaceLayout.FrameSize.y * Mathf.Sin(halfSpread * Mathf.Deg2Rad));
            float step = count < 2 ? 0f : Mathf.Min(142f, (hand.rect.width - outerWidth - 16f) / (count - 1));
            for (int index = 0; index < count; index++)
            {
                RetainedBattleHandCard view = retainedBattleHandViews[index];
                RectTransform rect = (RectTransform)view.Button.transform;
                float t = count < 2 ? 0f : index * 2f / (count - 1) - 1f;
                float angle = -t * halfSpread;
                rect.anchoredPosition = new Vector2((index - (count - 1) * .5f) * step,
                    hand.rect.yMin + 74f - 24f * t * t);
                rect.localScale = Vector3.one * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                if (view.Hover != null) view.Hover.SetRestingPose(scale, rect.localRotation, true);
            }
        }
    }
}
