using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class SettlementChestRewardPlanTests
    {
        [Test]
        public void PlanContainsOnlyActualPositiveRewards_AndPreservesExactAmounts()
        {
            var result = new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn, BankedGold = 0,
                BankedWood = 7, BankedIron = -2, BankedFame = 3, BankedFragments = 2,
                BankedPirateExperience = 15, BankedGem = 4, BankedAncientMaterial = 5,
                BankedRelic = 1, BankedBlackPearl = 6 };
            var plan = SettlementChestRewardPlan.Create(result);
            Assert.That(plan.Items.Count, Is.EqualTo(8));
            Assert.That(plan.Items.All(item => item.Amount > 0), Is.True);
            Assert.That(plan.Items.Any(item => item.Kind == SettlementChestRewardKind.Gold || item.Kind == SettlementChestRewardKind.Iron), Is.False);
            Assert.That(plan.Items.Single(item => item.Kind == SettlementChestRewardKind.Wood).Amount, Is.EqualTo(7));
            Assert.That(plan.Items.Single(item => item.Kind == SettlementChestRewardKind.PirateExperience).Amount, Is.EqualTo(15));
            Assert.That(plan.Items.Select(item => item.Kind).Distinct().Count(), Is.EqualTo(plan.Items.Count));
            Assert.That(result.BankedIron, Is.EqualTo(-2), "Presentation must not normalize or mutate earned data.");
        }

        [Test]
        public void LargestValuedRewardIsTheFinalReveal_AndOnlyThatItemIsJackpot()
        {
            var plan = SettlementChestRewardPlan.Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn,
                BankedGold = 20000, BankedWood = 11, BankedIron = 3, BankedFame = 9, BankedGem = 1 });
            Assert.That(plan.IsJackpot, Is.True);
            Assert.That(plan.Items.Last().ValueWeight, Is.EqualTo(plan.Items.Max(item => item.ValueWeight)));
            Assert.That(plan.Items.Last().Kind, Is.EqualTo(SettlementChestRewardKind.Gold));
            Assert.That(plan.Items.Last().IsJackpot, Is.True);
            Assert.That(plan.Items.Count(item => item.IsJackpot), Is.EqualTo(1));
        }

        [Test]
        public void OrdinaryRewardKeepsStableResourceOrder_WithoutJackpot()
        {
            var plan = SettlementChestRewardPlan.Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn,
                BankedGold = 10, BankedWood = 1 });
            Assert.That(plan.IsJackpot, Is.False);
            Assert.That(plan.Items.Select(item => item.Kind).ToArray(), Is.EqualTo(new[] { SettlementChestRewardKind.Gold, SettlementChestRewardKind.Wood }));
            Assert.That(plan.Items.All(item => !item.IsJackpot), Is.True);
        }

        [Test]
        public void FailedVoyageNeverCelebratesEvenWhenSalvageIsHuge()
        {
            var failed = SettlementPreviewSamples.Create(2);
            failed.BankedGold = 200000;
            failed.BankedGem = 999;
            var plan = SettlementChestRewardPlan.Create(failed);
            Assert.That(plan.IsFailure, Is.True);
            Assert.That(plan.HasRewards, Is.True);
            Assert.That(plan.IsJackpot, Is.False);
            Assert.That(plan.Items.All(item => !item.IsJackpot), Is.True);
        }

        [Test]
        public void EmptyAndNullResultsDoNotInventLoot()
        {
            Assert.That(SettlementChestRewardPlan.Create(null).HasRewards, Is.False);
            var plan = SettlementChestRewardPlan.Create(new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn });
            Assert.That(plan.HasRewards, Is.False);
            Assert.That(plan.IsJackpot, Is.False);
            Assert.That(plan.Items, Is.Empty);
        }

        [Test]
        public void HugeRewardsKeepIntAmounts_AndUseLongValuationWithoutOverflow()
        {
            var result = new VoyageSettlement { Outcome = VoyageOutcome.SafeReturn,
                BankedGold = int.MaxValue, BankedGem = int.MaxValue, BankedRelic = int.MaxValue };
            var plan = SettlementChestRewardPlan.Create(result);
            Assert.That(plan.Items.All(item => item.Amount == int.MaxValue), Is.True);
            Assert.That(plan.TotalValue, Is.GreaterThan((long)int.MaxValue));
            Assert.That(plan.TotalValue, Is.EqualTo(plan.Items.Sum(item => item.ValueWeight)));
            Assert.That(result.BankedGold, Is.EqualTo(int.MaxValue));
        }
    }
}
