using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // SafeReturn pays two water and one food immediately. Public sailing
        // forecasts must preserve that exit even when their own cost is payable;
        // opening a known vault needs its separately quoted rations as well.
        private static bool StrictCanFundRouteAndReturn(int water, int food, int routeWater, int routeFood, int openingRations) =>
            routeWater >= 0 && routeFood >= 0 && openingRations >= 0 &&
            (long)routeWater + openingRations + 2 <= water &&
            (long)routeFood + openingRations + 1 <= food;

        [TestCase(6, 29, 5, 5, 0, false)] // NoGm03 cycle5, seq2128: otherwise leaves water1.
        [TestCase(4, 25, 4, 4, 0, false)] // NoGm03 cycle7, seq2963: otherwise leaves water0.
        [TestCase(7, 6, 5, 5, 0, true)]
        [TestCase(6, 6, 5, 5, 0, false)]
        [TestCase(7, 5, 5, 5, 0, false)]
        [TestCase(10, 9, 5, 5, 3, true)]
        [TestCase(9, 9, 5, 5, 3, false)]
        [TestCase(10, 8, 5, 5, 3, false)]
        public void StrictTreasurePolicy_RouteBudgetPreservesReturnAfterSailingAndCoreOpening(
            int water, int food, int routeWater, int routeFood, int openingRations, bool affordable)
        {
            Assert.That(StrictCanFundRouteAndReturn(water, food, routeWater, routeFood, openingRations), Is.EqualTo(affordable));
        }

        // Inputs are only the observed full-vault requirements and the public
        // expedition counters. This helper never accesses or changes a map.
        private static double StrictTreasureProgressScore(CoreEncounterChoice objective, VoyageEffect effect,
            Func<string, bool> hasMark, int credit, int heat, int resonance, int curse)
        {
            if (objective == null || effect == null) return 0d;
            string[] requiredMarks = (objective.RequiredTreasureMark ?? string.Empty).Split('+');
            double score = !string.IsNullOrEmpty(effect.TreasureMark) && requiredMarks.Contains(effect.TreasureMark) &&
                !hasMark(effect.TreasureMark) ? 100000d : 0d;
            score += Math.Min(Math.Max(0, objective.MinimumTreasureCredit - credit), Math.Max(0, effect.TreasureCredit)) * 10000d;
            score += Math.Min(Math.Max(0, heat - objective.MaximumTreasureHeat), Math.Max(0, -effect.TreasureHeat)) * 10000d;
            score += Math.Min(Math.Max(0, objective.MinimumTreasureResonance - resonance), Math.Max(0, effect.TreasureResonance)) * 10000d;
            score += Math.Min(Math.Max(0, curse - objective.MaximumCurse), Math.Max(0, -effect.Curse)) * 10000d;
            return score;
        }

        [TestCase("weathered", "weathered_winch")]
        [TestCase("weathered", "weathered_bell")]
        [TestCase("gilded", "gilded_manifest")]
        [TestCase("moonlit", "moonlit_dawn")]
        [TestCase("moonlit", "moonlit_dusk")]
        [TestCase("moonlit", "moonlit_midnight")]
        public void StrictTreasurePolicy_MissingPublicCoreMarkIsAnObjectiveWithoutLegacyClues(string rank, string mark)
        {
            CoreEncounterChoice objective = StrictPolicyCore(rank);
            var effect = new VoyageEffect { TreasureMark = mark };
            Assert.That(StrictTreasureProgressScore(objective, effect, _ => false, 0, 0, 0, 0), Is.EqualTo(100000d));
            Assert.That(StrictTreasureProgressScore(objective, effect, _ => true, 0, 0, 0, 0), Is.Zero);
        }

        [Test]
        public void StrictTreasurePolicy_GildedCreditAndHeatStopAtTheFullVaultThreshold()
        {
            CoreEncounterChoice objective = StrictPolicyCore("gilded");
            var credit = new VoyageEffect { TreasureCredit = 3 };
            var clearHeat = new VoyageEffect { TreasureHeat = -5 };
            Assert.That(StrictTreasureProgressScore(objective, credit, _ => true, 2, 0, 0, 0), Is.EqualTo(10000d));
            Assert.That(StrictTreasureProgressScore(objective, credit, _ => true, 3, 0, 0, 0), Is.Zero);
            Assert.That(StrictTreasureProgressScore(objective, clearHeat, _ => true, 3, 3, 0, 0), Is.EqualTo(20000d));
            Assert.That(StrictTreasureProgressScore(objective, clearHeat, _ => true, 3, 1, 0, 0), Is.Zero);
        }

        [Test]
        public void StrictTreasurePolicy_MoonlitCleanseTargetsOnlyTheMissingCleanlinessRequirement()
        {
            CoreEncounterChoice objective = StrictPolicyCore("moonlit");
            var cleanse = new VoyageEffect { Curse = -3 };
            Assert.That(StrictTreasureProgressScore(objective, cleanse, _ => true, 0, 0, 3, 3), Is.EqualTo(20000d));
            Assert.That(StrictTreasureProgressScore(objective, cleanse, _ => true, 0, 0, 3, 1), Is.Zero);
            Assert.That(StrictTreasureProgressScore(objective, new VoyageEffect { TreasureMark = "weathered_camp" },
                _ => false, 0, 0, 3, 0), Is.Zero);
        }

        private static CoreEncounterChoice StrictPolicyCore(string rank) => CoreTreasureExpeditionCatalog
            .CreateEncounters(CoreTreasureHuntCatalog.FindRank(rank))
            .Single(encounter => encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId).Choices[0];
    }
}
