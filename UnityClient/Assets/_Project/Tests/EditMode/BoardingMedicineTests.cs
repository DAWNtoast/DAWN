using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingMedicineTests
    {
        private static IEnumerable<TestCaseData> DoctorHealingCases()
        {
            foreach (CoreSpecialCardDefinition doctor in CoreSpecialCardCatalog.All.Where(value => value.SourceId.Contains("_doctor_")))
                foreach (int experience in new[] { 0, 840 })
                    foreach (int medicine in new[] { 0, 1 })
                        yield return new TestCaseData(doctor.SourceId, experience, medicine);
        }

        [TestCaseSource(nameof(DoctorHealingCases))]
        public void PersonalDoctorCardsHealBoardingCrewAndRespectMedicineAndStage(string crewId, int experience, int medicine)
        {
            CoreSpecialCardDefinition definition = CoreSpecialCardCatalog.ForCrew(crewId);
            CoreVoyageSession session = Port(medicine, 28);
            session.ConfigureCrewCapabilities(false, false, true, false, false, false);
            session.ConfigureSpecialCardSources(new[] { crewId }, null);
            session.RestoreSpecialCardExperience(new Dictionary<string, int> { { definition.Card.Id, experience } });
            Start(ref session);
            Invoke(session, "BInitialize");
            int index = Index(definition.Card.Id);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
            session.Battle.PlayerBoardingHealth = 1;
            CoreBattleCard card = session.GetBattleCard(index);
            string key = card.BoardingSpec?.Key ?? string.Empty;
            bool delayed = key == "basic_07" || key == "tide_12";
            bool arcane = key == "arcane_05", tide = key == "tide_12", blood = key == "blood_09";
            int energy = session.Battle.Energy, hull = session.Hull;
            session.Battle.Stage = NavalBattleStage.Naval;
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            session.Battle.Stage = NavalBattleStage.Boarding;
            if (medicine > 0 && (arcane || tide))
            {
                Assert.That(session.PlayCard(0), Is.False, "Full treatment must pay its fixed school resource.");
                Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            }
            if (medicine > 0 && arcane)
                typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 2);
            if (medicine > 0 && tide)
            {
                Invoke(session, "CTBAddTideGuard", 8);
                session.Battle.PlayerBlock -= 8;
                Invoke(session, "CTBAfterEnemyAttack", 8, 0);
            }
            int full = blood ? new[] { 9, 12, 15 }[card.UpgradeLevel] : card.Repair;
            int fallback = blood ? 3 : 2;
            int heal = medicine > 0 ? full : fallback;
            CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
            Assert.That(quote.FullRepair, Is.EqualTo(full));
            Assert.That(quote.FallbackRepair, Is.EqualTo(fallback));
            Assert.That(quote.Repair, Is.EqualTo(heal));
            Assert.That(quote.MedicineCost, Is.EqualTo(medicine));
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(delayed ? 1 : 1 + heal));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - card.Energy));
            Assert.That(session.Battle.Schools.AvailableMana, Is.Zero);
            Assert.That(session.Battle.Boarding.TidePower, Is.Zero);
            if (delayed)
            {
                // Current treatment adds a decaying recovery pool: each player end
                // heals its remaining layers, then consumes exactly one layer.
                int health = 1;
                Assert.That(session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(heal));
                for (int remaining = heal; remaining > 0; remaining--)
                {
                    health = Math.Min(session.Battle.PlayerMaxBoardingHealth, health + remaining);
                    Invoke(session, "BPlayerEnd");
                    Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health));
                    Assert.That(session.Battle.Boarding.ContinuousRecovery, Is.EqualTo(remaining - 1));
                    Assert.That(session.Cargo.Medicine, Is.Zero, "Recovery pays medicine only when the card is played.");
                }
                Invoke(session, "BPlayerEnd");
                Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(health), "An exhausted recovery pool cannot heal again.");
            }
            Assert.That(session.Hull, Is.EqualTo(hull));
        }
        [TestCase(0)]
        [TestCase(1)]
        public void RealBandageNeedsMedicineForFullAuthoredHealing(int medicine)
        {
            CoreVoyageSession session = Battle(medicine);
            int baseline = AuthoredBandage(session);
            int healed = medicine > 0 ? baseline : 2;
            int consumed = medicine > 0 ? 1 : 0;
            CoreBattleCardEffects quote = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            Assert.That(quote.FullRepair, Is.EqualTo(baseline));
            Assert.That(quote.FallbackRepair, Is.EqualTo(2));
            Assert.That(quote.Repair, Is.EqualTo(healed));
            Assert.That(quote.ActualRepair, Is.EqualTo(healed));
            Assert.That(quote.MedicineCost, Is.EqualTo(consumed));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine), "Quoting is read-only.");
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + healed));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine - consumed));
            Assert.That(session.Battle.LastAction, Does.Contain("恢复 " + healed));
            Assert.That(session.Battle.LastAction.Contains("药品 -1"), Is.EqualTo(consumed == 1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(7)]
        public void FullHealthAndFallbackSizedWoundsNeverWasteMedicine(int missing)
        {
            CoreVoyageSession session = Battle(2);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - missing;
            int baseline = AuthoredBandage(session);
            CoreBattleCardEffects quote = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            int expectedHealing = Math.Min(missing, baseline);
            Assert.That(quote.Repair, Is.EqualTo(baseline), "The available healing ceiling is separate from this wound.");
            Assert.That(quote.ActualRepair, Is.EqualTo(expectedHealing));
            Assert.That(quote.MedicineCost, Is.EqualTo(missing > 2 ? 1 : 0));
            Assert.That(session.PlayCard(0), Is.EqualTo(missing > 0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth - missing + expectedHealing));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(missing > 2 ? 1 : 2));
        }

        [Test]
        public void OneBottleOnlyEnhancesFirstPlayAndDoesNotChangeRepeatFees()
        {
            CoreVoyageSession session = Battle(1, level: 3);
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { Bandage, Bandage, Bandage });
            session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            session.Battle.Energy = 6;
            int baseline = AuthoredBandage(session);
            int expectedHealth = Math.Min(session.Battle.PlayerMaxBoardingHealth, 10 + baseline);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(1));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(2));
            Assert.That(session.PlayCard(0), Is.True);
            expectedHealth = Math.Min(session.Battle.PlayerMaxBoardingHealth, expectedHealth + 2);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(3));
            Assert.That(session.PlayCard(0), Is.True);
            expectedHealth = Math.Min(session.Battle.PlayerMaxBoardingHealth, expectedHealth + 2);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(expectedHealth));
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Cargo.Medicine, Is.Zero);
        }

        [TestCase(7, 0)] [TestCase(10, 0)]
        [TestCase(14, 0)] [TestCase(14, 1)]
        [TestCase(14, 2)] [TestCase(14, 3)] [TestCase(28, 3)]
        public void UpgradesDetermineFullHealingIndependentlyOfCrewWhileFallbackStaysFixed(int crew, int level)
        {
            CoreVoyageSession session = Battle(1, crew, level);
            int baseline = AuthoredBandage(session);
            int enhanced = baseline;
            CoreBattleCardEffects effect = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            Assert.That(effect.FullRepair, Is.EqualTo(baseline));
            Assert.That(effect.FallbackRepair, Is.EqualTo(2));
            Assert.That(effect.Repair, Is.EqualTo(enhanced));
            int expectedMedicineCost = baseline > 2 ? 1 : 0;
            Assert.That(effect.MedicineCost, Is.EqualTo(expectedMedicineCost), "Medicine is paid only when full healing exceeds the fixed fallback.");
            Assert.That(session.GetBattleCard(Bandage).Repair, Is.EqualTo(new[] { 6, 8, 11, 11 }[level]));
            Assert.That(session.GetBattleCard(Bandage).UpgradeLevel, Is.EqualTo(Math.Min(2, level)));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(Math.Min(session.Battle.PlayerMaxBoardingHealth, 10 + enhanced)));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1 - expectedMedicineCost));
        }

        [Test]
        public void TinyHealsNeverGrowToFallbackAndLargeValuesDoNotOverflow()
        {
            CoreVoyageSession session = Port(1, 7);
            var tiny = new CoreBattleCard("tiny_heal", "细小治疗", "治疗", 1, 0, 0, 1, 0, 0, "测试", NavalBattleStages.Boarding);
            CoreBattleCardEffects effect = session.GetBattleCardEffects(tiny, NavalBattleStage.Boarding);
            Assert.That(effect.BaseRepair, Is.EqualTo(1));
            Assert.That(effect.Repair, Is.EqualTo(1), "A small authored heal is not lost to crew scaling or raised to the fallback cap.");
            Assert.That(effect.HasMedicineBoost, Is.False);
            Assert.That(effect.MedicineCost, Is.Zero);
            var minimum = new CoreBattleCard("minimum_heal", "最小治疗", "治疗", 1, 0, 0, 2, 0, 0, "测试", NavalBattleStages.Boarding);
            CoreBattleCardEffects minimumEffect = session.GetBattleCardEffects(minimum, NavalBattleStage.Boarding);
            Assert.That(minimumEffect.BaseRepair, Is.EqualTo(2));
            Assert.That(minimumEffect.Repair, Is.EqualTo(2), "Supply restores the complete value and does not invent a bonus.");
            Assert.That(minimumEffect.HasMedicineBoost, Is.False);
            var huge = new CoreBattleCard("huge_heal", "上限治疗", "治疗", 1, 0, 0, int.MaxValue, 0, 0, "测试", NavalBattleStages.Boarding);
            Assert.That(session.GetBattleCardEffects(huge, NavalBattleStage.Boarding).Repair, Is.GreaterThan(0));
            // A real battle snapshot verifies saturation without allowing a wrapped negative quote.
            Start(ref session);
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew)).SetValue(session.Battle, 28);
            CoreBattleCardEffects fullHealth = session.GetBattleCardEffects(huge, NavalBattleStage.Boarding);
            Assert.That(fullHealth.FullRepair, Is.EqualTo(int.MaxValue));
            Assert.That(fullHealth.FallbackRepair, Is.EqualTo(2));
            Assert.That(fullHealth.Repair, Is.EqualTo(int.MaxValue));
            Assert.That(fullHealth.ActualRepair, Is.Zero);
            Assert.That(fullHealth.MedicineCost, Is.Zero);
            session.Battle.PlayerBoardingHealth -= 5;
            CoreBattleCardEffects wounded = session.GetBattleCardEffects(huge, NavalBattleStage.Boarding);
            Assert.That(wounded.Repair, Is.EqualTo(int.MaxValue));
            Assert.That(wounded.ActualRepair, Is.EqualTo(5));
            Assert.That(wounded.MedicineCost, Is.EqualTo(1), "A five-point wound exceeds the fixed fallback even for a very large card.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
        }

        [TestCase(7)] [TestCase(14)] [TestCase(28)] [TestCase(56)]
        public void NoMedicineFallback_RemainsTwoFinalHealthAcrossEveryRankAndCrewCount(int crew)
        {
            CoreVoyageSession session = Battle(0, crew);
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew)).SetValue(session.Battle, crew);
            for (int rank = 0; rank <= 3; rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(Bandage, rank);
                CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                int expectedFull = new[] { 6, 8, 11, 11 }[rank];
                Assert.That(quote.FullRepair, Is.EqualTo(expectedFull));
                Assert.That(card.Repair, Is.EqualTo(expectedFull));
                Assert.That(quote.Repair, Is.EqualTo(2));
                Assert.That(quote.ActualRepair, Is.EqualTo(2));
                Assert.That(quote.MedicineCost, Is.Zero);
            }
        }

        [Test]
        public void PortAndFullHealthPreviewArePureAndLabelPotentialVersusActual()
        {
            CoreVoyageSession session = Port(1, 14);
            CoreBattleCard card = session.GetBattleCard(Bandage);
            int enhanced = AuthoredBandage(session);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                Assert.That(quote.IsRepairPreview, Is.True);
                Assert.That(quote.ActualRepair, Is.Zero);
                Assert.That(quote.MedicineCost, Is.Zero);
                Assert.That(session.BattleCardPortDescription(card), Is.EqualTo(card.Description));
                Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            }
            Start(ref session);
            Assert.That(session.BattleCardDescription(card), Does.Contain("恢复 2 点生命值").And.Contain("消耗 1 药物额外恢复 " + (enhanced - 2) + " 点生命值"));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
        }

        [TestCase(NavalBattleStage.Preparation)] [TestCase(NavalBattleStage.Naval)]
        public void RepairingTheShipNeverBoostsOrConsumesMedicine(NavalBattleStage stage)
        {
            CoreVoyageSession session = Battle(2);
            session.Battle.Stage = stage;
            session.Cargo.Wood = 1;
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(session, session.MaxHull - 30);
            int repair = Index("card_emergency_repair");
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(repair);
            int before = session.Hull;
            CoreBattleCardEffects effect = session.GetBattleCardEffects(repair, stage);
            Assert.That(effect.Repair, Is.EqualTo(session.GetBattleCard(repair).Repair));
            Assert.That(effect.HasMedicineBoost, Is.False);
            Assert.That(effect.MedicineCost, Is.Zero);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Hull, Is.EqualTo(before + effect.Repair));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(2));
        }

        [TestCase("energy")] [TestCase("stage")] [TestCase("missing_hand")] [TestCase("missing_crew")]
        public void UnplayableCardsNeverConsumeMedicineOrHeal(string failure)
        {
            CoreVoyageSession session = Battle(1);
            int slot = 0;
            if (failure == "energy") session.Battle.Energy = 0;
            if (failure == "stage") session.Battle.Stage = NavalBattleStage.Naval;
            if (failure == "missing_hand") slot = 100;
            if (failure == "missing_crew")
            {
                session.Battle.Hand[0] = Index("card_doctor_sutures");
            }
            Assert.That(session.CanPlayCard(slot), Is.False);
            Assert.That(session.PlayCard(slot), Is.False);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
        }

        [Test]
        public void EnemyTurnAndStageChangesNeverConsumeMedicineAndResetFeesNormally()
        {
            CoreVoyageSession session = Battle(2, level: 2);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(2));
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 500, 1, 7, 0, 0, 20, 1f, 1f);
            int before = session.Battle.PlayerBoardingHealth;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(before - 7));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(1));
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Bandage);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Naval);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(1));
            Assert.That(session.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void StoredMedicineDoesNotTreatTheShipAndFullHealthMixedCardsStillResolve()
        {
            CoreVoyageSession session = Port(2, 14);
            Assert.That(session.TryDepositPortCargo("medicine", 2), Is.True);
            session.ConfigureCrewCapabilities(false, false, false, false, true, false);
            Start(ref session);
            int mixed = Index("card_cook_rations");
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(mixed);
            CoreBattleCardEffects effects = session.GetBattleCardEffects(mixed, NavalBattleStage.Boarding);
            Assert.That(effects.Block, Is.GreaterThan(0));
            Assert.That(effects.HasMedicineBoost, Is.False);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(effects.Block));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.PortWarehouse.GetAmount(session.CurrentPort, "medicine"), Is.EqualTo(2));
        }

        private static int Bandage => Index("card_crew_bandage");
        private static int AuthoredBandage(CoreVoyageSession session) => session.GetBattleCard(Bandage).Repair;
        private static int Index(string id) => CoreVoyageSession.CardLibrary.ToList().FindIndex(card => card.Id == id);
        private static CoreVoyageSession Port(int medicine, int crew, int level = 0)
        {
            var session = new CoreVoyageSession();
            Assert.That(session.TryDepositAllPortCargo(), Is.True);
            Assert.That(session.PortWarehouse.Restore("v2"), Is.True);
            Assert.That(session.SelectShip(7), Is.True);
            session.Cargo.Medicine = medicine;
            session.ConfigureDepartureCrew(crew, 0);
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { "card_crew_bandage", level } });
            return session;
        }
        private static CoreVoyageSession Battle(int medicine, int crew = 14, int level = 0)
        {
            CoreVoyageSession session = Port(medicine, crew, level);
            Start(ref session);
            session.Battle.PlayerBoardingHealth = 10;
            return session;
        }
        private static void Start(ref CoreVoyageSession session)
        {
            session.SetSeed("BOARDING-MEDICINE-REGRESSION");
            if (!session.HasEnoughOrdinaryCrewForDeparture)
            {
                // Preserve low-crew legacy voyage coverage; new departures require 40% staffing.
                int ordinary = session.SelectedOrdinaryCrew;
                session.ConfigureDepartureCrew(session.MinimumDepartureOrdinaryCrew, session.SelectedSpecialCrewCount);
                session.StartVoyage();
                CoreVoyageCheckpoint checkpoint = session.CaptureVoyageCheckpoint();
                checkpoint.ordinaryCrew = ordinary;
                session = new CoreVoyageSession();
                Assert.That(session.TryRestoreVoyageCheckpoint(checkpoint, out string reason), Is.True, reason);
            }
            else session.StartVoyage();
            Invoke(session, "StartBattle", new CoreEncounter("medicine_fixture", "隔离敌舰", "海战", "药品回归", true, false, 1, 500), false);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Bandage);
            session.Battle.Energy = 10;
        }
        private static void Invoke(CoreVoyageSession session, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, args);
    }
}
