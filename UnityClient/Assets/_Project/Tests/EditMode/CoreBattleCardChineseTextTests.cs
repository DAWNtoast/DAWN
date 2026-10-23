using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardChineseTextTests
    {
        [Test]
        public void SupplyBonusesFollowIndependentEffectsAtEveryPublishedRankAndStage()
        {
            var session = new CoreVoyageSession();
            int checkedQuotes = 0;
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                if (CoreVoyageSession.CardLibrary[index].AllowedStages == NavalBattleStages.None) continue;
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                    {
                        if (!card.IsAllowedInStage(stage)) continue;
                        foreach (string description in new[] { card.Description,
                            session.BattleCardFaceDescription(card, stage), session.BattleCardEffectDescription(card, stage) })
                        {
                            string text = Compact(description);
                            Match paid = Regex.Match(text, @"消耗[^；。]*(?:木板|药物)[^；。]*额外");
                            if (!paid.Success) continue;
                            checkedQuotes++;
                            // Only complete clauses qualify: a comma/newline after a condition
                            // must never make its conditional result appear independent.
                            foreach (Match independent in Regex.Matches(text,
                                @"(?:^|[；。])(?:抽\d+张牌|获得\d+点(?:行动点|格挡))(?=[；。]|$)"))
                                Assert.That(independent.Index, Is.LessThan(paid.Index), card.Id + " +" + rank + " " + stage + ": " + text);
                            if (text.Contains("海战：") && card.Draw > 0)
                                Assert.That(text.IndexOf("抽"), Is.LessThan(text.IndexOf("海战：")),
                                    "Shared draw precedes both stage-specific healing groups: " + text);
                        }
                    }
                }
            }
            Assert.That(checkedQuotes, Is.GreaterThan(100), "Include upgrades that introduce a paid healing bonus.");
        }

        [TestCase("card_school_fire_b06", "抽", "敌方燃烧时")]
        [TestCase("card_school_curse_b06", "抽", "当前咒印每有一层")]
        [TestCase("card_school_thunder_b02", "抽", "下一次雷霆攻击")]
        [TestCase("card_school_thunder_b05", "获得", "下一次雷霆攻击")]
        public void ImmediateEffectsPrecedeTheSeparateConditionalOrFutureEffect(string id, string immediate, string condition)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                Assert.That(text, Does.Contain(condition));
                Assert.That(text.IndexOf(immediate), Is.GreaterThanOrEqualTo(0).And.LessThan(text.IndexOf(condition)), id + " +" + rank);
            }
        }

        [Test]
        public void FieldProtectionPrecedesConditionalGuardWithoutSeparatingSharedFieldBonuses()
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard basis in CoreVoyageSession.CardLibrary.Where(card =>
                card.SchoolEffect?.Mechanic == CoreSchoolMechanic.ComponentFieldShelter))
                for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(basis.Id); rank++)
                {
                    string text = Compact(session.GetBattleCardAtLevel(Index(basis.Id), rank).Description);
                    if (basis.SchoolId == "arcane" || basis.SchoolId == "blood")
                        Assert.That(text, Does.Match(@"位于己方[^；。]+时，额外获得\d+点格挡并(?:获得\d+点法力|修复\d+点船体)"), basis.Id);
                    else
                        Assert.That(text.IndexOf("免疫"), Is.GreaterThanOrEqualTo(0).And.LessThan(text.IndexOf("位于己方")), basis.Id);
                }
        }

        [TestCase("card_school_curse_n03", "造成")]
        [TestCase("card_school_tide_n02", "造成")]
        [TestCase("card_school_tide_n05", "移除")]
        public void AllResourceFormulasKeepTheTargetRangeWithoutSelectionLimits(string id, string action)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                foreach (string text in new[] { card.Description,
                    session.BattleCardFaceDescription(card, NavalBattleStage.Naval),
                    session.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
                    Assert.That(Compact(text), Does.Contain("范围1-4格：").And.Contain(action).And.Contain("X")
                        .And.Not.Contain("选择").And.Not.Contain("所选"), id + " +" + rank);
            }
        }

        [TestCase("card_school_thunder_b03", "已有的连锁每有一层，本次伤害提高")]
        [TestCase("card_school_thunder_b06", "出牌前的连锁每有一层，本次伤害提高")]
        public void ChainBonusRemainsPartOfTheSameAttack(string id, string phrase)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                string text = Compact(session.GetBattleCardAtLevel(index, rank).Description);
                Assert.That(text, Does.Contain(phrase).And.Not.Contain("额外造成"), id + " +" + rank);
            }
        }

        [TestCase("card_school_blood_b06", "敌方生命不高于35%时，本次伤害提高")]
        public void LiveExecutionThresholdHasACompletePredicateAndOnlyOneDamageAmount(string id, string condition)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.EnemyHull = 100;
            session.Battle.EnemyMaxHull = 1000;
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string text = Compact(session.BattleCardFaceDescription(card, NavalBattleStage.Naval));
                Assert.That(text, Does.Contain(condition).And.Not.Match(@"时[，；。]+$"));
                MatchCollection damage = Regex.Matches(text, @"造成(\d+)点船体伤害");
                Assert.That(damage.Count, Is.EqualTo(1), text);
                Assert.That(int.Parse(damage[0].Groups[1].Value), Is.EqualTo(session.GetBattleCardDamage(card, NavalBattleStage.Naval)));
                Assert.That(text, Does.Not.Match(@"本次伤害提高\d"), "The quoted damage already includes the bonus.");
            }
        }

        [Test]
        public void SlowingAndMovingFieldsKeepTheirAuthoredActionAndTargetRange()
        {
            var session = new CoreVoyageSession();
            string slow = Compact(session.GetBattleCardAtLevel(Index("card_school_tide_n04"), 0).Description);
            Assert.That(slow, Does.Contain("将4格内的目标推离己舰1格")
                .And.Contain("其下次主动移动上限减少1格"));
            string move = Compact(session.GetBattleCardAtLevel(Index("card_school_fire_n05"), 0).Description);
            Assert.That(move, Does.Contain("将1-4格内的己方1格火场移动到相邻的格子"));
        }

        [TestCase("card_boarding_basic_07")]
        [TestCase("card_boarding_tide_12")]
        [TestCase("card_special_crew_white_shell_doctor_mirei")]
        public void ADisplayCopyPreservesContinuousRecoveryStacksAndTheirPaidCondition(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                string quote = session.BattleCardFaceDescription(card, NavalBattleStage.Boarding);
                // The UI constructs this display-only card from the resolved current-stage quote.
                var display = new CoreBattleCard(card.Id, card.Name, card.Type, card.Energy,
                    card.Damage, card.Block, card.Repair, card.Draw, card.PowderCost, quote,
                    NavalBattleStages.Boarding, card.Action, card.RequiredCrew, card.ArtId,
                    card.CannonballCost, card.RequiredComponent, card.UpgradeLevel, card.SchoolId, card.SchoolEffect);
                Assert.That(display.Description, Is.EqualTo(quote), id + " +" + rank);
                Assert.That(Compact(display.Description), Does.Contain("获得持续恢复：")
                    .And.Contain("消耗1药物").And.Contain("额外获得持续恢复："));
            }
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
