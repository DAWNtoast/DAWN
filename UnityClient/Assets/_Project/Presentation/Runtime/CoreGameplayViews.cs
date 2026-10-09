using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private UiView portUiView, sailingUiView, encounterUiView, battleUiView, settlementUiView;
        private UiView currentUiView;

        private UiView SelectCurrentUiView()
        {
            if (session == null) return null;
            switch (session.Phase)
            {
                case CoreDemoPhase.Port:
                    return portUiView ?? (portUiView = new UiView(BuildPort,
                        () => portHeaderTime == null ? null : portHeaderTime.transform.parent,
                        UiRefreshRegion.Header, TryRefreshPortUiHeader));
                case CoreDemoPhase.Sailing:
                    return sailingUiView ?? (sailingUiView = new UiView(BuildSailing,
                        () => UiId.FindOrLegacyName(contentRoot, "Navigation Chart"), UiRefreshRegion.None));
                case CoreDemoPhase.Encounter:
                    return encounterUiView ?? (encounterUiView = new UiView(() =>
                    {
                        BuildSailing();
                        BuildEncounterOverlay();
                    }, () => UiId.FindOrLegacyName(contentRoot, "Navigation Chart"), UiRefreshRegion.None));
                case CoreDemoPhase.Battle:
                    return battleUiView ?? (battleUiView = new UiView(BuildBattleOverlay,
                        () => retainedBattleRoot, UiRefreshRegion.Battle, _ => TryRefreshRetainedBattleView()));
                case CoreDemoPhase.Settlement:
                    return settlementUiView ?? (settlementUiView = new UiView(BuildSettlement,
                        () => UiId.FindOrLegacyName(contentRoot, "Settlement Card"), UiRefreshRegion.None));
                default:
                    return null;
            }
        }

        private UiViewState CurrentUiViewState() => new UiViewState(session, session.CurrentPort,
            session.Phase == CoreDemoPhase.Port ? activePortFacility : 0,
            GameReadability.Level * 2 + (tutorialActive ? 1 : 0), contentRoot.rect.size);

        private void InvalidateCurrentUiView()
        {
            currentUiView?.Invalidate();
            currentUiView = null;
        }

        private void BuildCurrentUiView()
        {
            currentUiView = SelectCurrentUiView();
            currentUiView?.Build(contentRoot, CurrentUiViewState());
        }

        /// <summary>
        /// True means the requested region retained its objects. All other requests
        /// use the established full Render path, including its tutorial/modal lifecycle.
        /// Page-specific retention beyond header and battle is a later migration.
        /// </summary>
        private bool RequestUiViewRefresh(UiRefreshRegion dirty)
        {
            if (profileSaveBlocked || session == null || contentRoot == null) return false;
            if (TryRefreshCurrentUiView(dirty)) return true;
            Render();
            return false;
        }

        private bool TryRefreshCurrentUiView(UiRefreshRegion dirty) =>
            !profileSaveBlocked && session != null && contentRoot != null && currentUiView != null &&
            ReferenceEquals(currentUiView, SelectCurrentUiView()) &&
            currentUiView.TryRefresh(contentRoot, CurrentUiViewState(), dirty);

        private bool TryRefreshPortUiHeader(UiRefreshRegion dirty)
        {
            if (session.Phase != CoreDemoPhase.Port || portHeaderTime == null || portHeaderPort == null ||
                portHeaderGold == null || !portHeaderTime.gameObject.activeInHierarchy ||
                !portHeaderPort.gameObject.activeInHierarchy || !portHeaderGold.gameObject.activeInHierarchy ||
                !portHeaderTime.transform.IsChildOf(contentRoot) ||
                portHeaderPort.transform.parent != portHeaderTime.transform.parent ||
                portHeaderGold.transform.parent != portHeaderTime.transform.parent) return false;
            UpdatePortHeaderReadouts();
            return true;
        }

        private void UpdateUiViewHeader()
        {
            // A frame-driven readout update must never replace controls, restart
            // transitions or dismiss a quote/modal. Model/layout mismatches await
            // an explicit refresh request; authored previews keep their lifecycle too.
            if (session != null && session.Phase == CoreDemoPhase.Port &&
                TryRefreshCurrentUiView(UiRefreshRegion.Header)) return;
            UpdatePortHeaderReadouts();
        }
    }
}
