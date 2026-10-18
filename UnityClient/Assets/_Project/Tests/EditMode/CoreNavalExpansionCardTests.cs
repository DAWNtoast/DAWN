using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalExpansionCardTests
    {
        public static IEnumerable<string> NewCardIds => CoreNavalSchoolExpansionCatalog.All.Select(c => c.Id);

        [TestCaseSource(nameof(NewCardIds))]
        public void EveryNewCardExecutesItsRealInterpreterAndPaysExactlyOnce(string id)
        {
            CoreVoyageSession s = Start();
            Pose(s, false, 1, 0);
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(id);
            SetSchool(s, "Mana", 10); SetSchool(s, "CurseMarks", 4);
            SetSchool(s, "CurseTurns", 3); SetSchool(s, "CurseDamagePerMark", 2);
            SetSchool(s, "TidePower", 12); SetSchool(s, "ThunderChain", 3);
            s.Battle.EnemyBlock = 4;
            var options = new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 0, AnchorY = 0,
                HasSource = true, SourceX = -1, SourceY = 0, DirectionX = 1, ResourceAmount = spec.ResourceMinimum };
            if (spec.Mechanic == CoreNavalExpansionMechanic.MoveFire)
            { Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, true, -2, 0, false, 0, 0), Is.True); options.AnchorY = -1; options.AnchorX = -1; }
            if (spec.Mechanic == CoreNavalExpansionMechanic.HarborExplosion)
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            if (spec.Mechanic == CoreNavalExpansionMechanic.ThunderCollapse)
                Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Put(s, id); int beforeEnergy = s.Battle.Energy, beforePowder = s.Cargo.Powder;
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(s.GetNavalChoiceUnavailableReason(0, options), Is.Empty, id);
            Assert.That(s.PlayNavalCard(0, options), Is.True, id);
            Assert.That(SuccessfulUses(s, id), Is.EqualTo(1), id);
            Assert.That(s.Battle.Energy, Is.EqualTo(beforeEnergy - card.Energy), id);
            Assert.That(s.Cargo.Powder, Is.EqualTo(beforePowder - card.PowderCost), id);
            Assert.That(s.Battle.DiscardPile, Does.Contain(Index(id)), id);
        }

        [Test]
        public void InvalidShapeOrCancelledChoiceLeavesEveryResourceAndCardUntouched()
        {
            CoreVoyageSession s = Start(); Put(s, "card_school_fire_n01");
            int energy = s.Battle.Energy, powder = s.Cargo.Powder;
            Assert.That(s.PlayNavalCard(0, null), Is.False);
            Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 3, AnchorY = 0 }), Is.False);
            Assert.That(s.Battle.Energy, Is.EqualTo(energy)); Assert.That(s.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(s.Battle.Hand.Count, Is.EqualTo(1)); Assert.That(SuccessfulUses(s, "card_school_fire_n01"), Is.Zero);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.Zero);
        }

        [Test]
        public void EnemyTargetRangeUsesFourGridStepsAndRequiredManaIsNeverOptional()
        {
            CoreVoyageSession s = Start(); Put(s, "card_school_arcane_n03");
            SetSchool(s, "Mana", 1);
            int energy = s.Battle.Energy, enemy = s.Battle.EnemyHull;
            Assert.That(s.PlayNavalCard(0, null), Is.False);
            Assert.That(s.Battle.Schools.Mana, Is.EqualTo(1)); Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            SetSchool(s, "Mana", 2); Pose(s, false, 3, 2);
            Assert.That(s.PlayNavalCard(0, null), Is.False); Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy));
            Pose(s, false, 4, 0);
            Assert.That(s.PlayNavalCard(0, null), Is.True, "Four grid cells are legal even though 400 m exceeds legacy spell reach.");
        }

        [Test]
        public void FieldConsumptionCardsAreUnavailableWithoutAUsableField()
        {
            CoreVoyageSession s = Start();
            foreach (string id in new[] { "card_school_fire_n05", "card_school_fire_n06", "card_school_thunder_n06" })
            {
                Put(s, id); Assert.That(s.CardUnavailableReason(0), Is.Not.Empty, id);
                Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { HasAnchor = true, HasSource = true }), Is.False, id);
            }
            Assert.That(s.Battle.Energy, Is.EqualTo(100));
        }

        [Test]
        public void StarBeamPiercesTenAndPreservesUnusedGuardWithoutAddingRefraction()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 1); SetSchool(s, "TemporaryMana", 1);
            s.Battle.NavalFields.Player.ArcanePiercing = 4; s.Battle.EnemyBlock = 15;
            int before = s.Battle.EnemyHull; Play(s, "card_school_arcane_n03");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - 10)); Assert.That(s.Battle.EnemyBlock, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.AvailableMana, Is.Zero); Assert.That(s.Battle.NavalFields.Player.ArcanePiercing, Is.Zero);
        }

        [Test]
        public void PartialCurseConsumptionKeepsBatchAndExhaustionClearsAllTickState()
        {
            CoreVoyageSession s = Start();
            SetSchool(s, "CurseMarks", 3); SetSchool(s, "CurseTurns", 2);
            SetSchool(s, "CurseDamagePerMark", 4); SetSchool(s, "CurseNextTickBonus", 6);
            int before = s.Battle.EnemyHull;
            Play(s, "card_school_curse_n03", new CoreNavalCardPlayOptions { ResourceAmount = 2 });
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - 26));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1)); Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.EqualTo(6));
            Play(s, "card_school_curse_n02");
            Assert.That(new[] { s.Battle.Schools.CurseMarks, s.Battle.Schools.CurseTurns,
                s.Battle.Schools.CurseDamagePerMark, s.Battle.Schools.CurseNextTickBonus }, Is.EqualTo(new[] { 0, 0, 0, 0 }));
            Play(s, "card_school_curse_b03"); Assert.That(s.Battle.Schools.CurseDamagePerMark, Is.EqualTo(2));
        }

        [Test]
        public void TideChoiceRetainsUnspentResourceAndRejectsOverstripping()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "TidePower", 5);
            Play(s, "card_school_tide_n02", new CoreNavalCardPlayOptions { ResourceAmount = 2 });
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(3));
            s.Battle.EnemyBlock = 2; Put(s, "card_school_tide_n05");
            Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { ResourceAmount = 3 }), Is.False);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(3));
            Assert.That(s.PlayNavalCard(0, new CoreNavalCardPlayOptions { ResourceAmount = 2 }), Is.True);
            Assert.That(s.Battle.Schools.TidePower, Is.EqualTo(1)); Assert.That(s.Battle.EnemyBlock, Is.Zero);
        }

        [Test]
        public void OilWaitsThroughBlockedAttackAndTriggersOnlyOnceOnActualFireHit()
        {
            CoreVoyageSession s = Start(); Play(s, "card_school_fire_n03"); s.Battle.EnemyBlock = 100;
            Play(s, "card_school_fire_n02"); Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.EqualTo(2));
            s.Battle.EnemyBlock = 0; int before = s.Battle.EnemyHull;
            int damage = s.GetBattleCardDamage(s.GetBattleCard(Index("card_school_fire_n02")), NavalBattleStage.Naval);
            Play(s, "card_school_fire_n02");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage - 4));
            Assert.That(s.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(3)); Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
        }

        [Test]
        public void ThunderCollapseUsesOnlySurvivingInstanceCellsAndPaysAllChainBeforeRewards()
        {
            CoreVoyageSession s = Start();
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 0, 0, false, 0, 0), Is.True);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            SetSchool(s, "ThunderChain", 3); s.Battle.NavalExpansion.Precharge = true;
            s.Battle.NavalExpansion.GroundingReady = true;
            int hull = s.Hull, enemy = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_n06", new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 2, AnchorY = 0 });
            Assert.That(s.Hull, Is.EqualTo(hull)); Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - 30));
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(3)); Assert.That(s.Battle.NavalExpansion.GroundingUsed, Is.True);
            Assert.That(s.Battle.NavalFields.Cells.Count, Is.EqualTo(4)); Assert.That(s.GetNavalFieldAt(2, 0), Is.Null);
        }

        [Test]
        public void AreaDamageResolvesBothShipsBeforeMutualSinkingOutcome()
        {
            CoreVoyageSession s = Start(); Pose(s, false, 1, 0); SetHull(s, 20); s.Battle.EnemyHull = 20;
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            Play(s, "card_school_fire_n06", new CoreNavalCardPlayOptions { HasAnchor = true });
            Assert.That(s.Hull, Is.Zero); Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Defeat));
        }

        [Test]
        public void NoWoodFallbackStaysTwoAndUtilityCardsCannotGainUnspecifiedUpgrades()
        {
            CoreVoyageSession s = Start(); s.Cargo.Wood = 0;
            Assert.That(s.GetBattleCardEffects(s.GetBattleCard(Index("card_school_tide_n03")), NavalBattleStage.Naval).Block, Is.EqualTo(2));
            Play(s, "card_school_tide_n03"); Assert.That(s.Battle.PlayerBlock, Is.EqualTo(2));
            Assert.That(s.Battle.NavalFields.Player.AnchorCharges, Is.Zero);
            Assert.That(s.GetCardMaximumUpgradeLevel("card_school_thunder_n05"), Is.Zero);
            Assert.That(s.GetCardMaximumUpgradeLevel("card_school_fire_n01"), Is.Zero);
            Assert.That(CoreNavalSchoolExpansionCatalog.All.Count, Is.EqualTo(27));
        }

        [Test]
        public void ArcCannonSecondSegmentCanTriggerOneHitRewardAndDoesNotRepeatDamageBonuses()
        {
            CoreVoyageSession s = Start();
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 1, 0, false, 0, 0), Is.True);
            s.Battle.PlayerLoadedShot = 7; SetSchool(s, "ThunderReadyDamage", 5);
            s.Battle.NavalFields.Player.BloodAttackBonus = 4;
            s.Battle.NavalExpansion.GroundingReady = s.Battle.NavalExpansion.Precharge = true;
            SetSchool(s, "ThunderChain", 2);
            s.Battle.PlayerFireRounds = 1; s.Battle.PlayerFireRoundDamage = 3;
            CoreBattleCard card = s.GetBattleCard(Index("card_school_thunder_n02"));
            int liveFirstSegment = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            s.Battle.EnemyBlock = liveFirstSegment;
            int enemy = s.Battle.EnemyHull, energy = s.Battle.Energy, morale = s.Morale;
            Play(s, card.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(enemy - 6), "The second segment stays six even after load, precharge and blood bonuses.");
            Assert.That(s.Battle.Energy, Is.EqualTo(energy)); Assert.That(s.Battle.NavalExpansion.GroundingUsed, Is.True);
            Assert.That(s.Battle.Schools.ThunderChain, Is.EqualTo(5));
            Assert.That(s.Battle.PlayerFireRounds, Is.Zero); Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(3));
            Assert.That(s.Morale, Is.EqualTo(morale + 2));
        }

        [Test]
        public void SelfAreaDamageConsumesOrdinaryGuardBeforePaidGuardAndCannotGenerateTide()
        {
            CoreVoyageSession s = Start();
            s.Battle.PlayerBlock = 40; SetSchool(s, "TidePaidBlock", 6);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            Play(s, "card_school_fire_n06", new CoreNavalCardPlayOptions { HasAnchor = true });
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(10));
            Assert.That(s.Battle.Schools.TidePaidBlock, Is.EqualTo(6)); Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [Test]
        public void LifePaymentCannotCrossItsOwnLowHullThresholdOrSpendTheLastPoint()
        {
            CoreVoyageSession s = Start();
            SetHull(s, (int)((long)s.MaxHull * 40 / 100) + 1);
            int before = s.Battle.EnemyHull;
            int damage = s.GetBattleCardDamage(s.GetBattleCard(Index("card_school_blood_n03")), NavalBattleStage.Naval);
            Play(s, "card_school_blood_n03"); Assert.That(s.Battle.EnemyHull, Is.EqualTo(before - damage));
            SetHull(s, 5); Put(s, "card_school_blood_n03");
            int energy = s.Battle.Energy, powder = s.Cargo.Powder;
            Assert.That(s.PlayNavalCard(0, null), Is.False); Assert.That(s.Hull, Is.EqualTo(5));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy)); Assert.That(s.Cargo.Powder, Is.EqualTo(powder));
        }

        [Test]
        public void SameIdOnceLimitAndEscalatingCostShareAcrossAllCopies()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 3);
            Play(s, "card_school_arcane_n02"); Put(s, "card_school_arcane_n02");
            int mana = s.Battle.Schools.AvailableMana, energy = s.Battle.Energy;
            Assert.That(s.PlayNavalCard(0, null), Is.False); Assert.That(s.Battle.Schools.AvailableMana, Is.EqualTo(mana));
            Assert.That(s.Battle.Energy, Is.EqualTo(energy));
            Play(s, "card_school_fire_n04"); energy = s.Battle.Energy;
            Play(s, "card_school_fire_n04"); Assert.That(s.Battle.Energy, Is.EqualTo(energy - 2));
        }

        [Test]
        public void DebuffsPreserveZeroGainsAndCapWholeAttackReductionAndActualHealing()
        {
            CoreVoyageSession s = Start(); s.Battle.NavalExpansion.RuneClamp = s.Battle.NavalExpansion.LoadSeal = true;
            Assert.That(s.NavalExpansionEnemyBlockGain(0, true), Is.Zero); Assert.That(s.Battle.NavalExpansion.RuneClamp, Is.True);
            Assert.That(s.NavalExpansionEnemyLoadGain(0, true), Is.Zero); Assert.That(s.Battle.NavalExpansion.LoadSeal, Is.True);
            Assert.That(s.NavalExpansionEnemyBlockGain(10, true), Is.EqualTo(4));
            Assert.That(s.NavalExpansionEnemyLoadGain(4, true), Is.Zero);
            s.Battle.NavalExpansion.PulseReduction = 8; Assert.That(s.NavalExpansionEnemyAttackDamage(6, true), Is.EqualTo(3));
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull - 5; s.Battle.NavalExpansion.EnemyHealingHalved = true;
            Assert.That(s.NavalExpansionHealing(false, 20), Is.EqualTo(2));
        }

        private static CoreVoyageSession Start()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SetSeed("NAVAL-EXPANSION-CARDS"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[4]), Is.True); Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0); SetHull(s, 1000);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
        private static void SetHull(CoreVoyageSession s, int value) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, value);
        private static void SetSchool(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(s.Battle.Schools, value);
        private static int SuccessfulUses(CoreVoyageSession s, string id) => (int)typeof(CoreBattleState)
            .GetMethod("SuccessfulCardUses", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle, new object[] { id });
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        { Put(s, id); Assert.That(s.PlayCardOrNavalForTest(options), Is.True, s.CardUnavailableReason(0)); }
    }
    internal static class NavalExpansionTestExtensions
    {
        internal static bool PlayCardOrNavalForTest(this CoreVoyageSession session, CoreNavalCardPlayOptions options)
        {
            CoreBattleCard card = session.GetBattleCard(session.Battle.Hand[0]);
            return CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) == null ? session.PlayCard(0) : session.PlayNavalCard(0, options);
        }
    }
}
