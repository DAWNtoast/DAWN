using System.Collections.Generic;
using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly HashSet<string> treasureStoryCompletedKeys = new HashSet<string>();
        private string treasureStoryKey = string.Empty;
        private int treasureStoryPage;
        private string treasureStoryRenderedKey = string.Empty;
        private int treasureStoryRenderedPage = -1;
        private Coroutine treasureStoryPageTransition;

        private void BuildTreasureStoryOverlayIfNeeded()
        {
            // Render retires the previous page even when its story/page number
            // is unchanged, or the next screen no longer needs an overlay.
            StopTreasureStoryPageTransition();
            if (session == null || !session.IsTreasureVoyage || session.ActiveTreasureMap == null) return;
            TreasureStoryChapter chapter = CoreTreasureStoryCatalog.FindChapter(session.ActiveTreasureMap.RankId);
            if (chapter == null || chapter.Pages.Count != 2 || chapter.ImageResources.Count != 2) return;

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
            bool battlePrelude = battle != null;
            string imageResource = chapter.ImageResources[Mathf.Clamp(treasureStoryPage, 0, chapter.ImageResources.Count - 1)];
            Texture2D comic = Resources.Load<Texture2D>(imageResource);
            RectTransform overlay = CreateStretchPanel("Treasure Story Overlay", contentRoot,
                new Color(0.005f, 0.012f, 0.018f, 0.975f));
            Image overlaySurface = overlay.GetComponent<Image>();
            overlaySurface.raycastTarget = true;
            int displayedPage = treasureStoryPage;
            bool CurrentPage() => overlay != null && overlay.gameObject.activeInHierarchy &&
                treasureStoryRenderedKey == key && treasureStoryRenderedPage == displayedPage;

            RectTransform card = CreateFixedPanel("Treasure Story Card", overlay,
                new Vector2(1780f, 980f), Vector2.zero, new Color(0.035f, 0.075f, 0.085f, 0.98f));
            Outline cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.75f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            float scale = GameReadability.Scale, eyebrowHeight = 28 * scale, titleHeight = 54 * scale;
            float titleTop = Mathf.Min(468 - eyebrowHeight - 12, 470 - 44 * scale - 12);
            float actionHeight = 56 * scale, actionY = -474 + actionHeight * .5f;
            Text eyebrow = CreateText("Treasure Story Eyebrow", card, new Vector2(1200f, eyebrowHeight),
                new Vector2(-110f, 468 - eyebrowHeight * .5f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Teal);
            GameReadability.Body(eyebrow, 18);
            eyebrow.text = battlePrelude ? "藏宝图旧部档案 · 战前记录" : "藏宝图航海日志 · 章节前奏";
            Text heading = CreateText("Treasure Story Heading", card, new Vector2(1560f, titleHeight),
                new Vector2(0f, titleTop - titleHeight * .5f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            GameReadability.Heading(heading, 32);
            heading.text = chapter.RankName + "藏宝图 · " + (battlePrelude ? battle.ShipName : chapter.Title) +
                "　「第 " + (treasureStoryPage + 1) + " / 2 页」";

            RectTransform artFrame = CreateFixedPanel("Treasure Story Comic Frame", card,
                new Vector2(1510f, 780f), new Vector2(0f, 30f), Html("#161B1B"));
            RawImage comicImage = CreateRawImage("Treasure Story Comic Page", artFrame,
                new Vector2(1492f, 762f), Vector2.zero, comic, new Rect(0f, 0f, 1f, 1f));
            comicImage.raycastTarget = false;

            // Render() rebuilds the modal for each page. Animate only when the
            // story key or page actually changes so unrelated re-renders do
            // not replay the turn and its sound effect.
            bool pageChanged = !string.Equals(treasureStoryRenderedKey, key, System.StringComparison.Ordinal) ||
                treasureStoryRenderedPage != treasureStoryPage;
            treasureStoryRenderedKey = key;
            treasureStoryRenderedPage = treasureStoryPage;
            if (pageChanged)
            {
                if (Application.isPlaying)
                    treasureStoryPageTransition = StartCoroutine(AnimateTreasureStoryPage(artFrame));
            }

            RectTransform dialogue = CreateFixedPanel("Treasure Story Dialogue Rail", card,
                new Vector2(1510f, 112f), new Vector2(0f, -401f), new Color(0.015f, 0.04f, 0.047f, 0.96f));
            Image dialogueSurface = dialogue.GetComponent<Image>();
            dialogueSurface.raycastTarget = false;
            string text = battlePrelude ? battle.Pages[treasureStoryPage] : chapter.Pages[treasureStoryPage];
            Text line = CreateText("Treasure Story Dialogue", dialogue, new Vector2(1390f, 84f),
                new Vector2(0f, 1f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.verticalOverflow = VerticalWrapMode.Truncate;
            GameReadability.Body(line, 22);
            line.lineSpacing = 1.15f;
            line.text = text;
            float textHeight = Mathf.Ceil(line.preferredHeight) + 12;
            float dialogueHeight = Mathf.Clamp(textHeight + 24, 100, 250);
            float dialogueBottom = -474 + actionHeight + 12;
            dialogue.sizeDelta = new Vector2(1510, dialogueHeight);
            dialogue.anchoredPosition = new Vector2(0, dialogueBottom + dialogueHeight * .5f);
            RectTransform dialogueViewport = CreateRect("Treasure Story Dialogue Viewport", dialogue,
                new Vector2(1460, dialogueHeight - 20), Vector2.zero);
            dialogueViewport.gameObject.AddComponent<RectMask2D>();
            Image readingSurface = dialogueViewport.gameObject.AddComponent<Image>();
            readingSurface.color = Color.clear;
            line.rectTransform.SetParent(dialogueViewport, false);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = line.rectTransform.pivot = new Vector2(.5f, 1);
            line.rectTransform.sizeDelta = new Vector2(1390, Mathf.Max(textHeight, dialogueHeight - 20));
            line.rectTransform.anchoredPosition = Vector2.zero;
            ScrollRect readingScroll = dialogueViewport.gameObject.AddComponent<ScrollRect>();
            readingScroll.viewport = dialogueViewport; readingScroll.content = line.rectTransform;
            readingScroll.horizontal = false; readingScroll.vertical = true;
            readingScroll.scrollSensitivity = 40; readingScroll.movementType = ScrollRect.MovementType.Clamped;
            float artTop = titleTop - titleHeight - 14;
            float artBottom = dialogueBottom + dialogueHeight + 14;
            artFrame.sizeDelta = new Vector2(1510, artTop - artBottom);
            artFrame.anchoredPosition = new Vector2(0, (artTop + artBottom) * .5f);
            UiImageAspect.Fit(comicImage, artFrame.sizeDelta - new Vector2(18, 18));

            Button previous = CreateButton("Treasure Story Previous", card,
                new Vector2(210f, actionHeight), new Vector2(-650f, actionY), PanelRaised, Ink, 22);
            previous.GetComponentInChildren<Text>().text = "上一页";
            GameReadability.Body(previous.GetComponentInChildren<Text>(), 22);
            previous.interactable = treasureStoryPage > 0;
            previous.onClick.AddListener(() =>
            {
                if (!CurrentPage() || treasureStoryPage <= 0) return;
                treasureStoryPage--;
                Render();
            });

            Button next = CreateButton("Treasure Story Next", card,
                new Vector2(300f, actionHeight), new Vector2(650f, actionY), Gold, DeepSea, 22);
            next.GetComponentInChildren<Text>().text = treasureStoryPage < 1 ? "继续阅读" : battlePrelude ? "进入海战" : "继续航行";
            GameReadability.Body(next.GetComponentInChildren<Text>(), 22);
            next.onClick.AddListener(() =>
            {
                if (!CurrentPage()) return;
                if (treasureStoryPage < 1)
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

            Button skip = CreateButton("Treasure Story Skip", card,
                new Vector2(240f, 44f * scale), new Vector2(738f, 470 - 22 * scale), PanelRaised, MutedInk, 22);
            skip.GetComponentInChildren<Text>().text = "略过剧情";
            GameReadability.Body(skip.GetComponentInChildren<Text>(), 22);
            skip.onClick.AddListener(() =>
            {
                if (!CurrentPage()) return;
                treasureStoryCompletedKeys.Add(key);
                treasureStoryKey = string.Empty;
                treasureStoryPage = 0;
                treasureStoryRenderedKey = string.Empty;
                treasureStoryRenderedPage = -1;
                StopTreasureStoryPageTransition();
                PlaySfx("encounter_choice", 0.38f);
                Render();
            });

            Text note = CreateText("Treasure Story Note", card, new Vector2(450f, 32f * scale),
                new Vector2(0f, actionY), 18, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            GameReadability.Body(note, 18);
            note.text = battlePrelude ? "" : "";
        }

        private void StopTreasureStoryPageTransition()
        {
            if (treasureStoryPageTransition == null) return;
            StopCoroutine(treasureStoryPageTransition);
            treasureStoryPageTransition = null;
        }

        private IEnumerator AnimateTreasureStoryPage(RectTransform artFrame)
        {
            if (artFrame == null) yield break;

            CanvasGroup group = artFrame.GetComponent<CanvasGroup>();
            if (group == null) group = artFrame.gameObject.AddComponent<CanvasGroup>();
            Vector3 finalScale = artFrame.localScale;
            artFrame.localScale = new Vector3(0.035f, finalScale.y, finalScale.z);
            group.alpha = 0f;
            // Interface cues are muted while the battle audio bus is active;
            // use the existing card cue for a battle prelude so the page turn
            // still has an audible confirmation before combat.
            PlaySfx(session != null && session.Phase == CoreDemoPhase.Battle ? "battle_card_play" : "chart_draw",
                session != null && session.Phase == CoreDemoPhase.Battle ? 0.30f : 0.46f);

            const float duration = 0.42f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (artFrame == null || group == null || !artFrame.gameObject.activeInHierarchy)
                {
                    treasureStoryPageTransition = null;
                    yield break;
                }
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                artFrame.localScale = new Vector3(Mathf.Lerp(0.035f, finalScale.x, eased), finalScale.y, finalScale.z);
                group.alpha = Mathf.Clamp01((t - 0.05f) / 0.55f);
                yield return null;
            }

            if (artFrame != null && group != null && artFrame.gameObject.activeInHierarchy)
            {
                artFrame.localScale = finalScale;
                group.alpha = 1f;
            }
            treasureStoryPageTransition = null;
        }
    }
}
