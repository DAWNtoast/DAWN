using NUnit.Framework;
using ShatteredSail.Core.Tutorial;

namespace ShatteredSail.Core.Tests
{
    public sealed class TutorialVoyageSessionTests
    {
        [Test]
        public void FirstLeg_TeachesMandatoryOptionalCrewAndReturn_InOrder()
        {
            TutorialVoyageSession session = new TutorialVoyageSession();
            session.BeginFirstLeg();

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_first_event"));
            Assert.That(session.ResolveChoice(0), Is.True);

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_optional_event"));
            Assert.That(session.SkipCurrentEncounter(), Is.True);

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_crew_requirement_event"));
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(1), Is.True);

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_ambush_event"));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(TutorialVoyagePhase.FirstLegComplete));
            Assert.That(session.HasNavigator, Is.False);
        }

        [Test]
        public void AdvancedLeg_RevealsHiddenNode_DefeatsBoss_AndSavesChart()
        {
            TutorialVoyageSession session = CompleteFirstLeg();
            session.BeginAdvancedLeg();

            Assert.That(session.HasNavigator, Is.True);
            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_navigator_event"));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.HiddenLocationRevealed, Is.True);

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_hidden_event"));
            Assert.That(session.IsChoiceAvailable(1), Is.False);
            Assert.That(session.ResolveChoice(0), Is.True);

            Assert.That(session.SailToNext().Id, Is.EqualTo("tutorial_boss_event"));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(TutorialVoyagePhase.Battle));

            while (session.Phase == TutorialVoyagePhase.Battle)
            {
                Assert.That(session.PerformBattleAction(TutorialBattleAction.FireCannons), Is.True);
            }

            Assert.That(session.SailToNext(), Is.Null);
            Assert.That(session.Phase, Is.EqualTo(TutorialVoyagePhase.Complete));
            Assert.That(session.ChartSaved, Is.True);
        }

        [Test]
        public void Rations_UseAccumulatedCrewDays_AndOptionalSkipConsumesNoEventTime()
        {
            TutorialVoyageSession session = new TutorialVoyageSession();
            session.BeginFirstLeg();
            session.SailToNext();
            session.ResolveChoice(1);
            session.SailToNext();

            double hoursBeforeSkip = session.TotalHours;
            int waterBeforeSkip = session.Water;
            int foodBeforeSkip = session.Food;
            Assert.That(session.SkipCurrentEncounter(), Is.True);

            Assert.That(session.TotalHours, Is.EqualTo(hoursBeforeSkip));
            Assert.That(session.Water, Is.EqualTo(waterBeforeSkip));
            Assert.That(session.Food, Is.EqualTo(foodBeforeSkip));
            Assert.That(session.TotalNauticalMiles, Is.EqualTo(300f));
        }

        private static TutorialVoyageSession CompleteFirstLeg()
        {
            TutorialVoyageSession session = new TutorialVoyageSession();
            session.BeginFirstLeg();
            session.SailToNext();
            session.ResolveChoice(0);
            session.SailToNext();
            session.SkipCurrentEncounter();
            session.SailToNext();
            session.ResolveChoice(1);
            session.SailToNext();
            session.ResolveChoice(0);
            return session;
        }
    }
}
