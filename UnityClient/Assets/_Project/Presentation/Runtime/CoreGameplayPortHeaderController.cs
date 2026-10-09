using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private PortHeaderArtCatalog portHeaderArt;
        private Text portHeaderTime;
        private Text portHeaderPort;
        private Text portHeaderGold;
        private long displayedPortMinute = -1;
        private decimal displayedPortGold = -1m;
        private string displayedHeaderPort;

        private void EnsurePortHeaderArt()
        {
            if (portHeaderArt == null) portHeaderArt = new PortHeaderArtCatalog();
        }

        private void BuildPortHubHeader()
        {
            // Keep the hub's established root name for previews and navigation.
            // There is no facility to return from or cycle while on this page.
            RectTransform header = CreateRegionalPortHeader("Port Hub", "Port Hub Frame", out PortHeaderTheme theme);
            BuildPortHeaderReadouts(header, theme, true);
        }

        /// <summary>
        /// Creates the shared carved wood header used by voyage and battle screens.
        /// Its frame follows the voyage departure port, so the route keeps its
        /// visual identity after the player reaches another port.
        /// </summary>
        private RectTransform CreateVoyageHeader(string headerName, string frameName,
            string title, out PortHeaderTheme theme)
        {
            if (headerTitle != null && headerTitle.transform.parent != null)
            {
                // Keep the legacy Header object active for editor preview compatibility,
                // but remove its visuals so the regional carved frame is the only surface rendered.
                Transform legacyHeader = headerTitle.transform.parent;
                Image legacyImage = legacyHeader.GetComponent<Image>();
                if (legacyImage != null) legacyImage.enabled = false;
                headerTitle.enabled = false;
                if (headerStatus != null) headerStatus.enabled = false;
            }

            EnsurePortHeaderArt();
            string departurePort = session != null && session.Map != null &&
                !string.IsNullOrEmpty(session.Map.DepartureName)
                ? session.Map.DepartureName
                : session == null ? string.Empty : session.CurrentPort;
            theme = portHeaderArt.ForPort(departurePort);

            float headerHeight = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            RectTransform header = CreateFixedPanel(headerName, contentRoot,
                new Vector2(0f, headerHeight), Vector2.zero, Html("#211A13"));
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.anchoredPosition = new Vector2(0f, -headerHeight * 0.5f);
            header.GetComponent<Image>().raycastTarget = false;

            Image frame = CreateImage(frameName, header, Vector2.zero, Vector2.zero,
                Color.white, theme.Frame);
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = theme.FrameHeight / headerHeight;
            frame.raycastTarget = false;

            Text voyageTitle = CreateText(headerName + " Title", header,
                new Vector2(300f, 60f), new Vector2(-710f, 0f), 32,
                FontStyle.Bold, TextAnchor.MiddleLeft, theme.Accent);
            voyageTitle.text = title;
            GameReadability.Heading(voyageTitle);
            voyageTitle.raycastTarget = false;

            Text voyageStatus = CreateText(headerName + " Status", header,
                session != null && session.Phase == ShatteredSail.Core.Gameplay.CoreDemoPhase.Battle
                    ? new Vector2(420f, 60f) : new Vector2(673.135f, 60f),
                session != null && session.Phase == ShatteredSail.Core.Gameplay.CoreDemoPhase.Battle
                    ? new Vector2(660f, 0f) : new Vector2(511f, 0f), 18,
                FontStyle.Normal, TextAnchor.MiddleRight, Ink);
            voyageStatus.text = BuildHeaderStatus();
            voyageStatus.raycastTarget = false;
            GameReadability.Body(voyageStatus, 18);
            return header;
        }

        private void BuildVoyageHeader(string title)
        {
            RectTransform header = CreateVoyageHeader("Voyage Header", "Voyage Header Frame", title,
                out PortHeaderTheme theme);
            if (session == null || (session.Phase != ShatteredSail.Core.Gameplay.CoreDemoPhase.Sailing &&
                session.Phase != ShatteredSail.Core.Gameplay.CoreDemoPhase.Encounter)) return;
            // All voyage controls occupy the middle 68 pixels; the tutorial
            // objective and instruction retain their separate upper/lower bands.
            BuildVoyageHeaderTime(header);
            Button cargo = CreatePortHeaderButton("Voyage Cargo", header, new Vector2(144f, 64f),
                Vector2.zero, "船舱", 22, theme.Accent);
            cargo.onClick.AddListener(OpenVoyageCargo);
            Button settings = CreatePortHeaderButton("Voyage Settings", header, new Vector2(128f, 64f),
                Vector2.zero, "设置", 22, theme.Accent);
            settings.onClick.AddListener(OpenPortSettings);
        }

        private void BringVoyageHeaderToFront()
        {
            if (contentRoot == null) return;
            Transform header = contentRoot.Find("Voyage Header");
            if (header != null) header.SetAsLastSibling();
        }
        private void BringBattleReadoutsToFront()
        {
            if (contentRoot == null) return;
            Transform readouts = contentRoot.Find("Battle Readouts Overlay");
            if (readouts != null) readouts.SetAsLastSibling();
        }
        private RectTransform CreateRegionalPortHeader(string headerName, string frameName, out PortHeaderTheme theme)
        {
            if (headerTitle != null && headerTitle.transform.parent != null)
                headerTitle.transform.parent.gameObject.SetActive(false);
            EnsurePortHeaderArt();
            theme = portHeaderArt.ForPort(session.CurrentPort);
            float headerHeight = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            RectTransform header = CreateFixedPanel(headerName, contentRoot,
                new Vector2(0f, headerHeight), Vector2.zero, Html("#211A13"));
            // Both hub and facility bars meet the same scene edges. Do not move
            // the backdrop or existing facility hotspots to make room for the bar.
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.anchoredPosition = new Vector2(0f, -headerHeight * 0.5f);
            header.GetComponent<Image>().raycastTarget = false;
            // Opaque wood seals the transparent/antialiased corners of the frame.
            Image frame = CreateImage(frameName, header, Vector2.zero, Vector2.zero, Color.white, theme.Frame);
            frame.rectTransform.anchorMin = Vector2.zero;
            frame.rectTransform.anchorMax = Vector2.one;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = theme.FrameHeight / headerHeight;
            frame.raycastTarget = false;
            return header;
        }

        private void BuildPortHeaderReadouts(RectTransform header, PortHeaderTheme theme, bool hub)
        {
            // Keep one live data/settings path. The hub spreads its four items
            // across the bar instead of leaving the removed navigation half blank.
            string prefix = hub ? "Port Hub " : "Port Facility ";
            portHeaderTime = CreateText(prefix + "Time", header, new Vector2(hub ? 360f : 304f, 60f),
                new Vector2(hub ? -660f : 88f, 0f), 20, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            portHeaderPort = CreateText(prefix + "Port", header, new Vector2(hub ? 300f : 166f, 60f),
                new Vector2(hub ? -180f : 360f, 0f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            portHeaderGold = CreateText(prefix + "Gold", header, new Vector2(hub ? 300f : 234f, 60f),
                new Vector2(hub ? 300f : 596f, 0f), 24, FontStyle.Bold, TextAnchor.MiddleCenter, theme.Accent);
            foreach (Text readout in new[] { portHeaderTime, portHeaderPort, portHeaderGold })
            {
                readout.raycastTarget = false;
                GameReadability.Body(readout, 22);
                readout.rectTransform.sizeDelta = new Vector2(readout.rectTransform.sizeDelta.x, 74f);
            }
            Button settings = CreatePortHeaderButton(prefix + "Settings", header, new Vector2(138f, 58f),
                new Vector2(hub ? 780f : 826f, 0f), "设置", 22, theme.Accent);
            settings.onClick.AddListener(OpenPortSettings);
            if (!hub)
            {
                Text title = header.Find("Port Facility Title")?.GetComponent<Text>();
                GameReadability.Heading(title);
                if (GameReadability.Level > 0)
                {
                    SetHeaderReadingBox(header, "Return To Port Hub", new Vector2(184f, 58f + 60f * (GameReadability.Scale - 1f)), -838f);
                    SetHeaderReadingBox(header, "Previous Port Facility", new Vector2(228f, 58f + 60f * (GameReadability.Scale - 1f)), -622f);
                    SetHeaderReadingBox(header, "Port Facility Title", new Vector2(300f, 96f), -348f);
                    SetHeaderReadingBox(header, "Next Port Facility", new Vector2(228f, 58f + 60f * (GameReadability.Scale - 1f)), -74f);
                    SetHeaderReadingBox(header, prefix + "Time", new Vector2(304f, 74f), 202f);
                    SetHeaderReadingBox(header, prefix + "Port", new Vector2(174f, 74f), 451f);
                    SetHeaderReadingBox(header, prefix + "Gold", new Vector2(234f, 74f), 665f);
                    SetHeaderReadingBox(header, prefix + "Settings", settings.GetComponent<RectTransform>().sizeDelta, 861f);
                }
            }
            if (tutorialActive) FitPortReadoutsBetweenTutorialBands(header);
            UpdatePortHeaderReadouts(true);
        }

        private static void FitPortReadoutsBetweenTutorialBands(RectTransform header)
        {
            float bottom = -header.rect.height * .5f + 6f + 32f * GameReadability.Scale + 2f;
            float top = header.rect.height * .5f - 8f - 28f * GameReadability.Scale - 2f;
            float center = (top + bottom) * .5f;
            foreach (Transform child in header)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null || (child.GetComponent<Text>() == null && child.GetComponent<Button>() == null)) continue;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Min(rect.rect.height, top - bottom));
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, center);
                Text caption = child.GetComponent<Button>()?.GetComponentInChildren<Text>();
                if (caption != null) caption.rectTransform.sizeDelta = rect.sizeDelta - new Vector2(20f, 10f);
            }
        }

        private static void SetHeaderReadingBox(Transform header, string name, Vector2 size, float x)
        {
            RectTransform rect = header.Find(name) as RectTransform;
            if (rect == null) return;
            rect.sizeDelta = size; rect.anchoredPosition = new Vector2(x, 0f);
            Text caption = rect.GetComponent<Button>()?.GetComponentInChildren<Text>();
            if (caption != null) caption.rectTransform.sizeDelta = size - new Vector2(20f, 10f);
        }

        private Button CreatePortHeaderButton(string name, Transform parent, Vector2 size,
            Vector2 position, string caption, int fontSize, Color accent)
        {
            Color resting = new Color(0.09f, 0.055f, 0.026f, 0.22f);
            size.y = Mathf.Max(size.y, Mathf.Max(64f, 58f + 60f * (GameReadability.Scale - 1f)));
            Button button = CreateButton(name, parent, size, position, resting, Ink, fontSize);
            Image face = button.GetComponent<Image>();
            WoodButtonSkin.Apply(button, resting);
            Text label = button.GetComponentInChildren<Text>();
            label.text = caption;
            GameReadability.Body(label, Mathf.Max(22, fontSize));
            label.raycastTarget = false;
            Outline edge = face.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(accent.r, accent.g, accent.b, 0.18f);
            edge.effectDistance = new Vector2(1f, -1f);
            return button;
        }

        private void UpdatePortHeaderReadouts(bool force = false)
        {
            if (session == null || portHeaderTime == null || portHeaderGold == null || portHeaderPort == null) return;
            DateTime now = session.GameDateTime;
            long minute = now.Ticks / TimeSpan.TicksPerMinute;
            if (force || minute != displayedPortMinute)
            {
                portHeaderTime.text = "<size=" + GameReadability.FontSize(18) + ">游戏日期</size>\n" + now.ToString("yyyy-MM-dd HH:mm");
                displayedPortMinute = minute;
            }
            if (force || displayedHeaderPort != session.CurrentPort)
            {
                portHeaderPort.text = session.CurrentPort;
                displayedHeaderPort = session.CurrentPort;
            }
            if (force || displayedPortGold != session.ProfileGold)
            {
                portHeaderGold.text = "<size=" + GameReadability.FontSize(18) + ">总金币</size>\n" + PortMarketCatalog.FormatPrice(session.ProfileGold);
                displayedPortGold = session.ProfileGold;
            }
        }
    }
}


