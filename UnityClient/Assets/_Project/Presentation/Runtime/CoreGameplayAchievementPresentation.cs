using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool[] knownAchievementStates;
        private bool[] committedAchievementStates;
        private AchievementToastPresenter achievementToast;

        private void InitializeAchievementNotifications()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked) return;
            knownAchievementStates = new bool[AchievementCatalog.All.Count];
            committedAchievementStates = new bool[AchievementCatalog.All.Count];
            // Include old saves whose earned conditions have not yet been
            // captured. Opening a game must not replay its existing milestones.
            AchievementCatalog.FillUnlockStates(knownAchievementStates);
        }

        private void NotifyCommittedAchievementsSafely()
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked) return;
            try
            {
                if (knownAchievementStates == null) { InitializeAchievementNotifications(); return; }
                AchievementCatalog.FillCapturedStates(committedAchievementStates);
                int first = -1, count = 0;
                for (int index = 0; index < knownAchievementStates.Length; index++)
                {
                    if (!committedAchievementStates[index] || knownAchievementStates[index]) continue;
                    knownAchievementStates[index] = true;
                    if (first < 0) first = index;
                    count++;
                }
                if (first < 0) return;
                if (achievementToast == null) achievementToast = BuildAchievementToast();
                achievementToast.Show(AchievementCatalog.All[first], count);
            }
            catch (Exception error)
            {
                // The journal has already completed. UI errors cannot block
                // the caller's successful save/return action or revive it.
                if (achievementToast != null) achievementToast.Hide();
                Debug.LogWarning("成就提示暂未显示；角色保存已完成。" + error.Message);
            }
        }

        private void ClearAchievementNotifications()
        {
            if (achievementToast != null) achievementToast.Hide();
        }

        private AchievementToastPresenter BuildAchievementToast()
        {
            var host = new GameObject("Achievement Notification Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            host.transform.SetParent(legacyCanvasRoot, false);
            var hostRect = host.GetComponent<RectTransform>(); hostRect.anchorMin = Vector2.zero; hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = hostRect.offsetMax = Vector2.zero;
            Canvas canvas = host.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 1500;
            CanvasGroup group = host.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            RectTransform card = CreateRect("Achievement Notification", host.transform, new Vector2(480f, 98f), new Vector2(-28f, -26f));
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one;
            Image face = card.gameObject.AddComponent<Image>(); face.color = new Color(.035f, .13f, .15f, .98f); face.raycastTarget = false;
            RawImage icon = CreateRect("Achievement Notification Icon", card, new Vector2(48f, 48f), new Vector2(-192f, 0f)).gameObject.AddComponent<RawImage>();
            icon.raycastTarget = false;
            Text title = CreateText("Achievement Notification Title", card, new Vector2(360f, 32f), new Vector2(38f, 16f), 24, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            Text detail = CreateText("Achievement Notification Detail", card, new Vector2(360f, 28f), new Vector2(38f, -19f), 18, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            var view = host.AddComponent<AchievementToastPresenter>();
            view.Owner = this; view.Card = card.gameObject; view.Icon = icon; view.Title = title; view.Detail = detail;
            card.gameObject.SetActive(false);
            return view;
        }

        // A separate presenter owns the short display lifetime. No per-frame
        // achievement queries, modal input, focus changes or persistent keys.
        private sealed class AchievementToastPresenter : MonoBehaviour
        {
            internal CoreGameplayDemoController Owner;
            internal GameObject Card;
            internal RawImage Icon;
            internal Text Title, Detail;
            private float expires;

            internal void Show(CoreAchievementDefinition definition, int count)
            {
                ItemIconArt art = AchievementIconCatalog.ForId(definition.Id);
                Icon.texture = art.Texture; Icon.uvRect = art.Uv; Icon.enabled = art.IsAvailable;
                UiImageAspect.Fit(Icon, new Vector2(48f, 48f));
                Title.text = "成就解锁 · " + definition.Title;
                Detail.text = count > 1 ? "同时解锁 " + count + " 项成就 · 主菜单可查看" : "主菜单可查看成就";
                expires = Time.unscaledTime + 4f;
                Card.SetActive(true);
            }

            internal void Hide() { if (Card != null) Card.SetActive(false); }
            private void OnDisable() => Hide();
            private void Update()
            {
                if (Card == null || !Card.activeSelf) return;
                if (Owner == null || !Owner.isActiveAndEnabled || Owner.profileSaveBlocked || Time.unscaledTime >= expires) Hide();
            }
        }
    }
}
