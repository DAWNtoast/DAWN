using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private const BindingFlags ExplorationHidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] ExplorationSources = { "event_reef_broken_mast", "common_beach_castaway", "common_strait_beacon" };
        private static readonly string[] ExplorationTargets = { "event_reef_hidden_cache", "gold_wreck", "moon_obelisk" };
        private static readonly string[] ExplorationClues = { "reef_dock_chart", "trade_rescue_seal", "moon_star_chart" };
        private static readonly string[] ExplorationNames = { "reef", "trade", "moon" };

        [UnityTest] public IEnumerator VoyageExploration_ReefChain_RealButtonsAndDiskResume() => RunExplorationChain(0);
        [UnityTest] public IEnumerator VoyageExploration_TradeChain_RealButtonsAndDiskResume() => RunExplorationChain(1);
        [UnityTest] public IEnumerator VoyageExploration_MoonChain_RealButtonsAndDiskResume() => RunExplorationChain(2);

        [UnityTest]
        public IEnumerator VoyageExploration_LockedTargets_SkipAndResumeKeepUnclaimedLocations()
        {
            for (int route = 0; route < 3; route++)
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadExplorationFixture(route, true);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                yield return SailExplorationTo(controller, ExplorationTargets[route]);
                CoreVoyageSession session = controller.Session;
                int choice = ExplorationTargetChoice(session, route);
                Button locked = ActiveButton("Encounter Choice " + choice);
                Assert.That(locked.interactable, Is.False);
                Assert.That(session.ChoiceUnavailableReason(choice), Does.Contain(CoreVoyageSession.DiscoveryLabel(ExplorationClues[route])));
                string before = JsonUtility.ToJson(session.CaptureVoyageCheckpoint());
                locked.onClick.Invoke();
                Assert.That(JsonUtility.ToJson(session.CaptureVoyageCheckpoint()), Is.EqualTo(before), "A disabled callback must not spend supplies or award loot.");
                yield return WaitForEncounterDossierPresentationCompletion();
                CaptureExploration(controller, ExplorationNames[route] + "_target_locked", choice);
                ActiveButton("Skip Encounter").onClick.Invoke();
                yield return null;
                CoreMapNode target = ExplorationNode(session, ExplorationTargets[route]);
                Assert.That(target.Visited, Is.False, "Skipping a locked clue destination must preserve its reward for later.");
                Assert.That(target.Skipped, Is.True);
                yield return SaveAndResumeExploration(controller, storage, ExplorationNames[route] + "_locked_skipped");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                target = ExplorationNode(controller.Session, ExplorationTargets[route]);
                Assert.That(target.Visited, Is.False);
                Assert.That(ActiveButton("Event Node " + target.Id).interactable, Is.True);
                yield return SailExplorationTo(controller, ExplorationTargets[route]);
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(ExplorationTargets[route]));
                Assert.That(ActiveButton("Encounter Choice " + ExplorationTargetChoice(controller.Session, route)).interactable, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator RunExplorationChain(int route)
        {
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadExplorationFixture(route, false);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                CoreMapNode target = ExplorationNode(session, ExplorationTargets[route]);
                Assert.That(target.IsChartVisible, Is.False, "The controlled starting fixture has not discovered the destination.");
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.False);
                yield return SailExplorationTo(controller, ExplorationSources[route]);
                int sourceChoice = session.ActiveEncounter.Choices.Count - (route == 2 ? 2 : 1);
                Assert.That(ActiveButton("Encounter Choice " + sourceChoice).interactable, Is.True);
                CaptureExploration(controller, ExplorationNames[route] + "_source", sourceChoice);
                double sourceHours = session.TotalHours;
                int sourceWater = session.Cargo.Water, sourceFood = session.Cargo.Food;
                int sourceWood = session.Cargo.Wood, sourceCloth = session.Cargo.Cloth;
                int waitingRations = (int)typeof(CoreVoyageSession).GetMethod("RationCostFor", ExplorationHidden)
                    .Invoke(session, new object[] { session.ActiveEncounter.Choices[sourceChoice].Hours });
                ActiveButton("Encounter Choice " + sourceChoice).onClick.Invoke();
                yield return null;
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.True);
                Assert.That(session.TotalHours - sourceHours, Is.EqualTo(route == 2 ? 4d : 2d).Within(.0001));
                Assert.That(session.Cargo.Water, Is.EqualTo(sourceWater - waitingRations - (route == 1 ? 4 : 0)));
                Assert.That(session.Cargo.Food, Is.EqualTo(sourceFood - waitingRations - (route == 1 ? 4 : 0)));
                Assert.That(session.Cargo.Wood, Is.EqualTo(sourceWood - (route == 0 ? 3 : 0)));
                Assert.That(session.Cargo.Cloth, Is.EqualTo(sourceCloth - (route == 0 ? 1 : 0)));
                Assert.That(target.IntelRevealed, Is.True, "The authored choice must reveal the real target coordinate.");
                Assert.That(target.Visited, Is.False);
                Assert.That(ActiveButton("Event Node " + target.Id).interactable, Is.True);
                AssertExplorationStatus(session, ExplorationClues[route], ExplorationTargets[route]);
                yield return new WaitForSecondsRealtime(.5f);
                CaptureExploration(controller, ExplorationNames[route] + "_clue_acquired");

                // This is the production settings -> disk -> main-menu Continue -> new Awake path.
                yield return SaveAndResumeExploration(controller, storage, ExplorationNames[route] + "_clue");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(session.HasDiscovery(ExplorationClues[route]), Is.True);
                Assert.That(ExplorationNode(session, ExplorationTargets[route]).IntelRevealed, Is.True);
                AssertExplorationStatus(session, ExplorationClues[route], ExplorationTargets[route]);
                float distanceBeforeTarget = session.TotalDistanceNm;
                yield return SailExplorationTo(controller, ExplorationTargets[route]);
                Assert.That(session.TotalDistanceNm, Is.GreaterThan(distanceBeforeTarget), "Obtaining a clue must still require actual travel to its target.");
                int targetChoice = ExplorationTargetChoice(session, route);
                Assert.That(ActiveButton("Encounter Choice " + targetChoice).interactable, Is.True);
                CaptureExploration(controller, ExplorationNames[route] + "_target_unlocked", targetChoice);
                int ancient = session.Cargo.AncientMaterial, gems = session.Cargo.Gem, relics = session.Cargo.Relic;
                int medicine = session.Cargo.Medicine, fame = session.Cargo.Fame, wood = session.Cargo.Wood;
                int hull = session.Hull;
                double targetHours = session.TotalHours;
                ActiveButton("Encounter Choice " + targetChoice).onClick.Invoke();
                yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(ExplorationNode(session, ExplorationTargets[route]).Visited, Is.True);
                Assert.That(session.TotalHours - targetHours, Is.EqualTo(route == 2 ? 3d : 2d).Within(.0001));
                if (route == 0)
                {
                    Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(ancient + 2));
                    Assert.That(session.Cargo.Wood, Is.EqualTo(wood - 2));
                    Assert.That(hull, Is.LessThanOrEqualTo(session.MaxHull - 8), "The controlled hull damage must leave room to observe the full repair.");
                    Assert.That(session.Hull, Is.EqualTo(Math.Min(session.MaxHull, hull + 8)));
                    Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(16d));
                    Assert.That(session.ExplorationStatusSummary, Does.Contain("航速 +25%"));
                }
                else if (route == 1)
                {
                    Assert.That(session.Cargo.Gem, Is.EqualTo(gems + 1));
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine + 2));
                    Assert.That(session.Cargo.Fame, Is.EqualTo(fame + 8));
                }
                else
                {
                    Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(ancient - 1));
                    Assert.That(session.Cargo.Relic, Is.EqualTo(relics + 1));
                    Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(18d));
                    Assert.That(session.ExplorationStatusSummary, Does.Contain("瞭望 +35%"));
                }
                Assert.That(ActiveButton("Event Node " + ExplorationNode(session, ExplorationTargets[route]).Id).interactable, Is.False);
                Assert.That(session.ExplorationStatusSummary, Does.Contain("（已探索）"));
                yield return new WaitForSecondsRealtime(.5f);
                CaptureExploration(controller, ExplorationNames[route] + "_completed");
                yield return SaveAndResumeExploration(controller, storage, ExplorationNames[route] + "_reward");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                session = controller.Session;
                Assert.That(ExplorationNode(session, ExplorationTargets[route]).Visited, Is.True);
                Assert.That(ActiveButton("Event Node " + ExplorationNode(session, ExplorationTargets[route]).Id).interactable, Is.False);
                Assert.That(session.ExplorationStatusSummary, Does.Contain("（已探索）"));
                if (route == 0) Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(16d));
                if (route == 2) Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(18d));
                yield return new WaitForSecondsRealtime(.5f);
                CaptureExploration(controller, ExplorationNames[route] + "_restored_completed");

                // Advance a real sailing leg after restoring the live timed effect.
                if (route != 1)
                {
                    double beforeHours = session.TotalHours;
                    double beforeBoost = route == 0 ? session.SailingBoostHoursRemaining : session.SurveyBoostHoursRemaining;
                    session.SetCourseTo(session.Map.Departure);
                    session.SetDistance(80);
                    RenderExploration(controller);
                    SailingForecast forecast = session.GetForecast();
                    Assert.That(forecast.Blocked, Is.False);
                    if (route == 0 && forecast.Hours < beforeBoost)
                    {
                        // Comparison clone only: the live restored voyage keeps its earned boost.
                        CoreVoyageCheckpoint ordinaryCheckpoint = session.CaptureVoyageCheckpoint();
                        ordinaryCheckpoint.sailingBoostHoursRemaining = 0d;
                        var ordinary = new CoreVoyageSession();
                        Assert.That(ordinary.TryRestoreVoyageCheckpoint(ordinaryCheckpoint, out string reason), Is.True, reason);
                        Assert.That(forecast.ActualDistanceNm, Is.EqualTo(ordinary.GetForecast().ActualDistanceNm).Within(.001), "Compare equal travel distance, not speed-dependent automatic leg budgets.");
                        Assert.That(forecast.Hours, Is.EqualTo(ordinary.GetForecast().Hours / 1.25d).Within(.0001));
                    }
                    ActiveButton("Execute Sail").onClick.Invoke();
                    yield return WaitExplorationMotion(controller);
                    Assert.That(session.TotalHours, Is.GreaterThan(beforeHours));
                    Assert.That(route == 0 ? session.SailingBoostHoursRemaining : session.SurveyBoostHoursRemaining,
                        Is.EqualTo(Math.Max(0d, beforeBoost - (session.TotalHours - beforeHours))).Within(.0001));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadExplorationFixture(int route, bool beginAtLockedTarget)
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5) });
            session.SelectRoute(route);
            session.SetSeed("EXPLORATION-CHAIN-20261015-" + route);
            session.Cargo.Water = session.Cargo.Food = 300;
            session.Cargo.Wood = 8; session.Cargo.Cloth = 2;
            session.Cargo.Medicine = 2; session.Cargo.Beer = 2;
            session.Cargo.AncientMaterial = route == 2 ? 1 : 0;
            Assert.That(session.Cargo.UsedVolume, Is.LessThan(session.CargoCapacityValue - 15f), "Use a legal starting load with room for earned loot.");
            session.ConfigureCrewCapabilities(true, true, false, false, true, true);
            session.StartVoyage();
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 16);
            // Only the initial position, knowledge and supplies are arranged. Geography,
            // authored nodes, choices, sailing costs, rewards and disk I/O remain production.
            CoreMapNode first = ExplorationNode(session, beginAtLockedTarget ? ExplorationTargets[route] : ExplorationSources[route]);
            var path = session.PlanNavigablePath(first.Position, session.Map.Departure);
            Assert.That(path.Count, Is.GreaterThan(1));
            VoyagePoint next = path[1];
            float distance = Vector2.Distance(new Vector2(first.Position.X, first.Position.Y), new Vector2(next.X, next.Y));
            float fraction = Math.Min(.7f, 3f / distance);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(
                Mathf.Lerp(first.Position.X, next.X, fraction), Mathf.Lerp(first.Position.Y, next.Y, fraction)));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", ExplorationHidden).SetValue(session, true);
            CoreMapNode target = ExplorationNode(session, ExplorationTargets[route]);
            target.Discovered = target.IntelRevealed = false;
            first.Discovered = true;
            RenderExploration(controller);
            yield return new WaitForSecondsRealtime(.5f);
        }

        private static IEnumerator SailExplorationTo(CoreGameplayDemoController controller, string encounterId)
        {
            CoreVoyageSession session = controller.Session;
            for (int step = 0; step < 120; step++)
            {
                if (session.Phase == CoreDemoPhase.Encounter)
                {
                    yield return WaitForEncounterDossierPresentationCompletion();
                    if (session.ActiveEncounter.Id == encounterId) yield break;
                    if (!session.ActiveEncounter.Mandatory) ActiveButton("Skip Encounter").onClick.Invoke();
                    else
                    {
                        int safeChoice = Enumerable.Range(0, session.ActiveEncounter.Choices.Count).Where(index =>
                            !session.ActiveEncounter.Choices[index].StartsBattle && !session.ActiveEncounter.Choices[index].Effect.ForceReturn &&
                            session.IsChoiceAvailable(index)).DefaultIfEmpty(-1).First();
                        Assert.That(safeChoice, Is.GreaterThanOrEqualTo(0), "Unexpected mandatory combat interrupted chain: " + session.ActiveEncounter.Id);
                        ActiveButton("Encounter Choice " + safeChoice).onClick.Invoke();
                    }
                    yield return null;
                }
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                CoreMapNode target = ExplorationNode(session, encounterId);
                Assert.That(target.Visited, Is.False, encounterId);
                Button marker = ActiveButton("Event Node " + target.Id);
                Assert.That(marker.interactable, Is.True, encounterId);
                marker.onClick.Invoke();
                yield return null;
                if (session.Phase == CoreDemoPhase.Encounter) continue;
                Assert.That(session.HasDirectCourseTarget, Is.True);
                Assert.That(session.DirectCourseTarget.X, Is.EqualTo(target.Position.X).Within(.001));
                Assert.That(session.DirectCourseTarget.Y, Is.EqualTo(target.Position.Y).Within(.001));
                Assert.That(session.GetForecast().Blocked, Is.False, encounterId);
                double before = session.TotalHours;
                ActiveButton("Execute Sail").onClick.Invoke();
                yield return WaitExplorationMotion(controller);
                Assert.That(session.TotalHours, Is.GreaterThan(before), "The real Sail button must advance the voyage.");
            }
            Assert.Fail("Could not reach authored encounter: " + encounterId);
        }

        private static IEnumerator WaitExplorationMotion(CoreGameplayDemoController controller)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((bool)typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying", ExplorationHidden).GetValue(controller) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That((bool)typeof(CoreGameplayDemoController).GetField("navigationMotionPlaying", ExplorationHidden).GetValue(controller), Is.False);
            yield return null;
        }

        private static IEnumerator SaveAndResumeExploration(CoreGameplayDemoController controller, ExplorationStorageScope storage, string label)
        {
            // JsonUtility may round a double by one ULP on parse. Compare the full
            // state after the same real JSON boundary, rather than raw decimal text.
            CoreVoyageCheckpoint expected = JsonUtility.FromJson<CoreVoyageCheckpoint>(JsonUtility.ToJson(controller.Session.CaptureVoyageCheckpoint()));
            int oldControllerId = controller.GetInstanceID();
            int portGold = PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold"), -991);
            ActiveButton("Voyage Settings").onClick.Invoke();
            ActiveButton("Return To Main Menu").onClick.Invoke();
            ActiveButton("Confirm Return To Main Menu").onClick.Invoke();
            yield return null; yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            var store = new ProtectedCheckpointStore(storage.VoyageDirectory);
            Assert.That(store.TryLoad(out string payload, out string notice), Is.True, notice);
            Assert.That(payload, Does.Contain("explorationDiscoveries"));
            storage.PreserveEvidence(label, payload);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CargoKey("Gold"), -991), Is.EqualTo(portGold));
            Assert.That(ActiveButton("Continue Game").interactable, Is.True);
            ActiveButton("Continue Game").onClick.Invoke();
            yield return null; yield return null;
            var restored = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(restored, Is.Not.Null, "Continue must construct the actual game controller.");
            Assert.That(restored.GetInstanceID(), Is.Not.EqualTo(oldControllerId));
            CoreVoyageCheckpoint actual = restored.Session.CaptureVoyageCheckpoint();
            // Physical fittings can normalize legacy quality at the production restore boundary.
            expected.battleBowQuality = actual.battleBowQuality;
            expected.battleHullQuality = actual.battleHullQuality;
            Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)), "All voyage state, including claimed loot and clue visibility, must survive disk and controller reconstruction.");
            Assert.That(store.TryLoad(out _, out notice), Is.False, "Continue consumes the parked save so rewards cannot replay.");
            Assert.That(notice, Is.Null);
        }

        private static CoreMapNode ExplorationNode(CoreVoyageSession session, string id) => session.Map.Nodes.Single(node => node.Encounter.Id == id);
        private static int ExplorationTargetChoice(CoreVoyageSession session, int route) => session.ActiveEncounter.Choices.Count - (route == 2 ? 2 : 1);
        private static void AssertExplorationStatus(CoreVoyageSession session, string clue, string target)
        {
            Assert.That(GameObject.Find("Voyage Exploration Status"), Is.Null);
            Assert.That(GameObject.Find("Voyage Exploration Status Text"), Is.Null);
            Assert.That(session.ExplorationStatusSummary, Does.Contain(CoreVoyageSession.DiscoveryLabel(clue)));
            Assert.That(session.ExplorationStatusSummary, Does.Contain(CoreVoyageEventCatalog.DiscoveryTargetName(target)));
        }
        private static void RenderExploration(CoreGameplayDemoController controller) => typeof(CoreGameplayDemoController).GetMethod("Render", ExplorationHidden).Invoke(controller, null);
        private static void CaptureExploration(CoreGameplayDemoController controller, string state, int choice = -1)
        {
            Type capture = typeof(CoreGameplayDemoSceneTests).Assembly.GetType("ShatteredSail.Presentation.Tests.ExplorationUiCapture");
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-explorationUiCapturePath") >= 0 || Array.IndexOf(args, "-navalVisualCapturePath") >= 0)
                Assert.That(capture, Is.Not.Null, "An opt-in visual audit must install its actual UI capture helper.");
            capture?.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { controller, state, choice });
        }

        private sealed class ExplorationStorageScope : IDisposable
        {
            private readonly Dictionary<FieldInfo, object> previous = new Dictionary<FieldInfo, object>();
            private readonly string directory = Path.Combine(Path.GetTempPath(), "ShatteredSailExplorationPlayMode-" + Guid.NewGuid().ToString("N"));
            public string VoyageDirectory => Path.Combine(directory, "Voyage");
            public ExplorationStorageScope()
            {
                Type suspension = typeof(CoreGameplayDemoController).Assembly.GetType("ShatteredSail.Presentation.VoyageSuspensionPersistence", true);
                foreach (Type type in new[] { suspension, typeof(ProfileSaveJournal) })
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    previous.Add(field, field.GetValue(null));
                    field.SetValue(null, null);
                }
                suspension.GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, VoyageDirectory);
                typeof(ProfileSaveJournal).GetField("directoryOverride", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Path.Combine(directory, "Journal"));
            }
            public void PreserveEvidence(string label, string payload)
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-explorationUiCapturePath");
                if (index < 0 || index + 1 >= args.Length) return;
                string root = Path.GetFullPath(args[index + 1]);
                Assert.That(root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Does.Contain("Artifacts"));
                string output = Path.Combine(root, "save-evidence");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, label + ".json"), payload);
            }
            public void Dispose()
            {
                foreach (CoreGameplayDemoController game in Object.FindObjectsByType<CoreGameplayDemoController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(game.gameObject);
                foreach (MainMenuController menu in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(menu.gameObject);
                foreach (var pair in previous) pair.Key.SetValue(null, pair.Value);
                string resolved = Path.GetFullPath(directory);
                Assert.That(resolved.StartsWith(Path.Combine(Path.GetTempPath(), "ShatteredSailExplorationPlayMode-"), StringComparison.OrdinalIgnoreCase), Is.True);
                if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
            }
        }
    }
}
