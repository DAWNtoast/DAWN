using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private VoyageHeaderView cachedVoyageHeader;
#if UNITY_EDITOR
        public static bool ExportingVoyageHeaderPrefab { get; set; }
#endif

        private bool HasNavigationHeader => session != null &&
            (session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter);

        private bool TryBuildPrefabVoyageHeader(string title)
        {
            if (!HasNavigationHeader) return false;
#if UNITY_EDITOR
            if (ExportingVoyageHeaderPrefab) return false;
#endif
            bool retain = Application.isPlaying && !editorPreviewBuilding;
            VoyageHeaderView view = retain ? cachedVoyageHeader : null;
            if (view == null)
            {
                VoyageHeaderView prefab = Resources.Load<VoyageHeaderView>("UI/VoyageHeader");
                if (prefab == null) return false;
                if (!prefab.IsComplete) throw new InvalidOperationException("Incomplete voyage header prefab.");
                view = Instantiate(prefab, contentRoot, false);
                view.name = "Voyage Header";
                view.GetComponent<PrefabRuntimeSkinBindings>()?.Apply();
                if (retain) cachedVoyageHeader = view;
            }
            HideLegacyVoyageHeaderVisuals();
            RectTransform header = (RectTransform)view.transform;
            header.SetParent(contentRoot, false);
            header.SetAsLastSibling();
            float height = PhaseSurfaceLayout.GetHeaderHeight(tutorialActive);
            header.sizeDelta = new Vector2(0f, height);
            header.anchoredPosition = new Vector2(0f, -height * .5f);
            header.localScale = Vector3.one;
            view.Title.text = title;
            view.Status.text = BuildHeaderStatus();
            view.Timeline.SetVoyageHours(session.ElapsedGameHours);
            view.Time.text = "游戏 " + CoreVoyageSession.FormatGameDate(session.GameDateTime, true) + "　·　" +
                NavigationTimeOfDayUtility.Label(NavigationTimeOfDayUtility.FromVoyageHours(session.ElapsedGameHours));
            // Render restores outgoing tutorial guards before clearing the body.
            // Bind fresh callbacks so a retained header cannot keep the old step.
            view.Cargo.onClick = new Button.ButtonClickedEvent();
            view.Cargo.onClick.AddListener(OpenVoyageCargo);
            view.Cargo.interactable = true;
            view.Settings.onClick = new Button.ButtonClickedEvent();
            view.Settings.onClick.AddListener(OpenPortSettings);
            view.Settings.interactable = true;
            NavigationHeaderSkin.Apply(header);
            view.gameObject.SetActive(true);
            return true;
        }

        private bool IsRetainedVoyageHeader(Transform child) => cachedVoyageHeader != null && child == cachedVoyageHeader.transform;

        private void ParkUnusedVoyageHeader()
        {
            if (cachedVoyageHeader == null || HasNavigationHeader) return;
            cachedVoyageHeader.Frame.sprite = null;
            cachedVoyageHeader.Compass.sprite = null;
            ParkPortView(cachedVoyageHeader, true);
            // Removing a cache reference never destroys a sprite still used by
            // another live view. The normal coalesced collection owns unloading.
            NavigationHeaderSkin.ReleaseGameplayReferences();
        }
    }
}
