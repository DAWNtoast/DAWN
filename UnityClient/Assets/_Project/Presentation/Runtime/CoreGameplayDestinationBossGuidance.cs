using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildDestinationBossGuidance(RectTransform mapPanel)
        {
            CoreMapNode boss = session.DestinationBossGuidanceNode;
            if (tutorialActive || boss == null) return;
            RectTransform panel = CreateFixedPanel("Destination Boss Guidance", mapPanel,
                new Vector2(810f, 64f), new Vector2(0f, -244f), Html("#182C32"));
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = panelImage.overrideSprite = null;
            panelImage.color = Html("#182C32");
            panelImage.raycastTarget = false;
            Text message = CreateText("Destination Boss Guidance Text", panel, new Vector2(484f, 52f),
                new Vector2(-150f, 0f), 18, FontStyle.Bold, TextAnchor.MiddleLeft, Html("#F2D19A"));
            message.text = "港口封锁中 · 前往击败首领，解除封锁\n" + boss.Encounter.Name;
            message.raycastTarget = false;
            message.horizontalOverflow = HorizontalWrapMode.Wrap;
            message.verticalOverflow = VerticalWrapMode.Truncate;
            Button locate = CreateButton("Locate Blocking Boss", panel, new Vector2(136f, 44f),
                new Vector2(172f, 0f), PanelRaised, Ink, 19);
            locate.GetComponentInChildren<Text>().text = "定位首领";
            locate.interactable = session.Phase == CoreDemoPhase.Sailing && !navigationMotionPlaying;
            locate.onClick.AddListener(() => FocusDestinationBoss(false));
            Button plot = CreateButton("Plot Blocking Boss Course", panel, new Vector2(136f, 44f),
                new Vector2(322f, 0f), Gold, Ink, 19);
            plot.GetComponentInChildren<Text>().text = "规划航线";
            plot.interactable = locate.interactable;
            plot.onClick.AddListener(() => FocusDestinationBoss(true));
        }

        private void FocusDestinationBoss(bool plotCourse)
        {
            if (!isActiveAndEnabled || contentRoot == null || tutorialActive || navigationMotionPlaying ||
                voyageCargoOverlay != null || portSettingsOverlay != null || session.Phase != CoreDemoPhase.Sailing) return;
            CoreMapNode boss = session.DestinationBossGuidanceNode;
            if (boss == null) return;
            if (plotCourse)
            {
                session.SetCourseTo(boss.Position);
                PlaySfx("course_plot", .44f);
                Render();
            }
            // The complete chart is already visible. Locating the highlighted
            // boss keeps the same map bounds, including after a course refresh.
            Transform map = contentRoot.Find("Navigation Chart/Procedural Sea Chart Frame/Map Layer");
            map?.GetComponent<NavigationMapViewport>()?.EnsureVisible(MapPosition(boss.Position), 90f);
        }

        private void ApplyDestinationPortGuidance(Graphic marker, Image halo, string portName, bool destination)
        {
            if (!destination || tutorialActive || !session.DestinationPortDiscovered) return;
            bool blocked = session.IsDestinationBossGuidanceActive;
            if (blocked)
            {
                Color original = marker.color;
                marker.color = new Color(original.r * .46f, original.g * .46f, original.b * .46f, original.a);
                halo.color = new Color(.33f, .39f, .41f, .22f);
            }
            // Keep ordinary marker labels absent; this status belongs only to
            // the destination revealed during the current voyage.
            Text status = CreateText("Destination Port Guidance Label", marker.transform, new Vector2(196f, 28f),
                new Vector2(0f, -46f), 17, FontStyle.Bold, TextAnchor.MiddleCenter,
                blocked ? Html("#8C9998") : Html("#F1DAA9"));
            status.color = blocked ? Html("#8C9998") : Html("#F1DAA9");
            status.text = portName + (blocked ? " · 封锁" : string.Empty);
            status.raycastTarget = false;
            Shadow shadow = status.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .04f, .05f, .95f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        private void DecorateDestinationBossGuidance(Button marker, CoreMapNode node)
        {
            if (tutorialActive || node == null || node != session.DestinationBossGuidanceNode) return;
            RectTransform rect = marker.GetComponent<RectTransform>();
            Image halo = CreateImage("Destination Boss Guidance Halo", marker.transform,
                new Vector2(88f, 88f), Vector2.zero, new Color(1f, .67f, .23f, .34f), circleSprite);
            halo.raycastTarget = false;
            halo.transform.SetAsFirstSibling();
            halo.gameObject.AddComponent<VoyageEventMarkerPulse>().Configure(node.Id * .47f, true);
            Image ring = CreateImage("Destination Boss Guidance Ring", marker.transform,
                rect.sizeDelta + new Vector2(16f, 16f), Vector2.zero, Html("#EFC471"), circleSprite);
            ring.raycastTarget = false;
            ring.transform.SetSiblingIndex(1);
            Image inset = CreateImage("Destination Boss Guidance Inset", marker.transform,
                rect.sizeDelta + new Vector2(8f, 8f), Vector2.zero, Html("#172C32"), circleSprite);
            inset.raycastTarget = false;
            inset.transform.SetSiblingIndex(2);
            Text caption = CreateText("Destination Boss Guidance Caption", marker.transform, new Vector2(236f, 30f),
                new Vector2(0f, -48f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, Html("#FFE2A4"));
            caption.color = Html("#FFE2A4");
            caption.text = "封锁首领 · 点击规划航线";
            caption.raycastTarget = false;
            Shadow shadow = caption.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.02f, .04f, .05f, .95f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
    }
}
