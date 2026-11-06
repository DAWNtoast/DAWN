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
    public sealed class BattleCompactHudTests
    {
        private readonly NavalBattleVisualFeedbackTests fixture = new NavalBattleVisualFeedbackTests();
        private CoreGameplayDemoController controller;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return fixture.SetUp();
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
        }
        [UnityTearDown] public IEnumerator TearDown() { yield return fixture.TearDown(); }

        [UnityTest]
        public IEnumerator CornerPiles_ShowLiveCountsAndOpenTheRequestedReadOnlyGroup()
        {
            CoreVoyageSession session = FreshBattle();
            CoreBattleState battle = session.Battle;
            int card = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).First(index => CoreVoyageSession.CardLibrary[index].Id == "card_brace");
            battle.DrawPile.Clear();
            for (int index = 0; index < 11; index++) battle.DrawPile.Add(card);
            battle.DiscardPile.Clear();
            for (int index = 0; index < 3; index++) battle.DiscardPile.Add(card);
            Render();
            yield return null;
            AssertAmount("Battle Draw Pile", 11);
            AssertAmount("Battle Discard Pile", 3);
            var pilePointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            BattleStatusTooltip pileTooltip = Named("Battle Card").GetComponentInChildren<BattleStatusTooltip>(true);
            Assert.That(pileTooltip, Is.Not.Null);
            Assert.That(pileTooltip.gameObject.activeInHierarchy, Is.False);
            foreach (string pile in new[] { "Battle Draw Pile", "Battle Discard Pile" })
            {
                GameObject pileButton = Named(pile);
                Assert.That(pileButton.GetComponentsInChildren<BattleStatusHover>(true), Is.Empty, pile);
                Assert.That(pileButton.GetComponent<Button>().IsInteractable(), Is.True, pile);
                // Selectable handles pointer enter for button highlighting without opening a tooltip.
                Assert.That(ExecuteEvents.Execute(pileButton, pilePointer, ExecuteEvents.pointerEnterHandler), Is.True);
                yield return null;
                Assert.That(pileTooltip.gameObject.activeInHierarchy, Is.False, pile + " must not open a tooltip.");
                Assert.That(ExecuteEvents.Execute(pileButton, pilePointer, ExecuteEvents.pointerExitHandler), Is.True);
            }
            int[] draw = battle.DrawPile.ToArray(), discard = battle.DiscardPile.ToArray(), hand = battle.Hand.ToArray();
            int turn = battle.Turn;
            int retainedBattle = Named("Battle Card").GetInstanceID();

            Assert.That(ExecuteEvents.Execute(Named("Battle Draw Pile"), pilePointer, ExecuteEvents.pointerClickHandler), Is.True);
            yield return null;
            ScrollRect scroll = Named("Battle Deck Viewport").GetComponent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.001f));
            Assert.That(Named("Battle Deck Undrawn Heading").GetComponent<Text>().text, Does.Contain("11"));
            Assert.That(Named("End Battle Turn").GetComponent<Button>().IsInteractable(), Is.False);
            Named("Close Battle Deck").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.That(ExecuteEvents.Execute(Named("Battle Discard Pile"), pilePointer, ExecuteEvents.pointerClickHandler), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();
            scroll = Named("Battle Deck Viewport").GetComponent<ScrollRect>();
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(.99f), "Discard entry scrolls directly to its group.");
            Assert.That(WorldRect(Named("Battle Deck Discard Heading")).Overlaps(WorldRect(scroll.viewport.gameObject)), Is.True,
                "The requested discard heading is actually visible inside the viewport.");
            Named("Close Battle Deck").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(Named("Battle Card").GetInstanceID(), Is.EqualTo(retainedBattle));
            Assert.That(battle.Turn, Is.EqualTo(turn));
            CollectionAssert.AreEqual(draw, battle.DrawPile);
            CollectionAssert.AreEqual(discard, battle.DiscardPile);
            CollectionAssert.AreEqual(hand, battle.Hand);
            battle.DiscardPile.Add(battle.DrawPile[0]);
            battle.DrawPile.RemoveAt(0);
            Assert.That((bool)typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView", Private).Invoke(controller, null), Is.True);
            yield return null;
            AssertAmount("Battle Draw Pile", 10);
            AssertAmount("Battle Discard Pile", 4);
            Assert.That(Named("Battle Card").GetInstanceID(), Is.EqualTo(retainedBattle));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Resources_RefreshInTwoRowsAndSchoolStatusesStayOnTheirHolder()
        {
            CoreVoyageSession session = FreshBattle();
            Render();
            yield return null;
            int resourceView = Named("Battle Compact Resources").GetInstanceID();
            var resourcePointer = new PointerEventData(EventSystem.current);
            BattleStatusTooltip resourceTooltip = Named("Battle Card").GetComponentInChildren<BattleStatusTooltip>(true);
            Assert.That(resourceTooltip, Is.Not.Null);
            GameObject energyBadge = Named("Resource Energy");
            BattleStatusHover previousHover = energyBadge.AddComponent<BattleStatusHover>();
            previousHover.Configure(null, resourceTooltip, "能量", "旧版资源提示");
            Assert.That(ExecuteEvents.Execute(energyBadge, resourcePointer, ExecuteEvents.pointerEnterHandler), Is.True);
            Assert.That(resourceTooltip.gameObject.activeInHierarchy, Is.True);
            session.Battle.Energy = 2;
            Set(session.Battle, "NavalActionPoints", 3);
            Set(session.Battle.Schools, "Mana", 2);
            Set(session.Battle.Schools, "ThunderChain", 4);
            Set(session.Battle.Schools, "TidePower", 5);
            Set(session.Battle.Schools, "CurseMarks", 3);
            session.Cargo.Powder = 7; session.Cargo.Cannonballs = 8; session.Cargo.Wood = 9; session.Cargo.Medicine = 10;
            Assert.That((bool)typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView", Private).Invoke(controller, null), Is.True);
            Assert.That(resourceTooltip.gameObject.activeInHierarchy, Is.False,
                "Refreshing a retained resource immediately closes its old popup.");
            yield return null;
            Assert.That(Named("Battle Compact Resources").GetInstanceID(), Is.EqualTo(resourceView));
            foreach (var expected in new[] {
                new { Name="Energy", Value=2 }, new { Name="ActionPoint", Value=3 }, new { Name="Mana", Value=2 },
                new { Name="Powder", Value=7 }, new { Name="Cannonball", Value=8 }, new { Name="Planks", Value=9 }, new { Name="Medicine", Value=10 } })
                AssertAmount("Resource " + expected.Name, expected.Value);
            foreach (string resource in new[] { "Energy", "ActionPoint", "Mana", "Powder", "Cannonball", "Planks", "Medicine" })
            {
                GameObject badge = Named("Resource " + resource);
                Assert.That(badge.GetComponent<BattleStatusHover>(), Is.Null, resource);
                Assert.That(ExecuteEvents.Execute(badge, resourcePointer, ExecuteEvents.pointerEnterHandler), Is.False, resource);
                Assert.That(resourceTooltip.gameObject.activeInHierarchy, Is.False, resource + " must not open a tooltip.");
            }
            foreach (var expected in new[] {
                new { Id="thunder_chain", Resource="ThunderChain", Value=4, Side="Player", Rule="使用雷霆攻击牌后增加 1" },
                new { Id="tide_power", Resource="TidePower", Value=5, Side="Player", Rule="蓄潮格挡实际吸收的伤害转为等量潮势" },
                new { Id="curse_marks", Resource="CurseMark", Value=3, Side="Enemy", Rule="死神，来找你了！" } })
            {
                Assert.That(controller.GetComponentsInChildren<Transform>(false).Any(item => item.name == "Resource " + expected.Resource), Is.False);
                GameObject badge = Named(expected.Side + " Battle Status Row").transform.Find("Status " + expected.Id).gameObject;
                Assert.That(badge.transform.Find("Amount").GetComponent<Text>().text, Is.EqualTo(expected.Value.ToString()));
                Assert.That(ExecuteEvents.Execute(badge, resourcePointer, ExecuteEvents.pointerEnterHandler), Is.True);
                Assert.That(resourceTooltip.gameObject.activeInHierarchy, Is.True);
                Assert.That(resourceTooltip.GetComponentInChildren<Text>().text,
                    Does.Contain(expected.Rule));
                Assert.That(ExecuteEvents.Execute(badge, resourcePointer, ExecuteEvents.pointerExitHandler), Is.True);
                Assert.That(resourceTooltip.gameObject.activeInHierarchy, Is.False);
            }
            Rect line1 = WorldRect(Named("Battle Resource Line 1"));
            Rect line2 = WorldRect(Named("Battle Resource Line 2"));
            Rect draw = WorldRect(Named("Battle Draw Pile"));
            Assert.That(line1.yMin, Is.GreaterThanOrEqualTo(line2.yMax));
            Assert.That(line2.yMin, Is.GreaterThanOrEqualTo(draw.yMax));
            Assert.That(controller.GetComponentsInChildren<Transform>(false).Any(item => item.name == "Battle Resource Line 3"), Is.False);
            foreach (string name in new[] { "Resource Energy", "Resource ActionPoint", "Resource Mana" }) Assert.That(Named(name).transform.IsChildOf(Named("Battle Resource Line 1").transform), Is.True);
            foreach (string name in new[] { "Resource Powder", "Resource Cannonball", "Resource Planks", "Resource Medicine" }) Assert.That(Named(name).transform.IsChildOf(Named("Battle Resource Line 2").transform), Is.True);

            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(Named("Player Ship Art"), pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Rect endTurn = WorldRect(Named("End Battle Turn"));
            Rect commit = WorldRect(Named("Commit Naval Movement"));
            Rect clear = WorldRect(Named("Clear Naval Movement"));
            Rect ram = WorldRect(Named("Battle Ram"));
            Rect board = WorldRect(Named("Battle Board"));
            Rect discard = WorldRect(Named("Battle Discard Pile"));
            Assert.That(endTurn.yMin, Is.GreaterThanOrEqualTo(discard.yMax));
            Assert.That(commit.yMin, Is.GreaterThanOrEqualTo(endTurn.yMax));
            Assert.That(clear.center.y, Is.EqualTo(commit.center.y).Within(.5f));
            Assert.That(ram.yMin, Is.GreaterThanOrEqualTo(commit.yMax));
            Assert.That(board.center.y, Is.EqualTo(ram.center.y).Within(.5f));
            Assert.That(commit.Overlaps(clear), Is.False);
            Assert.That(ram.Overlaps(board), Is.False);
            Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Undo Naval Movement"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EnemyIntentIcons_PreserveEveryLiveActionAndItsNumericValueInBothStages()
        {
            CoreVoyageSession session = FreshBattle(100f);
            for (int stage = 0; stage < 2; stage++)
            {
                if (stage == 1)
                {
                    CoreGameplayDemoSceneTests.ConfigureCloseNavalTestPose(session.Battle, false);
                    Assert.That(session.TryBoard(), Is.True);
                    Set(session.Battle.BoardingEnemy, "Deck", CoreBoardingEnemyCatalog.ForSchool("thunder"));
                }
                session.Battle.PlayerBlock = 10;
                Render();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Rect statusIcon = WorldRect(Named("Player Battle Status Row").transform.Find("Status guard/Icon").gameObject);
                Assert.That(statusIcon.width, Is.GreaterThan(0f));
                BattleCommandIconGraphic[] resourceIcons = Named("Battle Compact Resources").GetComponentsInChildren<BattleCommandIconGraphic>(false);
                Assert.That(resourceIcons, Has.Length.EqualTo(7));
                foreach (BattleCommandIconGraphic icon in resourceIcons)
                    Assert.That(Vector2.Distance(WorldRect(icon.gameObject).size, statusIcon.size), Is.LessThanOrEqualTo(.1f),
                        session.Battle.Stage + " " + icon.Kind + " must match the visible status icon size.");
                var intents = CoreBattleIntentPresentation.Build(session);
                Assert.That(intents.Count, Is.GreaterThan(0));
                Transform row = Named("Enemy Intent Icons").transform;
                Transform[] badges = row.Cast<Transform>().Where(item => item.name.StartsWith("Intent ")).ToArray();
                Assert.That(badges.Length, Is.EqualTo(intents.Count));
                foreach (var intent in intents)
                {
                    Transform badge = badges.Single(item => item.name == "Intent " + intent.Id);
                    Assert.That(badge.Find("Amount").GetComponent<Text>().text, Is.EqualTo(intent.Value));
                    Assert.That(badge.Find("Icon").GetComponent<Graphic>(), Is.Not.Null);
                    Vector2 expectedIntentSize = stage == 0
                        ? statusIcon.size * (64f / BattleStatusRow.IconSizeForCell(
                            Named("Naval Tactical Board").GetComponent<NavalTacticalBoardView>().CellSizePixels))
                        : statusIcon.size;
                    Assert.That(Vector2.Distance(WorldRect(badge.Find("Icon").gameObject).size, expectedIntentSize), Is.LessThanOrEqualTo(.1f),
                        session.Battle.Stage + " " + intent.Id + " preserves the stage's readable intent size.");
                }
                Transform inspected = badges.Single(item => item.name == "Intent " + intents[0].Id);
                var pointer = new PointerEventData(EventSystem.current);
                Assert.That(ExecuteEvents.Execute(inspected.gameObject, pointer, ExecuteEvents.pointerEnterHandler), Is.True);
                yield return null;
                string explanation = Regex.Replace(string.Join("\n", Named("Battle Status Tooltip").GetComponentsInChildren<Text>(false).Select(label => label.text)), "<[^>]+>", string.Empty);
                Assert.That(explanation, Does.Contain(intents[0].Tooltip), "Intent hover preserves the model's explanation without needing a buff-row owner.");
                ExecuteEvents.Execute(inspected.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                yield return null;
                if (stage == 1) Assert.That(intents.Count, Is.GreaterThan(1), "The authored thunder turn must exercise compound intent display.");
                yield return new WaitForSecondsRealtime(.6f);
                NavalBattleVisualCapture.Capture(controller, stage == 0 ? "IconSize_Naval.png" : "IconSize_Boarding.png",
                    "资源尺寸保持一致；海战意图放大，接舷意图保持原尺寸");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private CoreVoyageSession FreshBattle(float distance = 400f) => (CoreVoyageSession)typeof(NavalBattleVisualFeedbackTests)
            .GetMethod("UseFreshNavalBattle", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { controller, distance, 0, false });
        private void Render() => typeof(CoreGameplayDemoController).GetMethod("Render", Private).Invoke(controller, null);
        private GameObject Named(string name)
        {
            Transform item = controller.GetComponentsInChildren<Transform>(false).SingleOrDefault(child => child.name == name);
            Assert.That(item, Is.Not.Null, "Missing active UI: " + name);
            return item.gameObject;
        }
        private void AssertAmount(string name, int expected)
        {
            string text = Named(name).transform.Find("Amount").GetComponent<Text>().text;
            Match number = Regex.Match(text, @"\d+");
            Assert.That(number.Success, Is.True, name + " displays a number.");
            Assert.That(int.Parse(number.Value), Is.EqualTo(expected), name);
        }
        private static Rect WorldRect(GameObject gameObject)
        {
            var corners = new Vector3[4]; gameObject.GetComponent<RectTransform>().GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
        }
        private static void Set(object target, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null) field.SetValue(target, value);
            else target.GetType().GetProperty(name, flags).GetSetMethod(true).Invoke(target, new[] { value });
        }
    }
}
