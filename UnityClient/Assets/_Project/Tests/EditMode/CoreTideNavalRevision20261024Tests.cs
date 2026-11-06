using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreTideNavalRevision20261024Tests
    {
        [TestCase("card_school_tide_b01", NavalBattleStage.Preparation)]
        [TestCase("card_school_tide_b02", NavalBattleStage.Preparation)]
        [TestCase("card_school_tide_b04", NavalBattleStage.Naval)]
        [TestCase("card_school_tide_b05", NavalBattleStage.Naval)]
        [TestCase("card_school_tide_n03", NavalBattleStage.Naval)]
        public void WoodBoostConvertsAllExistingGuardAndNewGuard(string id, NavalBattleStage stage)
        {
            var s = Start(); s.Battle.Stage = stage; s.Battle.PlayerBlock = 9;
            int wood = s.Cargo.Wood;
            Play(s, id);
            Assert.That(s.Cargo.Wood, Is.EqualTo(wood - 1));
            Assert.That(s.Battle.PlayerBlock, Is.GreaterThan(9));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(s.Battle.PlayerBlock));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero, "Conversion itself does not absorb damage.");
        }

        [TestCase("card_school_tide_b04")]
        [TestCase("card_school_tide_n03")]
        public void WithoutWoodExistingOrdinaryGuardIsNotConverted(string id)
        {
            var s = Start(); s.Cargo.Wood = 0; s.Battle.PlayerBlock = 9;
            Play(s, id);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(11));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.Zero);
        }

        [TestCase("burn")]
        [TestCase("direct")]
        [TestCase("thunder")]
        public void EveryDamageSourceAwardsOnlyActuallyAbsorbedTideGuard(string source)
        {
            var s = Start(); s.Battle.PlayerBlock = 9; Set(s.Battle.Schools, "TidePaidBlock", 7);
            Set(s.Battle.Schools, "TidePower", 3);
            if (source == "burn") { s.Battle.PlayerBurnStacks = 5; Invoke(s, "ApplyNavalPlayerBurn"); }
            if (source == "direct") Invoke(s, "NavalExpansionDirectDamage", true, 5, 0);
            if (source == "thunder")
            {
                Set(s, "Hull", 2000);
                Set(s.Battle.Schools, "ThunderChain", 45);
                s.Battle.NavalFields.Cells.Add(new CoreNavalFieldCell { Kind = CoreNavalFieldKind.Thunder,
                    X = 0, Y = 0, InstanceId = 99, ExpiresAtOwnerTurn = 5 });
                Invoke(s, "TriggerNavalFieldDamage", true,
                    new CoreNavalFieldCell { Kind = CoreNavalFieldKind.Thunder, X = 0, Y = 0, InstanceId = 99, ExpiresAtOwnerTurn = 5 }, true);
            }
            int damage = source == "thunder" ? 9 : 5;
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(9 - damage));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(9 - damage));
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(3 + damage - 2));
        }

        [Test]
        public void PiercingDamageDoesNotAwardUnabsorbedTide()
        {
            var s = Start(); s.Battle.PlayerBlock = 10; Set(s.Battle.Schools, "TidePaidBlock", 10);
            int hull = s.Hull; Invoke(s, "NavalExpansionDirectDamage", true, 8, 8);
            Assert.That(s.Hull, Is.EqualTo(hull - 8));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(10)); Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [Test]
        public void ReservePersistsAcrossPreparationNavalTurnsAndBoarding()
        {
            var s = Start(); s.Battle.Stage = NavalBattleStage.Preparation;
            Set(s.Battle.Schools, "TidePower", 18);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(18));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(18));
            Pose(s, true, 0, 0, 0); Pose(s, false, 0, 1, 0);
            Assert.That(s.TryBoard(), Is.True);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(18), "Changing stages does not truncate the carried resource to the boarding gain cap.");
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(18));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.Boarding.TidePower, Is.EqualTo(18));
        }

        [TestCase(0f, 1, 0)] [TestCase(90f, 2, -1)]
        [TestCase(180f, 3, 0)] [TestCase(270f, 2, 1)]
        public void RecedingWaveMovesBehindEnemyHeadingAndCapsNextMove(float heading, int x, int y)
        {
            var s = Start(); Pose(s, false, 2, 0, heading); Set(s.Battle.Schools, "TidePower", 3);
            Put(s, "card_school_tide_n04"); Assert.That(s.GetNavalCardChoice(0), Is.Null);
            Assert.That(s.PlayNavalCard(0, null), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(new VoyagePoint(x * 100, y * 100)));
            Assert.That(s.Battle.NavalFields.Enemy.NextMovementMaxCells, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [TestCase(true, true)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(false, false)]
        public void GreatWaveHitsOnlyCharactersInAnyCurrentWithoutPushing(bool playerInside, bool enemyInside)
        {
            var s = Start();
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, 0, 1), Is.True);
            Pose(s, true, playerInside ? 0 : -1, 0, 0);
            Pose(s, false, enemyInside ? 2 : 4, 0, 180);
            // Enemy ownership must not remove a current from this card's target set.
            foreach (var cell in s.Battle.NavalFields.Cells) cell.PlayerOwned = false;
            s.Battle.PlayerBlock = 4; s.Battle.EnemyBlock = 3;
            Set(s.Battle.Schools, "TidePower", 5);
            int hull = s.Hull, enemy = s.Battle.EnemyHull;
            var ownPosition = s.Battle.PlayerPositionMeters; var enemyPosition = s.Battle.EnemyPositionMeters;
            Play(s, "card_school_tide_n06");
            Assert.That(s.Hull, Is.EqualTo(hull - (playerInside ? 13 : 0)));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - (enemyInside ? 14 : 0)));
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(ownPosition));
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(enemyPosition));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [Test]
        public void TideAnchorGrantsCurrentImmunityUntilNextPlayerStartInsteadOfSinglePushCharge()
        {
            var s = Start();
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Tide, true, 0, 0, false, 1, 0), Is.True);
            Play(s, "card_school_tide_n03");
            Assert.That(s.Battle.NavalFields.Player.AnchorCharges, Is.Zero);
            Assert.That(s.PushNavalShip(true, -1, 0), Is.True);
            Pose(s, true, 0, 0, 0);
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 0)));
            s.NavalFieldsBeginTurn(false); s.NavalFieldsEndTurn(false); s.NavalFieldsBeginTurn(true);
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityMask, Is.Zero);
            Assert.That(s.PushNavalShip(true, -1, 0), Is.False, "Reverse-current movement is blocked as soon as the next player turn starts.");
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(100, 0)), "The following player end is no longer protected from the current push.");
        }

        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.SetSeed("TIDE-REVISION-20261024"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0, 0); Pose(s, false, 2, 0, 180);
            Set(s, "Hull", 1000); s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }
        private static void Pose(CoreVoyageSession s, bool player, int x, int y, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { player, new VoyagePoint(x * 100, y * 100), heading });
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static object Invoke(CoreVoyageSession s, string method, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static void Put(CoreVoyageSession s, string id)
        { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id)); }
        private static void Play(CoreVoyageSession s, string id)
        {
            Put(s, id);
            bool played = CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, null);
            Assert.That(played, Is.True, s.CardUnavailableReason(0));
        }
    }
}
