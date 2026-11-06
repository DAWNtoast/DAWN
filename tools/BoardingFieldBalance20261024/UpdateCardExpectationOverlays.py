from pathlib import Path

ROOT = Path('D:/Game')
SRC = ROOT / 'UnityClient/Assets/_Project/Tests/EditMode'
OUT = ROOT / 'outputs/boarding-field-balance-20261024/TestsOverlay'
OUT.mkdir(exist_ok=True)

def edit(name, replacements):
    path = SRC / (name + '.cs')
    text = path.read_text(encoding='utf-8-sig')
    for before, after in replacements:
        if before not in text:
            raise ValueError(f'{name}: missing {before!r}')
        text = text.replace(before, after)
    (OUT / path.name).write_text(text, encoding='utf-8-sig', newline='\r\n')

edit('BattleCardUpgradeTests', [
    ('Assert.That(effective.Repair, Is.EqualTo(Scaled(original.Repair, profile.RepairMultiplier(effectiveLevel))), original.Id);',
     '''int expectedRepair = original.BoardingSpec?.Key == "basic_07" ? new[] { 7, 8, 10, 12 }[effectiveLevel]
                    : original.BoardingSpec?.Key == "tide_12" ? new[] { 11, 13, 15, 18 }[effectiveLevel]
                    : Scaled(original.Repair, profile.RepairMultiplier(effectiveLevel));
                Assert.That(effective.Repair, Is.EqualTo(expectedRepair), original.Id);''')])

