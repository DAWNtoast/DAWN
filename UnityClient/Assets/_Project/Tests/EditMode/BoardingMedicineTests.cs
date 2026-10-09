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
            Start(session);
            int index = Index(definition.Card.Id);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
            session.Battle.PlayerBoardingHealth = 1;
            typeof(CoreVoyageSession).GetProperty(nameof(CoreVoyageSession.Hull)).SetValue(session, session.MaxHull - 5);
            int hull = session.Hull;

            session.Battle.Stage = NavalBattleStage.Naval;
            Assert.That(session.PlayCard(0), Is.False, "The doctor's aid is for boarding crew, not ship repair.");
            Assert.That(session.Cargo.Medicine, Is.EqualTo(medicine));
            Assert.That(session.Hull, Is.EqualTo(hull));
            session.Battle.Stage = NavalBattleStage.Boarding;
            CoreBattleCard card = session.GetBattleCard(index);
            int expectedHealing = card.Repair + (medicine > 0 ? (card.Repair + 1) / 2 : 0);
            int energy = session.Battle.Energy;
            int expectedCost = session.GetBattleCardEnergyCost(index);
            Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(1 + expectedHealing));
            Assert.That(session.Battle.PlayerBlock, Is.EqualTo(card.Block));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy - expectedCost));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.Hull, Is.EqualTo(hull));
        }

        [TestCase(0, 4, 0)]
        [TestCase(1, 6, 1)]
        public void RealBandageUsesOptionalMedicineAfterCrewScaling(int medicine, int healed, int consumed)
        {
            CoreVoyageSession session = Battle(medicine);
            CoreBattleCardEffects quote = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            Assert.That(quote.BaseRepair, Is.EqualTo(4));
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

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(4, 4)] [TestCase(5, 5)]
        public void FullHealthAndSmallWoundsNeverWasteMedicine(int missing, int expectedHealing)
        {
            CoreVoyageSession session = Battle(2);
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth - missing;
            CoreBattleCardEffects quote = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            Assert.That(quote.Repair, Is.EqualTo(6), "The available healing ceiling is separate from this wound.");
            Assert.That(quote.ActualRepair, Is.EqualTo(expectedHealing));
            Assert.That(quote.MedicineCost, Is.EqualTo(missing > 4 ? 1 : 0));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(session.Battle.PlayerMaxBoardingHealth));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(missing > 4 ? 1 : 2));
        }

        [Test]
        public void OneBottleOnlyEnhancesFirstPlayAndDoesNotChangeRepeatFees()
        {
            CoreVoyageSession session = Battle(1, level: 3);
            session.Battle.Hand.Clear(); session.Battle.Hand.AddRange(new[] { Bandage, Bandage, Bandage });
            session.Battle.DrawPile.Clear(); session.Battle.DiscardPile.Clear();
            session.Battle.Energy = 3;
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.Zero);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(22));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(1));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(30));
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(2));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(38));
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.PlayCard(0), Is.False);
            Assert.That(session.Cargo.Medicine, Is.Zero);
        }

        [TestCase(7, 0, 2, 3)] [TestCase(10, 0, 3, 5)]
        [TestCase(14, 0, 4, 6)] [TestCase(14, 1, 8, 12)]
        [TestCase(14, 2, 8, 12)] [TestCase(14, 3, 8, 12)] [TestCase(28, 3, 16, 24)]
        public void UpgradesAndCrewSnapshotPrecedeRoundedFiftyPercentBonus(int crew, int level, int baseline, int enhanced)
        {
            CoreVoyageSession session = Battle(1, crew, level);
            CoreBattleCardEffects effect = session.GetBattleCardEffects(Bandage, NavalBattleStage.Boarding);
            Assert.That(effect.BaseRepair, Is.EqualTo(baseline));
            Assert.That(effect.Repair, Is.EqualTo(enhanced));
            Assert.That(session.GetBattleCard(Bandage).Repair, Is.EqualTo(level == 0 ? 8 : 16));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(10 + enhanced));
            Assert.That(session.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void MinimumBonusAndLargeValuesDoNotOverflow()
        {
            CoreVoyageSession session = Port(1, 7);
            var tiny = new CoreBattleCard("tiny_heal", "细小治疗", "治疗", 1, 0, 0, 1, 0, 0, "测试", NavalBattleStages.Boarding);
            CoreBattleCardEffects effect = session.GetBattleCardEffects(tiny, NavalBattleStage.Boarding);
            Assert.That(effect.BaseRepair, Is.Zero);
            Assert.That(effect.Repair, Is.EqualTo(1));
            var huge = new CoreBattleCard("huge_heal", "上限治疗", "治疗", 1, 0, 0, int.MaxValue, 0, 0, "测试", NavalBattleStages.Boarding);
            Assert.That(session.GetBattleCardEffects(huge, NavalBattleStage.Boarding).Repair, Is.GreaterThan(0));
            // A real full-strength battle snapshot verifies saturation without allowing a wrapped negative quote.
            Start(session);
            typeof(CoreBattleState).GetProperty(nameof(CoreBattleState.PlayerOrdinaryCrew)).SetValue(session.Battle, 28);
            Assert.That(session.GetBattleCardEffects(huge, NavalBattleStage.Boarding).Repair, Is.EqualTo(int.MaxValue));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
        }

        [Test]
        public void PortAndFullHealthPreviewArePureAndLabelPotentialVersusActual()
        {
            CoreVoyageSession session = Port(1, 14);
            CoreBattleCard card = session.GetBattleCard(Bandage);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Boarding);
                Assert.That(quote.IsRepairPreview, Is.True);
                Assert.That(quote.ActualRepair, Is.Zero);
                Assert.That(quote.MedicineCost, Is.Zero);
                Assert.That(session.BattleCardPortDescription(card), Does.Contain("有药潜在 6").And.Contain("无药基础 4").And.Contain("预览不消耗"));
                Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            }
            Start(session);
            Assert.That(session.BattleCardDescription(card), Does.Contain("本次实际恢复 0").And.Contain("不耗药").And.Contain("有药潜在 6"));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
        }

        [TestCase(NavalBattleStage.Preparation)] [TestCase(NavalBattleStage.Naval)]
        public void RepairingTheShipNeverBoostsOrConsumesMedicine(NavalBattleStage stage)
        {
            CoreVoyageSession session = Battle(2);
            session.Battle.Stage = stage;
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
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.EqualTo(1));
            session.Battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Balanced, 500, 1, 7, 0, 0, 20, 1f, 1f);
            int before = session.Battle.PlayerBoardingHealth;
            Assert.That(session.EndBattleTurn(), Is.True);
            Assert.That(session.Battle.PlayerBoardingHealth, Is.EqualTo(before - 7));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.Zero);
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Bandage);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.Cargo.Medicine, Is.Zero);
            Invoke(session, "EnterBattleStage", NavalBattleStage.Naval);
            Assert.That(session.GetBattleCardEnergyCost(Bandage), Is.Zero);
            Assert.That(session.Cargo.Medicine, Is.Zero);
        }

        [Test]
        public void StoredMedicineDoesNotTreatTheShipAndFullHealthMixedCardsStillResolve()
        {
            CoreVoyageSession session = Port(2, 14);
            Assert.That(session.TryDepositPortCargo("medicine", 2), Is.True);
            session.ConfigureCrewCapabilities(false, false, false, false, true, false);
            Start(session);
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
            Start(session);
            session.Battle.PlayerBoardingHealth = 10;
            return session;
        }
        private static void Start(CoreVoyageSession session)
        {
            session.SetSeed("BOARDING-MEDICINE-REGRESSION"); session.StartVoyage();
            Invoke(session, "StartBattle", new CoreEncounter("medicine_fixture", "隔离敌舰", "海战", "药品回归", true, false, 1, 500), false);
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Bandage);
            session.Battle.Energy = 10;
        }
        private static void Invoke(CoreVoyageSession session, string name, params object[] args) =>
            typeof(CoreVoyageSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, args);
    }
}
