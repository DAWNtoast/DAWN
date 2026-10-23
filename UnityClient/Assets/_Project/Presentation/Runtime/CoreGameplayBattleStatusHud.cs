using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // Called after the rules commit an action and again by retained rendering.
        // Each view owns its baseline, so the latter refresh cannot replay a delta.
        private void RefreshBattleStateChangePresentation(RectTransform root, CoreBattleState battle)
        {
            if (root == null || battle == null || !isActiveAndEnabled || session == null ||
                session.Phase != CoreDemoPhase.Battle || !ReferenceEquals(session.Battle, battle) ||
                root != retainedBattleRoot || battle.Stage != retainedBattleStage || !root.gameObject.activeInHierarchy) return;
            RefreshBattleStatusHud(root, battle);
            RefreshCompactBattleResources(root, battle);
        }

        private void RefreshBattleStatusHud(RectTransform root, CoreBattleState battle)
        {
            NavalTacticalMovementInput movement = root.GetComponentInChildren<NavalTacticalMovementInput>();
            NavalTacticalBoardView board = root.GetComponentInChildren<NavalTacticalBoardView>();
            foreach (bool player in new[] { true, false })
            {
                string side = player ? "Player" : "Enemy";
                RectTransform track = root.Find(side + " Battle Hull Bar Track") as RectTransform;
                if (track == null) continue;
                BattleStatusRow row = root.Find(side + " Battle Status Row")?.GetComponent<BattleStatusRow>();
                if (row == null)
                {
                    RectTransform rect = CreateRect(side + " Battle Status Row", root, new Vector2(340f, 50f), Vector2.zero);
                    rect.pivot = new Vector2(.5f, 1f);
                    row = rect.gameObject.AddComponent<BattleStatusRow>();
                    row.Configure(interfaceFont, root, () =>
                        !isActiveAndEnabled || session == null || session.Battle != battle ||
                        session.Phase != CoreDemoPhase.Battle ||
                        battle.Stage != NavalBattleStage.Boarding &&
                        (movement != null && movement.isActiveAndEnabled && movement.IsShipSelected ||
                         battle.NavalAwaitingHeadingSelection || board != null && board.IsAnimating),
                        session.IsGuidedBossBattle && battle.Stage != NavalBattleStage.Boarding ? 90f : 140f);
                    row.FollowHealthBar(track);
                }
                row.SetCellSize(board == null ? 0f : board.CellSizePixels);
                row.SetOwner(this);
                row.SetStatuses(CoreBattleStatusPresentation.Build(session, player), Application.isPlaying && !editorPreviewBuilding);
            }
            Transform oldBuffs = root.Find("Battle Buff Panel");
            if (oldBuffs != null) oldBuffs.gameObject.SetActive(false);
            Transform burning = root.Find("Enemy Resource HUD/Enemy Burning");
            if (burning != null) burning.gameObject.SetActive(false);
            foreach (string side in new[] { "Player", "Enemy" })
            {
                Transform defense = root.Find(side + " Battle Hull Bar Track/" + side + " Armor Numeric");
                if (defense != null) defense.gameObject.SetActive(false);
            }
            if (battle.Stage == NavalBattleStage.Boarding)
            {
                RectTransform school = root.Find("Battle School Status Panel") as RectTransform;
                Text resources = school == null ? null : school.Find("Battle School Status")?.GetComponent<Text>();
                if (resources != null)
                {
                    resources.fontSize = 16;
                    resources.resizeTextForBestFit = false;
                    float height = Mathf.Clamp(resources.preferredHeight + 24f, 120f, 210f);
                    school.sizeDelta = new Vector2(190f, height);
                    school.anchoredPosition = new Vector2(-780f, 329f - height * .5f);
                    resources.rectTransform.sizeDelta = new Vector2(174f, height - 20f);
                }
                foreach (bool player in new[] { true, false })
                {
                    string side = player ? "Player" : "Enemy";
                    Vector2 position = new Vector2(player ? -480f : 480f, 410f);
                    SetNavalHudRect(root.Find(side + " Battle Name Board") as RectTransform, new Vector2(380f, 58f), position);
                    SetNavalHudText(root, side + " Battle Name", new Vector2(368f, 52f), position, 20);
                }
            }
        }
    }
}
