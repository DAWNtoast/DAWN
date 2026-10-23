using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreNavalAttackRangeTests
    {
        [Test]
        public void CannonPreviewContainsFortyCellsAndMatchesActualRangeAcrossTranslatedGrid()
        {
            CoreVoyageSession session = NewBattle();
            VoyagePoint origin = new VoyagePoint(-700f, 1100f);
            SetPlayerPosition(session, origin);
            CoreBattleCard cannon = Card("card_chain_shot");
            CoreNavalAttackRange range = session.GetNavalCardAttackRange(cannon);
            Assert.That(range.Kind, Is.EqualTo(CoreNavalAttackRangeKind.CannonCells));
            Assert.That(range.ScanRadiusCells, Is.EqualTo(4));
            int highlighted = 0;
            for (int x = -5; x <= 5; x++)
                for (int y = -5; y <= 5; y++)
                {
                    VoyagePoint cell = new VoyagePoint(origin.X + x * 100f, origin.Y + y * 100f);
                    bool expected = (x != 0 || y != 0) && CoreVoyageSession.NavalCannonInRange(origin, cell);
                    Assert.That(session.NavalCardCanTargetCell(cannon, cell), Is.EqualTo(expected), x + "," + y);
                    if (expected) highlighted++;
                }
            Assert.That(highlighted, Is.EqualTo(40));
            Assert.That(session.NavalCardCanTargetCell(cannon, new VoyagePoint(origin.X + 300f, origin.Y + 200f)), Is.False);
        }

        [TestCase("card_diver_sabotage", 100f, 100f, true)]
        [TestCase("card_diver_sabotage", 200f, 0f, true)]
        [TestCase("card_diver_sabotage", 200f, 100f, false)]
        [TestCase("card_school_arcane_b03", 300f, 100f, true)]
        [TestCase("card_school_arcane_b03", 300f, 200f, false)]
        [TestCase("card_school_arcane_b03", 400f, 0f, true)]
        public void NonCannonPreviewUsesAuthoredGridRange(string id, float x, float y, bool expected)
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleCard card = Card(id);
            CoreNavalAttackRange range = session.GetNavalCardAttackRange(card);
            Assert.That(range.Kind, Is.EqualTo(CoreNavalAttackRangeKind.CannonCells));
            Assert.That(range.ScanRadiusCells, Is.EqualTo(CoreNavalCardRangeRules.MaximumCells(card)));
            Assert.That(session.NavalCardCanTargetCell(card, new VoyagePoint(x, y)), Is.EqualTo(expected));
        }

        [TestCase("card_load_shot")]
        [TestCase("card_gunner_precision")]
        [TestCase("card_evasive_maneuver")]
        [TestCase("card_school_blood_b02")]
        [TestCase("card_school_fire_b01")]
        [TestCase("card_school_thunder_b01")]
        [TestCase("card_special_sail_starshear_rig")]
        public void DefensiveSelfDamageAndFutureAttackBuffsDoNotHaveAttackRanges(string id)
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Stage = NavalBattleStage.Preparation;
            CoreNavalAttackRange range = session.GetNavalCardAttackRange(Card(id));
            Assert.That(range.IsDamageCard, Is.False, id);
            Assert.That(range.Contains(new VoyagePoint(0f, 0f), new VoyagePoint(100f, 0f)), Is.False);
        }

        [TestCase("card_school_curse_b03")]
        [TestCase("card_school_curse_b05")]
        public void ExistingUnrestrictedDamageOverTimeEffectsRemainUnlimited(string id)
        {
            CoreVoyageSession session = NewBattle();
            CoreNavalAttackRange range = session.GetNavalCardAttackRange(Card(id));
            Assert.That(range.IsDamageCard, Is.True);
            Assert.That(range.IsUnlimited, Is.True);
            Assert.That(range.ScanRadiusCells, Is.Zero, "The UI must clip an unlimited range to the visible board.");
            Assert.That(session.NavalCardCanTargetCell(Card(id), new VoyagePoint(3000f, -2000f)), Is.True);
        }

        [Test]
        public void PreviewDoesNotSpendResourcesOrDisappearWhenResourcesAreMissing()
        {
            CoreVoyageSession session = NewBattle();
            session.Battle.Energy = 0;
            session.Cargo.Powder = session.Cargo.Cannonballs = 0;
            session.Battle.PlayerLoadedShot = 7;
            CoreBattleCard cannon = Card("card_school_tide_b03");
            Assert.That(session.Battle.Schools.TidePower, Is.Zero);
            Assert.That(session.NavalCardCanTargetCell(cannon, new VoyagePoint(200f, 200f)), Is.True);
            Assert.That(session.NavalCardCanTargetCell(Card("card_school_arcane_b03"), new VoyagePoint(300f, 0f)), Is.True);
            Assert.That(session.Battle.Energy, Is.Zero);
            Assert.That(session.Cargo.Powder, Is.Zero);
            Assert.That(session.Cargo.Cannonballs, Is.Zero);
            Assert.That(session.Battle.PlayerLoadedShot, Is.EqualTo(7));
            Assert.That(session.Battle.Schools.TidePower, Is.Zero);
            Assert.That(session.Battle.Schools.AvailableMana, Is.Zero);
        }

        [Test]
        public void PreparationAllowsReadOnlyNavalPreviewButBoardingAndBoardingOnlyCardsDoNot()
        {
            CoreVoyageSession session = NewBattle();
            CoreBattleCard cannon = Card("card_chain_shot");
            session.Battle.Stage = NavalBattleStage.Preparation;
            Assert.That(session.GetNavalCardAttackRange(cannon).IsDamageCard, Is.True);
            Assert.That(session.GetNavalCardAttackRange(Card("card_school_curse_b01")).IsUnlimited, Is.True);
            Assert.That(session.GetNavalCardAttackRange(Card("card_school_arcane_b07")).IsDamageCard, Is.False);
            session.Battle.Stage = NavalBattleStage.Naval;
            Assert.That(session.GetNavalCardAttackRange(Card("card_school_curse_b01")).IsDamageCard, Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(session.GetNavalCardAttackRange(cannon).IsDamageCard, Is.False);
        }

        [Test]
        public void MissingBattleNullCardAndInvalidCoordinatesNeverProduceHighlights()
        {
            CoreBattleCard cannon = Card("card_chain_shot");
            Assert.That(new CoreVoyageSession().GetNavalCardAttackRange(cannon).IsDamageCard, Is.False);
            CoreVoyageSession session = NewBattle();
            Assert.That(session.GetNavalCardAttackRange(null).IsDamageCard, Is.False);
            Assert.That(session.NavalCardCanTargetCell(cannon, new VoyagePoint(float.NaN, 0f)), Is.False);
            Assert.That(session.NavalCardCanTargetCell(Card("card_school_curse_b03"), new VoyagePoint(float.PositiveInfinity, 0f)), Is.False);
        }

        private static CoreBattleCard Card(string id) => CoreVoyageSession.CardLibrary.Single(card => card.Id == id);

        private static CoreVoyageSession NewBattle()
        {
            var session = new CoreVoyageSession();
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            session.Battle.Stage = NavalBattleStage.Naval;
            SetPlayerPosition(session, new VoyagePoint(0f, 0f));
            return session;
        }

        private static void SetPlayerPosition(CoreVoyageSession session, VoyagePoint position) =>
            typeof(CoreBattleState).GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session.Battle, new object[] { true, position, 0f });
    }
}
