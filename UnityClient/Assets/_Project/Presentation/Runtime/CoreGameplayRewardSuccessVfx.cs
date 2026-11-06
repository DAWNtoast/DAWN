using System;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private RectTransform FindRewardVisual(string name)
        {
            if (contentRoot == null) return null;
            foreach (RectTransform view in contentRoot.GetComponentsInChildren<RectTransform>())
                if (view.name == name && view.gameObject.activeInHierarchy) return view;
            return null;
        }

        private void PlayCommittedCardUpgradeVfx(int index, string previousDescription)
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || !isActiveAndEnabled) return;
            try
            {
                RectTransform face = FindRewardVisual("Upgrade Comparison Current Face");
                if (face == null) return;
                var card = session.GetBattleCard(index, selectedBattleDeckStage);
                Transform effectView = face.Find("Upgrade Comparison Body Current");
                Text effect = effectView == null ? null : effectView.GetComponent<Text>();
                Font font = effect != null ? effect.font : GameTypography.BodyFont;
                RewardSuccessMotion motion = RewardSuccessMotion.Play(face, CardSchoolArtCatalog.FrameBounds(card),
                    this, font, "+" + card.UpgradeLevel + " · 强化完成", new Color(1f, .79f, .35f));
                motion.HighlightChangedNumbers(effect, previousDescription);
            }
            catch (Exception error) { Debug.LogWarning("强化已完成；强化动效暂未显示。" + error.Message); }
        }

        private void PlayCommittedRecruitmentVfx(string crewName)
        {
            if (!Application.isPlaying || editorPreviewBuilding || profileSaveBlocked || !isActiveAndEnabled) return;
            try
            {
                RectTransform detail = FindRewardVisual("Tavern Crew Detail");
                if (detail == null) return;
                RectTransform nameView = FindRewardVisual("Tavern Crew Detail Name");
                Text name = nameView == null ? null : nameView.GetComponent<Text>();
                Font font = name != null ? name.font : GameTypography.BodyFont;
                Rect bounds = new Rect(TavernDossierLayerWindowPosition - TavernDossierLayerWindowSize * .5f,
                    TavernDossierLayerWindowSize);
                RewardSuccessMotion.Play(detail, bounds, this, font, "契约已签 · " + crewName,
                    new Color(1f, .80f, .40f), true);
            }
            catch (Exception error) { Debug.LogWarning("招募已完成；签约动效暂未显示。" + error.Message); }
        }
    }
}
