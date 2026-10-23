using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardWordingTests
    {
        [TestCase("格挡+2。", "获得 2 点格挡。")]
        [TestCase("获得3格挡。", "获得 3 点格挡。")]
        [TestCase("格挡额外+4。", "额外获得 4 点格挡。")]
        [TestCase("获得2普通格挡。", "获得 2 点普通格挡。")]
        [TestCase("敌舰船体 -12，无需弹药。", "造成 12 点船体伤害，无需弹药。")]
        [TestCase("造成8伤害。", "造成 8 点伤害。")]
        [TestCase("再造成6雷霆直伤。", "再造成 6 点雷霆直接伤害。")]
        [TestCase("双方受23＋4X直伤。", "双方受 23＋4X 点直接伤害。")]
        [TestCase("恢复7船体。", "修复 7 点船体。")]
        [TestCase("恢复6生命。", "恢复 6 点生命。")]
        [TestCase("恢复当前阶段生命 7。", "恢复 7 点当前阶段生命。")]
        [TestCase("恢复船员 6。", "恢复 6 点船员生命。")]
        [TestCase("恢复船员生命 8。", "恢复 8 点船员生命。")]
        [TestCase("修复船体 9。", "修复 9 点船体。")]
        [TestCase("恢复2能量。", "恢复 2 点能量。")]
        [TestCase("法力+2。", "恢复 2 点法力。")]
        [TestCase("抽2张。", "抽 2 张牌。")]
        [TestCase("使用1药物。", "消耗 1 药物。")]
        [TestCase("消耗1火药、1炮弹。", "消耗 1 火药、1 炮弹。")]
        [TestCase("下次炮击额外伤害+5。", "下次炮击伤害提高 5 点。")]
        [TestCase("下次炮击船体伤害+5。", "下次炮击船体伤害提高 5 点。")]
        [TestCase("每层额外+2伤害。", "每层伤害提高 2 点。")]
        [TestCase("下张主动攻击首段+4。", "下张主动攻击首段伤害提高 4 点。")]
        [TestCase("结算后+1连锁。", "结算后获得 1 层连锁。")]
        [TestCase("位于阵内获6格挡。", "位于阵内获得 6 点格挡。")]
        [TestCase("已有至少2连锁时改为−8。", "已有至少 2 层连锁时改为减少 8 点。")]
        public void EquivalentEffectPhrasesUseTheSamePlayerVocabulary(string source, string expected)
        {
            Assert.That(CoreBattleCardText.FormatEffects(source), Is.EqualTo(expected));
        }

        [TestCase("4 格内：造成6＋2×所选潮势炮击伤害；消耗1火药、1炮弹；消耗0～4潮势。")]
        [TestCase("造成(4+2×当前咒痕层数)×1.25（结果四舍五入）伤害；触发一次，不叠加。")]
        [TestCase("再造成6雷霆直伤；双方受23＋4X直伤。")]
        [TestCase("消耗全部潮势，允许0；造成30+向下取整的1.5X伤害，最多48。")]
        [TestCase("仅生命不高于最大生命40%时可用；支付3生命，至少留1；恢复2能量。同名所有副本共享每回合一次。")]
        [TestCase("150米内；获得3发附火，上限4；命中后灼烧6×2回合；持续至第2次后续己方回合开始。")]
        public void FormattingPreservesEveryNumberAndIsIdempotent(string source)
        {
            string formatted = CoreBattleCardText.FormatEffects(source);
            CollectionAssert.AreEqual(Numbers(source), Numbers(formatted), formatted);
            Assert.That(CoreBattleCardText.FormatEffects(formatted), Is.EqualTo(formatted));
        }

        [Test]
        public void PercentagesRemainPercentagesRatherThanFlatPointAmounts()
        {
            const string source = "伤害+50%；伤害额外+12.5%；下次炮击伤害+X%；额外+25%格挡；恢复已失生命的30%，上限8。";
            string formatted = Compact(CoreBattleCardText.FormatEffects(source));
            CollectionAssert.AreEqual(Percentages(source), Percentages(formatted), formatted);
            Assert.That(formatted, Does.Not.Contain("点%").And.Not.Contain("层%"));
            CollectionAssert.AreEqual(Numbers(source), Numbers(formatted));
        }

        [Test]
        public void FormulasRangesAndTriggerConditionsKeepTheirMeaning()
        {
            const string source = "4格曼哈顿范围，每一格须合法；造成6＋2×所选潮势炮击伤害；" +
                "消耗0～4潮势；消耗1火药、1炮弹；目标在火场上时先移除最多6格挡。" +
                "同名每回合一次，不叠加；最迟于目标下个回合结束失效。";
            string formatted = Compact(CoreBattleCardText.FormatEffects(source));
            foreach (string rule in new[] { "4格曼哈顿范围，每一格须合法", "6＋2×所选潮势", "0～4", "消耗1火药、1炮弹",
                "目标在火场上时先移除最多", "同名每回合一次", "不叠加", "最迟于目标下个回合结束失效" })
                Assert.That(formatted, Does.Contain(rule), rule);
            CollectionAssert.AreEqual(Numbers(source), Numbers(formatted));
        }

        [Test]
        public void FutureBonusesStayScopedToTheFutureAttack()
        {
            string formatted = Compact(CoreBattleCardText.FormatEffects(
                "下一次雷霆攻击伤害+8，不增加连锁；本回合下一张即时奥术攻击追加其直接伤害的50%，向下取整，最多18；不叠加。"));
            Assert.That(formatted, Does.Contain("下一次雷霆攻击伤害提高8点"));
            Assert.That(formatted, Does.Contain("不增加连锁"));
            Assert.That(formatted, Does.Contain("本回合下一张即时奥术攻击追加其直接伤害的50%"));
            Assert.That(formatted, Does.Contain("向下取整，最多18"));
            Assert.That(formatted, Does.Contain("不叠加"));
        }

        [Test]
        public void ConditionalBonusesAreNotRewordedAsSeparateDamageEvents()
        {
            const string source = "出牌前每有1层自燃再+2，最多+6；" +
                "支付前己方船体不高于最大船体40%时额外+12；第二击+4；" +
                "下次炮击额外伤害+5；消耗1法力，伤害额外+9。";
            string formatted = Compact(CoreBattleCardText.FormatEffects(source));
            foreach (string phrase in new[] { "每有1层自燃伤害提高2点", "40%时伤害提高12点",
                "第二击伤害提高4点", "下次炮击伤害提高5点", "消耗1点法力，伤害提高9点" })
                Assert.That(formatted, Does.Contain(phrase));
            Assert.That(formatted, Does.Not.Contain("额外造成"));
            CollectionAssert.AreEqual(Numbers(source), Numbers(formatted));
            Assert.That(Compact(CoreBattleCardText.FormatEffects(formatted)), Is.EqualTo(formatted));
            Assert.That(Compact(CoreBattleCardText.FormatEffects("命中后额外造成4伤害。")),
                Is.EqualTo("命中后额外造成4点伤害。"), "An actual separate damage event keeps its wording.");
        }

        [Test]
        public void SupplyPreviewsUseTheSameHealingRepairAndCostVocabulary()
        {
            var session = new CoreVoyageSession();
            var card = new CoreBattleCard("wording_supply_preview", "预览", "恢复", 1,
                0, 0, 6, 0, 0, "恢复6当前阶段生命。",
                NavalBattleStages.Preparation | NavalBattleStages.Naval | NavalBattleStages.Boarding);
            int medicine = session.Cargo.Medicine, wood = session.Cargo.Wood;
            string healing = Compact(session.BattleCardMedicineDescription(card, NavalBattleStage.Boarding, true));
            string repair = Compact(session.BattleCardWoodDescription(card, NavalBattleStage.Naval, true));
            Assert.That(healing, Does.Contain("恢复2点生命值").And.Contain("消耗1药物额外恢复4点生命值").And.Not.Contain("预览不消耗"));
            Assert.That(repair, Does.Contain("修复2点船体").And.Contain("消耗1木板额外修复4点船体").And.Not.Contain("预览不消耗"));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine));
            Assert.That(session.Cargo.Wood, Is.EqualTo(wood));
        }

        [Test]
        public void DrawsRestrictedToTheCurrentStageRetainThatRestriction()
        {
            string formatted = CoreBattleCardText.FormatEffects("抽2张当前阶段牌。");
            Assert.That(formatted, Is.EqualTo("抽 2 张当前阶段牌。"));
            Assert.That(formatted, Does.Not.Contain("张牌当前"));
            Assert.That(CoreBattleCardText.FormatEffects(formatted), Is.EqualTo(formatted));
        }

        [Test]
        public void AuthoredTemplatesStayRawWhileEveryUpgradeRendersItsActualValues()
        {
            const string authored = "格挡 +{block}；保留当前航向与距离。";
            var session = new CoreVoyageSession();
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(value => CoreVoyageSession.CardLibrary[value].Id == "card_evasive_maneuver");
            int[] expectedBlock = { 5, 7, 9 };
            Assert.That(session.GetCardMaximumUpgradeLevel(index), Is.EqualTo(expectedBlock.Length - 1));
            for (int level = 0; level < expectedBlock.Length; level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                Assert.That(card.DescriptionTemplate, Is.EqualTo(authored), "The renderer must not rewrite the authored template.");
                Assert.That(card.Block, Is.EqualTo(expectedBlock[level]));
                Assert.That(card.Description, Does.Contain("获得 " + expectedBlock[level] + " 点格挡"));
                Assert.That(card.Description, Does.Not.Contain("保留当前航向与距离。"));
            }
        }

        [Test]
        public void EveryPublishedCardStageAndLegalUpgradeUsesStableUnifiedWording()
        {
            var session = new CoreVoyageSession();
            int identities = 0, records = 0;
            var numbers = new HashSet<int>();
            foreach (int index in Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count))
            {
                CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
                if (!Published(original)) continue;
                identities++;
                Assert.That(numbers.Add(CoreBattleCardNumbering.Number(original)), Is.True, original.Id);
                foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                {
                    if (!original.IsAllowedInStage(stage)) continue;
                    for (int level = 0; level <= session.GetCardMaximumUpgradeLevel(index); level++)
                    {
                        CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                        if (stage == NavalBattleStage.Boarding) card = CoreBoardingCardCatalog.ForBoarding(card);
                        string context = original.Id + " " + stage + " +" + level;
                        records++;
                        AssertUnifiedRenderedWording(card.Description, context + " definition");
                        AssertUnifiedRenderedWording(session.BattleCardFaceDescription(card, stage), context + " face");
                        AssertUnifiedRenderedWording(session.BattleCardEffectDescription(card, stage), context + " effect");
                    }
                }
            }
            Assert.That(identities, Is.EqualTo(315));
            Assert.That(records, Is.EqualTo(1023));
        }

        private static void AssertUnifiedRenderedWording(string description, string context)
        {
            Assert.That(description, Is.Not.Empty, context);
            Assert.That(description, Does.Not.Contain("{").And.Not.Contain("}"), context);
            Assert.That(Regex.IsMatch(description, @"格挡\s*[+＋]"), Is.False, context);
            Assert.That(Regex.IsMatch(description, @"获得\s*\d+\s*(?:普通|蓄潮|基础)?格挡"), Is.False, context);
            Assert.That(Regex.IsMatch(description, @"(?:恢复(?:当前阶段生命|船员(?:生命)?)|修复船体)\s*\d"), Is.False, context);
            Assert.That(description, Does.Not.Contain("张牌当前阶段"), context);
            foreach (string commonRule in new[] { "后续费用", "每一格须合法", "曼哈顿范围", "放置本身不触发",
                "单结算链最多一次场地推动", "用于海图移动或转向", "开炮另付弹药并清空增益" })
                Assert.That(description, Does.Not.Contain(commonRule), context);
            Assert.That(Regex.IsMatch(description, @"[，；。]\s*[，；。]|^[，；。]|[，；]$"), Is.False, context + " orphan separator");
            Assert.That(CoreBattleCardText.FormatEffects(description), Is.EqualTo(description), context);
        }

        private static bool Published(CoreBattleCard card) => card.AllowedStages != NavalBattleStages.None &&
            !CoreBattleCardAliases.IsAlias(card.Id) && !(card.AllowedStages == NavalBattleStages.Boarding &&
            card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 && CoreBoardingCardCatalog.IsLegacyAlias(card));

        private static string Compact(string value) => Regex.Replace(value, @"\s+", "");
        private static string[] Numbers(string value) => Regex.Matches(value, @"\d+(?:\.\d+)?")
            .Cast<Match>().Select(match => match.Value).ToArray();
        private static string[] Percentages(string value) => Regex.Matches(value, @"(?:\d+(?:\.\d+)?|X)%")
            .Cast<Match>().Select(match => match.Value).ToArray();
    }
}