edit('CoreBoardingCardCatalogTests', [
    ('"basic_01", 1, 8,', '"basic_01", 1, 16,'),
    ('"basic_02", 2, 20,', '"basic_02", 2, 40,'),
    ('"basic_03", 3, 35,', '"basic_03", 3, 70,'),
    ('"basic_04", 1, 0, 4,', '"basic_04", 1, 0, 10,'),
    ('"basic_05", 2, 0, 10,', '"basic_05", 2, 0, 24,'),
    ('"basic_06", 1, 0, 0, 6,', '"basic_06", 1, 0, 0, 12,'),
    ('"basic_07", 2, 0, 0, 5,', '"basic_07", 2, 0, 0, 7,'),
    ('"basic_11", 1, 4,', '"basic_11", 1, 8,'),
    ('"basic_12", 1, 0, 2,', '"basic_12", 1, 0, 4,'),
    ('"basic_13", 1, 12,', '"basic_13", 1, 24,'),
    ('FindByKey("fire_05").Repair, Is.EqualTo(10)', 'FindByKey("fire_05").Repair, Is.EqualTo(20)'),
    ('FindByKey("arcane_05").Repair, Is.EqualTo(13)', 'FindByKey("arcane_05").Repair, Is.EqualTo(26)'),
    ('FindByKey("curse_12").Repair, Is.EqualTo(13)', 'FindByKey("curse_12").Repair, Is.EqualTo(26)'),
    ('FindByKey("curse_15").Block, Is.EqualTo(10)', 'FindByKey("curse_15").Block, Is.EqualTo(20)'),
    ('FindByKey("tide_11").Repair, Is.EqualTo(10)', 'FindByKey("tide_11").Repair, Is.EqualTo(20)'),
    ('FindByKey("tide_12").Repair, Is.EqualTo(8)', 'FindByKey("tide_12").Repair, Is.EqualTo(11)'),
    ('FindByKey("blood_02").Damage, Is.EqualTo(34)', 'FindByKey("blood_02").Damage, Is.EqualTo(68)'),
])
edit('CoreCardUpgradeLimitTests', [
    ('[TestCase(0, 4, 1)] [TestCase(1, 5, 1)] [TestCase(2, 7, 1)] [TestCase(3, 9, 2)]', '[TestCase(0, 8, 1)] [TestCase(1, 10, 1)] [TestCase(2, 14, 1)] [TestCase(3, 18, 2)]'),
    ('[TestCase(0, 1, 8)] [TestCase(1, 1, 10)] [TestCase(2, 1, 14)] [TestCase(3, 1, 14)]', '[TestCase(0, 1, 16)] [TestCase(1, 1, 20)] [TestCase(2, 1, 28)] [TestCase(3, 1, 28)]'),
    ('[TestCase(0, 8)] [TestCase(1, 10)] [TestCase(2, 14)] [TestCase(3, 14)]', '[TestCase(0, 6)] [TestCase(1, 7)] [TestCase(2, 9)] [TestCase(3, 9)]'),
    ('        [TestCase("card_boarding_arcane_03")]\n', ''),
    ('[TestCase("card_boarding_arcane_03", 0)]', '[TestCase("card_boarding_arcane_03", 2)]'),
    ('[TestCase("card_boarding_fire_04", 3)]', '[TestCase("card_boarding_arcane_03", 2)]\n        [TestCase("card_boarding_curse_02", 2)]\n        [TestCase("card_boarding_fire_04", 3)]'),
    ('[TestCase("card_boarding_basic_11", 3)]\n        public void Purchases', '[TestCase("card_boarding_basic_11", 3)]\n        [TestCase("card_boarding_arcane_03", 2)]\n        [TestCase("card_boarding_curse_02", 2)]\n        public void Purchases'),
])
edit('CoreArcaneCatalogRevisionTests', [('移除8点格挡，造成10点伤害。', '移除16点格挡，造成20点伤害。')])
edit('CoreCurseCatalogRevisionTests', [
    ('敌方咒印层数≥1才可以使用：造成20+8X点咒术伤害', '敌方咒印层数≥2才可以使用：造成40+8X点咒术伤害'),
    ('敌方咒印层数≥2时，消耗1药物额外恢复11点生命值', '敌方咒印层数≥4时，消耗1药物额外恢复24点生命值'),
    ('敌方咒印层数≥2才可以使用：获得2点能量', '敌方咒印层数≥4才可以使用：获得2点能量'),
    ('敌方咒印层数≥2才可以使用：获得10点格挡', '敌方咒印层数≥4才可以使用：获得20点格挡'),
])
edit('CoreThunderCatalogRevisionTests', [
    ('造成3点伤害2次，出牌前每有一层连锁，本次伤害提高2点。', '造成6点伤害2次，出牌前每有一层连锁，每次伤害提高3点。'),
    ('造成5点伤害；连锁2：敌方下一次攻击伤害降低10点。', '造成10点伤害；连锁2：敌方下一次攻击伤害降低20点。'),
    ('造成20+5X点伤害。', '造成40+7X点伤害。'),
    ('造成16+4X点无视格挡的伤害。', '造成32+6X点无视格挡的伤害。'),
    ('造成28+6X点伤害，连锁10：伤害翻倍，连锁20：伤害翻倍。', '造成56+8X点伤害，连锁10：伤害翻倍，连锁20：伤害翻倍。'),
])
edit('CoreTideCatalogRevisionTests', [('获得15点蓄潮格挡。', '获得30点蓄潮格挡。'), ('造成30+1.5X点伤害。', '造成60+1.5X点伤害。')])
edit('CoreBloodCatalogRevisionTests', [
    ('造成9点伤害；吸血。', '造成18点伤害；吸血。'),
    ('敌方对我方造成伤害时，对其造成8点伤害。', '敌方对我方造成伤害时，对其造成16点伤害。'),
    ('消耗1药物额外恢复4点生命值，绝境：改为额外恢复8点生命值。', '消耗1药物额外恢复10点生命值，绝境：改为额外恢复21点生命值。'),
    ('造成7点伤害；绝境：额外造成10点伤害。', '造成14点伤害；绝境：额外造成20点伤害。'),
    ('获得5点格挡；绝境：改为获得12点格挡。', '获得10点格挡；绝境：改为获得24点格挡。'),
    ('延迟：造成12点伤害；绝境：改为造成22点伤害。', '延迟：造成24点伤害；绝境：改为造成44点伤害。'),
    ('攻击牌额外造成8点伤害。', '攻击牌额外造成16点伤害。'),
    ('消耗1药物额外恢复5点生命值，绝境：改为额外恢复12点生命值。', '消耗1药物额外恢复12点生命值，绝境：改为额外恢复28点生命值。'),
    ('"card_boarding_blood_09", 5, 6, 13', '"card_boarding_blood_09", 5, 12, 26'),
    ('"card_special_crew_broken_anchor_doctor_hector", 4, 7, 16', '"card_special_crew_broken_anchor_doctor_hector", 4, 14, 32'),
])
edit('CoreBattleCardIconCostTextTests', [
    ('造成5点伤害2+X次。', '造成5点伤害4+X次。'),
    ('敌方咒印层数≥1才可以使用', '敌方咒印层数≥2才可以使用'),
    ('"1药物和2点魔力"', '"1药物和4点魔力"'),
    ('"1药物和6点潮势"', '"1药物和12点潮势"'),
    ('"1药物和8点潮势"', '"1药物和16点潮势"'),
    ('敌方咒印层数≥2时', '敌方咒印层数≥4时'),
    ('Not.Contain("消耗2层咒印")', 'Not.Contain("消耗4层咒印")'),
])
edit('CoreBattleCardDescriptionConsistencyTests', [
    ('"(24+6X)×1.25（结果四舍五入）", 53', '"(48+6X)×1.25（结果四舍五入）", 83'),
    ('"造成 6 点伤害 2+X 次", 30', '"造成 6 点伤害 4+X 次", 42'),
    ('"第二击伤害提高 5 点", 35', '"第二击伤害提高 10 点", 70'),
    ('"(3+2×出牌前连锁层数)×1.33（结果四舍五入）", 30', '"(6+3×出牌前连锁层数)×1.33（结果四舍五入）", 48'),
])
edit('CoreBattleCardFaceDescriptionTests', [('[TestCase(1, 40, 6, 1)]', '[TestCase(1, 40, 12, 1)]'), ('[TestCase(1, 7, 6, 1)]', '[TestCase(1, 7, 7, 1)]')])
edit('CoreBattleCardWordingTests', [('1058', '1062')])
edit('CoreCardAuthoredDamageTests', [('new[] { 8, 10, 12, 16 }[rank]', 'new[] { 16, 20, 24, 32 }[rank]')])
edit('CoreArcaneManaRevisionTests', [
    ('Play(s, "card_boarding_arcane_03");\n            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(8));', 'Play(s, "card_boarding_arcane_03");\n            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(11));'),
    ('Play(s, "card_boarding_arcane_10");\n            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(6));', 'Play(s, "card_boarding_arcane_10");\n            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(7));'),
    ('[TestCase("arcane_04", 72)]', '[TestCase("arcane_04", 96)]'),
    ('[TestCase("arcane_06", 50)]', '[TestCase("arcane_06", 60)]'),
    ('[TestCase("arcane_11", 100)]', '[TestCase("arcane_11", 136)]'),
])
edit('CoreBattleStatusPresentationTests', [('weakness.Value, Is.EqualTo("6")', 'weakness.Value, Is.EqualTo("12")'), ('当前伤害 6', '当前伤害 12'), ('Does.Contain("对敌咒印 3"));\n            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("反击"))', 'Does.Contain("对敌咒印 6"));\n            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("反击"))')])

