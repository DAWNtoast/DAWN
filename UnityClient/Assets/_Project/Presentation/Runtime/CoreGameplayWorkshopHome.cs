using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Both the painting and its furniture use the fixed game viewport, including letterboxing.
        // Coordinates below are measured on workshop_home_v1.png (top-left pixel origin).
        private static readonly Vector2 WorkshopHomeSourceSize = new Vector2(1672f, 941f);

        private void BuildCardWorkshopHome()
        {
            RectTransform room = CreateRect("Workshop Home Panel", contentRoot,
                PhaseSurfaceLayout.ContentSize, Vector2.zero);
            BuildWorkshopEntrance(room, WorkshopPage.Collection, "查看已有卡牌",
                new Rect(36f, 286f, 430f, 410f), new Vector2(236f, 620f),
                new[] { new Vector2(.04f, 1f), new Vector2(.79f, 1f), new Vector2(1f, .38f),
                    new Vector2(.92f, .06f), new Vector2(.08f, 0f), new Vector2(0f, .38f) });
            BuildWorkshopEntrance(room, WorkshopPage.DeckBuilder, "组建牌组",
                new Rect(470f, 370f, 420f, 300f), new Vector2(690f, 562f),
                new[] { new Vector2(.2f, 1f), new Vector2(.87f, 1f), new Vector2(1f, .58f),
                    new Vector2(.84f, .1f), new Vector2(.17f, 0f), new Vector2(0f, .6f) });
            BuildWorkshopEntrance(room, WorkshopPage.Packs, "打开卡包",
                new Rect(900f, 400f, 395f, 445f), new Vector2(1086f, 747f),
                new[] { new Vector2(.31f, 1f), new Vector2(.93f, .9f), new Vector2(1f, .26f),
                    new Vector2(.81f, 0f), new Vector2(.06f, .16f), new Vector2(0f, .63f) });
            BuildWorkshopEntrance(room, WorkshopPage.Upgrade, "卡牌强化",
                new Rect(1320f, 365f, 345f, 410f), new Vector2(1482f, 667f),
                new[] { new Vector2(.12f, .87f), new Vector2(.82f, 1f), new Vector2(1f, .66f),
                    new Vector2(.91f, .12f), new Vector2(.18f, 0f), new Vector2(0f, .48f) });
        }

        private static Vector2 WorkshopHomePosition(Vector2 pixel) => new Vector2(
            (pixel.x / WorkshopHomeSourceSize.x - .5f) * PhaseSurfaceLayout.ContentSize.x,
            (.5f - pixel.y / WorkshopHomeSourceSize.y) * PhaseSurfaceLayout.ContentSize.y);

        private void BuildWorkshopEntrance(RectTransform room, WorkshopPage page, string title,
            Rect sourceBounds, Vector2 labelPixel, Vector2[] polygon)
        {
            Vector2 size = Vector2.Scale(sourceBounds.size, new Vector2(
                PhaseSurfaceLayout.ContentSize.x / WorkshopHomeSourceSize.x,
                PhaseSurfaceLayout.ContentSize.y / WorkshopHomeSourceSize.y));
            Vector2 center = WorkshopHomePosition(sourceBounds.center);
            RectTransform region = CreateRect("Workshop Entrance " + page, room, size, center);
            WorkshopSceneHotspot hotspot = region.gameObject.AddComponent<WorkshopSceneHotspot>();
            Button button = region.gameObject.AddComponent<Button>();
            // Match the port landmark nameplate dimensions and typography for all four workstations.
            Vector2 signSize = new Vector2(Mathf.Max(168f, title.Length * 22f + 62f), 48f);
            // Lift every sign by one nameplate height so it sits closer to its workstation.
            Vector2 signPosition = WorkshopHomePosition(labelPixel) - center + Vector2.up * 48f;
            RectTransform plate = CreateRect("Workshop Entrance Nameplate " + page, region,
                signSize, signPosition);
            PortNameplateGraphic sign = plate.gameObject.AddComponent<PortNameplateGraphic>();
            sign.color = Color.white;
            sign.raycastTarget = false;
            Text label = CreateText("Workshop Entrance Label " + page, region,
                new Vector2(signSize.x - 54f, 34f),
                signPosition + new Vector2(16f, 1.5f),
                22, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            label.text = title;
            label.raycastTarget = false;
            UiLabelBacking.Apply(label);
            hotspot.Configure(button, label, polygon, sign);
            button.onClick.AddListener(() =>
            {
                if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable() ||
                    activePortFacility != 4 || CurrentWorkshopPage != WorkshopPage.Home) return;
                SelectWorkshopPage(page);
            });
        }
    }
}
