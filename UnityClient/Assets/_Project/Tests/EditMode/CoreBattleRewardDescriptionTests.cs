using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleRewardDescriptionTests
    {
        [Test]
        public void EntryCostsStayUnscaledAndDoctorRoundsMedicineCostUp()
        {
            var effect = new VoyageEffect { Gold = -25, Medicine = -5, Wood = -4, Hull = -7, Morale = -2 };
            string detail = CoreBattleRewardDescriptions.AdditionalEffects(effect, .25d, doctor: true);
            Assert.That(detail, Does.Contain("金币 -25").And.Contain("药品 -4").And.Contain("木材 -4")
                .And.Contain("船体 -7").And.Contain("士气 -2"));
            Assert.That(CoreBattleRewardDescriptions.AdditionalEffects(effect, .25d), Does.Contain("药品 -5"));
        }

        [Test]
        public void AllPositivePhysicalCargoIsRepresentedOnlyByCash()
        {
            var effect = new VoyageEffect {
                Water = 1, Food = 1, Beer = 1, Medicine = 1, Wood = 1, Iron = 1, Cloth = 1, Powder = 1,
                Cannonballs = 1, Gem = 1, AncientMaterial = 1, Relic = 1, BlackPearl = 1, AncientCompass = 1, Lantern = 1,
            };
            Assert.That(CoreBattleRewardDescriptions.AdditionalEffects(effect, doctor: true), Is.Empty);
            Assert.That(CoreBattleRewardDescriptions.Victory(false, effect), Does.Contain("金币 +500"));
        }

        [Test]
        public void ExhaustedChartStillShowsCostsAndUnscaledCluesAndCurse()
        {
            var effect = new VoyageEffect { Gold = -20, Medicine = -4, Hull = -3, Morale = 2, Reveal = 2, TreasureClues = 2, Curse = 1 };
            string detail = CoreBattleRewardDescriptions.AdditionalEffects(effect, 0d, doctor: true);
            Assert.That(detail, Does.Contain("金币 -20").And.Contain("药品 -3").And.Contain("船体 -3")
                .And.Contain("藏宝线索 +2").And.Contain("诅咒 +1"));
            Assert.That(detail, Does.Not.Contain("士气").And.Not.Contain("情报"));
            Assert.That(CoreBattleRewardDescriptions.Victory(false, effect, 0d), Does.Contain("金币 +0").And.Contain("声望 +0"));
        }

        [Test]
        public void AuthoredFragmentsKeepExistingCaptureAndChartScaling()
        {
            string detail = CoreBattleRewardDescriptions.AdditionalEffects(new VoyageEffect { MapFragments = 3 }, .5d);
            Assert.That(detail, Does.Contain("藏宝图碎片 +1／夺船 +2"));
        }
    }
}
