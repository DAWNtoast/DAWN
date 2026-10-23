using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSpecialCardRedesignTests
    {
        [Test]
        public void EverySpecialIdentityIsUniqueAndAllNinetyComponentCardsStayNavalAtEveryRank()
        {
            Assert.That(CoreSpecialCardCatalog.All.Count, Is.EqualTo(126));
            Assert.That(CoreSpecialCardCatalog.All.Count(c => c.SourceKind == CoreSpecialCardSourceKind.Crew), Is.EqualTo(36));
            Assert.That(CoreSpecialCardCatalog.All.Count(c => c.SourceKind == CoreSpecialCardSourceKind.Component), Is.EqualTo(90));
            Assert.That(CoreSpecialCardCatalog.All.Select(c => c.Card.Id).Distinct().Count(), Is.EqualTo(126));
            Assert.That(CoreSpecialCardCatalog.All.Select(c => c.SourceId).Distinct().Count(), Is.EqualTo(126));
            var session = new CoreVoyageSession();
            foreach (CoreBattleCard component in CoreVoyageSession.CardLibrary.Where(c => c.RequiredComponent.Length > 0))
                Assert.That(component.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), component.Id);
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                bool crew = definition.SourceKind == CoreSpecialCardSourceKind.Crew;
                int index = Index(definition.Card.Id);
                Assert.That(CoreVoyageSession.CardLibrary[index], Is.SameAs(definition.Card), definition.Card.Id);
                if (!crew) Assert.That(CoreBoardingCardCatalog.ForBoarding(definition.Card), Is.SameAs(definition.Card), definition.Card.Id);
                for (int rank = 0; rank <= 3; rank++)
                {
                    CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                    Assert.That(card.Id, Is.EqualTo(definition.Card.Id));
                    Assert.That(crew ? card.RequiredCrew : card.RequiredComponent, Is.EqualTo(definition.SourceId), card.Id);
                    Assert.That(CoreBattleCardNumbering.Number(card) / 1000, Is.EqualTo(crew ? 8 : 9), card.Id);
                    Assert.That(card.Description, Is.Not.Empty, card.Id);
                    StringAssert.DoesNotContain("{", card.Description, card.Id);
                    if (!crew) Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), card.Id + " rank " + rank);
                }
            }
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void EachLegendaryInstrumentDrawsAndShapesFieldsWithoutInstallingADoctrine(string school)
        {
            CoreBattleCard card = Mastery(school);
            Assert.That(card.RequiredComponent.StartsWith("instrument_", StringComparison.Ordinal), Is.True, card.Id);
            Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval));
            Assert.That(card.Energy, Is.EqualTo(2));
            Assert.That(card.Draw, Is.EqualTo(1));
            Assert.That(card.Damage + card.Block + card.Repair + card.PowderCost + card.CannonballCost + CoreSpecialCardRules.HealthCost(card), Is.Zero);
            CoreVoyageSession session = Start();
            int hull = session.Hull, powder = session.Cargo.Powder, shells = session.Cargo.Cannonballs;
            Play(session, card.Id);
            Assert.That(session.Battle.Energy, Is.EqualTo(98));
            Assert.That(CoreSpecialCardRules.IsDoctrine(card), Is.False);
            Assert.That(card.SchoolEffect.Mechanic, Is.EqualTo(CoreSchoolMechanic.ComponentFieldMastery));
            Assert.That(session.Battle.SpecialCards.IsActive(school), Is.False);
            Assert.That(session.Hull, Is.EqualTo(hull));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powder));
            Assert.That(session.Cargo.Cannonballs, Is.EqualTo(shells));
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1), "A field mastery card still draws when no matching field exists.");
            Assert.That(new[] { session.Battle.EnemyBurnTurns, session.Battle.Schools.Mana,
                session.Battle.Schools.CurseMarks, session.Battle.Schools.TidePower, session.Battle.Schools.ThunderChain },
                Is.EqualTo(new[] { 0, 0, 0, 0, 0 }), "The retired first-card rewards are not installed.");
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty, "Field mastery expands existing fields instead of creating one from nothing.");
        }

        [Test]
        public void SixNavigatorsHaveDistinctActualEnergyGuardDrawAndActionPointBenefits()
        {
            CoreSpecialCardDefinition[] navigators = CoreSpecialCardCatalog.All
                .Where(c => c.SourceKind == CoreSpecialCardSourceKind.Crew && c.SourceId.Contains("_navigator_")).ToArray();
            Assert.That(navigators.Length, Is.EqualTo(6));
            string[] signatures = navigators.Select(definition =>
            {
                CoreVoyageSession session = Start();
                int energy = session.Battle.Energy, block = session.Battle.PlayerBlock, points = session.Battle.NavalActionPoints;
                Play(session, definition.Card.Id);
                return string.Join("|", new[] { energy - session.Battle.Energy, session.Battle.PlayerBlock - block,
                    session.Battle.Hand.Count, session.Battle.NavalActionPoints - points });
            }).ToArray();
            Assert.That(signatures.Distinct().Count(), Is.EqualTo(6),
                "Legacy movement action names cannot count as distinct when their current effect is the same +1 action point.");
        }

        [TestCase("card_school_blood_b03", 3)]
        [TestCase("card_school_blood_b05", 5)]
        [TestCase("card_school_blood_n01", 4)]
        [TestCase("card_school_blood_n02", 3)]
        [TestCase("card_school_blood_n03", 5)]
        public void Legacy_BloodDoctrineAllowsFirstPaidCardAtOneHullAcrossBothCostPaths(string id, int originalCost)
        {
            CoreVoyageSession session = Start();
            InstallLegacyDoctrine(session, "blood"); SetHull(session, 1);
            CoreBattleCard card = session.GetBattleCard(Index(id));
            Assert.That(CoreSpecialCardRules.HealthCost(card), Is.EqualTo(originalCost));
            Assert.That(CoreSpecialCardRules.HealthCost(card, session.Battle), Is.Zero);
            Play(session, id, FieldOptions());
            Assert.That(session.Hull, Is.EqualTo(1));
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.True);
            Assert.That(CoreSpecialCardRules.HealthCost(card, session.Battle), Is.EqualTo(originalCost));
            Put(session, "card_school_blood_b05");
            Assert.That(session.PlayCard(0), Is.False, "The second Blood card must pay life and cannot be played at one hull.");
            Assert.That(session.Battle.Hand.Count, Is.EqualTo(1));
            Assert.That(session.Hull, Is.EqualTo(1));
        }

        [Test]
        public void Legacy_BloodDoctrineSecondCardPaysAndRepeatedInstallationCannotResetTheBudget()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood");
            int hull = session.Hull;
            Play(session, "card_school_blood_b05");
            Assert.That(session.Hull, Is.EqualTo(hull));
            InstallLegacyDoctrine(session, "blood");
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.True);
            Play(session, "card_school_blood_n02");
            Assert.That(session.Hull, Is.EqualTo(hull - 3));
            Assert.That(session.Battle.SpecialCards.IsActive("blood"), Is.True);
        }

        [Test]
        public void Legacy_PlayingBeforeInstallationConsumesThisTurnsEligibilityAndNextTurnRefreshesIt()
        {
            CoreVoyageSession session = Start(); int hull = session.Hull;
            Play(session, "card_school_blood_b05");
            Assert.That(session.Hull, Is.EqualTo(hull - 5));
            InstallLegacyDoctrine(session, "blood");
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.True);
            Assert.That(CoreSpecialCardRules.HealthCost(session.GetBattleCard(Index("card_school_blood_b05")), session.Battle), Is.EqualTo(5));
            session.Battle.PlayerBlock = 1000;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(session.Battle.SpecialCards.IsActive("blood"), Is.True);
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.False);
            SetHull(session, 1); session.Battle.Energy = 100;
            Play(session, "card_school_blood_b05");
            Assert.That(session.Hull, Is.EqualTo(1));
        }

        [Test]
        public void Legacy_FailedResourcesCancelledChoicesAndInvalidTargetsDoNotSpendBloodPrivilege()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood"); SetHull(session, 1);
            int energy = session.Battle.Energy;
            Put(session, "card_school_blood_n01");
            Assert.That(session.PlayNavalCard(0, null), Is.False, "Cancelling the field selector is not a successful play.");
            Assert.That(session.PlayNavalCard(0, new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 30 }), Is.False);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.False);
            Put(session, "card_school_blood_b03"); session.Cargo.Cannonballs = 0;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.False);
            session.Cargo.Cannonballs = 100;
            Play(session, "card_school_blood_b03");
            Assert.That(session.Hull, Is.EqualTo(1));
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.True);
        }

        [Test]
        public void Legacy_ZeroLifeCostCardStillConsumesTheFirstBloodCardPrivilege()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood");
            Play(session, "card_school_blood_b04");
            Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.True);
            Assert.That(CoreSpecialCardRules.HealthCost(session.GetBattleCard(Index("card_school_blood_b05")), session.Battle), Is.EqualTo(5));
        }

        [Test]
        public void Legacy_LiveBloodQuotesKeepCostsInIconsAndNeverConsumeThePrivilege()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood"); SetHull(session, 1);
            int energy = session.Battle.Energy;
            foreach (string id in new[] { "card_school_blood_b03", "card_school_blood_b05", "card_school_blood_n01", "card_school_blood_n02", "card_school_blood_n03" })
            {
                CoreBattleCard card = session.GetBattleCard(Index(id));
                string first = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
                Assert.That(first, Does.Not.Match(@"支付\s*\d").And.Not.Contain("（血契特权）"), id);
                Assert.That(session.BattleCardFaceDescription(card, NavalBattleStage.Naval), Is.EqualTo(first));
                Assert.That(CoreSpecialCardRules.HealthCost(card, session.Battle), Is.Zero);
                if (CoreNavalSchoolExpansionCatalog.FindSpec(id) != null)
                    Assert.That(CoreBattleCardText.FormatEffects(CoreSpecialCardRules.CostDescription(card.DescriptionTemplate, card, session.Battle)),
                        Does.Contain("支付 0 点船体（血契特权）"), "Raw payment templates still support non-face quotes.");
                Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.False, id);
            }
            Assert.That(session.Hull, Is.EqualTo(1)); Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Play(session, "card_school_blood_b05");
            CoreBattleCard field = session.GetBattleCard(Index("card_school_blood_n01"));
            Assert.That(session.BattleCardFaceDescription(field, NavalBattleStage.Naval), Does.Not.Match(@"支付\s*\d"));
            Assert.That(CoreSpecialCardRules.HealthCost(field, session.Battle), Is.EqualTo(4));
            Assert.That(CoreSpecialCardRules.CostDescription(field.Description, field, session.Battle), Is.EqualTo(field.Description));
        }

        [Test]
        public void Legacy_FreeBloodPrivilegeNeverRewritesExecuteHealthPercentageAsALifeCost()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood");
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary.Where(c => c.SchoolEffect?.Mechanic == CoreSchoolMechanic.BloodExecute &&
                c.IsAllowedInStage(NavalBattleStage.Naval)))
            {
                string face = session.BattleCardFaceDescription(card, NavalBattleStage.Naval);
                StringAssert.Contains(card.SchoolEffect.Threshold + "%", face, card.Id);
                StringAssert.DoesNotContain("0%", face.Replace(card.SchoolEffect.Threshold + "%", "threshold"), card.Id);
                Assert.That(CoreSpecialCardRules.HealthCost(card, session.Battle), Is.Zero);
                Assert.That(session.Battle.SpecialCards.HasPlayed("blood"), Is.False);
            }
        }

        [Test]
        public void Legacy_FireDoctrineIgnitesAfterFirstUtilityCardOnlyAndNeverStacksFromReinstallation()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "fire"); InstallLegacyDoctrine(session, "fire");
            Assert.That(session.Battle.EnemyBurnTurns, Is.Zero);
            Play(session, "card_school_fire_n04");
            Assert.That(session.Battle.EnemyBurnDamage, Is.EqualTo(3));
            Assert.That(session.Battle.EnemyBurnTurns, Is.EqualTo(3));
            session.Battle.EnemyBurnDamage = session.Battle.EnemyBurnTurns = 0;
            Play(session, "card_school_fire_n04");
            Assert.That(session.Battle.EnemyBurnTurns, Is.Zero);
        }

        [Test]
        public void Legacy_ArcaneDoctrineRefundsOnePermanentManaOnlyAfterSuccessfulPayment()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "arcane");
            Put(session, "card_school_arcane_b03");
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Battle.SpecialCards.HasPlayed("arcane"), Is.False);
            SetSchool(session, "Mana", 2);
            Play(session, "card_school_arcane_b03");
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(2));
            Play(session, "card_school_arcane_b03");
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(1));
            CoreVoyageSession capped = Start(); InstallLegacyDoctrine(capped, "arcane"); SetSchool(capped, "Mana", 3);
            Play(capped, "card_school_arcane_n01", FieldOptions());
            Assert.That(capped.Battle.Schools.Mana, Is.EqualTo(3), "The refund respects the existing mana capacity.");
        }

        [Test]
        public void Legacy_CurseDoctrineAddsOneDecayingStackAndHonorsItsOwnSixMarkCap()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "curse");
            Play(session, "card_school_curse_n01", FieldOptions());
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(1));
            Assert.That(session.Battle.Schools.CurseTurns, Is.EqualTo(1));
            Assert.That(session.Battle.Schools.CurseDamagePerMark, Is.Zero);
            Play(session, "card_school_curse_n01", FieldOptions());
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(1));
            CoreVoyageSession capped = Start(); InstallLegacyDoctrine(capped, "curse");
            SetSchool(capped, "CurseMarks", 6); SetSchool(capped, "CurseTurns", 6);
            SetSchool(capped, "CurseNextTickBonus", 2);
            Play(capped, "card_school_curse_n01", FieldOptions());
            Assert.That(capped.Battle.Schools.CurseMarks, Is.EqualTo(6));
            Assert.That(capped.Battle.Schools.CurseTurns, Is.EqualTo(6), "The lifetime mirrors the stack count.");
            Assert.That(capped.Battle.Schools.CurseNextTickBonus, Is.EqualTo(2), "Adding a stack does not consume or multiply a separate damage bonus.");
        }

        [Test]
        public void Legacy_TideDoctrineGrantsThreeTideOnlyOnceAndClampsToTwenty()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "tide");
            Play(session, "card_school_tide_b04");
            Assert.That(session.Battle.Schools.TidePower, Is.EqualTo(3));
            Play(session, "card_school_tide_b04");
            Assert.That(session.Battle.Schools.TidePower, Is.EqualTo(3));
            CoreVoyageSession capped = Start(); InstallLegacyDoctrine(capped, "tide"); SetSchool(capped, "TidePower", 19);
            Play(capped, "card_school_tide_b04");
            Assert.That(capped.Battle.Schools.TidePower, Is.EqualTo(20));
        }

        [Test]
        public void Legacy_ThunderDoctrineIgnoresUtilityAndAwardsOnlyOneExtraChainOnFirstAttack()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "thunder");
            Play(session, "card_school_thunder_n05");
            Assert.That(session.Battle.SpecialCards.HasPlayed("thunder"), Is.False);
            Assert.That(session.Battle.Schools.ThunderChain, Is.Zero);
            Play(session, "card_school_thunder_b03");
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(2));
            Play(session, "card_school_thunder_n02");
            Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(3));
            CoreVoyageSession capped = Start(); InstallLegacyDoctrine(capped, "thunder"); SetSchool(capped, "ThunderChain", 5);
            Play(capped, "card_school_thunder_b03");
            Assert.That(capped.Battle.Schools.ThunderChain, Is.EqualTo(6));
        }

        [Test]
        public void Legacy_EnteringBoardingClearsAllDoctrinesAndRejectsTheirComponentCards()
        {
            CoreVoyageSession session = Start();
            foreach (string school in Schools) InstallLegacyDoctrine(session, school);
            Play(session, "card_school_blood_b05");
            typeof(CoreVoyageSession).GetMethod("EnterBattleStage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, new object[] { NavalBattleStage.Boarding });
            foreach (string school in Schools)
            {
                Assert.That(session.Battle.SpecialCards.IsActive(school), Is.False, school);
                Assert.That(session.Battle.SpecialCards.HasPlayed(school), Is.False, school);
                Put(session, Mastery(school).Id);
                Assert.That(session.PlayCard(0), Is.False, school);
            }
        }

        [Test]
        public void Legacy_BattleVictoryClearsInstalledDoctrinesAndSuccessfulUseHistory()
        {
            CoreVoyageSession session = Start(); InstallLegacyDoctrine(session, "blood");
            CoreBattleState battle = session.Battle;
            battle.EnemyHull = 1;
            Play(session, "card_school_blood_b03");
            Assert.That(session.TrainingOutcome, Is.EqualTo(CoreTrainingBattleOutcome.Victory));
            Assert.That(battle.SpecialCards.IsActive("blood"), Is.False);
            Assert.That(battle.SpecialCards.HasPlayed("blood"), Is.False);
        }

        private static readonly string[] Schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
        private static CoreBattleCard Mastery(string school) => CoreSpecialCardCatalog.All.Select(c => c.Card)
            .Single(c => c.SchoolEffect?.Mechanic == CoreSchoolMechanic.ComponentFieldMastery && c.SchoolId == school);
        // Keep coverage of the retired state for compatibility without treating current instruments as doctrines.
        private static void InstallLegacyDoctrine(CoreVoyageSession session, string school) => typeof(CoreSpecialCardBattleState)
            .GetMethod("Activate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session.Battle.SpecialCards, new object[] { school });
        private static CoreNavalCardPlayOptions FieldOptions() => new CoreNavalCardPlayOptions { HasAnchor = true, AnchorX = 0, AnchorY = 0, DirectionX = 1 };
        private static CoreVoyageSession Start()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            session.ConfigureSpecialCardSources(CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Crew).Select(c => c.SourceId).ToArray(),
                CoreSpecialCardCatalog.All.Where(c => c.SourceKind == CoreSpecialCardSourceKind.Component).Select(c => c.SourceId).ToArray());
            session.SetSeed("SPECIAL-CARD-REDESIGN");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            SetHull(session, session.MaxHull); session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0; session.Battle.Energy = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = 100;
            Pose(session, true, 0, 0); Pose(session, false, 2, 0);
            return session;
        }
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
        private static void Put(CoreVoyageSession session, string id) { session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id)); }
        private static void Play(CoreVoyageSession session, string id, CoreNavalCardPlayOptions options = null)
        {
            Put(session, id); string reason = session.CardUnavailableReason(0);
            bool played = CoreNavalSchoolExpansionCatalog.FindSpec(id) == null ? session.PlayCard(0) : session.PlayNavalCard(0, options);
            Assert.That(played, Is.True, id + ": " + reason);
        }
        private static void SetHull(CoreVoyageSession session, int value) => typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, value);
        private static void SetSchool(CoreVoyageSession session, string name, int value) => typeof(CoreSchoolBattleState).GetProperty(name).SetValue(session.Battle.Schools, value);
        private static void Pose(CoreVoyageSession session, bool player, int x, int y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session.Battle,
                new object[] { player, new VoyagePoint(x * 100, y * 100), player ? 0f : 180f });
    }
}
