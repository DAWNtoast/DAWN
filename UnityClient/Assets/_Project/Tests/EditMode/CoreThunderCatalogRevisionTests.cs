using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreThunderCatalogRevisionTests
    {
        [TestCase("card_school_thunder_b03", "范围1-4格：造成8点船体伤害；连锁1：额外造成4点伤害。")]
        [TestCase("card_school_thunder_b04", "范围1-4格：造成15点船体伤害；出牌前每有一层连锁，本次伤害提高3点。")]
        [TestCase("card_school_thunder_b06", "范围1-4格：造成10+1X点船体伤害；结算后清空连锁，出牌时处于雷栅则不清空。")]
        [TestCase("card_school_thunder_n01", "范围1-4格：铺设持续时间为5回合的7×7雷栅。")]
        [TestCase("card_school_thunder_n02", "范围1-4格：造成6点炮击伤害；连锁2：额外造成12点伤害。")]
        [TestCase("card_school_thunder_n05", "抽2张牌；本回合第一次使用雷霆牌对敌方造成伤害时，获得1点能量。")]
        [TestCase("card_school_thunder_n06", "范围1-4格：选择含己方雷栅的2×2区域，消耗其中己方雷栅；区域内双方受到18+8X+4×移除雷栅格数点伤害；结算后清空连锁，出牌时处于雷栅则不清空。")]
        [TestCase("card_boarding_thunder_07", "连锁2：抽1张牌。")]
        [TestCase("card_boarding_thunder_09", "连锁2：获得2点能量。")]
        [TestCase("card_boarding_thunder_11", "造成6点伤害2次，出牌前每有一层连锁，每次伤害提高3点。")]
        [TestCase("card_boarding_thunder_12", "造成10点伤害；连锁2：敌方下一次攻击伤害降低20点。")]
        [TestCase("card_boarding_thunder_13", "造成40+7X点伤害。")]
        [TestCase("card_boarding_thunder_14", "造成32+6X点无视格挡的伤害。")]
        [TestCase("card_boarding_thunder_15", "造成56+8X点伤害，连锁10：伤害翻倍，连锁20：伤害翻倍。")]
        public void RevisedBaseEffectsMatchTheManuscriptAndClarifications(string id, string expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description), Is.EqualTo(expected));
        }

        [TestCase("card_school_thunder_b03", 4, 1)]
        [TestCase("card_school_thunder_b04", 3, 0)]
        [TestCase("card_school_thunder_b06", 1, 0)]
        [TestCase("card_special_crew_bronze_bell_gunner_helen", 2, 0)]
        public void RevisedCoefficientsAreDataAndUpgradeWithoutChangingThresholds(string id, int power, int threshold)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            Assert.That(session.GetBattleCardAtLevel(index, 0).SchoolEffect.Power, Is.EqualTo(power));
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                Assert.That(session.GetBattleCardAtLevel(index, rank).SchoolEffect.Threshold, Is.EqualTo(threshold));
        }

        [Test]
        public void PrechargeHasNoChainResourceCost()
        {
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec("card_school_thunder_n03");
            Assert.That(spec.ResourceLabel, Is.Empty);
            Assert.That(spec.ResourceMinimum, Is.Zero);
            Assert.That(spec.ResourceMaximum, Is.Zero);
            Assert.That(new CoreVoyageSession().GetCardMaximumUpgradeLevel("card_school_thunder_n03"), Is.Zero);
        }

        [TestCase("card_special_bow_blackreef_hammer")]
        [TestCase("card_special_bow_worldsplitter")]
        [TestCase("card_special_instrument_stormoracle")]
        public void EveryExpansionRankExtendsAllFriendlyFieldsOneStep(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1));
                Assert.That(card.SchoolEffect.Secondary, Is.Zero);
                Assert.That(Compact(card.Description), Does.Contain("将所有己方雷栅向上下左右各延伸1格")
                    .And.Not.Contain("延长").And.Not.Contain("4格内").And.Not.Contain("最多新增"));
            }
        }

        [TestCase("card_special_sail_aurora_fan", 2)]
        [TestCase("card_special_sail_skybreaker_array", 5)]
        public void SailsExtendAllFriendlyFieldsForTheRevisedDuration(string id, int rounds)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), 0);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(rounds));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方雷栅延长" + rounds + "回合"));
        }

        [TestCase("card_special_plating_aurora_laminate")]
        [TestCase("card_special_plating_stormark_bulwark")]
        public void InsulationUsesTheSharedNegativeFieldEffectImmunityWording(string id)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description),
                Does.Contain("直到下个己方回合开始时，免疫雷栅负面效果影响"));
        }

        [Test]
        public void EveryPublishedRankHasUsefulGrowthAndNoRetiredChainRules()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                if (basis == null || basis.SchoolId != "thunder" || basis.AllowedStages == NavalBattleStages.None ||
                    CoreBattleCardAliases.IsAlias(basis.Id) || (basis.AllowedStages == NavalBattleStages.Boarding &&
                    basis.RequiredCrew.Length == 0 && basis.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(basis))) continue;
                count++;
                string previous = null;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                    Assert.That(text, Is.Not.EqualTo(previous), basis.Id + " +" + rank);
                    Assert.That(Regex.IsMatch(text, "上限6|回合结束清空|最多新增|雷栅内的敌舰造成实际|最多(?:14|20|40|50|64)点伤害"),
                        Is.False, basis.Id + " +" + rank + ": " + text);
                    if (basis.Id == CoreNavalThunderPower.CircuitId)
                        Assert.That(text, Does.Contain("每回合合计只能使用一次"), "The new energy circuit has its own explicit turn limit.");
                    else
                        Assert.That(text, Does.Not.Contain("每回合合计只能"), basis.Id + " +" + rank);
                    previous = text;
                }
            }
            Assert.That(count, Is.EqualTo(42));
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
