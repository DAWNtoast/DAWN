using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCurseNavalRevision20261024Tests
    {
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ForbiddenFieldAddsRemainingMarksToEitherSideAtItsOwnEnd(bool owner, bool target)
        {
            CoreVoyageSession s = Start();
            AddCell(s, owner, target ? 0 : 2, 0, 5);
            int enemy = s.Battle.EnemyHull, player = s.Hull;
            Assert.That(s.Battle.Schools.CurseMarks + s.Battle.PlayerCurseMarks, Is.Zero);
            Assert.That(s.NavalFieldsEndTurn(target), Is.False);
            Assert.That(target ? s.Battle.PlayerCurseMarks : s.Battle.Schools.CurseMarks, Is.EqualTo(5));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy)); Assert.That(s.Hull, Is.EqualTo(player));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(target ? 0 : 2, 0)), Is.EqualTo(owner == target ? 5 : 4));
            s.NavalFieldsEndTurn(target);
            Assert.That(target ? s.Battle.PlayerCurseMarks : s.Battle.Schools.CurseMarks, Is.EqualTo(5), "Repeated end must not add twice.");
        }

        [Test]
        public void FinalForbiddenFieldTickAddsOneBeforeExpiry()
        {
            CoreVoyageSession s = Start(); AddCell(s, false, 0, 0, 1);
            Assert.That(s.NavalFieldsEndTurn(true), Is.False);
            Assert.That(s.Battle.PlayerCurseMarks, Is.EqualTo(1));
            Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void ForbiddenFieldCanExecuteWithoutAnyPriorMarks()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 5; s.Battle.EnemyBlock = 999;
            AddCell(s, false, 2, 0, 5);
            Assert.That(s.NavalFieldsEndTurn(false), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
        }

        [Test]
        public void MarksBelowCurrentHullSurviveAndAdditionalCardMarksExecuteImmediately()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 10; s.Battle.EnemyBlock = 999;
            s.ApplyNavalEnemyCurseMarks(9);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(10));
            Play(s, "card_school_curse_b03");
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MarksBelowCurrentHullSurviveAndEqualityExecutesEitherSide(bool player)
        {
            CoreVoyageSession s = Start(); CoreBattleState battle = s.Battle;
            if (player) { SetHull(s, 10); battle.PlayerBlock = 999; s.ApplyNavalPlayerCurseMarks(9); }
            else { battle.EnemyHull = 10; battle.EnemyBlock = 999; s.ApplyNavalEnemyCurseMarks(9); }
            Assert.That(player ? s.Hull : battle.EnemyHull, Is.EqualTo(10), "Nine marks are below ten current hull and do not execute.");
            Assert.That(player ? battle.PlayerNavalExecutedByCurse : battle.EnemyNavalExecutedByCurse, Is.False);
            // Use the real field-turn context: it adds the final mark and resolves the battle outcome.
            AddCell(s, !player, player ? 0 : 2, 0, 1);
            Assert.That(s.NavalFieldsEndTurn(player), Is.True);
            Assert.That(player ? battle.PlayerCurseMarks : battle.Schools.CurseMarks, Is.EqualTo(10));
            Assert.That(player ? battle.PlayerBlock : battle.EnemyBlock, Is.EqualTo(999), "Execution bypasses guard.");
            Assert.That(player ? s.Hull : battle.EnemyHull, Is.Zero, "The tenth mark reaches exactly 100% and executes.");
            Assert.That(player ? battle.PlayerNavalExecutedByCurse : battle.EnemyNavalExecutedByCurse, Is.True);
            Assert.That(s.TrainingOutcome, Is.EqualTo(player ? CoreTrainingBattleOutcome.Defeat : CoreTrainingBattleOutcome.Victory));
        }

        [Test]
        public void MarksNeverTickDamageOrDecay()
        {
            CoreVoyageSession s = Start(); s.ApplyNavalEnemyCurseMarks(5);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.SettleNavalCurse(true), Is.False);
            Assert.That(s.SettleNavalCurse(false), Is.False);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
        }

        [Test]
        public void CalamityDetonationIsImmediatePiercingDamageWithoutConsumingGuardOrMarks()
        {
            CoreVoyageSession s = Start(); s.ApplyNavalEnemyCurseMarks(5); s.Battle.EnemyBlock = 3;
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_curse_b05");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 5)); Assert.That(s.Battle.EnemyBlock, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
            Assert.That(s.Battle.Schools.CurseTickBonus + s.Battle.Schools.CurseNextTickBonus, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(999)]
        public void CalamityDetonationSpendsAllAttachedFireAndBypassesAnyGuard(int block)
        {
            CoreVoyageSession s = Start(); s.ApplyNavalEnemyCurseMarks(5); s.Battle.EnemyBlock = block;
            s.Battle.PlayerFireRounds = 7;
            Play(s, "card_school_curse_b05");
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(7));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(995));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(block));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(5));
        }

        [Test]
        public void CalamityExecutionReportsOnlyFiveRealDamageAndStopsAttachedFire()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 10; s.ApplyNavalEnemyCurseMarks(5);
            s.Battle.PlayerFireRounds = 7;
            Play(s, "card_school_curse_b05");
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.LastAction, Does.Contain("已造成 5 点实际伤害").And.Not.Contain("10 点"));
            Assert.That(s.Battle.PlayerFireRounds, Is.EqualTo(7));
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero);
        }

        [Test]
        public void OrdinaryDamageCanCrossExecutionThresholdAndCancelLaterHit()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 20; s.Battle.EnemyBlock = 25;
            s.ApplyNavalEnemyCurseMarks(10); s.Battle.Schools.Mana = 10;
            Play(s, "card_school_arcane_n03");
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(25), "The first piercing hit executes; the normal second hit must not consume guard.");
        }

        [Test]
        public void BurningDamageExecutesBeforeForbiddenFieldCanAddMoreMarks()
        {
            // The enemy must not gain guard before its three-point end-of-turn burn.
            CoreVoyageSession s = Start(0); s.Battle.EnemyHull = 10; s.ApplyNavalEnemyCurseMarks(7);
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 3;
            AddCell(s, true, 2, 0, 5);
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(7));
        }

        [Test]
        public void PayingHealthCanExecuteBeforeThePaidCardGrantsAnything()
        {
            CoreVoyageSession s = Start(); SetHull(s, 10); s.ApplyNavalPlayerCurseMarks(7);
            int enemy = s.Battle.EnemyHull;
            Play(s, "card_school_blood_n02");
            Assert.That(s.Hull, Is.Zero); Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
        }

        [Test]
        public void CannonSealRequiresMarksWithoutSpendingThemAndPersistsUntilOneCannonShot()
        {
            CoreVoyageSession s = Start(); Put(s, "card_school_curse_n02");
            Assert.That(s.PlayCard(0), Is.False);
            s.ApplyNavalEnemyCurseMarks(1); s.Battle.EnemyLoadedShot = 12;
            Assert.That(s.PlayCard(0), Is.True);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1)); Assert.That(s.Battle.EnemyLoadedShot, Is.EqualTo(12));
            s.NavalExpansionEndEnemyTurn();
            int reduced = InvokeInt(s, "EnemyCannonDamage", 20, false, null);
            int committed = InvokeInt(s, "EnemyCannonDamage", 20, true, null);
            int next = InvokeInt(s, "EnemyCannonDamage", 20, false, null);
            Assert.That(committed, Is.EqualTo(reduced)); Assert.That(next - reduced, Is.EqualTo(6));
        }

        [TestCase("card_special_instrument_eye_of_depths")]
        [TestCase("card_special_bow_gravecurrent_spur")]
        [TestCase("card_special_bow_crownless_colossus")]
        public void ForbiddenExpansionUsesEveryFriendlySourceAndMaximumInheritedDuration(string id)
        {
            CoreVoyageSession s = Start();
            AddCell(s, true, 0, 0, 2); AddCell(s, true, 2, 0, 5); AddCell(s, true, 20, 0, 3); AddCell(s, false, -1, 0, 4);
            Play(s, id);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(1, 0)), Is.EqualTo(5));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(21, 0)), Is.EqualTo(3));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(2));
            Assert.That(s.GetNavalFieldAt(-1, 0).PlayerOwned, Is.False); Assert.That(s.GetNavalFieldAt(4, 0), Is.Null);
        }

        [TestCase("card_special_sail_crown_of_gales", 5)]
        [TestCase("card_special_sail_eclipse_shroud", 2)]
        public void ForbiddenExtensionAffectsAllFriendlyFieldsOnly(string id, int turns)
        {
            CoreVoyageSession s = Start(); AddCell(s, true, 20, 0, 3); AddCell(s, false, 0, 0, 4);
            Play(s, id);
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(20, 0)), Is.EqualTo(3 + turns));
            Assert.That(s.GetNavalFieldRemainingTurns(s.GetNavalFieldAt(0, 0)), Is.EqualTo(4));
        }

        [Test]
        public void FieldShelterBlocksForbiddenMarksUntilNextPlayerStart()
        {
            CoreVoyageSession s = Start(); AddCell(s, false, 0, 0, 5);
            Play(s, "card_special_plating_eclipse_mail");
            s.NavalFieldsEndTurn(true); Assert.That(s.Battle.PlayerCurseMarks, Is.Zero);
            s.NavalFieldsBeginTurn(true); s.NavalFieldsEndTurn(true);
            Assert.That(s.Battle.PlayerCurseMarks, Is.EqualTo(4));
        }

        [Test]
        public void EnemyPreviewShowsMarkGainAndImmediateExecutionWithoutMutatingState()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 5; AddCell(s, true, 2, 0, 5);
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(0);
            Assert.That(preview.Summary, Does.Contain("获得 5 层咒印").And.Contain("消灭"));
            Assert.That(preview.Sinks, Is.False, "End-of-turn execution must not cancel an earlier enemy attack.");
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero); Assert.That(s.Battle.EnemyHull, Is.EqualTo(5));
        }

        [Test]
        public void PlayerThunderDamagePreviewAndExecutionBothCrossTheCurseThreshold()
        {
            CoreVoyageSession s = Start(); Pose(s, false, 600, 0); SetHull(s, 10); s.ApplyNavalPlayerCurseMarks(9);
            s.Battle.Schools.ThunderChain = 45;
            s.Battle.PlayerBlock = 997;
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 1, 0, false, 0, 0), Is.True);
            s.Battle.NavalActionPoints = 3;
            Assert.That(s.TryAppendNavalMovementSegment(new VoyagePoint(100, 0), out string reason), Is.True, reason);
            CoreNavalMovementFieldPreview preview = s.NavalMovementFieldPreview;
            Assert.That(preview.Sinks, Is.True); Assert.That(preview.Summary, Does.Contain("咒印立即消灭"));
            Assert.That(s.Hull, Is.EqualTo(10));
            Assert.That(s.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(s.Hull, Is.Zero); Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
        }

        [Test]
        public void AnAlreadyLethalMarkStateCannotBeRescuedByPlayingAConsumeCard()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 10; s.Battle.Schools.CurseMarks = 10;
            Put(s, "card_school_curse_n03");
            Assert.That(s.PlayCard(0), Is.False);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(10));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScorchExecutionStopsTailRewardsAndOnlyReportsActualDamage(bool expansion)
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 10; s.ApplyNavalEnemyCurseMarks(5);
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 1;
            s.Battle.Schools.ScorchDamage = 3; s.Battle.Schools.ScorchCharges = 1;
            string id = expansion ? "card_school_fire_n02" : "card_energy_order";
            WithMixedCard(s, id, 2, "fire", () =>
            {
                PrepareTailRewards(s, id);
                int energy = s.Battle.Energy;
                Assert.That(s.PlayCard(0), Is.True);
                Assert.That(s.Battle.EnemyHull, Is.Zero); Assert.That(s.Battle.Hand, Is.Empty);
                Assert.That(s.Battle.Energy, Is.EqualTo(energy), "The later energy recovery must not run.");
                Assert.That(s.Battle.LastAction, Does.Contain("已造成 5 点实际伤害").And.Contain("咒印"));
                Assert.That(s.Battle.LastAction, Does.Not.Contain("10 点"));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DoctrineExecutionStopsTailDrawAndEnergyRecovery(bool expansion)
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 6; s.ApplyNavalEnemyCurseMarks(5);
            s.Battle.SpecialCards.Activate("curse");
            string id = expansion ? "card_school_curse_n02" : "card_energy_order";
            WithMixedCard(s, id, 0, "curse", () =>
            {
                PrepareTailRewards(s, id);
                int energy = s.Battle.Energy;
                if (!expansion) Assert.That(s.GetBattleCardEnergyRecovery(s.Battle.Hand[0]), Is.EqualTo(1));
                Assert.That(s.PlayCard(0), Is.True);
                Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(6)); Assert.That(s.Battle.EnemyHull, Is.Zero);
                Assert.That(s.Battle.Hand, Is.Empty); Assert.That(s.Battle.Energy, Is.EqualTo(energy));
                Assert.That(s.Battle.LastAction, Does.Contain("咒印").And.Not.Contain("伤害"));
            });
        }

        [TestCase("card_chain_shot")]
        [TestCase("card_school_tide_n02")]
        public void OrdinaryCannonKillKeepsMoraleAndDamageFeedback(string id)
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyHull = 1;
            typeof(CoreVoyageSession).GetProperty("Morale").SetValue(s, 10);
            Play(s, id);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Morale, Is.EqualTo(12)); Assert.That(s.Battle.LastAction, Does.Contain("伤害").And.Not.Contain("咒印"));
            Assert.That(s.Battle.EnemyNavalExecutedByCurse, Is.False);
        }

        [TestCase(0, true)]
        [TestCase(4, false)]
        public void EnemyEndFieldPreviewConsumesGuardAndBurnBeforeAddingMarks(int guard, bool executes)
        {
            CoreVoyageSession s = Start(guard); s.Battle.EnemyHull = 12; s.ApplyNavalEnemyCurseMarks(6);
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 4; AddCell(s, true, 2, 0, 2);
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(0);
            Assert.That(preview.Summary, Does.Contain("获得 2 层咒印"));
            Assert.That(preview.Summary.Contains("触发咒印消灭"), Is.EqualTo(executes));
            Assert.That(preview.Sinks, Is.False, "End-of-turn execution must allow the earlier enemy action.");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(12)); Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(6));
            int player = s.Hull; s.EndBattleTurn();
            Assert.That(s.Hull, Is.LessThan(player), "The enemy still attacks before its end-of-turn execution.");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(executes ? 0 : 12));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(8));
        }

        [Test]
        public void EnemyEndBurnExecutionPreventsThePredictedAndActualFieldMarks()
        {
            CoreVoyageSession s = Start(0); s.Battle.EnemyHull = 10; s.ApplyNavalEnemyCurseMarks(9);
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 1; AddCell(s, true, 2, 0, 2);
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(0);
            Assert.That(preview.Summary, Does.Contain("燃烧结算时被消灭").And.Not.Contain("获得 2 层咒印"));
            Assert.That(preview.Sinks, Is.False);
            s.EndBattleTurn(); Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(9));
        }

        [TestCase(CoreNavalFieldKind.Curse, true)]
        [TestCase(CoreNavalFieldKind.Fire, true)]
        [TestCase(CoreNavalFieldKind.Curse, false)]
        [TestCase(CoreNavalFieldKind.Fire, false)]
        public void OnlyAValidSurvivingRamOmitsNavalTurnEndFields(CoreNavalFieldKind kind, bool adjacent)
        {
            CoreVoyageSession s = Start();
            s.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced,
                1000, 0, 1, 0, 0, 20, 1, 1, ramChance: 1, healthMultiplier: 1);
            Pose(s, false, adjacent ? 100 : 200, 0);
            AddCell(s, true, adjacent ? 1 : 2, 0, 5);
            s.Battle.NavalFields.Cells[0].Kind = kind;
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 2;
            Assert.That(s.EnemyWillRam, Is.EqualTo(adjacent));
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(0);
            Assert.That(preview.EntersBoarding, Is.EqualTo(adjacent));
            if (adjacent)
            {
                Assert.That(preview.Summary, Does.Not.Contain("回合末"));
                Assert.That(s.GetEnemyIntent(), Does.Not.Contain("回合结束燃烧"));
                s.EndBattleTurn();
                Assert.That(s.Battle.Stage, Is.EqualTo(NavalBattleStage.Boarding));
                Assert.That(s.Battle.NavalFields.Cells, Is.Empty);
                Assert.That(s.Battle.EnemyBurnTurns + s.Battle.Schools.CurseMarks, Is.Zero);
            }
            else Assert.That(preview.Summary, Does.Contain("回合末获得 5 层"));
        }

        // Exercise both interpreters with a mixed definition so there is a real
        // energy/draw reward after the killing hook. Always restore the shared catalog.
        private static void WithMixedCard(CoreVoyageSession session, string id, int damage, string school, Action run)
        {
            var library = (ReadOnlyCollection<CoreBattleCard>)CoreVoyageSession.CardLibrary;
            var items = (IList<CoreBattleCard>)typeof(ReadOnlyCollection<CoreBattleCard>)
                .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(library);
            int index = Enumerable.Range(0, items.Count).Single(i => items[i].Id == id);
            CoreBattleCard original = items[index];
            items[index] = new CoreBattleCard(id, "尾部结算回归", "策略", 0, damage, 0, 0, 1, 0,
                "造成伤害；获得能量；抽牌。", NavalBattleStages.Naval, NavalBattleCardAction.Energize, schoolId: school);
            try
            {
                // The temporary school identity must be explicitly owned like any real school card.
                CoreCardPackProgress owned = session.CaptureCardPackProgress();
                if (!owned.cards.Any(card => card.id == id))
                    owned.cards = owned.cards.Concat(new[] { new CoreCardCopySave { id = id, count = 2 } }).ToArray();
                Assert.That(session.RestoreCardPackProgress(owned), Is.True);
                run();
            }
            finally { items[index] = original; }
        }

        private static void PrepareTailRewards(CoreVoyageSession s, string id)
        {
            Put(s, id); s.Battle.DrawPile.Clear(); s.Battle.DiscardPile.Clear();
            s.Battle.DrawPile.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .Single(i => CoreVoyageSession.CardLibrary[i].Id == "card_tactical_draw"));
        }

        private static CoreVoyageSession Start(int guard = 2)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureSpecialCardSources(null, CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Component).Select(c => c.SourceId).ToArray());
            s.SetSeed("CURSE-NAVAL-REVISION"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(new CoreTrainingTarget("curse_test_target", "咒术木桩", 1000, 80, 10, 8, guard, 0)), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 200, 0);
            s.Battle.Energy = 100; s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 1000; s.Cargo.Powder = s.Cargo.Cannonballs = 100;
            return s;
        }
        private static void AddCell(CoreVoyageSession s, bool owner, int x, int y, int remaining)
        {
            CoreNavalFieldState state = s.Battle.NavalFields;
            state.Cells.Add(new CoreNavalFieldCell { Kind = CoreNavalFieldKind.Curse, PlayerOwned = owner, X = x, Y = y,
                InstanceId = state.NextInstanceId++, ExpiresAtOwnerTurn = (owner ? state.CompletedEnemyTurns : state.CompletedPlayerTurns) + remaining });
        }
        private static void Put(CoreVoyageSession s, string id)
        { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id)); }
        private static void Play(CoreVoyageSession s, string id) { Put(s, id); Assert.That(s.PlayCard(0), Is.True, s.CardUnavailableReason(0)); }
        private static int InvokeInt(CoreVoyageSession s, string name, params object[] args) =>
            (int)typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static void SetHull(CoreVoyageSession s, int value) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, value);
        private static void Pose(CoreVoyageSession s, bool player, float x, float y) => typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(s.Battle, new object[] { player, new VoyagePoint(x, y), player ? 0f : 180f });
    }
}
