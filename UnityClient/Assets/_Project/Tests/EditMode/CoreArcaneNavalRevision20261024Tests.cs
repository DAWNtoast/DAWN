using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreArcaneNavalRevision20261024Tests
    {
        [TestCase(CoreNavalFieldKind.Fire, true, 5)]
        [TestCase(CoreNavalFieldKind.Fire, false, 5)]
        [TestCase(CoreNavalFieldKind.Arcane, true, 3)]
        [TestCase(CoreNavalFieldKind.Arcane, false, 3)]
        [TestCase(CoreNavalFieldKind.Curse, true, 5)]
        [TestCase(CoreNavalFieldKind.Curse, false, 5)]
        [TestCase(CoreNavalFieldKind.Tide, true, 5)]
        [TestCase(CoreNavalFieldKind.Tide, false, 5)]
        [TestCase(CoreNavalFieldKind.Thunder, true, 5)]
        [TestCase(CoreNavalFieldKind.Thunder, false, 5)]
        [TestCase(CoreNavalFieldKind.Blood, true, 5)]
        [TestCase(CoreNavalFieldKind.Blood, false, 5)]
        public void AllFieldsOnlyLoseDurationAtTheirOpponentsTurnEnd(CoreNavalFieldKind kind, bool owner, int duration)
        {
            CoreVoyageSession s = Start();
            Place(s, kind, owner, 1, 1);
            CoreNavalFieldCell cell = s.GetNavalFieldAt(1, 1);
            Assert.That(s.GetNavalFieldRemainingTurns(cell), Is.EqualTo(duration));
            s.NavalFieldsBeginTurn(owner);
            s.NavalFieldsEndTurn(owner);
            Assert.That(s.GetNavalFieldRemainingTurns(cell), Is.EqualTo(duration));
            s.NavalFieldsBeginTurn(!owner);
            Assert.That(s.GetNavalFieldRemainingTurns(cell), Is.EqualTo(duration));
            s.NavalFieldsEndTurn(!owner);
            Assert.That(s.GetNavalFieldRemainingTurns(cell), Is.EqualTo(duration - 1));
            s.NavalFieldsEndTurn(!owner);
            Assert.That(s.GetNavalFieldRemainingTurns(cell), Is.EqualTo(duration - 1), "Repeated settlement is idempotent.");
            CoreNavalFieldState checkpoint = s.CaptureNavalFields();
            s.ClearNavalFields();
            Assert.That(s.RestoreNavalFields(checkpoint), Is.True);
            for (int turn = 1; turn < duration; turn++)
            {
                s.NavalFieldsBeginTurn(!owner);
                s.NavalFieldsEndTurn(!owner);
            }
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void RefractionGrantsItsRemainingDurationToEitherSideAtStart(bool owner, bool recipient)
        {
            CoreVoyageSession s = Start();
            int x = recipient ? 0 : 6;
            SetSchool(s, "Mana", 7);
            s.Battle.NavalFields.Enemy.Mana = 99;
            Place(s, CoreNavalFieldKind.Arcane, owner, x, 0);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(7), "Placing a field grants no mana.");
            Assert.That(s.Battle.NavalFields.Enemy.Mana, Is.EqualTo(99));
            s.NavalFieldsBeginTurn(!owner);
            s.NavalFieldsEndTurn(!owner);
            // Clear an earlier start benefit so this assertion isolates the next start.
            SetSchool(s, "Mana", 7);
            s.Battle.NavalFields.Enemy.Mana = 99;
            s.NavalFieldsBeginTurn(true);
            if (!recipient) s.NavalFieldsBeginTurn(false);
            Assert.That(recipient ? s.Battle.Schools.Mana : s.Battle.NavalFields.Enemy.Mana,
                Is.EqualTo(recipient ? 9 : 5));
            Assert.That(s.Battle.Schools.TemporaryMana, Is.Zero);
            Assert.That(s.Battle.NavalFields.Player.ArcanePiercing + s.Battle.NavalFields.Enemy.ArcanePiercing, Is.Zero);
        }

        [Test]
        public void FinalFireTickSettlesOldBurnThenAddsNewBurnBeforeExpiry()
        {
            CoreVoyageSession s = Start();
            Place(s, CoreNavalFieldKind.Fire, false, 0, 0);
            Place(s, CoreNavalFieldKind.Fire, true, 6, 0);
            foreach (CoreNavalFieldCell cell in s.Battle.NavalFields.Cells)
                cell.ExpiresAtOwnerTurn = (cell.PlayerOwned ? s.Battle.NavalFields.CompletedEnemyTurns : s.Battle.NavalFields.CompletedPlayerTurns) + 1;
            s.Battle.PlayerBurnStacks = 3;
            s.Battle.PlayerBlock = 3;
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 3;
            int hull = s.Hull, enemy = s.Battle.EnemyHull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(hull));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - 1), "Enemy guard absorbs two of the three existing burn damage.");
            Assert.That(s.Battle.PlayerBurnStacks, Is.EqualTo(3));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(3));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [TestCase("card_special_bow_voidray_edge")]
        [TestCase("card_special_bow_last_horizon")]
        [TestCase("card_special_instrument_last_horizon_lens")]
        public void ArcaneExpansionUsesAllFriendlySourcesAndLongestInheritedLifetime(string id)
        {
            CoreVoyageSession s = Start();
            Pose(s, false, 200, 0);
            AddCell(s, CoreNavalFieldKind.Arcane, true, 0, 0, 2);
            AddCell(s, CoreNavalFieldKind.Arcane, true, 2, 0, 5);
            AddCell(s, CoreNavalFieldKind.Arcane, true, 20, 0, 3);
            AddCell(s, CoreNavalFieldKind.Arcane, false, -1, 0, 4);
            AddCell(s, CoreNavalFieldKind.Blood, true, 0, -1, 2);
            Play(s, id);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(1, 0)), Is.EqualTo(5));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(21, 0)), Is.EqualTo(3));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(2), "Mastery no longer extends fields.");
            Assert.That(s.GetNavalFieldAt(-1, 0).PlayerOwned, Is.False);
            Assert.That(s.GetNavalFieldAt(0, -1).Kind, Is.EqualTo(CoreNavalFieldKind.Blood));
            Assert.That(s.GetNavalFieldAt(4, 0), Is.Null, "New cells do not expand in the same cast.");
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(s.RestoreNavalFields(s.CaptureNavalFields()), Is.True);
        }

        [TestCase("card_special_sail_moonweave", 1)]
        [TestCase("card_special_sail_last_free_wind", 2)]
        public void ArcaneSailsExtendAllFriendlyFieldsOnly(string id, int extra)
        {
            CoreVoyageSession s = Start();
            AddCell(s, CoreNavalFieldKind.Arcane, true, 20, 0, 3);
            AddCell(s, CoreNavalFieldKind.Arcane, false, 0, 0, 3);
            AddCell(s, CoreNavalFieldKind.Fire, true, 1, 0, 3);
            Play(s, id);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(20, 0)), Is.EqualTo(3 + extra));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(3));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(1, 0)), Is.EqualTo(3));
        }

        [TestCase("card_special_instrument_lunar_astrolabe", 2)]
        [TestCase("card_special_plating_moonsilver_skin", 1)]
        [TestCase("card_special_plating_worldtree_bark", 2)]
        public void ComponentManaCanExceedTheResourceLimit(string id, int added)
        {
            CoreVoyageSession s = Start();
            AddCell(s, CoreNavalFieldKind.Arcane, true, 0, 0, 3);
            SetSchool(s, "Mana", 5); SetSchool(s, "ManaLimit", 3);
            Play(s, id);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(5 + added));
        }

        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(null, CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Component).Select(c => c.SourceId).ToArray());
            s.SetSeed("ARCANE-NAVAL-REVISION"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 600, 0);
            s.Battle.Energy = 100; s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000;
            s.Cargo.Powder = s.Cargo.Cannonballs = 100;
            return s;
        }
        private static void AddCell(CoreVoyageSession s, CoreNavalFieldKind kind, bool owner, int x, int y, int remaining)
        {
            CoreNavalFieldState state = s.Battle.NavalFields;
            state.Cells.Add(new CoreNavalFieldCell { Kind = kind, PlayerOwned = owner, X = x, Y = y,
                InstanceId = state.NextInstanceId++, ExpiresAtOwnerTurn = (owner ? state.CompletedEnemyTurns : state.CompletedPlayerTurns) + remaining });
        }
        private static void Place(CoreVoyageSession s, CoreNavalFieldKind kind, bool owner, int x, int y)
        {
            VoyagePoint original = owner ? s.Battle.PlayerPositionMeters : s.Battle.EnemyPositionMeters;
            Pose(s, owner, (x + 1) * 100, (y + 1) * 100);
            Assert.That(s.PlaceNavalField(kind, owner, x, y, false, kind == CoreNavalFieldKind.Tide ? 1 : 0, 0), Is.True);
            Pose(s, owner, original.X, original.Y);
        }
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear();
            s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id));
            Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0));
        }
        private static void SetSchool(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
        private static void Pose(CoreVoyageSession s, bool player, float x, float y) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(s.Battle, new object[] { player, new VoyagePoint(x, y), player ? 0f : 180f });
    }
}
