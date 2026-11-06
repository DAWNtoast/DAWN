using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTideCatalogRevisionTests
    {
        [TestCase("card_school_tide_n01", "范围1-4格：铺设持续时间为5回合的4×4定向水流。")]
        [TestCase("card_school_tide_n04", "范围1-4格：使敌方向后移动1格，敌方下次移动时至多移动1格。")]
        [TestCase("card_school_tide_n06", "对定向水流中的角色造成12+X点伤害。")]
        [TestCase("card_school_tide_b03", "范围1-4格：造成12+X点船体伤害。")]
        [TestCase("card_school_tide_b06", "范围1-4格：造成10+1.25X点船体伤害。")]
        [TestCase("card_boarding_tide_02", "获得30点蓄潮格挡。")]
        [TestCase("card_boarding_tide_05", "本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。")]
        [TestCase("card_boarding_tide_08", "造成60+1.5X点伤害。")]
        public void RevisedBaseDescriptionsMatchTheManuscriptAndClarifications(string id, string expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description), Is.EqualTo(expected));
        }

        [TestCase("card_school_tide_b01")]
        [TestCase("card_school_tide_b02")]
        [TestCase("card_school_tide_b04")]
        [TestCase("card_school_tide_b05")]
        [TestCase("card_school_tide_n03")]
        [TestCase("card_special_crew_white_shell_navigator_saina")]
        public void EveryWoodShieldConvertsAllCurrentGuardAtEveryRank(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                Assert.That(text, Does.Contain("消耗1木板额外获得").And.Contain("将当前全部格挡变为蓄潮格挡")
                    .And.Not.Contain("本牌格挡").And.Not.Contain("拒绝一次"));
                // The 2026-10-27 protection revision keeps the wood payment and ends at the next owner start.
                if (id == "card_school_tide_n03")
                    Assert.That(text, Does.Contain("直到下个己方回合开始时，免疫定向水流负面效果影响"));
            }
        }

        [TestCase("card_special_bow_abyssal_fang")]
        [TestCase("card_special_bow_leviathan_ram")]
        [TestCase("card_special_instrument_worldcompass")]
        public void TideExpansionStaysOneCardinalStepWithoutExtensionAtEveryRank(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1));
                Assert.That(card.SchoolEffect.Secondary, Is.Zero);
                Assert.That(Compact(card.Description), Does.Contain("将所有己方定向水流向上下左右各延伸1格")
                    .And.Not.Contain("4格内").And.Not.Contain("延长").And.Not.Contain("最多新增"));
            }
        }

        [TestCase("card_special_sail_ghostcurrent_canvas", 0, 2)]
        [TestCase("card_special_sail_ghostcurrent_canvas", 1, 4)]
        [TestCase("card_special_sail_ghostcurrent_canvas", 2, 6)]
        [TestCase("card_special_sail_tempest_wing", 0, 5)]
        [TestCase("card_special_sail_tempest_wing", 1, 8)]
        [TestCase("card_special_sail_tempest_wing", 2, 10)]
        [TestCase("card_special_sail_tempest_wing", 3, 13)]
        public void TideSailsExtendAllFriendlyFieldsAtEveryRank(string id, int rank, int rounds)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), rank);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(rounds));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方定向水流延长" + rounds + "回合")
                .And.Not.Contain("4格内"));
        }

        [Test]
        public void ThunderFieldAlsoUsesTheConfirmedSquareShape()
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index("card_school_thunder_n01"), 0).Description),
                Does.Contain("铺设持续时间为5回合的7×7雷栅").And.Not.Contain("1×3").And.Not.Contain("横向或纵向"));
        }

        [Test]
        public void EveryPublishedTideRankHasUsefulGrowthAndNoRetiredExpirationOrExpansionRules()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                if (basis == null || basis.SchoolId != "tide" || basis.AllowedStages == NavalBattleStages.None ||
                    CoreBattleCardAliases.IsAlias(basis.Id) || (basis.AllowedStages == NavalBattleStages.Boarding &&
                    basis.RequiredCrew.Length == 0 && basis.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(basis))) continue;
                count++;
                string previous = null;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                    Assert.That(text, Is.Not.EqualTo(previous), basis.Id + " +" + rank + " must change an authored reward.");
                    Assert.That(Regex.IsMatch(text, "最多新增|本牌格挡|拒绝一次强制位移|潮势保留到|选择波向|1×3区域"),
                        Is.False, basis.Id + " +" + rank + ": " + text);
                    previous = text;
                }
            }
            Assert.That(count, Is.EqualTo(39));
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
