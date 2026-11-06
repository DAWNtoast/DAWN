using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalBossImmunityIntentTests
    {
        [TestCase("boss_route_shattered_sail_reef", CoreNavalFieldKind.Fire)]
        [TestCase("boss_route_shattered_sail_reef_tide", CoreNavalFieldKind.Tide)]
        [TestCase("boss_route_golden_tide_trade", CoreNavalFieldKind.Blood)]
        [TestCase("boss_route_golden_tide_trade_thunder", CoreNavalFieldKind.Thunder)]
        [TestCase("boss_route_moon_tide_ruins", CoreNavalFieldKind.Curse)]
        [TestCase("boss_route_moon_tide_ruins_arcane", CoreNavalFieldKind.Arcane)]
        [TestCase(CoreBattleBalance.TreasureBossId, CoreNavalFieldKind.Tide)]
        public void SchoolProtectionIsForecastBeforeItIsActiveAndExpiresAfterEnemyTurn(string id, CoreNavalFieldKind school)
        {
            var s = Start(id, 3);
            var b = s.Battle;
            int phaseTurn = b.PhaseTurn, actions = b.EnemyNavalBossActionsCompleted;
            string name = CoreComponentFieldCards.FieldName(school);
            var forecast = CoreBattleIntentPresentation.Build(s).Single(x => x.Id == "enemy_boss_field_immunity");
            Assert.That(forecast.Kind, Is.EqualTo(CoreBattleIntentKind.Immunity));
            Assert.That(forecast.Tooltip, Does.Contain(name).And.Contain("负面"));
            Assert.That(forecast.Amount, Is.Null, "Protection is not an extra guard amount.");
            Assert.That(s.GetEnemyIntent(), Does.Contain(name));
            Assert.That(CoreBattleStatusPresentation.Build(s, false).Any(x => x.Id == "boss_field_immunity"), Is.False);
            Assert.That(b.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero);
            Assert.That(b.EnemyNavalBossActionsCompleted, Is.EqualTo(actions));
            Assert.That(b.PhaseTurn, Is.EqualTo(phaseTurn));
            Assert.That(CoreBattleIntentPresentation.Build(s).Single(x => x.Id == forecast.Id).Tooltip,
                Is.EqualTo(forecast.Tooltip), "Repeated intent reads must not advance the action chain.");

            s.NavalFieldsBeginTurn(false);
            var active = CoreBattleStatusPresentation.Build(s, false).Single(x => x.Id == "boss_field_immunity");
            Assert.That(active.Kind, Is.EqualTo(CoreBattleStatusKind.Immunity));
            Assert.That(active.Tooltip, Does.Contain(name).And.Contain("负面"));
            Assert.That(b.NavalFields.Enemy.BossFieldImmunityMask, Is.EqualTo(1 << (int)school));
            Assert.That(CoreBattleStatusPresentation.Build(s, true).Any(x => x.Id == "boss_field_immunity"), Is.False);
            s.NavalFieldsEndTurn(false);
            Assert.That(CoreBattleStatusPresentation.Build(s, false).Any(x => x.Id == "boss_field_immunity"), Is.False);
        }

        [TestCase(5)]
        [TestCase(10)]
        [TestCase(15)]
        public void FullProtectionIsVisibleEvenWhenBossCannotAdvance(int turn)
        {
            var s = Start("boss_route_moon_tide_ruins_arcane", turn);
            Pose(s, false, 1, 0);
            var intents = CoreBattleIntentPresentation.Build(s);
            var immunity = intents.Single(x => x.Id == "enemy_boss_field_immunity");
            Assert.That(immunity.Name, Is.EqualTo("全场地防护"));
            Assert.That(immunity.Tooltip, Does.Contain("所有").And.Contain("负面"));
            Assert.That(s.EnemyNavalBossRushThisTurn, Is.True);
            Assert.That(s.GetEnemyIntent(), Does.Contain("5 格"));
            Assert.That(s.Battle.NavalFields.Enemy.BossFieldImmunityMask, Is.Zero);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(9)]
        public void OrdinaryRoundsHaveNoProtectionIntent(int turn)
        {
            var s = Start("boss_route_shattered_sail_reef", turn);
            Assert.That(CoreBattleIntentPresentation.Build(s).Any(x => x.Kind == CoreBattleIntentKind.Immunity), Is.False);
            Assert.That(CoreBattleStatusPresentation.Build(s, false).Any(x => x.Id == "boss_field_immunity"), Is.False);
        }

        private static CoreVoyageSession Start(string id, int turn)
        {
            var s = new CoreVoyageSession();
            var encounter = CoreRouteBossCatalog.Find(id)?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            typeof(CoreVoyageSession).GetMethod("StartBattle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(s, new object[] { encounter, true });
            s.Battle.Stage = NavalBattleStage.Naval;
            s.Battle.PhaseTurn = turn;
            Pose(s, true, 0, 0); Pose(s, false, 3, 0);
            return s;
        }

        private static void Pose(CoreVoyageSession s, bool player, int x, int y) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(s.Battle, new object[] { player, new VoyagePoint(x * 100, y * 100), 90f });
    }
}
