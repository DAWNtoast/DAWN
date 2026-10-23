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

            if (original.Id == "card_school_fire_b01")
            {
                foreach (CoreBattleCard card in levels)
                {
                    Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.FireLoad));
                    string expected = "获得 " + card.SchoolEffect.Secondary + " 发附火";
                    if (card.SchoolEffect.Power != 4)
                        expected += "（燃烧 " + card.SchoolEffect.Power + " 层）";
                    Assert.That(card.Description, Is.EqualTo(expected + "。"),
                        card.Id + " rank " + card.UpgradeLevel + " shows the enhanced payload instead of repeating the shared four-stack rule.");
                }
                return;
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
            Assert.That(chain.Description, Does.Contain("范围 1-4 格：造成 " + chain.Damage + " 点船体伤害。")
                .And.Not.Contain("消耗"));
            Assert.That(chain.PowderCost, Is.EqualTo(1));
            Assert.That(chain.CannonballCost, Is.EqualTo(1));

            CoreBattleCard barrage = Card(session, "card_cannon_barrage", level);
            Assert.That(barrage.Description, Does.Contain("范围 1-4 格：造成 " + barrage.Damage + " 点船体伤害"));
            Assert.That(barrage.Description, Does.Not.Contain("消耗"));
            Assert.That(barrage.PowderCost, Is.EqualTo(1));
            Assert.That(barrage.CannonballCost, Is.EqualTo(1));

            CoreBattleCard parallel = Card(session, "card_parallel_course", level);
            Assert.That(parallel.Id, Is.EqualTo("card_approach"), "The retired parallel card resolves to the canonical maneuver.");
            int maneuverActionPoints = level == 0 ? 1 : 2;
            Assert.That(parallel.NavalActionPointGain, Is.EqualTo(maneuverActionPoints));
            Assert.That(parallel.Description, Is.EqualTo("获得 " + maneuverActionPoints + " 点行动点。"));

            CoreBattleCard sabotage = Card(session, "card_diver_sabotage", level);
            Assert.That(sabotage.Description, Does.Contain("范围 1-2 格：造成 " + sabotage.Damage + " 点船体伤害。"));
            Assert.That(sabotage.PowderCost + sabotage.CannonballCost, Is.Zero);

            CoreBattleCard fire = Card(session, "card_special_crew_salt_crown_gunner_rokan", level);
            Assert.That(fire.Description, Does.Contain("获得 " + fire.StrategyAmount + " 发烈焰强化，最多储备 " + fire.StrategyLimit + " 发；"));
            Assert.That(fire.Description, Does.Contain("炮击造成有效直伤后，施加 " + fire.BurnDamage + " 层燃烧"),
                "Upgrades scale burning stacks while preserving reserve quantity and the requirement to damage through block.");
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
                Does.Contain("额外格挡：" + polish + "。"));
            Assert.That(Card(session, "card_load_shot", level).Description,
                Does.Contain("额外装药：" + load + "；"));
            Assert.That(Card(session, "card_load_shot", level).Description, Does.Not.Contain("消耗"));
            Assert.That(Card(session, "card_load_shot", level).PowderCost, Is.EqualTo(1));
            Assert.That(Card(session, "card_load_shot", level).Description, Does.Not.Contain("开炮另付弹药并清空增益。"));
            Assert.That(Card(session, "card_gunner_precision", level).Description,
                Does.Contain("额外装药：" + precision + "；"));
            Assert.That(Card(session, "card_gunner_precision", level).Description, Does.Not.Contain("消耗"));
            Assert.That(Card(session, "card_gunner_precision", level).PowderCost, Is.EqualTo(1));
            Assert.That(Card(session, "card_circle_enemy", level).Description, Does.Contain("获得 " + circleGuard + " 点格挡"),
                "Circle already grants guard at level zero; its description must not imply that upgrading adds a new effect.");
        }

        [TestCase("card_evasive_maneuver", "格挡")]
        [TestCase("card_brace", "格挡")]
        [TestCase("card_deck_polish", "额外格挡：")]
        [TestCase("card_load_shot", "额外装药：")]
        [TestCase("card_crew_guard", "格挡") ]
        [TestCase("card_crew_rally", "张牌。") ]
        [TestCase("card_cook_rations", "海战：修复")]
        [TestCase("card_cook_rations", "接舷战：恢复")]
        [TestCase("card_diver_sabotage", "范围 1-2 格：")]
        [TestCase("card_component_bow", "范围 1-2 格：")]
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
                        Assert.That(card.Description, Does.Contain("范围 1-2 格："));
                        if (card.PowderCost > 0)
                        {
                            Assert.That(card.Description, Does.Not.Contain("消耗 " + card.PowderCost + " 火药"),
                                "The powder icon owns the fixed ignition cost.");
                            Assert.That(card.PowderCost, Is.EqualTo(definition.Card.PowderCost),
                                "Fire sabotage still pays its authored powder despite not firing a cannonball.");
                        }
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
                {
                    CoreBattleCard card = Card(session, definition.Card.Id, level);
                    Assert.That(card.Description, Does.Contain("范围 1-2 格：").And.Not.Contain("无需弹药"),
                        definition.Card.Id + " level " + level);
                    Assert.That(card.PowderCost + card.CannonballCost, Is.Zero);
                }
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void HealingDescriptions_KeepTheirWordsAndFixedMedicineCostWhileValuesChange(int level)
        {
            CoreVoyageSession session = BoardingPreviewSession(1);

            CoreBattleCard bandage = Card(session, "card_crew_bandage", level);
            int baseHealing = System.Math.Min(CoreBattleCardEffects.UnprovisionedRepair, bandage.Repair);
            int extraHealing = bandage.Repair - baseHealing;
            Assert.That(bandage.Description, Does.Contain("恢复 " + baseHealing + " 点生命值")
                .And.Contain("消耗 1 药物额外恢复 " + extraHealing + " 点生命值"));
            string preview = session.BattleCardPortDescription(bandage);
            Assert.That(preview, Does.Contain("恢复 " + baseHealing));
            Assert.That(preview, Does.Contain("消耗 1 药物额外恢复 " + extraHealing + " 点生命值"));
            Assert.That(baseHealing + extraHealing, Is.EqualTo(session.GetBattleCardEffects(bandage, NavalBattleStage.Boarding).FullRepair));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1), "Rendering a preview must not consume its quoted resource.");
        }

        [TestCase("arcane_04", "(24+6X)×1.25（结果四舍五入）", 53)]
        [TestCase("arcane_06", "造成 6 点伤害 2+X 次", 30)]
        [TestCase("blood_07", "第二击伤害提高 5 点", 35)]
        [TestCase("thunder_11", "(3+出牌前连锁层数)×1.33（结果四舍五入）", 18)]
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
            Assert.That(card.Description, Does.Contain(expected).And.Not.Contain("总计")
                .And.Not.Match(@"即\s*\d"));
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
                    Assert.That(card.Description, Does.Contain("查看牌库顶 5 张牌").And.Contain("张加入手牌").And.Contain("其余按原顺序放回牌库底"));
                if (key == "thunder_08")
                    Assert.That(card.Description, Does.Contain("连锁 3：额外抽 1 张牌"),
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
            string cardNoun = original.BoardingSpec?.Key == "arcane_09" ? " 张加入手牌" : @" 张(?:当前阶段|本阶段)?牌";
            Assert.That(Regex.IsMatch(description, @"(?<!额外)" + verb + " " + expectedDraw + cardNoun),
                Is.True, original.Id + " expected main draw/selection amount " + expectedDraw + ": " + description);
            Assert.That(Regex.Matches(description, @"(?<!额外)" + verb + @" \d+" + cardNoun).Count, Is.EqualTo(1),
                original.Id + " existing draw/selection effects must be updated rather than duplicated.");
        }

        private static string WithoutNumbers(string description)
        {
            // An upgrade that raises repair above the free two-point base gains
            // a paid-supply clause. Its arithmetic is verified separately; every
            // other word, clause and hard break must still remain unchanged.
            string withoutSupplyBonus = Regex.Replace(description,
                @"；\s*消耗\s*1\s*(?:药物|木板)额外(?:恢复|修复)\s*\d+\s*点(?:生命值|船体)", string.Empty);
            return Regex.Replace(withoutSupplyBonus, @"\d+", "#");
        }

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
