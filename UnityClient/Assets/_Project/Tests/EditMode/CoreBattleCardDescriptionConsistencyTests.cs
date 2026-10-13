using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleCardDescriptionConsistencyTests
    {
        private static IEnumerable<TestCaseData> AllCardCases()
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                if (card.AllowedStages == NavalBattleStages.None) continue;
                yield return new TestCaseData(index)
                    .SetName("UpgradeDescriptionsPreserveWordingAndLineBreaks_" + card.Id);
            }
        }

        private static IEnumerable<TestCaseData> BoardingCardCases()
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
            {
                CoreBattleCard card = CoreVoyageSession.CardLibrary[index];
                if (!card.IsAllowedInStage(NavalBattleStage.Boarding)) continue;
                for (int medicine = 0; medicine <= 1; medicine++)
                    yield return new TestCaseData(index, medicine)
                        .SetName("BoardingDescriptionsPreserveWordingAndLineBreaks_" + card.Id + "_Medicine" + medicine);
            }
        }

        [TestCaseSource(nameof(AllCardCases))]
        public void EveryCard_PreservesWordingEffectOrderPunctuationAndLineBreaksAcrossUpgrades(int index)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard original = session.GetBattleCardAtLevel(index, 0);
            var levels = new CoreBattleCard[4];
            for (int level = 0; level <= 3; level++)
            {
                levels[level] = session.GetBattleCardAtLevel(index, level);
                Assert.That(levels[level].Description, Is.Not.Empty, original.Id + " level " + level);
                Assert.That(levels[level].Description, Does.Not.Contain("{").And.Not.Contain("}"),
                    original.Id + " level " + level + " must not show unresolved placeholders.");
            }

            Assert.That(WithoutNumbers(levels[1].Description), Is.EqualTo(WithoutNumbers(original.Description)),
                original.Id + " first upgrade must change values without rewriting the sentence or its hard breaks.");
            Assert.That(WithoutNumbers(levels[2].Description), Is.EqualTo(WithoutNumbers(levels[1].Description)),
                original.Id + " second upgrade must preserve wording while scaling direct values.");

            string finalDescription = levels[3].Description;
            if (original.Draw > 0)
            {
                Assert.That(finalDescription, Does.Contain("抽 " + (original.Draw + 1) + " 张"), original.Id);
                Assert.That(Regex.Matches(finalDescription, @"抽 \d+ 张").Count, Is.EqualTo(1),
                    original.Id + " existing draw effects must be updated rather than duplicated.");
            }

            Assert.That(WithoutNumbers(finalDescription), Is.EqualTo(WithoutNumbers(original.Description)),
                original.Id + " third upgrade must preserve the existing wording, order, punctuation and hard breaks.");
        }

        [TestCaseSource(nameof(BoardingCardCases))]
        public void EveryBoardingCard_PreservesItsDynamicWordingAndLineBreaksAcrossUpgrades(int index, int medicine)
        {
            CoreVoyageSession session = BoardingPreviewSession(medicine);
            CoreBattleCard original = session.GetBattleCardAtLevel(index, 0);
            var descriptions = new string[4];
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, level);
                CoreBattleCardEffects effects = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                Assert.That(effects.OrdinaryCrew, Is.EqualTo(28), "Every level must use the same crew snapshot.");
                descriptions[level] = session.BattleCardEffectDescription(card, NavalBattleStage.Boarding);
                Assert.That(descriptions[level], Is.Not.Empty, original.Id + " level " + level);
                Assert.That(descriptions[level], Does.Not.Contain("{").And.Not.Contain("}"),
                    original.Id + " dynamic level " + level + " must not show unresolved placeholders.");
            }

            Assert.That(WithoutNumbers(descriptions[1]), Is.EqualTo(WithoutNumbers(descriptions[0])),
                original.Id + " first upgrade must preserve the dynamic description's wording, order and hard breaks.");
            Assert.That(WithoutNumbers(descriptions[2]), Is.EqualTo(WithoutNumbers(descriptions[1])),
                original.Id + " second upgrade must preserve dynamic wording.");

            string finalDescription = descriptions[3];
            if (original.Draw > 0)
            {
                Assert.That(finalDescription, Does.Contain("抽 " + (original.Draw + 1) + " 张"), original.Id);
                Assert.That(Regex.Matches(finalDescription, @"抽 \d+ 张").Count, Is.EqualTo(1),
                    original.Id + " existing dynamic draw effects must not be duplicated.");
            }

            Assert.That(WithoutNumbers(finalDescription), Is.EqualTo(WithoutNumbers(descriptions[0])),
                original.Id + " third upgrade must retain the complete original dynamic paragraph and its hard breaks.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine), "Dynamic description previews must not consume medicine.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Descriptions_UpdateEffectValuesWithoutChangingRangeDurationOrResourceCosts(int level)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard chain = Card(session, "card_chain_shot", level);
            Assert.That(chain.Description, Does.Contain("350 米内：" + chain.Damage + " 点船体伤害；\n消耗 1 火药、1 炮弹。"));

            CoreBattleCard barrage = Card(session, "card_cannon_barrage", level);
            Assert.That(barrage.Description, Does.Contain("350 米内：" + barrage.Damage + " 点船体伤害"));
            Assert.That(barrage.Description, Does.Contain("消耗 1 火药、1 炮弹。"));

            CoreBattleCard parallel = Card(session, "card_parallel_course", level);
            Assert.That(parallel.Description, Does.Contain("靠近 " + parallel.MovementMeters + " 米并转为平行；\n平行且距离不超过 60 米可接舷。"),
                "Movement upgrades must not double the separate boarding distance requirement.");

            CoreBattleCard sabotage = Card(session, "card_diver_sabotage", level);
            Assert.That(sabotage.Description, Does.Contain("150 米内：敌舰船体 -" + sabotage.Damage + "，无需弹药。"));

            CoreBattleCard fire = Card(session, "card_special_crew_salt_crown_gunner_rokan", level);
            Assert.That(fire.Description, Does.Contain("获得 " + fire.StrategyAmount + " 发烈焰强化，最多 " + fire.StrategyLimit + " 发；"));
            Assert.That(fire.Description, Does.Contain("炮击穿过格挡造成伤害后点燃敌舰，连续 2 回合灼烧 " + fire.BurnDamage + " 船体"),
                "Upgrade damage and reserves, but retain the duration and the requirement to damage through block.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Descriptions_UpdateStrategyValuesAndShowExistingCircleGuardAtEveryLevel(int level)
        {
            var session = new CoreVoyageSession();
            int polish = Card(session, "card_deck_polish", level).StrategyAmount;
            int polishLimit = Card(session, "card_deck_polish", level).StrategyLimit;
            int load = Card(session, "card_load_shot", level).StrategyAmount;
            int loadLimit = Card(session, "card_load_shot", level).StrategyLimit;
            int precision = Card(session, "card_gunner_precision", level).StrategyAmount;
            int circleGuard = Card(session, "card_circle_enemy", level).Block;
            Assert.That(Card(session, "card_deck_polish", level).Description,
                Does.Contain("本场船体防御牌额外 +" + polish + " 格挡，可叠至 +" + polishLimit + "。"));
            Assert.That(Card(session, "card_load_shot", level).Description,
                Does.Contain("下次炮击 +" + load + "，最多 +" + loadLimit + "；"));
            Assert.That(Card(session, "card_load_shot", level).Description, Does.Contain("消耗 1 火药"));
            Assert.That(Card(session, "card_load_shot", level).Description, Does.Contain("开炮另付弹药并清空增益。"));
            Assert.That(Card(session, "card_gunner_precision", level).Description,
                Does.Contain("下次炮击 +" + precision + "，最多 +" + loadLimit + "；"));
            Assert.That(Card(session, "card_gunner_precision", level).Description, Does.Contain("消耗 1 火药"));
            Assert.That(Card(session, "card_circle_enemy", level).Description, Does.Contain("格挡 +" + circleGuard),
                "Circle already grants guard at level zero; its description must not imply that upgrading adds a new effect.");
        }

        [TestCase("card_evasive_maneuver", "保留当前航向与距离。")]
        [TestCase("card_brace", "准备阶段的格挡保留到首轮炮战。")]
        [TestCase("card_deck_polish", "本场船体防御牌")]
        [TestCase("card_load_shot", "开炮另付弹药并清空增益。")]
        [TestCase("card_crew_guard", "船员格挡")]
        [TestCase("card_crew_rally", "张白刃战牌。")]
        [TestCase("card_cook_rations", "炮战修复")]
        [TestCase("card_cook_rations", "白刃战恢复")]
        [TestCase("card_diver_sabotage", "无需弹药。")]
        [TestCase("card_component_bow", "无需弹药。")]
        public void EveryLevel_RetainsCardSpecificRules(string id, string rule)
        {
            var session = new CoreVoyageSession();
            for (int level = 0; level <= 3; level++)
                Assert.That(Card(session, id, level).Description, Does.Contain(rule), id + " level " + level);
        }

        [Test]
        public void EverySpecialNavigatorAndSabotageCard_RetainsItsDistinctRuleAtEveryLevel()
        {
            var session = new CoreVoyageSession();
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                if (definition.Card.SchoolId.Length > 0 && definition.Card.Action == NavalBattleCardAction.Sabotage)
                {
                    for (int level = 0; level <= 3; level++)
                    {
                        CoreBattleCard card = Card(session, definition.Card.Id, level);
                        Assert.That(card.Description, Does.Contain("150 米内"));
                        if (card.PowderCost > 0)
                            Assert.That(card.Description, Does.Contain("消耗 " + card.PowderCost + " 火药"),
                                "Fire sabotage consumes its authored ignition powder despite not firing a cannonball.");
                        Assert.That(card.CannonballCost, Is.Zero);
                    }
                    continue;
                }
                string rule = definition.Card.Action == NavalBattleCardAction.Navigator ? "可从尾舷接舷" :
                    definition.Card.Action == NavalBattleCardAction.Sabotage ? "无需弹药" : null;
                if (rule == null) continue;
                for (int level = 0; level <= 3; level++)
                    Assert.That(Card(session, definition.Card.Id, level).Description, Does.Contain(rule),
                        definition.Card.Id + " level " + level);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HealingDescriptions_KeepTheirWordsAndFixedMedicineCostWhileValuesChange(int level)
        {
            CoreVoyageSession session = BoardingPreviewSession(1);

            CoreBattleCard bandage = Card(session, "card_crew_bandage", level);
            int baseHealing = bandage.Repair;
            Assert.That(bandage.Description, Does.Contain("恢复 " + baseHealing + " 点己方船员生命。"));
            string preview = session.BattleCardPortDescription(bandage);
            Assert.That(preview, Does.Contain("有药完整 " + baseHealing));
            Assert.That(preview, Does.Contain("无药保底 2"));
            Assert.That(preview, Does.Contain("有效治疗消耗 1 药品，预览不消耗。"));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Rendering a preview must not consume its quoted resource.");
        }

        private static string WithoutNumbers(string description) => Regex.Replace(description, @"\d+", "#");

        private static CoreVoyageSession BoardingPreviewSession(int medicine)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.PortWarehouse.Restore("v2"), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.ConfigureDepartureCrew(28, 0);
            session.Cargo.Medicine = medicine;
            return session;
        }

        private static CoreBattleCard Card(CoreVoyageSession session, string id, int level)
        {
            for (int index = 0; index < CoreVoyageSession.CardLibrary.Count; index++)
                if (CoreVoyageSession.CardLibrary[index].Id == id)
                    return session.GetBattleCardAtLevel(index, level);
            Assert.Fail("Card not found: " + id);
            return null;
        }
    }
}
