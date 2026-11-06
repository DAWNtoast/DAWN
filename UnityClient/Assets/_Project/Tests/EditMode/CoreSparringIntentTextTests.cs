using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSparringIntentTextTests
    {
        private const string Tide = "legendary_pirate_tide_requiem";
        private const string Horizon = "legendary_pirate_last_horizon";
        private const string Ordinary = "sparring_text_ordinary_enemy";

        [TestCase(Tide, true)]
        [TestCase(Horizon, true)]
        [TestCase("legendary_pirate_iron_oath", false)]
        [TestCase("legendary_pirate_thunder_crown", false)]
        [TestCase("legendary_pirate_red_widow", false)]
        [TestCase("legendary_pirate_ember_ark", false)]
        [TestCase(Ordinary, false)]
        public void NavalPreActionThreshold_ChangesOnlyTheFriendlyText(string id, bool sparring)
        {
            var session = Battle(id);
            session.Battle.EnemyHull = 6;
            Set(session.Battle.Schools, "CurseMarks", 6);
            string before = State(session);
            var preview = session.GetEnemyNavalFieldPreview(0);
            var intent = CoreBattleIntentPresentation.Build(session).Single(item => item.Id == "enemy_cannot_act");
            Assert.That(preview.Sinks, Is.True, "Keep the existing rule result and serialized terminology.");
            Assert.That(preview.HullDamage, Is.Zero);
            if (sparring)
            {
                AssertSparring(preview.Summary);
                AssertSparring(intent.Tooltip);
            }
            else
            {
                Assert.That(preview.Summary, Is.EqualTo("敌舰将在行动前沉没，不再航行"));
                Assert.That(intent.Tooltip, Does.Contain("敌舰将在行动前或移动途中沉没，不会继续获得格挡、装填、治疗或攻击。"));
                Assert.That(intent.Tooltip, Does.Not.Contain("认输").And.Not.Contain("切磋"));
            }
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide, CoreNavalFieldKind.Blood, "不再结算血池")]
        [TestCase(Tide, CoreNavalFieldKind.Fire, "不再结算所在场地")]
        [TestCase(Tide, CoreNavalFieldKind.Curse, "不再结算所在场地")]
        [TestCase(Tide, CoreNavalFieldKind.Tide, "不再受水流推动")]
        [TestCase(Horizon, CoreNavalFieldKind.Blood, "不再结算血池")]
        [TestCase(Horizon, CoreNavalFieldKind.Fire, "不再结算所在场地")]
        [TestCase(Horizon, CoreNavalFieldKind.Curse, "不再结算所在场地")]
        [TestCase(Horizon, CoreNavalFieldKind.Tide, "不再受水流推动")]
        public void NavalEndBurn_StopsTheSameLaterEffectsWithoutClaimingDeath(
            string id, CoreNavalFieldKind kind, string laterEffect)
        {
            var session = Battle(id);
            session.Battle.EnemyHull = 5;
            session.Battle.EnemyBurnDamage = session.Battle.EnemyBurnTurns = 1000;
            Field(session, kind, 2);
            string before = State(session);
            var sparring = session.GetEnemyNavalFieldPreview(0);
            AssertSparring(sparring.Summary);
            Assert.That(sparring.Summary, Does.Contain(laterEffect));
            UseEncounter(session, Ordinary);
            var ordinary = session.GetEnemyNavalFieldPreview(0);
            Assert.That(ordinary.Summary, Is.EqualTo("回合末燃烧结算时被消灭，" + laterEffect));
            AssertSamePreview(sparring, ordinary);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide, CoreNavalFieldKind.Blood, "，被消灭")]
        [TestCase(Tide, CoreNavalFieldKind.Curse, "，触发咒印消灭")]
        [TestCase(Horizon, CoreNavalFieldKind.Blood, "，被消灭")]
        [TestCase(Horizon, CoreNavalFieldKind.Curse, "，触发咒印消灭")]
        public void NavalEndFieldThreshold_UsesYieldingForBothFriendlyShips(
            string id, CoreNavalFieldKind kind, string ordinaryEnding)
        {
            var session = Battle(id);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1;
            Field(session, kind, 2);
            string before = State(session);
            var sparring = session.GetEnemyNavalFieldPreview(0);
            AssertSparring(sparring.Summary);
            UseEncounter(session, Ordinary);
            var ordinary = session.GetEnemyNavalFieldPreview(0);
            Assert.That(ordinary.Summary, Does.EndWith(ordinaryEnding).And.Not.Contain("认输"));
            AssertSamePreview(sparring, ordinary);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide, true)]
        [TestCase(Tide, false)]
        [TestCase(Horizon, true)]
        [TestCase(Horizon, false)]
        public void NavalMovementThunderAndCurse_CoversEitherSideWithoutChangingDamage(string id, bool player)
        {
            var session = Battle(id);
            Set(session, "Hull", 1001);
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 1001;
            Set(session.Battle, "PlayerCurseMarks", 2);
            Set(session.Battle.Schools, "CurseMarks", 2);
            Set(session.Battle.Schools, "ThunderChain", 49);
            session.Battle.NavalFields.Enemy.ThunderChain = 49;
            Field(session, CoreNavalFieldKind.Thunder, 1);
            Field(session, CoreNavalFieldKind.Thunder, 2);
            var path = new List<CoreNavalMovementNode> {
                new CoreNavalMovementNode(new VoyagePoint(player ? 0 : 200, 0), player ? 0 : 180),
                new CoreNavalMovementNode(new VoyagePoint(100, 0), player ? 0 : 180)
            };
            string before = State(session);
            var sparring = PreviewPath(session, path, player);
            AssertSparring(sparring.Summary);
            Assert.That(sparring.Summary, Does.Contain(player ? "己方" : "对方"));
            Assert.That(sparring.Sinks, Is.True);
            Assert.That(sparring.Damage, Is.EqualTo(CoreThunderRules.OverloadDamage));
            Assert.That(sparring.HullDamage, Is.EqualTo(1001), "Curse still ends the same predicted health pool.");
            UseEncounter(session, Ordinary);
            var ordinary = PreviewPath(session, path, player);
            Assert.That(ordinary.Summary, Does.Contain("咒印立即消灭").And.Contain("船体归零，后续航线终止"));
            AssertSamePreview(sparring, ordinary);
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide, true)]
        [TestCase(Tide, false)]
        [TestCase(Horizon, true)]
        [TestCase(Horizon, false)]
        public void BoardingCurse_UsesYieldingForEitherSideAndLeavesPreviewStateUntouched(string id, bool player)
        {
            var session = Boarding(id, "neutral");
            if (player) session.Battle.PlayerBoardingHealth = 1;
            else session.Battle.EnemyBoardingHealth = 1;
            session.Battle.BoardingFields.Fields.Add(new CoreBoardingField {
                Kind = CoreNavalFieldKind.Curse, PlayerOwned = true, RemainingTurns = 6
            });
            string before = State(session);
            var preview = session.GetBoardingEnemyPreview();
            Assert.That(player ? preview.PlayerExecutedByCurse : preview.EnemyExecutedByCurse, Is.True);
            string sparring = session.GetEnemyIntent();
            AssertSparring(sparring);
            Assert.That(sparring, Does.Contain(player ? "己方立即达到认输条件" : "对方立即达到认输条件"));
            if (player)
                AssertSparring(CoreBattleIntentPresentation.Build(session).Single().Tooltip);
            UseEncounter(session, Ordinary);
            Assert.That(session.GetEnemyIntent(), Does.Contain(player ? "咒印将立即消灭我方" : "咒印将立即消灭敌方"));
            var ordinary = session.GetBoardingEnemyPreview();
            Assert.That(ordinary.PlayerExecutedByCurse, Is.EqualTo(preview.PlayerExecutedByCurse));
            Assert.That(ordinary.EnemyExecutedByCurse, Is.EqualTo(preview.EnemyExecutedByCurse));
            Assert.That(ordinary.AttackDamage, Is.EqualTo(preview.AttackDamage));
            Assert.That(ordinary.Actions.Count, Is.EqualTo(preview.Actions.Count));
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide)]
        [TestCase(Horizon)]
        public void RamAndBoardingOpening_KeepNumbersAndOrdinarySurvivalCondition(string id)
        {
            var session = Battle(id, 1d);
            session.Battle.DistanceMeters = 100f;
            Set(session.Battle, "PlayerHeadingDegrees", 90f);
            Assert.That(session.EnemyWillRam, Is.True);
            string before = State(session);
            var sparring = CoreBattleIntentPresentation.Build(session);
            Assert.That(sparring.Single(item => item.Kind == CoreBattleIntentKind.Ram).Tooltip,
                Does.Contain("双方均未达到认输条件则进入接舷切磋"));
            Assert.That(sparring.Single(item => item.Id == "enemy_boarding_first_strike").Tooltip,
                Does.Contain("冲撞后双方均未达到认输条件时"));
            UseEncounter(session, Ordinary);
            var ordinary = CoreBattleIntentPresentation.Build(session);
            Assert.That(ordinary.Single(item => item.Kind == CoreBattleIntentKind.Ram).Tooltip, Does.Contain("双方存活则进入接舷"));
            Assert.That(ordinary.Single(item => item.Id == "enemy_boarding_first_strike").Tooltip, Does.Contain("冲撞后双方仍存活时"));
            Assert.That(sparring.Select(item => item.Id + ":" + item.Amount), Is.EqualTo(ordinary.Select(item => item.Id + ":" + item.Amount)));
            Assert.That(State(session), Is.EqualTo(before));
        }

        [TestCase(Tide, "curse", "立即消灭该角色")]
        [TestCase(Horizon, "curse", "立即消灭该角色")]
        [TestCase(Tide, "thunder", "任一方死亡立即停止")]
        [TestCase(Horizon, "thunder", "任一方死亡立即停止")]
        public void BoardingMechanicText_UsesTheSameThresholdWithSparringLanguage(string id, string school, string ordinaryText)
        {
            var session = Boarding(id, school);
            AssertSparring(session.GetBoardingEnemyMechanicDescription());
            if (school == "curse") Assert.That(session.GetBoardingEnemyMechanicDescription(),
                Does.Contain(CoreCurseRules.DefaultExecutionThresholdPercent + "%"));
            UseEncounter(session, Ordinary);
            Assert.That(session.GetBoardingEnemyMechanicDescription(), Does.Contain(ordinaryText).And.Not.Contain("认输"));
        }

        private static CoreVoyageSession Battle(string id, double ramChance = 0d)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            // Identical combat rules let the tests isolate encounter-sensitive narration.
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 10, 20, 6, 0, 20, 1f, 1f, ramChance: ramChance, healthMultiplier: 1m);
            session.Battle.IsBoss = false;
            session.Battle.Stage = NavalBattleStage.Naval;
            Pose(session, true, 0, 0);
            Pose(session, false, 200, 180);
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.NavalFields.Cells.Clear();
            UseEncounter(session, id);
            return session;
        }

        private static CoreVoyageSession Boarding(string id, string school)
        {
            var session = Battle(id);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 1000;
            Set(session.Battle.BoardingEnemy, "Deck", new CoreBoardingEnemyDeck(school,
                new CoreBoardingEnemyTurn("等待", new CoreBoardingEnemyCard("wait", "等待"))));
            typeof(CoreVoyageSession).GetMethod("BInitialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
            return session;
        }

        private static void UseEncounter(CoreVoyageSession session, string id) => Set(session, "ActiveEncounter",
            CoreLegendaryWarships.Find(id)?.CreateEncounter() ?? new CoreEncounter(id, "普通敌舰", "海战", "", false, false, 1, 1000));

        private static void Pose(CoreVoyageSession session, bool player, float x, float heading) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session.Battle, new object[] { player, new VoyagePoint(x, 0), heading });

        private static void Field(CoreVoyageSession session, CoreNavalFieldKind kind, int x) =>
            session.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell {
                X = x, Y = 0, InstanceId = x + 1, Kind = kind, PlayerOwned = true, ExpiresAtOwnerTurn = 5, DirectionX = 1
            });

        private static CoreNavalMovementFieldPreview PreviewPath(CoreVoyageSession session,
            IReadOnlyList<CoreNavalMovementNode> path, bool player) => (CoreNavalMovementFieldPreview)typeof(CoreVoyageSession)
                .GetMethod("PreviewNavalFields", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { path, player, null });

        private static void AssertSparring(string text) =>
            Assert.That(text, Does.Contain("认输").And.Contain("切磋").And.Not.Contain("消灭").And.Not.Contain("沉没").And.Not.Contain("死亡"));

        private static void AssertSamePreview(CoreNavalMovementFieldPreview first, CoreNavalMovementFieldPreview second)
        {
            foreach (PropertyInfo property in typeof(CoreNavalMovementFieldPreview).GetProperties().Where(item =>
                item.PropertyType == typeof(int) || item.PropertyType == typeof(bool) || item.PropertyType == typeof(VoyagePoint)))
                Assert.That(property.GetValue(first), Is.EqualTo(property.GetValue(second)), property.Name);
            Assert.That(first.Events.Select(item => item.Damage), Is.EqualTo(second.Events.Select(item => item.Damage)));
        }

        private static string State(CoreVoyageSession session) => string.Join("|", new object[] {
            session.Phase, session.Hull, session.Battle.EnemyHull, session.Battle.PlayerBoardingHealth, session.Battle.EnemyBoardingHealth,
            session.Battle.PlayerBlock, session.Battle.EnemyBlock, session.Battle.PlayerCurseMarks, session.Battle.Schools.CurseMarks,
            session.Battle.EnemyBurnDamage, session.Battle.EnemyBurnTurns, session.Battle.Schools.ThunderChain,
            session.Battle.Boarding.CurseMarks, session.Battle.BoardingEnemy.CurseMarks, session.Battle.Boarding.Revision,
            session.Battle.Turn, session.Battle.PhaseTurn, session.Battle.NavalFields.PlayerTurn, session.Battle.NavalFields.EnemyTurn,
            string.Join(",", session.Battle.NavalFields.TriggerLedger),
            string.Join(",", session.Battle.BoardingFields.Fields.Select(field => field.Kind + ":" + field.RemainingTurns)),
            session.Battle.LastAction, string.Join("\n", session.LogEntries)
        });

        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
