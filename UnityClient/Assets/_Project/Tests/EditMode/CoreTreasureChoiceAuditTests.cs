using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureChoiceAuditTests
    {
        private static IEnumerable<TestCaseData> EveryExpeditionChoice()
        {
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                foreach (CoreEncounter encounter in CoreTreasureExpeditionCatalog.CreateEncounters(rank))
                    for (int index = 0; index < encounter.Choices.Count; index++)
                        yield return new TestCaseData(rank.Id, encounter.Id, index)
                            .SetName("TreasureChoiceExecutes_" + rank.Id + "_" + encounter.Id + "_" + index);
        }

        [TestCaseSource(nameof(EveryExpeditionChoice))]
        public void EveryAuthoredExpeditionChoiceExecutesWithItsRequirementsAndCannotDoubleApply(string rank, string id, int choiceIndex)
        {
            CoreEncounter encounter = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(rank))
                .Single(e => e.Id == id);
            CoreEncounterChoice choice = encounter.Choices[choiceIndex];
            CoreVoyageSession session = Start(rank, choice);
            PrepareExpeditionRequirements(session, choice);
            // The seeded chart only selects some side sites. Reuse one real
            // chart node for an omitted authored site; entry and completion
            // still go through the same public chart and resolver APIs.
            CoreMapNode node = session.Map.Nodes.FirstOrDefault(n => n.Encounter.Id == id) ?? session.Map.Nodes[0];
            node.Encounter = encounter;
            OpenNearby(session, id);
            int[] before = CargoProperties.Select(p => (int)p.GetValue(session.Cargo)).ToArray();
            int hull = session.Hull, morale = session.Morale;
            int credit = session.TreasureExpeditionCredit, heat = session.TreasureExpeditionHeat,
                resonance = session.TreasureExpeditionResonance;
            int rations = (int)Math.Ceiling(session.EstimateDailyRationConsumption(session.EffectiveCrewCount, session.HasCook) * choice.Hours / 24d - 1e-9d);

            Assert.That(session.IsChoiceAvailable(choiceIndex), Is.True, session.ChoiceUnavailableReason(choiceIndex));
            Assert.That(session.ResolveChoice(choiceIndex), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(choice.Hours));
            Assert.That(session.Hull, Is.EqualTo(Math.Max(0, Math.Min(session.MaxHull, hull + choice.Effect.Hull))));
            Assert.That(session.Morale, Is.EqualTo(Math.Max(0, Math.Min(CoreVoyageSession.MaxMorale, morale + choice.Effect.Morale))));
            Assert.That(session.TreasureExpeditionCredit, Is.EqualTo(Math.Max(0, Math.Min(6, credit + choice.Effect.TreasureCredit))));
            Assert.That(session.TreasureExpeditionHeat, Is.EqualTo(Math.Max(0, Math.Min(5, heat + choice.Effect.TreasureHeat))));
            Assert.That(session.TreasureExpeditionResonance, Is.EqualTo(Math.Max(0, Math.Min(3, resonance + choice.Effect.TreasureResonance))));
            if (!string.IsNullOrEmpty(choice.Effect.TreasureMark))
                Assert.That(session.HasTreasureMark(choice.Effect.TreasureMark), Is.True);

            if (choice.Effect.TreasureComplete)
            {
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Assert.That(session.TreasureAdventureCompleted, Is.True);
                Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.TreasureRecovered));
                Assert.That(session.Settlement.TreasureCompleted, Is.True);
                Assert.That(session.Settlement.BankedGold, Is.EqualTo((int)Math.Floor(Math.Max(0, choice.Effect.Gold) * .95f)));
                Assert.That(session.Settlement.BankedGem, Is.EqualTo((int)Math.Ceiling(Math.Max(0, choice.Effect.Gem) * .95d)));
                Assert.That(session.Settlement.BankedRelic, Is.EqualTo((int)Math.Ceiling(Math.Max(0, choice.Effect.Relic) * .95d)));
                Assert.That(session.Settlement.BankedBlackPearl, Is.EqualTo((int)Math.Ceiling(Math.Max(0, choice.Effect.BlackPearl) * .95d)));
            }
            else
            {
                Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(session.Settlement, Is.Null);
                for (int index = 0; index < CargoProperties.Length; index++)
                {
                    PropertyInfo property = CargoProperties[index];
                    int expected = before[index] + (int)typeof(VoyageEffect).GetProperty(property.Name).GetValue(choice.Effect);
                    if (property.Name == "Water" || property.Name == "Food") expected -= rations;
                    Assert.That(property.GetValue(session.Cargo), Is.EqualTo(expected), property.Name);
                }
                Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(choice.Effect.SailingBoostHours));
                Assert.That(session.SurveyBoostHoursRemaining, Is.EqualTo(choice.Effect.SurveyBoostHours));
            }
            Assert.That(node.Visited, Is.EqualTo(!choice.Effect.PostponeTreasureCore));
            Assert.That(session.ActiveEncounter, Is.Null);
            string resolved = State(session);
            VoyageSettlement settlement = session.Settlement;
            Assert.That(session.IsChoiceAvailable(choiceIndex), Is.False);
            Assert.That(session.ResolveChoice(choiceIndex), Is.False);
            Assert.That(State(session), Is.EqualTo(resolved));
            Assert.That(session.Settlement, Is.SameAs(settlement));
        }

        [TestCase("weathered", "weathered_tide_turbine", 1)]
        [TestCase("gilded", "gilded_relief", 1)]
        [TestCase("moonlit", "moonlit_star_current", 1)]
        public void CompletedRewardSiteCannotBeReopenedOrClaimedAgainAfterCheckpoint(string rank, string id, int choice)
        {
            CoreVoyageSession session = Start(rank);
            if (rank == "moonlit")
            {
                OpenNearby(session, "moonlit_dawn");
                Assert.That(session.ResolveChoice(1), Is.True);
            }
            OpenNearby(session, id);
            Assert.That(session.ResolveChoice(choice), Is.True, session.ChoiceUnavailableReason(choice));
            Assert.That(session.Map.Nodes.Single(n => n.Encounter.Id == id).Visited, Is.True);
            if (rank == "weathered") Assert.That(session.Cargo.Iron, Is.EqualTo(5));
            if (rank == "gilded") Assert.That(session.TreasureExpeditionCredit, Is.EqualTo(3));
            if (rank == "moonlit") Assert.That(session.SailingBoostHoursRemaining, Is.EqualTo(12d));

            string earned = State(session);
            AssertCannotClaimAgain(session, id, choice, earned);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(session.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            Assert.That(State(restored), Is.EqualTo(earned));
            Assert.That(restored.Map.Nodes.Single(n => n.Encounter.Id == id).Visited, Is.True);
            AssertCannotClaimAgain(restored, id, choice, earned);
        }

        [TestCase("weathered", "treasure_core", 2, 0d)]
        [TestCase("gilded", "treasure_core", 3, 0d)]
        [TestCase("moonlit", "treasure_core", 3, 0d)]
        [TestCase("moonlit", "moonlit_dawn", 2, 3d)]
        public void DeferredSitesStayRevisitableAcrossCheckpointWithoutGrantingRewards(string rank, string id, int choice, double hours)
        {
            CoreVoyageSession session = Start(rank);
            int[] baseline = CargoProperties.Select(p => (int)p.GetValue(session.Cargo)).ToArray();
            OpenNearby(session, id);
            double before = session.TotalHours;
            Assert.That(session.ResolveChoice(choice), Is.True);
            Assert.That(session.TotalHours, Is.EqualTo(before + hours));
            CoreMapNode deferred = session.Map.Nodes.Single(n => n.Encounter.Id == id);
            Assert.That(deferred.Visited, Is.False);
            Assert.That(deferred.Skipped, Is.True);
            AssertNoReward(session, baseline);

            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(session.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            CoreMapNode node = restored.Map.Nodes.Single(n => n.Encounter.Id == id);
            restored.SetCourseTo(node.Position);
            Assert.That(restored.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(restored.ActiveEncounter.Id, Is.EqualTo(id));
            Assert.That(restored.ResolveChoice(choice), Is.True);
            Assert.That(restored.TotalHours, Is.EqualTo(before + hours * 2d));
            Assert.That(node.Visited, Is.False);
            AssertNoReward(restored, baseline);
        }

        private static CoreVoyageSession Start(string rank, CoreEncounterChoice preparedChoice = null)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress {
                pirateExperience = 1400, selectedMapId = rank,
                maps = new[] { new CoreTreasureMapSave {
                    id = rank, seed = "CHOICE-AUDIT-" + rank, rankId = rank, contentVersion = 1, mapGeometryVersion = 1 } }
            });
            string crew = preparedChoice?.RequiredCrew ?? string.Empty;
            session.ConfigureCrewCapabilities(crew == "navigator", crew == "carpenter", crew == "doctor",
                crew == "gunner", crew == "cook", crew == "diver");
            foreach (PropertyInfo property in CargoProperties) property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 100;
            if (preparedChoice != null)
            {
                foreach (PropertyInfo property in CargoProperties)
                {
                    int cost = Math.Max(0, -(int)typeof(VoyageEffect).GetProperty(property.Name).GetValue(preparedChoice.Effect));
                    property.SetValue(session.Cargo, Math.Max((int)property.GetValue(session.Cargo), cost));
                }
                session.Cargo.Curse = Math.Max(session.Cargo.Curse, preparedChoice.MinimumCurse);
                double clock = preparedChoice.RequiredTime == CoreVoyageTimeBand.Dusk ? 10d :
                    preparedChoice.RequiredTime == CoreVoyageTimeBand.Night ? 14d : 0d;
                Assert.That(session.RestoreGameClock(clock), Is.True);
            }
            Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
            return session;
        }

        private static void PrepareExpeditionRequirements(CoreVoyageSession session, CoreEncounterChoice choice)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var marks = (HashSet<string>)typeof(CoreVoyageSession).GetField("treasureExpeditionMarks", hidden).GetValue(session);
            marks.UnionWith(choice.RequiredTreasureMark.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries));
            if (session.ActiveTreasureMap.RankId == "moonlit")
            {
                foreach (string mark in new[] { "moonlit_dawn", "moonlit_dusk", "moonlit_midnight" })
                {
                    if (marks.Count >= choice.MinimumTreasureResonance) break;
                    if (mark != choice.Effect.TreasureMark) marks.Add(mark);
                }
                typeof(CoreVoyageSession).GetField("treasureExpeditionResonance", hidden).SetValue(session, marks.Count);
            }
            if (session.ActiveTreasureMap.RankId == "gilded")
            {
                typeof(CoreVoyageSession).GetField("treasureExpeditionCredit", hidden)
                    .SetValue(session, Math.Max(choice.MinimumTreasureCredit, -choice.Effect.TreasureCredit));
                typeof(CoreVoyageSession).GetField("treasureExpeditionHeat", hidden)
                    .SetValue(session, Math.Min(choice.MaximumTreasureHeat, Math.Max(0, -choice.Effect.TreasureHeat)));
            }
            // Leave room for authored healing while retaining enough hull for
            // every current risky recovery. No checkpoint is fabricated here.
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, Math.Max(30, session.MaxHull - 24));
        }

        private static void OpenNearby(CoreVoyageSession session, string id)
        {
            CoreMapNode node = session.Map.Nodes.Single(n => n.Encounter.Id == id);
            // Only position is arranged: use the actual chart selection API to
            // enter the authored node and retain its normal visited semantics.
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, node.Position);
            session.SetCourseTo(node.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(id));
        }

        private static void AssertCannotClaimAgain(CoreVoyageSession session, string id, int choice, string expected)
        {
            session.SetCourseTo(session.Map.Nodes.Single(n => n.Encounter.Id == id).Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
            Assert.That(session.IsChoiceAvailable(choice), Is.False);
            Assert.That(session.ResolveChoice(choice), Is.False);
            Assert.That(State(session), Is.EqualTo(expected));
        }

        private static void AssertNoReward(CoreVoyageSession session, int[] baseline)
        {
            for (int index = 0; index < CargoProperties.Length; index++)
            {
                PropertyInfo property = CargoProperties[index];
                if (property.Name == "Water" || property.Name == "Food") continue;
                Assert.That(property.GetValue(session.Cargo), Is.EqualTo(baseline[index]), property.Name);
            }
            Assert.That(session.TreasureExpeditionCredit + session.TreasureExpeditionHeat + session.TreasureExpeditionResonance, Is.Zero);
            Assert.That(session.CaptureVoyageCheckpoint().treasureExpeditionMarks, Is.Empty);
            Assert.That(session.TreasureAdventureCompleted, Is.False);
        }

        private static readonly PropertyInfo[] CargoProperties = typeof(VoyageCargo).GetProperties()
            .Where(p => p.PropertyType == typeof(int) && p.CanWrite).ToArray();

        private static string State(CoreVoyageSession session) => string.Join("|", new[] {
            string.Join(",", CargoProperties.Select(p => p.Name + "=" + p.GetValue(session.Cargo))),
            session.Hull.ToString(), session.Morale.ToString(), session.TotalHours.ToString("R"),
            session.TreasureExpeditionCredit.ToString(), session.TreasureExpeditionHeat.ToString(), session.TreasureExpeditionResonance.ToString(),
            session.SailingBoostHoursRemaining.ToString("R"), session.SurveyBoostHoursRemaining.ToString("R"),
            session.TreasureAdventureCompleted.ToString(), session.ProfileGold.ToString(),
            string.Join(",", new[] { "weathered_winch", "weathered_bell", "weathered_camp", "gilded_manifest", "gilded_witness",
                "moonlit_dawn", "moonlit_dusk", "moonlit_midnight" }.Where(session.HasTreasureMark)) });
    }
}
