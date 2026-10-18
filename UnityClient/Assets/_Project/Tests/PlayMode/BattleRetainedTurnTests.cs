using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator BattleResponsive_SameStageEnemyTurnRetainsSceneAndRefreshesAllLiveHudValues()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_chain_shot");
            SetResponsiveDrawPile(session, "card_brace", "card_chain_shot", "card_emergency_repair", "card_brace", "card_full_sail_turn");
            CoreBattleState battle = session.Battle;
            battle.Energy = 0;
            battle.PlayerBlock = 7;
            battle.EnemyBlock = 1;
            battle.PlayerDeckBuff = 2;
            battle.PlayerLoadedShot = 3;
            battle.PlayerFireRounds = 2;
            battle.EnemyBurnTurns = 2;
            battle.EnemyBurnDamage = 3;
            ConfigureCloseNavalTestPose(battle, false);
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(battle, new object[] { true, new VoyagePoint(-200f, 0f), 0f });
            battle.PhaseTurn = 2;
            int originalTurn = battle.Turn;
            int originalEnemyHull = battle.EnemyHull;
            RenderResponsiveBattle(controller);
            yield return null;

            RectTransform originalScene = RequiredResponsive<RectTransform>("Battle Card");
            NavalTacticalBoardView originalBoard = RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board");
            RawImage originalSea = RequiredResponsive<RawImage>("Shared tactical sea");
            NavalTacticalGridGraphic originalGrid = RequiredResponsive<NavalTacticalGridGraphic>("100 metre grid");
            Texture originalSeaTexture = originalSea.texture;
            Assert.That(originalSeaTexture, Is.Not.Null);
            Assert.That(originalSea.transform.IsChildOf(originalBoard.transform), Is.True);
            Assert.That(originalGrid.transform.IsChildOf(originalBoard.transform), Is.True);
            RectTransform originalPlayer = RequiredResponsive<RectTransform>("Player Ship Art");
            RectTransform originalEnemy = RequiredResponsive<RectTransform>("Enemy Ship Art");
            BattleDefenseHud originalPlayerHud = RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            BattleDefenseHud originalEnemyHud = RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track");
            int[] originalIds = { originalScene.GetInstanceID(), originalBoard.GetInstanceID(), originalSea.GetInstanceID(), originalGrid.GetInstanceID(),
                originalPlayer.GetInstanceID(), originalEnemy.GetInstanceID(), originalPlayerHud.GetInstanceID(), originalEnemyHud.GetInstanceID() };
            Assert.That(RetainedVisibleBadgeAmount("Resource Energy"), Is.EqualTo("0/" + battle.MaxEnergy));
            BattleStatusRow initialPlayerStatuses = AssertRetainedVisibleStatuses(session, true);
            Assert.That(RetainedVisibleStatusAmount(initialPlayerStatuses, "guard"), Is.EqualTo("7"));
            AssertRetainedVisibleStatuses(session, false);
            AssertRetainedVisibleIntents(session);

            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle, Is.SameAs(battle));
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(battle.Turn, Is.EqualTo(originalTurn + 1));
            CollectionAssert.AreEqual(originalIds, new[] {
                RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(),
                RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board").GetInstanceID(),
                RequiredResponsive<RawImage>("Shared tactical sea").GetInstanceID(),
                RequiredResponsive<NavalTacticalGridGraphic>("100 metre grid").GetInstanceID(),
                RequiredResponsive<RectTransform>("Player Ship Art").GetInstanceID(),
                RequiredResponsive<RectTransform>("Enemy Ship Art").GetInstanceID(),
                RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").GetInstanceID(),
                RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track").GetInstanceID()
            }, "The same-stage enemy turn must not reconstruct the tactical board, sea, grid, ships or defense HUDs.");
            Assert.That(originalSea.texture, Is.SameAs(originalSeaTexture));
            Assert.That(originalBoard.IsAnimating, Is.False);
            Assert.That(RequiredResponsive<Text>("Battle Badge").text, Does.EndWith("回合 " + battle.Turn));
            Assert.That(GameObject.Find("Battle Status"), Is.Null, "Retained turns keep the removed hint absent.");
            Assert.That(GameObject.Find("Battle Card Cost Rule Panel"), Is.Null);
            Assert.That(GameObject.Find("Battle Collision Quality"), Is.Null, "Retained turns keep collision quality text removed.");
            Assert.That(RequiredResponsive<Text>("Battle Range").text, Is.EqualTo(session.NavalDistanceCells + " 格 / 炮击4格"));
            AssertRetainedVisibleIntents(session);
            Assert.That(RetainedVisibleBadgeAmount("Resource Energy"), Is.EqualTo(battle.Energy + "/" + battle.MaxEnergy));
            Assert.That(RetainedVisibleBadgeAmount("Resource Powder"), Is.EqualTo(session.Cargo.Powder.ToString()));
            Assert.That(RetainedVisibleBadgeAmount("Resource Cannonball"), Is.EqualTo(session.Cargo.Cannonballs.ToString()));
            Assert.That(RetainedVisibleBadgeAmount("Resource Planks"), Is.EqualTo(session.Cargo.Wood.ToString()));
            Assert.That(RetainedVisibleBadgeAmount("Battle Draw Pile"), Is.EqualTo(battle.DrawPile.Count.ToString()));
            Assert.That(RetainedVisibleBadgeAmount("Battle Discard Pile"), Is.EqualTo(battle.DiscardPile.Count.ToString()));
            BattleStatusRow playerStatuses = AssertRetainedVisibleStatuses(session, true);
            BattleStatusRow enemyStatuses = AssertRetainedVisibleStatuses(session, false);
            Assert.That(playerStatuses.transform.Find("Status guard"), Is.Null, "Expired guard must leave the visible status set.");
            Assert.That(RetainedVisibleStatusAmount(playerStatuses, "loaded_shot"), Is.EqualTo(session.PlayerLoadedShotDamageBonus.ToString()));
            Assert.That(RetainedVisibleStatusAmount(playerStatuses, "deck_buff"), Is.EqualTo("2"));
            Assert.That(RetainedVisibleStatusAmount(playerStatuses, "fire_rounds"), Is.EqualTo("2"));
            Assert.That(RetainedVisibleStatusAmount(enemyStatuses, "burn"), Is.EqualTo("3"));
            // Burn damage lives on the badge; the remaining duration is visible in its real hover explanation.
            Transform burn = enemyStatuses.transform.Find("Status burn");
            var pointer = new PointerEventData(EventSystem.current);
            Assert.That(ExecuteEvents.Execute(burn.gameObject, pointer, ExecuteEvents.pointerEnterHandler), Is.True);
            yield return null;
            Text burnExplanation = RequiredResponsive<RectTransform>("Battle Status Tooltip").Find("Description").GetComponent<Text>();
            Assert.That(burnExplanation.text, Does.Contain("直接失去 3 点生命").And.Contain("剩余 1 次"));
            ExecuteEvents.Execute(burn.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return null;
            Assert.That(originalPlayerHud.DisplayedHull, Is.EqualTo(session.Hull));
            Assert.That(originalPlayerHud.DisplayedGuard, Is.EqualTo(battle.PlayerBlock));
            Assert.That(battle.PlayerBlock, Is.Zero);
            Assert.That(originalEnemyHud.DisplayedHull, Is.EqualTo(battle.EnemyHull));
            Assert.That(originalEnemyHud.DisplayedGuard, Is.EqualTo(battle.EnemyBlock));
            Assert.That(battle.EnemyHull, Is.EqualTo(originalEnemyHull - 3));
            Assert.That(battle.EnemyBurnTurns, Is.EqualTo(1));
            Assert.That(RequiredResponsive<Button>("Battle Ram").interactable, Is.EqualTo(session.CanRam(out _)));
            Assert.That(RequiredResponsive<Button>("Battle Board").interactable, Is.EqualTo(session.CanBoard(out _)));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }

        private static string RetainedVisibleBadgeAmount(string name)
        {
            RectTransform badge = RequiredResponsive<RectTransform>(name);
            Text amount = badge.Find("Amount").GetComponent<Text>();
            Assert.That(amount.gameObject.activeInHierarchy, Is.True, name + " amount must be visible.");
            return amount.text;
        }

        private static string RetainedVisibleStatusAmount(BattleStatusRow row, string id)
        {
            Transform slot = row.transform.Find("Status " + id);
            Assert.That(slot, Is.Not.Null, "Missing live status: " + id);
            Assert.That(slot.gameObject.activeInHierarchy, Is.True, id + " must be visible.");
            return slot.Find("Amount").GetComponent<Text>().text;
        }

        private static BattleStatusRow AssertRetainedVisibleStatuses(CoreVoyageSession session, bool player)
        {
            BattleStatusRow row = RequiredResponsive<BattleStatusRow>((player ? "Player" : "Enemy") + " Battle Status Row");
            var expected = CoreBattleStatusPresentation.Build(session, player);
            Assert.That(row.IsSuppressed, Is.False);
            Assert.That(row.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(row.VisibleStatusCount, Is.EqualTo(expected.Count));
            foreach (CoreBattleStatus status in expected)
                Assert.That(RetainedVisibleStatusAmount(row, status.Id), Is.EqualTo(status.Value), status.Id);
            return row;
        }

        private static void AssertRetainedVisibleIntents(CoreVoyageSession session)
        {
            RectTransform row = RequiredResponsive<RectTransform>("Enemy Intent Icons");
            var expected = CoreBattleIntentPresentation.Build(session);
            Assert.That(expected.Count, Is.GreaterThan(0));
            Assert.That(row.Cast<Transform>().Count(child => child.gameObject.activeInHierarchy), Is.EqualTo(expected.Count));
            foreach (CoreBattleIntent intent in expected)
            {
                Transform slot = row.Find("Intent " + intent.Id);
                Assert.That(slot, Is.Not.Null, "Missing live enemy intent: " + intent.Id);
                Assert.That(slot.gameObject.activeInHierarchy, Is.True);
                Assert.That(slot.Find("Amount").GetComponent<Text>().text, Is.EqualTo(intent.Value), intent.Id);
            }
        }
    }
}