edit('CoreCardAuthoredUpgradeTests', [
    ('int expected = CoreCardUpgradeDesign.ScaleEffect(card, 8, card.UpgradeLevel);', 'int expected = id.EndsWith("fire_03", StringComparison.Ordinal)\n                ? new[] { 6, 7, 9 }[card.UpgradeLevel] : new[] { 16, 20, 28 }[card.UpgradeLevel];'),
    ('            // Only values consumed by real boarding rules qualify as hidden growth.',
     '''            // Observe resources executed outside printed damage/guard channels. Exact values are
            // separately checked by BoardingResourceRanksExecuteTheirAuthoredGain.
            result.Add(CoreBoardingCardBalance.Mana(card.BoardingSpec?.Key, card.UpgradeLevel));
            result.Add(CoreBoardingCardBalance.Marks(card.BoardingSpec?.Key, card.UpgradeLevel));
            result.Add(CoreBoardingCardBalance.Burn(card.BoardingSpec?.Key, card.UpgradeLevel));
            result.Add(card.BoardingSpec?.Key == "fire_06" ? CoreBoardingCardBalance.Burn("fire_06", card.UpgradeLevel, true) : 0);
            result.Add(card.BoardingSpec?.Key == "fire_07" ? CoreBoardingCardBalance.BurnFuel(3, card.UpgradeLevel) : 0);
            // Only values consumed by real boarding rules qualify as hidden growth.'''),
    ('                case "fire_02": return new[] { 4 };\n                case "fire_03": return new[] { 8 };\n                case "fire_04": return new[] { 6 };\n                case "fire_06": return new[] { 3, 5 };\n                case "fire_07": return new[] { 7, 10, 13 };\n', ''),
    ('case "fire_08": return new[] { 6, 10, 14 };', 'case "fire_08": return new[] { 12, 20, 28 };'),
    ('case "arcane_07": return new[] { 8 };', 'case "arcane_07": return new[] { 16 };'),
    ('case "curse_07": return new[] { 3, 9 };', 'case "curse_07": return new[] { 6, 12 };'),
    ('case "curse_08": return new[] { 4, 16, 2, 8 };', 'case "curse_08": return new[] { 8, 20, 4, 10 };'),
    ('case "tide_04": return new[] { 4 };', 'case "tide_04": return new[] { 8 };'),
    ('case "tide_09": return new[] { 16 };', 'case "tide_09": return new[] { 32 };'),
    ('case "tide_10": return new[] { 3 };', 'case "tide_10": return new[] { 6 };'),
    ('case "thunder_04": return new[] { 6 };', 'case "thunder_04": return new[] { 12 };'),
    ('case "thunder_06": return new[] { 8 };', 'case "thunder_06": return new[] { 16 };'),
    ('case "thunder_10": return new[] { 4 };', 'case "thunder_10": return new[] { 8 };'),
    ('case "thunder_12": return new[] { 2, 5 };', 'case "thunder_12": return new[] { 10, 20 };'),
    ('case "blood_08": return new[] { 8 };', 'case "blood_08": return new[] { 16 };'),
    ('case "blood_09": return new[] { 6, 13 };', 'case "blood_09": return new[] { 12, 26 };'),
    ('case "blood_11": return new[] { 17 };', 'case "blood_11": return new[] { 34 };'),
    ('case "blood_12": return new[] { 12 };', 'case "blood_12": return new[] { 24 };'),
    ('case "blood_13": return new[] { 22 };', 'case "blood_13": return new[] { 44 };'),
    ('case "blood_15": return new[] { 8 };', 'case "blood_15": return new[] { 16 };'),
    ('        private static IEnumerable<CoreBattleCard> LiveCards()',
     '''        [TestCase("arcane_01", 0, 2)] [TestCase("arcane_01", 1, 2)] [TestCase("arcane_01", 2, 3)] [TestCase("arcane_01", 3, 4)]
        [TestCase("arcane_02", 0, 2)] [TestCase("arcane_02", 1, 3)] [TestCase("arcane_02", 2, 4)] [TestCase("arcane_02", 3, 4)]
        [TestCase("arcane_03", 0, 6)] [TestCase("arcane_03", 1, 7)] [TestCase("arcane_03", 2, 9)] [TestCase("arcane_03", 3, 9)]
        [TestCase("curse_01", 0, 2)] [TestCase("curse_01", 1, 2)] [TestCase("curse_01", 2, 3)] [TestCase("curse_01", 3, 4)]
        [TestCase("curse_02", 0, 6)] [TestCase("curse_02", 1, 8)] [TestCase("curse_02", 2, 10)] [TestCase("curse_02", 3, 10)]
        [TestCase("curse_03", 0, 2)] [TestCase("curse_03", 1, 3)] [TestCase("curse_03", 2, 4)] [TestCase("curse_03", 3, 4)]
        public void BoardingResourceRanksExecuteTheirAuthoredGain(string key, int rank, int expected)
        {
            CoreVoyageSession session = Start("card_boarding_" + key, rank, true);
            int mana = session.Battle.Schools.Mana;
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            int actual = key.StartsWith("arcane", StringComparison.Ordinal)
                ? session.Battle.Schools.Mana - mana : session.Battle.Boarding.CurseMarks;
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(session.GetBattleCard(Index("card_boarding_" + key)).Description,
                Does.Contain(expected + (key.StartsWith("arcane", StringComparison.Ordinal) ? " 点魔力" : " 层咒印")));
        }

        private static IEnumerable<CoreBattleCard> LiveCards()'''),
])

