using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildTacticalNavalScene(RectTransform root, CoreBattleState battle)
        {
            RectTransform boardRect = CreateRect("Naval Tactical Board", root, new Vector2(1860f, 900f), Vector2.zero);
            NavalTacticalBoardView board = boardRect.gameObject.AddComponent<NavalTacticalBoardView>();
            Image player = CreateImage("Player Ship Art", root, new Vector2(112f, 168f), Vector2.zero, Color.white);
            Image enemy = CreateImage("Enemy Ship Art", root, new Vector2(112f, 168f), Vector2.zero, Color.white);
            foreach (Image ship in new[] { player, enemy })
            {
                ship.raycastTarget = false;
                Outline outline = ship.gameObject.AddComponent<Outline>();
                outline.effectColor = ship == player ? new Color(.15f, .86f, 1f, .95f) : new Color(1f, .24f, .16f, .95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
            CreateBattleHullBar("Player Battle Hull Bar", root, new Vector2(310f, 24f), Vector2.zero,
                session.Hull, session.MaxHull, Green);
            CreateBattleHullBar("Enemy Battle Hull Bar", root, new Vector2(310f, 24f), Vector2.zero,
                battle.EnemyHull, battle.EnemyMaxHull, Coral);
            board.Configure(battle, player.rectTransform, enemy.rectTransform,
                root.Find("Player Battle Hull Bar Track") as RectTransform,
                root.Find("Enemy Battle Hull Bar Track") as RectTransform, interfaceFont, session);
            NavalAttackRangePreview attackRange = boardRect.gameObject.AddComponent<NavalAttackRangePreview>();
            attackRange.Configure(session, board, interfaceFont,
                () => !isActiveAndEnabled || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen);
            boardRect.gameObject.AddComponent<NavalFieldBoardOverlay>().Configure(session, board, interfaceFont, this);
            player.raycastTarget = true;
            player.gameObject.AddComponent<NavalAttackRangeShipClick>().Configure(attackRange);
            if (session.NavalActionMovementActive)
            {
                Image inputSurface = boardRect.gameObject.AddComponent<Image>();
                inputSurface.color = Color.clear;
                inputSurface.raycastTarget = true;
                NavalTacticalMovementInput movement = boardRect.gameObject.AddComponent<NavalTacticalMovementInput>();
                movement.Configure(session, board, root, interfaceFont,
                    () => !isActiveAndEnabled || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen || GuidedNavalMovementBlocked,
                    () => BeginNavalGridMovement(board, null), heading => BeginNavalGridMovement(board, heading));
            }
        }

        private void BeginNavalGridMovement(NavalTacticalBoardView board, float? arrivalHeading)
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen || GuidedNavalMovementBlocked ||
                board == null || !board.gameObject.activeInHierarchy || !session.NavalActionMovementActive) return;
            if (arrivalHeading.HasValue != session.Battle.NavalAwaitingHeadingSelection) return;
            StartCoroutine(AnimateNavalGridMovement(board, arrivalHeading));
        }

        private IEnumerator AnimateNavalGridMovement(NavalTacticalBoardView board, float? arrivalHeading)
        {
            battleAnimationPlaying = true;
            try
            {
                CoreBattleState battle = session.Battle;
                if (battle == null) yield break;
                BattleAnimationSnapshot before = new BattleAnimationSnapshot(session, battle);
                NavalBoardingConnection ramConnection = NavalBoardingConnection.Planned;
                bool committed = arrivalHeading.HasValue
                    ? session.ConfirmNavalArrivalHeading(arrivalHeading.Value, out _)
                    : session.CommitNavalMovement(out _, out ramConnection);
                if (!committed) yield break;
                BattleAnimationSnapshot after = new BattleAnimationSnapshot(session, session.Battle ?? battle);
                RectTransform stage = board.transform.parent as RectTransform;
                RefreshBattleStateChangePresentation(stage, battle);
                LockBattleInput(stage);
                PlaySfx("battle_maneuver", .72f);
                // A lethal field clears session.Battle during the commit. The captured
                // battle retains the authoritative shortened route and terminal result.
                yield return board.AnimateMovement(battle.LastTacticalMovement);
                if (ramConnection != NavalBoardingConnection.Planned && stage != null)
                    yield return AnimateCommittedPlayerRam(stage, stage.Find("Player Ship Art") as RectTransform,
                        stage.Find("Enemy Ship Art") as RectTransform, before, after, ramConnection, true);
                if (session.Phase != CoreDemoPhase.Battle && stage != null)
                {
                    RectTransform player = stage.Find("Player Ship Art") as RectTransform;
                    RectTransform enemy = stage.Find("Enemy Ship Art") as RectTransform;
                    Image playerHull = stage.Find("Player Battle Hull Bar Track/Player Battle Hull Bar")?.GetComponent<Image>();
                    if (playerHull != null && ramConnection == NavalBoardingConnection.Planned)
                        yield return AnimateBattleDamage(playerHull, before.PlayerHull, after.PlayerHull,
                            before.PlayerMaxHull, before.PlayerBlock, after.PlayerBlock, stage);
                    yield return AnimateStagedBattleResolution(stage, player, enemy, before, after);
                }
            }
            finally
            {
                CompleteStagedBattleAnimation();
            }
        }

        private IEnumerator AnimateTacticalNavalMovement(RectTransform root, BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            NavalTacticalBoardView board = root == null ? null : root.GetComponentInChildren<NavalTacticalBoardView>();
            if (board == null || after.TacticalMovement == null || before.TacticalRevision == after.TacticalRevision) yield break;
            PlaySfx("battle_maneuver", .72f);
            yield return board.AnimateMovement(after.TacticalMovement);
            Text range = FindBattleReadout(root, "Battle Range");
            if (range != null && after.TacticalMovement.Frames.Count > 0)
            {
                CoreNavalTacticalFrame finalFrame = after.TacticalMovement.Frames[after.TacticalMovement.Frames.Count - 1];
                range.text = CoreVoyageSession.NavalGridDistance(finalFrame.PlayerPositionMeters, finalFrame.EnemyPositionMeters) + " 格 / 炮击4格";
            }
        }

        private IEnumerator AnimateTacticalBoardingConnection(RectTransform stage, RectTransform player, RectTransform enemy)
        {
            PlaySfx("battle_melee", .84f);
            Image[] ropes = new Image[2];
            try
            {
                Vector2 from = player.anchoredPosition;
                Vector2 to = enemy.anchoredPosition;
                Vector2 direction = (to - from).normalized;
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float inset = Mathf.Min(12f, Vector2.Distance(from, to) * .2f);
                for (int index = 0; index < ropes.Length; index++)
                {
                    Vector2 side = perpendicular * (index == 0 ? 16f : -16f);
                    ropes[index] = CreateLine(stage, from + direction * inset + side,
                        to - direction * inset + side, Gold, 3f);
                    ropes[index].name = "Tactical Boarding Rope " + index;
                    ropes[index].raycastTarget = false;
                }
                // The ships are already within boarding range. Keep their real
                // chart positions instead of spreading them to old side-view anchors.
                for (float elapsed = 0f; elapsed < .4f; elapsed += Time.unscaledDeltaTime)
                {
                    if (stage == null || player == null || enemy == null) yield break;
                    foreach (Image rope in ropes)
                        if (rope != null) rope.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01(elapsed / .2f));
                    yield return null;
                }
            }
            finally
            {
                foreach (Image rope in ropes) if (rope != null) Destroy(rope.gameObject);
            }
        }
    }
}
