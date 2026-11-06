using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalStatImpactTests
    {
        [TestCase(-5, 10, 5)]
        [TestCase(0, 10, 5)]
        [TestCase(4, 10, 8)]
        [TestCase(5, 10, 9)]
        [TestCase(6, 10, 10)]
        [TestCase(7, 10, 11)]
        [TestCase(16, 10, 18)]
        [TestCase(22, 10, 23)]
        [TestCase(38, 20, 71)]
        public void FirepowerHasNoStarterDeadZoneAndScalesTheAuthoredShot(int firepower, int shot, int expected)
        {
            Assert.That(CoreNavalStatRules.ScaleCannonDamage(shot, firepower), Is.EqualTo(expected));
        }

        [TestCase(100, 85)]
        [TestCase(1000, 805)]
        [TestCase(10000, 8005)]
        public void FirepowerContinuesGrowingBeyondCurrentEquipmentWithoutAMultiplierCap(int firepower, int expected)
        {
            CoreVoyageSession session = Training(firepowerBonus: firepower - 6);
            CoreBattleCard cannon = session.GetBattleCard(Index("card_chain_shot"));
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Naval), Is.EqualTo(expected));
        }

        [TestCase(20, -1, 24)]
        [TestCase(20, 0, 24)]
        [TestCase(20, 2, 20)]
        [TestCase(20, 6, 15)]
        [TestCase(20, 10, 12)]
        [TestCase(20, 13, 11)]
        [TestCase(20, 14, 10)]
        [TestCase(20, 15, 10)]
        [TestCase(20, 16, 10)]
        [TestCase(20, 24, 10)]
        [TestCase(20, 38, 10)]
        [TestCase(3, 8, 2)]
        [TestCase(5, int.MaxValue, 3)]
        public void ArmorReducesHeavyAttacksWithoutRoundingThroughExactDivision(int attack, int armor, int expected)
        {
            Assert.That(CoreNavalStatRules.ReduceIncomingCannonDamage(attack, armor), Is.EqualTo(expected));
        }

        [Test]
        public void StatRulesPreserveZeroAndSaturateWithoutOverflow()
        {
            foreach (int stat in new[] { int.MinValue, 0, 6, int.MaxValue })
            {
                Assert.That(CoreNavalStatRules.ScaleCannonDamage(0, stat), Is.Zero);
                Assert.That(CoreNavalStatRules.ScaleCannonDamage(-1, stat), Is.Zero);
                Assert.That(CoreNavalStatRules.ReduceIncomingCannonDamage(0, stat), Is.Zero);
                Assert.That(CoreNavalStatRules.ReduceIncomingCannonDamage(-1, stat), Is.Zero);
            }
            Assert.That(CoreNavalStatRules.ScaleCannonDamage(int.MaxValue, int.MaxValue), Is.EqualTo(int.MaxValue));
            Assert.That(CoreNavalStatRules.ReduceIncomingCannonDamage(int.MaxValue, 0), Is.EqualTo(int.MaxValue));
            Assert.That(CoreNavalStatRules.ReduceIncomingCannonDamage(int.MaxValue, int.MaxValue), Is.EqualTo(1073741824));
            Assert.That(CoreNavalStatRules.CannonDamageMultiplier(6), Is.EqualTo(1m));
            Assert.That(CoreNavalStatRules.CannonDamageMultiplier(16), Is.EqualTo(1.8m));
            Assert.That(CoreNavalStatRules.ArmorDamageMultiplier(2), Is.EqualTo(1m));
            Assert.That(CoreNavalStatRules.ArmorDamageMultiplier(10), Is.EqualTo(.6m));
            Assert.That(CoreNavalStatRules.ArmorDamageMultiplier(int.MaxValue), Is.EqualTo(.5m));
        }

        [TestCase(-6, 0, 5)]
        [TestCase(-2, 4, 8)]
        [TestCase(0, 6, 10)]
        [TestCase(10, 16, 18)]
        public void EquippedFirepowerChangesCardFaceAndRealCannonHit(int bonus, int effective, int damage)
        {
            CoreVoyageSession session = Training(firepowerBonus: bonus);
            Assert.That(session.EffectiveFirepower, Is.EqualTo(effective));
            CoreBattleCard cannon = session.GetBattleCard(Index("card_chain_shot"));
            session.Battle.EnemyBlock = 0;
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Naval), Is.EqualTo(damage));
            Assert.That(Regex.IsMatch(session.BattleCardFaceDescription(cannon, NavalBattleStage.Naval),
                @"(?:船体伤害\s*" + damage + @"(?!\d)|" + damage + @"\s*点船体伤害)"), Is.True,
                "The card face must retain negative firepower adjustments as well as positive ones.");
            int enemyHull = session.Battle.EnemyHull, powder = session.Cargo.Powder, balls = session.Cargo.Cannonballs;
            PlaySingle(session, "card_chain_shot");
            Assert.That(enemyHull - session.Battle.EnemyHull, Is.EqualTo(damage));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder - 1));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(balls - 1));
        }

        [Test]
        public void LoadingRemainsFinalDamageAndEnemyGuardAbsorbsAfterFirepower()
        {
            CoreVoyageSession session = Training(firepowerBonus: 10);
            session.Battle.PlayerLoadedShot = 4;
            session.Battle.EnemyBlock = 5;
            int enemyHull = session.Battle.EnemyHull;
            Assert.That(session.GetBattleCardDamage(session.GetBattleCard(Index("card_chain_shot")), NavalBattleStage.Naval), Is.EqualTo(22));
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.EqualTo(4));
            PlaySingle(session, "card_chain_shot");
            Assert.That(enemyHull - session.Battle.EnemyHull, Is.EqualTo(17), "18 from the ship's cannon, plus 4 load, minus 5 guard.");
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.PlayerLoadedShotDamageBonus, Is.Zero);
        }

        [TestCase(0)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(10)]
        public void OrdinaryCrewCountDoesNotReduceFirepowerOrActualCannonDamage(int ordinaryCrew)
        {
            CoreVoyageSession session = Training(firepowerBonus: 10, ordinaryCrew: ordinaryCrew);
            Assert.That(session.Firepower, Is.EqualTo(16));
            Assert.That(session.EffectiveFirepower, Is.EqualTo(16));
            Assert.That(session.GetBattleCardDamage(session.GetBattleCard(Index("card_chain_shot")),
                NavalBattleStage.Naval), Is.EqualTo(18));
            session.Battle.EnemyBlock = 0;
            int enemyHull = session.Battle.EnemyHull;
            PlaySingle(session, "card_chain_shot");
            Assert.That(enemyHull - session.Battle.EnemyHull, Is.EqualTo(18));
        }

        [Test]
        public void ShipFirepowerDoesNotScaleNonCannonOrBoardingDamage()
        {
            CoreVoyageSession session = Training(firepowerBonus: 30);
            var cannon = new CoreBattleCard("stat_test_cannon", "测试炮", "炮击", 1, 10, 0, 0, 0, 0,
                "{damage}", NavalBattleStages.All, NavalBattleCardAction.Cannon);
            var spell = new CoreBattleCard("stat_test_spell", "测试法术", "法术", 1, 10, 0, 0, 0, 0,
                "{damage}", NavalBattleStages.All);
            Assert.That(session.GetBattleCardDamage(spell, NavalBattleStage.Naval), Is.EqualTo(10));
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Boarding), Is.EqualTo(10));
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Preparation), Is.EqualTo(10));
            var invalidCannon = new CoreBattleCard("stat_test_negative", "非法负伤害炮", "炮击", 1, -5, 0, 0, 0, 0,
                "{damage}", NavalBattleStages.Naval, NavalBattleCardAction.Cannon);
            Assert.That(session.GetBattleCardDamage(invalidCannon, NavalBattleStage.Naval), Is.Zero,
                "Clamping a malformed negative base must not turn it into an unscaled positive bonus.");
        }

        [Test]
        public void SupplyFallbackQuoteScalesOnlyTheRemainingBaseDamage()
        {
            // The current catalogue reserves fallback attacks for boarding. This synthetic
            // naval cannon guards the shared supply-adjustment API if such a card is authored.
            CoreVoyageSession session = Training(firepowerBonus: 10);
            session.Cargo.Powder = 0;
            var cannon = new CoreBattleCard("stat_test_fallback", "缺火药炮", "炮击", 1, 20, 0, 0, 0, 2,
                "{damage} 点船体伤害", NavalBattleStages.Naval, NavalBattleCardAction.Cannon,
                schoolId: "fire", schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.FireIgnite,
                    power: 2, fallbackDamage: 5));
            Assert.That(session.GetBattleCardEffects(cannon, NavalBattleStage.Naval).Damage, Is.EqualTo(5));
            Assert.That(session.GetBattleCardDamage(cannon, NavalBattleStage.Naval), Is.EqualTo(9));
            Assert.That(session.BattleCardFaceDescription(cannon, NavalBattleStage.Naval), Does.Contain("伤害 9").And.Not.Contain("36"));
        }

        [TestCase(0, 2, 24)]
        [TestCase(4, 6, 18)]
        [TestCase(8, 10, 15)]
        [TestCase(14, 16, 12)]
        [TestCase(36, 38, 12)]
        public void EquippedArmorChangesRealEnemyTurnAndBothIntentQuotes(int bonus, int effective, int damage)
        {
            CoreVoyageSession session = Training(targetIndex: 2, armorBonus: bonus);
            Assert.That(session.EffectiveArmor, Is.EqualTo(effective));
            session.Battle.PlayerBlock = 3;
            int hull = session.Hull;
            string first = session.GetEnemyIntent();
            Assert.That(first, Does.Contain("炮击伤害 " + damage));
            CoreBattleIntent icon = CoreBattleIntentPresentation.Build(session).Single(intent => intent.Kind == CoreBattleIntentKind.Cannon);
            Assert.That(icon.Amount, Is.EqualTo(damage));
            Assert.That(icon.Tooltip, Does.Contain("装甲后、格挡前"));
            Assert.That(session.GetEnemyIntent(), Is.EqualTo(first), "Repeated previews must be read-only.");
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(3));
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(3));
            Assert.That(hull - session.Hull, Is.EqualTo(damage - 3));
        }

        [Test]
        public void ArmorAloneRetainsHalfDamageButGuardCanPreventIt()
        {
            CoreVoyageSession exposed = Training(targetIndex: 4, armorBonus: 98);
            CoreVoyageSession guarded = Training(targetIndex: 4, armorBonus: 98);
            guarded.Battle.PlayerBlock = 25;
            int exposedHull = exposed.Hull, guardedHull = guarded.Hull;
            Assert.That(exposed.EndBattleTurn(), Is.True);
            Assert.That(guarded.EndBattleTurn(), Is.True);
            Assert.That(exposedHull - exposed.Hull, Is.EqualTo(25), "50 damage retains half its damage even with 100 armor.");
            Assert.That(guarded.Hull, Is.EqualTo(guardedHull));
            Assert.That(guarded.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(25));
        }

        [TestCase(0, 0, "52%", "120%")]
        [TestCase(6, 2, "100%", "100%")]
        [TestCase(8, 4, "116%", "85.7%")]
        [TestCase(38, 38, "356%", "50%")]
        public void PermanentStatStatusesExplainCurrentMultipliersWithoutConsumingEffects(int firepower, int armor,
            string firepowerPercent, string armorPercent)
        {
            CoreVoyageSession session = Training(firepowerBonus: firepower - 6, armorBonus: armor - 2);
            session.Battle.PlayerLoadedShot = 4;
            session.Battle.PlayerBlock = 7;
            int hull = session.Hull, energy = session.Battle.Energy, powder = session.Cargo.Powder;
            for (int read = 0; read < 2; read++)
            {
                var statuses = CoreBattleStatusPresentation.Build(session, true);
                CoreBattleStatus guns = statuses.Single(status => status.Id == "firepower");
                CoreBattleStatus plating = statuses.Single(status => status.Id == "armor");
                Assert.That(guns.Value, Is.EqualTo(firepower.ToString()));
                Assert.That(plating.Value, Is.EqualTo(armor.ToString()));
                Assert.That(guns.Tooltip, Does.Contain("倍率 " + firepowerPercent));
                Assert.That(plating.Tooltip, Does.Contain("承伤比例 " + armorPercent));
                Assert.That(guns.Tooltip, Does.Contain("倍率不设上限"));
                Assert.That(plating.Tooltip, Does.Contain("最多减伤 50%"));
                Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(status => status.Id == "firepower" || status.Id == "armor"), Is.False);
            }
            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(4));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(7));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(status => status.Id == "firepower" || status.Id == "armor"), Is.False);
        }

        [Test]
        public void SameEnemyDeckSeedAndPolicyNeedFewerTurnsWithFirepowerAndLoseLessHullWithArmor()
        {
            Duel baseline = RunFixedDeck(0, 0), guns = RunFixedDeck(10, 0), armor = RunFixedDeck(0, 8), combined = RunFixedDeck(10, 8);
            Assert.That(baseline.Outcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(guns.Outcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(armor.Outcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(combined.Outcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(guns.Turns, Is.LessThanOrEqualTo(baseline.Turns * .75), "Equipment should materially shorten a fixed real battle.");
            Assert.That(guns.HullLoss, Is.LessThan(baseline.HullLoss), "Killing sooner also avoids enemy turns.");
            Assert.That(armor.Turns, Is.EqualTo(baseline.Turns), "Armor changes survival, not the offensive deck or enemy durability.");
            Assert.That(armor.HullLoss, Is.LessThanOrEqualTo(baseline.HullLoss * .7), "Armor should materially reduce a fixed real battle's cost.");
            Assert.That(combined.Turns, Is.EqualTo(guns.Turns));
            Assert.That(combined.HullLoss, Is.LessThan(guns.HullLoss));
            Console.WriteLine("Fixed deck: baseline turns={0}, hull loss={1}; firepower +10 turns={2}, hull loss={3}; armor +8 turns={4}, hull loss={5}; combined turns={6}, hull loss={7}",
                baseline.Turns, baseline.HullLoss, guns.Turns, guns.HullLoss, armor.Turns, armor.HullLoss, combined.Turns, combined.HullLoss);
        }

        private static CoreVoyageSession Training(int targetIndex = 0, int firepowerBonus = 0, int armorBonus = 0,
            int ordinaryCrew = 10, bool fixedDeck = false)
        {
            var session = new CoreVoyageSession();
            session.SetSeed("NAVAL-STAT-IMPACT-FIXED");
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureDepartureCrew(ordinaryCrew, 0);
            // Equal durability in all duel variants lets the unchanged weak loadout finish
            // the comparison rather than truncating its measured incoming damage at death.
            session.ConfigureComponentBonuses(fixedDeck ? 120 : 0, armorBonus, firepowerBonus, 0, 0);
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            if (fixedDeck)
            {
                int[] deck = new[] { "card_chain_shot", "card_cannon_barrage", "card_brace", "card_load_shot", "card_evasive_maneuver" }
                    .SelectMany(id => new[] { Index(id), Index(id) }).ToArray();
                Assert.That(session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            }
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[targetIndex]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            session.Battle.DistanceMeters = 300f;
            return session;
        }

        private static void PlaySingle(CoreVoyageSession session, string id)
        {
            session.Battle.Hand.Clear();
            session.Battle.Hand.Add(Index(id));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
        }

        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private sealed class Duel
        {
            public int Turns, HullLoss;
            public CoreTrainingBattleOutcome Outcome;
        }

        private static Duel RunFixedDeck(int firepowerBonus, int armorBonus)
        {
            CoreVoyageSession session = Training(firepowerBonus: firepowerBonus, armorBonus: armorBonus, fixedDeck: true);
            int initialHull = session.Hull, turns = 0;
            while (session.TrainingOutcome == CoreTrainingBattleOutcome.InProgress && turns < 30)
            {
                turns++;
                // Identical deterministic policy and real shuffled hand: use free defense,
                // then the largest affordable shot, then remaining affordable defense.
                while (session.Phase == CoreDemoPhase.Battle)
                {
                    int chosen = -1, best = int.MinValue;
                    for (int hand = 0; hand < session.Battle.Hand.Count; hand++)
                    {
                        if (!session.CanPlayCard(hand)) continue;
                        int index = session.Battle.Hand[hand];
                        CoreBattleCard card = session.GetBattleCard(index);
                        int cost = session.GetBattleCardEnergyCost(index);
                        int score = card.Block > 0 && cost == 0 ? 10000 : card.Action == NavalBattleCardAction.Cannon
                            ? 1000 + session.GetBattleCardDamage(card, NavalBattleStage.Naval) : card.Block > 0 ? card.Block : -1;
                        if (score >= 0 && score > best) { best = score; chosen = hand; }
                    }
                    if (chosen < 0) break;
                    Assert.That(session.PlayCard(chosen), Is.True);
                }
                if (session.TrainingOutcome == CoreTrainingBattleOutcome.InProgress)
                    Assert.That(session.EndBattleTurn(), Is.True);
            }
            return new Duel { Turns = turns, HullLoss = initialHull - session.Hull, Outcome = session.TrainingOutcome };
        }
    }
}