edit('CoreBattleCardRuleTextBoardingTests', [
    ('"arcane_05", "2魔力"', '"arcane_05", "4点魔力"'),
    ('        [TestCase("curse_12", "2层咒印", "咒印")]\n', ''),
    ('"tide_11", "6潮势"', '"tide_11", "12点潮势"'),
    ('"消耗1药物和8潮势额外获得持续恢复：" +\n                    (CoreCardUpgradeDesign.ScaleEffect(card, 8, card.UpgradeLevel) - 2)', '"消耗1药物和16点潮势额外获得持续恢复：" +\n                    (new[] { 11, 13, 15, 18 }[card.UpgradeLevel] - 2)'),
    ('[TestCase("thunder_13", 50, false)]\n        [TestCase("thunder_14", 40, true)]\n        [TestCase("thunder_15", 64, false)]\n        public void FinishersKeepTheirScaledDamageCapsAndThePiercingException(string key, int baseCap, bool piercing)', '[TestCase("thunder_13", "40+7X", false)]\n        [TestCase("thunder_14", "32+6X", true)]\n        [TestCase("thunder_15", "56+8X", false)]\n        public void FinishersKeepTheirUncappedAuthoredFormulaAndThePiercingException(string key, string formula, bool piercing)'),
    ('                Contains(text, "终结：造成");', '                Contains(text, "造成");'),
    ('                Contains(text, "最多" + CoreCardUpgradeDesign.ScaleEffect(design, baseCap, card.UpgradeLevel));\n                Contains(text, "连锁为0时也可使用");', '                Contains(text, formula);\n                Assert.That(text, Does.Not.Contain("最多"));\n                if (key == "thunder_15") Contains(text, "连锁10：伤害翻倍，连锁20：伤害翻倍");'),
    ('[TestCase("tide_08", 0, "30+向下取整的X×150%")]\n        [TestCase("thunder_02", 14, "最多")]', '[TestCase("tide_08", 0, "60+1.5X")]'),
    ('CurrentFormulasDistinguishUncappedEffectsFromRetainedDamageCaps', 'CurrentFormulasRemainUncappedAcrossUpgrades'),
    ('"thunder_01", true', '"thunder_01", false'),
    ('"thunder_07", true', '"thunder_07", false'),
    ('"thunder_09", true', '"thunder_09", false'),
    ('"thunder_06", true', '"thunder_06", false'),
    ('"thunder_10", true', '"thunder_10", false'),
    ('CurrentStackingRestrictionsSurviveEveryUpgrade', 'CardFacesOmitSharedStackingImplementationRulesAcrossUpgrades'),
    ('"blood_01", 3', '"blood_01", 6'), ('"blood_02", 4', '"blood_02", 8'),
    ('"blood_03", 3', '"blood_03", 6'), ('"blood_04", 3', '"blood_04", 6'), ('"blood_05", 3', '"blood_05", 6'),
    ('ScaleEffect(design, 6, card.UpgradeLevel)', 'ScaleEffect(design, 12, card.UpgradeLevel)'),
    ('ScaleEffect(design, 13, card.UpgradeLevel)', 'ScaleEffect(design, 26, card.UpgradeLevel)'),
    ('Is.EqualTo("咒印伤害额外结算1次。")', 'Is.EqualTo("本场战斗中，咒印消灭阈值降低10个百分点（可叠加）；降至0%时立即消灭敌方。")'),
    ('Contains(Compact(card.Description), "获得" + CoreCardUpgradeDesign.ScaleEffect(card, 6, card.UpgradeLevel) + "点潮势");', 'Assert.That(Compact(card.Description), Is.EqualTo("本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。"));'),
    ('"fire_02", "灼烧"', '"fire_02", "燃烧"'), ('"fire_03", "灼烧"', '"fire_03", "燃烧"'), ('"fire_04", "灼烧"', '"fire_04", "燃烧"'),
    ('"fire_11", "施加3层自燃"', '"fire_11", "对自身施加3层燃烧"'),
    ('"curse_01", "施加1层咒印"', '"curse_01", "咒印"'), ('"curse_02", "施加3层咒印"', '"curse_02", "咒印"'),
    ('        private static IEnumerable<CoreBattleCard> AtEveryLevel(string key)',
     '''        [Test]
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
                Contains(text, card.UpgradeLevel == 0 ? "出牌前每有一层连锁，本次伤害提高2点" : "16+2×出牌前连锁层数");
                Assert.That(text, Does.Not.Contain("最多"));
            }
        }

        private static IEnumerable<CoreBattleCard> AtEveryLevel(string key)'''),
])
status_path = OUT / 'CoreBattleStatusPresentationTests.cs'
status_text = status_path.read_text(encoding='utf-8-sig')
status_text = status_text.replace('        private static CoreVoyageSession Battle(NavalBattleStage stage)', '''        [TestCase("fire_13", 0, "fire_retaliation", true, "每次伤害为当前燃烧层数；", 1)]
        [TestCase("fire_13", 2, "fire_retaliation", true, "每次伤害为当前燃烧层数；", 1)]
        [TestCase("curse_04", 0, "curse_contract", true, "施加 4 层咒印", 1)]
        [TestCase("curse_04", 0, "curse_contract", true, "施加 8 层咒印", 2)]
        [TestCase("curse_05", 0, "curse_link", true, "各给敌方施加 2 层咒印", 1)]
        [TestCase("curse_07", 0, "curse_weakness", false, "伤害减少 12", 1)]
        [TestCase("curse_07", 2, "curse_weakness", false, "伤害减少 20", 1)]
        [TestCase("curse_07", 0, "curse_weakness", false, "伤害减少 24", 2)]
        [TestCase("curse_08", 0, "curse_armor", false, "格挡减少 10", 1)]
        [TestCase("curse_08", 2, "curse_armor", false, "格挡减少 15", 1)]
        [TestCase("curse_08", 0, "curse_armor", false, "格挡减少 20", 2)]
        [TestCase("curse_09", 0, "curse_counter", true, "当前伤害 12", 1)]
        [TestCase("curse_09", 2, "curse_counter", true, "当前伤害 24", 1)]
        [TestCase("curse_09", 0, "curse_counter", true, "当前伤害 24", 2)]
        [TestCase("tide_09", 0, "tide_ambush", true, "反击 32 点伤害", 1)]
        [TestCase("tide_09", 2, "tide_ambush", true, "反击 56 点伤害", 1)]
        [TestCase("tide_09", 0, "tide_ambush", true, "反击 64 点伤害", 2)]
        [TestCase("tide_10", 0, "tide_weakness", false, "伤害减少 6", 1)]
        [TestCase("tide_10", 2, "tide_weakness", false, "伤害减少 10", 1)]
        [TestCase("tide_10", 0, "tide_weakness", false, "伤害减少 12", 2)]
        [TestCase("blood_08", 0, "blood_debt", true, "反击 16 点伤害", 1)]
        [TestCase("blood_08", 2, "blood_debt", true, "反击 28 点伤害", 1)]
        [TestCase("blood_15", 0, "blood_oath", true, "额外造成 16 点伤害", 1)]
        [TestCase("blood_15", 2, "blood_oath", true, "额外造成 28 点伤害", 1)]
        public void BoardingCardStatusesShowAuthoredRanksAndAllActiveCopies(
            string key, int rank, string statusId, bool player, string expected, int copies)
        {
            string id = "card_boarding_" + key;
            var session = Battle(NavalBattleStage.Boarding, id, rank);
            session.Battle.PlayerBoardingHealth = 200;
            session.Cargo.Powder = 100;
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(session.Battle.Schools, 100);
            typeof(CoreVoyageSession).GetMethod("BInitialize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(session, null);
            typeof(CoreVoyageSession).GetMethod("CTBAddMarks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(session, new object[] { 6 });
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            for (int copy = 0; copy < copies; copy++)
            {
                session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            }
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == statusId);
            Assert.That(status.Tooltip, Does.Contain(expected));
            if (key == "fire_13") Assert.That(status.Tooltip, Does.Not.Contain("×2"));
            Assert.That(CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == statusId).Tooltip,
                Is.EqualTo(status.Tooltip), "Reading a tooltip never consumes a live effect.");
        }

        private static CoreVoyageSession Battle(NavalBattleStage stage, string upgradedCard = null, int rank = 0)''')
status_text = status_text.replace('''            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);''', '''            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            if (upgradedCard != null) session.ConfigureCardUpgrades(new System.Collections.Generic.Dictionary<string, int> { { upgradedCard, rank } });
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);''')
status_path.write_text(status_text, encoding='utf-8-sig', newline='\r\n')
print('Wrote 17 reviewed fixture overlays, including 24 resource and 24 HUD regression cases.')
