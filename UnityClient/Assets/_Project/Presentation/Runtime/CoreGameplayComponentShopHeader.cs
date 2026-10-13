using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly Dictionary<RectTransform, ShopHeaderPose> componentShopHeaderOriginal =
            new Dictionary<RectTransform, ShopHeaderPose>();

        private struct ShopHeaderPose
        {
            internal Vector2 Position, Size;
            internal int FontSize, Minimum, Maximum;
            internal TextAnchor Alignment;
        }

        private void ApplyComponentShopHeaderLayout(Transform root, bool shop)
        {
            string[] names = { "Return To Port Hub", "Previous Port Facility", "Next Port Facility",
                "Port Facility Title", "Port Facility Port", "Port Facility Time", "Port Facility Gold", "Port Facility Settings" };
            Vector2[] positions = { new Vector2(-800f, 0), new Vector2(-591f, 0), new Vector2(-419f, 0),
                new Vector2(-120f, 0), new Vector2(125f, 0), new Vector2(398f, 0), new Vector2(661f, 0), new Vector2(848f, 0) };
            Vector2[] sizes = { new Vector2(234f, 66f), new Vector2(146f, 58f), new Vector2(174f, 58f),
                new Vector2(266f, 64f), new Vector2(195f, 54f), new Vector2(273f, 40f), new Vector2(225f, 60f), new Vector2(104f, 58f) };
            int[] fonts = { 23, 22, 22, 34, 23, 16, 28, 21 };
            for (int index = 0; index < names.Length; index++)
            {
                RectTransform rect = root.Find(names[index]) as RectTransform;
                if (rect == null) continue;
                Text label = rect.GetComponent<Text>();
                if (label == null) label = rect.GetComponentInChildren<Text>(true);
                RememberShopHeaderPose(rect, rect.GetComponent<Text>());
                if (label != null && label.transform != rect) RememberShopHeaderPose(label.rectTransform, label);
                RestoreShopHeaderPose(rect, rect.GetComponent<Text>());
                if (label != null && label.transform != rect) RestoreShopHeaderPose(label.rectTransform, label);
                if (!shop) continue;
                rect.anchoredPosition = positions[index];
                rect.sizeDelta = sizes[index];
                if (label == null) continue;
                if (label.transform != rect)
                {
                    label.rectTransform.sizeDelta = sizes[index] - new Vector2(20f, 8f);
                    label.rectTransform.anchoredPosition = Vector2.zero;
                }
                label.fontSize = fonts[index];
                label.resizeTextMinSize = Mathf.Max(14, fonts[index] - 4);
                label.resizeTextMaxSize = fonts[index];
                label.alignment = TextAnchor.MiddleCenter;
                if (index == 1) label.text = "酒馆";
                else if (index == 2) label.text = "卡组工坊";
                else if (index == 3) label.text = "部件商店";
            }
        }

        private void RememberShopHeaderPose(RectTransform rect, Text text)
        {
            if (componentShopHeaderOriginal.ContainsKey(rect)) return;
            componentShopHeaderOriginal.Add(rect, new ShopHeaderPose
            {
                Position = rect.anchoredPosition, Size = rect.sizeDelta,
                FontSize = text != null ? text.fontSize : 0,
                Minimum = text != null ? text.resizeTextMinSize : 0,
                Maximum = text != null ? text.resizeTextMaxSize : 0,
                Alignment = text != null ? text.alignment : TextAnchor.MiddleCenter
            });
        }

        private void RestoreShopHeaderPose(RectTransform rect, Text text)
        {
            ShopHeaderPose pose = componentShopHeaderOriginal[rect];
            rect.anchoredPosition = pose.Position;
            rect.sizeDelta = pose.Size;
            if (text == null) return;
            text.fontSize = pose.FontSize;
            text.resizeTextMinSize = pose.Minimum;
            text.resizeTextMaxSize = pose.Maximum;
            text.alignment = pose.Alignment;
        }
    }
}
