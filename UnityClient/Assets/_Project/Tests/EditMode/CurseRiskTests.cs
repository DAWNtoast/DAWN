using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CurseRiskTests
    {
        [TestCase(0, 7, 7)]
        [TestCase(1, 7, 8)]
        [TestCase(3, 7, 10)]
        [TestCase(10, 7, 14)]
        [TestCase(int.MaxValue, 7, 14)]
        [TestCase(10, 0, 0)]
        [TestCase(10, -1, 0)]
        [TestCase(10, int.MaxValue, int.MaxValue)]
        public void DamageUsesIntegerCeilingPreservesZeroAndCapsWithoutOverflow(int layers, int damage, int expected)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Curse = layers;
            Assert.That(session.ApplyCurseBattleDamage(damage), Is.EqualTo(expected));
            Assert.That(session.CurseCount, Is.EqualTo(layers));
        }

        [TestCase(0, 15, 8)]
        [TestCase(1, 17, 10)]
        [TestCase(3, 20, 13)]
        [TestCase(10, 30, 23)]
        public void CannonAppliesCurseAfterArmorBeforeGuardAndForecastMatches(int layers, int attack, int loss)
        {
            CoreVoyageSession session = StartBattle(layers, armorBonus: 12);
            Assert.That(session.EffectiveArmor, Is.EqualTo(14));
            session.Battle.PlayerBlock = 7;
            session.Battle.DistanceMeters = 200;
            Assert.That(session.GetEnemyIntent(), Does.Contain("炮击：伤害 " + attack + "（装甲后、格挡前）"));
            int hull = session.Hull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(hull - session.Hull, Is.EqualTo(loss));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(7));
            Assert.That(session.Battle.LastAction, Does.Contain("实际损失 " + loss));
        }

        [TestCase(0, 7, 2)]
        [TestCase(1, 8, 3)]
        [TestCase(10, 14, 9)]
        public void BoardingAppliesCurseBeforeCrewGuardAndForecastMatches(int layers, int attack, int loss)
        {
            CoreVoyageSession session = StartBattle(layers);
            session.Battle.DistanceMeters = 40;
            session.Battle.Alignment = NavalBattleAlignment.Parallel;
            Assert.That(session.TryBoard(), Is.True);
            session.Battle.PlayerBlock = 5;
            Assert.That(session.GetEnemyIntent(), Does.Contain("船员伤害 " + attack + "（格挡前）"));
            int health = session.Battle.PlayerBoardingHealth;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(health - session.Battle.PlayerBoardingHealth, Is.EqualTo(loss));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(5));
        }

        [Test]
        public void GuardCanStillFullyPreventAmplifiedDamage()
        {
            CoreVoyageSession session = StartBattle(10);
            session.Battle.DistanceMeters = 200;
            session.Battle.PlayerBlock = 40;
            int hull = session.Hull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Battle.LastEnemyGuardAbsorbed, Is.EqualTo(40));
        }

        [TestCase(0, NavalBattleAlignment.HeadOn, 13, 7)]
        [TestCase(1, NavalBattleAlignment.HeadOn, 15, 8)]
        [TestCase(10, NavalBattleAlignment.HeadOn, 26, 14)]
        [TestCase(0, NavalBattleAlignment.Parallel, 13, 7)]
        [TestCase(1, NavalBattleAlignment.Parallel, 15, 8)]
        [TestCase(10, NavalBattleAlignment.Parallel, 26, 14)]
        public void EnemyRamAmplifiesOnlyPlayerHullAndOpeningCrewDamage(int layers, NavalBattleAlignment alignment, int hullLoss, int crewLoss)
        {
            CoreVoyageSession session = StartBattle(layers, ramChance: 1d);
            session.Battle.Alignment = alignment;
            session.Battle.DistanceMeters = 90;
            session.Battle.PlayerBlock = 999;
            Assert.That(session.EnemyWillRam, Is.True);
            Assert.That(session.EnemyRamFirstStrikeDamage, Is.EqualTo(crewLoss));
            string intent = session.GetEnemyIntent();
            Assert.That(intent, Does.Contain("己舰 -" + hullLoss + " / 敌舰 -9"));
            Assert.That(intent, Does.Contain("敌方先手：船员伤害 " + crewLoss));
            int hull = session.Hull, health = session.Battle.PlayerBoardingHealth, enemy = session.Battle.EnemyHull;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(hull - session.Hull, Is.EqualTo(hullLoss));
            int expectedHealth = (int)System.Math.Ceiling(health *
                (1d - System.Math.Max(0, hull - session.Hull) / (2d * session.MaxHull))) - crewLoss;
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(enemy - session.Battle.EnemyHull, Is.EqualTo(9), "The captain's curse does not strengthen damage dealt to the enemy.");
        }

        [TestCase(0, 9)]
        [TestCase(1, 10)]
        [TestCase(10, 18)]
        public void PlayerRamAlsoAmplifiesSelfDamageWithoutGivingEnemyAnOpening(int layers, int loss)
        {
            CoreVoyageSession session = StartBattle(layers);
            session.Battle.DistanceMeters = 40;
            session.Battle.Alignment = NavalBattleAlignment.HeadOn;
            session.Battle.PlayerMovingTowardEnemy = true;
            session.Battle.PlayerBlock = 999;
            int hull = session.Hull, health = session.Battle.PlayerBoardingHealth, enemy = session.Battle.EnemyHull;
            Assert.That(session.TryRam(), Is.True);
            Assert.That(hull - session.Hull, Is.EqualTo(loss));
            Assert.That(enemy - session.Battle.EnemyHull, Is.EqualTo(26));
            int expectedHealth = (int)Math.Ceiling(health * (1d - loss / (2d * session.MaxHull)));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(session.Battle.BoardingInitiatedByEnemy, Is.False);
            Assert.That(session.Battle.LastAction, Does.Contain("己舰船体 -" + loss));
        }

        [TestCase("treasure_false_cache", 0)]
        [TestCase("treasure_smuggler_jetty", 1)]
        public void ActualCurseRewardSurvivesReturnShipChangeWarehouseAndReloadUntilPaidPurification(string encounterId, int choice)
        {
            CoreVoyageSession session = StartTreasureWithEvent(encounterId);
            CoreMapNode node = session.Map.Nodes.Single(n => n.Encounter.Id == encounterId);
            Assert.That(session.CurseCount, Is.Zero);
            Assert.That(node.Encounter.Choices[choice].Detail, Does.Contain("战斗承伤 +10%").And.Contain("30 金/层"));
            // Jump to a generated node only; acquisition and settlement use real APIs.
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, node.Encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            Assert.That(session.ResolveChoice(choice), Is.True, session.ChoiceUnavailableReason(choice));
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Assert.That(session.CurseCount, Is.EqualTo(1));
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.TryDepositPortCargo("curse", 1), Is.False);
            Assert.That(session.TryWithdrawPortCargo("curse", 1), Is.False);
            Assert.That(session.SelectShip(1), Is.True);
            Assert.That(session.CurseCount, Is.EqualTo(1));

            var restored = new CoreVoyageSession();
            restored.RestoreProfile(session.ProfileGold, session.CurrentPort);
            restored.RestoreTreasureProgress(session.CaptureTreasureProgress());
            Assert.That(restored.PortWarehouse.Restore(session.PortWarehouse.Serialize()), Is.True);
            Assert.That(restored.CurseCount, Is.EqualTo(1));
            Assert.That(restored.CurseDamageBonusPercent, Is.EqualTo(10));
            decimal before = restored.ProfileGold;
            Assert.That(restored.TryPurifyCurse(1, out string message), Is.True, message);
            Assert.That(restored.ProfileGold, Is.EqualTo(before - 30m));
            Assert.That(restored.CurseCount, Is.Zero);
            var afterPurification = new CoreVoyageSession();
            afterPurification.RestoreTreasureProgress(restored.CaptureTreasureProgress());
            Assert.That(afterPurification.CurseCount, Is.Zero);
        }

        [Test]
        public void OneLayerPurificationRejectsReplayedCallbackAndUsesExactDecimalPayment()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(90.125m, "暮潮港");
            session.RestoreTreasureProgress(new CoreTreasureProgress { curses = 3 });
            Assert.That(session.TryPurifyCurse(3, out string reason), Is.True, reason);
            Assert.That(session.ProfileGold, Is.EqualTo(60.125m));
            Assert.That(session.CurseCount, Is.EqualTo(2));
            Assert.That(session.CurseDamageBonusPercent, Is.EqualTo(20));
            Assert.That(session.TryPurifyCurse(3, out reason), Is.False);
            Assert.That(reason, Does.Contain("已变化"));
            Assert.That(session.ProfileGold, Is.EqualTo(60.125m));
            Assert.That(session.CurseCount, Is.EqualTo(2));
            Assert.That(session.TryPurifyCurse(2, out reason), Is.True, reason);
            Assert.That(session.ProfileGold, Is.EqualTo(30.125m));
            Assert.That(session.CurseCount, Is.EqualTo(1));
        }

        [Test]
        public void PoorOrUncursedCaptainCannotPayAndHighLegacyCountKeepsEveryLayer()
        {
            var session = new CoreVoyageSession();
            session.RestoreProfile(29.9999m, "断锚港");
            session.RestoreTreasureProgress(new CoreTreasureProgress { curses = int.MaxValue });
            Assert.That(session.TryPurifyCurse(int.MaxValue, out _), Is.False);
            Assert.That(session.CurseCount, Is.EqualTo(int.MaxValue));
            Assert.That(session.ProfileGold, Is.EqualTo(29.9999m));
            session.RestoreProfile(60m, "灰鸥港");
            Assert.That(session.TryPurifyCurse(int.MaxValue, out _), Is.True);
            Assert.That(session.CurseCount, Is.EqualTo(int.MaxValue - 1));
            Assert.That(session.CurseDamageBonusPercent, Is.EqualTo(100));
            session.RestoreTreasureProgress(new CoreTreasureProgress());
            Assert.That(session.TryPurifyCurse(0, out _), Is.False);
            Assert.That(session.ProfileGold, Is.EqualTo(30m));
        }

        [TestCase(CoreDemoPhase.Sailing)]
        [TestCase(CoreDemoPhase.Encounter)]
        [TestCase(CoreDemoPhase.Battle)]
        [TestCase(CoreDemoPhase.Settlement)]
        public void PurificationRequiresPortAndDoesNotAlterMoneyOrLayersElsewhere(CoreDemoPhase phase)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { curses = 2 });
            decimal gold = session.ProfileGold;
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, phase);
            Assert.That(session.TryPurifyCurse(2, out string reason), Is.False);
            Assert.That(reason, Does.Contain("返回港口"));
            Assert.That(session.CurseCount, Is.EqualTo(2));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
        }

        private static CoreVoyageSession StartBattle(int layers, int armorBonus = 0, double ramChance = 0d)
        {
            var session = new CoreVoyageSession();
            session.Cargo.Curse = layers;
            session.ConfigureComponentBonuses(0, armorBonus, 0, 0, 0);
            session.SetSeed("CURSE-COMBAT-REGRESSION");
            session.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { new CoreEncounter("curse_fixture", "诅咒测试敌舰", "海战", "隔离核心夹具", true, false, 1, 70), false });
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                500, 20, 7, 0, 0, 30, 1f, 1f, ramChance, 120f);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 500;
            session.Battle.EnemyLoadedShot = 0;
            session.Battle.PlayerBowQuality = session.Battle.PlayerHullQuality = 1f;
            session.Battle.EnemyBowQuality = session.Battle.EnemyHullQuality = 1f;
            return session;
        }

        private static CoreVoyageSession StartTreasureWithEvent(string encounterId)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var session = new CoreVoyageSession();
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = 1400, selectedMapId = "curse-risk",
                    maps = new[] { new CoreTreasureMapSave { id = "curse-risk", rankId = "weathered", seed = "CURSE-RISK-" + seed } }
                });
                session.Cargo.Water = session.Cargo.Food = 100;
                session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Beer = 0;
                session.Cargo.Medicine = session.Cargo.Powder = session.Cargo.Cannonballs = 0;
                Assert.That(session.TryStartTreasureVoyage(out string reason), Is.True, reason);
                if (session.Map.Nodes.Any(n => n.Encounter.Id == encounterId)) return session;
            }
            throw new InvalidOperationException("Could not find generated treasure event " + encounterId);
        }
    }
}
