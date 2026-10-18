using ShatteredSail.Content;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Measured blue-window bounds in the unmodified 6336x2688 frame,
        // with a small overlap under its opaque rim: [450,1914)x[445,2301).
        private static readonly Vector2 TavernDossierLayerWindowSize =
            new Vector2(1464f / 6336f * 1780f, 1856f / 2688f * 705f);
        private static readonly Vector2 TavernDossierLayerWindowPosition =
            new Vector2((1182f / 6336f - .5f) * 1780f, (.5f - 1373f / 2688f) * 705f - 4f);

        private bool tavernDossierAtlasComparison;
        private string tavernDossierComparisonCrewId;
        private RawImage tavernDossierComparisonParchment;
        private RectTransform tavernDossierComparisonLayers;
        private RawImage tavernDossierComparisonPortrait;
        private RawImage tavernDossierComparisonFrame;
        private Button tavernDossierOriginalArtButton;
        private Button tavernDossierAtlasArtButton;

        public bool TavernDossierAtlasComparison => tavernDossierAtlasComparison;

        /// <summary>Changes only dossier artwork, preserving the selected crew and reading page.</summary>
        public void SetTavernDossierAtlasComparison(bool useAtlas)
        {
            // Keep the preference when another crew member is opened on this
            // controller. It is a local comparison setting, never save data.
            tavernDossierAtlasComparison = useAtlas;
            if (tavernDossierComparisonParchment == null ||
                !tavernDossierComparisonParchment.transform.parent.gameObject.activeInHierarchy) return;
            ApplyTavernDossierArtComparison();
        }

        private void BuildTavernDossierArt(RectTransform detail, CrewVariantDefinition crew)
        {
            tavernDossierComparisonCrewId = null;
            tavernDossierComparisonParchment = null;
            tavernDossierComparisonLayers = null;
            tavernDossierComparisonPortrait = null;
            tavernDossierComparisonFrame = null;
            tavernDossierOriginalArtButton = null;
            tavernDossierAtlasArtButton = null;

            if (TryBuildTavernDossierPortrait(detail, crew.Id)) return;

            bool supportsComparison = PortArtCatalog.TryTavernDossierTrialUv(crew.Id, out _);
            // Defer the original painting while entering a supported dossier in
            // B mode. Apply below falls back to A if either new layer is missing.
            Texture2D paper = supportsComparison && tavernDossierAtlasComparison
                ? null
                : portArt.CrewDossier(crew.Id);
            RawImage parchment = CreateRawImage("Tavern Dossier Parchment", detail,
                new Vector2(1780f, 705f), new Vector2(0f, -4f), paper, new Rect(0f, 0f, 1f, 1f));
            parchment.raycastTarget = false;
            if (!supportsComparison) return;

            tavernDossierComparisonCrewId = crew.Id;
            tavernDossierComparisonParchment = parchment;
            Color ink = Html("#352315");
            Color buttonPaper = new Color(.85f, .71f, .49f, .92f);
            // The strip is above both portrait compositions and left of the
            // writing bay, so neither mode covers a face or a reading control.
            tavernDossierOriginalArtButton = CreateButton("Tavern Dossier Art Original", detail,
                new Vector2(154f, 32f), new Vector2(-676f, 302f), buttonPaper, ink, 18);
            tavernDossierOriginalArtButton.GetComponentInChildren<Text>().text = "原档案";
            tavernDossierOriginalArtButton.onClick.AddListener(() => SetTavernDossierAtlasComparison(false));
            tavernDossierAtlasArtButton = CreateButton("Tavern Dossier Art Atlas", detail,
                new Vector2(154f, 32f), new Vector2(-506f, 302f), buttonPaper, ink, 18);
            tavernDossierAtlasArtButton.GetComponentInChildren<Text>().text = "分层组合";
            tavernDossierAtlasArtButton.onClick.AddListener(() => SetTavernDossierAtlasComparison(true));
            ApplyTavernDossierArtComparison();
        }

        private bool TryBuildTavernDossierPortrait(RectTransform detail, string crewId)
        {
            Texture2D portrait = portArt.CrewDossierPortrait(crewId);
            Texture2D frame = portrait != null ? portArt.DossierFrame : null;
            if (portrait == null || frame == null) return false;

            RectTransform viewport = CreateRect("Tavern Dossier Portrait Viewport", detail,
                TavernDossierLayerWindowSize, TavernDossierLayerWindowPosition);
            viewport.gameObject.AddComponent<RectMask2D>();
            Vector2 sourceSize = new Vector2(portrait.width, portrait.height);
            float scale = Mathf.Max(TavernDossierLayerWindowSize.x / sourceSize.x,
                TavernDossierLayerWindowSize.y / sourceSize.y);
            RawImage painting = CreateRawImage("Tavern Dossier Background Portrait", viewport,
                sourceSize * scale, Vector2.zero, portrait, new Rect(0f, 0f, 1f, 1f));
            painting.raycastTarget = false;
            painting.rectTransform.anchorMin = painting.rectTransform.anchorMax = new Vector2(.5f, 1f);
            painting.rectTransform.pivot = new Vector2(.5f, 1f);
            painting.rectTransform.anchoredPosition = Vector2.zero;
            // Preserve the authored face and scene colors. Only the old frame
            // uses a blue screen; the new portrait is an opaque painting.
            RawImage parchment = CreateRawImage("Tavern Dossier Parchment", detail,
                new Vector2(1780f, 705f), new Vector2(0f, -4f), frame, new Rect(0f, 0f, 1f, 1f));
            parchment.raycastTarget = false;
            CrewArtChromaKey.Attach(parchment, tolerance: .12f);
            return true;
        }

        private void ApplyTavernDossierArtComparison()
        {
            if (tavernDossierComparisonParchment == null ||
                !PortArtCatalog.TryTavernDossierTrialUv(tavernDossierComparisonCrewId, out Rect uv)) return;

            Texture2D atlas = tavernDossierAtlasComparison
                ? portArt.TavernDossierTrialAtlas(tavernDossierComparisonCrewId)
                : null;
            Texture2D frame = atlas != null ? portArt.TavernDossierTrialFrame(tavernDossierComparisonCrewId) : null;
            bool showAtlas = atlas != null && frame != null;

            if (showAtlas)
            {
                if (tavernDossierComparisonPortrait == null)
                {
                    tavernDossierComparisonLayers = CreateRect("Tavern Dossier Layered Art",
                        tavernDossierComparisonParchment.transform.parent, new Vector2(1820f, 650f), Vector2.zero);
                    tavernDossierComparisonLayers.SetSiblingIndex(
                        tavernDossierComparisonParchment.transform.GetSiblingIndex() + 1);
                    RectTransform viewport = CreateRect("Tavern Dossier Atlas Viewport",
                        tavernDossierComparisonLayers,
                        TavernDossierLayerWindowSize, TavernDossierLayerWindowPosition);
                    viewport.gameObject.AddComponent<RectMask2D>();
                    Image backdrop = CreateImage("Tavern Dossier Portrait Backdrop", viewport,
                        viewport.sizeDelta, Vector2.zero, Html("#8E7455"));
                    backdrop.raycastTarget = false;
                    tavernDossierComparisonPortrait = CreateRawImage("Tavern Dossier Atlas Portrait",
                        viewport, Vector2.zero, Vector2.zero, atlas, uv);
                    tavernDossierComparisonPortrait.raycastTarget = false;
                    RectTransform portraitRect = tavernDossierComparisonPortrait.rectTransform;
                    portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(.5f, 1f);
                    portraitRect.pivot = new Vector2(.5f, 1f);
                    portraitRect.anchoredPosition = Vector2.zero;
                    // The frame is foreground art: its transparent blue window
                    // reveals the portrait and fill below, never an old person.
                    tavernDossierComparisonFrame = CreateRawImage("Tavern Dossier Frame Foreground",
                        tavernDossierComparisonLayers, new Vector2(1780f, 705f), new Vector2(0f, -4f),
                        frame, new Rect(0f, 0f, 1f, 1f));
                    tavernDossierComparisonFrame.raycastTarget = false;
                }
                tavernDossierComparisonParchment.gameObject.SetActive(false);
                tavernDossierComparisonPortrait.texture = atlas;
                tavernDossierComparisonPortrait.uvRect = uv;
                // Fill the measured window width without stretching the face.
                // Top alignment preserves the head; only the lower bust is
                // clipped. The independently authored source atlas stays intact.
                Vector2 sourceSize = new Vector2(atlas.width * uv.width, atlas.height * uv.height);
                tavernDossierComparisonPortrait.rectTransform.sizeDelta =
                    sourceSize * (TavernDossierLayerWindowSize.x / sourceSize.x);
                tavernDossierComparisonFrame.texture = frame;
                tavernDossierComparisonLayers.gameObject.SetActive(true);
                tavernDossierComparisonPortrait.gameObject.SetActive(true);
                // Measured JPEG backgrounds vary around the requested #0080FF.
                CrewArtChromaKey.Attach(tavernDossierComparisonPortrait, tolerance: .12f);
                CrewArtChromaKey.Attach(tavernDossierComparisonFrame, tolerance: .12f);
            }
            else
            {
                tavernDossierComparisonParchment.texture = portArt.CrewDossier(tavernDossierComparisonCrewId);
                tavernDossierComparisonParchment.gameObject.SetActive(true);
                // Keep each small chroma material for the next A/B toggle;
                // the components destroy their owned material with this view.
                if (tavernDossierComparisonLayers != null)
                    tavernDossierComparisonLayers.gameObject.SetActive(false);
                if (tavernDossierComparisonPortrait != null)
                    tavernDossierComparisonPortrait.gameObject.SetActive(false);
            }

            WoodButtonSkin.SetSelected(tavernDossierOriginalArtButton, !showAtlas);
            WoodButtonSkin.SetSelected(tavernDossierAtlasArtButton, showAtlas);
        }
    }
}
