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
        [TestCase("arcane_05", "4点魔力", "魔力")]
        [TestCase("tide_11", "12点潮势", "潮势")]
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
                Contains(text, "消耗1药物和16点潮势额外获得持续恢复：" +
                    (new[] { 11, 13, 15, 18 }[card.UpgradeLevel] - 2));
                Assert.That(text, Does.Not.Contain("不叠加").And.Not.Contain("这两次治疗"));
            }
        }

        [TestCase("thunder_13", "40+7X|50+9X|64+11X", false)]
        [TestCase("thunder_14", "32+6X|40+8X|52+10X", true)]
        [TestCase("thunder_15", "56+8X|68+10X|84+12X", false)]
        public void FinishersKeepTheirUncappedAuthoredFormulaAndThePiercingException(string key, string formula, bool piercing)
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey(key);
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                Contains(text, "造成");
                Assert.That(text, Does.Contain("X").And.Not.Contain("X为").And.Not.Contain("消耗全部连锁"));
                Contains(text, formula.Split('|')[card.UpgradeLevel]);
                Assert.That(text, Does.Not.Contain("最多"));
                if (key == "thunder_15") Contains(text, "连锁10：伤害翻倍，连锁20：伤害翻倍");
                Assert.That(text.Contains("无视格挡"), Is.EqualTo(piercing), key + " +" + card.UpgradeLevel);
                Assert.That(text, Does.Not.Contain("结算后不加链"));
            }
        }

        [TestCase("arcane_13", 0, "50%|63%|84%")]
        [TestCase("curse_09", 0, "2×咒印层数|3×咒印层数|4×咒印层数")]
        [TestCase("tide_08", 0, "60+1.5X|72+2X|90+2X")]
        public void CurrentFormulasRemainUncappedAcrossUpgrades(string key, int baseCap, string formula)
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey(key);
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
                if (baseCap > 0) Contains(text, formula + CoreCardUpgradeDesign.ScaleEffect(design, baseCap, card.UpgradeLevel));
                else
                {
                    Contains(text, formula.Split('|')[card.UpgradeLevel]);
                    Assert.That(text, Does.Not.Contain("最多"));
                    Assert.That(text, Does.Not.Contain("四舍五入").And.Not.Match(@"\)×\d"));
                }
            }
        }

        [TestCase("fire_14", false)]
        [TestCase("arcane_10", false)]
        [TestCase("curse_14", false)]
        [TestCase("tide_14", false)]
        [TestCase("thunder_01", false)]
        [TestCase("thunder_07", false)]
        [TestCase("thunder_09", false)]
        [TestCase("blood_05", false)]
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
        [TestCase("thunder_06", false)]
        [TestCase("thunder_10", false)]
        [TestCase("blood_08", false)]
        [TestCase("blood_15", false)]
        public void CardFacesOmitSharedStackingImplementationRulesAcrossUpgrades(string key, bool restricted)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
                Assert.That(Compact(card.Description).Contains("不叠加"), Is.EqualTo(restricted),
                    key + " +" + card.UpgradeLevel);
        }

        [TestCase("blood_01", 6)]
        [TestCase("blood_02", 8)]
        [TestCase("blood_03", 6)]
        [TestCase("blood_04", 6)]
        [TestCase("blood_05", 6)]
        public void LifePaymentMovesToIconsWhileEveryUpgradeKeepsItsOwnEffect(string key, int lifeCost)
        {
            foreach (CoreBattleCard card in AtEveryLevel(key))
            {
                string text = Compact(card.Description);
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
                int normal = CoreCardUpgradeDesign.ScaleEffect(design, 12, card.UpgradeLevel);
                int desperate = CoreCardUpgradeDesign.ScaleEffect(design, 26, card.UpgradeLevel);
                Contains(text, "恢复2点生命值，绝境：改为恢复5点生命值");
                Contains(text, "消耗1药物额外恢复" + (normal - 2) + "点生命值，绝境：改为额外恢复" + (desperate - 5) + "点生命值");
                Assert.That(text, Does.Not.Contain("最大生命40%").And.Not.Contain("是否处于绝境"));
            }
        }

        [Test]
        public void LifeDrainHasNoPerTurnBudgetOrHealingCap()
        {
            CoreBattleCard design = CoreBoardingCardCatalog.FindByKey("blood_06");
            foreach (CoreBattleCard card in AtEveryLevel("blood_06"))
            {
                string text = Compact(card.Description);
                Contains(text, "吸血");
                Assert.That(text, Does.Not.Contain("最多恢复").And.Not.Contain("每回合合计"));
            }
        }

        [Test]
        public void OncePerBattleRestrictionAndCurrentCurseSettlementAndTideGainRemainVisible()
        {
            foreach (CoreBattleCard card in AtEveryLevel("blood_14"))
            {
                string text = Compact(card.Description);
                Contains(text, "绝境时才可使用");
                Contains(text, "使用后移出本场");
                Assert.That(text, Does.Not.Contain("每场战斗合计只能使用一次"));
            }
            foreach (CoreBattleCard card in AtEveryLevel("curse_10"))
                Assert.That(Compact(card.Description), Is.EqualTo("本场战斗中，咒印消灭阈值降低5个百分点（可叠加）；降至0%时立即消灭敌方。"));
            foreach (CoreBattleCard card in AtEveryLevel("tide_05"))
                Assert.That(Compact(card.Description), Is.EqualTo("本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。"));
        }

        [TestCase("fire_02", "燃烧", "每层在敌方回合结束")]
        [TestCase("fire_03", "燃烧", "每层每次")]
        [TestCase("fire_04", "燃烧", "每批独立结算")]
        [TestCase("fire_11", "对自身施加3层燃烧", "自燃每层每次")]
        [TestCase("curse_01", "咒印", "咒痕上限6")]
        [TestCase("curse_02", "咒印", "每批持续3次")]
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

        [Test]
        public void CurseHealingRequiresFourMarksButOnlyPaysMedicineAtEveryRank()
        {
            foreach (CoreBattleCard card in AtEveryLevel("curse_12"))
            {
                string text = Compact(card.Description);
                Contains(text, "恢复2点生命值");
                Contains(text, "敌方咒印层数≥4时，消耗1药物额外恢复" + (card.Repair - 2) + "点生命值");
                Assert.That(text, Does.Not.Contain("消耗4层咒印"));
            }
        }

        [Test]
        public void ThunderAttackKeepsItsUncappedPerChainDamageAcrossEveryUpgrade()
        {
            foreach (CoreBattleCard card in AtEveryLevel("thunder_02"))
            {
                string text = Compact(card.Description);
                Contains(text, new[] { "出牌前每有一层连锁，本次伤害提高2点", "20+3×出牌前连锁层数",
                    "24+3×出牌前连锁层数", "30+4×出牌前连锁层数" }[card.UpgradeLevel]);
                Assert.That(text, Does.Not.Contain("最多"));
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
