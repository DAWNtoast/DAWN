using System;
using System.Collections.Generic;
using System.Linq;
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
        private int treasureStoryPanel;
        private string treasureStoryRenderedKey = string.Empty;
        private int treasureStoryRenderedPage = -1;
        private int treasureStoryRenderedPanel = -1;
        private RectTransform treasureStoryOverlay;
        private ComicAutoAdvance treasureStoryAutoAdvance;
        private string treasureStoryTimerKey = string.Empty;
        private int treasureStoryTimerPage = -1;
        private int treasureStoryTimerPanel = -1;
        private float treasureStoryRemainingSeconds = TreasureStorySecondsPerPage;
        private CoreVoyageMap treasureStoryVoyage;
        private CoreBattleState legendaryStoryBattle;
        private string legendaryStoryEncounterId = string.Empty;
        private CoreMapNode legendaryStoryNode;
        private string pendingLegendaryVictoryId = string.Empty;
        private LegendaryFleetComicReadProgress legendaryComicReadProgress;

        private LegendaryFleetComicReadProgress LegendaryComicReadProgress => editorPreviewBuilding ? null :
            legendaryComicReadProgress ?? (Application.isPlaying
                ? legendaryComicReadProgress = LegendaryFleetComicReadProgress.ForCurrentProfile() : null);

        private bool IsTreasureStoryCompleted(string key) => treasureStoryCompletedKeys.Contains(key) ||
            (LegendaryComicReadProgress?.HasReadStoryKey(key) ?? false);

        private static string LegendaryStoryKey(string encounterId, bool after) =>
            "legendary:v" + LegendaryFleetComicCatalog.ContentVersion + ":" + (after ? "after:" : "before:") + encounterId;

        private void SynchronizeTreasureStoryProgress()
        {
            if (session == null) return;
            if (!ReferenceEquals(treasureStoryVoyage, session.Map))
            {
                treasureStoryVoyage = session.Map;
                treasureStoryCompletedKeys.Clear();
                treasureStoryKey = string.Empty;
                treasureStoryPage = 0;
                treasureStoryPanel = 0;
                treasureStoryRenderedKey = string.Empty;
                treasureStoryRenderedPage = -1;
                treasureStoryTimerKey = string.Empty;
                treasureStoryTimerPage = -1;
                treasureStoryRenderedPanel = treasureStoryTimerPanel = -1;
                legendaryStoryBattle = null;
                legendaryStoryNode = null;
                pendingLegendaryVictoryId = string.Empty;
            }

            if (legendaryStoryBattle != null && !ReferenceEquals(legendaryStoryBattle, session.Battle))
            {
                // The completed map node is the committed victory boundary for
                // both ordinary and treasure voyages. The fallback also permits
                // node-less battle previews without treating mutual defeat as a win.
                bool won = session.Battle == null && session.Phase == CoreDemoPhase.Sailing &&
                    session.Settlement == null && !session.LastBattleRetreated &&
                    (legendaryStoryNode != null ? legendaryStoryNode.Visited && !legendaryStoryNode.Skipped :
                        legendaryStoryBattle.EnemyHull <= 0 || legendaryStoryBattle.Stage == NavalBattleStage.Boarding &&
                        legendaryStoryBattle.EnemyBoardingHealth <= 0);
                if (won &&
                    !IsTreasureStoryCompleted(LegendaryStoryKey(legendaryStoryEncounterId, true)))
                    pendingLegendaryVictoryId = legendaryStoryEncounterId;
                legendaryStoryBattle = null;
                legendaryStoryNode = null;
            }

            if (session.Phase == CoreDemoPhase.Battle && session.Battle != null &&
                !ReferenceEquals(legendaryStoryBattle, session.Battle) &&
                LegendaryFleetComicCatalog.Find(session.ActiveEncounter?.Id) != null)
            {
                legendaryStoryBattle = session.Battle;
                legendaryStoryEncounterId = session.ActiveEncounter.Id;
                legendaryStoryNode = session.Map?.Nodes.FirstOrDefault(node => node.Encounter.Id == legendaryStoryEncounterId);
            }
        }

        private void CaptureTreasureStoryProgress(VoyageSuspensionRecord record)
        {
            SynchronizeTreasureStoryProgress();
            record.completedTreasureStoryKeys = treasureStoryCompletedKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            record.treasureStoryKey = treasureStoryKey;
            record.treasureStoryPage = treasureStoryPage;
            record.treasureStoryPanel = treasureStoryPanel;
            record.pendingLegendaryVictoryId = pendingLegendaryVictoryId;
        }

        private void RestoreTreasureStoryProgress(VoyageSuspensionRecord record)
        {
            treasureStoryVoyage = session.Map;
            legendaryStoryBattle = null;
            legendaryStoryNode = null;
            treasureStoryCompletedKeys.Clear();
            foreach (string key in record.completedTreasureStoryKeys ?? Array.Empty<string>())
                if (key != null && key.StartsWith("legendary:", StringComparison.Ordinal))
                    treasureStoryCompletedKeys.Add(key);
            // TryRestoreSuspendedVoyage has already validated this snapshot's
            // profileScope. Only completed legendary pages migrate; a pending
            // page or the older sea-scene prelude is not a reading acknowledgement.
            LegendaryComicReadProgress?.ImportReadStoryKeys(record.completedTreasureStoryKeys?
                .Where(key => key != null && key.StartsWith("legendary:", StringComparison.Ordinal)));
            treasureStoryKey = record.treasureStoryKey ?? string.Empty;
            treasureStoryPage = record.treasureStoryPage;
            treasureStoryPanel = record.treasureStoryPanel;
            // Retired treasure-map introductions must not resume from an old checkpoint.
            if (!treasureStoryKey.StartsWith("legendary:", StringComparison.Ordinal))
            {
                treasureStoryKey = string.Empty;
                treasureStoryPage = treasureStoryPanel = 0;
            }
            pendingLegendaryVictoryId = record.pendingLegendaryVictoryId ?? string.Empty;
            treasureStoryRenderedKey = treasureStoryTimerKey = string.Empty;
            treasureStoryRenderedPage = treasureStoryTimerPage = -1;
            treasureStoryRenderedPanel = treasureStoryTimerPanel = -1;
        }

        private void BuildTreasureStoryOverlayIfNeeded()
        {
            CloseTreasureStoryOverlay();
            SynchronizeTreasureStoryProgress();
            if (session == null) return;

            LegendaryFleetComicStory aftermath = LegendaryFleetComicCatalog.Find(pendingLegendaryVictoryId);
            if (aftermath != null && !IsTreasureStoryCompleted(LegendaryStoryKey(aftermath.EncounterId, true)))
            {
                BuildLegendaryFleetComic(aftermath, true);
                return;
            }
            if (session.Phase == CoreDemoPhase.Battle)
            {
                LegendaryFleetComicStory prelude = LegendaryFleetComicCatalog.Find(session.ActiveEncounter?.Id);
                if (prelude != null && !IsTreasureStoryCompleted(LegendaryStoryKey(prelude.EncounterId, false)))
                    BuildLegendaryFleetComic(prelude, false);
                return;
            }

        }

        private void SelectTreasureStoryPage(string key, int pageCount)
        {
            if (treasureStoryKey != key) treasureStoryPage = treasureStoryPanel = 0;
            treasureStoryKey = key;
            treasureStoryPage = Mathf.Clamp(treasureStoryPage, 0, pageCount - 1);
        }

        private void BuildLegendaryFleetComic(LegendaryFleetComicStory story, bool after)
        {
            string key = LegendaryStoryKey(story.EncounterId, after);
            SelectTreasureStoryPage(key, 1);
            BuildTreasureStoryOverlay((after ? story.After : story.Before).ImageResource, key);
        }

        private void BuildTreasureStoryOverlay(string imageResource, string key)
        {
            IReadOnlyList<Rect> panels = ComicPanelCatalog.PanelsFor(imageResource);
            treasureStoryPanel = Mathf.Clamp(treasureStoryPanel, 0, panels.Count - 1);
            Texture2D comic = ComicPanelCatalog.LoadPanel(imageResource, treasureStoryPanel, out Rect sourcePanel);
            if (comic == null) return;
            treasureStoryOverlay = CreateComicOverlay("Legendary Fleet Comic Overlay");
            RectTransform overlay = treasureStoryOverlay;
            int displayedPage = treasureStoryPage;
            int displayedPanel = treasureStoryPanel;
            bool CurrentPage() => isActiveAndEnabled && overlay != null && overlay.gameObject.activeInHierarchy &&
                treasureStoryOverlay == overlay && treasureStoryRenderedKey == key && treasureStoryRenderedPage == displayedPage &&
                treasureStoryRenderedPanel == displayedPanel && treasureStoryKey == key &&
                treasureStoryPage == displayedPage && treasureStoryPanel == displayedPanel;

            ComicPageGraphic page = CreateComicPage("Legendary Fleet Comic Page", overlay, comic);
            page.SetPanel(sourcePanel, false);
            // Keep the complete current panel in view without stretching its figures.
            AspectRatioFitter fit = page.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectRatio = comic.width * sourcePanel.width / (comic.height * sourcePanel.height);
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            bool pageChanged = !string.Equals(treasureStoryRenderedKey, key, System.StringComparison.Ordinal) ||
                treasureStoryRenderedPage != treasureStoryPage || treasureStoryRenderedPanel != treasureStoryPanel;
            treasureStoryRenderedKey = key;
            treasureStoryRenderedPage = treasureStoryPage;
            treasureStoryRenderedPanel = treasureStoryPanel;
            if (pageChanged && Application.isPlaying)
                PlaySfx("battle_card_play", .30f);

            Button advance = CreateComicAdvance("Legendary Fleet Comic Advance", overlay);
            advance.onClick.AddListener(() =>
            {
                if (!CurrentPage()) return;
                if (treasureStoryPanel < panels.Count - 1)
                {
                    treasureStoryPanel++;
                    Render();
                    return;
                }
                treasureStoryPanel = 0;
                LegendaryComicReadProgress?.MarkReadStoryKey(key);
                treasureStoryCompletedKeys.Add(key);
                if (key == LegendaryStoryKey(pendingLegendaryVictoryId, true)) pendingLegendaryVictoryId = string.Empty;
                treasureStoryKey = string.Empty;
                treasureStoryPage = 0;
                Render();
            });
            treasureStoryAutoAdvance = overlay.gameObject.AddComponent<ComicAutoAdvance>();
            treasureStoryAutoAdvance.Configure(advance, TreasureStorySecondsPerPage, CurrentPage);
            if (!pageChanged && treasureStoryTimerKey == key && treasureStoryTimerPage == displayedPage &&
                treasureStoryTimerPanel == displayedPanel)
                treasureStoryAutoAdvance.RestoreRemainingSeconds(treasureStoryRemainingSeconds);
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
                treasureStoryTimerPanel = treasureStoryRenderedPanel;
            }
            treasureStoryAutoAdvance = null;
            treasureStoryOverlay = null;
            CloseComicOverlay(overlay);
        }
    }
}
