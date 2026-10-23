using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreGuidedBossScriptTests
    {
        internal static CoreVoyageSession StartBoss(int supplies = 5, int upgrade = 0)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCardUpgrades(CoreVoyageSession.CardLibrary.Where(c => c.AllowedStages != NavalBattleStages.None)
                .ToDictionary(c => c.Id, c => upgrade));
            session.ConfigureComponentBonuses(30, 30, 30, 0, 0);
            session.ConfigureBattleEquipment(5.5f, 5.5f);
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Medicine = session.Cargo.Wood = supplies;
            session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            session.StartVoyage();
            SailToEncounter(session, CoreGuidedTutorialContent.BossEventId);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            Assert.That(session.ResolveChoice(0), Is.True);
            return session;
        }

        internal static void SailToEncounter(CoreVoyageSession session, string expected)
        {
            session.SetHeading(SailingDirection.East); session.SetDistance(120);
            for (int i = 0; i < 5 && session.Phase == CoreDemoPhase.Sailing; i++) Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(expected));
        }

        internal static int PlayToCompletion(CoreVoyageSession session)
        {
            int actions = 0;
            while (session.Phase == CoreDemoPhase.Battle && actions++ < 250) Advance(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing), session.GuidedBossStep + ": " + session.Battle?.LastAction);
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            return actions;
        }

        internal static void Advance(CoreVoyageSession session)
        {
            string reason;
            switch (session.GuidedBossExpectedAction)
            {
                case GuidedBossActionKind.Inspect:
                    Assert.That(session.TryInspectGuidedBoss(session.GuidedBossInspectionTopic, out reason), Is.True, reason); break;
                case GuidedBossActionKind.Continue:
                    Assert.That(session.GuidedBossExerciseFailed, Is.False, session.Battle.LastAction);
                    Assert.That(session.TryContinueGuidedBossLesson(out reason), Is.True, reason); break;
                case GuidedBossActionKind.Card:
                    int hand = ExpectedHand(session);
                    Assert.That(hand, Is.GreaterThanOrEqualTo(0), "Required card is missing: " + session.GuidedBossStep);
                    Assert.That(session.PlayCard(hand), Is.True, session.GuidedBossStep + ": " + session.CardUnavailableReason(hand)); break;
                case GuidedBossActionKind.BlockedCard:
                    Assert.That(session.TryObserveGuidedBossBlockedCard(ExpectedHand(session), out reason), Is.True, reason); break;
                case GuidedBossActionKind.EndTurn: Assert.That(session.EndBattleTurn(), Is.True); break;
                case GuidedBossActionKind.Movement:
                    var p = session.Battle.PlayerPositionMeters;
                    if (session.GuidedBossStep == GuidedBossBattleStep.CourseManeuver)
                    {
                        Append(session, new VoyagePoint(p.X + 100, p.Y));
                        Append(session, new VoyagePoint(p.X + 100, p.Y + 100));
                    }
                    else if (session.GuidedBossStep == GuidedBossBattleStep.CourseGunneryMove)
                        for (int i = 1; i <= 3; i++) Append(session, new VoyagePoint(p.X + 100 * i, p.Y));
                    else Append(session, session.GetNavalLegalNextSteps()[0]);
                    GuidedBossBattleStep step = session.GuidedBossStep;
                    Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
                    Assert.That(session.GuidedBossStep, Is.EqualTo(step));
                    Assert.That(session.ConfirmNavalArrivalHeading(session.GetNavalArrivalHeadings()[0], out reason), Is.True, reason);
                    break;
                case GuidedBossActionKind.FreePlay:
                    if (session.GuidedBossLesson == GuidedBossLesson.BoardingContact) Assert.That(session.TryBoard(), Is.True);
                    else if (session.GuidedBossLesson == GuidedBossLesson.RamContact) Assert.That(session.TryRam(), Is.True);
                    else AdvanceFinal(session);
                    break;
                default: Assert.Fail("No authored action at " + session.GuidedBossStep); break;
            }
        }

        private static void AdvanceFinal(CoreVoyageSession s)
        {
            Assert.That(s.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            if (s.CanRam(out _) && s.TryRam()) return;
            for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
                if (s.CanPlayCard(slot)) { Assert.That(s.PlayCard(slot), Is.True); return; }
            if (s.NavalActionMovementActive && s.Battle.DistanceMeters > 350 && s.GetNavalLegalNextSteps().Count > 0)
            {
                do { Append(s, s.GetNavalLegalNextSteps()[0]); }
                while (s.GetNavalLegalNextSteps().Count > 0 && s.NavalMovementPlannedSteps < 3);
                Assert.That(s.CommitNavalMovement(out string reason), Is.True, reason);
                Assert.That(s.ConfirmNavalArrivalHeading(s.GetNavalArrivalHeadings()[0], out reason), Is.True, reason);
                return;
            }
            Assert.That(s.EndBattleTurn(), Is.True);
        }

        internal static void Reach(CoreVoyageSession session, GuidedBossBattleStep step)
        {
            for (int i = 0; session.GuidedBossStep != step && i < 180; i++) Advance(session);
            Assert.That(session.GuidedBossStep, Is.EqualTo(step));
        }
        private static int ExpectedHand(CoreVoyageSession s) => s.Battle.Hand.FindIndex(i => s.GetBattleCard(i).Id == s.GuidedBossExpectedCardId);
        private static void Append(CoreVoyageSession s, VoyagePoint target) => Assert.That(s.TryAppendNavalMovementStep(target, out string reason), Is.True, reason);

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(5, 3)]
        public void SevenLessonsUseRealActionsAndOnlyFinalVictoryGrantsOneReward(int supplies, int upgrade)
        {
            CoreVoyageSession s = StartBoss(supplies, upgrade);
            int[] decks = s.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            for (int i = 0; s.GuidedBossLesson != GuidedBossLesson.FinalBattle && i < 100; i++)
            {
                Assert.That(s.GuidedTutorialBossDefeated, Is.False);
                Assert.That(s.VoyageTreasureFragments, Is.Zero);
                Assert.That(s.CanCaptureVoyageCheckpoint, Is.False);
                Advance(s);
            }
            Assert.That(s.GuidedBossLesson, Is.EqualTo(GuidedBossLesson.FinalBattle));
            Assert.That(s.Battle.DistanceMeters, Is.EqualTo(600));
            Assert.That(s.TryRestartGuidedBossLesson(out _), Is.False);
            PlayToCompletion(s);
            Assert.That(s.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(s.GetBattleDeck(NavalBattleStage.Naval), Is.EqualTo(decks));
            Assert.That(s.Cargo.Powder, Is.EqualTo(supplies));
            Assert.That(s.Cargo.Cannonballs, Is.EqualTo(supplies));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(supplies));
            Assert.That(s.Cargo.Wood, Is.EqualTo(supplies));
            int cannon = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_chain_shot");
            Assert.That(s.GetBattleCard(cannon).UpgradeLevel, Is.EqualTo(upgrade));
            Assert.That(s.SafeReturn(), Is.True);
            Assert.That(s.Settlement.BankedFragments, Is.EqualTo(1));
            s.ReturnToPortAfterSettlement(); s.ReturnToPortAfterSettlement();
            Assert.That(s.TreasureFragments, Is.EqualTo(1));
        }

        [Test]
        public void ResourceAndCardInspectionsAreIdempotentAndDoNotConsumeAnything()
        {
            CoreVoyageSession s = StartBoss();
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            string snapshot = Snapshot(s);
            for (int i = 0; i < 10; i++)
            { s.GetEnemyIntent(); s.CanPlayCard(0); s.GetNavalLegalNextSteps(); s.GetBattleCardEffects(0, NavalBattleStage.Naval); }
            Assert.That(s.TryInspectGuidedBoss("enemy", out _), Is.False);
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.EndBattleTurn(), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(snapshot));
            Advance(s);
            Assert.That(s.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseCardInspect));
            Assert.That(s.TryInspectGuidedBoss("resources", out _), Is.False);
            Advance(s);
            Assert.That(s.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseFreeMove));
            Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
        }

        [TestCase(0f)] [TestCase(90f)] [TestCase(270f)]
        public void FirstFreeMoveAcceptsAllLegalHeadingsThenCardEnablesAnotherMove(float heading)
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseFreeMove);
            Append(s, new VoyagePoint(-500f, 0f));
            Assert.That(s.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(s.Battle.Energy, Is.EqualTo(3)); Assert.That(s.Battle.NavalActionPoints, Is.Zero);
            Assert.That(s.PlayCard(0), Is.False); Assert.That(s.EndBattleTurn(), Is.False);
            Assert.That(s.ConfirmNavalArrivalHeading(180f, out _), Is.False);
            Assert.That(s.ConfirmNavalArrivalHeading(heading, out reason), Is.True, reason);
            Advance(s);
            Assert.That(s.Battle.Energy, Is.EqualTo(3)); Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
            Advance(s);
            Assert.That(s.GuidedBossCanContinue, Is.True); Assert.That(s.Battle.NavalActionPoints, Is.Zero);
        }

        [TestCase(90f)] [TestCase(0f)] [TestCase(180f)]
        public void MultiCellTurnLessonUsesNormalPathAndArrivalRules(float arrival)
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseManeuver);
            Append(s, new VoyagePoint(-500, 0));
            Assert.That(s.CommitNavalMovement(out _), Is.False);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
            Assert.That(s.TryAppendNavalMovementStep(new VoyagePoint(-600, 0), out _), Is.False);
            Append(s, new VoyagePoint(-500, 100));
            Assert.That(s.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(s.ConfirmNavalArrivalHeading(270f, out _), Is.False);
            Assert.That(s.ConfirmNavalArrivalHeading(arrival, out reason), Is.True, reason);
            Assert.That(s.GuidedBossCanContinue, Is.True);
        }

        [Test]
        public void GunneryUsesRepeatCostsAmmoRealPilesAndReadOnlyRangeFailure()
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseRangeAttempt);
            Assert.That(s.Battle.PlayerLoadedShot, Is.EqualTo(5));
            string before = Snapshot(s);
            Assert.That(s.PlayCard(ExpectedHand(s)), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Advance(s); Advance(s);
            Assert.That(s.Battle.Energy, Is.EqualTo(3)); Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(2));
            Assert.That(s.GetBattleCardEnergyCost(s.Battle.Hand[ExpectedHand(s)]), Is.EqualTo(1));
            Advance(s);
            Assert.That(s.Battle.Energy, Is.EqualTo(2)); Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(3));
            Advance(s); Advance(s);
            Assert.That(s.Battle.Energy, Is.EqualTo(3)); Assert.That(s.Battle.NavalActionPoints, Is.Zero);
            int powder = s.Cargo.Powder, balls = s.Cargo.Cannonballs;
            Advance(s);
            Assert.That(s.Cargo.Powder, Is.EqualTo(powder - 1)); Assert.That(s.Cargo.Cannonballs, Is.EqualTo(balls - 1));
            Assert.That(s.Battle.PlayerLoadedShot, Is.Zero);
            Reach(s, GuidedBossBattleStep.CourseDraw);
            Assert.That(s.Battle.DrawPile, Is.Empty);
            Assert.That(s.Battle.DiscardPile.Count, Is.GreaterThan(0));
            int deckSize = s.Battle.Hand.Count + s.Battle.DrawPile.Count + s.Battle.DiscardPile.Count;
            Advance(s);
            Assert.That(s.GuidedBossDeckShuffles, Is.EqualTo(1));
            Assert.That(s.Battle.Hand.Count + s.Battle.DrawPile.Count + s.Battle.DiscardPile.Count, Is.EqualTo(deckSize));
            Assert.That(s.Battle.Hand.Count, Is.LessThanOrEqualTo(8));
        }

        [TestCase(90f)] [TestCase(270f)]
        public void DirectBoardingNeedsNoPriorMovementWhileRamLessonReacquiresSpentActionPoint(float playerHeading)
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseBoardContact);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1)); Assert.That(s.Battle.Energy, Is.EqualTo(3));
            Assert.That(s.Battle.PlayerMovingTowardEnemy, Is.False);
            Assert.That(s.Battle.PlayerPositionMeters.X, Is.EqualTo(-100f));
            Assert.That(s.Battle.PlayerPositionMeters.Y, Is.EqualTo(0f));
            Assert.That(s.Battle.EnemyPositionMeters.X, Is.EqualTo(0f));
            Assert.That(s.Battle.EnemyPositionMeters.Y, Is.EqualTo(0f));
            Assert.That(s.Battle.PlayerHeadingDegrees, Is.EqualTo(90f));
            Assert.That(s.Battle.EnemyHeadingDegrees, Is.EqualTo(90f));
            Assert.That(s.GuidedBossInstruction, Does.Contain("两舰朝北").And.Contain("同向或反向均可"));
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(s.Battle, new object[] { true, s.Battle.PlayerPositionMeters, playerHeading });
            Assert.That(s.CanBoard(out string reason), Is.True, reason);
            Advance(s);
            Assert.That(s.Battle.Boarding.Initialized, Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Reach(s, GuidedBossBattleStep.CourseRamMove); Advance(s);
            Assert.That(s.Battle.NavalActionPoints, Is.Zero); Assert.That(s.CanRam(out _), Is.False);
            Advance(s);
            Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
            Advance(s);
            Assert.That(s.Battle.BoardingConnection, Is.EqualTo(NavalBoardingConnection.RamBowToBroadside));
            Assert.That(s.GuidedBossCanContinue, Is.True);
        }

        [Test]
        public void FormalBoardingGateAndDelayedEffectsRunAtTheirRealTurnBoundaries()
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseBoardingAttack);
            Assert.That(s.Battle.Boarding.Initialized, Is.True);
            int wrong = s.Battle.Hand.FindIndex(i => s.GetBattleCard(i).Id == "card_crew_guard");
            string before = Snapshot(s);
            Assert.That(s.PlayCard(wrong, new CoreBoardingPlayOptions()), Is.False);
            Assert.That(Snapshot(s), Is.EqualTo(before));
            Reach(s, GuidedBossBattleStep.CourseBandage);
            int medicine = s.Cargo.Medicine, health = s.Battle.PlayerBoardingHealth;
            Advance(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(Math.Min(s.Battle.PlayerMaxBoardingHealth, health + 6)));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - 1));
            Reach(s, GuidedBossBattleStep.CourseAid);
            health = s.Battle.PlayerBoardingHealth; medicine = s.Cargo.Medicine;
            Advance(s);
            Assert.That(s.Battle.PlayerBoardingHealth, Is.EqualTo(health));
            Assert.That(s.Battle.Boarding.HealingTicks.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(5));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - 1));
            int enemy = s.Battle.EnemyBoardingHealth;
            Advance(s);
            Assert.That(s.Battle.EnemyBoardingHealth, Is.EqualTo(enemy));
            Assert.That(s.Battle.Boarding.DelayedDamage.Count, Is.EqualTo(1));
            Advance(s);
            Assert.That(s.Battle.Boarding.DelayedDamage, Is.Empty);
            Assert.That(s.Battle.Boarding.HealingTicks.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(4));
            Advance(s); Advance(s);
            Assert.That(s.Battle.Boarding.HealingTicks.Count, Is.EqualTo(1));
            Assert.That(s.Battle.Boarding.ContinuousRecovery, Is.EqualTo(3));
            Assert.That(s.Cargo.Medicine, Is.EqualTo(medicine - 1));
            Assert.That(s.GuidedBossCanContinue, Is.True);
        }

        [Test]
        public void WrongRamArrivalHeadingCanBeRecoveredThroughLegalMovesAndRealTurns()
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseRamMove);
            Append(s, new VoyagePoint(-100, 0));
            Assert.That(s.CommitNavalMovement(out string reason), Is.True, reason);
            Assert.That(s.ConfirmNavalArrivalHeading(90f, out reason), Is.True, reason);
            Advance(s);
            Assert.That(s.GuidedBossExpectedAction, Is.EqualTo(GuidedBossActionKind.FreePlay));
            Assert.That(s.CanRam(out _), Is.False);
            var targets = new[] { new VoyagePoint(-100, 100), new VoyagePoint(0, 100),
                new VoyagePoint(100, 100), new VoyagePoint(100, 0) };
            float[] headings = { 0f, 0f, 270f, 180f };
            for (int i = 0; i < targets.Length; i++)
            {
                Append(s, targets[i]);
                Assert.That(s.CommitNavalMovement(out reason), Is.True, reason);
                Assert.That(s.ConfirmNavalArrivalHeading(headings[i], out reason), Is.True, reason);
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Battle.NavalActionPoints, Is.EqualTo(1));
            }
            Assert.That(s.CanRam(out reason), Is.True, reason);
            Assert.That(s.TryRam(), Is.True);
            Assert.That(s.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseRamDone));
        }

        [Test]
        public void CourseCannonAndArmorQuotesUseFormalEquipmentRules()
        {
            var s = StartBoss(); Reach(s, GuidedBossBattleStep.CourseFire);
            CoreBattleCard card = s.GetBattleCard(s.Battle.Hand[ExpectedHand(s)]);
            int quoted = card.Damage + Math.Max(0, (s.EffectiveFirepower - 8) / 2) + s.PlayerLoadedShotDamageBonus;
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(quoted));
            Reach(s, GuidedBossBattleStep.CourseEnemyTurn);
            int rawAfterArmor = Math.Max(1, 12 - Math.Max(0, (s.EffectiveArmor - 4) / 2));
            int block = s.Battle.PlayerBlock, hull = s.Hull;
            Assert.That(s.GetEnemyIntent(), Does.Contain("炮击：伤害 " + rawAfterArmor + "（装甲后、格挡前）"));
            Advance(s);
            Assert.That(hull - s.Hull, Is.EqualTo(Math.Max(0, rawAfterArmor - block)));
        }

        [Test]
        public void ExerciseRamRetriesAndChapterTransitionsRestoreEntryMorale()
        {
            var s = StartBoss();
            int entryMorale = s.Morale;
            Reach(s, GuidedBossBattleStep.CourseRamMove);
            Assert.That(s.Morale, Is.EqualTo(entryMorale), "Earlier cannon practice must not carry morale into another chapter.");
            for (int retry = 0; retry < 2; retry++)
            {
                Reach(s, GuidedBossBattleStep.CourseRamDone);
                Assert.That(s.Morale, Is.EqualTo(entryMorale + 5), "The exercise still demonstrates the real ram morale gain.");
                Assert.That(s.TryRestartGuidedBossLesson(out string reason), Is.True, reason);
                Assert.That(s.Morale, Is.EqualTo(entryMorale), "Restarting must not accumulate ram morale.");
            }
            Reach(s, GuidedBossBattleStep.CourseRamDone);
            Assert.That(s.TryContinueGuidedBossLesson(out string nextReason), Is.True, nextReason);
            Assert.That(s.Morale, Is.EqualTo(entryMorale));
            Reach(s, GuidedBossBattleStep.CourseFinal);
            Assert.That(s.Morale, Is.EqualTo(entryMorale), "Final combat starts from the entry morale baseline.");
        }

        [Test]
        public void ExerciseEmergencyExitRestoresEntryMoraleAfterActualCannonGain()
        {
            var s = StartBoss();
            int entryMorale = s.Morale;
            Reach(s, GuidedBossBattleStep.CourseFire);
            Advance(s);
            Assert.That(s.Morale, Is.EqualTo(entryMorale + 2));
            s.Cargo.Water = s.Cargo.Food = 0;
            Assert.That(s.CanRequestEmergencyTow(), Is.True);
            Assert.That(s.RequestEmergencyTow(), Is.True);
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(s.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(s.Morale, Is.EqualTo(entryMorale));
            Assert.That(s.GuidedTutorialBossDefeated, Is.False);
            Assert.That(s.VoyageTreasureFragments, Is.Zero);
            s.ReturnToPortAfterSettlement();
            Assert.That(s.Morale, Is.EqualTo(entryMorale));
        }

        [TestCase(false)] [TestCase(true)]
        public void FinalBattlePreservesActualRamMoraleOnVictoryAndDefeat(bool victory)
        {
            var s = StartBoss();
            int entryMorale = s.Morale;
            Reach(s, GuidedBossBattleStep.CourseFinal);
            Assert.That(s.EndBattleTurn(), Is.True);
            MethodInfo setPose = typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setPose, Is.Not.Null);
            setPose.Invoke(s.Battle, new object[] { true, new VoyagePoint(-100f, 0f), 0f });
            setPose.Invoke(s.Battle, new object[] { false, new VoyagePoint(0f, 0f), 90f });
            s.Battle.PlayerBowQuality = 1f; // Keep this impact non-lethal so both boarding outcomes are exercised.
            Assert.That(s.CanRam(out string reason), Is.True, reason);
            Assert.That(s.TryRam(), Is.True);
            Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
            Assert.That(s.Morale, Is.EqualTo(entryMorale + 5));
            if (victory)
            {
                s.Battle.EnemyBoardingHealth = 1;
                int attack = s.Battle.Hand.FindIndex(index => s.GetBattleCard(index).Id == "card_crew_thrust");
                Assert.That(attack, Is.GreaterThanOrEqualTo(0));
                Assert.That(s.PlayCard(attack), Is.True);
                Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                Assert.That(s.GuidedTutorialBossDefeated, Is.True);
                Assert.That(s.VoyageTreasureFragments, Is.EqualTo(1));
            }
            else
            {
                s.Battle.PlayerBoardingHealth = 0;
                Assert.That(s.EndBattleTurn(), Is.True);
                Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
                Assert.That(s.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
                Assert.That(s.GuidedTutorialBossDefeated, Is.False);
                Assert.That(s.VoyageTreasureFragments, Is.Zero);
            }
            Assert.That(s.Morale, Is.EqualTo(entryMorale + 5), "Ordinary final combat morale must survive course resource restoration.");
        }

        [Test]
        public void ExerciseFailureRetriesWithoutRewardButFinalFailureRestoresSuppliesAndSettles()
        {
            var s = StartBoss(5);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 0);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(s.GuidedBossExerciseFailed, Is.True);
            Assert.That(s.TryContinueGuidedBossLesson(out _), Is.True);
            Assert.That(s.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.CourseResources));
            Assert.That(s.VoyageTreasureFragments, Is.Zero);
            Reach(s, GuidedBossBattleStep.CourseFinal);
            Assert.That(s.TryRestartGuidedBossLesson(out _), Is.False);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 0);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(s.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(s.Cargo.Powder, Is.EqualTo(5)); Assert.That(s.Cargo.Medicine, Is.EqualTo(5));
            Assert.That(s.Cargo.Wood, Is.EqualTo(5)); Assert.That(s.Cargo.Cannonballs, Is.EqualTo(5));
            Assert.That(s.VoyageTreasureFragments, Is.Zero);
        }
        [Test]
        public void CombinedSecondVoyage_CrewThenNonReturningAmbushThenBoss_ResumesEachStableBoundaryOnce()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(true, false, false, false, false, false);
            session.ConfigureGuidedTutorialVoyage(2); session.StartVoyage();
            Assert.That(session.UsesCombinedGuidedTutorialVoyage, Is.True);
            Assert.That(session.Map.Nodes.Select(n => n.Encounter.Id), Is.EqualTo(new[] { CoreGuidedTutorialContent.NavigatorEventId, CoreGuidedTutorialContent.BossEventId }));
            SailToEncounter(session, CoreGuidedTutorialContent.NavigatorEventId);
            session = Restore(session);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            session = Restore(session);
            SailToEncounter(session, CoreGuidedTutorialContent.AmbushEventId);
            Assert.That(session.ActiveEncounter.HasChartLocation, Is.False);
            Assert.That(session.ActiveEncounter.Choices[0].StartsBattle, Is.False);
            session = Restore(session);
            int hull = session.Hull;
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Hull, Is.EqualTo(hull)); Assert.That(session.Settlement, Is.Null);
            session = Restore(session);
            SailToEncounter(session, CoreGuidedTutorialContent.BossEventId);
            session = Restore(session);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.CanCaptureVoyageCheckpoint, Is.False);
            PlayToCompletion(session);
            session = Restore(session);
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(session.GuidedTutorialCompletedEncounterIds, Is.EqualTo(new[] { CoreGuidedTutorialContent.NavigatorEventId, CoreGuidedTutorialContent.AmbushEventId, CoreGuidedTutorialContent.BossEventId }));
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(80), "The combined voyage earns the existing boss-lesson experience.");
        }

        [Test]
        public void LegacyNavigatorCheckpointKeepsOneEventRoute_UnknownLayoutIsRejectedAtomically()
        {
            var old = new CoreVoyageSession();
            old.ConfigureCrewCapabilities(true, false, false, false, false, false);
            old.ConfigureGuidedTutorialVoyage(2);
            typeof(CoreVoyageSession).GetField("guidedTutorialLayoutVersion", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(old, 0);
            old.StartVoyage();
            SailToEncounter(old, CoreGuidedTutorialContent.NavigatorEventId);
            CoreVoyageCheckpoint checkpoint = old.CaptureVoyageCheckpoint();
            Assert.That(checkpoint.guidedTutorialLayoutVersion, Is.Zero);
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            Assert.That(restored.UsesCombinedGuidedTutorialVoyage, Is.False);
            Assert.That(restored.Map.Nodes, Has.Count.EqualTo(1));
            Assert.That(restored.ResolveChoice(0), Is.True);
            Assert.That(restored.Settlement.Outcome, Is.EqualTo(VoyageOutcome.ArrivedAtPort));
            Assert.That(restored.Settlement.BankedPirateExperience, Is.EqualTo(20));
            checkpoint.guidedTutorialLayoutVersion = 2;
            var target = new CoreVoyageSession();
            Assert.That(target.TryRestoreVoyageCheckpoint(checkpoint, out _), Is.False);
            Assert.That(target.Phase, Is.EqualTo(CoreDemoPhase.Port)); Assert.That(target.Map, Is.Null);
        }

        private static CoreVoyageSession Restore(CoreVoyageSession source)
        {
            var restored = new CoreVoyageSession();
            Assert.That(restored.TryRestoreVoyageCheckpoint(source.CaptureVoyageCheckpoint(), out string reason), Is.True, reason);
            return restored;
        }

        private static string Snapshot(CoreVoyageSession session)
        {
            CoreBattleState b = session.Battle;
            return session.GuidedBossStep + ":" + session.Hull + ":" + session.Cargo.Powder + ":" + session.Cargo.Cannonballs + ":" + session.Cargo.Medicine +
                ":" + b.Energy + ":" + b.NavalActionPoints + ":" + b.NavalAwaitingHeadingSelection + ":" + b.TacticalRevision +
                ":" + b.CardsPlayedThisTurn + ":" + b.Turn + ":" + b.PhaseTurn + ":" + b.DistanceMeters + ":" + b.Alignment +
                ":" + b.PlayerBlock + ":" + b.EnemyBlock + ":" + b.PlayerBoardingHealth + ":" + b.EnemyBoardingHealth + ":" + b.EnemyHull +
                ":" + b.PlayerLoadedShot + ":" + b.LastAction + ":" + string.Join(",", b.Hand) + ":" + string.Join(",", b.DrawPile) + ":" + string.Join(",", b.DiscardPile);
        }
    }
}
