using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCurseCatalogRevisionTests
    {
        [TestCase("card_school_curse_b03", "施加3层咒印。")]
        [TestCase("card_school_curse_b04", "施加6层咒印。")]
        [TestCase("card_school_curse_b05", "对敌方造成1×咒印层数点咒术伤害。")]
        [TestCase("card_school_curse_b06", "获得1点普通格挡；获得1点行动点；抽1张牌；敌方每有1层咒印，获得1点普通格挡。")]
        [TestCase("card_school_curse_n01", "范围1-4格：铺设持续时间为5回合的2×2禁域。")]
        [TestCase("card_boarding_curse_10", "本场战斗中，咒印消灭阈值降低5个百分点（可叠加）；降至0%时立即消灭敌方。")]
        [TestCase("card_boarding_curse_11", "敌方咒印层数≥2才可以使用：造成40+8X点咒术伤害；清除咒印效果。")]
        [TestCase("card_boarding_curse_12", "恢复2点生命值；敌方咒印层数≥4时，消耗1药物额外恢复24点生命值。")]
        [TestCase("card_boarding_curse_14", "敌方咒印层数≥4才可以使用：获得2点能量。")]
        [TestCase("card_boarding_curse_15", "敌方咒印层数≥4才可以使用：获得20点格挡。")]
        public void RevisedBaseDescriptionsMatchTheManuscript(string id, string expected)
        {
            var session = new CoreVoyageSession();
            Assert.That(Compact(session.GetBattleCardAtLevel(Index(id), 0).Description), Is.EqualTo(expected));
        }

        [Test]
        public void LoadSealRequiresOneMarkButHasNoMarkPayment()
        {
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec("card_school_curse_n02");
            Assert.That(spec.ResourceMinimum, Is.EqualTo(1));
            Assert.That(spec.ResourceMaximum, Is.Zero);
            Assert.That(spec.ConsumesAllResource, Is.False);
            var session = new CoreVoyageSession();
            string description = Compact(session.GetBattleCardAtLevel(Index("card_school_curse_n02"), 0).Description);
            Assert.That(description, Does.Contain("敌方咒印层数≥1才可以使用").And.Contain("敌方下一次炮击伤害降低6点"));
        }

        [Test]
        public void DetonateDamagePerMarkGrowsAtEveryRank()
        {
            var session = new CoreVoyageSession();
            int index = Index("card_school_curse_b05");
            int[] damagePerMark = { 1, 2, 4, 5 };
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(3));
            for (int level = 0; level < damagePerMark.Length; level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(damagePerMark[level]));
                Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.CurseDetonate));
                Assert.That(Compact(card.Description), Does.Contain(damagePerMark[level] + "×咒印层数点咒术伤害")
                    .And.Not.Contain("下次").And.Not.Contain("额外扣除"));
            }
        }

        [TestCase("card_school_curse_b03", new[] { 3, 8, 12 })]
        [TestCase("card_school_curse_b04", new[] { 6, 9, 20, 30 })]
        public void NavalMarkEntrypointsHaveUsefulBaseAndUpgradeValues(string id, int[] amounts)
        {
            var session = new CoreVoyageSession();
            for (int rank = 0; rank < amounts.Length; rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(Index(id), rank);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(amounts[rank]));
                Assert.That(Compact(card.Description), Is.EqualTo("施加" + amounts[rank] + "层咒印。"));
            }
        }

        [TestCase("card_school_curse_b01", 0, 2, 1, 0)]
        [TestCase("card_school_curse_b01", 1, 5, 1, 0)]
        [TestCase("card_school_curse_b01", 2, 8, 2, 0)]
        [TestCase("card_school_curse_b02", 0, 2, 0, 2)]
        [TestCase("card_school_curse_b02", 1, 5, 0, 3)]
        [TestCase("card_school_curse_b02", 2, 8, 0, 4)]
        public void PreparationUpgradesApplyMoreMarksWithoutChangingTheirOtherAuthoredRewards(
            string id, int rank, int marks, int draw, int guard)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10),
            });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("CURSE-PREPARATION-UPGRADE-20261024");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Preparation));
            Assert.That(session.GetCardMaximumUpgradeLevel(Index(id)), Is.EqualTo(2));
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1000;
            session.Battle.EnemyBlock = 23;
            session.Battle.PlayerBlock = 7;
            session.Battle.Energy = 3;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = session.Cargo.Medicine = 20;
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index(id));
            session.Battle.DrawPile.Clear();
            session.Battle.DiscardPile.Clear();
            for (int i = 0; i < 10; i++) session.Battle.DrawPile.Add(Index("card_brace"));
            CoreBattleCard card = session.GetBattleCard(Index(id));
            Assert.That(card.Energy, Is.EqualTo(1));
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(marks));
            Assert.That(Compact(card.Description), Does.Contain("施加" + marks + "层咒印"));

            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(marks), "The new resource growth must reach real battle state.");
            Assert.That(session.Battle.Energy, Is.EqualTo(2));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(7 + guard));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(draw));
            Assert.That(session.Battle.DrawPile.Count, Is.EqualTo(10 - draw));
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(1000));
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(23));
            Assert.That(session.Cargo.Powder, Is.EqualTo(20));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(20));
            Assert.That(session.Cargo.Wood, Is.EqualTo(20));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(20));

            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(marks), "Preparation marks must remain available in naval combat.");
        }

        [TestCase("card_special_bow_gravecurrent_spur")]
        [TestCase("card_special_bow_crownless_colossus")]
        [TestCase("card_special_instrument_eye_of_depths")]
        public void CurseExpansionStaysOneCardinalStepWithoutExtensionAtEveryRank(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.SchoolEffect.Power, Is.EqualTo(1));
                Assert.That(card.SchoolEffect.Secondary, Is.Zero);
                Assert.That(Compact(card.Description), Does.Contain("将所有己方禁域向上下左右各延伸1格")
                    .And.Not.Contain("4格内").And.Not.Contain("延长").And.Not.Contain("最多新增"));
            }
        }

        [Test]
        public void CrownOfGalesExtendsAllFriendlyFieldsFiveRounds()
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index("card_special_sail_crown_of_gales"), 0);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(5));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方禁域延长5回合").And.Not.Contain("4格内"));
        }

        [Test]
        public void EclipseShroudExtendsAllFriendlyCurseFieldsTwoRounds()
        {
            var session = new CoreVoyageSession();
            CoreBattleCard card = session.GetBattleCardAtLevel(Index("card_special_sail_eclipse_shroud"), 0);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(2));
            Assert.That(Compact(card.Description), Does.Contain("使所有己方禁域延长2回合").And.Not.Contain("火场"));
            Assert.That(session.GetCardMaximumUpgradeLevel(Index("card_boarding_curse_10")), Is.Zero,
                "缠咒 retains its authored fixed-mechanic upgrade limit.");
        }

        [Test]
        public void EveryPublishedCurseRankHasUsefulGrowthAndNoRetiredMarkDamageRules()
        {
            var session = new CoreVoyageSession();
            int count = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard basis = session.GetBattleCardAtLevel(index, 0);
                if (basis == null || basis.SchoolId != "curse" || basis.AllowedStages == NavalBattleStages.None ||
                    CoreBattleCardAliases.IsAlias(basis.Id) || (basis.AllowedStages == NavalBattleStages.Boarding &&
                    basis.RequiredCrew.Length == 0 && basis.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(basis))) continue;
                count++;
                string previous = null;
                for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
                {
                    string text = Compact(session.GetBattleCardAtLevel(index, level).Description);
                    Assert.That(text, Is.Not.EqualTo(previous), basis.Id + " +" + level + " must change an authored reward.");
                    Assert.That(Regex.IsMatch(text, "咒痕|咒印伤害额外结算|下次结算咒印伤害|咒印额外扣除|最多新增"),
                        Is.False, basis.Id + " +" + level + ": " + text);
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
