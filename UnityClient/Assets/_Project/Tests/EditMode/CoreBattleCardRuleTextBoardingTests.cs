using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardRuleTextBoardingTests
    {
        [TestCase("fire_05", "1火药", "火药")]
        [TestCase("arcane_05", "2魔力", "魔力")]
        [TestCase("curse_12", "2层咒印", "咒印")]
        [TestCase("tide_11", "6潮势", "潮势")]
        public void UpgradedHealingKeepsMedicineAndSecondaryResourceFallbacks(string key, string cost, string resource)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                Contains(text, "恢复2点生命值");
                Contains(text, "消耗1药物和" + cost + "额外恢复" + (card.Repair - 2) + "点生命值");
                Assert.That(text, Does.Not.Contain("无药物"));
                string template = Compact(CoreBattleCardText.FormatEffects(card.DescriptionTemplate));
                Contains(template, "恢复2点生命值");
                Contains(template, "消耗1药物和" + cost);
                Assert.That(template, Does.Contain(resource));
            }
        }

        [Test]
        public void ContinuousTreatmentKeepsItsBaseAndPaidRecoveryLayers()
        {
            foreach (CoreBattleCard card in AtEveryLevel("tide_12"))
            {
                string text = Compact(card.Description);
                Contains(text, "获得持续恢复：2");
                Contains(text, "消耗1药物和8潮势额外获得持续恢复：" +
                    (CoreCardUpgradeDesign.ScaleEffect(card, 8, card.UpgradeLevel) - 2));
                Assert.That(text, Does.Not.Contain("不叠加").And.Not.Contain("这两次治疗"));
            }
        }

        [TestCase("thunder_13", 50, false)]
        [TestCase("thunder_14", 40, true)]
        [TestCase("thunder_15", 64, false)]
        public void FinishersKeepTheirScaledDamageCapsAndThePiercingException(string key, int baseCap, bool piercing)
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey(key);
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                Contains(text, "终结：造成");
                Assert.That(text, Does.Contain("X").And.Not.Contain("X为").And.Not.Contain("消耗全部连锁"));
                Contains(text, "最多" + CoreCardUpgradeDesign.ScaleEffect(design, baseCap, card.UpgradeLevel));
                Contains(text, "连锁为0时也可使用");
                Assert.That(text.Contains("无视格挡"), Is.EqualTo(piercing), key + " +" + card.UpgradeLevel);
                Assert.That(text, Does.Not.Contain("结算后不加链"));
            }
        }

        [TestCase("arcane_13", 0, "50%")]
        [TestCase("curse_09", 0, "2×咒印层数")]
        [TestCase("tide_08", 0, "30+向下取整的X×150%")]
        [TestCase("thunder_02", 14, "最多")]
        public void CurrentFormulasDistinguishUncappedEffectsFromRetainedDamageCaps(string key, int baseCap, string formula)
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey(key);
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                if (baseCap > 0) Contains(text, formula + CoreCardUpgradeDesign.ScaleEffect(design, baseCap, card.UpgradeLevel));
                else
                {
                    Contains(text, formula);
                    Assert.That(text, Does.Not.Contain("最多"));
                    if (card.UpgradeLevel > 0) Contains(text, "结果四舍五入");
                }
            }
        }

        [TestCase("fire_14", false)]
        [TestCase("arcane_10", false)]
        [TestCase("curse_14", false)]
        [TestCase("tide_14", false)]
        [TestCase("thunder_01", true)]
        [TestCase("thunder_07", true)]
        [TestCase("thunder_09", true)]
        [TestCase("blood_05", true)]
        public void CurrentOncePerTurnRestrictionsSurviveEveryUpgrade(string key, bool restricted)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
                Assert.That(Compact(card.Description).Contains("同名牌每回合合计只能使用一次"),
                    Is.EqualTo(restricted), key + " +" + card.UpgradeLevel);
        }

        [TestCase("fire_12", false)]
        [TestCase("arcane_13", false)]
        [TestCase("curse_09", false)]
        [TestCase("tide_09", false)]
        [TestCase("tide_12", false)]
        [TestCase("thunder_06", true)]
        [TestCase("thunder_10", true)]
        [TestCase("blood_08", true)]
        [TestCase("blood_15", true)]
        public void CurrentStackingRestrictionsSurviveEveryUpgrade(string key, bool restricted)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
                Assert.That(Compact(card.Description).Contains("不叠加"), Is.EqualTo(restricted),
                    key + " +" + card.UpgradeLevel);
        }

        [TestCase("blood_01", 3)]
        [TestCase("blood_02", 4)]
        [TestCase("blood_03", 3)]
        [TestCase("blood_04", 3)]
        [TestCase("blood_05", 3)]
        public void LifePaymentMovesToIconsWhileEveryUpgradeKeepsItsOwnEffect(string key, int lifeCost)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                Contains(Compact(CoreBattleCardText.FormatEffects(card.DescriptionTemplate)), "支付" + lifeCost + "生命");
                Assert.That(text, Does.Not.Contain("支付" + lifeCost + "点生命"));
                if (card.Damage > 0) Contains(text, "造成" + card.Damage + "点伤害");
                if (card.Block > 0) Contains(text, "获得" + card.Block + "点格挡");
                if (card.Draw > 0) Contains(text, "抽" + card.Draw + "张牌");
                if (key == "blood_05") Contains(text, "获得" + CoreBattleUpgradeBalance.EnergyRecovery(card) + "点能量");
                Assert.That(text, Does.Not.Contain("至少留1").And.Not.Contain("生命支付不受格挡"));
            }
        }

        [Test]
        public void DesperationHealingKeepsBothFallbackValuesWithoutRepeatingTheSharedKeyword()
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey("blood_09");
            foreach (CoreBattleCard card in AtEveryLevel("blood_09"))
            {
                string text = Compact(card.Description);
                int normal = CoreCardUpgradeDesign.ScaleEffect(design, 7, card.UpgradeLevel);
                int desperate = CoreCardUpgradeDesign.ScaleEffect(design, 9, card.UpgradeLevel);
                Contains(text, "恢复2点生命值，若处于绝境则改为恢复3点生命值");
                Contains(text, "消耗1药物额外恢复" + (normal - 2) + "点生命值，若处于绝境则改为额外恢复" + (desperate - 3) + "点生命值");
                Assert.That(text, Does.Not.Contain("最大生命40%").And.Not.Contain("是否处于绝境"));
            }
        }

        [Test]
        public void LifeDrainKeepsItsSuccessBudgetAndScaledHealingCap()
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey("blood_06");
            foreach (CoreBattleCard card in AtEveryLevel("blood_06"))
            {
                string text = Compact(card.Description);
                Contains(text, "最多恢复" + CoreCardUpgradeDesign.ScaleEffect(design, 2, card.UpgradeLevel) + "点生命");
                Contains(text, "同名牌每回合合计只能成功吸血一次，己方生命值已满或本次攻击未使敌方失去生命值时，不消耗次数");
            }
        }

        [Test]
        public void OncePerBattleRestrictionAndCurrentCurseSettlementAndTideGainRemainVisible()
        {
            foreach (CoreBattleCard card in AtEveryLevel("blood_14"))
            {
                string text = Compact(card.Description);
                Contains(text, "只有处于绝境时才可使用");
                Contains(text, "使用后移出本场");
                Contains(text, "同名牌每场战斗合计只能使用一次");
            }
            foreach (CoreBattleCard card in AtEveryLevel("curse_10"))
                Assert.That(Compact(card.Description), Is.EqualTo("咒印伤害额外结算1次。"));
            foreach (CoreBattleCard card in AtEveryLevel("tide_05"))
                Contains(Compact(card.Description), "获得" + CoreCardUpgradeDesign.ScaleEffect(card, 6, card.UpgradeLevel) + "点潮势");
        }

        [TestCase("fire_02", "灼烧", "每层在敌方回合结束")]
        [TestCase("fire_03", "灼烧", "每层每次")]
        [TestCase("fire_04", "灼烧", "每批独立结算")]
        [TestCase("fire_11", "施加3层自燃", "自燃每层每次")]
        [TestCase("curse_01", "施加1层咒印", "咒痕上限6")]
        [TestCase("curse_02", "施加3层咒印", "每批持续3次")]
        [TestCase("tide_01", "蓄潮格挡", "每实际吸收")]
        [TestCase("tide_02", "蓄潮格挡", "每实际吸收")]
        [TestCase("tide_03", "蓄潮格挡", "等量产势")]
        public void StatusApplicationsRemainWhileSharedDefinitionsDisappearFromEveryView(string key, string effect, string rule)
        {
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                foreach (string description in new[] { card.Description,
                    session.BattleCardFaceDescription(card, NavalBattleStage.Boarding),
                    session.BattleCardEffectDescription(card, NavalBattleStage.Boarding) })
                {
                    string text = Compact(description);
                    Contains(text, effect);
                    Assert.That(text, Does.Not.Contain(rule), key + " +" + card.UpgradeLevel);
                    Assert.That(Regex.IsMatch(description, @"[，；。]\s*[，；。]|^[，；。]|[，；]$"), Is.False, description);
                }
                Assert.That(Compact(card.DescriptionTemplate), Does.Contain(effect).And.Not.Contain(rule),
                    "Manuscript templates retain the status application, not retired duration or cap rules.");
            }
        }

        private static IEnumerable<CoreBattleCard> AtEveryLevel(string key)
        {
            var session = new CoreVoyageSession();
            string id = "card_boarding_" + key;
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == id);
            for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
                yield return session.GetBattleCardAtLevel(index, level);
        }

        private static void Contains(string text, string authoredPhrase) =>
            Assert.That(text, Does.Contain(Compact(CoreBattleCardText.FormatEffects(authoredPhrase))), text);
        private static string Compact(string value) => Regex.Replace(value, @"\s+", string.Empty);
    }
}
