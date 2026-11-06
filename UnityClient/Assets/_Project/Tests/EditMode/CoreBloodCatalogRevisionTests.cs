using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBloodCatalogRevisionTests
    {
        [TestCase("card_school_blood_b01", "获得5点格挡；消耗5点船体额外获得8点格挡。")]
        [TestCase("card_school_blood_b02", "获得8点格挡；消耗3点船体抽1张牌。")]
        [TestCase("card_school_blood_b03", "范围1-4格：造成10点船体伤害；消耗5点船体，额外造成14点伤害。")]
        [TestCase("card_school_blood_b04", "范围1-4格：造成5点船体伤害；吸血。")]
        [TestCase("card_school_blood_b05", "获得5点格挡；消耗5点船体额外获得8点格挡。")]
        [TestCase("card_school_blood_b06", "范围1-4格：造成20点船体伤害；敌方生命不高于35%时，伤害提高10点。")]
        [TestCase("card_school_blood_n01", "范围1-4格：铺设持续时间为5回合的2×2血池。")]
        [TestCase("card_school_blood_n02", "范围1-4格：施加2回合禁疗。")]
        [TestCase("card_school_blood_n03", "范围1-4格：造成35点炮击伤害；绝境：消耗5点船体额外造成16点伤害。")]
        [TestCase("card_boarding_blood_05", "获得2点能量。")]
        [TestCase("card_boarding_blood_06", "造成18点伤害；吸血。")]
        [TestCase("card_boarding_blood_08", "直到下下个敌方回合结束，敌方对我方造成伤害时，对其造成16点伤害。")]
        [TestCase("card_boarding_blood_09", "恢复2点生命值，绝境：改为恢复5点生命值；消耗1药物额外恢复10点生命值，绝境：改为额外恢复21点生命值。")]
        [TestCase("card_boarding_blood_11", "造成14点伤害；绝境：额外造成20点伤害。")]
        [TestCase("card_boarding_blood_12", "获得10点格挡；绝境：改为获得24点格挡。")]
        [TestCase("card_boarding_blood_13", "延迟：造成24点伤害；绝境：改为造成44点伤害。")]
        [TestCase("card_boarding_blood_14", "绝境时才可使用；获得1点能量，抽1张牌，使用后移出本场战斗。")]
        [TestCase("card_boarding_blood_15", "绝境时才可使用：直到下个我方回合结束，攻击牌额外造成16点伤害。")]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", "恢复2点生命值，绝境：改为恢复4点生命值；消耗1药物额外恢复12点生命值，绝境：改为额外恢复28点生命值。")]
        public void RevisedBaseEffectsMatchTheManuscript(string id, string expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description), Is.EqualTo(expected));
        }

        [TestCase("card_school_blood_b01", "获得5点格挡", "额外获得8点格挡")]
        [TestCase("card_school_blood_b02", "获得8点格挡", "抽1张牌")]
        [TestCase("card_school_blood_b03", "造成10点船体伤害", "额外造成14点伤害")]
        [TestCase("card_school_blood_b05", "获得5点格挡", "额外获得8点格挡")]
        public void CollectionFaceDoesNotCountOptionalBloodBenefitsTwice(string id, string basis, string bonus)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), 0);
            NavalBattleStage stage = card.IsAllowedInStage(NavalBattleStage.Preparation) ? NavalBattleStage.Preparation : NavalBattleStage.Naval;
            string text = Compact(session.BattleCardFaceDescription(card, stage));
            Assert.That(text, Does.Contain(basis).And.Contain(bonus));
        }

        [TestCase("card_special_bow_deepcurrent_fang")]
        [TestCase("card_special_bow_freewake_lance")]
        [TestCase("card_special_instrument_allseeing_sextant")]
        public void ExpansionAlwaysExtendsAllFriendlyBloodPoolsOneStep(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1));
                Assert.That(card.SchoolEffect.Secondary, Is.Zero);
                Assert.That(Compact(card.Description), Does.Contain("将所有己方血池向上下左右各延伸1格")
                    .And.Not.Contain("延长").And.Not.Contain("4格内").And.Not.Contain("最多新增"));
            }
        }

        [TestCase("card_special_sail_fogshroud_wing", 2)]
        [TestCase("card_special_sail_thunderreef_wing", 5)]
        public void SailsExtendAllFriendlyBloodPools(string id, int rounds)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), 0);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(rounds));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方血池延长" + rounds + "回合"));
        }

        [Test]
        public void OrdinaryDrainHasNoPercentageReductionOrHealingCap()
        {
            var session = new CoreVoyageSession();
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel("card_school_blood_b04"); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(Index("card_school_blood_b04"), rank);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(100));
                Assert.That(card.SchoolEffect.Limit, Is.Zero);
                Assert.That(card.Description, Does.Contain("吸血").And.Not.Contain("上限"));
            }
        }

        [TestCase("card_boarding_blood_09", 5, 12, 26)]
        [TestCase("card_special_crew_broken_anchor_doctor_hector", 4, 14, 32)]
        public void HealingRanksKeepFallbacksAndScaleTheWholeMedicineBenefit(string id, int desperateBase, int normalTotal, int desperateTotal)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain("恢复2点生命值，绝境：改为恢复" + desperateBase + "点生命值"));
                Assert.That(text, Does.Contain("额外恢复" + (CoreCardUpgradeDesign.ScaleEffect(card, normalTotal, rank) - 2) + "点生命值"));
                Assert.That(text, Does.Contain("额外恢复" + (CoreCardUpgradeDesign.ScaleEffect(card, desperateTotal, rank) - desperateBase) + "点生命值"));
            }
        }

        [Test]
        public void EveryPublishedRankHasUsefulGrowthAndNoRetiredBloodRules()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                if (basis == null || basis.SchoolId != "blood" || basis.AllowedStages == NavalBattleStages.None ||
                    CoreBattleCardAliases.IsAlias(basis.Id) || (basis.AllowedStages == NavalBattleStages.Boarding &&
                    basis.RequiredCrew.Length == 0 && basis.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(basis))) continue;
                count++;
                string previous = null;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                    Assert.That(text, Is.Not.EqualTo(previous), basis.Id + " +" + rank);
                    Assert.That(Regex.IsMatch(text, "血誓阵|恢复减半|吸血，最多|每回合合计只能|每场战斗合计只能|每回合第一张|最多新增"),
                        Is.False, basis.Id + " +" + rank + ": " + text);
                    previous = text;
                }
            }
            Assert.That(count, Is.EqualTo(36));
        }

        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
        private static int Index(string id)
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == id) return index;
            throw new ArgumentException(id);
        }
    }
}
