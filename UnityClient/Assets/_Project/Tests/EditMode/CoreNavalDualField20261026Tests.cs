using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalDualField20261026Tests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(3, true, true, false, 5)]
        [TestCase(3, true, false, true, 3)]
        [TestCase(1, false, false, true, 1)]
        [TestCase(0, true, true, false, 0)]
        [TestCase(int.MaxValue, true, true, false, int.MaxValue)]
        public void AttackMultipliersRoundOnlyOnceAndCannotOverflow(int damage, bool fire, bool arcane, bool curse, int expected)
        {
            Assert.That(CoreFieldTradeoffRules.ApplyAttackDamage(damage, fire, arcane, curse), Is.EqualTo(expected));
        }

        [TestCase("card_school_fire_n07", 0, -1)]
        [TestCase("card_school_fire_n07", -1, 1)]
        [TestCase("card_school_fire_n07", -1, 2)]
        [TestCase("card_school_fire_n07", 0, 1)]
        [TestCase("card_school_fire_n07", 0, 2)]
        [TestCase("card_school_arcane_n07", 0, 1)]
        [TestCase("card_school_arcane_n07", 0, 2)]
        [TestCase("card_school_arcane_n04", 0, 1)]
        [TestCase("card_school_blood_b04", 0, 2)]
        public void PlayerAttackQuoteAndPaidDamageUseBothOccupiedFields(string id, int attackerField, int defenderField)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Set(s.Battle.Schools, "Mana", 11);
            int original = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            AddOptionalField(s, attackerField, true, false);
            AddOptionalField(s, defenderField, false, true);
            int expected = Scaled(original, attackerField == 0, defenderField == 1, defenderField == 2);
            long sequence = s.Battle.NavalFields.PresentationSequence;
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expected));
            Assert.That(s.Battle.NavalFields.PresentationSequence, Is.EqualTo(sequence), "Reading a quote must not trigger a field.");
            s.Battle.EnemyBlock = 3;
            int hull = s.Battle.EnemyHull;
            Play(s, id);
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(Math.Max(0, expected - 3)));
            if (id == "card_school_fire_n07")
                Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6), "Attached burn stacks are not attack damage.");
        }

        [Test]
        public void CurseDetonationCountsAsAnActiveAttackEvenWithZeroAuthoredBaseDamage()
        {
            CoreVoyageSession s = NewBattle();
            Set(s.Battle.Schools, "CurseMarks", 3);
            AddField(s, CoreNavalFieldKind.Fire, true, false, 5);
            AddField(s, CoreNavalFieldKind.Arcane, false, true, 5);
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_curse_b05");
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(5));
        }

        [Test]
        public void StarBeamScalesBothHitsAndPreservesItsFullyPiercingFirstHit()
        {
            CoreVoyageSession s = NewBattle();
            AddField(s, CoreNavalFieldKind.Fire, true, false, 5);
            AddField(s, CoreNavalFieldKind.Arcane, false, true, 5);
            s.Battle.EnemyBlock = 100;
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_arcane_n03");
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(16), "The first 10 damage remains fully piercing after scaling.");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(59), "The second 26 damage is independently scaled once to 41.");
        }

        [Test]
        public void ThunderCannonScalesItsSecondaryActiveHitWithoutDoubleScalingThePrimary()
        {
            CoreVoyageSession s = NewBattle();
            Set(s.Battle.Schools, "ThunderChain", 2);
            CoreBattleCard card = s.GetBattleCard(Index("card_school_thunder_n02"));
            int raw = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            AddField(s, CoreNavalFieldKind.Fire, true, false, 5);
            AddField(s, CoreNavalFieldKind.Arcane, false, true, 5);
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_thunder_n02");
            Assert.That(hull - s.Battle.EnemyHull, Is.EqualTo(Scaled(raw, true, true, false) + 19));
        }

        [TestCase("card_school_arcane_n03", true)]
        [TestCase("card_school_thunder_n02", true)]
        [TestCase("card_school_arcane_n04", true)]
        [TestCase("card_school_thunder_n09", false)]
        [TestCase("card_school_blood_e03", false)]
        [TestCase("card_school_curse_b05", true)]
        public void LiveCardFaceKeepsCompleteUnmodifiedFormulaAndExplainsCombinedFieldScaling(string id, bool arcaneTarget)
        {
            CoreVoyageSession s = NewBattle();
            if (id == "card_school_blood_e03")
                id = CoreSpecialCardCatalog.All.Select(d => d.Card).Single(c => c.ArtId == id).Id;
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Set(s.Battle.Schools, "Mana", 11);
            Set(s.Battle.Schools, "ThunderChain", 3);
            Set(s.Battle.Schools, "CurseMarks", 3);
            string original = s.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            AddField(s, CoreNavalFieldKind.Fire, true, false, 5);
            AddField(s, arcaneTarget ? CoreNavalFieldKind.Arcane : CoreNavalFieldKind.Curse, false, true, 5);
            string face = s.BattleCardFaceDescription(card, NavalBattleStage.Naval);
            Assert.That(face, Does.StartWith(original + "\n场地修正"), "The authored formula and fixed secondary hit must not be partly scaled twice.");
            Assert.That(face, Does.Contain("应用于上述完整伤害，合并后向上取整"));
            Assert.That(face, Does.Contain("火场：主动攻击伤害 +25%"));
            Assert.That(face, Does.Contain(arcaneTarget ? "敌舰折射阵：对其主动攻击伤害 +25%" : "敌舰禁域：对其主动攻击伤害 -25%"));
            Assert.That(face, Does.Not.Contain("{"));
        }

        [TestCase(true)] [TestCase(false)]
        public void BossCannonPreviewUsesTheFireFieldAtItsFutureLandingWithoutMutatingBattle(bool owner)
        {
            CoreVoyageSession s = NewBattle();
            Call(s, "StartBattle", CoreRouteBossCatalog.Find("boss_route_shattered_sail_reef").CreateEncounter(), true);
            s.Battle.Stage = NavalBattleStage.Naval;
            s.Battle.PhaseTurn = 3; // Fire protection makes the destination a useful field to seek.
            s.Battle.EnemyNavalBossActionsCompleted = 3; // The authored pressure action follows place, extend and expand.
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.EnemyLoadedShot = 0;
            Pose(s, true, 0, 0); Pose(s, false, 4, 0);
            Assert.That(s.EnemyNavalBossRushThisTurn, Is.False);
            object originalPlan = Call(s, "BuildEnemyPlan");
            float meters = (float)PlanField(originalPlan, "Move");
            CoreNavalMovementFieldPreview landing = s.GetEnemyNavalFieldPreview(meters);
            Assert.That(landing.ActiveSteps, Is.GreaterThan(0));
            CoreNavalFieldCell fire = AddField(s, CoreNavalFieldKind.Fire, false, owner, 5);
            fire.X = (int)Math.Round(landing.FinalPositionMeters.X / 100);
            fire.Y = (int)Math.Round(landing.FinalPositionMeters.Y / 100);
            Assert.That(fire.PositionMeters, Is.Not.EqualTo(s.Battle.EnemyPositionMeters));
            CoreNavalMovementFieldPreview preview = s.GetEnemyNavalFieldPreview(meters);
            Assert.That(preview.FinalPositionMeters, Is.EqualTo(fire.PositionMeters));
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage(
                Scaled((int)PlanField(originalPlan, "Attack"), true, false, false), s.EffectiveArmor);
            AssertReadOnlyEnemyQuote(s, expected, meters);
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.EnemyPositionMeters, Is.EqualTo(preview.FinalPositionMeters));
            Assert.That(hull - s.Hull, Is.EqualTo(expected));
        }

        [TestCase(1, false)] [TestCase(5, true)]
        public void EnemyIntentTracksPlayersPendingCurrentPushAndDestinationFieldExpiry(int turns, bool vulnerable)
        {
            CoreVoyageSession s = NewBattle();
            CoreNavalFieldCell current = AddField(s, CoreNavalFieldKind.Tide, true, true, 5);
            current.DirectionY = 1;
            CoreNavalFieldCell destination = AddField(s, CoreNavalFieldKind.Arcane, true, false, turns);
            destination.Y = 1;
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage(
                Scaled(CoreTrainingTargets.All[0].CannonDamage, false, vulnerable, false), s.EffectiveArmor);
            AssertReadOnlyEnemyQuote(s, expected, 0);
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Battle.PlayerPositionMeters, Is.EqualTo(new VoyagePoint(0, 100)));
            Assert.That(hull - s.Hull, Is.EqualTo(expected));
        }

        [Test]
        public void ThunderBossIntentAppliesTheNewFieldBeforeQuotingItsAttack()
        {
            CoreVoyageSession s = NewBattle();
            Call(s, "StartBattle", CoreRouteBossCatalog.Find("boss_route_golden_tide_trade_thunder").CreateEncounter(), true);
            s.Battle.Stage = NavalBattleStage.Naval;
            s.Battle.PhaseTurn = 2;
            s.Battle.EnemyNavalBossActionsCompleted = 0;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.EnemyLoadedShot = 0;
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            object plan = Call(s, "BuildEnemyPlan");
            Assert.That(((CoreNavalBossMove)PlanField(plan, "BossMove")).PlaceThunderField, Is.True);
            AddField(s, CoreNavalFieldKind.Fire, false, true, 5);
            AddField(s, CoreNavalFieldKind.Arcane, true, false, 5);
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage((int)PlanField(plan, "Attack"), s.EffectiveArmor);
            AssertReadOnlyEnemyQuote(s, expected, 0);
            Assert.That(s.GetEnemyNavalFieldIntent(), Does.Not.Contain("停留火场"));
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(expected));
            Assert.That(s.GetNavalFieldAt(0, 0).Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
            Assert.That(s.GetNavalFieldAt(2, 0).Kind, Is.EqualTo(CoreNavalFieldKind.Thunder));
            Assert.That(s.Battle.EnemyBurnTurns, Is.Zero, "The replaced fire field cannot add end-turn burn.");
        }

        [TestCase(0, -1)] [TestCase(-1, 1)] [TestCase(-1, 2)]
        [TestCase(0, 1)] [TestCase(0, 2)]
        public void EnemyCannonIntentMatchesActualDamageAfterFieldModifiers(int attackerField, int defenderField)
        {
            CoreVoyageSession s = NewBattle();
            Assert.That(s.EffectiveArmor, Is.Zero);
            AddOptionalField(s, attackerField, false, true);
            AddOptionalField(s, defenderField, true, false);
            int expected = CoreNavalStatRules.ReduceIncomingCannonDamage(
                Scaled(CoreTrainingTargets.All[0].CannonDamage, attackerField == 0, defenderField == 1, defenderField == 2), s.EffectiveArmor);
            string intent = s.GetEnemyIntent();
            Match quote = Regex.Match(intent, @"炮击伤害 (\d+)");
            Assert.That(quote.Success, Is.True, intent);
            Assert.That(int.Parse(quote.Groups[1].Value), Is.EqualTo(expected));
            int hull = s.Hull;
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(hull - s.Hull, Is.EqualTo(expected));
        }

        [TestCase(CoreNavalFieldKind.Fire, true)]
        [TestCase(CoreNavalFieldKind.Arcane, false)]
        [TestCase(CoreNavalFieldKind.Curse, false)]
        public void AttackModifierEndsImmediatelyWhenItsOccupantLeaves(CoreNavalFieldKind kind, bool player)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleCard card = s.GetBattleCard(Index("card_school_fire_n07"));
            int original = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            AddField(s, kind, player, !player, 5);
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.Not.EqualTo(original));
            Pose(s, player, player ? -1 : 3, 0);
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(original));
        }

        [TestCase(CoreNavalFieldKind.Arcane)] [TestCase(CoreNavalFieldKind.Curse)]
        public void BurningIsNotAnActiveAttackAndIgnoresTargetDamageModifiers(CoreNavalFieldKind kind)
        {
            CoreVoyageSession s = NewBattle();
            AddField(s, kind, true, false, 5);
            AddField(s, CoreNavalFieldKind.Fire, false, true, 5);
            s.Battle.PlayerBurnStacks = 8;
            s.Battle.PlayerBlock = 3;
            int hull = s.Hull;
            Call(s, "ApplyNavalPlayerBurn");
            Assert.That(s.Hull, Is.EqualTo(hull - 5));
            Assert.That(s.Battle.PlayerBlock, Is.Zero);
            Assert.That(s.Battle.PlayerBurnStacks, Is.EqualTo(7));
        }

        [Test]
        public void ThunderOverloadIsNotBoostedByTheOtherShipsFireField()
        {
            CoreVoyageSession s = NewBattle();
            AddField(s, CoreNavalFieldKind.Fire, true, true, 5);
            AddField(s, CoreNavalFieldKind.Thunder, false, true, 5);
            s.Battle.NavalFields.Enemy.ThunderChain = 49;
            s.Battle.EnemyBlock = 7;
            int hull = s.Battle.EnemyHull;
            Call(s, "GainNavalThunderChain", false, 1);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 993));
            Assert.That(s.Battle.EnemyBlock, Is.Zero);
            Assert.That(s.Battle.NavalFields.Enemy.ThunderChain, Is.EqualTo(50));
        }

        [TestCase(true, true)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(false, false)]
        public void BloodPaymentGivesDoubleGuardButEndTurnLossIsNotAnotherPayment(bool player, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            if (!player) s.NavalFieldsBeginTurn(false);
            AddField(s, CoreNavalFieldKind.Blood, player, owner, 7);
            int health = Health(s, player);
            Call(s, "PayBattleHealthCost", player, 5);
            Call(s, "PayBattleHealthCost", player, 3);
            Assert.That(Health(s, player), Is.EqualTo(health - 8));
            Assert.That(Guard(s, player), Is.EqualTo(16));
            Assert.That(s.NavalFieldsEndTurn(player), Is.False);
            Assert.That(Health(s, player), Is.EqualTo(health - 15));
            Assert.That(Guard(s, player), Is.EqualTo(16), "Passive loss bypasses guard and cannot earn a rebate.");
            int after = Health(s, player);
            s.NavalFieldsEndTurn(player);
            Assert.That(Health(s, player), Is.EqualTo(after), "Repeated settlement must not drain twice.");
        }

        [TestCase(true, true)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(false, false)]
        public void BloodLossUsesRemainingDurationBeforeItsExpirationTick(bool player, bool owner)
        {
            CoreVoyageSession s = NewBattle();
            if (!player) s.NavalFieldsBeginTurn(false);
            CoreNavalFieldCell field = AddField(s, CoreNavalFieldKind.Blood, player, owner, 1);
            int health = Health(s, player);
            s.NavalFieldsEndTurn(player);
            Assert.That(Health(s, player), Is.EqualTo(health - 1));
            Assert.That(s.Battle.NavalFields.Cells.Contains(field), Is.EqualTo(player == owner));
        }

        [TestCase(true)] [TestCase(false)]
        public void BloodEndLossCanKillEitherSideDespiteGuard(bool player)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleState battle = s.Battle;
            if (!player) s.NavalFieldsBeginTurn(false);
            AddField(s, CoreNavalFieldKind.Blood, player, true, 5);
            if (player) Set(s, "Hull", 4); else battle.EnemyHull = 4;
            battle.PlayerBlock = battle.EnemyBlock = 1000;
            Assert.That(s.NavalFieldsEndTurn(player), Is.True);
            Assert.That(player ? s.Hull : battle.EnemyHull, Is.Zero);
            Assert.That(player ? battle.PlayerBlock : battle.EnemyBlock, Is.EqualTo(1000));
        }

        [Test]
        public void ExtendingBloodPoolImmediatelyRaisesItsTurnEndCost()
        {
            CoreVoyageSession s = NewBattle();
            CoreNavalFieldCell field = AddField(s, CoreNavalFieldKind.Blood, true, true, 2);
            Call(s, "ExtendAllPlayerFields", CoreNavalFieldKind.Blood, 5);
            Assert.That(s.GetNavalFieldRemainingTurns(field), Is.EqualTo(7));
            int hull = s.Hull;
            s.NavalFieldsEndTurn(true);
            Assert.That(s.Hull, Is.EqualTo(hull - 7));
        }

        [TestCase(true)] [TestCase(false)]
        public void BloodEndLossCanCrossAnExistingCurseExecutionThreshold(bool player)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleState battle = s.Battle;
            if (!player) s.NavalFieldsBeginTurn(false);
            if (player) { Set(s, "Hull", 100); battle.PlayerCurseMarks = 99; }
            else { battle.EnemyHull = 100; Set(battle.Schools, "CurseMarks", 99); }
            AddField(s, CoreNavalFieldKind.Blood, player, true, 1);
            Assert.That(s.NavalFieldsEndTurn(player), Is.True);
            Assert.That(player ? s.Hull : battle.EnemyHull, Is.Zero);
        }

        [Test]
        public void ARealBloodCardPaysItsOriginalCostAndReceivesDoublePoolRebate()
        {
            CoreVoyageSession s = NewBattle();
            AddField(s, CoreNavalFieldKind.Blood, true, false, 5);
            int hull = s.Hull;
            Play(s, "card_school_blood_n02");
            Assert.That(s.Hull, Is.EqualTo(hull - 3));
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(6));
            Assert.That(s.Battle.NavalExpansion.EnemyHealingBanTurns, Is.EqualTo(2));
        }

        [TestCase(CoreNavalFieldKind.Arcane)] [TestCase(CoreNavalFieldKind.Curse)]
        public void NegativeFieldImmunitySuppressesVulnerabilityButKeepsCurseDefense(CoreNavalFieldKind kind)
        {
            CoreVoyageSession s = NewBattle();
            CoreBattleCard card = s.GetBattleCard(Index("card_school_fire_n07"));
            int original = s.GetBattleCardDamage(card, NavalBattleStage.Naval);
            AddField(s, kind, false, true, 5);
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.Not.EqualTo(original));
            s.Battle.NavalFields.Enemy.AllFieldEffectsImmune = true;
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval),
                Is.EqualTo(kind == CoreNavalFieldKind.Arcane ? original : Scaled(original, false, false, true)));
        }

        [Test]
        public void BossRushKeepsBloodPaymentRebateAndPreventsOnlyEndTurnDrain()
        {
            CoreVoyageSession s = NewBattle();
            Call(s, "StartBattle", CoreRouteBossCatalog.Find("boss_route_shattered_sail_reef").CreateEncounter(), true);
            s.Battle.Stage = NavalBattleStage.Naval;
            s.Battle.PhaseTurn = 5;
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            Pose(s, false, 2, 0);
            s.NavalFieldsBeginTurn(false);
            AddField(s, CoreNavalFieldKind.Blood, false, true, 9);
            s.Battle.EnemyBlock = 0;
            Assert.That(s.Battle.NavalFields.Enemy.AllFieldEffectsImmune, Is.True);
            Call(s, "PayBattleHealthCost", false, 4);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(9996));
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(8));
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(9996));
            Assert.That(s.Battle.NavalFields.Enemy.AllFieldEffectsImmune, Is.False);
            s.NavalFieldsEndTurn(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(9996), "Repeating an already immune turn end must not apply a delayed field cost.");
        }

        [TestCase(true)] [TestCase(false)]
        public void PlayerRamUsesFieldDamageButDoesNotAmplifyItsOwnRecoil(bool targetArcane)
        {
            CoreVoyageSession baseline = NewBattle();
            CoreVoyageSession modified = NewBattle();
            foreach (CoreVoyageSession s in new[] { baseline, modified })
            {
                Pose(s, false, 1, 0);
                Set(s.Battle, "NavalActionPoints", 1);
            }
            AddField(modified, CoreNavalFieldKind.Fire, true, true, 5);
            AddField(modified, targetArcane ? CoreNavalFieldKind.Arcane : CoreNavalFieldKind.Curse, false, false, 5);
            int baseHull = baseline.Hull, changedHull = modified.Hull;
            int baseEnemy = baseline.Battle.EnemyHull, changedEnemy = modified.Battle.EnemyHull;
            Assert.That(baseline.TryRam(), Is.True);
            Assert.That(modified.TryRam(), Is.True);
            Assert.That(changedEnemy - modified.Battle.EnemyHull,
                Is.EqualTo(Scaled(baseEnemy - baseline.Battle.EnemyHull, true, targetArcane, !targetArcane)));
            Assert.That(changedHull - modified.Hull, Is.EqualTo(baseHull - baseline.Hull));
        }

        private static CoreVoyageSession NewBattle()
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(s.RestoreCardPackProgress(null, s.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            s.ConfigureComponentBonuses(10000, -1000, 0, 0, 0);
            s.SetSeed("DUAL-FIELD-20261026");
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = s.Battle.PlayerLoadedShot = 0;
            s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = 100;
            Set(s, "Morale", 0);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            return s;
        }

        private static int Scaled(int amount, bool fire, bool arcane, bool curse) => (int)Math.Ceiling(
            amount * (fire ? 1.25m : 1m) * (arcane ? 1.25m : curse ? .75m : 1m));
        private static object PlanField(object plan, string name) => plan.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(plan);
        private static void AssertReadOnlyEnemyQuote(CoreVoyageSession s, int expected, float meters)
        {
            string before = Snapshot(s);
            string intent = s.GetEnemyIntent();
            for (int read = 0; read < 3; read++)
            {
                Assert.That(s.GetEnemyIntent(), Is.EqualTo(intent));
                int amount = CoreBattleIntentPresentation.Build(s).Where(x => x.Kind == CoreBattleIntentKind.Cannon).Sum(x => x.Amount ?? 0);
                Assert.That(amount, Is.EqualTo(expected), intent);
                s.GetEnemyNavalFieldPreview(meters);
                Assert.That(Snapshot(s), Is.EqualTo(before), "Reading a pending move or field cannot settle it early.");
            }
        }
        private static string Snapshot(CoreVoyageSession s)
        {
            CoreBattleState b = s.Battle;
            CoreNavalFieldState f = b.NavalFields;
            return string.Join("|", new object[] { s.Hull, b.EnemyHull, b.PlayerBlock, b.EnemyBlock,
                b.PhaseTurn, b.Turn, b.TacticalRevision, b.EnemyNavalBossActionsCompleted, b.DecisionSeed,
                b.PlayerPositionMeters.X, b.PlayerPositionMeters.Y, b.PlayerHeadingDegrees,
                b.EnemyPositionMeters.X, b.EnemyPositionMeters.Y, b.EnemyHeadingDegrees,
                b.PlayerBurnStacks, b.EnemyBurnTurns, b.PlayerCurseMarks, b.Schools.CurseMarks,
                f.PlayerTurn, f.EnemyTurn, f.CompletedPlayerTurns, f.CompletedEnemyTurns, f.PresentationSequence,
                string.Join(";", f.TriggerLedger),
                string.Join(";", f.Player.GetType().GetFields().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(f.Player))),
                string.Join(";", f.Enemy.GetType().GetFields().OrderBy(x => x.Name).Select(x => x.Name + "=" + x.GetValue(f.Enemy))),
                string.Join(";", f.Cells.Select(x => x.Kind + ":" + x.X + ":" + x.Y + ":" + x.ExpiresAtOwnerTurn)) });
        }
        private static int Health(CoreVoyageSession s, bool player) => player ? s.Hull : s.Battle.EnemyHull;
        private static int Guard(CoreVoyageSession s, bool player) => player ? s.Battle.PlayerBlock : s.Battle.EnemyBlock;
        private static void AddOptionalField(CoreVoyageSession s, int kind, bool player, bool owner)
        { if (kind >= 0) AddField(s, (CoreNavalFieldKind)kind, player, owner, 5); }
        private static CoreNavalFieldCell AddField(CoreVoyageSession s, CoreNavalFieldKind kind, bool player, bool owner, int turns)
        {
            VoyagePoint point = player ? s.Battle.PlayerPositionMeters : s.Battle.EnemyPositionMeters;
            var field = new CoreNavalFieldCell { Kind = kind, X = (int)Math.Round(point.X / 100), Y = (int)Math.Round(point.Y / 100),
                PlayerOwned = owner, InstanceId = s.Battle.NavalFields.NextInstanceId++, ExpiresAtOwnerTurn = turns +
                    (owner ? s.Battle.NavalFields.CompletedEnemyTurns : s.Battle.NavalFields.CompletedPlayerTurns) };
            s.Battle.NavalFields.Cells.Add(field);
            return field;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Play(CoreVoyageSession s, string id)
        {
            s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id));
            Assert.That(s.PlayCard(0), Is.True, id + ": " + s.CardUnavailableReason(0));
        }
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) =>
            Call(s.Battle, "SetTacticalPose", player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        private static void Set(object target, string name, object value) => target.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}
