using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class AdditionalBattleVfxTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void BurnFactsContainCommittedGuardAndHealthLossAndClaimOnlyOnce(bool player)
        {
            var session = Start(); var battle = session.Battle;
            battle.PlayerBlock = battle.EnemyBlock = 4;
            battle.PlayerBurnStacks = battle.EnemyBurnTurns = battle.EnemyBurnDamage = 7;
            int before = player ? session.Hull : battle.EnemyHull;
            long sequence = battle.SettlementSequence;
            Invoke(session, player ? "ApplyNavalPlayerBurn" : "ApplySchoolNaturalBurn");
            var cursor = new BattleSettlementCueCursor();
            var facts = cursor.Claim(battle, sequence);
            Assert.That(facts.Count, Is.EqualTo(1));
            Assert.That(facts[0].Kind, Is.EqualTo(CoreBattleSettlementKind.Burn));
            Assert.That(facts[0].Player, Is.EqualTo(player));
            Assert.That(facts[0].GuardLost, Is.EqualTo(4));
            Assert.That(facts[0].HealthLost, Is.EqualTo(3));
            Assert.That(facts[0].HealthBefore, Is.EqualTo(before));
            Assert.That(facts[0].HealthAfter, Is.EqualTo(before-3));
            Assert.That(facts[0].GuardBefore, Is.EqualTo(4));
            Assert.That(facts[0].GuardAfter, Is.Zero);
            Assert.That(player ? session.Hull : battle.EnemyHull, Is.EqualTo(before-3));
            Assert.That(cursor.Claim(battle, sequence), Is.Empty);
            long after = battle.SettlementSequence;
            session.GetEnemyIntent(); session.GetEnemyIntent();
            Assert.That(battle.SettlementSequence, Is.EqualTo(after), "Read-only previews must not manufacture effects.");
        }

        [Test]
        public void BurnFollowedByCurseExecutionRecordsTwoDistinctCauses()
        {
            var session = Start(); var battle = session.Battle;
            battle.EnemyHull = 10; battle.EnemyBlock = 0; battle.EnemyBurnTurns = battle.EnemyBurnDamage = 3;
            typeof(CoreSchoolBattleState).GetProperty("CurseMarks").SetValue(battle.Schools, 8);
            long sequence = battle.SettlementSequence;
            Invoke(session, "ApplySchoolNaturalBurn");
            var facts = new BattleSettlementCueCursor().Claim(battle, sequence);
            Assert.That(facts.Select(f => f.Kind), Is.EqualTo(new[] {CoreBattleSettlementKind.Burn, CoreBattleSettlementKind.CurseExecution}));
            Assert.That(facts.Select(f => f.HealthLost), Is.EqualTo(new[] {3,7}), "The execution must never be called extra burn damage.");
            Assert.That(battle.EnemyHull, Is.Zero);
            Assert.That(CoreGameplayDemoController.BattleSettlementText(facts[1]), Is.EqualTo("咒印消灭"));
        }

        [TestCase("enemy_boarding_fire_01a", "fire", BoardingSchoolCastRole.Charge)]
        [TestCase("enemy_boarding_fire_03a", "fire", BoardingSchoolCastRole.Affliction)]
        [TestCase("enemy_boarding_curse_02", "curse", BoardingSchoolCastRole.Affliction)]
        [TestCase("enemy_boarding_tide_01", "tide", BoardingSchoolCastRole.Guard)]
        [TestCase("enemy_boarding_tide_03a", "tide", BoardingSchoolCastRole.Heal)]
        [TestCase("enemy_boarding_arcane_01", "arcane", BoardingSchoolCastRole.Guard)]
        public void EnemySupportUsesAuthoredIdsAndMechanics(string id, string schoolId, BoardingSchoolCastRole expected)
        {
            var result = new CoreBoardingEnemyActionResult();
            typeof(CoreBoardingEnemyActionResult).GetProperty("CardId").SetValue(result,id);
            typeof(CoreBoardingEnemyActionResult).GetProperty("Name").SetValue(result,"Unrelated translated title");
            Assert.That(BoardingEnemyCastPlan.TryResolve(CoreBoardingEnemyCatalog.ForSchool(schoolId),result,out _,out var role),Is.True);
            Assert.That(role,Is.EqualTo(expected));
            typeof(CoreBoardingEnemyActionResult).GetProperty("RawAttack").SetValue(result,1);
            Assert.That(BoardingEnemyCastPlan.TryResolve(CoreBoardingEnemyCatalog.ForSchool(schoolId),result,out _,out _),Is.False,
                "An executed attack keeps the existing attack sequence, without a second support cast.");
        }

        [Test]
        public void TurnFactsRespectPlayerAndEnemyWindows()
        {
            var session = Start(); var battle=session.Battle;
            battle.PlayerBurnStacks=3; battle.EnemyBurnTurns=battle.EnemyBurnDamage=2;
            long sequence=battle.SettlementSequence;
            Assert.That(session.EndBattleTurn(),Is.True);
            var facts = new BattleSettlementCueCursor().Claim(battle,sequence);
            Assert.That(facts.Any(f=>f.Player&&f.Kind==CoreBattleSettlementKind.Burn&&f.Window==CoreBattleSettlementWindow.PlayerEnd),Is.True);
            Assert.That(facts.Any(f=>!f.Player&&f.Kind==CoreBattleSettlementKind.Burn&&f.Window==CoreBattleSettlementWindow.EnemyEnd),Is.True);
        }

        private static void Invoke(CoreVoyageSession session,string name) =>
            typeof(CoreVoyageSession).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(session,null);
        private static CoreVoyageSession Start()
        {
            var session=new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress {pirateExperience=CoreVoyageSession.PirateExperienceForLevel(10)});
            Assert.That(session.RestoreCardPackProgress(null,session.PirateLevel),Is.True);
            session.SetSeed("ADDITIONAL-BATTLE-VFX"); session.StartVoyage();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[4]),Is.True);
            Assert.That(session.EndBattleTurn(),Is.True);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session,1000);
            session.Battle.EnemyHull=session.Battle.EnemyMaxHull=10000;
            session.Battle.PlayerBlock=session.Battle.EnemyBlock=0;
            return session;
        }
    }
}
