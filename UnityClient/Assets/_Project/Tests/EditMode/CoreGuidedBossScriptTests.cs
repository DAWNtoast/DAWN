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
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Medicine = supplies;
            session.ConfigureGuidedTutorialVoyage(CoreGuidedTutorialContent.BossVoyageLeg);
            session.StartVoyage();
            SailToEncounter(session, CoreGuidedTutorialContent.BossEventId);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            Assert.That(session.ResolveChoice(0), Is.True);
            return session;
        }

        internal static void SailToEncounter(CoreVoyageSession session, string expected)
        {
            session.SetHeading(SailingDirection.East);
            session.SetDistance(120);
            for (int i = 0; i < 5 && session.Phase == CoreDemoPhase.Sailing; i++) Assert.That(session.Sail(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(session.ActiveEncounter.Id, Is.EqualTo(expected));
        }

        internal static int PlayToCompletion(CoreVoyageSession session)
        {
            int actions = 0;
            while (session.Phase == CoreDemoPhase.Battle && actions++ < 30) Advance(session);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            return actions;
        }

        internal static void Advance(CoreVoyageSession session)
        {
            switch (session.GuidedBossExpectedAction)
            {
                case GuidedBossActionKind.Card:
                    Assert.That(session.PlayCard(ExpectedHand(session)), Is.True, session.GuidedBossStep.ToString()); break;
                case GuidedBossActionKind.BlockedCard:
                    Assert.That(session.TryObserveGuidedBossBlockedCard(ExpectedHand(session), out string reason), Is.True, reason); break;
                case GuidedBossActionKind.EndTurn: Assert.That(session.EndBattleTurn(), Is.True); break;
                case GuidedBossActionKind.Ram: Assert.That(session.TryRam(), Is.True); break;
                default: Assert.Fail("No authored action at " + session.GuidedBossStep); break;
            }
        }

        private static int ExpectedHand(CoreVoyageSession session) =>
            session.Battle.Hand.FindIndex(i => session.GetBattleCard(i).Id == session.GuidedBossExpectedCardId);

        [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 3)] [TestCase(5, 3)]
        public void AuthoredBattle_UnscaledSecondBoardingAttackWinsWithExactCostsAndPreservesPlayerLoadout(int supplies, int upgrade)
        {
            CoreVoyageSession session = StartBoss(supplies, upgrade);
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull), "The lesson repairs prior voyage attrition at entry.");
            int hull = session.Hull;
            CoreBattleState battle = session.Battle;
            int boardingHealthAfterRam = InitialBoardingHealth(battle.PlayerMaxBoardingHealth,
                battle.NavalStartPlayerHull, hull - 10, session.MaxHull);
            // Reduced naval guard lets one hull damage through, then ram costs nine.
            // Two unscaled eight-damage thrusts defeat the ten-health boarding enemy.
            // The intervening guard and medicine lesson now executes with its exact costs.
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(battle.DistanceMeters, Is.EqualTo(400));
            Assert.That(battle.PlayerOrdinaryCrew, Is.EqualTo(10));
            Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(60));
            Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(12));
            Assert.That(session.Cargo.Powder, Is.EqualTo(2));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(1));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.GetBattleCard(ExpectedHandLibrary(session)).UpgradeLevel, Is.Zero);
            for (int i = 1; i <= (int)GuidedBossBattleStep.BoardingAttack2; i++)
            {
                Assert.That(session.GuidedBossStep, Is.EqualTo((GuidedBossBattleStep)i));
                switch (session.GuidedBossStep)
                {
                    case GuidedBossBattleStep.NavalEndTurn1:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(530));
                        Assert.That(session.GetEnemyIntent(), Does.Contain("靠近 100 米").And.Contain("格挡设为 3")); break;
                    case GuidedBossBattleStep.NavalRangeAttempt:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(430)); Assert.That(battle.EnemyBlock, Is.EqualTo(3)); break;
                    case GuidedBossBattleStep.NavalFire:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(280));
                        Assert.That(session.Cargo.Powder, Is.EqualTo(1), "Loading spent one charge; firing still needs its own charge."); break;
                    case GuidedBossBattleStep.NavalEndTurn2:
                        Assert.That(battle.EnemyHull, Is.EqualTo(40)); Assert.That(battle.PlayerBlock, Is.EqualTo(2));
                        Assert.That(session.GetEnemyIntent(), Does.Contain("靠近 180 米").And.Contain("伤害 3")); break;
                    case GuidedBossBattleStep.NavalLoadAttempt:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(100)); Assert.That(battle.LastEnemyGuardAbsorbed, Is.EqualTo(2));
                        Assert.That(session.Cargo.Powder, Is.Zero, "Loading and firing consumed the two training charges.");
                        Assert.That(session.Hull, Is.EqualTo(hull - 1)); break;
                    case GuidedBossBattleStep.NavalApproach3:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(40)); Assert.That(battle.Alignment, Is.EqualTo(NavalBattleAlignment.EnemyBroadside)); break;
                    case GuidedBossBattleStep.NavalRam:
                        Assert.That(battle.DistanceMeters, Is.EqualTo(30), "The final approach preserves room for the ram.");
                        Assert.That(battle.PlayerMovingTowardEnemy, Is.True);
                        Assert.That(battle.Alignment, Is.EqualTo(NavalBattleAlignment.EnemyBroadside));
                        Assert.That(battle.Energy, Is.EqualTo(1));
                        Assert.That(session.CanRam(out string ramReason), Is.True, ramReason);
                        Assert.That(session.GetEnemyIntent(), Does.Not.Contain("靠近"), "The enemy also holds at the minimum gap.");
                        break;
                    case GuidedBossBattleStep.BoardingAttack1:
                        Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Boarding)); Assert.That(battle.PhaseTurn, Is.EqualTo(1));
                        Assert.That(session.Hull, Is.EqualTo(hull - 10)); Assert.That(battle.EnemyHull, Is.EqualTo(27));
                        Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(boardingHealthAfterRam));
                        Assert.That(session.GetBattleCardEffects(ExpectedHandLibrary(session), NavalBattleStage.Boarding).Damage, Is.EqualTo(8));
                        Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(10)); break;
                    case GuidedBossBattleStep.BoardingGuard1:
                        Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(2)); break;
                    case GuidedBossBattleStep.BoardingEndTurn1:
                        Assert.That(battle.PlayerBlock, Is.EqualTo(4)); break;
                    case GuidedBossBattleStep.BoardingHeal:
                        Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(boardingHealthAfterRam - 3));
                        Assert.That(battle.LastEnemyGuardAbsorbed, Is.EqualTo(4));
                        Assert.That(session.GetBattleCardEffects(ExpectedHandLibrary(session), NavalBattleStage.Boarding).Repair, Is.EqualTo(6));
                        Assert.That(session.Cargo.Medicine, Is.EqualTo(1)); break;
                    case GuidedBossBattleStep.BoardingAttack2:
                        Assert.That(battle.PlayerBoardingHealth, Is.EqualTo(boardingHealthAfterRam + 3));
                        Assert.That(session.Cargo.Medicine, Is.Zero);
                        Assert.That(session.GetBattleCardEffects(ExpectedHandLibrary(session), NavalBattleStage.Boarding).Damage, Is.EqualTo(8));
                        Assert.That(battle.EnemyBoardingHealth, Is.EqualTo(2)); break;
                }
                GuidedBossBattleStep stepBefore = session.GuidedBossStep;
                int powderBefore = session.Cargo.Powder, ballsBefore = session.Cargo.Cannonballs, medicineBefore = session.Cargo.Medicine;
                Advance(session);
                if (session.Phase == CoreDemoPhase.Battle)
                {
                    Assert.That(session.Cargo.Powder, Is.EqualTo(powderBefore -
                        (stepBefore == GuidedBossBattleStep.NavalLoad || stepBefore == GuidedBossBattleStep.NavalFire ? 1 : 0)), stepBefore.ToString());
                    Assert.That(session.Cargo.Cannonballs, Is.EqualTo(ballsBefore -
                        (stepBefore == GuidedBossBattleStep.NavalFire ? 1 : 0)), stepBefore.ToString());
                    Assert.That(session.Cargo.Medicine, Is.EqualTo(medicineBefore -
                        (stepBefore == GuidedBossBattleStep.BoardingHeal ? 1 : 0)), stepBefore.ToString());
                }
            }
            Assert.That(session.GuidedBossStep, Is.EqualTo(GuidedBossBattleStep.BoardingGuard2), "The second thrust is lethal after the guard and healing lesson.");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
            Assert.That(battle.EnemyBoardingHealth, Is.Zero, "The final authored thrust clamps lethal damage at zero.");
            Assert.That(session.VoyageTreasureFragments, Is.EqualTo(1));
            Assert.That(session.Cargo.Powder, Is.EqualTo(Math.Max(0, supplies - 2)));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(Math.Max(0, supplies - 1)));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(Math.Max(0, supplies - 1)), "The healing lesson spends exactly one medicine before the second thrust wins.");
            int cannon = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_chain_shot");
            Assert.That(session.GetBattleCard(cannon).UpgradeLevel, Is.EqualTo(upgrade), "The tutorial never overwrites owned upgrades.");
            Assert.That(session.SafeReturn(), Is.True);
            Assert.That(session.Settlement.BankedFragments, Is.EqualTo(1));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureFragments, Is.EqualTo(1));
            session.ReturnToPortAfterSettlement();
            Assert.That(session.TreasureFragments, Is.EqualTo(1));
        }

        private static int ExpectedHandLibrary(CoreVoyageSession session) => session.Battle.Hand[ExpectedHand(session)];

        private static int InitialBoardingHealth(int maxBoardingHealth, int startHull, int currentHull, int maxHull)
        {
            return (int)Math.Ceiling(maxBoardingHealth * (1d - Math.Max(0, startHull - currentHull) / (2d * maxHull)));
        }

        [Test]
        public void WrongActionsAndPureQueriesNeverAdvance_OnlyTheTwoObservedFailuresDo()
        {
            CoreVoyageSession session = StartBoss();
            for (int action = 0; action < 30 && session.Phase == CoreDemoPhase.Battle; action++)
            {
                GuidedBossBattleStep step = session.GuidedBossStep;
                string unchanged = Snapshot(session);
                for (int hand = 0; hand < session.Battle.Hand.Count; hand++)
                {
                    session.CanPlayCard(hand); session.CardUnavailableReason(hand); session.GetEnemyIntent();
                    if (session.GetBattleCard(session.Battle.Hand[hand]).Id != session.GuidedBossExpectedCardId)
                        Assert.That(session.PlayCard(hand), Is.False);
                }
                Assert.That(session.PlayCard(-1), Is.False);
                Assert.That(session.TryObserveGuidedBossBlockedCard(-1, out _), Is.False);
                Assert.That(session.TryBoard(), Is.False);
                Assert.That(session.RetreatFromBattle(), Is.False);
                if (session.GuidedBossExpectedAction != GuidedBossActionKind.EndTurn) Assert.That(session.EndBattleTurn(), Is.False);
                if (session.GuidedBossExpectedAction != GuidedBossActionKind.Ram) Assert.That(session.TryRam(), Is.False);
                Assert.That(Snapshot(session), Is.EqualTo(unchanged));
                if (session.GuidedBossExpectedAction == GuidedBossActionKind.BlockedCard)
                {
                    int hand = ExpectedHand(session);
                    Assert.That(session.PlayCard(hand), Is.False);
                    Assert.That(Snapshot(session), Is.EqualTo(unchanged));
                    Assert.That(session.TryObserveGuidedBossBlockedCard(hand, out string reason), Is.True);
                    Assert.That(reason, Is.EqualTo(step == GuidedBossBattleStep.NavalRangeAttempt ? "炮击需要距离不超过 350 米" : "火药不足"));
                    string after = Snapshot(session);
                    Assert.That(session.TryObserveGuidedBossBlockedCard(hand, out _), Is.False);
                    Assert.That(Snapshot(session), Is.EqualTo(after));
                }
                else Advance(session);
            }
            Assert.That(session.GuidedTutorialBossDefeated, Is.True);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(5)]
        public void FailureRestoresOnlyOwnedSupplyReserveWithoutLeakingTemporaryTrainingSupplies(int supplies)
        {
            CoreVoyageSession session = StartBoss(supplies);
            while (session.GuidedBossStep != GuidedBossBattleStep.NavalRam) Advance(session);
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 1);
            Assert.That(session.TryRam(), Is.True);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Hull, Is.Zero);
            Assert.That(session.Cargo.Powder, Is.EqualTo(Math.Max(0, supplies - 2)));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(Math.Max(0, supplies - 1)));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(supplies), "Unspent owned medicine is restored without retaining borrowed training medicine.");
            Assert.That(session.VoyageTreasureFragments, Is.Zero);
            Assert.That(session.Settlement.BankedPirateExperience, Is.EqualTo(80), "The legacy leg-3 outcome keeps its existing experience rule.");
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
                ":" + b.Energy + ":" + b.CardsPlayedThisTurn + ":" + b.Turn + ":" + b.PhaseTurn + ":" + b.DistanceMeters + ":" + b.Alignment +
                ":" + b.PlayerBlock + ":" + b.EnemyBlock + ":" + b.PlayerBoardingHealth + ":" + b.EnemyBoardingHealth + ":" + b.EnemyHull +
                ":" + b.PlayerLoadedShot + ":" + b.LastAction + ":" + string.Join(",", b.Hand) + ":" + string.Join(",", b.DrawPile) + ":" + string.Join(",", b.DiscardPile);
        }
    }
}
