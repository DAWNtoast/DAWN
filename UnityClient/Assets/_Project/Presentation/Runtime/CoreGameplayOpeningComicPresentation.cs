using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform openingComicOverlay;
        private ComicPageGraphic openingComicPage;
        private Text openingComicNarration;
        private ComicAutoAdvance openingComicAutoAdvance;
        private int openingComicPageIndex = -1;
        private int openingComicGeneration;
        private int openingComicLastAdvanceFrame = -1;
        private CanvasGroup openingComicBackgroundGroup;
        private bool openingComicPreviousInteractable;
        private bool openingComicPreviousRaycasts;
        private EventSystem openingComicEventSystem;
        private bool openingComicPreviousNavigation;

        public bool IsOpeningComicPlaying => openingComicOverlay != null;

        private void BuildOpeningComicOverlay()
        {
            CloseOpeningComic();
            if (contentRoot == null || contentRoot.parent == null || OpeningComicCatalog.Pages.Count == 0) return;

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

            openingComicOverlay = CreateComicOverlay("Opening Comic Overlay");
            RectTransform overlay = openingComicOverlay;
            int generation = ++openingComicGeneration;
            openingComicLastAdvanceFrame = -1;
            openingComicPage = CreateComicPage("Opening Comic Page", overlay, null);
            RectTransform lettering = CreateRect("Opening Comic Narration", openingComicPage.transform, Vector2.zero, Vector2.zero);
            openingComicNarration = lettering.gameObject.AddComponent<Text>();
            openingComicNarration.font = GameTypography.InterfaceFont;
            openingComicNarration.fontStyle = FontStyle.Normal;
            openingComicNarration.fontSize = 36;
            openingComicNarration.resizeTextForBestFit = true;
            openingComicNarration.resizeTextMinSize = 18;
            openingComicNarration.resizeTextMaxSize = 36;
            openingComicNarration.color = new Color32(49, 30, 19, 255);
            openingComicNarration.alignment = TextAnchor.MiddleCenter;
            openingComicNarration.lineSpacing = 1f;
            openingComicNarration.supportRichText = false;
            openingComicNarration.horizontalOverflow = HorizontalWrapMode.Wrap;
            openingComicNarration.verticalOverflow = VerticalWrapMode.Truncate;
            openingComicNarration.raycastTarget = false;

            bool CurrentOverlay() => isActiveAndEnabled && overlay != null && overlay.gameObject.activeInHierarchy &&
                openingComicOverlay == overlay && openingComicGeneration == generation;
            Button advance = CreateComicAdvance("Opening Comic Advance", overlay);
            advance.onClick.AddListener(() =>
            {
                if (!CurrentOverlay() || openingComicLastAdvanceFrame == Time.frameCount) return;
                openingComicLastAdvanceFrame = Time.frameCount;
                if (openingComicPageIndex + 1 < OpeningComicCatalog.Pages.Count)
                    SetOpeningComicPage(openingComicPageIndex + 1);
                else
                    CloseOpeningComic();
            });
            openingComicAutoAdvance = overlay.gameObject.AddComponent<ComicAutoAdvance>();
            openingComicAutoAdvance.Configure(advance, OpeningComicCatalog.SecondsPerPage, CurrentOverlay);
            SetOpeningComicPage(0);
        }

        private void SetOpeningComicPage(int index)
        {
            if (openingComicOverlay == null || openingComicPage == null || index < 0 || index >= OpeningComicCatalog.Pages.Count) return;
            OpeningComicPage definition = OpeningComicCatalog.Pages[index];
            Texture2D texture = Resources.Load<Texture2D>(definition.ImageResource);
            if (texture == null)
            {
                Debug.LogWarning("Opening comic page was not found at Resources/" + definition.ImageResource + ".");
                CloseOpeningComic();
                return;
            }
            openingComicPageIndex = index;
            openingComicPage.texture = texture;
            openingComicPage.SetPanel(new Rect(0f, 0f, 1f, 1f), false);
            openingComicNarration.text = definition.Narration;
            RectTransform lettering = openingComicNarration.rectTransform;
            lettering.anchorMin = definition.NarrativeBounds.min;
            lettering.anchorMax = definition.NarrativeBounds.max;
            lettering.offsetMin = new Vector2(4f, 4f);
            lettering.offsetMax = new Vector2(-4f, -4f);
            openingComicAutoAdvance?.ResetTimer();
        }

        private void CloseOpeningComic()
        {
            RectTransform overlay = openingComicOverlay;
            openingComicOverlay = null;
            openingComicPage = null;
            openingComicNarration = null;
            openingComicAutoAdvance = null;
            openingComicPageIndex = -1;
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
