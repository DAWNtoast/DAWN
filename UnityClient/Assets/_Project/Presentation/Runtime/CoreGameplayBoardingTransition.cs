using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform boardingTransitionOverlay;
        private readonly List<BoardingArrivalPose> boardingArrivalPoses = new List<BoardingArrivalPose>(4);

        private struct BoardingArrivalPose
        {
            public RectTransform Rect;
            public Vector2 Position, Offset;
            public Vector3 Scale;
            public CanvasGroup Group;
            public float Alpha;
            public bool Party;
        }

        private IEnumerator AnimateBoardingSceneTransition()
        {
            if (contentRoot == null || session.Phase != CoreDemoPhase.Battle ||
                session.Battle == null || session.Battle.Stage != NavalBattleStage.Boarding) yield break;
            CloseBoardingTransition();
            // A sibling survives the phase-content rebuild at full coverage.
            boardingTransitionOverlay = CreateStretchPanel("Boarding Scene Transition", contentRoot.parent, Color.clear);
            Canvas canvas = boardingTransitionOverlay.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 160;
            boardingTransitionOverlay.gameObject.AddComponent<GraphicRaycaster>();
            Image blocker = boardingTransitionOverlay.GetComponent<Image>();
            blocker.raycastTarget = true;
            RectTransform wipeRoot = CreateRect("Boarding Transition Wipe", boardingTransitionOverlay, Vector2.zero, Vector2.zero);
            wipeRoot.anchorMin = Vector2.zero;
            wipeRoot.anchorMax = Vector2.one;
            BoardingSceneWipeGraphic wipe = wipeRoot.gameObject.AddComponent<BoardingSceneWipeGraphic>();
            wipe.color = new Color(.88f, .84f, .70f, 1f);
            wipe.raycastTarget = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            try
            {
                const float expand = .30f;
                for (float elapsed = 0f; elapsed < expand && wipe != null; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / expand);
                    wipe.SetProgress(t);
                    blocker.color = new Color(.015f, .025f, .032f, t * .85f);
                    yield return null;
                }
                if (wipe == null) yield break;
                wipe.SetProgress(1f);
                yield return null; // Show one fully covered frame before swapping views.
                CloseRamComic();
                Render();
                if (retainedBattleRoot != null) LockBattleInput(retainedBattleRoot);
                PrepareBoardingArrival();
                ApplyBoardingArrival(0f);
                const float reveal = .48f;
                for (float elapsed = 0f; elapsed < reveal && wipe != null; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / reveal);
                    wipe.SetProgress(1f - t);
                    blocker.color = new Color(.015f, .025f, .032f, (1f - t) * .85f);
                    ApplyBoardingArrival(t);
                    yield return null;
                }
                ApplyBoardingArrival(1f);
            }
            finally { CloseBoardingTransition(); }
        }

        private void PrepareBoardingArrival()
        {
            boardingArrivalPoses.Clear();
            if (retainedBattleRoot == null) return;
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "Battle Hand",
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" })
            {
                RectTransform rect = retainedBattleRoot.Find(name) as RectTransform;
                if (rect == null) continue;
                bool party = name.EndsWith("Ship Art", System.StringComparison.Ordinal);
                CanvasGroup group = GetOrAddCanvasGroup(rect.gameObject);
                boardingArrivalPoses.Add(new BoardingArrivalPose
                {
                    Rect = rect, Position = rect.anchoredPosition, Scale = rect.localScale,
                    Offset = party ? new Vector2(-rect.anchoredPosition.x * .35f, 20f) : new Vector2(0f, -70f),
                    Group = group, Alpha = group.alpha, Party = party,
                });
            }
        }

        private void ApplyBoardingArrival(float progress)
        {
            foreach (BoardingArrivalPose pose in boardingArrivalPoses)
            {
                if (pose.Rect == null) continue;
                pose.Rect.anchoredPosition = pose.Position + pose.Offset * (1f - progress);
                pose.Rect.localScale = pose.Scale * (pose.Party ? Mathf.Lerp(.76f, 1f, progress) : 1f);
                if (pose.Group != null) pose.Group.alpha = pose.Alpha * Mathf.Lerp(.15f, 1f, progress);
            }
        }

        private void CloseBoardingTransition()
        {
            ApplyBoardingArrival(1f);
            boardingArrivalPoses.Clear();
            RectTransform overlay = boardingTransitionOverlay;
            boardingTransitionOverlay = null;
            if (overlay == null) return;
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
        }
    }
}
