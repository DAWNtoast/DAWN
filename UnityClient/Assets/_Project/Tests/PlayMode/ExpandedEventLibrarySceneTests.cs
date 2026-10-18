using System;
using System.Collections;
using System.Linq;
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
        private static readonly string[] ExpandedLibrarySources = {
            "event_reef_v2_tidewright_register", "event_trade_v2_notary_barge", "event_moon_v2_calendar_mosaic"
        };
        private static readonly string[] ExpandedLibraryRandoms = {
            "event_reef_v2_salvage_argument", "event_trade_v2_sample_sails", "event_moon_v2_star_in_jar"
        };

        [UnityTest] public IEnumerator ExpandedEventLibrary_Reef_RealButtonsAndDiskResume() => RunExpandedEventLibrary(0);
        [UnityTest] public IEnumerator ExpandedEventLibrary_Trade_RealButtonsAndDiskResume() => RunExpandedEventLibrary(1);
        [UnityTest] public IEnumerator ExpandedEventLibrary_Moon_RealButtonsAndDiskResume() => RunExpandedEventLibrary(2);

        private IEnumerator RunExpandedEventLibrary(int route)
        {
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadExpandedEventLibraryFixture(route);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                string prefix = "expanded_" + ExplorationNames[route];
                CoreMapNode source = ExplorationNode(session, ExpandedLibrarySources[route]);
                ActiveButton("Event Node " + source.Id).onClick.Invoke();
                yield return null;
                int requested = Math.Max(CoreVoyageSession.MinimumDistanceNm, Math.Min(120, session.MaximumDistanceNm - 7));
                GameObject.Find("Distance Slider").GetComponent<Slider>().value = requested;
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, source);
                ScheduleInterruptedDistanceEvent(session, ExpandedLibraryRandoms[route], 1f);
                float beforeSailing = session.TotalDistanceNm;
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitExplorationMotion(controller);
                Assert.That(session.TotalDistanceNm, Is.GreaterThan(beforeSailing));
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo(ExpandedLibraryRandoms[route]));
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, source);
                int randomChoice = route == 0 ? 1 : 0;
                yield return WaitForEncounterDossierPresentationCompletion();
                AssertExpandedDossier(session, randomChoice);
                NavalBattleVisualCapture.Capture(controller, prefix + "_random_active.png",
                    "新增航行随机事件由真实航行触发；选择、效果、原航程与目标均在现役界面中显示", "Encounter Card");
                yield return SaveAndResumeExploration(controller, storage, prefix + "_random_active");

                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                source = ExplorationNode(session, ExpandedLibrarySources[route]);
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo(ExpandedLibraryRandoms[route]));
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, source);
                yield return WaitForEncounterDossierPresentationCompletion();
                AssertExpandedDossier(session, randomChoice);
                CoreVoyageCheckpoint before = session.CaptureVoyageCheckpoint();
                double randomHours = route == 1 ? 1d : 2d;
                int rations = ExpandedChoiceRations(session, randomHours);
                ActiveButton("Encounter Choice " + randomChoice).onClick.Invoke();
                yield return null;
                AssertExpandedTimeAndRations(session, before, randomHours, rations);
                if (route == 0)
                {
                    Assert.That(session.Cargo.Iron, Is.EqualTo(before.cargo.iron + 4));
                    Assert.That(session.Cargo.Fame, Is.EqualTo(before.cargo.fame + 5));
                }
                else if (route == 1)
                {
                    Assert.That(session.Cargo.Cloth, Is.EqualTo(before.cargo.cloth - 3));
                    Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(18d));
                    Assert.That(ExplorationStatusText(), Does.Contain("航速 +25%"));
                }
                else
                {
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.cargo.medicine - 1));
                    Assert.That(session.Cargo.Cloth, Is.EqualTo(before.cargo.cloth - 1));
                    Assert.That(session.Cargo.Lantern, Is.EqualTo(before.cargo.lantern + 1));
                }
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.False,
                    "The selected random reward must leave the new fixed clue source meaningful.");
                AssertExpandedRandomHistory(session, route);
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, source);
                yield return SaveAndResumeExploration(controller, storage, prefix + "_random_resolved");

                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                AssertExpandedRandomHistory(session, route);
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, ExplorationNode(session, ExpandedLibrarySources[route]));
                // Keep subsequent travel focused on the authored fixed chain. This
                // changes only the next random timer, never geography or nodes.
                SetPersistentCourseField(session, "nextChartRandomDistance", 100000f);
                yield return SailExplorationTo(controller, ExpandedLibrarySources[route]);
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo(ExpandedLibrarySources[route]));
                AssertInterruptedDistanceSelection(session, requested);
                AssertExpandedDossier(session, 0);
                NavalBattleVisualCapture.Capture(controller, prefix + "_fixed_active.png",
                    "真实 SS5 海图抽中的新增固定地点，线索选择及材料成本可读且可以执行", "Encounter Card");
                yield return SaveAndResumeExploration(controller, storage, prefix + "_fixed_active");

                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(session.ActiveEncounter.Id, Is.EqualTo(ExpandedLibrarySources[route]));
                Assert.That(session.Map.GeometryVersion, Is.EqualTo(3));
                AssertInterruptedDistanceSelection(session, requested);
                yield return WaitForEncounterDossierPresentationCompletion();
                AssertExpandedDossier(session, 0);
                before = session.CaptureVoyageCheckpoint();
                double sourceHours = route == 1 ? 2d : 3d;
                rations = ExpandedChoiceRations(session, sourceHours);
                ActiveButton("Encounter Choice 0").onClick.Invoke();
                yield return null;
                AssertExpandedTimeAndRations(session, before, sourceHours, rations);
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.True);
                CoreMapNode target = ExplorationNode(session, ExplorationTargets[route]);
                Assert.That(target.IsChartVisible, Is.True, "The new clue must identify the production chain destination.");
                Assert.That(target.Visited, Is.False);
                if (route == 0) Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(6d));
                if (route == 1) Assert.That(session.Cargo.Gold, Is.EqualTo(before.cargo.gold - 10));
                if (route == 2)
                {
                    Assert.That(session.Cargo.Wood, Is.EqualTo(before.cargo.wood - 2));
                    Assert.That(session.Cargo.Cloth, Is.EqualTo(before.cargo.cloth - 1));
                }
                AssertExpandedFixedClaimedOnce(session, ExpandedLibrarySources[route]);
                AssertExplorationStatus(ExplorationClues[route], ExplorationTargets[route]);
                AssertInterruptedDistanceSelection(session, requested);
                yield return SaveAndResumeExploration(controller, storage, prefix + "_fixed_resolved");

                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                AssertExpandedFixedClaimedOnce(session, ExpandedLibrarySources[route]);
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.True);
                AssertInterruptedDistanceSelection(session, requested);
                string noReplay = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
                ActiveButton("Event Node " + ExplorationNode(session, ExpandedLibrarySources[route]).Id).onClick.Invoke();
                yield return null;
                Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(noReplay),
                    "Clicking an already claimed location after disk restore must not award its effect again.");
                target = ExplorationNode(session, ExplorationTargets[route]);
                ActiveButton("Event Node " + target.Id).onClick.Invoke();
                yield return null;
                AssertInterruptedDistanceSelection(session, requested);
                AssertPersistentCourseTarget(session, target);
                float beforeChainTravel = session.TotalDistanceNm;
                yield return SailExplorationTo(controller, ExplorationTargets[route]);
                Assert.That(session.TotalDistanceNm, Is.GreaterThan(beforeChainTravel),
                    "The new clue source must lead to actual travel to the existing authored reward.");
                AssertInterruptedDistanceSelection(session, requested);
                int targetChoice = ExplorationTargetChoice(session, route);
                AssertExpandedDossier(session, targetChoice);
                NavalBattleVisualCapture.Capture(controller, prefix + "_chain_unlocked.png",
                    "新增地点所得线索在原探索链终点解锁真实奖励选择，改选目标保留原航程", "Encounter Card");
                before = session.CaptureVoyageCheckpoint();
                ActiveButton("Encounter Choice " + targetChoice).onClick.Invoke();
                yield return null;
                if (route == 0)
                {
                    Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(before.cargo.ancientMaterial + 2));
                    Assert.That(session.Cargo.Wood, Is.EqualTo(before.cargo.wood - 2));
                    Assert.That(session.Hull, Is.EqualTo(Math.Min(session.MaxHull, before.hull + 8)));
                    Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(16d));
                }
                else if (route == 1)
                {
                    Assert.That(session.Cargo.Gem, Is.EqualTo(before.cargo.gem + 1));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(before.cargo.medicine + 2));
                    Assert.That(session.Cargo.Fame, Is.EqualTo(before.cargo.fame + 8));
                }
                else
                {
                    Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(before.cargo.ancientMaterial - 1));
                    Assert.That(session.Cargo.Relic, Is.EqualTo(before.cargo.relic + 1));
                    Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(18d));
                }
                AssertExpandedFixedClaimedOnce(session, ExplorationTargets[route]);
                AssertInterruptedDistanceSelection(session, requested);
                yield return SaveAndResumeExploration(controller, storage, prefix + "_chain_resolved");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                AssertExpandedFixedClaimedOnce(session, ExpandedLibrarySources[route]);
                AssertExpandedFixedClaimedOnce(session, ExplorationTargets[route]);
                AssertExpandedRandomHistory(session, route);
                AssertInterruptedDistanceSelection(session, requested);
                Assert.That(ExplorationStatusText(), Does.Contain("（已探索）"));
                NavalBattleVisualCapture.Capture(controller, prefix + "_restored_complete.png",
                    "新增随机奖励、固定线索与探索链奖励均经真实磁盘恢复，已领取地点禁用，航程设定保留", "Navigation Chart");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadExpandedEventLibraryFixture(int route)
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            Assert.That(session.RestoreSeaChartProgress(new CoreSeaChartProgress()), Is.True);
            session.ConfigureCrewCapabilities(true, true, false, false, true, false);
            bool found = false;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                string seed = CoreVoyageSession.CreateChartSeed(route, session.CurrentPort, session.SelectedDestinationPort);
                Assert.That(seed, Does.StartWith("SS5."));
                Assert.That(session.TrySelectChartSeed(seed, out string reason), Is.True, reason);
                CoreVoyageMap preview = session.PreviewSelectedSeaChart();
                CoreMapNode candidate = preview.Nodes.SingleOrDefault(node => node.Encounter.Id == ExpandedLibrarySources[route]);
                if (candidate == null) continue;
                CoreMapNode destination = preview.Nodes.Single(node => node.Encounter.Id == ExplorationTargets[route]);
                if (PersistentCourseDistance(candidate.Position, destination.Position) * preview.Route.NauticalMilesPerMapUnit < 360f) continue;
                found = true;
                break;
            }
            Assert.That(found, Is.True, "Find a real current chart containing the new fixed source and a separately reachable chain destination.");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.Cargo.Wood = 8; session.Cargo.Cloth = 8; session.Cargo.Iron = 4;
            session.Cargo.Medicine = 2; session.Cargo.Beer = 2; session.Cargo.Gold = 80;
            session.Cargo.AncientMaterial = route == 2 ? 1 : 0;
            Assert.That(session.Cargo.UsedVolume, Is.LessThan(session.CargoCapacityValue - 15f));
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Map.GeometryVersion, Is.EqualTo(3));
            Assert.That(session.CurrentVoyageRandomEventIds.Count, Is.EqualTo(24));
            Assert.That(session.CurrentVoyageRandomEventIds.Distinct().Count(), Is.EqualTo(24));
            Assert.That(session.CaptureVoyageCheckpoint().chartRandomVersion, Is.EqualTo(2));
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 16);
            // Arrange only the starting vessel position, initial knowledge and
            // supplies. Keep every generated node and every geography cell intact.
            CoreMapNode source = ExplorationNode(session, ExpandedLibrarySources[route]);
            var path = session.PlanNavigablePath(source.Position, session.Map.Departure);
            Assert.That(path.Count, Is.GreaterThan(1));
            VoyagePoint next = path[1];
            float fraction = Math.Min(.7f, 3f / PersistentCourseDistance(source.Position, next));
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(
                Mathf.Lerp(source.Position.X, next.X, fraction), Mathf.Lerp(source.Position.Y, next.Y, fraction)));
            SetPersistentCourseField(session, "randomAmbushOccurred", true);
            CoreMapNode target = ExplorationNode(session, ExplorationTargets[route]);
            target.Discovered = target.IntelRevealed = false;
            source.Discovered = true;
            RenderExploration(controller);
            yield return new WaitForSecondsRealtime(.5f);
        }

        private static void AssertExpandedDossier(CoreVoyageSession session, int choice)
        {
            Assert.That(GameObject.Find("Encounter Title").GetComponent<Text>().text, Is.EqualTo(session.ActiveEncounter.Name));
            Assert.That(GameObject.Find("Encounter Description").GetComponent<Text>().text, Is.EqualTo(session.ActiveEncounter.Description));
            Button button = ActiveButton("Encounter Choice " + choice);
            Assert.That(button.interactable, Is.True, session.ChoiceUnavailableReason(choice));
            Assert.That(button.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains(session.ActiveEncounter.Choices[choice].Label)), Is.True);
        }

        private static int ExpandedChoiceRations(CoreVoyageSession session, double hours) =>
            (int)typeof(CoreVoyageSession).GetMethod("RationCostFor", ExplorationHidden).Invoke(session, new object[] { hours });

        private static void AssertExpandedTimeAndRations(CoreVoyageSession session, CoreVoyageCheckpoint before, double hours, int rations)
        {
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalHours, Is.EqualTo(before.totalHours + hours).Within(.0001));
            Assert.That(session.Cargo.Water, Is.EqualTo(before.cargo.water - rations));
            Assert.That(session.Cargo.Food, Is.EqualTo(before.cargo.food - rations));
        }

        private static void AssertExpandedRandomHistory(CoreVoyageSession session, int route)
        {
            CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
            Assert.That(checkpoint.chartRandomIndex, Is.EqualTo(1), "Restoring a resolved random event must not consume or reroll another bag entry.");
            Assert.That(checkpoint.chartRandomVersion, Is.EqualTo(2));
            Assert.That(checkpoint.chartRandomOrder.Length, Is.EqualTo(24));
            string[] history = checkpoint.seaChartProgress.randomHistory.Single(item => item.routeIndex == route).recentEvents;
            Assert.That(history, Is.EqualTo(new[] { ExpandedLibraryRandoms[route] }));
        }

        private static void AssertExpandedFixedClaimedOnce(CoreVoyageSession session, string id)
        {
            CoreMapNode node = ExplorationNode(session, id);
            Assert.That(node.Visited, Is.True, id);
            Assert.That(ActiveButton("Event Node " + node.Id).interactable, Is.False, id);
            CoreSeaChartState chart = session.CaptureSeaChartProgress().charts.Single(item => item.seed == session.CurrentChartSeed);
            Assert.That(chart.events.Single(item => item.eventId == id).count, Is.EqualTo(1), id);
        }
    }
}
