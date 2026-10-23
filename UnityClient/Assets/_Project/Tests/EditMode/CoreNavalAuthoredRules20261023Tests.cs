using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalAuthoredRules20261023Tests
    {
        [Test]
        public void BurnUsesOneDecayingTotalAndNewApplicationsExtendThatSameTotal()
        {
            CoreVoyageSession s = Start();
            Play(s, "card_school_fire_b03");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2));
            Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(2));
            int hull = s.Battle.EnemyHull;
            Invoke(s, "ApplySchoolNaturalBurn");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 2));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(1));
            Invoke(s, "ApplySchoolBurn", 3, 99);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(4));
            hull = s.Battle.EnemyHull;
            for (int n = 4; n > 0; n--)
            {
                Invoke(s, "ApplySchoolNaturalBurn");
                hull -= n;
                Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull));
                Assert.That(s.Battle.EnemyBurnDamage, Is.EqualTo(n - 1));
                Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(n - 1));
            }
        }

        [Test]
        public void BasicCurseBonusAddsToEveryTickWithoutAddingLayersOrMultiplyingByThem()
        {
            CoreVoyageSession s = Start();
            Play(s, "card_school_curse_b04");
            Play(s, "card_school_curse_b05");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(2));
            int hull = s.Battle.EnemyHull;
            Invoke(s, "ApplySchoolCurseTick");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 4));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.CurseTurns, Is.EqualTo(1));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.EqualTo(2));
            Invoke(s, "ApplySchoolCurseTick");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 7));
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.Zero);
        }

        [Test]
        public void PersistentCurseBonusesStackAndExtraSettlementPreservesTheirLayers()
        {
            CoreVoyageSession s = Start(); Play(s, "card_school_curse_b04");
            Play(s, "card_school_curse_b05"); Play(s, "card_school_curse_b05");
            SetSchool(s, "CurseNextTickBonus", 3);
            int hull = s.Battle.EnemyHull;
            s.SettleNavalCurse(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 9), "Two layers, four persistent bonus and three one-shot bonus.");
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.CurseTurns, Is.EqualTo(2));
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.EqualTo(4));
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            s.SettleNavalCurse(false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 15));
            s.SettleNavalCurse(true);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 21));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(1));
            s.SettleNavalCurse(true);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 26));
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.Zero);
            Play(s, "card_school_curse_b03");
            s.SettleNavalCurse(true);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 27), "A new curse cannot inherit an expired curse's bonus.");
        }

        [Test]
        public void SpecialCurseDetonationStillAddsOnlyToTheNextSettlement()
        {
            CoreVoyageSession s = Start(); Play(s, "card_school_curse_b04");
            var special = new CoreBattleCard("test_special_curse_detonation", "一次引爆", "策略", 0, 0, 0, 0, 0, 0,
                "", NavalBattleStages.Naval, schoolId: "curse", schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.CurseDetonate, power: 3));
            Invoke(s, "ApplySchoolCardEffect", special, false, 0, false);
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.EqualTo(3));
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.Zero);
            int hull = s.Battle.EnemyHull;
            s.SettleNavalCurse(true); s.SettleNavalCurse(true);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 6));
        }

        [Test]
        public void FirePotAlwaysAddsItsLayersThenExtendsEvenWhenDirectDamageIsBlocked()
        {
            CoreVoyageSession s = Start(); s.Battle.EnemyBlock = 100;
            Play(s, "card_school_fire_b05");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(3));
            Play(s, "card_school_fire_b05");
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(6));
        }

        [Test]
        public void BurningEnemyStillMovesBeforeItsEndOfTurnDamage()
        {
            CoreVoyageSession s = Start();
            s.Battle.EnemyHull = 2;
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 2;
            Assert.That(s.GetEnemyNavalFieldPreview(100).Sinks, Is.False);
        }

        [Test]
        public void LethalEndOfTurnBurnDoesNotCancelTheEnemyAttackOrItsReadOnlyIntent()
        {
            CoreVoyageSession s = Start(0);
            s.Battle.EnemyHull = 2;
            s.Battle.EnemyBurnTurns = s.Battle.EnemyBurnDamage = 2;
            int playerHull = s.Hull;
            var intent = CoreBattleIntentPresentation.Build(s);
            CoreBattleIntent attack = intent.Single(x => x.Kind == CoreBattleIntentKind.Cannon);
            Assert.That(attack.Amount, Is.GreaterThan(0));
            Assert.That(intent.Any(x => x.Id == "enemy_cannot_act"), Is.False);
            Assert.That(s.GetEnemyIntent(), Does.Contain("炮击伤害 " + attack.Amount));
            Assert.That(CoreBattleIntentPresentation.Build(s).Single(x => x.Kind == CoreBattleIntentKind.Cannon).Amount, Is.EqualTo(attack.Amount));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(2)); Assert.That(s.Battle.EnemyHull, Is.EqualTo(2));
            Assert.That(s.EndBattleTurn(), Is.True);
            Assert.That(s.Hull, Is.EqualTo(playerHull - attack.Amount.Value));
            Assert.That(s.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(s.Battle.EnemyHull, Is.Zero);
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(1));
        }

        [Test]
        public void ExtraFireSettlementDoesNotConsumeALayer()
        {
            CoreVoyageSession s = Start();
            CoreBattleCard card = new CoreBattleCard("test_extra_fire_tick", "额外燃烧", "炮击", 1, 1, 0, 0, 0, 0,
                "", NavalBattleStages.Naval, schoolId: "fire", schoolEffect: new CoreSchoolCardEffect(CoreSchoolMechanic.FireExtraTick, power: 2));
            Invoke(s, "ApplySchoolBurn", 3, 3);
            int hull = s.Battle.EnemyHull;
            Invoke(s, "ApplySchoolCardEffect", card, true, 1, false);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 5));
            Assert.That(s.Battle.EnemyBurnTurns, Is.EqualTo(5));
        }

        [Test]
        public void ArcaneReserveBoostsTheNextAttackAndDoesNotBoostItsOwnCreatingAttack()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 20);
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_arcane_b03");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 5));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.EqualTo(5));
            CoreBattleCard cannon = s.GetBattleCard(Index("card_chain_shot"));
            int quote = s.GetBattleCardDamage(cannon, NavalBattleStage.Naval);
            hull = s.Battle.EnemyHull;
            Play(s, "card_chain_shot");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - quote));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.Zero);
        }

        [Test]
        public void AnExistingAttackReserveIsConsumedWhileTheCreatingCardStoresANewReserve()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 20); SetSchool(s, "ArcaneNextAttackDamage", 7);
            CoreBattleCard card = s.GetBattleCard(Index("card_school_arcane_b03"));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(12));
            Assert.That(s.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(12));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.EqualTo(7), "Quoting cannot consume the old reserve.");
            int hull = s.Battle.EnemyHull;
            Play(s, card.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 12));
            Assert.That(s.Battle.Schools.ArcaneNextAttackDamage, Is.EqualTo(5), "Only this card's newly created reserve remains.");
        }

        [Test]
        public void ArcaneShieldStoresFiveForTheNextPositiveBlockGain()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 20);
            Play(s, "card_school_arcane_b05");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(5));
            Assert.That(s.Battle.Schools.ArcaneNextBlockBonus, Is.EqualTo(5));
            Assert.That(s.NavalArcaneBlockGain(0, true), Is.Zero);
            Play(s, "card_brace");
            Assert.That(s.Battle.PlayerBlock, Is.EqualTo(12));
            Assert.That(s.Battle.Schools.ArcaneNextBlockBonus, Is.Zero);
        }

        [Test]
        public void StarBeamUsesSeparateOrdinaryAndPiercingHits()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 10);
            s.Battle.EnemyBlock = 20; int hull = s.Battle.EnemyHull;
            Play(s, "card_school_arcane_n03");
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(6));
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 10));
        }

        [Test]
        public void LoadSealReducesExistingLoadAndDoesNotReduceFutureLoad()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "CurseMarks", 2);
            s.Battle.EnemyLoadedShot = 9;
            Play(s, "card_school_curse_n02");
            Assert.That(s.Battle.EnemyLoadedShot, Is.EqualTo(3));
            Assert.That(s.NavalExpansionEnemyLoadGain(8, true), Is.EqualTo(8));
        }

        [Test]
        public void PulseReducesEveryAttackUntilEnemyTurnEndsWithoutHalfDamageCap()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "ThunderChain", 2);
            Play(s, "card_school_thunder_n04");
            Assert.That(s.NavalExpansionEnemyAttackDamage(6, true), Is.Zero);
            Assert.That(s.NavalExpansionEnemyAttackDamage(12, true), Is.EqualTo(4));
            Assert.That(s.Battle.NavalExpansion.PulseReduction, Is.EqualTo(8));
            s.NavalExpansionEndEnemyTurn();
            Assert.That(s.NavalExpansionEnemyAttackDamage(12), Is.EqualTo(12));
        }

        [Test]
        public void CursePeelUsesTheWholeCurseTotalAndClearsItsBonus()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "CurseMarks", 5); SetSchool(s, "CurseTurns", 5);
            SetSchool(s, "CurseNextTickBonus", 2);
            SetSchool(s, "CurseTickBonus", 4);
            int hull = s.Battle.EnemyHull;
            Play(s, "card_school_curse_n03", new CoreNavalCardPlayOptions { ResourceAmount = 2 });
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 44));
            Assert.That(s.Battle.Schools.CurseMarks, Is.Zero);
            Assert.That(s.Battle.Schools.CurseNextTickBonus, Is.Zero);
            Assert.That(s.Battle.Schools.CurseTickBonus, Is.Zero);
        }

        [Test]
        public void TheTwoAuthoredPreparationThunderCardsStackAndOtherReadyCardsKeepTheirMaximumRule()
        {
            CoreVoyageSession s = Start(); s.Battle.Stage = NavalBattleStage.Preparation;
            Play(s, "card_school_thunder_b01"); Play(s, "card_school_thunder_b01"); Play(s, "card_school_thunder_b02");
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.EqualTo(20));
            Assert.That(s.Battle.Schools.ThunderChain, Is.Zero);
            s.Battle.Stage = NavalBattleStage.Naval;
            Play(s, "card_school_thunder_b05"); Play(s, "card_school_thunder_b05");
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.EqualTo(20));
            CoreBattleCard attack = s.GetBattleCard(Index("card_school_thunder_b03"));
            int quote = s.GetBattleCardDamage(attack, NavalBattleStage.Naval), hull = s.Battle.EnemyHull;
            Play(s, attack.Id);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - quote));
            Assert.That(s.Battle.Schools.ThunderReadyDamage, Is.Zero);
        }

        [TestCase(0, 1)] [TestCase(1, 2)] [TestCase(2, 3)]
        public void CurseMarkHasARealLayerIncreaseAtEveryRank(int rank, int layers)
        {
            CoreVoyageSession s = Start();
            CoreBattleCard card = s.GetBattleCardAtLevel(Index("card_school_curse_b03"), rank);
            Assert.That(card.SchoolEffect.Power, Is.EqualTo(layers));
            Invoke(s, "ApplySchoolCardEffect", card, false, 0, false);
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(layers));
            Assert.That(s.Battle.Schools.CurseTurns, Is.EqualTo(layers));
        }

        [TestCase("card_school_curse_n03", "CurseMarks", 0, 14)]
        [TestCase("card_school_curse_n03", "CurseMarks", 7, 56)]
        [TestCase("card_school_tide_n02", "TidePower", 0, 6)]
        [TestCase("card_school_tide_n02", "TidePower", 7, 20)]
        public void AllResourceAttacksUseTheWholeTotalWithoutASelectionOrMinimum(string id, string resource, int amount, int expectedDamage)
        {
            CoreVoyageSession s = Start(); SetSchool(s, resource, amount); Put(s, id);
            var spec = CoreNavalSchoolExpansionCatalog.FindSpec(id);
            Assert.That(spec.ConsumesAllResource, Is.True);
            Assert.That(s.NavalExpansionResourceAmount(spec), Is.EqualTo(amount));
            Assert.That(s.GetNavalCardChoice(0), Is.Null);
            Assert.That(s.GetNavalChoiceUnavailableReason(0, null), Is.Empty);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayNavalCard(0, null), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - expectedDamage));
            Assert.That(s.NavalExpansionResourceAmount(spec), Is.Zero);
        }

        [TestCase(0, 3)] [TestCase(8, 0)]
        public void TideStripUsesAllTideWithoutAChoiceAndAllowsZero(int amount, int remainingBlock)
        {
            CoreVoyageSession s = Start(); SetSchool(s, "TidePower", amount); s.Battle.EnemyBlock = 3;
            Put(s, "card_school_tide_n05");
            Assert.That(s.GetNavalCardChoice(0), Is.Null);
            Assert.That(s.PlayNavalCard(0, null), Is.True);
            Assert.That(s.Battle.EnemyBlock, Is.EqualTo(remainingBlock));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
        }

        [TestCase(0)] [TestCase(12)]
        public void GreatWaveOnlyChoosesAreaAndDirectionAndUsesAllTide(int amount)
        {
            CoreVoyageSession s = Start(); SetSchool(s, "TidePower", amount); Put(s, "card_school_tide_n06");
            CoreNavalCardChoice choice = s.GetNavalCardChoice(0);
            Assert.That(choice.RequiresAnchor && choice.RequiresDirection, Is.True);
            Assert.That(choice.ResourceMinimum, Is.Zero); Assert.That(choice.ResourceMaximum, Is.Zero);
            var options = new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 2, AnchorY = -1, DirectionX = 1 };
            Assert.That(s.GetNavalChoiceUnavailableReason(0, options), Is.Empty);
            int hull = s.Battle.EnemyHull;
            Assert.That(s.PlayNavalCard(0, options), Is.True);
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - 12 - 2 * amount));
            Assert.That(s.Battle.Schools.TidePower, Is.Zero);
            Assert.That(s.Battle.EnemyPositionMeters.X, Is.EqualTo(300));
        }

        [TestCase(1)] [TestCase(5)]
        public void CurseFieldAddsTwoWithoutChangingLayersOrMultiplyingByThem(int layers)
        {
            CoreVoyageSession s = Start(); SetSchool(s, "CurseMarks", layers); SetSchool(s, "CurseTurns", layers);
            Assert.That(s.PlaceNavalField(CoreNavalFieldKind.Curse, true, 2, 0, false, 0, 0), Is.True);
            Assert.That(s.GetEnemyNavalFieldPreview(0).Summary, Does.Contain("追加 2 伤害"));
            int hull = s.Battle.EnemyHull;
            Invoke(s, "ApplySchoolCurseTick");
            Assert.That(s.Battle.EnemyHull, Is.EqualTo(hull - layers - 2));
            Assert.That(s.Battle.Schools.CurseMarks, Is.EqualTo(layers - 1));
        }

        [Test]
        public void RemovedCardSpecificPlayLimitsAllowRepeatedClampAndFireImmunity()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 20);
            Play(s, "card_school_arcane_n02"); Play(s, "card_school_arcane_n02");
            Assert.That(s.NavalExpansionEnemyBlockGain(15, true), Is.EqualTo(3));
            Play(s, "card_school_fire_n04"); Play(s, "card_school_fire_n04");
            Assert.That(s.Battle.NavalFields.Player.FireImmunityCharges, Is.EqualTo(4));
            Assert.That(s.RestoreNavalFields(s.CaptureNavalFields()), Is.True);
        }

        [Test]
        public void AuthoredGridRangeAgreesBetweenValidationAndHighlight()
        {
            CoreVoyageSession s = Start(); SetSchool(s, "Mana", 20);
            foreach (string id in new[] { "card_school_fire_b05", "card_school_arcane_b03" })
            {
                CoreBattleCard card = s.GetBattleCard(Index(id));
                int maximum = CoreNavalCardRangeRules.MaximumCells(card);
                Pose(s, false, maximum, 0); Put(s, id);
                Assert.That(s.CardUnavailableReason(0), Is.Empty, id);
                Assert.That(s.GetNavalCardAttackRange(card).Contains(s.Battle.PlayerPositionMeters, s.Battle.EnemyPositionMeters), Is.True, id);
                Pose(s, false, maximum + 1, 0);
                Assert.That(s.CardUnavailableReason(0), Is.Not.Empty, id);
                Assert.That(s.GetNavalCardAttackRange(card).Contains(s.Battle.PlayerPositionMeters, s.Battle.EnemyPositionMeters), Is.False, id);
            }
        }

        [TestCase("card_school_fire_n04")]
        [TestCase("card_school_thunder_n03")]
        [TestCase("card_school_tide_n03")]
        public void EnemyDistanceDoesNotRestrictSelfTargetedExpansionCards(string id)
        {
            CoreVoyageSession s = Start(); SetSchool(s, "ThunderChain", 2);
            Pose(s, false, 8, 0); Put(s, id);
            CoreBattleCard card = s.GetBattleCard(Index(id));
            Assert.That(CoreNavalCardRangeRules.MaximumCells(card), Is.Zero);
            Assert.That(CoreNavalCardRangeRules.Contains(card, s.Battle.PlayerPositionMeters, s.Battle.EnemyPositionMeters), Is.True);
            Assert.That(s.CardUnavailableReason(0), Is.Empty);
        }

        [TestCase(0, 2, 2)] [TestCase(1, 3, 3)] [TestCase(2, 3, 4)] [TestCase(3, 4, 5)]
        public void BurnAndCurseUpgradesGrowThePrintedLayerCount(int level, int burnLayers, int curseLayers)
        {
            CoreVoyageSession s = Start();
            CoreBattleCard burn = s.GetBattleCardAtLevel(Index("card_school_fire_b03"), level);
            CoreBattleCard curse = s.GetBattleCardAtLevel(Index("card_school_curse_b04"), level);
            Assert.That(burn.SchoolEffect.Power, Is.EqualTo(burnLayers));
            Assert.That(curse.SchoolEffect.Power, Is.EqualTo(curseLayers));
        }

        private static CoreVoyageSession Start(int targetIndex = 4)
        {
            var s = new CoreVoyageSession();
            s.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            s.SetSeed("NAVAL-AUTHORED-RULES-20261023"); s.StartVoyage();
            Assert.That(s.StartTrainingBattle(CoreTrainingTargets.All[targetIndex]), Is.True);
            Assert.That(s.EndBattleTurn(), Is.True);
            Pose(s, true, 0, 0); Pose(s, false, 2, 0);
            s.Battle.EnemyHull = s.Battle.EnemyMaxHull = 10000;
            s.Battle.PlayerBlock = s.Battle.EnemyBlock = 0; s.Battle.Energy = 100;
            s.Cargo.Powder = s.Cargo.Cannonballs = s.Cargo.Wood = 100;
            return s;
        }
        private static object Invoke(CoreVoyageSession s, string method, params object[] args) => typeof(CoreVoyageSession)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, args);
        private static void Pose(CoreVoyageSession s, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
        private static void SetSchool(CoreVoyageSession s, string name, int value) => typeof(CoreSchoolBattleState)
            .GetProperty(name).SetValue(s.Battle.Schools, value);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession s, string id) { s.Battle.Hand.Clear(); s.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession s, string id, CoreNavalCardPlayOptions options = null)
        {
            Put(s, id);
            bool result = CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? s.PlayCard(0) : s.PlayNavalCard(0, options);
            Assert.That(result, Is.True, id + ": " + (s.Battle.Hand.Count > 0 ? s.CardUnavailableReason(0) : string.Empty));
        }
    }
}
