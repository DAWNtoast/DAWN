using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleStatusHudTests
    {
        // Reuse the existing battle fixture's save/restore contract and real scene.
        private readonly NavalBattleVisualFeedbackTests fixture = new NavalBattleVisualFeedbackTests();
        private CoreGameplayDemoController controller;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return fixture.SetUp();
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown() { yield return fixture.TearDown(); }

        [UnityTest]
        public IEnumerator NavalStatusRows_ShowBothSidesAndHoverExplainsTheCurrentStatus()
        {
            CoreVoyageSession session = FreshBattle();
            Render();
            yield return null;
            AssertRows(session);
            foreach (bool player in new[] { true, false })
            {
                var status = CoreBattleStatusPresentation.Build(session, player).First();
                BattleStatusRow row = Row(player);
                Transform badge = row.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Status " + status.Id);
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, badge.position) };
                Assert.That(ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerEnterHandler), Is.True);
                yield return null;
                GameObject tooltip = Named("Battle Status Tooltip", true);
                Assert.That(tooltip.activeInHierarchy, Is.True);
                string explanation = Regex.Replace(string.Join("\n", tooltip.GetComponentsInChildren<Text>(false).Select(text => text.text)), "<[^>]+>", string.Empty);
                Assert.That(explanation, Does.Contain(status.Name));
                Assert.That(explanation, Does.Contain(status.Tooltip));
                ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return null;
                Assert.That(tooltip.activeInHierarchy, Is.False);
            }
            NavalBattleVisualCapture.Capture(controller, "BattleHud_Naval.png", "海战连续血条及双方状态图标");
            // Retained refresh removes expired buffs and closes their open popup.
            Transform guard = Row(true).GetComponentsInChildren<Transform>(false).Single(item => item.name == "Status guard");
            Transform retained = Row(true).GetComponentsInChildren<Transform>(false).First(item => item.name.StartsWith("Status ") && item.name != "Status guard");
            string retainedName = retained.name;
            int retainedId = retained.GetInstanceID();
            ExecuteEvents.Execute(guard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            session.Battle.PlayerBlock = 0;
            Row(true).SetStatuses(CoreBattleStatusPresentation.Build(session, true));
            yield return null;
            Assert.That(Named("Battle Status Tooltip", true).activeInHierarchy, Is.False);
            Assert.That(Row(true).GetComponentsInChildren<Transform>(false).Any(item => item.name == "Status guard"), Is.False);
            Assert.That(Row(true).GetComponentsInChildren<Transform>(false).Single(item => item.name == retainedName).GetInstanceID(), Is.EqualTo(retainedId),
                "Unaffected status slots remain retained when an expired effect is removed.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ClickingShip_HidesBothStatusRowsAndOpenTooltip_AndRestoresAfterLeavingMovement()
        {
            CoreVoyageSession session = FreshBattle();
            Render();
            yield return null;
            var movement = Named("Naval Tactical Board").GetComponent<NavalTacticalMovementInput>();
            var status = CoreBattleStatusPresentation.Build(session, true).First();
            Transform badge = Row(true).GetComponentsInChildren<Transform>(true).Single(item => item.name == "Status " + status.Id);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(badge.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(Named("Battle Status Tooltip", true).activeInHierarchy, Is.True);
            Assert.That(ExecuteEvents.Execute(Named("Player Ship Art"), pointer, ExecuteEvents.pointerClickHandler), Is.True);
            yield return null;
            Assert.That(movement.IsShipSelected, Is.True);
            AssertSuppressed(true);
            Assert.That(Named("Battle Status Tooltip", true).activeInHierarchy, Is.False,
                "A hover popup must close when its row disappears for movement.");
            NavalBattleVisualCapture.Capture(controller, "BattleHud_Movement.png", "点击船只规划移动时双方状态图标隐去");

            ExecuteEvents.Execute(Named("Player Ship Art"), pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(movement.IsShipSelected, Is.False);
            AssertSuppressed(false);
            AssertRows(session);

            // Arrival heading is still part of movement even after selection resets.
            SetBattleMember(session.Battle, "NavalAwaitingHeadingSelection", true);
            movement.RefreshNow();
            yield return null;
            AssertSuppressed(true);
            SetBattleMember(session.Battle, "NavalAwaitingHeadingSelection", false);
            movement.RefreshNow();
            yield return null;
            AssertSuppressed(false);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoardingStatusRows_UseCrewHealthAndRemainVisibleAfterTheRealBoardingTransition()
        {
            CoreVoyageSession session = FreshBattle(100f);
            CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, false);
            Render();
            yield return null;
            Button board = Named("Battle Board").GetComponent<Button>();
            Assert.That(board.interactable, Is.True);
            board.onClick.Invoke();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            yield return null;
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            // Buffs are applied to the disposable fixture after the transition so
            // the rows exercise fresh boarding values rather than stale naval data.
            session.Battle.PlayerBlock = 7;
            session.Battle.EnemyBlock = 9;
            Render();
            yield return null;
            AssertRows(session);
            AssertSuppressed(false);
            BattleDefenseHud playerHud = Named("Player Battle Hull Bar Track").GetComponent<BattleDefenseHud>();
            BattleDefenseHud enemyHud = Named("Enemy Battle Hull Bar Track").GetComponent<BattleDefenseHud>();
            Assert.That(playerHud.MaximumHull, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(enemyHud.MaximumHull, Is.EqualTo(session.Battle.EnemyMaxBoardingHealth));
            NavalBattleVisualCapture.Capture(controller, "BattleHud_Boarding.png", "接舷战生命与双方状态图标");
            LogAssert.NoUnexpectedReceived();
        }

        private CoreVoyageSession FreshBattle(float distance = 500f)
        {
            var method = typeof(NavalBattleVisualFeedbackTests).GetMethod("UseFreshNavalBattle", BindingFlags.Static | BindingFlags.NonPublic);
            var session = (CoreVoyageSession)method.Invoke(null, new object[] { controller, distance, 0, false });
            session.Battle.PlayerBlock = 7;
            session.Battle.EnemyBlock = 9;
            session.Battle.PlayerFireRounds = 2;
            return session;
        }

        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
        private BattleStatusRow Row(bool player) => Named((player ? "Player" : "Enemy") + " Battle Status Row", true).GetComponent<BattleStatusRow>();
        private GameObject Named(string name, bool inactive = false)
        {
            Transform item = controller.GetComponentsInChildren<Transform>(inactive).SingleOrDefault(child => child.name == name);
            Assert.That(item, Is.Not.Null, "Missing battle UI: " + name);
            return item.gameObject;
        }

        private void AssertRows(CoreVoyageSession session)
        {
            foreach (bool player in new[] { true, false })
            {
                var statuses = CoreBattleStatusPresentation.Build(session, player);
                Assert.That(statuses.Count, Is.GreaterThan(0));
                BattleStatusRow row = Row(player);
                Assert.That(row.VisibleStatusCount, Is.EqualTo(statuses.Count));
                foreach (var status in statuses)
                {
                    Assert.That(status.Tooltip, Is.Not.Empty);
                    Assert.That(row.GetComponentsInChildren<Transform>(false).Any(item => item.name == "Status " + status.Id), Is.True, status.Id);
                }
            }
        }

        private void AssertSuppressed(bool expected)
        {
            foreach (bool player in new[] { true, false })
            {
                BattleStatusRow row = Row(player);
                Assert.That(row.IsSuppressed, Is.EqualTo(expected), row.name);
                CanvasGroup group = row.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null, row.name);
                Assert.That(group.alpha, Is.EqualTo(expected ? 0f : 1f).Within(.001f), row.name);
                Assert.That(group.blocksRaycasts, Is.EqualTo(!expected), row.name);
                Assert.That(group.interactable, Is.EqualTo(!expected), row.name);
            }
        }

        private static void SetBattleMember(CoreBattleState battle, string name, object value)
        {
            var field = typeof(CoreBattleState).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) field.SetValue(battle, value);
            else typeof(CoreBattleState).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true).Invoke(battle, new[] { value });
        }
    }
}
