using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreFireRules20261024Tests
    {
        [TestCase("card_chain_shot")]
        [TestCase("card_school_fire_b05")]
        [TestCase("card_school_fire_n02")]
        public void AttachedFireSpendsAllStacksOnNextAttackEvenWhenBlocked(string id)
        {
            CoreVoyageSession s = Start();
            s.Battle.PlayerFireRounds = 7;
            s.Battle.EnemyBlock = 500;
            int hull = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero);
            int intrinsic = id == "card_school_fire_b05" ? 4 : 0;
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(7 + intrinsic));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
        }

        [Test]
        public void AttachedFireStacksWithoutAmmunitionCapAndUtilityDoesNotConsumeIt()
        {
            CoreVoyageSession s = Start();
            s.Battle.PlayerFireRounds = 8;
            s.Battle.Stage = NavalBattleStage.Preparation;
            Play(s, "card_school_fire_b01");
            s.Battle.Stage = NavalBattleStage.Naval;
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(10));
            Play(s, "card_brace");
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(10));
            Play(s, "card_chain_shot");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(10));
            Play(s, "card_chain_shot");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(10));
        }

        [Test]
        public void OilDoublesOnlyTheNextBurnApplicationAndAddsNoDamage()
        {
            CoreVoyageSession s = Start();
            Play(s, "card_school_fire_n03");
            int hull = s.Battle.EnemyHull;
            Invoke(s, "ApplyFireBurn", false, 3);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Invoke(s, "ApplyFireBurn", false, 2);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(8));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NavalBurnUsesGuardThenHealthAndDecaysExactlyOnce(bool player)
        {
            CoreVoyageSession s = Start();
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 4;
            int health = player ? s.Hull : s.Battle.EnemyHull;
            Invoke(s, "ApplyFireBurn", player, 7);
            Invoke(s, player ? "ApplyNavalPlayerBurn" : "ApplySchoolNaturalBurn");
            Assert.That(player ? s.Hull : s.Battle.EnemyHull, Is.EqualTo(health - 3));
            Assert.That(player ? s.Battle.PlayerBlock : s.Battle.EnemyBlock, Is.Zero);
            Assert.That(player ? s.Battle.PlayerBurnStacks : s.Battle.EnemyBurnTurns, Is.EqualTo(6));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero, "Burn is not an active attack.");
        }

        [Test]
        public void PreventingNewBurnPreservesExistingStacksAndDoesNotConsumeOil()
        {
            CoreVoyageSession s = Start();
            Invoke(s, "ApplyFireBurn", false, 3);
            s.GrantNavalFireImmunity(false);
            s.Battle.NavalExpansion.OilPlayerEndsRemaining = 1;
            Invoke(s, "ApplyFireBurn", false, 5);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(3));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.EqualTo(1));
            s.NavalFieldsBeginTurn(false);
            Invoke(s, "ApplyFireBurn", false, 2);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(7));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PreparedFireGuardRequiresPlayerCausedEnemyBurn(bool playerOwned)
        {
            CoreVoyageSession s = Start();
            typeof(CoreSchoolBattleState).GetProperty("PrepareFireGuard").SetValue(s.Battle.Schools, 5);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, playerOwned, 0, -1, false, 0, 0), Is.True,
                s.CanPlaceNavalField(CoreNavalFieldKind.Fire, playerOwned, 0, -1, false, 0, 0));
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(5));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(playerOwned ? 5 : 0));
            Assert.That(s.Battle.Schools.PrepareFireGuard, Is.EqualTo(playerOwned ? 0 : 5));
            Invoke(s, "ApplyFireBurn", false, 2);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(5));
            Assert.That(s.Battle.Schools.PrepareFireGuard, Is.Zero);
        }

        [Test]
        public void CurrentCatalogNeverDisplaysRetiredFireKeywordsInEffects()
        {
            var s = new CoreVoyageSession();
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(card => card.AllowedStages != NavalBattleStages.None))
                foreach (NavalBattleStage stage in Enum.GetValues(typeof(NavalBattleStage)))
                {
                    if (!card.IsAllowedInStage(stage)) continue;
                    CoreBattleCard resolved = stage == NavalBattleStage.Boarding ? CoreBoardingCardCatalog.ForBoarding(card) : card;
                    Assert.That(resolved.Description, Does.Not.Match("烈焰强化|灼烧|燃尽|自燃|火场免疫"), card.Id + " / " + stage);
                }
        }

        private static object Invoke(CoreVoyageSession s, string method, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("FIRE-REWRITE-20261024"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { true, new VoyagePoint(0, 0), 0f });
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { false, new VoyagePoint(100, 0), 180f });
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 1000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear();
            s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
    }
}
