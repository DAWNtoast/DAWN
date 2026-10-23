using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform departureChartPreview;
        private RawImage departureChartPreviewWater;
        private VoyageChartSurfaceGraphic departureChartPreviewSurface;
        private Text departureChartPreviewRoute;
        private Text departureChartPreviewPorts;
        private Text departureChartPreviewEmpty;
        private RectTransform departureChartPreviewOrigin;
        private RectTransform departureChartPreviewDestination;
        private CoreVoyageMap departureChartPreviewMap;
        private string departureChartPreviewSeed;
        private string departureChartPreviewPort;

        private void BuildDepartureChartPreview(RectTransform selection)
        {
            departureChartPreviewMap = null;
            departureChartPreviewSeed = null;
            departureChartPreviewPort = null;
            departureChartPreviewRoute = CreateText("Departure Chart Route", selection,
                new Vector2(1000f, 32f), new Vector2(0f, 184f), 22, FontStyle.Bold,
                TextAnchor.MiddleCenter, Gold);
            UiLabelBacking.Apply(departureChartPreviewRoute, true);

            departureChartPreview = CreateRect("Departure Chart Preview", selection,
                new Vector2(720f, 405f), new Vector2(0f, -58f));
            departureChartPreviewWater = CreateRawImage("Departure Chart Preview Water", departureChartPreview,
                departureChartPreview.sizeDelta, Vector2.zero, VoyageMapArt.ChartBase, new Rect(0f, 0f, 1f, 1f));
            departureChartPreviewWater.raycastTarget = false;
            RectTransform surface = CreateRect("Departure Chart Preview Surface", departureChartPreview,
                departureChartPreview.sizeDelta, Vector2.zero);
            departureChartPreviewSurface = surface.gameObject.AddComponent<VoyageChartSurfaceGraphic>();
            departureChartPreviewSurface.raycastTarget = false;
            departureChartPreviewOrigin = BuildDepartureChartPreviewPort("Departure Chart Preview Origin", "出发", Teal);
            departureChartPreviewDestination = BuildDepartureChartPreviewPort("Departure Chart Preview Destination", "到达", Gold);

            departureChartPreviewPorts = CreateText("Departure Chart Ports", selection,
                new Vector2(1000f, 42f), new Vector2(0f, -292f), 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, Ink);
            UiLabelBacking.Apply(departureChartPreviewPorts);
            departureChartPreviewEmpty = CreateText("Departure Chart Preview Empty", selection,
                new Vector2(920f, 160f), new Vector2(0f, -58f), 24, FontStyle.Bold,
                TextAnchor.MiddleCenter, Ink);
            UiLabelBacking.Apply(departureChartPreviewEmpty);
            RefreshDepartureChartPreview();
        }

        private RectTransform BuildDepartureChartPreviewPort(string name, string caption, Color tint)
        {
            RectTransform marker = CreateRect(name, departureChartPreview, new Vector2(28f, 28f), Vector2.zero);
            Image halo = CreateImage("Halo", marker, new Vector2(34f, 34f), Vector2.zero,
                new Color(.04f, .025f, .015f, .9f), circleSprite);
            halo.raycastTarget = false;
            Image dot = CreateImage("Marker", marker, new Vector2(20f, 20f), Vector2.zero, tint, circleSprite);
            dot.raycastTarget = false;
            Text label = CreateText("Label", marker, new Vector2(76f, 28f), new Vector2(0f, -28f),
                18, FontStyle.Bold, TextAnchor.MiddleCenter, tint);
            label.text = caption;
            UiLabelBacking.Apply(label, true);
            return marker;
        }

        private void RefreshDepartureChartPreview()
        {
            if (departureChartPreview == null || session == null) return;
            bool mode = DepartureChartModeActive;
            CoreSeaChartDefinition chart = session.SelectedChartDefinition;
            bool show = mode && departureChartValid && chart != null;
            if (show && (departureChartPreviewMap == null || departureChartPreviewSeed != chart.Seed ||
                departureChartPreviewPort != session.CurrentPort))
            {
                departureChartPreviewMap = session.PreviewSelectedSeaChart();
                if (departureChartPreviewMap != null)
                {
                    departureChartPreviewWater.color = departureChartPreviewWater.texture == null
                        ? FallbackWaterColor(chart.RouteIndex, departureChartPreviewMap.Hash)
                        : RouteWaterTint(chart.RouteIndex);
                    departureChartPreviewSurface.Initialize(departureChartPreviewMap, VoyageMapArt, chart.RouteIndex);
                    departureChartPreviewOrigin.anchoredPosition = DepartureChartPreviewPosition(departureChartPreviewMap.Departure);
                    departureChartPreviewDestination.anchoredPosition = DepartureChartPreviewPosition(departureChartPreviewMap.Destination);
                    departureChartPreviewSeed = chart.Seed;
                    departureChartPreviewPort = session.CurrentPort;
                }
            }
            show &= departureChartPreviewMap != null;
            departureChartPreview.gameObject.SetActive(show);
            departureChartPreviewRoute.gameObject.SetActive(show);
            departureChartPreviewPorts.gameObject.SetActive(show);
            departureChartPreviewEmpty.gameObject.SetActive(mode && !show);
            if (show)
            {
                departureChartPreviewRoute.text = departureChartPreviewMap.Route.Name;
                departureChartPreviewPorts.text = "出发港口：" + departureChartPreviewMap.DepartureName +
                    "    →    到达港口：" + departureChartPreviewMap.DestinationName;
            }
            else
            {
                departureChartPreviewEmpty.text = string.Empty;
            }
        }

        private Vector2 DepartureChartPreviewPosition(VoyagePoint point)
        {
            Vector2 size = departureChartPreview.sizeDelta;
            return new Vector2((point.X / departureChartPreviewMap.WorldWidth - .5f) * size.x,
                (point.Y / departureChartPreviewMap.WorldHeight - .5f) * size.y);
        }
    }
}
