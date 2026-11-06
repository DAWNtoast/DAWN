using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform openingComicOverlay;
        private ComicPageGraphic openingComicPage;
        private ComicAutoAdvance openingComicAutoAdvance;
        private int openingComicPageIndex = -1;
        private int openingComicPanelIndex = -1;
        private int openingComicGeneration;
        private int openingComicLastAdvanceFrame = -1;
        private CanvasGroup openingComicBackgroundGroup;
        private bool openingComicPreviousInteractable;
        private bool openingComicPreviousRaycasts;
        private EventSystem openingComicEventSystem;
        private bool openingComicPreviousNavigation;
        private string openingComicStoryKey;
        private OpeningPortComicProgress openingPortComicProgress;

        public bool IsOpeningComicPlaying => openingComicOverlay != null && openingComicStoryKey == OpeningPortComicProgress.OpeningKey;
        public bool IsArrivalComicPlaying => openingComicOverlay != null && openingComicStoryKey != OpeningPortComicProgress.OpeningKey;

        private OpeningPortComicProgress OpeningPortProgress => editorPreviewBuilding ? null :
            openingPortComicProgress ?? (Application.isPlaying ? openingPortComicProgress = OpeningPortComicProgress.ForCurrentProfile() : null);

        private void BuildOpeningComicOverlay()
        {
            OpeningPortProgress?.TryQueue(OpeningPortComicProgress.OpeningKey);
            ResumeOpeningPortComic();
        }

        private void QueueArrivalComic(VoyageSettlement settlement)
        {
            if (editorPreviewBuilding || profileSaveBlocked || session == null || session.Phase != CoreDemoPhase.Port ||
                settlement == null || settlement.Outcome != VoyageOutcome.ArrivedAtPort ||
                settlement.Destination != session.CurrentPort) return;
            ArrivalComicStory story = ArrivalComicCatalog.Find(settlement.Destination);
            if (story != null) OpeningPortProgress?.TryQueue(OpeningPortComicProgress.ArrivalKey(story.Id));
        }

        private void ResumeOpeningPortComic()
        {
            if (openingComicOverlay != null || editorPreviewBuilding || profileSaveBlocked || session == null ||
                session.Phase != CoreDemoPhase.Port || contentRoot == null || contentRoot.parent == null) return;
            OpeningPortComicProgress progress = OpeningPortProgress;
            if (progress == null || string.IsNullOrEmpty(progress.PendingStoryKey)) return;
            CloseOpeningComic();
            openingComicStoryKey = progress.PendingStoryKey;
            bool opening = openingComicStoryKey == OpeningPortComicProgress.OpeningKey;
            string prefix = opening ? "Opening Comic" : "Arrival Comic";

            openingComicBackgroundGroup = contentRoot.GetComponent<CanvasGroup>();
            if (openingComicBackgroundGroup == null)
                openingComicBackgroundGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            openingComicPreviousInteractable = openingComicBackgroundGroup.interactable;
            openingComicPreviousRaycasts = openingComicBackgroundGroup.blocksRaycasts;
            openingComicBackgroundGroup.interactable = false;
            openingComicBackgroundGroup.blocksRaycasts = false;

            // Pointer input belongs to the full-screen surface. Disabling navigation
            // also keeps Submit/Move from reaching a previously focused port control.
            openingComicEventSystem = EventSystem.current;
            if (openingComicEventSystem != null)
            {
                openingComicPreviousNavigation = openingComicEventSystem.sendNavigationEvents;
                openingComicEventSystem.sendNavigationEvents = false;
                openingComicEventSystem.SetSelectedGameObject(null);
            }

            openingComicOverlay = CreateComicOverlay(prefix + " Overlay");
            RectTransform overlay = openingComicOverlay;
            int generation = ++openingComicGeneration;
            openingComicLastAdvanceFrame = -1;
            openingComicPage = CreateComicPage(prefix + " Page", overlay, null);
            openingComicPage.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            bool CurrentOverlay() => isActiveAndEnabled && overlay != null && overlay.gameObject.activeInHierarchy &&
                openingComicOverlay == overlay && openingComicGeneration == generation;
            Button advance = CreateComicAdvance(prefix + " Advance", overlay);
            advance.onClick.AddListener(() =>
            {
                if (!CurrentOverlay() || openingComicLastAdvanceFrame == Time.frameCount) return;
                openingComicLastAdvanceFrame = Time.frameCount;
                if (!progress.AdvancePanel(openingComicStoryKey, openingComicPageIndex, openingComicPanelIndex)) return;
                if (!string.IsNullOrEmpty(progress.PendingStoryKey))
                    SetOpeningComicPage(progress.PendingPage, progress.PendingPanel);
                else
                {
                    CloseOpeningComic();
                    if (Application.isPlaying) ShowWarehouseIncomeReceipt();
                }
            });
            openingComicAutoAdvance = overlay.gameObject.AddComponent<ComicAutoAdvance>();
            openingComicAutoAdvance.Configure(advance, OpeningComicCatalog.SecondsPerPage, CurrentOverlay);
            SetOpeningComicPage(progress.PendingPage, progress.PendingPanel);
        }

        private void SetOpeningComicPage(int index, int panel)
        {
            if (openingComicOverlay == null || openingComicPage == null || index < 0 ||
                index >= OpeningPortComicProgress.PageCount(openingComicStoryKey)) return;
            string resource = openingComicStoryKey == OpeningPortComicProgress.OpeningKey
                ? OpeningComicCatalog.Pages[index].ImageResource
                : ArrivalComicCatalog.FindById(openingComicStoryKey.Substring("arrival:".Length)).Pages[index];
            Texture2D texture = ComicPanelCatalog.LoadPanel(resource, panel, out Rect source);
            if (texture == null)
            {
                Debug.LogWarning("Story comic page was not found at Resources/" + resource + ".");
                CloseOpeningComic();
                return;
            }
            bool changedTexture = openingComicPage.texture != null && openingComicPage.texture != texture;
            openingComicPageIndex = index;
            openingComicPanelIndex = panel;
            openingComicPage.texture = texture;
            openingComicPage.SetPanel(source, false);
            openingComicPage.GetComponent<AspectRatioFitter>().aspectRatio =
                texture.width * source.width / (texture.height * source.height);
            openingComicAutoAdvance?.ResetTimer();
            // A story can span many independent HD panels without changing the
            // underlying port view. Retire previous panels through the shared,
            // deferred collector; the current Graphic keeps its borrowed art.
            if (changedTexture) QueueUnusedArtCollection(true);
        }

        private void CloseOpeningComic()
        {
            RectTransform overlay = openingComicOverlay;
            openingComicOverlay = null;
            openingComicPage = null;
            openingComicStoryKey = null;
            openingComicAutoAdvance = null;
            openingComicPageIndex = -1;
            openingComicPanelIndex = -1;
            openingComicGeneration++;
            CloseComicOverlay(overlay);
            if (openingComicBackgroundGroup != null)
            {
                openingComicBackgroundGroup.interactable = openingComicPreviousInteractable;
                openingComicBackgroundGroup.blocksRaycasts = openingComicPreviousRaycasts;
            }
            openingComicBackgroundGroup = null;
            if (openingComicEventSystem != null)
            {
                openingComicEventSystem.SetSelectedGameObject(null);
                openingComicEventSystem.sendNavigationEvents = openingComicPreviousNavigation;
            }
            openingComicEventSystem = null;
        }
    }
}
