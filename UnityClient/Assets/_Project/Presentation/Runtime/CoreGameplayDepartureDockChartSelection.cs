using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly List<Button> departureDockChartRows = new List<Button>();
        private readonly List<CoreSeaChartDefinition> departureDockCharts = new List<CoreSeaChartDefinition>();
        private Text departureDockChartEmpty;

        private void BuildDepartureDockChartRecords(RectTransform selection)
        {
            departureDockChartRows.Clear();
            departureDockCharts.Clear();
            var seeds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DepartureChartRecord record in RecordedDepartureCharts)
            {
                // Availability is a read-only check: browsing must never select a
                // chart, unlock a route, or remove records usable at another port.
                if (record == null || !CoreVoyageSession.TryParseChartSeed(record.seed, out CoreSeaChartDefinition chart) ||
                    !chart.ContainsPort(session.CurrentPort) || !session.CanSelectRoute(chart.RouteIndex, out _) ||
                    !seeds.Add(chart.Seed)) continue;
                departureDockCharts.Add(chart);
            }
            Text heading = CreateText("Departure Dock Chart Records Heading", selection, Vector2.one, Vector2.zero,
                16, FontStyle.Bold, TextAnchor.MiddleLeft, DepartureSkin.Ink);
            heading.text = "可用海图 " + departureDockCharts.Count + " 张 · 点击选择 / 滚动查看更多";
            heading.raycastTarget = false;
            departureDockChartEmpty = CreateText("Departure Dock Chart Records Empty", selection, Vector2.one, Vector2.zero,
                17, FontStyle.Normal, TextAnchor.MiddleCenter, DepartureSkin.Ink);
            departureDockChartEmpty.text = "暂无当前港口可用的已绘海图\n可粘贴种子，或由航海士在航程结算时绘制";
            departureDockChartEmpty.raycastTarget = false;

            RectTransform root = CreateRect("Departure Dock Chart Records", selection, new Vector2(466, 82), Vector2.zero);
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            RectTransform viewport = CreateRect("Viewport", root, Vector2.zero, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-12, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, new Vector2(0, Mathf.Max(82, departureDockCharts.Count * 42 - 4)), Vector2.zero);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            scroll.viewport = viewport;
            scroll.content = content;
            RectTransform bar = CreateRect("Scrollbar", root, new Vector2(8, 0), new Vector2(-4, 0));
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(.29f, .19f, .10f, .18f);
            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = CreateRect("Handle", bar, Vector2.zero, Vector2.zero);
            Image thumb = handle.gameObject.AddComponent<Image>();
            thumb.color = new Color(.35f, .25f, .13f, .75f);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = thumb;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            bar.gameObject.SetActive(departureDockCharts.Count > 2);
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            RectTransform view = departureChartView;
            CoreVoyageSession owner = session;
            int revision = departureChartRevision;
            for (int index = 0; index < departureDockCharts.Count; index++)
            {
                CoreSeaChartDefinition chart = departureDockCharts[index];
                RectTransform row = CreateRect("Departure Dock Chart Record " + index, content,
                    new Vector2(0, 38), new Vector2(0, -index * 42));
                row.anchorMin = new Vector2(0, 1);
                row.anchorMax = Vector2.one;
                row.pivot = new Vector2(.5f, 1);
                Image face = row.gameObject.AddComponent<Image>();
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1.12f, 1.12f, 1.06f);
                colors.pressedColor = new Color(.8f, .85f, .8f);
                button.colors = colors;
                Text label = CreateText("Label", row, new Vector2(434, 36), new Vector2(0, -19),
                    16, FontStyle.Normal, TextAnchor.MiddleLeft, DepartureSkin.Ink);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 1);
                label.raycastTarget = false;
                button.onClick.AddListener(() =>
                {
                    if (!button.IsInteractable() || !button.gameObject.activeInHierarchy ||
                        !DepartureChartViewCurrent(view, owner, revision) || !DepartureChartModeActive) return;
                    departureChartDraft = chart.Seed;
                    departureChartInput.SetTextWithoutNotify(chart.Seed);
                    ValidateDepartureChartDraft(true);
                });
                departureDockChartRows.Add(button);
            }
            scroll.verticalNormalizedPosition = 1;
            RefreshDepartureDockChartRecords();
        }

        private void RefreshDepartureDockChartRecords()
        {
            if (departureChartSelection == null || !UsesDepartureDockScene) return;
            bool inline = DepartureChartModeActive && session.SelectedTreasureMap == null;
            if (departureDockChartEmpty != null)
                departureDockChartEmpty.gameObject.SetActive(inline && departureDockCharts.Count == 0);
            for (int index = 0; index < departureDockChartRows.Count; index++)
            {
                Button row = departureDockChartRows[index];
                if (row == null) continue;
                CoreSeaChartDefinition chart = departureDockCharts[index];
                bool selected = departureChartValid && session.SelectedChartDefinition?.Seed == chart.Seed;
                row.GetComponent<Image>().color = selected ? new Color(.12f, .29f, .25f, .96f) : new Color(1f, .95f, .80f, .60f);
                Text label = row.GetComponentInChildren<Text>();
                label.color = selected ? new Color(1f, .95f, .79f) : DepartureSkin.Ink;
                string shortSeed = chart.Seed.Length > 38 ? chart.Seed.Substring(0, 38) + "…" : chart.Seed;
                label.text = (selected ? "✓ " : "  ") + CoreVoyageSession.Routes[chart.RouteIndex].Name + " · " +
                    session.CurrentPort + " → " + chart.OtherPort(session.CurrentPort) + "\n  <size=11>" + shortSeed + "</size>";
            }
            if (!inline) return;
            if (departureChartPreviewPorts != null && departureChartPreviewMap != null)
                departureChartPreviewPorts.text = departureChartPreviewMap.DepartureName + " → " + departureChartPreviewMap.DestinationName;
            if (departureChartPreviewEmpty != null)
                departureChartPreviewEmpty.text = "选择左侧海图\n查看航线预览";
        }
    }
}
