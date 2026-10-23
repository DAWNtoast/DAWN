using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreSeaChartExhaustedRewardTests
    {
        private const string Diver = "event_moon_relic_diver";
        private const string Buoy = "event_moon_v2_ossuary_buoy";
        private const string DiverChart = "SS6.2.0.1.0.0.9F0725AC.95E3DF52.B9A8439A";
        private const string BuoyChart = "SS6.2.0.1.0.0.364E6B56.185B1C27.1EB4614E";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(Diver, 1)]
        [TestCase(Buoy, 0)]
        public void AuthoredExchangePaysFirstRewardThenRejectsEmptyRevisitWithoutMutation(string id, int index)
        {
            CoreVoyageSession session = Ready(id);
            session.StartVoyage();
            Enter(session, id);
            Assert.That(session.ResolveChoice(index), Is.True, session.ChoiceUnavailableReason(index));
            Assert.That(id == Diver ? session.Cargo.AncientCompass : session.Cargo.Relic, Is.EqualTo(1));
            Assert.That(id == Diver ? session.Cargo.Iron : session.Cargo.Lantern, Is.EqualTo(id == Diver ? 6 : 1));
            ReturnAndRevisit(session, id);
            Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(1));
            VoyageEffect quote = session.EffectiveChoiceEffect(index);
            Assert.That(id == Diver ? quote.AncientCompass : quote.Relic, Is.Zero);
            string before = Snapshot(session);
            string reason = session.EncounterChoiceConditionReason(session.ActiveEncounter.Choices[index]);
            Assert.That(reason, Does.Contain("奖励已耗尽"));
            Assert.That(session.ChoiceUnavailableReason(index), Is.EqualTo(reason));
            Assert.That(session.IsChoiceAvailable(index), Is.False);
            Assert.That(session.ResolveChoice(index), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before), "Rejected choices must not spend cargo, time, hull or a claim.");
        }

        [TestCase("gold")] [TestCase("hull")] [TestCase("morale")] [TestCase("cleanse")]
        [TestCase("discovery")] [TestCase("target")] [TestCase("reveal")]
        [TestCase("sailing")] [TestCase("survey")]
        public void ExhaustedItemDoesNotBlockAnotherEffectiveReward(string benefit)
        {
            CoreVoyageSession session = SecondVisit();
            var effect = new VoyageEffect { Iron = -1, AncientCompass = 1 };
            switch (benefit)
            {
                case "gold": effect.Gold = 4; break;
                case "hull": effect.Hull = 4; Set(session, "Hull", session.MaxHull - 10); break;
                case "morale": effect.Morale = 4; Set(session, "Morale", CoreVoyageSession.MaxMorale - 10); break;
                case "cleanse": effect.Curse = -1; session.Cargo.Curse = 1; break;
                case "discovery": effect.GrantDiscovery = "moon_star_chart"; break;
                case "target":
                    CoreMapNode target = session.Map.Nodes.Single(n => n.Encounter.Id == "moon_obelisk");
                    target.Discovered = target.IntelRevealed = target.Visited = false;
                    effect.RevealEncounterId = target.Encounter.Id;
                    break;
                case "reveal":
                    CoreMapNode hidden = session.Map.Nodes.First(n => n.Encounter.CanRevealByIntel);
                    hidden.IntelRevealed = hidden.Visited = false;
                    effect.Reveal = 2;
                    break;
                case "sailing": effect.SailingBoostHours = 8; break;
                case "survey": effect.SurveyBoostHours = 8; break;
            }
            ReplaceChoice(session, effect);
            Assert.That(session.EffectiveChoiceEffect(0).AncientCompass, Is.Zero);
            Assert.That(session.EncounterChoiceConditionReason(session.ActiveEncounter.Choices[0]), Is.Empty);
            Assert.That(session.ResolveChoice(0), Is.True, benefit + ": " + session.ChoiceUnavailableReason(0));
            Assert.That(session.Cargo.Iron, Is.EqualTo(11));
        }

        [TestCase("hull")] [TestCase("morale")] [TestCase("cleanse")]
        public void CappedSecondaryBenefitDoesNotMakeAnEmptyExchangeChargeable(string benefit)
        {
            CoreVoyageSession session = SecondVisit();
            var effect = new VoyageEffect { Iron = -1, AncientCompass = 1 };
            if (benefit == "hull") { effect.Hull = 4; Set(session, "Hull", session.MaxHull); }
            if (benefit == "morale") { effect.Morale = 4; Set(session, "Morale", CoreVoyageSession.MaxMorale); }
            if (benefit == "cleanse") { effect.Curse = -1; session.Cargo.Curse = 0; }
            ReplaceChoice(session, effect);
            string before = Snapshot(session);
            Assert.That(session.EncounterChoiceConditionReason(session.ActiveEncounter.Choices[0]), Does.Contain("奖励已耗尽"));
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
        }

        [TestCase("return")] [TestCase("postpone")]
        public void ExitEffectsRemainEligible(string progress)
        {
            CoreVoyageSession session = SecondVisit();
            var effect = new VoyageEffect { Iron = -1, AncientCompass = 1 };
            if (progress == "return") effect.ForceReturn = true;
            if (progress == "postpone") effect.PostponeTreasureCore = true;
            ReplaceChoice(session, effect);
            Assert.That(session.EncounterChoiceConditionReason(session.ActiveEncounter.Choices[0]), Is.Empty);
            Assert.That(session.IsChoiceAvailable(0), Is.True);
        }

        [TestCase(true)] [TestCase(false)]
        public void PureCostAndNoEffectFallbacksRemainUsable(bool hasCost)
        {
            CoreVoyageSession session = SecondVisit();
            ReplaceChoice(session, hasCost ? new VoyageEffect { Hull = -2 } : new VoyageEffect());
            Assert.That(session.SkipEncounter(), Is.False, "An ordinary mandatory fallback cannot be bypassed.");
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        [TestCase("choice")] [TestCase("encounter")] [TestCase("random")]
        public void RenewableRewardsKeepFullValueAndRemainUsable(string scope)
        {
            CoreVoyageSession session = SecondVisit();
            ReplaceChoice(session, new VoyageEffect { Iron = -1, AncientCompass = 1 }, renewable: scope == "choice");
            if (scope == "encounter") session.ActiveEncounter.RepeatPolicy = CoreEncounterRepeatPolicy.Renewable;
            if (scope == "random") session.ActiveEncounter.RepeatPolicy = CoreEncounterRepeatPolicy.RandomVoyage;
            Assert.That(session.SkipEncounter(), Is.False);
            Assert.That(session.EffectiveChoiceEffect(0).AncientCompass, Is.EqualTo(1));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Cargo.AncientCompass, Is.EqualTo(1));
        }

        [Test]
        public void BattleChoicesKeepTheirOwnVictoryRewardBoundary()
        {
            CoreVoyageSession session = SecondVisit();
            ReplaceChoice(session, new VoyageEffect { AncientCompass = 1 }, battle: true);
            Assert.That(session.SkipEncounter(), Is.False, "Diminished rewards cannot bypass mandatory combat.");
            Assert.That(session.EncounterChoiceConditionReason(session.ActiveEncounter.Choices[0]), Is.Empty);
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
        }

        [Test]
        public void FullyExhaustedOptionalLocationStillAllowsFreeSkipWithoutRewards()
        {
            const string id = "event_moon_drowned_library";
            CoreVoyageSession session = Ready(Diver);
            bool found = false;
            for (int candidate = 0; candidate < 100; candidate++)
            {
                string payload = "SS6.2.0.1.0.1." + SeedHash.Compute("EXHAUSTED-LOCATION-" + candidate).ToString("X8") + ".00000000";
                Assert.That(session.TrySelectChartSeed(payload + "." + SeedHash.Compute(payload).ToString("X8"), out string reason), Is.True, reason);
                if (session.PreviewSelectedSeaChart().Nodes.Any(n => n.Encounter.Id == id)) { found = true; break; }
            }
            Assert.That(found, Is.True, "The deterministic fixture must contain the actual location.");
            session.StartVoyage(); Enter(session, id);
            for (int visit = 0; visit < 3; visit++)
            {
                Assert.That(session.ActiveEncounter.Mandatory, Is.False);
                Assert.That(session.ResolveChoice(1), Is.True);
                ReturnAndRevisit(session, id);
            }
            Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, session.ActiveEncounter.Choices.Count).All(i => !session.IsChoiceAvailable(i)), Is.True);
            Assert.That(session.ActiveEncounter.Mandatory, Is.False);
            Assert.That(session.CanRequestEmergencyTow(), Is.False, "Exhausted rewards are not a supply shortage.");
            int water = session.Cargo.Water, food = session.Cargo.Food;
            double hours = session.TotalHours;
            Assert.That(session.SkipEncounter(), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(session.CompletedEventCount(), Is.Zero);
        }

        private static CoreVoyageSession Ready(string id)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Refill(session);
            Assert.That(session.TrySelectChartSeed(id == Buoy ? BuoyChart : DiverChart, out string reason), Is.True, reason);
            return session;
        }

        private static CoreVoyageSession SecondVisit()
        {
            CoreVoyageSession session = Ready(Diver);
            session.StartVoyage(); Enter(session, Diver);
            Assert.That(session.ResolveChoice(1), Is.True);
            ReturnAndRevisit(session, Diver);
            return session;
        }

        private static void ReturnAndRevisit(CoreVoyageSession session, string id)
        {
            Assert.That(session.SafeReturn(), Is.True);
            session.ReturnToPortAfterSettlement();
            Refill(session); session.StartVoyage(); Enter(session, id);
        }

        private static void Refill(CoreVoyageSession session)
        {
            foreach (PropertyInfo property in typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int)))
                property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 100;
            session.Cargo.Iron = 12; session.Cargo.Wood = 6; session.Cargo.Lantern = 2;
        }

        private static void Enter(CoreVoyageSession session, string id)
        {
            CoreMapNode node = session.Map.Nodes.Single(n => n.Encounter.Id == id);
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter); Set(session, "Phase", CoreDemoPhase.Encounter);
        }

        private static void ReplaceChoice(CoreVoyageSession session, VoyageEffect effect, bool renewable = false, bool battle = false)
        {
            var encounter = new CoreEncounter(Diver, "收益条件夹具", "交易", "复用已领取一次的海图记录", true, false, 0, 70,
                new CoreEncounterChoice("兑换", "条件边界", 1d, effect, startsBattle: battle, renewable: renewable));
            session.Map.Nodes.Single(n => n.Encounter.Id == Diver).Encounter = encounter;
            Set(session, "ActiveEncounter", encounter);
        }

        private static void Set(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);

        private static string Snapshot(CoreVoyageSession session) =>
            string.Join("|", typeof(VoyageCargo).GetProperties().Where(p => p.PropertyType == typeof(int)).Select(p => p.Name + "=" + p.GetValue(session.Cargo))) +
            "|" + session.Hull + "|" + session.Morale + "|" + session.TotalHours + "|" + session.ElapsedGameHours +
            "|" + session.ActiveEncounterClaimCount + "|" + session.Phase + "|" + string.Join(";", session.LogEntries);
    }
}
