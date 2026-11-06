using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalSixSchoolIntegrationTests
    {
        [Test]
        public void ArcaneMissileGetsNoRetiredBloodBonusOrRefractionEntryBenefit()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session, false, 300, 0, 180);
            Set(session.Battle.Schools, "Mana", 1);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, true, 1, 0, false, 0, 0), Is.True);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Arcane, true, 2, 0, false, 0, 0), Is.True);
            Assert.That(session.PushNavalShip(true, 1, 0), Is.True);
            session.Battle.EnemyBlock = 15;
            int hp = session.Battle.EnemyHull;
            CoreBattleCard missile = session.GetBattleCard(CardIndex("card_school_arcane_b03"));
            Assert.That(session.GetNavalFieldAt(3, 0).Kind, Is.EqualTo(CoreNavalFieldKind.Arcane));
            Assert.That(session.GetBattleCardDamage(missile, NavalBattleStage.Naval), Is.EqualTo(13),
                "The target's refraction vulnerability scales ten damage by 25%; entering either field still grants no attack reserve.");
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
            Play(session, missile.Id);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp));
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(2), "All thirteen damage remains blockable; refraction entry grants no piercing.");
            Assert.That(session.Battle.Schools.ArcaneNextAttackDamage, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.ArcanePiercing, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.Zero);
            Assert.That(session.Battle.Schools.Mana, Is.Zero, "Entering refraction grants no mana, so the card spends the existing point.");
            Assert.That(session.Battle.Schools.TemporaryMana, Is.Zero);
        }

        [Test]
        public void LegacyDefense_DoesNotConsumeAttackOnlyFieldBuffs()
        {
            CoreVoyageSession session = NewBattle();
            Set(session.Battle.Schools, "Mana", 3);
            session.Battle.NavalFields.Player.BloodAttackBonus = 4;
            session.Battle.NavalFields.Player.ArcanePiercing = 4;
            int hp = session.Battle.EnemyHull;
            Play(session, "card_school_arcane_b05");
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(12));
            Assert.That(session.Battle.Schools.ArcaneNextBlockBonus, Is.Zero);
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp));
            Assert.That(session.Battle.NavalFields.Player.BloodAttackBonus, Is.EqualTo(4));
            Assert.That(session.Battle.NavalFields.Player.ArcanePiercing, Is.Zero, "Retired refraction charges cannot affect new cards.");
        }

        [Test]
        public void FireAttackConsumesOilForNewBurnEvenWhenFullyBlocked()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.NavalExpansion.OilPlayerEndsRemaining = 2;
            session.Battle.EnemyBlock = 100;
            int hp = session.Battle.EnemyHull;
            Play(session, "card_school_fire_b03");
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp));
            Assert.That(session.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(12), "Oil doubles the new burn regardless of hull damage.");
            session.Battle.EnemyBlock = 0;
            int quote = session.GetBattleCardDamage(session.GetBattleCard(CardIndex("card_school_fire_b03")), NavalBattleStage.Naval);
            Play(session, "card_school_fire_b03");
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - quote));
            Assert.That(session.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(18));
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(18));
            Play(session, "card_school_fire_b03");
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - quote * 2));
        }

        [Test]
        public void ExistingBurnSettlesBeforeFieldAddsOilDoubledBurnWithoutActiveHitRewards()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.NavalExpansion.OilPlayerEndsRemaining = 2;
            session.Battle.EnemyBurnDamage = 2;
            session.Battle.EnemyBurnTurns = 2;
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 1, -1, false, 0, 0), Is.True);
            int hp = session.Battle.EnemyHull;
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp), "The two ordinary guard absorb existing burn before the field adds any new stacks.");
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(11), "Existing burn decays to one, then the five-round field adds ten stacks through oil.");
            Assert.That(session.Battle.Schools.ThunderChain, Is.Zero);
        }

        [Test]
        public void LegacyThunderAttack_UsesOneGroundingRefundAndAddsOneChainPlusPrechargeTwo()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Thunder, true, 2, 0, false, 0, 0), Is.True);
            session.Battle.NavalExpansion.GroundingReady = true;
            session.Battle.NavalExpansion.Precharge = true;
            session.Battle.Energy = 10;
            int index = CardIndex("card_school_thunder_b03");
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(10), "One energy was paid and exactly one refunded.");
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(3));
            Assert.That(session.Battle.NavalExpansion.Precharge, Is.False);
            Assert.That(session.Battle.NavalExpansion.GroundingUsed, Is.True);
            session.Battle.NavalExpansion.GroundingReady = true;
            session.Battle.Hand.Add(index);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(8), "A redrawn copy pays the repeated two-energy price without a second refund.");
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(4));
        }

        [Test]
        public void DomainAddsMarksAtEndWhileDetonationDealsImmediateOrdinaryDamage()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Curse, true, 2, 0, false, 0, 0), Is.True);
            int hp = session.Battle.EnemyHull;
            Play(session, "card_school_curse_b03");
            Play(session, "card_school_curse_b03");
            Play(session, "card_school_curse_b05");
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - 5), "The defender's forbidden domain reduces the six-point active detonation by 25%, rounded up.");
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(6));
            Assert.That(session.Battle.Schools.CurseTickBonus, Is.Zero);
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - 5), "Marks no longer cause turn-end damage.");
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(2), "The field grants marks without consuming guard.");
            Assert.That(session.Battle.Schools.CurseNextTickBonus, Is.Zero);
            Assert.That(session.Battle.Schools.CurseTickBonus, Is.Zero);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(11));
            Assert.That(session.Battle.Schools.CurseTurns, Is.EqualTo(11));
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyHull, Is.EqualTo(hp - 5));
            Assert.That(session.Battle.Schools.CurseTickBonus, Is.Zero);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(15));
            Assert.That(session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(2, 0)), Is.EqualTo(3));
        }

        [Test]
        public void EnemyBloodPool_DoesNotGrantGuardOrStrengthenCannonAtTurnStart()
        {
            CoreVoyageSession baseline = NewBattle();
            int baselineHull = baseline.Hull;
            baseline.EndBattleTurn();
            int baselineDamage = baselineHull - baseline.Hull;
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 2, 0, false, 0, 0), Is.True);
            int hp = session.Hull;
            session.EndBattleTurn();
            Assert.That(session.Hull, Is.EqualTo(hp - baselineDamage));
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(2));
            Assert.That(session.Battle.NavalFields.Enemy.BloodAttackBonus, Is.Zero);
            hp = session.Hull;
            session.EndBattleTurn();
            Assert.That(session.Hull, Is.EqualTo(hp - baselineDamage));
            Assert.That(session.Battle.EnemyBlock, Is.EqualTo(2));
        }

        [Test]
        public void RuneClamp_IsNotConsumedByStandingBloodPoolAndStopsNormalActionGuard()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 2, 0, false, 0, 0), Is.True);
            session.Battle.NavalExpansion.RuneClamp = true;
            session.EndBattleTurn();
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.Battle.NavalExpansion.RuneClamp, Is.False);
        }

        [Test]
        public void PreparationTransition_DoesNotShortenFirstNavalFieldLifetime()
        {
            CoreVoyageSession session = NewBattle(false);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, true, 0, 0, false, 0, 0), Is.False);
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, true, 0, 0, false, 0, 0), Is.True);
            session.EndBattleTurn();
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(4));
            Assert.That(session.Battle.PlayerBlock, Is.Zero);
            for (int round = 1; round < 5; round++) session.EndBattleTurn();
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [Test]
        public void BoardingEntry_ClearsFieldsLedgerAndExpansionStatuses()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 0, false, 0, 0), Is.True);
            session.PushNavalShip(true, 1, 0);
            session.Battle.NavalExpansion.OilPlayerEndsRemaining = 2;
            session.Battle.NavalExpansion.GroundingReady = true;
            session.Battle.NavalFields.Player.ArcanePiercing = 4;
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
            Assert.That(session.Battle.NavalFields.Player.ArcanePiercing, Is.Zero);
            Assert.That(session.Battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
            Assert.That(session.Battle.NavalExpansion.GroundingReady, Is.False);
        }

        [Test]
        public void LegacyLethalAttack_ClearsNavalFieldsAtTrainingBattleCompletion()
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleState battle = session.Battle;
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 1, -1, false, 0, 0), Is.True);
            battle.EnemyHull = 1;
            battle.NavalExpansion.OilPlayerEndsRemaining = 2;
            Play(session, "card_school_fire_b03");
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(session.Phase, Is.Not.EqualTo(CoreDemoPhase.Battle));
            Assert.That(battle.NavalFields.Cells, Is.Empty);
            Assert.That(battle.NavalExpansion.OilPlayerEndsRemaining, Is.Zero);
        }

        [Test]
        public void EnemyIntent_DoesNotIncludeRetiredBloodAttackBonus()
        {
            CoreVoyageSession session = NewBattle();
            int ordinary = int.Parse(Regex.Match(session.GetEnemyIntent(), @"炮击伤害 (\d+)").Groups[1].Value);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 2, 0, false, 0, 0), Is.True);
            string intent = session.GetEnemyIntent();
            Assert.That(int.Parse(Regex.Match(intent, @"炮击伤害 (\d+)").Groups[1].Value), Is.EqualTo(ordinary), intent);
            Assert.That(session.Battle.NavalFields.Enemy.BloodAttackBonus, Is.Zero, "A visible intent is read-only.");
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
        }
        [Test]
        public void ZeroStepEnemyPreview_ReturnsNoBloodBenefitOrClampConsumption()
        {
            CoreVoyageSession session = NewBattle();
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 2, 0, false, 0, 0), Is.True);
            CoreNavalMovementFieldPreview ordinary = session.GetEnemyNavalFieldPreview(0);
            Assert.That(ordinary.FinalBlock, Is.Zero);
            Assert.That(ordinary.AttackBonus, Is.Zero);
            Assert.That(ordinary.RuneClampConsumed, Is.False);
            session.Battle.NavalExpansion.RuneClamp = true;
            CoreNavalMovementFieldPreview clamped = session.GetEnemyNavalFieldPreview(0);
            Assert.That(clamped.FinalBlock, Is.Zero);
            Assert.That(clamped.AttackBonus, Is.Zero);
            Assert.That(clamped.RuneClampConsumed, Is.False);
            Assert.That(session.Battle.NavalExpansion.RuneClamp, Is.True);
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty);
        }

        [Test]
        public void EnemyPreviewCrossesBloodAndFireWithoutEntryBenefitsOrDamage()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session, false, 500, 0, 180);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 3, 0, false, 0, 0), Is.True);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 1, -1, false, 0, 0), Is.True);
            CoreNavalMovementFieldPreview preview = session.GetEnemyNavalFieldPreview(300);
            Assert.That(preview.ActiveSteps, Is.EqualTo(3));
            Assert.That(preview.FinalPositionMeters.X, Is.EqualTo(200));
            Assert.That(preview.Damage, Is.Zero);
            Assert.That(preview.HullDamage, Is.Zero);
            Assert.That(preview.FinalBlock, Is.Zero);
            Assert.That(preview.AttackBonus, Is.Zero);
            Assert.That(preview.RuneClampConsumed, Is.False);
            Assert.That(session.Battle.EnemyBlock, Is.Zero);
            Assert.That(session.Battle.NavalFields.Enemy.BloodAttackBonus, Is.Zero);
        }

        [Test]
        public void EnemyPreviewAvoidsEndingInBloodAndPreservesClamp()
        {
            CoreVoyageSession session = NewBattle();
            SetPose(session, false, 500, 0, 180);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Blood, false, 3, 0, false, 0, 0), Is.True);
            session.Battle.NavalExpansion.RuneClamp = true;
            CoreNavalMovementFieldPreview preview = session.GetEnemyNavalFieldPreview(100);
            Assert.That(preview.FinalPositionMeters, Is.EqualTo(new VoyagePoint(500, 0)), "A one-cell approach would end in the blood pool and lose hull, so the safe wait wins.");
            Assert.That(preview.ActiveSteps, Is.Zero);
            Assert.That(preview.FinalBlock, Is.Zero);
            Assert.That(preview.AttackBonus, Is.Zero);
            Assert.That(preview.RuneClampConsumed, Is.False);
            Assert.That(session.Battle.NavalExpansion.RuneClamp, Is.True);
        }
        private static CoreVoyageSession NewBattle(bool naval = true)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            if (naval) session.Battle.Stage = NavalBattleStage.Naval;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.Energy = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = 30;
            SetPose(session, true, 0, 0, 0);
            SetPose(session, false, 200, 0, 180);
            return session;
        }
        private static int CardIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Energy = Math.Max(100, session.Battle.Energy);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(CardIndex(id));
            string reason = session.CardUnavailableReason(0);
            Assert.That(session.PlayCard(0), Is.True, id + ": " + reason);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void SetPose(CoreVoyageSession session, bool player, float x, float y, float heading) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { player, new VoyagePoint(x, y), heading });
    }
}


