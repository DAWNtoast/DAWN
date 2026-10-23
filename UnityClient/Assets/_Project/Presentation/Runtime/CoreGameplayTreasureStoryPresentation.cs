using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float TreasureStorySecondsPerPage = 12f;
        private readonly HashSet<string> treasureStoryCompletedKeys = new HashSet<string>();
        private string treasureStoryKey = string.Empty;
        private int treasureStoryPage;
        private string treasureStoryRenderedKey = string.Empty;
        private int treasureStoryRenderedPage = -1;
        private RectTransform treasureStoryOverlay;
        private ComicAutoAdvance treasureStoryAutoAdvance;
        private string treasureStoryTimerKey = string.Empty;
        private int treasureStoryTimerPage = -1;
        private float treasureStoryRemainingSeconds = TreasureStorySecondsPerPage;

        private void BuildTreasureStoryOverlayIfNeeded()
        {
            CloseTreasureStoryOverlay();
            if (session == null || !session.IsTreasureVoyage || session.ActiveTreasureMap == null) return;
            TreasureStoryChapter chapter = CoreTreasureStoryCatalog.FindChapter(session.ActiveTreasureMap.RankId);
            if (chapter == null || chapter.ImageResources.Count == 0) return;

            bool battlePrelude = session.Phase == CoreDemoPhase.Battle && session.ActiveEncounter != null &&
                session.ActiveEncounter.BossKind == CoreBossKind.Legendary;
            bool voyageIntro = session.Phase == CoreDemoPhase.Sailing || session.Phase == CoreDemoPhase.Encounter;
            if (!battlePrelude && !voyageIntro) return;

            string encounterId = battlePrelude ? session.ActiveEncounter.Id : string.Empty;
            TreasureBattleStory battle = battlePrelude ? CoreTreasureStoryCatalog.FindBattle(encounterId) : null;
            if (battlePrelude && battle == null) return;
            string key = battlePrelude
                ? "battle:" + session.ActiveTreasureMap.Id + ":" + encounterId
                : "intro:" + session.ActiveTreasureMap.Id;
            if (treasureStoryCompletedKeys.Contains(key)) return;
            if (treasureStoryKey != key)
            {
                treasureStoryKey = key;
                treasureStoryPage = 0;
            }

            BuildTreasureStoryOverlay(chapter, battle, key);
        }

        private void BuildTreasureStoryOverlay(TreasureStoryChapter chapter, TreasureBattleStory battle, string key)
        {
            string imageResource = chapter.ImageResources[Mathf.Clamp(treasureStoryPage, 0, chapter.ImageResources.Count - 1)];
            Texture2D comic = Resources.Load<Texture2D>(imageResource);
            treasureStoryOverlay = CreateComicOverlay("Treasure Story Overlay");
            RectTransform overlay = treasureStoryOverlay;
            int displayedPage = treasureStoryPage;
            bool CurrentPage() => isActiveAndEnabled && overlay != null && overlay.gameObject.activeInHierarchy &&
                treasureStoryOverlay == overlay && treasureStoryRenderedKey == key && treasureStoryRenderedPage == displayedPage;

            ComicPageGraphic page = CreateComicPage("Treasure Story Comic Page", overlay, comic);
            page.SetPanel(new Rect(0f, 0f, 1f, 1f), false);
            if (battle == null)
                page.gameObject.AddComponent<TreasureComicDialogueLayout>().Configure(page,
                    TreasureComicDialogueCatalog.GetPage(chapter.RankId, displayedPage, session.ActiveTreasureMap.ContentVersion));
            else
                BuildLegendaryShipPrelude(overlay, page, battle, displayedPage);
            bool pageChanged = !string.Equals(treasureStoryRenderedKey, key, System.StringComparison.Ordinal) ||
                treasureStoryRenderedPage != treasureStoryPage;
            treasureStoryRenderedKey = key;
            treasureStoryRenderedPage = treasureStoryPage;
            if (pageChanged && Application.isPlaying)
                PlaySfx(battle != null ? "battle_card_play" : "chart_draw", battle != null ? .30f : .46f);

            Button advance = CreateComicAdvance("Treasure Story Advance", overlay);
            advance.onClick.AddListener(() =>
            {
                if (!CurrentPage()) return;
                if (treasureStoryPage < chapter.ImageResources.Count - 1)
                {
                    treasureStoryPage++;
                    Render();
                    return;
                }
                treasureStoryCompletedKeys.Add(key);
                treasureStoryKey = string.Empty;
                treasureStoryPage = 0;
                Render();
            });
            treasureStoryAutoAdvance = overlay.gameObject.AddComponent<ComicAutoAdvance>();
            treasureStoryAutoAdvance.Configure(advance, TreasureStorySecondsPerPage, CurrentPage);
            if (!pageChanged && treasureStoryTimerKey == key && treasureStoryTimerPage == displayedPage)
                treasureStoryAutoAdvance.RestoreRemainingSeconds(treasureStoryRemainingSeconds);
        }

        private void BuildLegendaryShipPrelude(RectTransform overlay, ComicPageGraphic background,
            TreasureBattleStory story, int pageIndex)
        {
            // The same authored sea scene drives the event, chart preview and battle prelude.
            // It already contains the ship and ocean; do not add a second ship or water layer.
            ResolveEncounterIllustration(story.EncounterId, out Texture2D scene, out Rect uv);
            background.texture = scene;
            background.SetPanel(uv, true);
            background.color = Color.white;
            RectTransform plaque = CreateFixedPanel("Treasure Legendary Story Plaque", overlay,
                Vector2.zero, Vector2.zero, new Color(.025f, .055f, .075f, .94f));
            plaque.anchorMin = new Vector2(.10f, .075f); plaque.anchorMax = new Vector2(.90f, .27f);
            plaque.GetComponent<Image>().raycastTarget = false;
            Text title = CreateText("Treasure Legendary Story Title", plaque, Vector2.zero, Vector2.zero,
                29, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            title.rectTransform.anchorMin = new Vector2(.035f, .66f); title.rectTransform.anchorMax = new Vector2(.965f, .96f);
            title.text = story.ShipName + " · " + (pageIndex + 1) + " / " + story.Pages.Count;
            title.raycastTarget = false;
            Text prose = CreateText("Treasure Legendary Story Text", plaque, Vector2.zero, Vector2.zero,
                24, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            prose.rectTransform.anchorMin = new Vector2(.035f, .08f); prose.rectTransform.anchorMax = new Vector2(.965f, .66f);
            prose.text = story.Pages[Mathf.Clamp(pageIndex, 0, story.Pages.Count - 1)];
            prose.resizeTextForBestFit = true; prose.resizeTextMinSize = 18; prose.resizeTextMaxSize = 24;
            prose.raycastTarget = false;
        }

        private void CloseTreasureStoryOverlay()
        {
            RectTransform overlay = treasureStoryOverlay;
            // Render can recreate the same page for an unrelated UI refresh. Keep
            // its reading progress; an actual page or story change starts a new timer.
            if (treasureStoryAutoAdvance != null)
            {
                treasureStoryRemainingSeconds = treasureStoryAutoAdvance.RemainingSeconds;
                treasureStoryTimerKey = treasureStoryRenderedKey;
                treasureStoryTimerPage = treasureStoryRenderedPage;
            }
            treasureStoryAutoAdvance = null;
            treasureStoryOverlay = null;
            CloseComicOverlay(overlay);
        }
    }
}
