using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreArcaneCatalogRevisionTests
    {
        [TestCase("card_school_arcane_b03", 10, 0, 1)]
        [TestCase("card_school_arcane_b04", 25, 0, 2)]
        [TestCase("card_school_arcane_b05", 0, 12, 2)]
        public void RewrittenNavalCardsKeepManaCostAndOnlyImmediateEffects(string id, int damage, int block, int manaCost)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
            Assert.That(basis.Damage, Is.EqualTo(damage));
            Assert.That(basis.Block, Is.EqualTo(block));
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.SchoolEffect.Threshold, Is.EqualTo(manaCost));
                Assert.That(card.SchoolEffect.Power, Is.Zero);
                Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(block > 0 ? CoreSchoolMechanic.ArcaneWard : CoreSchoolMechanic.ArcaneRelease));
                string expected = block > 0 ? "获得" + card.Block + "点格挡。" : "范围1-4格：造成" + card.Damage + "点船体伤害。";
                Assert.That(Compact(card.Description), Is.EqualTo(expected));
            }
        }

        [TestCase("card_school_arcane_n01", "范围1-4格：铺设持续3回合的2×2折射阵。")]
        [TestCase("card_school_arcane_n03", "范围1-4格：造成10点无视格挡伤害；造成26点伤害。")]
        [TestCase("card_boarding_arcane_07", "移除16点格挡，造成20点伤害。")]
        [TestCase("card_boarding_arcane_13", "下一张秘术攻击牌获得效果：延迟：造成其伤害的50%的伤害。")]
        public void RevisedBaseDescriptionsMatchTheManuscript(string id, string expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description), Is.EqualTo(expected));
        }

        [TestCase("card_special_bow_voidray_edge")]
        [TestCase("card_special_bow_last_horizon")]
        [TestCase("card_special_instrument_last_horizon_lens")]
        public void ArcaneExpansionStaysOneCardinalStepWithoutExtensionAtEveryRank(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1));
                Assert.That(card.SchoolEffect.Secondary, Is.Zero);
                Assert.That(Compact(card.Description), Does.Contain("将所有己方折射阵向上下左右各延伸1格")
                    .And.Not.Contain("4格内").And.Not.Contain("延长").And.Not.Contain("最多新增"));
            }
        }

        [TestCase("card_special_sail_last_free_wind", 2)]
        [TestCase("card_special_sail_moonweave", 1)]
        public void ArcaneSailsExtendAllFriendlyFields(string id, int rounds)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), 0);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(rounds));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方折射阵延长" + rounds + "回合")
                .And.Not.Contain("4格内"));
        }

        [Test]
        public void EveryPublishedArcaneRankHasUsefulGrowthAndNoRetiredManaRules()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                if (basis == null || basis.SchoolId != "arcane" || basis.AllowedStages == NavalBattleStages.None ||
                    CoreBattleCardAliases.IsAlias(basis.Id) || (basis.AllowedStages == NavalBattleStages.Boarding &&
                    basis.RequiredCrew.Length == 0 && basis.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(basis))) continue;
                count++;
                string previous = null;
                for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                    string text = Compact(card.Description);
                    Assert.That(text, Is.Not.EqualTo(previous), basis.Id + " +" + level + " must change an authored reward.");
                    Assert.That(Regex.IsMatch(text, "临时法力|临时魔力|法力[^；。]*不超过|魔力[^；。]*不超过|跨回合保留|不会自然恢复"),
                        Is.False, basis.Id + " +" + level + ": " + text);
                    previous = text;
                }
            }
            Assert.That(count, Is.EqualTo(40));
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
