using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float RamComicSecondsPerPanel = 4f;
        private readonly Texture2D[] ramComicTextures = new Texture2D[RamComicCatalog.ResourceCount];
        private readonly bool[] ramComicLoadAttempted = new bool[RamComicCatalog.ResourceCount];
        private Texture2D[] activeRamComicPages;
        private RectTransform ramComicOverlay;
        private ComicPageGraphic ramComicPage;
        private ComicAutoAdvance ramComicAutoAdvance;
        private int ramComicPanelIndex;
        private bool ramComicAdvanceRequested, ramComicResumeAfterPause;

        private Texture2D RamComicTexture(bool enemyInitiated, NavalBattleAlignment impactAlignment)
        {
            int sequence = RamComicCatalog.SequenceIndex(enemyInitiated, impactAlignment);
            int first = sequence * RamComicCatalog.PagesPerSequence;
            bool complete = true;
            for (int page = 0; page < RamComicCatalog.PagesPerSequence; page++)
            {
                int index = first + page;
                if (!ramComicLoadAttempted[index])
                {
                    ramComicLoadAttempted[index] = true;
                    ramComicTextures[index] = Resources.Load<Texture2D>(RamComicCatalog.ImageResource(sequence, page));
                }
                complete &= ramComicTextures[index] != null;
            }
            // Reject an incomplete sequence before presentation starts so either
            // initiator retains the existing physical collision fallback.
            return complete ? ramComicTextures[first] : null;
        }

        // Also used by isolated Editor captures via reflection. It builds the
        // same presentation without committing a battle action or touching saves.
        private RawImage BuildRamComicOverlay(Texture2D art, bool enemyInitiated, bool headOn, string resultSummary)
        {
            CloseRamComic();
            if (art == null || contentRoot == null) return null;
            NavalBattleAlignment alignment = headOn ? NavalBattleAlignment.HeadOn : NavalBattleAlignment.EnemyBroadside;
            if (RamComicTexture(enemyInitiated, alignment) == null) return null;
            int first = RamComicCatalog.SequenceIndex(enemyInitiated, alignment) * RamComicCatalog.PagesPerSequence;
            // Keep the live sequence pinned while routine cache collection runs.
            activeRamComicPages = new Texture2D[RamComicCatalog.PagesPerSequence];
            System.Array.Copy(ramComicTextures, first, activeRamComicPages, 0, activeRamComicPages.Length);
            ramComicAdvanceRequested = false;
            ramComicOverlay = CreateComicOverlay("Battle Ram Comic Overlay");
            RectTransform overlay = ramComicOverlay;
            ramComicPage = CreateComicPage("Battle Ram Comic Page", overlay, activeRamComicPages[0]);
            Button advance = CreateComicAdvance("Battle Ram Comic Advance", overlay);
            bool CurrentPage() => overlay != null && isActiveAndEnabled && battleAnimationPlaying && ramComicOverlay == overlay &&
                overlay.gameObject.activeInHierarchy && advance.IsInteractable() && !ramComicAdvanceRequested;
            advance.onClick.AddListener(() =>
            {
                if (!CurrentPage()) return;
                ramComicAdvanceRequested = true;
            });
            ramComicAutoAdvance = overlay.gameObject.AddComponent<ComicAutoAdvance>();
            ramComicAutoAdvance.Configure(advance, RamComicSecondsPerPanel, CurrentPage);
            SetRamComicPanel(0);
            return ramComicPage;
        }

        private void SetRamComicPanel(int panel)
        {
            if (ramComicPage == null || activeRamComicPages == null) return;
            panel = Mathf.Clamp(panel, 0, activeRamComicPages.Length - 1);
            ramComicPanelIndex = panel;
            ramComicPage.texture = activeRamComicPages[panel];
            ramComicPage.SetPanel(new Rect(0f, 0f, 1f, 1f));
            if (ramComicAutoAdvance != null) ramComicAutoAdvance.ResetTimer();
        }

        private IEnumerator AnimateRamComic(Texture2D art, bool enemyInitiated, BattleAnimationSnapshot before, BattleAnimationSnapshot after, int priorBurnDamage)
        {
            if (art == null || BuildRamComicOverlay(art, enemyInitiated, false,
                string.Empty) == null) yield break;
            try
            {
                // A hull-killing impact never reached the later boarding panels.
                // If boarding happened and its first strike killed the crew, the
                // full sequence remains truthful and ends in the real defeat.
                bool hullOutcome = session.Phase != CoreDemoPhase.Battle && after.Stage != NavalBattleStage.Boarding;
                int panelCount = hullOutcome ? 2 : 4;
                for (int panel = 0; panel < panelCount && ramComicOverlay != null; panel++)
                {
                    ramComicAdvanceRequested = false;
                    SetRamComicPanel(panel);
                    if (panel == 0) PlaySfx("battle_ram_charge", .85f);
                    else if (panel == 1) PlaySfx("battle_ram_impact", .95f);
                    while (!ramComicAdvanceRequested && ramComicOverlay != null) yield return null;
                }
                if (ramComicOverlay == null) yield break;
                ramComicOverlay.GetComponentInChildren<Button>().interactable = false;
                if (session.Phase == CoreDemoPhase.Battle && session.Battle != null && session.Battle.Stage == NavalBattleStage.Boarding)
                {
                    yield return AnimateBoardingSceneTransition();
                }
                else if (ramComicOverlay != null)
                {
                    Image curtain = CreateStretchPanel("Battle Ram Comic Fade", ramComicOverlay, Color.clear).GetComponent<Image>();
                    curtain.raycastTarget = true;
                    const float duration = .22f;
                    for (float elapsed = 0f; elapsed < duration && curtain != null; elapsed += Time.unscaledDeltaTime)
                    {
                        curtain.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / duration));
                        yield return null;
                    }
                    if (curtain != null)
                    {
                        curtain.color = Color.black;
                        yield return null;
                    }
                }
            }
            finally { CloseRamComic(); }
        }

        private void CloseRamComic()
        {
            RectTransform overlay = ramComicOverlay;
            ramComicOverlay = null;
            ramComicPage = null;
            ramComicAutoAdvance = null;
            activeRamComicPages = null;
            ramComicPanelIndex = -1;
            ramComicAdvanceRequested = false;
            CloseComicOverlay(overlay);
            // Page textures are shared Resources assets, never owned here.
            // Naval combat and boarding share the same resource scope. Closing
            // the comic must retire its four large pages even when Render does
            // not change phase. A newly opened sequence retains its own pages
            // while the existing deferred collector coalesces close/reopen.
            if (overlay != null) QueueUnusedArtCollection(true);
        }

    }
}
