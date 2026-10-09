using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private CanvasGroup portSettingsSheetGroup;
        private Button returnMenuButton;
        private RectTransform returnMenuConfirmation;
        private Button returnMenuConfirmButton;
        private Button returnMenuCancelButton;
        private Text returnMenuMessage;
        private bool returnMenuSaving;

        private Button BuildReturnToMenuSettingsButton(RectTransform sheet)
        {
            returnMenuButton = CreatePortSettingsButton("Return To Main Menu", sheet,
                new Vector2(0f, -427f), "保存并返回主菜单", Html("#705136"));
            returnMenuButton.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 48f * GameReadability.Scale);
            returnMenuButton.GetComponentInChildren<Text>().rectTransform.sizeDelta = new Vector2(476f, 48f * GameReadability.Scale - 10f);
            RectTransform owner = portSettingsOverlay;
            returnMenuButton.onClick.AddListener(() =>
            {
                if (owner != null && owner == portSettingsOverlay && owner.gameObject.activeInHierarchy)
                    OpenReturnToMenuConfirmation();
            });
            return returnMenuButton;
        }

        private void OpenReturnToMenuConfirmation()
        {
            if (portSettingsOverlay == null || returnMenuSaving || returnMenuConfirmation != null) return;
            if (navigationMotionPlaying)
            {
                portSettingsHint.text = "请等待本次航行动画结束后再保存。";
                return;
            }
            if (Application.isPlaying && !editorPreviewBuilding &&
                (session == null || (session.Phase != CoreDemoPhase.Port && !CanSuspendVoyage)))
            {
                portSettingsHint.text = "当前阶段暂不能返回主菜单，请先完成当前操作。";
                return;
            }

            portSettingsSheetGroup.interactable = false;
            portSettingsSheetGroup.blocksRaycasts = false;
            returnMenuConfirmation = CreateStretchPanel("Return To Main Menu Confirmation", portSettingsOverlay,
                new Color(0.075f, 0.04f, 0.015f, 0.8f));
            RectTransform owner = returnMenuConfirmation;
            owner.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>().OnBackgroundPressed = FocusReturnToMenuConfirmation;
            RectTransform card = CreateFixedPanel("Return To Main Menu Sheet", owner,
                new Vector2(1040f, 520f), Vector2.zero, Html("#DFC591"));
            Text title = CreateText("Return To Main Menu Title", card, new Vector2(960f, 80f),
                new Vector2(0f, 166f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, Html("#322215"));
            GameReadability.Heading(title);
            title.text = "保存并返回主菜单？";
            returnMenuMessage = CreateText("Return To Main Menu Message", card, new Vector2(960f, 200f),
                new Vector2(0f, 10f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, Html("#322215"));
            GameReadability.Body(returnMenuMessage, 22);
            returnMenuMessage.text = session != null && session.Phase == CoreDemoPhase.Port
                ? "保存港口进度，下次可继续。\n未应用的设置不保存。"
                : "保存航程，下次可继续航行。\n未应用的设置不保存。";
            returnMenuCancelButton = CreatePortSettingsButton("Cancel Return To Main Menu", card,
                new Vector2(-225f, -170f), "取消 · 继续游戏", Html("#705136"));
            returnMenuConfirmButton = CreatePortSettingsButton("Confirm Return To Main Menu", card,
                new Vector2(225f, -170f), "保存并返回", Html("#98682D"));
            returnMenuCancelButton.onClick.AddListener(() =>
            {
                if (owner != null && owner == returnMenuConfirmation) CloseReturnToMenuConfirmation();
            });
            returnMenuConfirmButton.onClick.AddListener(() =>
            {
                if (owner != null && owner == returnMenuConfirmation) ConfirmReturnToMainMenu();
            });
            foreach (Button control in new[] { returnMenuCancelButton, returnMenuConfirmButton })
            {
                Button other = control == returnMenuCancelButton ? returnMenuConfirmButton : returnMenuCancelButton;
                control.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = other, selectOnDown = other, selectOnLeft = other, selectOnRight = other,
                };
                control.gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested = () =>
                {
                    if (owner != null && owner == returnMenuConfirmation) CloseReturnToMenuConfirmation();
                };
            }
            if (!Application.isPlaying) SetEditorPreviewFlags(owner);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(returnMenuCancelButton.gameObject);
        }

        private void FocusReturnToMenuConfirmation()
        {
            if (returnMenuConfirmation == null || returnMenuSaving || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(returnMenuConfirmation)) return;
            if (returnMenuCancelButton != null) EventSystem.current.SetSelectedGameObject(returnMenuCancelButton.gameObject);
        }

        private void CloseReturnToMenuConfirmation()
        {
            if (returnMenuSaving || returnMenuConfirmation == null) return;
            GameObject confirmation = returnMenuConfirmation.gameObject;
            confirmation.SetActive(false);
            returnMenuConfirmation = null;
            if (Application.isPlaying) Destroy(confirmation); else DestroyImmediate(confirmation);
            returnMenuConfirmButton = null;
            returnMenuCancelButton = null;
            returnMenuMessage = null;
            if (portSettingsSheetGroup != null)
            {
                portSettingsSheetGroup.interactable = true;
                portSettingsSheetGroup.blocksRaycasts = true;
            }
            if (EventSystem.current != null && returnMenuButton != null)
                EventSystem.current.SetSelectedGameObject(returnMenuButton.gameObject);
        }

        private void ConfirmReturnToMainMenu()
        {
            if (returnMenuSaving || returnMenuConfirmation == null) return;
            if (!Application.isPlaying || editorPreviewBuilding)
            {
                returnMenuMessage.text = "编辑预览：不保存进度或切换场景。";
                return;
            }
            if (navigationMotionPlaying)
            {
                returnMenuMessage.text = "请等待本次航行动画结束后再保存。当前航程仍保留在游戏中。";
                return;
            }
            returnMenuSaving = true;
            returnMenuConfirmButton.interactable = false;
            returnMenuCancelButton.interactable = false;
            returnMenuMessage.text = "正在保存当前进度……";
            bool saved = false;
            string reason;
            try { saved = TrySaveAndReturnToMainMenu(out reason); }
            catch (Exception)
            {
                reason = "保存过程出现异常，请重试。当前航程仍保留在游戏中。";
            }
            // The persistence owner switches scenes only after a successful
            // synchronous write. Keep the guard raised until that scene loads.
            if (saved) return;
            // Profile failures own a blocking recovery dialog. Keep keyboard
            // focus there instead of re-enabling the underlying confirmation.
            if (profileSaveBlocked) return;
            returnMenuSaving = false;
            if (returnMenuConfirmation == null) return;
            returnMenuConfirmButton.interactable = true;
            returnMenuCancelButton.interactable = true;
            returnMenuMessage.text = "未能保存并返回：" + (string.IsNullOrWhiteSpace(reason) ? "请稍后重试。" : reason);
            FocusReturnToMenuConfirmation();
        }
    }
}
