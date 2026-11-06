using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBloodFieldRevision20261024Tests
    {
        [TestCase(true)] [TestCase(false)]
        public void BloodPoolUsesTwoByTwoLowerLeftFootprintAndFiveOpposingTurnEnds(bool owner)
        {
            CoreVoyageSession s = NewBattle();
            Pose(s, owner, 0, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, owner, 4, 0, false, 0, 0), Is.True);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            foreach (int x in new[] { 4, 5 }) foreach (int y in new[] { 0, 1 })
                Assert.That(s.GetNavalFieldAt(x, y), Is.Not.Null);
            for (int remaining = 5; remaining > 0; remaining--)
            {
                s.NavalFieldsBeginTurn(owner);
                s.NavalFieldsEndTurn(owner);
                Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(4, 0)), Is.EqualTo(remaining));
                s.NavalFieldsBeginTurn(!owner);
                s.NavalFieldsEndTurn(!owner);
                if (remaining > 1) Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(4, 0)), Is.EqualTo(remaining - 1));
            }
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [TestCase(true, true)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(false, false)]
        public void EveryPaymentGrantsDoubleGuardToEitherCharacterInEitherOwnersPool(bool player, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            int x = player ? 0 : 2;
            Field(s, x, 0, owner, 5);
            long sequence = s.Battle.NavalFields.PresentationSequence;
            PayHook(s, player, 3);
            PayHook(s, player, 8);
            Assert.That(player ? s.Battle.PlayerBlock : s.Battle.EnemyBlock, Is.EqualTo(22));
            Assert.That(s.Battle.NavalFields.TriggerLedger, Is.Empty, "Payments do not use a once-per-turn ledger.");
            Assert.That(s.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence + 2));
            CoreNavalFieldPresentationEvent last = s.Battle.NavalFields.PresentationEventAt(s.Battle.NavalFields.PresentationEventCount - 1);
            Assert.That(last.TargetPlayer, Is.EqualTo(player));
            Assert.That(last.Kind, Is.EqualTo(CoreNavalFieldKind.Blood));
            Assert.That(last.MovementTrigger, Is.False);
        }

        [TestCase(0)] [TestCase(-1)]
        public void ZeroOrInvalidPaymentCannotTriggerBloodPool(int amount)
        {
            CoreVoyageSession s = NewBattle();
            Field(s, 0, 0, true, 5);
            long sequence = s.Battle.NavalFields.PresentationSequence;
            PayHook(s, true, amount);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void PaymentOutsideBloodPoolOrAfterEnteringBoardingHasNoFieldGuard()
        {
            CoreVoyageSession s = NewBattle();
            Field(s, 1, 0, true, 5);
            PayHook(s, true, 9);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Field(s, 0, 0, false, 5);
            s.Battle.Stage = NavalBattleStage.Boarding;
            PayHook(s, true, 9);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
        }

        [Test]
        public void EnteringAndMovementPreviewGrantNoBloodBuffsWhileStandingDrainsHull()
        {
            CoreVoyageSession s = NewBattle();
            Pose(s, false, 500, 0);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Blood, true, 1, 0, false, 0, 0), Is.True);
            Set(s.Battle, "NavalActionPoints", 2);
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(100, 0), out _), Is.True);
            CoreNavalMovementFieldPreview preview = s.NavalMovementFieldPreview;
            Assert.That(preview.FinalBlock, Is.Zero);
            Assert.That(preview.AttackBonus, Is.Zero);
            Assert.That(preview.Events, Is.Empty);
            Assert.That(s.CommitNavalMovement(out _), Is.True);
            s.NavalFieldsBeginTurn(true);
            int hull = s.Hull;
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
            Assert.That(s.Hull, Is.EqualTo(hull - 5));
            Assert.That(s.Battle.NavalFields.TriggerLedger.Count, Is.EqualTo(1), "The end-turn drain is recorded once.");
        }

        [TestCase("l05")] [TestCase("e01")] [TestCase("l01")]
        public void BloodExpansionGrowsAllFriendlySourcesOneCardinalCellAndUsesLongestDuration(string key)
        {
            CoreVoyageSession s = NewBattle();
            Field(s, 0, 1, true, 3);
            Field(s, 2, 1, true, 8);
            Field(s, 12, 0, true, 6);
            Field(s, -1, 1, false, 9);
            int enemyInstance = s.GetNavalFieldAt(-1, 1).InstanceId;
            Play(s, Component(key));
            Assert.That(s.GetNavalFieldAt(1, 1), Is.Not.Null);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(1, 1)), Is.EqualTo(8));
            Assert.That(s.GetNavalFieldAt(12, 1), Is.Not.Null, "Remote sources participate even outside four cells.");
            Assert.That(s.GetNavalFieldAt(13, 0), Is.Not.Null);
            Assert.That(s.GetNavalFieldAt(14, 0), Is.Null, "Newly added cells cannot expand again in the same cast.");
            Assert.That(s.GetNavalFieldAt(-1, 1).InstanceId, Is.EqualTo(enemyInstance));
            Assert.That(s.GetNavalFieldAt(-2, 1), Is.Null, "Enemy pools cannot expand.");
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 1)), Is.EqualTo(3));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(12, 0)), Is.EqualTo(6));
        }

        [TestCase("e02", 2)] [TestCase("l02", 5)]
        public void BloodExtensionReachesRemoteFriendlyCellsAndPreservesEnemyDuration(string key, int turns)
        {
            CoreVoyageSession s = NewBattle();
            Field(s, 0, 1, true, 3);
            Field(s, 12, 0, true, 8);
            Field(s, -1, 1, false, 9);
            Play(s, Component(key));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 1)), Is.EqualTo(3 + turns));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(12, 0)), Is.EqualTo(8 + turns));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(-1, 1)), Is.EqualTo(9));
        }

        [TestCase("e05", 6)] [TestCase("e04", 4)] [TestCase("l04", 8)]
        public void BloodRepairComponentsUseOnlyFriendlyPoolsWithoutSpendingWood(string key, int repair)
        {
            CoreVoyageSession s = NewBattle();
            Set(s, "Hull", 20);
            Field(s, 0, 0, true, 5);
            Play(s, Component(key));
            Assert.That(s.Hull, Is.EqualTo(20 + repair));
            Assert.That(s.Cargo.Wood, Is.Zero);
        }

        [TestCase("e04", true)] [TestCase("e04", false)]
        [TestCase("l04", true)] [TestCase("l04", false)]
        public void BloodShelterGrantedOutsidePoolSurvivesRestoreAndExpiresAtNextPlayerStart(string key, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleCard card = Component(key);
            int hull = s.Hull;
            Play(s, card);
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(card.Block), "No pool is required for the base guard or protection.");
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityMask, Is.EqualTo(1 << (int)CoreNavalFieldKind.Blood));
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityExpires, Is.EqualTo(s.Battle.NavalFields.PlayerTurn + 1));
            Field(s, 0, 0, owner, 5);
            CoreNavalFieldState snapshot = s.CaptureNavalFields();
            s.ClearNavalFields();
            Assert.That(s.RestoreNavalFields(snapshot), Is.True, "The new Blood protection bit must survive a field-state round trip.");

            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            Assert.That(s.Hull, Is.EqualTo(hull));
            s.NavalFieldsBeginTurn(false);
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityMask, Is.EqualTo(1 << (int)CoreNavalFieldKind.Blood));
            s.NavalFieldsEndTurn(false);
            s.NavalFieldsBeginTurn(true);
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityMask, Is.Zero);
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityExpires, Is.Zero);
            int loss = s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0));
            Assert.That(loss, Is.GreaterThan(0));
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Hull, Is.EqualTo(hull - loss), "Protection ends at the next player start, not that turn's end.");
        }

        [TestCase("e04", true)] [TestCase("e04", false)]
        [TestCase("l04", true)] [TestCase("l04", false)]
        public void BloodShelterPreservesFriendlyBonusesAndRealCardHealthPayments(string key, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            Set(s, "Hull", 20);
            Field(s, 0, 0, owner, 5);
            CoreBattleCard card = Component(key);
            Play(s, card);
            Assert.That(s.Hull, Is.EqualTo(20 + (owner ? card.SchoolEffect.Secondary : 0)));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(card.Block + (owner ? card.SchoolEffect.Power : 0)));
            Assert.That(s.Cargo.Wood, Is.Zero);
            int hull = s.Hull, guard = s.Battle.PlayerBlock;

            Play(s, CoreVoyageSession.CardLibrary.Single(c => c.Id == "card_school_blood_b05"));
            Assert.That(s.Hull, Is.EqualTo(hull - 5), "Field protection cannot waive the card's health cost.");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard + 13 + 10), "The paid card's guard and double pool rebate both remain active.");
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Hull, Is.EqualTo(hull - 5), "Only the passive pool loss is prevented.");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(guard + 23));
        }

        [TestCase(true)] [TestCase(false)]
        public void EnemyBloodProtectionPreviewAndActualEndLossAgreeBeforeAndAfterExpiry(bool owner)
        {
            CoreVoyageSession s = NewBattle();
            s.NavalFieldsBeginTurn(false);
            Field(s, 2, 0, owner, 5);
            s.Battle.NavalFields.Enemy.FieldImmunityMask = 1 << (int)CoreNavalFieldKind.Blood;
            s.Battle.NavalFields.Enemy.FieldImmunityExpires = s.Battle.NavalFields.EnemyTurn + 1;
            Assert.That(s.RestoreNavalFields(s.CaptureNavalFields()), Is.True);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.GetEnemyNavalFieldPreview(0).Summary, Does.Not.Contain("血池"));
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));

            s.NavalFieldsBeginTurn(false);
            Assert.That(s.Battle.NavalFields.Enemy.FieldImmunityMask, Is.Zero);
            int loss = s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(2, 0));
            Assert.That(s.GetEnemyNavalFieldPreview(0).Summary, Does.Contain("回合末流失 " + loss + " 点船体"));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull), "Reading intent must not settle the loss.");
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - loss));
        }

        [Test]
        public void UpcomingEnemyTurnPreviewExpiresBloodProtectionWithoutChangingLiveState()
        {
            CoreVoyageSession s = NewBattle();
            Field(s, 2, 0, true, 5);
            CoreNavalFieldShipState enemy = s.Battle.NavalFields.Enemy;
            enemy.FieldImmunityMask = 1 << (int)CoreNavalFieldKind.Blood;
            enemy.FieldImmunityExpires = s.Battle.NavalFields.EnemyTurn + 1;
            int expiry = enemy.FieldImmunityExpires;
            Assert.That(s.GetEnemyNavalFieldPreview(0).Summary, Does.Contain("回合末流失 5 点船体"));
            Assert.That(enemy.FieldImmunityMask, Is.EqualTo(1 << (int)CoreNavalFieldKind.Blood));
            Assert.That(enemy.FieldImmunityExpires, Is.EqualTo(expiry));
            int hull = s.Battle.EnemyHull;
            s.NavalFieldsBeginTurn(false);
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 5));
        }

        [Test]
        public void BloodProtectionSnapshotAcceptsCombinedKnownBitsButRejectsInvalidBitsAndExpiry()
        {
            CoreVoyageSession s = NewBattle();
            Play(s, Component("e04"));
            CoreNavalFieldState snapshot = s.CaptureNavalFields();
            int validMask = (1 << (int)CoreNavalFieldKind.Blood) | (1 << (int)CoreNavalFieldKind.Curse);
            snapshot.Player.FieldImmunityMask = validMask;
            Assert.That(s.RestoreNavalFields(snapshot), Is.True);
            int expiry = s.Battle.NavalFields.Player.FieldImmunityExpires;
            foreach (int invalidBit in new[] { 1 << (int)CoreNavalFieldKind.Arcane, 1 << 30 })
            {
                snapshot = s.CaptureNavalFields();
                snapshot.Player.FieldImmunityMask |= invalidBit;
                Assert.That(s.RestoreNavalFields(snapshot), Is.False);
                Assert.That(s.Battle.NavalFields.Player.FieldImmunityMask, Is.EqualTo(validMask));
            }
            snapshot = s.CaptureNavalFields();
            snapshot.Player.FieldImmunityExpires++;
            Assert.That(s.RestoreNavalFields(snapshot), Is.False);
            Assert.That(s.Battle.NavalFields.Player.FieldImmunityExpires, Is.EqualTo(expiry));
        }

        [Test]
        public void RestoringLegacyFieldSnapshotDiscardsRetiredAttackCharges()
        {
            CoreVoyageSession s = NewBattle();
            CoreNavalFieldState snapshot = s.CaptureNavalFields();
            snapshot.Player.BloodAttackBonus = snapshot.Enemy.BloodAttackBonus = 4;
            Assert.That(s.RestoreNavalFields(snapshot), Is.True);
            Assert.That(s.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.BloodAttackBonus, Is.Zero);
        }

        private static CoreVoyageSession NewBattle()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(null, CoreSpecialCardCatalog.All.Where(d => d.SourceKind == CoreSpecialCardSourceKind.Component).Select(d => d.SourceId).ToArray());
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.Energy = 100;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Cargo.Powder = s.Cargo.Cannonballs = 100;
            s.Cargo.Wood = 0;
            Pose(s, true, 0, 0); Pose(s, false, 200, 0);
            return s;
        }
        private static CoreBattleCard Component(string key) => CoreSpecialCardCatalog.All.Select(d => d.Card)
            .Single(c => c.ArtId == "card_school_blood_" + key);
        private static void Play(CoreVoyageSession s, CoreBattleCard card)
        {
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == card.Id);
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(index);
            Assert.That(s.PlayCard(0), Is.True, card.Id + ": " + s.CardUnavailableReason(0));
        }
        private static CoreNavalFieldCell Field(CoreVoyageSession s, int x, int y, bool owner, int turns)
        {
            var cell = new CoreNavalFieldCell { X = x, Y = y, Kind = CoreNavalFieldKind.Blood, PlayerOwned = owner,
                InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = turns +
                    (owner ? s.Battle.NavalFields.CompletedEnemyTurns : s.Battle.NavalFields.CompletedPlayerTurns) };
            s.Battle.NavalFields.Cells.Add(cell);
            return cell;
        }
        private static void PayHook(CoreVoyageSession s, bool player, int amount) => typeof(CoreVoyageSession)
            .GetMethod("ApplyNavalBloodPoolGuard", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, new object[] { player, amount });
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession s, bool player, float x, float y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x, y), player ? 0f : 180f });
    }
}
