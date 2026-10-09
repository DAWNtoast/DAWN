using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreLanternRecoveryTests
    {
        [TestCase("weathered", VoyageOutcome.SafeReturn)]
        [TestCase("gilded", VoyageOutcome.SafeReturn)]
        [TestCase("moonlit", VoyageOutcome.SafeReturn)]
        [TestCase("weathered", VoyageOutcome.TreasureRecovered)]
        [TestCase("gilded", VoyageOutcome.TreasureRecovered)]
        [TestCase("moonlit", VoyageOutcome.TreasureRecovered)]
        [TestCase("weathered", VoyageOutcome.Failure)]
        [TestCase("gilded", VoyageOutcome.Failure)]
        [TestCase("moonlit", VoyageOutcome.Failure)]
        public void ActualFogbellReward_SurvivesSettlementStorageAndReload(string rank, VoyageOutcome outcome)
        {
            CoreVoyageSession session = StartWithFogbell(rank);
            Assert.That(session.Cargo.Lantern, Is.Zero, "The fixture must earn its first lamp, not inject one.");
            CoreMapNode fogbell = session.Map.Nodes.Single(n => n.Encounter.Id == "treasure_clue_fogbell");
            Choose(session, fogbell, 1);
            Assert.That(fogbell.Visited, Is.True);
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1));
            Assert.That(session.ResolveChoice(1), Is.False, "A completed event cannot award the lamp twice.");

            if (outcome == VoyageOutcome.TreasureRecovered)
            {
                foreach (CoreMapNode clue in session.Map.Nodes.Where(n => n.Encounter.Category == "线索" && !n.Visited).ToArray())
                {
                    if (session.TreasureClues >= session.TreasureRequiredClues) break;
                    Choose(session, clue, -1);
                }
                Assert.That(session.TreasureClues, Is.GreaterThanOrEqualTo(session.TreasureRequiredClues));
                Choose(session, session.Map.Nodes.Single(n => n.Encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId), 0);
            }
            else
            {
                // Exercise the normal rescue branch for a voyage that runs out
                // of water; do not call private settlement or change its ratio.
                if (outcome == VoyageOutcome.Failure) session.Cargo.Water = 0;
                Assert.That(session.SafeReturn(), Is.True);
            }
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Settlement.Outcome, Is.EqualTo(outcome));
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1), "Indivisible rare loot must not lose its only unit to rounding.");
            session.ReturnToPortAfterSettlement();
            session.ReturnToPortAfterSettlement();
            Assert.That(session.Cargo.Lantern, Is.EqualTo(1));
            Assert.That(session.TryDepositPortCargo("lantern", 1), Is.True);
            Assert.That(session.Cargo.Lantern, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "lantern"), Is.EqualTo(1));

            var restored = new CoreVoyageSession();
            restored.RestoreProfile(session.ProfileGold, session.CurrentPort);
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.PortWarehouse.Restore(session.PortWarehouse.Serialize()), Is.True);
            Assert.That(restored.Cargo.Lantern, Is.Zero);
            Assert.That(restored.PortWarehouse.GetAmount(restored.CurrentPort, "lantern"), Is.EqualTo(1));
        }

        // Arrange maps, experience, navigator and supplies only. Rewards, costs,
        // settlement and transfers all use the production APIs. Jumping between
        // generated nodes isolates this regression from navigation and combat.
        private static CoreVoyageSession StartWithFogbell(string rank)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var session = new CoreVoyageSession();
                session.ConfigureCrewCapabilities(true, false, false, false, false, false);
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = 1400, selectedMapId = "lantern-recovery",
                    maps = new[] { new CoreTreasureMapSave { id = "lantern-recovery", rankId = rank, seed = "LAMP-AUDIT-" + seed } }
                });
                session.Cargo.Water = session.Cargo.Food = 150;
                session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Beer = 0;
                session.Cargo.Medicine = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
                Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
                if (session.Map.Nodes.Any(n => n.Encounter.Id == "treasure_clue_fogbell")) return session;
            }
            throw new InvalidOperationException("Could not find a generated fogbell map for " + rank);
        }

        private static void Choose(CoreVoyageSession session, CoreMapNode node, int choice)
        {
            Assert.That(node.Visited, Is.False);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            // Some basic clue options now require night or dusk. Use an
            // available clue route, retaining its real cost and reward.
            if (choice < 0)
                choice = Enumerable.Range(0, node.Encounter.Choices.Count).First(index =>
                    node.Encounter.Choices[index].Effect.TreasureClues > 0 && session.IsChoiceAvailable(index));
            Assert.That(session.ResolveChoice(choice), Is.True, session.ChoiceUnavailableReason(choice));
        }
    }
}
