using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTreasureExpeditionCatalogTests
    {
        [Test]
        public void EveryExpeditionSideEvent_HasAnExplicitDiscoveryRuleMatchingItsAuthoredVisibility()
        {
            var sides = CoreTreasureHuntCatalog.Ranks.SelectMany(CoreTreasureExpeditionCatalog.CreateEncounters)
                .Where(e => e.Id != CoreTreasureHuntCatalog.CoreEncounterId).ToArray();
            Assert.That(sides.Length, Is.EqualTo(36));
            foreach (CoreEncounter encounter in sides)
            {
                Assert.That(CoreTreasureExpeditionCatalog.TryClassifyDiscovery(encounter.Id, out EncounterDiscoveryKind kind),
                    Is.True, encounter.Id + " must explicitly choose a disclosure type.");
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(encounter.Id, out EncounterDiscoveryKind registered), Is.True);
                Assert.That(registered, Is.EqualTo(kind), encounter.Id);
                Assert.That(encounter.DiscoveryKind, Is.EqualTo(kind), encounter.Id);
                Assert.That(encounter.Hidden, Is.EqualTo(kind == EncounterDiscoveryKind.HiddenLocation), encounter.Id);
                Assert.That(encounter.HasChartLocation, Is.True, encounter.Id);
                Assert.That(encounter.CanRevealByIntel, Is.EqualTo(encounter.Hidden), encounter.Id);
                Assert.That(CoreEncounterDiscoveryRules.Classify(encounter.Id, !encounter.Hidden), Is.EqualTo(kind),
                    "A stale hidden flag must not override the authored rule: " + encounter.Id);
            }
            Assert.That(CoreTreasureExpeditionCatalog.TryClassifyDiscovery("weathered_unregistered_custom_event", out _), Is.False,
                "A matching prefix is not an explicit authored rule.");
        }

        [Test]
        public void ThreeExpeditions_DoNotShareSideEventsOrCoreStories()
        {
            var catalogs = CoreTreasureHuntCatalog.Ranks.Select(CoreTreasureExpeditionCatalog.CreateEncounters).ToArray();
            var sides = catalogs.SelectMany(events => events.Where(e => e.Id != CoreTreasureHuntCatalog.CoreEncounterId)).ToArray();
            Assert.That(sides.Length, Is.EqualTo(36));
            Assert.That(sides.Select(e => e.Id).Distinct().Count(), Is.EqualTo(sides.Length), "No shared side-event IDs across ranks.");
            Assert.That(sides.Select(e => e.Name).Distinct().Count(), Is.EqualTo(sides.Length), "No renamed-copy experiences hidden behind the same player-facing title.");
            var cores = catalogs.Select(events => events.Single(e => e.Id == CoreTreasureHuntCatalog.CoreEncounterId)).ToArray();
            Assert.That(cores.Select(e => e.Name).Distinct().Count(), Is.EqualTo(3));
            Assert.That(cores.Select(e => e.Description).Distinct().Count(), Is.EqualTo(3));
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void GuaranteedLandmarks_ProvideEveryRequiredMarkAndFunctionalExplorationBenefit(string rankId)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.FindRank(rankId);
            var events = CoreTreasureExpeditionCatalog.CreateEncounters(rank);
            var sides = events.Where(e => e.Id != CoreTreasureHuntCatalog.CoreEncounterId).ToArray();
            var guaranteedIds = CoreTreasureExpeditionCatalog.GuaranteedEventIds(rankId);
            var guaranteed = guaranteedIds.Select(id => sides.Single(e => e.Id == id)).ToArray();
            Assert.That(sides.Length, Is.EqualTo(12));
            Assert.That(sides.All(e => e.Id.StartsWith(rankId + "_", StringComparison.Ordinal)), Is.True);
            Assert.That(guaranteedIds.Distinct().Count(), Is.EqualTo(guaranteedIds.Count));
            Assert.That(guaranteedIds.Count, Is.LessThan(rank.SidePointCount), "Keep room for a different optional-event combination.");
            Assert.That(rank.SidePointCount, Is.LessThan(sides.Length));
            Assert.That(guaranteed.All(e => !e.Hidden), Is.True, "Players must be able to find mechanism-critical landmarks.");
            Assert.That(sides.Count(e => e.Hidden), Is.GreaterThanOrEqualTo(3), "Survey and investigation must still have undiscovered optional sites to find.");
            Assert.That(sides.Select(e => e.Category).Distinct().Count(), Is.GreaterThanOrEqualTo(6));
            var providedMarks = guaranteed.SelectMany(e => e.Choices).Select(c => c.Effect.TreasureMark)
                .Where(mark => !string.IsNullOrEmpty(mark)).Distinct().ToArray();
            var requiredMarks = events.SelectMany(e => e.Choices).SelectMany(c => c.RequiredTreasureMark.Split('+'))
                .Where(mark => !string.IsNullOrEmpty(mark)).Distinct().ToArray();
            Assert.That(requiredMarks.All(mark => providedMarks.Contains(mark)), Is.True,
                "Every tool, proof or ritual needed by an authored branch must exist in every generated map.");
            Assert.That(guaranteed.SelectMany(e => e.Choices).Any(c => c.Effect.SailingBoostHours > 0 || c.Effect.SurveyBoostHours > 0), Is.True,
                "Mechanisms should help this voyage before its final payout.");
        }

        [TestCase("weathered")]
        [TestCase("gilded")]
        [TestCase("moonlit")]
        public void EachCore_HasAnUnconditionalNoncombatRecoveryAndAZeroHourReturn(string rankId)
        {
            CoreEncounter core = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(rankId))
                .Single(e => e.Id == CoreTreasureHuntCatalog.CoreEncounterId);
            Assert.That(core.Choices.Any(c => c.Effect.TreasureComplete && !c.StartsBattle && c.RequiredCrew.Length == 0 &&
                c.RequiredTreasureMark.Length == 0 && c.RequiredTreasureClues == 0 && c.MinimumTreasureCredit == 0 &&
                c.MinimumTreasureResonance == 0 && c.MinimumCurse == 0 && c.MaximumCurse == int.MaxValue &&
                c.MaximumTreasureHeat == 5 && c.RequiredTime == CoreVoyageTimeBand.Any && c.Effect.Gold >= 0 &&
                c.Effect.Wood >= 0 && c.Effect.Iron >= 0 && c.Effect.Cloth >= 0 && c.Effect.Powder >= 0), Is.True,
                "A spent credit balance, missing tools or missed timing must not force combat or trap the player.");
            Assert.That(core.Choices.Any(c => c.Hours == 0 && c.Effect.PostponeTreasureCore && !c.Effect.TreasureComplete), Is.True);
        }

        [Test]
        public void FullRewards_RepresentEngineeringTradeAndRitualInsteadOfOneScaledBundle()
        {
            VoyageEffect Full(string id) => CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank(id))
                .Single(e => e.Id == CoreTreasureHuntCatalog.CoreEncounterId).Choices[0].Effect;
            VoyageEffect engineering = Full("weathered"), trade = Full("gilded"), ritual = Full("moonlit");
            Assert.That(engineering.AncientMaterial > 0 && engineering.AncientCompass > 0 && engineering.Lantern > 0, Is.True);
            Assert.That(engineering.Relic + engineering.BlackPearl + engineering.Gem, Is.Zero);
            Assert.That(trade.Gold > 0 && trade.Gem > 0 && trade.Medicine > 0, Is.True);
            Assert.That(trade.AncientMaterial + trade.AncientCompass + trade.Relic, Is.Zero);
            Assert.That(ritual.Relic > 0 && ritual.BlackPearl > 0, Is.True);
            Assert.That(ritual.Gold + ritual.Gem + ritual.Medicine, Is.Zero);
        }

        [Test]
        public void GuildGuarantees_OfferEnoughResourceFreeCreditAndAResourceFreeHeatReset()
        {
            var events = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("gilded"));
            var guaranteed = CoreTreasureExpeditionCatalog.GuaranteedEventIds("gilded").Select(id => events.Single(e => e.Id == id));
            int safeCredit = guaranteed.Sum(e => e.Choices.Where(c => c.RequiredCrew.Length == 0 &&
                c.RequiredTreasureMark.Length == 0 && c.Effect.Gold >= 0 && c.Effect.Food >= 0 && c.Effect.Water >= 0 &&
                c.Effect.TreasureHeat <= 0).Select(c => c.Effect.TreasureCredit).DefaultIfEmpty(0).Max());
            Assert.That(safeCredit, Is.GreaterThanOrEqualTo(6), "Legal departure plus an optional purchase must be possible without random-event luck.");
            Assert.That(guaranteed.SelectMany(e => e.Choices).Any(c => c.RequiredCrew.Length == 0 &&
                c.RequiredTreasureMark.Length == 0 && c.Effect.Gold == 0 && c.Effect.TreasureHeat <= -5), Is.True);
        }

        [Test]
        public void RitualMarksAndResonance_CanOnlyBeEarnedTogetherAtThreeDistinctAltars()
        {
            var events = CoreTreasureExpeditionCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("moonlit"));
            string[] altarIds = { "moonlit_dawn", "moonlit_dusk", "moonlit_midnight" };
            foreach (string id in altarIds)
            {
                CoreEncounter altar = events.Single(e => e.Id == id);
                Assert.That(altar.Choices[0].RequiredTime, Is.Not.EqualTo(CoreVoyageTimeBand.Any));
                Assert.That(altar.Choices[0].Effect.Curse, Is.Zero);
                Assert.That(altar.Choices[1].RequiredTime, Is.EqualTo(CoreVoyageTimeBand.Any));
                Assert.That(altar.Choices[1].Effect.Curse, Is.EqualTo(1));
                Assert.That(altar.Choices.Take(2).All(c => c.Effect.TreasureMark == id && c.Effect.TreasureResonance == 1), Is.True);
                Assert.That(altar.Choices[2].Hours, Is.EqualTo(3));
                Assert.That(altar.Choices[2].Effect.PostponeTreasureCore, Is.True);
                Assert.That(altar.Choices[2].Effect.TreasureResonance, Is.Zero);
                Assert.That(altar.Choices[2].Effect.TreasureMark, Is.Null.Or.Empty);
            }
            Assert.That(events.Where(e => !altarIds.Contains(e.Id)).SelectMany(e => e.Choices)
                .All(c => c.Effect.TreasureResonance == 0), Is.True,
                "Optional rewards cannot manufacture or consume resonance independently of saved altar marks.");
        }
    }
}
