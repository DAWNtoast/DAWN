using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator InterruptedDistance_FixedEventSkipAndNearbyReopenPreserveSelection()
        {
            using (var storage = new ExplorationStorageScope())
            {
                // This is the authored, revisitable cache on an unmodified map.
                yield return LoadExplorationFixture(0, true);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                CoreMapNode cache = ExplorationNode(session, "event_reef_hidden_cache");
                ActiveButton("Event Node " + cache.Id).onClick.Invoke();
                yield return null;
                int requested = Math.Min(120, session.MaximumDistanceNm);
                GameObject.Find("Distance Slider").GetComponent<Slider>().value = requested;
                AssertInterruptedDistanceSelection(session, requested);
                float before = session.TotalDistanceNm;
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitForNavigationMotionCompletion(controller);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo(cache.Encounter.Id));
                Assert.That(session.TotalDistanceNm - before, Is.InRange(.01f, requested - .01f),
                    "The fixed event must interrupt a longer configured leg.");
                AssertInterruptedDistanceSelection(session, requested);
                yield return WaitForEncounterDossierPresentationCompletion();
                ActiveButton("Skip Encounter").onClick.Invoke();
                yield return null;
                Assert.That(cache.Visited, Is.False);
                Assert.That(cache.Skipped, Is.True);
                AssertInterruptedDistanceSelection(session, requested);
                ActiveButton("Event Node " + cache.Id).onClick.Invoke();
                yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                AssertInterruptedDistanceSelection(session, requested);
                yield return WaitForEncounterDossierPresentationCompletion();
                ActiveButton("Encounter Choice 0").onClick.Invoke();
                yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(cache.Visited, Is.True);
                AssertInterruptedDistanceSelection(session, requested);
                yield return SaveAndResumeExploration(controller, storage, "distance_fixed_cache_resolved");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                AssertInterruptedDistanceSelection(controller.Session, requested);
                NavalBattleVisualCapture.Capture(controller, "Distance_Fixed_Event_Resolved_And_Restored.png",
                    "固定事件中断、暂缓、近处重开、处理及真实存档恢复后仍保留玩家航程设定", "Navigation Controls");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InterruptedDistance_BoostExpiryAndEventDamageKeepSliderAboveReducedLimit()
        {
            yield return LoadPersistentCourseFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            Array.Clear(session.Map.CurrentVectors, 0, session.Map.CellCount);
            Array.Clear(session.Map.CurrentStrengths, 0, session.Map.CellCount);
            session.Map.GlobalWindStrength = 0f;
            SetPersistentCourseField(session, "sailingBoostHoursRemaining", .5d);
            CoreMapNode target = session.Map.Nodes[0];
            RenderDestinationBossFixture(controller);
            yield return null;
            ActiveButton("Event Node " + target.Id).onClick.Invoke();
            yield return null;
            int requested = session.MaximumDistanceNm;
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = requested;
            AssertInterruptedDistanceSelection(session, requested);
            ScheduleInterruptedDistanceEvent(session, "event_reef_rigging_snap", 15f);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo("event_reef_rigging_snap"));
            Assert.That(session.SailingBoostHoursRemaining, Is.Zero);
            Assert.That(session.MaximumDistanceNm, Is.LessThan(requested), "The interrupted voyage consumes its remaining speed boost.");
            AssertInterruptedDistanceSelection(session, requested);
            int hull = session.Hull, maximumBeforeDamage = session.MaximumDistanceNm;
            yield return WaitForEncounterDossierPresentationCompletion();
            ActiveButton("Encounter Choice 1").onClick.Invoke();
            yield return null;
            Assert.That(session.Hull, Is.EqualTo(hull - 3));
            Assert.That(session.MaximumDistanceNm, Is.LessThan(maximumBeforeDamage));
            Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d + .0001d));
            Assert.That(session.PlannedDistanceNm, Is.EqualTo(session.MaximumDistanceNm));
            AssertInterruptedDistanceSelection(session, requested);
            Assert.That(GameObject.Find("Distance Limit").GetComponent<Text>().text, Does.Contain("本段上限"));
            AssertPersistentCourseTarget(session, target);
            ActiveButton("Event Node " + target.Id).onClick.Invoke();
            yield return null;
            AssertInterruptedDistanceSelection(session, requested);
            AssertPersistentCourseTarget(session, target);
            // Turning toward the other real event also changes the wind
            // projection. The smaller safe leg must not become a new selection.
            session.Map.GlobalWindVector = new VoyagePoint(0f, -1f);
            session.Map.GlobalWindDirection = SailingDirection.South;
            session.Map.GlobalWindStrength = 8f;
            RenderDestinationBossFixture(controller);
            yield return null;
            int maximumBeforeSwitch = session.MaximumDistanceNm;
            target = session.Map.Nodes[1];
            ActiveButton("Event Node " + target.Id).onClick.Invoke();
            yield return null;
            Assert.That(session.MaximumDistanceNm, Is.LessThan(maximumBeforeSwitch),
                "The replacement target heads into the wind and reduces this leg's 24-hour limit.");
            Assert.That(session.GetForecast().WindRelation, Is.EqualTo("逆风"));
            Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d + .0001d));
            AssertInterruptedDistanceSelection(session, requested);
            AssertPersistentCourseTarget(session, target);
            NavalBattleVisualCapture.Capture(controller, "Distance_Reduced_Limit_Keeps_Selection.png",
                "加速到期、索具损伤及改选逆风目标降低本段上限，滑条仍保留玩家设定，实际航程不超过 24 小时", "Navigation Controls");
            ScheduleInterruptedDistanceEvent(session, "random_tailwind", 15f);
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo("random_tailwind"));
            AssertInterruptedDistanceSelection(session, requested);
            yield return WaitForEncounterDossierPresentationCompletion();
            ActiveButton("Skip Encounter").onClick.Invoke();
            yield return null;
            AssertInterruptedDistanceSelection(session, requested);
            AssertPersistentCourseTarget(session, target);
            SetPersistentCourseField(session, "nextChartRandomDistance", 100000f);
            double beforeHours = session.TotalHours;
            ActiveButton("Execute Sail").onClick.Invoke();
            yield return WaitForNavigationMotionCompletion(controller);
            Assert.That(session.TotalHours, Is.GreaterThan(beforeHours));
            Assert.That(session.TotalHours - beforeHours, Is.LessThanOrEqualTo(24d + .0001d));
            AssertInterruptedDistanceSelection(session, requested);
            NavalBattleVisualCapture.Capture(controller, "Distance_After_Skip_And_Next_Leg.png",
                "处理强制事件、跳过随机事件并再次实际航行后，玩家设定未被当前海况覆盖", "Navigation Controls");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InterruptedDistance_RandomEncounterAndDiskResumeKeepSelectionAndTarget()
        {
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
                session.Cargo.Food = session.Cargo.Water = 100;
                session.StartVoyage();
                CoreMapNode target = session.Map.Nodes.Where(node => !node.IsBoss && node.Encounter.HasChartLocation &&
                    node.Encounter.BossKind == CoreBossKind.None).OrderByDescending(node => PersistentCourseDistance(session.Position, node.Position)).First();
                target.Discovered = true;
                SetPersistentCourseField(session, "randomAmbushOccurred", true);
                RenderDestinationBossFixture(controller);
                yield return new WaitForSecondsRealtime(.4f);
                ActiveButton("Event Node " + target.Id).onClick.Invoke();
                yield return null;
                int requested = Math.Max(CoreVoyageSession.MinimumDistanceNm, session.MaximumDistanceNm - 7);
                GameObject.Find("Distance Slider").GetComponent<Slider>().value = requested;
                Assert.That(session.GetForecast().ActualDistanceNm, Is.GreaterThan(10f));
                ScheduleInterruptedDistanceEvent(session, "event_reef_rigging_snap", 1f);
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitForNavigationMotionCompletion(controller);
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo("event_reef_rigging_snap"));
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                yield return WaitForEncounterDossierPresentationCompletion();
                yield return SaveAndResumeExploration(controller, storage, "distance_random_interrupted");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                target = session.Map.Nodes.Single(node => node.Id == target.Id);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                yield return WaitForEncounterDossierPresentationCompletion();
                ActiveButton("Encounter Choice 1").onClick.Invoke();
                yield return null;
                AssertInterruptedDistanceSelection(session, requested);
                int previousTargetId = target.Id;
                target = session.Map.Nodes.Where(node => node.Id != previousTargetId && !node.IsBoss &&
                    node.Encounter.HasChartLocation && node.Encounter.BossKind == CoreBossKind.None)
                    .OrderByDescending(node => PersistentCourseDistance(session.Position, node.Position)).First();
                target.Discovered = true;
                RenderDestinationBossFixture(controller);
                yield return null;
                ActiveButton("Event Node " + target.Id).onClick.Invoke();
                yield return null;
                Assert.That(target.Id, Is.Not.EqualTo(previousTargetId));
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                Assert.That(session.GetForecast().Hours, Is.LessThanOrEqualTo(24d + .0001d));
                yield return SaveAndResumeExploration(controller, storage, "distance_random_switched_target");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                target = session.Map.Nodes.Single(node => node.Id == target.Id);
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                ActiveButton("Event Node " + target.Id).onClick.Invoke();
                yield return null;
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                NavalBattleVisualCapture.Capture(controller, "Distance_Random_Event_Disk_Resume.png",
                    "处理随机事件后改选另一真实探索目标，再经存档、主菜单和继续游戏；原设定距离与新目标完整保留", "Navigation Controls");
                ScheduleInterruptedDistanceEvent(session, "random_tailwind", 1f);
                double beforeHours = session.TotalHours;
                float beforeDistance = session.TotalDistanceNm;
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitForNavigationMotionCompletion(controller);
                Assert.That(session.TotalDistanceNm, Is.GreaterThan(beforeDistance));
                Assert.That(session.TotalHours, Is.GreaterThan(beforeHours));
                Assert.That(session.TotalHours - beforeHours, Is.LessThanOrEqualTo(24d + .0001d));
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo("random_tailwind"));
                yield return WaitForEncounterDossierPresentationCompletion();
                ActiveButton("Skip Encounter").onClick.Invoke();
                yield return null;
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertInterruptedDistanceSelection(CoreVoyageSession session, int requested)
        {
            Assert.That(session.UsesDefaultDistance, Is.False);
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(requested), "Event interruption must retain the player's configured distance.");
            Slider slider = GameObject.Find("Distance Slider").GetComponent<Slider>();
            Assert.That(slider.value, Is.EqualTo(requested));
            Assert.That(slider.maxValue, Is.GreaterThanOrEqualTo(requested));
            Text label = GameObject.Find("Distance Label").GetComponent<Text>();
            Assert.That(label.text, Does.Contain(requested + " 海里"));
            if (session.HasDirectCourseTarget)
            {
                Assert.That(label.text, Does.Contain("设定 " + requested));
                Assert.That(label.text, Does.Contain("本段"));
            }
        }

        private static void ScheduleInterruptedDistanceEvent(CoreVoyageSession session, string id, float distance)
        {
            // Reorder the real complete bag, preserving the production checkpoint
            // invariants. Only the next encounter's timing is arranged.
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            string[] order = (string[])typeof(CoreVoyageSession).GetField("chartRandomOrder", flags).GetValue(session);
            CoreEncounter[] encounters = (CoreEncounter[])typeof(CoreVoyageSession).GetField("chartRandomEncounters", flags).GetValue(session);
            int next = (int)typeof(CoreVoyageSession).GetField("chartRandomIndex", flags).GetValue(session);
            int selected = Array.IndexOf(order, id, next);
            Assert.That(selected, Is.GreaterThanOrEqualTo(next), "The fixture requires an unseen production event: " + id);
            string heldId = order[next]; order[next] = order[selected]; order[selected] = heldId;
            CoreEncounter heldEncounter = encounters[next]; encounters[next] = encounters[selected]; encounters[selected] = heldEncounter;
            SetPersistentCourseField(session, "nextChartRandomDistance", session.TotalDistanceNm + distance);
        }
    }
}
