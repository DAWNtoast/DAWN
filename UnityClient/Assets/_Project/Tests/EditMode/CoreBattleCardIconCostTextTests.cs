using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardIconCostTextTests
    {
        [TestCase("card_load_shot")]
        [TestCase("card_chain_shot")]
        [TestCase("card_school_arcane_n02")]
        [TestCase("card_school_blood_n02")]
        public void FixedCostsLeaveEffectTextWhileRemainingInTheCardDefinition(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.Energy, Is.EqualTo(original.Energy));
                Assert.That(card.PowderCost, Is.EqualTo(original.PowderCost));
                Assert.That(card.CannonballCost, Is.EqualTo(original.CannonballCost));
                foreach (string text in new[] { card.Description,
                    session.BattleCardFaceDescription(card, NavalBattleStage.Naval),
                    session.BattleCardEffectDescription(card, NavalBattleStage.Naval) })
                {
                    Assert.That(text, Is.Not.Empty, id + " +" + rank);
                    Assert.That(Regex.IsMatch(text, @"(?:消耗|支付)\s*\d+\s*(?:点\s*)?(?:能量|火药|炮弹|法力|魔力|船体)"),
                        Is.False, id + " +" + rank + ": " + text);
                }
            }
            if (id == "card_load_shot")
            {
                Assert.That(original.PowderCost, Is.EqualTo(1));
                Assert.That(original.CannonballCost, Is.Zero);
                Assert.That(Compact(original.Description), Does.Contain("额外装药：5").And.Contain("抽1张牌"));
            }
        }

        [Test]
        public void EnergyRecoveryEffectDoesNotRepeatItsUpfrontEnergyCost()
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey("basic_10");
            Assert.That(card.Energy, Is.EqualTo(2));
            var session = new CoreVoyageSession();
            foreach (string text in new[] { card.Description,
                session.BattleCardFaceDescription(card, NavalBattleStage.Boarding),
                session.BattleCardEffectDescription(card, NavalBattleStage.Boarding) })
                Assert.That(Compact(text), Is.EqualTo("获得3点能量。"));
        }

        [TestCase("card_boarding_arcane_04")]
        [TestCase("card_boarding_arcane_06")]
        [TestCase("card_boarding_arcane_11")]
        [TestCase("card_boarding_curse_11")]
        [TestCase("card_boarding_tide_06")]
        [TestCase("card_boarding_tide_07")]
        [TestCase("card_boarding_tide_08")]
        [TestCase("card_boarding_thunder_13")]
        [TestCase("card_boarding_thunder_14")]
        [TestCase("card_boarding_thunder_15")]
        [TestCase("card_school_thunder_n06")]
        public void AllResourceEffectsKeepTheirFormulasWithoutDefiningTheIconX(string id)
        {
            var session = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                NavalBattleStage stage = card.BoardingSpec == null ? NavalBattleStage.Naval : NavalBattleStage.Boarding;
                Assert.That(card.DescriptionTemplate, Does.Contain("X"), "The authored formula preserves the all-resource variable.");
                foreach (string text in new[] { card.Description, session.BattleCardFaceDescription(card, stage),
                    session.BattleCardEffectDescription(card, stage) })
                {
                    Assert.That(text, Does.Contain("X").And.Not.Contain("消耗全部"));
                    Assert.That(text, Does.Not.Match(@"X\s*(?:为|是|表示|代表|[=＝])"));
                    Assert.That(text, Does.Not.Match(@"^[，；。]|[：][，；。]"), text);
                }
            }
            if (id == "card_boarding_arcane_06")
                Assert.That(Compact(session.GetBattleCardAtLevel(index, 0).Description),
                    Is.EqualTo("造成5点伤害2+X次。"));
            if (id == "card_boarding_curse_11")
                Assert.That(Compact(session.GetBattleCardAtLevel(index, 0).Description), Does.Contain("目标至少有1层咒印时才可使用"));
        }

        [TestCase("fire_07")]
        [TestCase("fire_08")]
        public void SelectedHandCardCountKeepsItsRangeInTheAction(string key)
        {
            string text = Compact(CoreBoardingCardCatalog.FindByKey(key).Description);
            Assert.That(text, Does.Contain("焚烧X张其他手牌（1～3张）").And.Not.Contain("X为"));
        }

        [TestCase("card_emergency_repair", NavalBattleStage.Naval)]
        [TestCase("card_crew_bandage", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_basic_07", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_fire_05", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_arcane_05", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_curse_12", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_tide_11", NavalBattleStage.Boarding)]
        [TestCase("card_boarding_tide_12", NavalBattleStage.Boarding)]
        [TestCase("card_special_crew_gray_gull_cook_pepe", NavalBattleStage.Naval)]
        public void SupplyHealingAtEveryRankKeepsTheFreeBaseAndPaidTotal(string id, NavalBattleStage stage)
        {
            CoreVoyageSession session = Battle(stage);
            int index = Index(id);
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.IsAllowedInStage(stage), Is.True, id);
                string supply = stage == NavalBattleStage.Boarding ? "药物" : "木板";
                session.Cargo.Medicine = session.Cargo.Wood = 10;
                CoreBattleCardEffects full = session.GetBattleCardEffects(card, stage);
                string paidText = session.BattleCardFaceDescription(card, stage);
                AssertHealingParts(paidText, full.FallbackRepair, full.FullRepair, supply, id + " +" + rank);
                Assert.That(session.Cargo.Medicine, Is.EqualTo(10));
                Assert.That(session.Cargo.Wood, Is.EqualTo(10));

                session.Cargo.Medicine = session.Cargo.Wood = 0;
                CoreBattleCardEffects fallback = session.GetBattleCardEffects(card, stage);
                Assert.That(fallback.Repair, Is.EqualTo(full.FallbackRepair));
                Assert.That(fallback.MedicineCost + fallback.WoodCost, Is.Zero);
                string freeText = session.BattleCardFaceDescription(card, stage);
                AssertHealingParts(freeText, full.FallbackRepair, full.FullRepair, supply, id + " no supply +" + rank);
            }
        }

        [TestCase("fire_05", "1药物和1火药")]
        [TestCase("arcane_05", "1药物和2点魔力")]
        [TestCase("curse_12", "1药物和2层咒印")]
        [TestCase("tide_11", "1药物和6点潮势")]
        [TestCase("tide_12", "1药物和8点潮势")]
        public void ConditionalTreatmentRetainsItsLinkedPaymentAsTheExtraEffectCondition(string key, string payment)
        {
            CoreBattleCard card = CoreBoardingCardCatalog.FindByKey(key);
            string text = Compact(card.Description);
            string paymentCondition = key == "tide_12" ? "消耗" + payment + "额外获得持续恢复：" :
                "消耗" + payment + "额外恢复";
            Assert.That(text, Does.Contain(paymentCondition), text);
            Assert.That(text, Does.Not.Contain("有药物时固定消耗").And.Not.Contain("无药物恢复"), text);
        }

        [TestCase("card_school_tide_b05")]
        [TestCase("card_school_tide_n03")]
        public void WoodGuardAtEveryRankKeepsBothBranchesWithoutChangingShieldAmounts(string id)
        {
            var preview = new CoreVoyageSession();
            int index = Index(id);
            for (int rank = 0; rank <= preview.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = preview.GetBattleCardAtLevel(index, rank);
                string text = Compact(card.Description);
                Assert.That(text, Does.Contain("获得2点格挡").And.Contain("消耗1木板额外获得"), id + " +" + rank);
                int extra = Value(text, @"额外获得(?<value>\d+)点格挡");
                Assert.That(2 + extra, Is.EqualTo(card.Block), text);
                Assert.That(text, Does.Contain("本牌格挡变为蓄潮格挡"), text);
                if (id == "card_school_tide_n03")
                    Assert.That(text, Does.Contain("可在下个己方回合开始前拒绝一次强制位移"));

                foreach (int wood in new[] { 0, 1 })
                {
                    CoreVoyageSession session = Battle(NavalBattleStage.Naval, id, rank);
                    session.Cargo.Wood = wood;
                    session.Battle.Hand.Clear();
                    session.Battle.Hand.Add(index);
                    int before = session.Battle.PlayerBlock;
                    Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
                    Assert.That(session.Battle.PlayerBlock - before, Is.EqualTo(wood == 0 ? 2 : card.Block),
                        id + " +" + rank + " wood=" + wood);
                    Assert.That(session.Cargo.Wood, Is.Zero);
                    Assert.That(session.Battle.Schools.TidePaidBlock, Is.EqualTo(wood == 0 ? 0 : card.Block));
                }
            }
        }

        private static void AssertHealingParts(string text, int fallback, int total, string supply, string context)
        {
            string compact = Compact(text);
            bool continuous = compact.Contains("持续恢复：");
            int basis = Value(compact, continuous ? @"获得持续恢复：(?<value>\d+)" : @"(?:恢复|修复)(?<value>\d+)点(?:生命值?|船体)");
            int extra = Value(compact, continuous ? @"额外获得持续恢复：(?<value>\d+)" : @"额外(?:恢复|修复)(?<value>\d+)点(?:生命值?|船体)");
            Assert.That(basis, Is.EqualTo(fallback), context + ": " + text);
            Assert.That(basis + extra, Is.EqualTo(total), context + ": " + text);
            Assert.That(compact, Does.Contain("消耗1" + supply), context + ": " + text);
        }

        private static CoreVoyageSession Battle(NavalBattleStage stage, string upgradedCard = null, int rank = 0)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            if (upgradedCard != null)
                session.ConfigureCardUpgrades(new System.Collections.Generic.Dictionary<string, int> { { upgradedCard, rank } });
            session.ConfigureSpecialCardSources(CoreVoyageSession.CardLibrary.Select(card => card.RequiredCrew)
                .Where(id => id.Length > 0).Distinct().ToArray(), CoreVoyageSession.CardLibrary.Select(card => card.RequiredComponent)
                .Where(id => id.Length > 0).Distinct().ToArray());
            session.ConfigureCrewCapabilities(true, true, true, true, true, true);
            session.SetSeed("ICON-COST-EFFECTS");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session,
                new object[] { new CoreEncounter("icon_effect", "费用描述测试", "海战", "费用描述", true, false, 1, 1000), false });
            session.Battle.Stage = stage;
            session.Battle.Energy = 100;
            session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.PlayerBoardingHealth = 900;
            session.Battle.EnemyBlock = 0;
            session.Battle.PlayerBlock = session.Battle.PlayerDeckBuff = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            Set(session, "Hull", Math.Max(1, session.MaxHull - 50));
            Set(session.Battle.Schools, "Mana", 12);
            Set(session.Battle.Schools, "CurseMarks", 6);
            Set(session.Battle.Schools, "TidePower", 20);
            return session;
        }

        private static int Value(string text, string pattern)
        {
            Match match = Regex.Match(text, pattern);
            Assert.That(match.Success, Is.True, "Missing effect amount: " + text);
            return int.Parse(match.Groups["value"].Value);
        }
        private static void Set(object target, string property, object value) =>
            target.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static string Compact(string text) => Regex.Replace(text, @"\s+", string.Empty);
    }
}
