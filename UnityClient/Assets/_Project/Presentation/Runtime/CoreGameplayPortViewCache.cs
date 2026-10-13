using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private PortHeaderView cachedFacilityHeader, cachedHubHeader;
        private RawImage cachedPortBackdrop;
        private Image cachedPortTint;
        private RectTransform portViewCacheRoot;
        private CoreVoyageSession renderedPortSession;
        private string renderedPortName;
        private int renderedPortFacility = int.MinValue;

        private bool CanRetainPortBody => Application.isPlaying && !editorPreviewBuilding &&
            (!tutorialActive || GuidedTutorialRecoveryActive && activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex) &&
            session != null && session.Phase == CoreDemoPhase.Port &&
            ReferenceEquals(renderedPortSession, session) && renderedPortName == session.CurrentPort &&
            renderedPortFacility == activePortFacility;

        private bool TryRefreshPortBody()
        {
            if (!CanRetainPortBody) return false;
            bool refreshed = activePortFacility == PortFacilityArtCatalog.MarketFacilityIndex
                ? TryRefreshMarketFacilityView()
                : activePortFacility == 0 && TryRefreshWarehousePresentation();
            if (!refreshed) return false;
            UpdatePortHeaderReadouts();
            if (tutorialActive) BuildGuidedTutorialOverlay();
            return true;
        }

        private void RecordRenderedPortView()
        {
            renderedPortSession = session != null && session.Phase == CoreDemoPhase.Port ? session : null;
            renderedPortName = renderedPortSession?.CurrentPort;
            renderedPortFacility = renderedPortSession == null ? int.MinValue : activePortFacility;
        }

        private void ClearPhaseContentForRender()
        {
            ReleaseDepartureBasicSupplyView();
            ReleaseWarehouseRetainedView();
            ReleaseMarketRetainedView();
            // Editor previews are independent, serializable snapshots. Only live
            // gameplay retains a bounded set of two headers and one backdrop.
            if (!Application.isPlaying || editorPreviewBuilding)
            {
                ClearChildren(contentRoot);
                return;
            }
            bool port = session != null && session.Phase == CoreDemoPhase.Port;
            ParkUnusedVoyageHeader();
            ParkPortView(cachedFacilityHeader, !port || activePortFacility < 0);
            ParkPortView(cachedHubHeader, !port || activePortFacility >= 0);
            ParkPortView(cachedPortBackdrop, !port);
            ParkPortView(cachedPortTint, !port);
            for (int index = contentRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = contentRoot.GetChild(index);
                if (IsRetainedPortChrome(child) || IsRetainedVoyageHeader(child)) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private bool IsRetainedPortChrome(Transform child) =>
            (cachedFacilityHeader != null && child == cachedFacilityHeader.transform) ||
            (cachedHubHeader != null && child == cachedHubHeader.transform) ||
            (cachedPortBackdrop != null && child == cachedPortBackdrop.transform) ||
            (cachedPortTint != null && child == cachedPortTint.transform);

        private void ParkPortView(Component view, bool park)
        {
            if (view == null || !park) return;
            view.gameObject.SetActive(false);
            // Keep the inexpensive hierarchy for reuse, but do not let an
            // invisible header/background retain a previous port's large art.
            // The binding methods restore these references before activation.
            if (view is RawImage backdrop) backdrop.texture = null;
            if (view is PortHeaderView header && header.Frame != null) header.Frame.sprite = null;
            if (view.transform.parent != contentRoot) return;
            if (portViewCacheRoot == null)
            {
                portViewCacheRoot = CreateRect("Port View Cache", transform, Vector2.zero, Vector2.zero);
                portViewCacheRoot.gameObject.SetActive(false);
            }
            view.transform.SetParent(portViewCacheRoot, false);
        }

        private RawImage BuildReusablePortBackdrop(string name, Texture texture, Color color, string tintName, Color tintColor,
            bool fullSurface = false)
        {
            bool retain = Application.isPlaying && !editorPreviewBuilding;
            // Collection-room paintings contain the whole 16:9 composition. Their
            // cabinet shelves and ledger frame use screen coordinates beneath the
            // overlaid header, unlike the older cropped facility compositions.
            Vector2 size = fullSurface ? PhaseSurfaceLayout.ContentSize : PhaseSurfaceLayout.BackgroundSize;
            Vector2 position = fullSurface ? Vector2.zero : PhaseSurfaceLayout.BackgroundPosition;
            Rect uv = fullSurface ? new Rect(0f, 0f, 1f, 1f) : PhaseSurfaceLayout.BackgroundUv(texture);
            RawImage backdrop = retain ? cachedPortBackdrop : null;
            if (backdrop == null)
                backdrop = CreateRawImage(name, contentRoot, size, position, texture, uv);
            backdrop.name = name;
            backdrop.transform.SetParent(contentRoot, false);
            backdrop.transform.SetAsFirstSibling();
            // The same RawImage is reused when moving between warehouse and port;
            // always restore geometry, not only the new texture and UV rectangle.
            backdrop.rectTransform.sizeDelta = size;
            backdrop.rectTransform.anchoredPosition = position;
            backdrop.texture = texture;
            backdrop.uvRect = uv;
            backdrop.color = color;
            backdrop.raycastTarget = false;
            backdrop.gameObject.SetActive(true);
            Image tint = retain ? cachedPortTint : null;
            if (tint == null) tint = CreateStretchPanel(tintName, contentRoot, tintColor).GetComponent<Image>();
            tint.name = tintName;
            tint.transform.SetParent(contentRoot, false);
            tint.transform.SetSiblingIndex(1);
            tint.color = tintColor;
            tint.raycastTarget = false;
            tint.gameObject.SetActive(true);
            if (retain)
            {
                cachedPortBackdrop = backdrop;
                cachedPortTint = tint;
            }
            return backdrop;
        }

        private bool TryBuildPrefabPortHeader(bool hub, int facility = -1)
        {
            bool retain = Application.isPlaying && !editorPreviewBuilding;
            PortHeaderView view = retain ? (hub ? cachedHubHeader : cachedFacilityHeader) : null;
            if (view == null)
            {
                PortHeaderView prefab = Resources.Load<PortHeaderView>(hub ? "UI/PortHubHeader" : "UI/PortFacilityHeader");
                // The procedural path remains available to the initial exporter
                // and resource-stripped developer fixtures.
                if (prefab == null || !prefab.IsComplete(hub)) return false;
                view = Instantiate(prefab, contentRoot, false);
                view.name = hub ? "Port Hub" : "Port Facility Header";
                if (retain)
                {
                    // Isolate the frequently changing body from the header's
                    // geometry, without changing draw order or modal layering.
                    view.gameObject.AddComponent<Canvas>();
                    view.gameObject.AddComponent<GraphicRaycaster>();
                    if (hub) cachedHubHeader = view;
                    else cachedFacilityHeader = view;
                }
            }
            // A tutorial render retires its guarded events. Rebind retained
            // controls before this frame's tutorial layer applies new guards.
            view.Settings.onClick = new Button.ButtonClickedEvent();
            view.Settings.onClick.AddListener(OpenPortSettings);
            if (!hub)
            {
                view.Back.onClick = new Button.ButtonClickedEvent();
                view.Back.onClick.AddListener(ReturnFromCachedFacility);
                view.Previous.onClick = new Button.ButtonClickedEvent();
                view.Previous.onClick.AddListener(PreviousCachedFacility);
                view.Next.onClick = new Button.ButtonClickedEvent();
                view.Next.onClick.AddListener(NextCachedFacility);
            }
            view.transform.SetParent(contentRoot, false);
            view.transform.SetAsLastSibling();
            EnsurePortHeaderArt();
            PortHeaderTheme theme = portHeaderArt.ForPort(session.CurrentPort);
            RectTransform rect = (RectTransform)view.transform;
            float height = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -height * .5f);
            view.Frame.sprite = theme.Frame;
            view.Frame.pixelsPerUnitMultiplier = theme.FrameHeight / height;
            view.Gold.color = theme.Accent;
            if (!hub)
            {
                view.Back.GetComponentInChildren<Text>(true).text = FacilityReturnCaption;
                view.Title.text = PortFacilityArtCatalog.RegionalFacilityTitle(facility, session.CurrentPort, PortFacilityNames[facility]);
                view.Title.color = theme.Accent;
                int previous = (facility + PortFacilityNames.Length - 1) % PortFacilityNames.Length;
                int next = (facility + 1) % PortFacilityNames.Length;
                view.PreviousLabel.text = "← " + PortFacilityArtCatalog.RegionalFacilityTitle(previous, session.CurrentPort, PortFacilityNames[previous]);
                view.NextLabel.text = PortFacilityArtCatalog.RegionalFacilityTitle(next, session.CurrentPort, PortFacilityNames[next]) + " →";
            }
            foreach (Outline outline in view.GetComponentsInChildren<Outline>(true))
                if (outline.GetComponent<Button>() != null)
                    outline.effectColor = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, .18f);
            portHeaderTime = view.Time;
            portHeaderPort = view.Port;
            portHeaderGold = view.Gold;
            if (!hub) ApplyComponentShopHeaderLayout(view.transform, facility == PortFacilityArtCatalog.ComponentShopFacilityIndex);
            UpdatePortHeaderReadouts(true);
            view.gameObject.SetActive(true);
            return true;
        }

        private void RefreshFacilityReturnCaption()
        {
            Transform back = contentRoot.Find("Port Facility Header/Return To Port Hub");
            Text label = back != null ? back.GetComponentInChildren<Text>(true) : null;
            if (label != null) label.text = FacilityReturnCaption;
        }

        private void ReturnFromCachedFacility()
        {
            if (BlockIncompleteDeckBuilderNavigation()) return;
            if (pendingMarketPurchase != null) return;
            if (TryReturnToWorkshopSpecialCards()) return;
            if (IsTavernDossierOpen)
            {
                ResetTavernDossierView();
                portNotice = "请选择要了解的特殊船员。";
                Render();
                return;
            }

            if (IsComponentDossierOpen)
            {
                componentDossierOfferId = null;
                componentDossierPage = 0;
                selectedComponentShopOfferId = null;
                portNotice = "已返回" + session.CurrentPort + "部件商店陈列区。";
                Render();
                return;
            }

            bool returnToDeparture = HasDepartureReturn && activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex;
            ResetCachedFacilityNavigation();
            activePortFacility = returnToDeparture ? PortFacilityArtCatalog.VoyageFacilityIndex : -1;
            portNotice = returnToDeparture ? "继续配置出航。" : "选择港口设施。";
            Render();
        }
        private void PreviousCachedFacility() => NavigateCachedFacility(-1);
        private void NextCachedFacility() => NavigateCachedFacility(1);
        private void NavigateCachedFacility(int delta)
        {
            if (BlockIncompleteDeckBuilderNavigation()) return;
            if (pendingMarketPurchase != null) return;
            ResetCachedFacilityNavigation();
            activePortFacility = (activePortFacility + PortFacilityNames.Length + delta) % PortFacilityNames.Length;
            Render();
        }
        private void ResetCachedFacilityNavigation()
        {
            ClearWorkshopSpecialCardReturn();
            ClearDepartureReturn();
            ResetCardWorkshopView();
            ResetComponentShopView();
            investmentSupplyPanelOpen = false;
        }
    }
}

