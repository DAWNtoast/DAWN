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
        [UnityTest] public IEnumerator TreasureExpedition_Weathered_ToolsOpenWrecksAndSurviveDiskResume() => RunTreasureExpedition("weathered");
        [UnityTest] public IEnumerator TreasureExpedition_Gilded_CreditAndHeatChangeChoicesAndSurviveDiskResume() => RunTreasureExpedition("gilded");
        [UnityTest] public IEnumerator TreasureExpedition_Moonlit_TimeCurseAndResonanceSurviveDiskResume() => RunTreasureExpedition("moonlit");

        [UnityTest]
        public IEnumerator TreasureExpedition_FolioAndDepartureExplainThreeDistinctObjectives()
        {
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadProvisioningFixture();
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                string[] ranks = { "weathered", "gilded", "moonlit" };
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                    maps = ranks.Select((rank, index) => new CoreTreasureMapSave {
                        id = "folio-expedition-" + rank, seed = "FOLIO-EXPEDITION-" + rank, rankId = rank,
                        // An unused legacy chart also previews the new adventure it
                        // will receive when it is consumed at the departure boundary.
                        contentVersion = index == 0 ? 0 : 1 }).ToArray()
                });
                session.Cargo.Food = session.Cargo.Water = 100;
                ActiveButton("Port Hotspot Treasure").onClick.Invoke(); yield return null;
                ActiveButton("Treasure Open Folio").onClick.Invoke(); yield return null;
                foreach (int index in Enumerable.Range(0, ranks.Length))
                {
                    Assert.That(GameObject.Find("Treasure Folio Rank Theme " + index).GetComponent<Text>().text,
                        Is.EqualTo(CoreTreasureExpeditionCatalog.ThemeName(ranks[index])));
                }
                CaptureExploration(controller, "treasure_port_folio_overview");
                ActiveButton("Treasure Browse Unused").onClick.Invoke(); yield return null;
                Assert.That(GameObject.Find("Treasure Map Details 0").GetComponent<Text>().text,
                    Does.Contain(CoreTreasureExpeditionCatalog.ThemeName("weathered")));
                CaptureExploration(controller, "treasure_port_folio_weathered_gilded");
                ActiveButton("Treasure Folio Next Page").onClick.Invoke(); yield return null;
                CaptureExploration(controller, "treasure_port_folio_moonlit");
                ActiveButton("Treasure Close Folio").onClick.Invoke(); yield return null;
                // Return through the facility's actual navigation, then select
                // each seed with the production dropdown and pointer helper.
                ActiveButton("Return To Port Hub").onClick.Invoke(); yield return null;
                ActiveButton("Port Hotspot Voyage").onClick.Invoke(); yield return null;
                ActiveButton("Departure Treasure Seed Picker").onClick.Invoke(); yield return null;
                for (int index = 0; index < ranks.Length; index++)
                {
                    yield return SelectDepartureTreasureOption(index);
                    Assert.That(GameObject.Find("Departure Dock Treasure Detail").GetComponent<Text>().text,
                        Does.Contain(CoreTreasureExpeditionCatalog.ThemeName(ranks[index])));
                    CaptureExploration(controller, "treasure_port_departure_" + ranks[index]);
                }
                Assert.That(session.TreasureMaps.Count, Is.EqualTo(3), "Browsing never consumes a chart.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator RunTreasureExpedition(string rank)
        {
            using (var storage = new ExplorationStorageScope())
            {
                yield return LoadTreasureExpeditionFixture(rank);
                var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                CoreVoyageSession session = controller.Session;
                Assert.That(session.IsTreasureExpedition, Is.True);
                Assert.That(session.Map.Nodes.Where(node => !node.Encounter.Id.StartsWith("legendary_"))
                    .All(node => node.Encounter.Id == "treasure_core" || node.Encounter.Id.StartsWith(rank + "_")), Is.True,
                    "A rank must generate its own event pool, not renamed copies of shared events.");
                yield return SailTreasureExpeditionTo(controller, "treasure_core");
                yield return AssertTreasureChoiceLocked(controller, 0);
                CaptureExploration(controller, "treasure_" + rank + "_core_locked", 0);
                yield return PostponeTreasureExpedition(controller);

                if (rank == "weathered")
                {
                    yield return SailTreasureExpeditionTo(controller, "weathered_winch");
                    yield return AssertTreasureChoiceLocked(controller, 0);
                    Assert.That(ActiveButton("Encounter Choice 2").interactable, Is.True, "Slow salvage must remain possible without materials or a carpenter.");
                    CaptureExploration(controller, "treasure_weathered_material_shortage", 0);
                    ActiveButton("Skip Encounter").onClick.Invoke(); yield return null;
                    Assert.That(ExplorationNode(session, "weathered_winch").Visited, Is.False);
                    yield return SailTreasureExpeditionTo(controller, "weathered_camp");
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.Cargo.Wood, Is.EqualTo(6)); Assert.That(session.Cargo.Iron, Is.EqualTo(8));
                    Assert.That(session.Cargo.Cloth, Is.EqualTo(3));
                    yield return SailTreasureExpeditionTo(controller, "weathered_winch");
                    CaptureExploration(controller, "treasure_weathered_tool_ready", 0);
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.HasTreasureMark("weathered_winch"), Is.True);
                    Assert.That(session.Cargo.Wood, Is.Zero); Assert.That(session.Cargo.Iron, Is.EqualTo(4));
                    yield return SaveAndResumeExploration(controller, storage, "treasure_weathered_winch");
                    controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
                    Assert.That(session.HasTreasureMark("weathered_winch"), Is.True);
                    Assert.That(ExplorationNode(session, "weathered_winch").Visited, Is.True);
                    yield return SailTreasureExpeditionTo(controller, "weathered_bell");
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.Cargo.Iron, Is.Zero); Assert.That(session.Cargo.Cloth, Is.Zero);
                    Assert.That(session.HasTreasureMark("weathered_bell"), Is.True);
                    Assert.That(session.TreasureExpeditionStatusSummary, Does.Contain(CoreVoyageSession.TreasureMarkLabel("weathered_bell")));
                    yield return ClaimTreasureExpeditionSideBenefit(controller, rank);
                }
                else if (rank == "gilded")
                {
                    yield return SailTreasureExpeditionTo(controller, "gilded_manifest");
                    CaptureExploration(controller, "treasure_gilded_legal_or_forged", 1);
                    yield return ChooseTreasureExpedition(controller, 1);
                    Assert.That(session.TreasureExpeditionCredit, Is.EqualTo(3));
                    Assert.That(session.TreasureExpeditionHeat, Is.EqualTo(2));
                    yield return SaveAndResumeExploration(controller, storage, "treasure_gilded_forged_manifest");
                    controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
                    Assert.That(session.TreasureExpeditionHeat, Is.EqualTo(2));
                    Assert.That(session.HasTreasureMark("gilded_manifest"), Is.True);
                    yield return SailTreasureExpeditionTo(controller, "treasure_core");
                    yield return AssertTreasureChoiceLocked(controller, 0);
                    Assert.That(session.ChoiceUnavailableReason(0), Does.Contain("通缉"));
                    CaptureExploration(controller, "treasure_gilded_heat_blocks_vault", 0);
                    yield return PostponeTreasureExpedition(controller);
                    yield return SailTreasureExpeditionTo(controller, "gilded_customs");
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.TreasureExpeditionHeat, Is.Zero);
                    yield return ClaimTreasureExpeditionSideBenefit(controller, rank);
                    Assert.That(session.TreasureExpeditionCredit, Is.LessThan(3), "Spending credit has a real opportunity cost at the core.");
                    yield return SailTreasureExpeditionTo(controller, "gilded_witness");
                    int food = session.Cargo.Food, water = session.Cargo.Water;
                    int rations = TreasureChoiceRations(session, 0);
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.Cargo.Food, Is.EqualTo(food - rations - 12));
                    Assert.That(session.Cargo.Water, Is.EqualTo(water - rations - 12));
                    Assert.That(session.TreasureExpeditionCredit, Is.GreaterThanOrEqualTo(3));
                    Assert.That(session.HasTreasureMark("gilded_witness"), Is.True);
                }
                else
                {
                    yield return SailTreasureExpeditionTo(controller, "moonlit_dawn");
                    // Deliberately wait through the real choice, without setting the clock.
                    bool observedLockedTime = false;
                    bool capturedWait = false;
                    for (int waits = 0; waits < 9; waits++)
                    {
                        if (session.IsChoiceAvailable(0) && observedLockedTime) break;
                        if (!session.IsChoiceAvailable(0))
                        {
                            observedLockedTime = true;
                            yield return AssertTreasureChoiceLocked(controller, 0);
                            if (!capturedWait)
                            {
                                CaptureExploration(controller, "treasure_moonlit_wait_for_phase", 2);
                                capturedWait = true;
                            }
                        }
                        double before = session.TotalHours;
                        yield return ChooseTreasureExpedition(controller, 2);
                        Assert.That(session.TotalHours - before, Is.EqualTo(3d).Within(1e-9));
                        Assert.That(ExplorationNode(session, "moonlit_dawn").Visited, Is.False);
                        yield return SailTreasureExpeditionTo(controller, "moonlit_dawn");
                    }
                    Assert.That(observedLockedTime, Is.True);
                    Assert.That(session.IsChoiceAvailable(0), Is.True);
                    int curse = session.Cargo.Curse;
                    CaptureExploration(controller, "treasure_moonlit_clean_ritual", 0);
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.Cargo.Curse, Is.EqualTo(curse));
                    Assert.That(session.TreasureExpeditionResonance, Is.EqualTo(1));
                    yield return SaveAndResumeExploration(controller, storage, "treasure_moonlit_day_altar");
                    controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
                    foreach (string altar in new[] { "moonlit_dusk", "moonlit_midnight" })
                    {
                        yield return SailTreasureExpeditionTo(controller, altar);
                        curse = session.Cargo.Curse;
                        yield return ChooseTreasureExpedition(controller, 1);
                        Assert.That(session.Cargo.Curse, Is.EqualTo(curse + 1));
                        Assert.That(session.HasTreasureMark(altar), Is.True);
                    }
                    Assert.That(session.TreasureExpeditionResonance, Is.EqualTo(3));
                    yield return SailTreasureExpeditionTo(controller, "treasure_core");
                    yield return AssertTreasureChoiceLocked(controller, 0);
                    Assert.That(session.ChoiceUnavailableReason(0), Does.Contain("诅咒"));
                    CaptureExploration(controller, "treasure_moonlit_curse_blocks_core", 0);
                    yield return PostponeTreasureExpedition(controller);
                    yield return SailTreasureExpeditionTo(controller, "moonlit_cleanse");
                    CaptureExploration(controller, "treasure_moonlit_cleanse", 0);
                    yield return ChooseTreasureExpedition(controller, 0);
                    Assert.That(session.Cargo.Curse, Is.Zero);
                    yield return ClaimTreasureExpeditionSideBenefit(controller, rank);
                }

                yield return new WaitForSecondsRealtime(.5f);
                CaptureExploration(controller, "treasure_" + rank + "_progress_and_side_reward");
                yield return SaveAndResumeExploration(controller, storage, "treasure_" + rank + "_side_reward");
                controller = Object.FindFirstObjectByType<CoreGameplayDemoController>(); session = controller.Session;
                float distance = session.TotalDistanceNm;
                double hours = session.TotalHours;
                double speed = session.SailingBoostHoursRemaining, survey = session.SurveyBoostHoursRemaining;
                yield return SailTreasureExpeditionTo(controller, "treasure_core");
                Assert.That(session.TotalDistanceNm, Is.GreaterThan(distance), "Earned state must still be carried to the physical core by sailing.");
                Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(Math.Max(0d, speed - (session.TotalHours - hours))).Within(.0001));
                Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(Math.Max(0d, survey - (session.TotalHours - hours))).Within(.0001));
                Assert.That(ActiveButton("Encounter Choice 0").interactable, Is.True, session.ChoiceUnavailableReason(0));
                CaptureExploration(controller, "treasure_" + rank + "_core_unlocked", 0);
                int ancient = session.Cargo.AncientMaterial, gems = session.Cargo.Gem;
                int relics = session.Cargo.Relic, pearls = session.Cargo.BlackPearl;
                int compass = session.Cargo.AncientCompass, lantern = session.Cargo.Lantern;
                int credit = session.TreasureExpeditionCredit;
                string seed = session.ActiveTreasureMap.Seed;
                yield return ChooseTreasureExpedition(controller, 0);
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
                Assert.That(session.TreasureAdventureCompleted, Is.True);
                double protection = session.Settlement.ProtectionRatio;
                if (rank == "weathered")
                {
                    Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(Math.Floor((ancient + 10) * protection)));
                    Assert.That(session.Cargo.AncientCompass, Is.EqualTo(compass + 1));
                    Assert.That(session.Cargo.Lantern, Is.EqualTo(lantern + 1));
                }
                else if (rank == "gilded")
                {
                    Assert.That(session.TreasureExpeditionCredit, Is.EqualTo(credit - 3));
                    Assert.That(session.Cargo.Gem, Is.EqualTo(Math.Ceiling((gems + 2) * protection)));
                    Assert.That(session.Cargo.Curse, Is.Zero);
                }
                else
                {
                    Assert.That(session.Cargo.Relic, Is.EqualTo(Math.Ceiling((relics + 3) * protection)));
                    Assert.That(session.Cargo.BlackPearl, Is.EqualTo(Math.Ceiling((pearls + 2) * protection)));
                    Assert.That(session.Cargo.AncientCompass, Is.EqualTo(compass + 1));
                }
                ActiveButton("Skip Settlement Reveal").onClick.Invoke(); yield return null;
                ActiveButton("Return To Port").onClick.Invoke(); yield return null;
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                Assert.That(session.CompletedTreasureSeeds, Does.Contain(seed));
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Does.Contain(seed));
                Assert.That(session.TreasureExpeditionStatusSummary, Is.Empty);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadTreasureExpeditionFixture(string rank)
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(5),
                identifiedWeatheredFragments = rank == "weathered" ? 4 : 0,
                identifiedGildedFragments = rank == "gilded" ? 4 : 0,
                identifiedMoonlitFragments = rank == "moonlit" ? 4 : 0
            });
            string seed = "TREASURE-DIFFERENTIATION-20261015-" + rank;
            Assert.That(session.TryCraftTreasureMapForRank(rank, seed, out string reason), Is.True, reason);
            Assert.That(session.TrySelectTreasureSeed(seed, out reason), Is.True, reason);
            Assert.That(session.SelectedTreasureMap.ContentVersion, Is.EqualTo(1));
            Assert.That(session.SelectedTreasureMap.MapGeometryVersion, Is.EqualTo(4), "Exercise the geometry produced by new crafting.");
            // The moonlit regression deliberately visits the distant core three
            // times to test both locks, then sails back for cleansing. Provision
            // that extended itinerary before departure, within the actual hold.
            session.Cargo.Food = session.Cargo.Water = rank == "moonlit" ? 450 : 300;
            session.Cargo.Gold = 100;
            Assert.That(session.Cargo.UsedVolume, Is.LessThanOrEqualTo(session.CargoCapacityValue - (rank == "moonlit" ? 8f : 30f)));
            session.ConfigureCrewCapabilities(true, true, false, false, false, false);
            Assert.That(session.TryStartTreasureVoyage(out reason), Is.True, reason);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, session.MaxHull - 36);
            // Only the initial position, supplies, map visibility and ambush fixture
            // are arranged. No marks, credit, resonance, rewards or visits are forged.
            foreach (CoreMapNode node in session.Map.Nodes) node.Discovered = true;
            CoreMapNode core = ExplorationNode(session, "treasure_core");
            var path = session.PlanNavigablePath(core.Position, session.Map.Departure);
            Assert.That(path.Count, Is.GreaterThan(1));
            VoyagePoint next = path[1];
            float length = Vector2.Distance(new Vector2(core.Position.X, core.Position.Y), new Vector2(next.X, next.Y));
            float fraction = Math.Min(.7f, 3f / length);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session,
                new VoyagePoint(Mathf.Lerp(core.Position.X, next.X, fraction), Mathf.Lerp(core.Position.Y, next.Y, fraction)));
            typeof(CoreVoyageSession).GetField("randomAmbushOccurred", ExplorationHidden).SetValue(session, true);
            RenderExploration(controller); yield return null;
            yield return DismissTreasureStoryIfPresent(controller);
            yield return new WaitForSecondsRealtime(.5f);
        }

        private static IEnumerator AssertTreasureChoiceLocked(CoreGameplayDemoController controller, int choice)
        {
            Button button = ActiveButton("Encounter Choice " + choice);
            Assert.That(button.interactable, Is.False);
            string before = JsonUtility.ToJson(controller.Session.CaptureVoyageCheckpoint());
            button.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureVoyageCheckpoint()), Is.EqualTo(before),
                "A disabled production callback must not spend time, cargo or expedition progress.");
            // The guarded callback still rebuilds the real dossier. Wait for
            // that newly created animation before observing or clicking it.
            yield return WaitForEncounterDossierPresentationCompletion();
        }

        private static int TreasureChoiceRations(CoreVoyageSession session, int choice) =>
            (int)typeof(CoreVoyageSession).GetMethod("RationCostFor", ExplorationHidden)
                .Invoke(session, new object[] { session.ActiveEncounter.Choices[choice].Hours });

        private static IEnumerator ChooseTreasureExpedition(CoreGameplayDemoController controller, int choice)
        {
            yield return WaitForEncounterDossierPresentationCompletion();
            Button button = ActiveButton("Encounter Choice " + choice);
            Assert.That(button.interactable, Is.True, controller.Session.ActiveEncounter.Id + ": " + controller.Session.ChoiceUnavailableReason(choice));
            ScrollRect scroll = GameObject.Find("Encounter Decision Viewport").GetComponent<ScrollRect>();
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1f; Canvas.ForceUpdateCanvases();
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0f)
            {
                Vector3[] corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                float top = corners.Max(corner => scroll.viewport.InverseTransformPoint(corner).y);
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((scroll.viewport.rect.yMax - top) / overflow);
            }
            yield return null; Canvas.ForceUpdateCanvases();
            ClickAcceptanceButton("Encounter Choice " + choice); yield return null;
        }

        private static IEnumerator PostponeTreasureExpedition(CoreGameplayDemoController controller)
        {
            string id = controller.Session.ActiveEncounter.Id;
            int choice = Enumerable.Range(0, controller.Session.ActiveEncounter.Choices.Count)
                .Single(index => controller.Session.ActiveEncounter.Choices[index].Effect.PostponeTreasureCore);
            yield return ChooseTreasureExpedition(controller, choice);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(ExplorationNode(controller.Session, id).Visited, Is.False);
        }

        private static IEnumerator SailTreasureExpeditionTo(CoreGameplayDemoController controller, string id)
        {
            CoreVoyageSession session = controller.Session;
            for (int step = 0; step < 180; step++)
            {
                yield return DismissTreasureStoryIfPresent(controller);
                if (session.Phase == CoreDemoPhase.Encounter)
                {
                    yield return WaitForEncounterDossierPresentationCompletion();
                    if (session.ActiveEncounter.Id == id) yield break;
                    if (session.ActiveEncounter.Mandatory) yield return PostponeTreasureExpedition(controller);
                    else { ActiveButton("Skip Encounter").onClick.Invoke(); yield return null; }
                }
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), "Sailing toward " + id);
                CoreMapNode target = ExplorationNode(session, id);
                Assert.That(target.Visited, Is.False, id);
                Button marker = ActiveButton("Event Node " + target.Id);
                Assert.That(marker.interactable, Is.True, id); marker.onClick.Invoke(); yield return null;
                if (session.Phase == CoreDemoPhase.Encounter) continue;
                Assert.That(session.HasDirectCourseTarget, Is.True);
                Assert.That(session.GetForecast().Blocked, Is.False, id);
                double hours = session.TotalHours;
                ActiveButton("Execute Sail").onClick.Invoke(); yield return WaitExplorationMotion(controller);
                Assert.That(session.TotalHours, Is.GreaterThan(hours));
            }
            Assert.Fail("Real sailing did not reach " + id);
        }

        private static IEnumerator ClaimTreasureExpeditionSideBenefit(CoreGameplayDemoController controller, string rank)
        {
            CoreVoyageSession session = controller.Session;
            var candidates = session.Map.Nodes.Where(node => node.Encounter.Id.StartsWith(rank + "_") && !node.Visited)
                .SelectMany(node => node.Encounter.Choices.Select((choice, index) => new { node, choice, index }))
                .Where(item => !item.choice.StartsBattle && !item.choice.Effect.TreasureComplete && item.choice.Effect.Wood >= 0 &&
                    (rank == "gilded" ? item.choice.Effect.TreasureCredit < 0 :
                        item.choice.Effect.SailingBoostHours > 0 || item.choice.Effect.SurveyBoostHours > 0))
                // The moon chart is a long voyage. Use the nearest earned
                // ritual benefit rather than detouring across the entire map
                // solely to take the longest advertised buff.
                .OrderBy(item => rank == "moonlit"
                    ? Math.Pow(item.node.Position.X - session.Position.X, 2) + Math.Pow(item.node.Position.Y - session.Position.Y, 2)
                    : -(item.choice.Effect.SailingBoostHours + item.choice.Effect.SurveyBoostHours)).ToArray();
            Assert.That(candidates.Length, Is.GreaterThan(0), "Generated map must offer usable side benefits.");
            foreach (var candidate in candidates)
            {
                yield return SailTreasureExpeditionTo(controller, candidate.node.Encounter.Id);
                if (!session.IsChoiceAvailable(candidate.index))
                { ActiveButton("Skip Encounter").onClick.Invoke(); yield return null; continue; }
                VoyageEffect effect = candidate.choice.Effect;
                int credit = session.TreasureExpeditionCredit, medicine = session.Cargo.Medicine, lantern = session.Cargo.Lantern;
                int compass = session.Cargo.AncientCompass;
                int hull = session.Hull;
                CaptureExploration(controller, "treasure_" + rank + "_side_benefit", candidate.index);
                yield return ChooseTreasureExpedition(controller, candidate.index);
                Assert.That(candidate.node.Visited, Is.True);
                if (effect.TreasureCredit < 0) Assert.That(session.TreasureExpeditionCredit, Is.EqualTo(credit + effect.TreasureCredit));
                if (effect.Medicine > 0) Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine + effect.Medicine));
                if (effect.Lantern > 0) Assert.That(session.Cargo.Lantern, Is.EqualTo(lantern + effect.Lantern));
                if (effect.AncientCompass > 0) Assert.That(session.Cargo.AncientCompass, Is.EqualTo(compass + effect.AncientCompass));
                if (effect.Hull > 0) Assert.That(session.Hull, Is.EqualTo(Math.Min(session.MaxHull, hull + effect.Hull)));
                if (effect.SailingBoostHours > 0) Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(effect.SailingBoostHours));
                if (effect.SurveyBoostHours > 0) Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(effect.SurveyBoostHours));
                yield break;
            }
            Assert.Fail("No generated side benefit was usable with earned expedition state.");
        }
    }
}
