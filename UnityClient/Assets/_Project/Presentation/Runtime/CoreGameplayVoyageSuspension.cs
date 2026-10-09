using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool voyageMenuTransition;
        private bool CanSuspendVoyage => session != null && session.CanCaptureVoyageCheckpoint && !navigationMotionPlaying;

        private bool TrySaveAndReturnToMainMenu(out string reason)
        {
            reason = "当前阶段暂不能返回主菜单，请先完成当前操作。";
            if (profileSaveBlocked) { reason = profileSaveFailure ?? "角色保存尚未恢复，已停止档案写入。"; return false; }
            if (!Application.isPlaying || editorPreviewBuilding || voyageMenuTransition || session == null || navigationMotionPlaying) return false;
            if (TryGetSeaChartSaveProtectionReason(out reason)) return false;
            if (!Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                reason = "主菜单场景不可用，请保留当前游戏并检查安装文件。";
                return false;
            }
            if (treasureProgressReadFailed)
            {
                reason = "藏宝档案无法读取，已保护原档案。请先备份并修复档案后再保存。";
                return false;
            }
            voyageMenuTransition = true;
            try
            {
                if (session.Phase == CoreDemoPhase.Port)
                {
                    if (!VoyageSuspensionPersistence.TryRead(out VoyageSuspensionRecord pending, out reason)) return false;
                    if (pending != null) { reason = "另有未恢复的航程，请先通过主菜单继续该航程。"; return false; }
                    SaveProgress();
                }
                else if (!TrySaveVoyageSuspension(out reason)) return false;
                SceneManager.LoadScene("MainMenu");
                reason = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = "保存或返回失败，当前航程仍保留在游戏中：" + exception.Message;
                return false;
            }
            finally { voyageMenuTransition = false; }
        }

        private bool TrySaveVoyageSuspension(out string reason)
        {
            reason = "请等待航行动作结束后再保存当前航程。";
            if (profileSaveBlocked) { reason = profileSaveFailure ?? "角色保存尚未恢复，已停止档案写入。"; return false; }
            if (!Application.isPlaying || editorPreviewBuilding || !CanSuspendVoyage) return false;
            if (TryGetSeaChartSaveProtectionReason(out reason)) return false;
            if (treasureProgressReadFailed)
            {
                reason = "藏宝档案无法读取，原档案已保留，未覆盖当前存档。";
                return false;
            }
            // Flush already-earned component/collection records without writing
            // voyage cargo into the settled port profile.
            PlayerPrefs.Save();
            return VoyageSuspensionPersistence.TrySave(new VoyageSuspensionRecord
            {
                format = VoyageSuspensionPersistence.Format, version = 1,
                profileScope = PlayerPrefs.GetString(WorldEconomyScopeKey, string.Empty),
                voyage = session.CaptureVoyageCheckpoint(), tutorialActive = tutorialActive,
                tutorialCheckpoint = guidedTutorialCheckpoint, navigationInputStep = (int)guidedTutorialNavigationInputStep,
                selectedOrdinaryCrew = selectedDepartureOrdinaryCrew, departureCrewShipIndex = departureCrewShipIndex,
                departureCrewPort = departureCrewPort ?? string.Empty,
                selectedSpecialCrew = selectedDepartureSpecialCrew.ToArray(), standbyCrew = departureStandbyCrew.ToArray(),
            }, out reason);
        }

        private bool TryRestoreSuspendedVoyage(VoyageSuspensionRecord record, out string reason)
        {
            if (!VoyageSuspensionPersistence.Validate(record, out reason)) return false;
            if (TryGetSeaChartSaveProtectionReason(out reason)) return false;
            if (treasureProgressReadFailed)
            {
                reason = "藏宝档案无法读取，航程及原档案已保留，请先备份后修复档案。";
                return false;
            }
            if (!session.TryRestoreVoyageCheckpoint(record.voyage, out reason)) return false;
            tutorialActive = record.tutorialActive;
            guidedTutorialCheckpoint = record.tutorialCheckpoint;
            guidedTutorialNavigationInputStep = (GuidedTutorialNavigationInputStep)record.navigationInputStep;
            guidedNavigationInitialized = true;
            guidedNavigationMap = session.Map;
            guidedNavigationPreviousPhase = session.Phase;
            guidedNavigationDistanceNm = session.TotalDistanceNm;
            guidedNavigationCompletedEncounters = session.GuidedTutorialCompletedEncounters;
            selectedDepartureOrdinaryCrew = record.selectedOrdinaryCrew;
            departureCrewShipIndex = record.departureCrewShipIndex;
            departureCrewPort = record.departureCrewPort;
            selectedDepartureSpecialCrew.Clear();
            selectedDepartureSpecialCrew.AddRange(record.selectedSpecialCrew);
            departureStandbyCrew.Clear();
            foreach (string crew in record.standbyCrew) departureStandbyCrew.Add(crew);
            // Consume the parked snapshot before any gameplay/UI reward writer
            // can run. Otherwise an old snapshot could replay an already banked
            // encounter reward. The next explicit return writes a fresh snapshot.
            return VoyageSuspensionPersistence.TryClear(out reason);
        }

        private void ReturnToMenuAfterSuspensionFailure(string reason)
        {
            VoyageSuspensionPersistence.PendingNotice = reason;
            enabled = false;
            SceneManager.LoadScene("MainMenu");
        }
    }
}
