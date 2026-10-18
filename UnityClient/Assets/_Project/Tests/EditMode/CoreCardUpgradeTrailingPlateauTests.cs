using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardUpgradeTrailingPlateauTests
    {
        [Test]
        public void GhostSail_GrowsShieldTwiceAndStopsAtItsAuthoredMaximum()
        {
            const string id = "card_school_curse_b06";
            var session = new CoreVoyageSession();
            session.RestoreProfile(10000, session.CurrentPort);
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            int index = Find(id);
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeCost(index), Is.EqualTo(120));
            Assert.That(session.TryUpgradeBattleCard(index, out string firstReason), Is.True, firstReason);
            Assert.That(session.GetBattleCard(index).Draw, Is.EqualTo(1));
            Assert.That(session.GetBattleCard(index).Block, Is.EqualTo(2));
            Assert.That(session.GetBattleCard(index).SchoolEffect.Power, Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeCost(index), Is.EqualTo(240));
            Assert.That(session.TryUpgradeBattleCard(index, out string secondReason), Is.True, secondReason);
            CoreBattleCard maximum = session.GetBattleCard(index);
            Assert.That(maximum.UpgradeLevel, Is.EqualTo(2));
            Assert.That(maximum.Block, Is.EqualTo(3));
            Assert.That(maximum.SchoolEffect.Power, Is.EqualTo(3));
            Assert.That(maximum.Draw, Is.EqualTo(1));
            Assert.That(session.ProfileGold, Is.EqualTo(9640));
            Assert.That(session.GetCardUpgradeCost(index), Is.Zero);
            Assert.That(session.TryUpgradeBattleCard(index, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(9640), "The redundant third rank must not charge another 480 gold.");
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(2));
        }

        [Test]
        public void GhostSail_HistoricalThirdRankSurvivesWhileTheEffectiveRankStopsAtTwo()
        {
            const string id = "card_school_curse_b06";
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, 3 } });
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(3));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(2));
            Assert.That(session.GetBattleCardAtLevel(Find(id), 3).UpgradeLevel, Is.EqualTo(2));
            session.ConfigureCardUpgrades(session.CardUpgradeLevels);
            Assert.That(session.CardUpgradeLevels[id], Is.EqualTo(3));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(2));
            Assert.That(session.GetCardUpgradeCost(Find(id)), Is.Zero);
        }

        [TestCase("card_navigator_course", 2, 3, 2)]
        [TestCase("card_component_sail", 1, 2, 1)]
        public void SourceCards_KeepEarnedExperienceAfterTheirLastUsefulCombatRank(string id, int maximum, int block, int draw)
        {
            var session = new CoreVoyageSession();
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { id, 840 } });
            Assert.That(session.GetCardMaximumUpgradeLevel(id), Is.EqualTo(maximum));
            Assert.That(session.GetCardUpgradeLevel(id), Is.EqualTo(maximum));
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(840));
            Assert.That(session.GetSpecialCardNextThreshold(id), Is.Zero);
            CoreBattleCard grown = session.GetBattleCard(Find(id));
            Assert.That(grown.UpgradeLevel, Is.EqualTo(maximum));
            Assert.That(grown.Block, Is.EqualTo(block));
            Assert.That(grown.Draw, Is.EqualTo(draw));
            session.RestoreSpecialCardExperience(session.SpecialCardExperience);
            Assert.That(session.GetSpecialCardExperience(id), Is.EqualTo(840));
            Assert.That(session.GetCardUpgradeCost(Find(id)), Is.Zero);
        }

        [TestCase("damage", 1, 2)] [TestCase("damage", 2, 3)]
        [TestCase("block", 1, 2)] [TestCase("block", 2, 3)]
        [TestCase("repair", 1, 2)] [TestCase("repair", 2, 3)]
        [TestCase("boardingBlock", 1, 2)] [TestCase("boardingBlock", 2, 3)]
        public void NumericalCaps_TrimOnlyTheFinalPlateauAndKeepIntermediateFlatRanks(string field, int baseline, int maximum)
        {
            var card = new CoreBattleCard("plateau_" + field, "末阶平台", "测试", 1,
                field == "damage" ? baseline : 0, field == "block" ? baseline : 0,
                field == "repair" ? baseline : 0, 0, 0, "数值成长",
                boardingBlock: field == "boardingBlock" ? (int?)baseline : null);
            int[] growth = Enumerable.Range(0, 4).Select(rank => CoreBattleUpgradeBalance.ScaleNumerical(baseline, rank)).ToArray();
            Assert.That(growth, Is.EqualTo(baseline == 1 ? new[] { 1, 1, 2, 2 } : new[] { 2, 2, 3, 4 }));
            Assert.That(CoreBattleUpgradeBalance.MaximumLevelFor(card), Is.EqualTo(maximum));
        }

        [TestCase(1, 2)] [TestCase(2, 3)]
        public void AuthoredSchoolPower_UsesTheSameTrailingPlateauRule(int baseline, int maximum)
        {
            var card = new CoreBattleCard("school_plateau", "系别末阶平台", "策略", 1, 0, 0, 0, 0, 0, "每层 {schoolPower} 格挡",
                schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.CurseWard, power: baseline,
                    upgrades: CoreSchoolUpgradeFields.Power));
            Assert.That(CoreBattleUpgradeBalance.MaximumLevelFor(card), Is.EqualTo(maximum));
            Assert.That(card.SchoolEffect.AtLevel(1).Power, Is.EqualTo(baseline), "An unchanged intermediate rank does not erase later growth.");
            Assert.That(card.SchoolEffect.AtLevel(maximum).Power, Is.EqualTo(baseline * 2));
        }

        private static int Find(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
    }
}
