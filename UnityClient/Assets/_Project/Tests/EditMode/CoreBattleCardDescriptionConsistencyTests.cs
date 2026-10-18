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
                Assert.That(levels[level].Energy, Is.EqualTo(original.Energy), "Upgrades preserve energy costs.");
                Assert.That(levels[level].PowderCost, Is.EqualTo(original.PowderCost), "Upgrades preserve powder costs.");
                Assert.That(levels[level].CannonballCost, Is.EqualTo(original.CannonballCost), "Upgrades preserve cannonball costs.");
            }

            if (original.BoardingSpec == null)
            {
                Assert.That(WithoutNumbers(levels[1].Description), Is.EqualTo(WithoutNumbers(original.Description)),
                    original.Id + " first upgrade must change values without rewriting the sentence or its hard breaks.");
                Assert.That(WithoutNumbers(levels[2].Description), Is.EqualTo(WithoutNumbers(levels[1].Description)),
                    original.Id + " second upgrade must preserve wording while scaling direct values.");
            }

            string finalDescription = levels[3].Description;
            if (original.Draw > 0 && (original.BoardingSpec == null || original.DescriptionTemplate.Contains("{draw}")))
            {
                AssertDrawText(original, finalDescription, levels[3].Draw);
            }

            if (original.BoardingSpec == null)
                Assert.That(WithoutNumbers(finalDescription), Is.EqualTo(WithoutNumbers(original.Description)),
                    original.Id + " third upgrade must preserve the existing wording, order, punctuation and hard breaks.");
        }

        [TestCaseSource(nameof(BoardingCardCases))]
        public void EveryBoardingCard_PreservesItsDynamicWordingAndLineBreaksAcrossUpgrades(int index, int medicine)
        {
            CoreVoyageSession session = BoardingPreviewSession(medicine);
            CoreBattleCard original = CoreBoardingCardCatalog.ForBoarding(session.GetBattleCardAtLevel(index, 0));
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

            if (original.BoardingSpec == null)
            {
                Assert.That(WithoutNumbers(descriptions[1]), Is.EqualTo(WithoutNumbers(descriptions[0])),
                    original.Id + " first upgrade must preserve the dynamic description's wording, order and hard breaks.");
                Assert.That(WithoutNumbers(descriptions[2]), Is.EqualTo(WithoutNumbers(descriptions[1])),
                    original.Id + " second upgrade must preserve dynamic wording.");
            }

            string finalDescription = descriptions[3];
            if (original.Draw > 0 && (original.BoardingSpec == null || original.DescriptionTemplate.Contains("{draw}")))
            {
                CoreBattleCard finalCard = CoreBoardingCardCatalog.ForBoarding(session.GetBattleCardAtLevel(index, 3));
                AssertDrawText(original, finalDescription, finalCard.Draw);
            }

            if (original.BoardingSpec == null)
                Assert.That(WithoutNumbers(finalDescription), Is.EqualTo(WithoutNumbers(descriptions[0])),
                    original.Id + " third upgrade must retain the complete original dynamic paragraph and its hard breaks.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine), "Dynamic description previews must not consume medicine.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void Descriptions_UpdateEffectValuesWithoutChangingRangeDurationOrResourceCosts(int level)
        {
            var session = new CoreVoyageSession();
            CoreBattleCard chain = Card(session, "card_chain_shot", level);
            Assert.That(chain.Description, Does.Contain("4 格内：" + chain.Damage + " 点船体伤害；\n消耗 1 火药、1 炮弹。"));

            CoreBattleCard barrage = Card(session, "card_cannon_barrage", level);
            Assert.That(barrage.Description, Does.Contain("4 格内：" + barrage.Damage + " 点船体伤害"));
            Assert.That(barrage.Description, Does.Contain("消耗 1 火药、1 炮弹。"));

            CoreBattleCard parallel = Card(session, "card_parallel_course", level);
            Assert.That(parallel.Id, Is.EqualTo("card_approach"), "The retired parallel card resolves to the canonical maneuver.");
            int maneuverActionPoints = level == 0 ? 1 : 2;
            Assert.That(parallel.NavalActionPointGain, Is.EqualTo(maneuverActionPoints));
            Assert.That(parallel.Description, Is.EqualTo("获得 " + maneuverActionPoints + " 点行动点。"));

            CoreBattleCard sabotage = Card(session, "card_diver_sabotage", level);
            Assert.That(sabotage.Description, Does.Contain("150 米内：敌舰船体 -" + sabotage.Damage + "，无需弹药。"));

            CoreBattleCard fire = Card(session, "card_special_crew_salt_crown_gunner_rokan", level);
            Assert.That(fire.Description, Does.Contain("获得 " + fire.StrategyAmount + " 发烈焰强化，上限 " + fire.StrategyLimit + "；"));
            Assert.That(fire.Description, Does.Contain("炮击造成有效直伤后灼烧 " + fire.BurnDamage + "×2 回合"),
                "Upgrade burn damage, but retain reserve quantity, duration and the requirement to damage through block.");
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
        [TestCase("card_crew_guard", "格挡") ]
        [TestCase("card_crew_rally", "张。") ]
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
                if (definition.Card.Action == NavalBattleCardAction.Navigator)
                {
                    int[] actionPoints = definition.Card.Id == "card_special_crew_bronze_bell_navigator_vick" ||
                        definition.Card.Id == "card_special_crew_gray_gull_navigator_evelyn"
                        ? new[] { 1, 1, 2, 2 } : new[] { 1, 2, 2, 2 };
                    for (int level = 0; level <= 3; level++)
                    {
                        CoreBattleCard card = Card(session, definition.Card.Id, level);
                        Assert.That(card.NavalActionPointGain, Is.EqualTo(actionPoints[level]), definition.Card.Id + " level " + level);
                        Assert.That(card.Description, Does.Contain("获得 " + actionPoints[level] + " 点行动点"),
                            definition.Card.Id + " level " + level);
                    }
                    continue;
                }
                if (definition.Card.Action != NavalBattleCardAction.Sabotage) continue;
                for (int level = 0; level <= 3; level++)
                    Assert.That(Card(session, definition.Card.Id, level).Description, Does.Contain("无需弹药"),
                        definition.Card.Id + " level " + level);
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HealingDescriptions_KeepTheirWordsAndFixedMedicineCostWhileValuesChange(int level)
        {
            CoreVoyageSession session = BoardingPreviewSession(1);

            CoreBattleCard bandage = Card(session, "card_crew_bandage", level);
            int baseHealing = bandage.Repair;
            Assert.That(bandage.Description, Does.Contain("恢复 " + baseHealing + " 生命"));
            string preview = session.BattleCardPortDescription(bandage);
            Assert.That(preview, Does.Contain("恢复 " + baseHealing));
            Assert.That(preview, Does.Contain("无药物恢复2"));
            Assert.That(preview, Does.Contain("使用1药物"));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Rendering a preview must not consume its quoted resource.");
        }

        [TestCase("arcane_04", "即30/38/45/53", 53)]
        [TestCase("arcane_06", "每枚6伤害，总计12/18/24/30", 30)]
        [TestCase("blood_07", "第二击+5，总计30或35", 35)]
        [TestCase("thunder_11", "总计8至24", 18)]
        public void BoardingSavedUpgradeRank_DescriptionsMatchWholeFormulaAndPerHitRounding(string key, string expected, int damage)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(session, new object[] { NavalBattleStage.Boarding });
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 3);
            typeof(CoreSchoolBattleState).GetProperty("ThunderChain").SetValue(session.Battle.Schools, 4);
            session.Battle.EnemyBlock = 0;
            CoreBattleCard card = Card(session, "card_boarding_" + key, 1);
            Assert.That(card.Description, Does.Contain(expected));
            Assert.That(session.GetBoardingCardDamageQuote(card), Is.EqualTo(damage));
        }

        [TestCase("basic_08", 2, 3, 4, 5, 5)]
        [TestCase("fire_09", 1, 3, 4, 4, 4)]
        [TestCase("thunder_03", 3, 1, 1, 1, 2)]
        [TestCase("arcane_09", 2, 3, 4, 5, 5)]
        [TestCase("thunder_08", 2, 3, 4, 5, 5)]
        [TestCase("tide_14", 2, 1, 2, 2, 2)]
        public void BoardingSavedUpgradeRank_DrawEffectsFollowTheirAuthoredCurveAndCap(string key, int maximum,
            int baseDraw, int firstDraw, int secondDraw, int thirdDraw)
        {
            var session = new CoreVoyageSession();
            int[] expected = { baseDraw, firstDraw, secondDraw, thirdDraw };
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard card = Card(session, "card_boarding_" + key, level);
                Assert.That(card.UpgradeLevel, Is.EqualTo(System.Math.Min(level, maximum)), key + " effective rank");
                Assert.That(card.Draw, Is.EqualTo(expected[level]), key + " level " + level);
                if (CoreBoardingCardCatalog.FindByKey(key).DescriptionTemplate.Contains("{draw}"))
                    AssertDrawText(card, card.Description, expected[level]);
                if (key == "arcane_09")
                    Assert.That(card.Description, Does.Contain("查看牌库顶5张").And.Contain("张入手").And.Contain("其余按原顺序放回牌库底"));
                if (key == "thunder_08")
                    Assert.That(card.Description, Does.Contain("出牌前至少3连锁时额外抽1张").And.Contain("不消耗连锁"),
                        "The conditional extra draw and chain threshold stay fixed when the base draw grows.");
            }
        }

        [TestCase("card_load_shot", 3, 1, 1, 1, 2)]
        [TestCase("card_navigator_course", 2, 1, 1, 2, 2)]
        [TestCase("card_special_crew_gray_gull_carpenter_barlow", 3, 1, 1, 1, 2)]
        [TestCase("card_special_crew_twilight_tide_cook_lume", 3, 1, 1, 2, 2)]
        [TestCase("card_special_crew_salt_crown_cook_maz", 2, 2, 3, 4, 4)]
        [TestCase("card_special_crew_gray_gull_cook_pepe", 1, 1, 1, 1, 1)]
        public void FixedDelayedAndEarlyDrawGrowth_UsesIndependentlyAuthoredValues(string id, int maximum,
            int baseDraw, int firstDraw, int secondDraw, int thirdDraw)
        {
            var session = new CoreVoyageSession();
            int[] expected = { baseDraw, firstDraw, secondDraw, thirdDraw };
            for (int level = 0; level <= 3; level++)
            {
                CoreBattleCard card = Card(session, id, level);
                Assert.That(card.UpgradeLevel, Is.EqualTo(System.Math.Min(level, maximum)), id + " effective rank");
                Assert.That(card.Draw, Is.EqualTo(expected[level]), id + " level " + level);
                AssertDrawText(card, card.Description, expected[level]);
            }
        }

        private static void AssertDrawText(CoreBattleCard original, string description, int expectedDraw)
        {
            string verb = original.BoardingSpec?.Key == "arcane_09" ? "选择" : "抽";
            Assert.That(description, Does.Contain(verb + " " + expectedDraw + " 张"), original.Id);
            Assert.That(Regex.Matches(description, verb + @" \d+ 张").Count, Is.EqualTo(1),
                original.Id + " existing draw/selection effects must be updated rather than duplicated.");
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
