using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageSafetyAuditTests
    {
        [Test]
        public void EveryAuthoredEventHasValidDataAndAResolutionSkipOrRescuePath()
        {
            List<CoreEncounter> variants = AuthoredEncounterVariants();
            int choiceCount = 0, battleChoices = 0;
            foreach (CoreEncounter encounter in variants)
            {
                Assert.That(CoreEncounterDiscoveryRules.TryClassify(encounter.Id, out _), Is.True, encounter.Id);
                Assert.That(encounter.Choices.Count, Is.GreaterThan(0), encounter.Id);
                var equipped = Voyage();
                foreach (CoreEncounterChoice choice in encounter.Choices)
                {
                    choiceCount++;
                    Assert.That(choice, Is.Not.Null, encounter.Id);
                    Assert.That(double.IsNaN(choice.Hours) || double.IsInfinity(choice.Hours), Is.False, encounter.Id);
                    Assert.That(choice.Hours, Is.GreaterThanOrEqualTo(0d), encounter.Id);
                    Assert.That(equipped.HasEncounterCrewRequirement(choice.RequiredCrew), Is.True,
                        encounter.Id + " has an unknown required profession: " + choice.RequiredCrew);
                    if (!choice.StartsBattle) continue;
                    battleChoices++;
                    var battleEntry = Voyage(requiredShip: choice.RequiredShip);
                    SetProperty(battleEntry, "Phase", CoreDemoPhase.Encounter);
                    SetProperty(battleEntry, "ActiveEncounter", encounter);
                    int index = encounter.Choices.ToList().IndexOf(choice);
                    int gold = battleEntry.Cargo.Gold, fame = battleEntry.Cargo.Fame;
                    Assert.That(battleEntry.ResolveChoice(index), Is.True, encounter.Id + ": " + battleEntry.ChoiceUnavailableReason(index));
                    Assert.That(battleEntry.Phase, Is.EqualTo(CoreDemoPhase.Battle), encounter.Id);
                    Assert.That(battleEntry.Battle.EnemyHull, Is.GreaterThan(0), encounter.Id);
                    Assert.That(battleEntry.Cargo.Gold, Is.EqualTo(gold + Math.Min(0, choice.Effect.Gold)),
                        encounter.Id + " must not prepay promised combat gold.");
                    Assert.That(battleEntry.Cargo.Fame, Is.EqualTo(fame + Math.Min(0, choice.Effect.Fame)),
                        encounter.Id + " must not prepay promised combat fame.");
                }
                foreach (bool empty in new[] { false, true })
                {
                    var session = new CoreVoyageSession();
                    session.ConfigureCrewCapabilities(false, false, false, false, false, false);
                    session.StartVoyage();
                    if (empty)
                    {
                        session.Cargo.Water = session.Cargo.Food = session.Cargo.Beer = session.Cargo.Medicine = 0;
                        session.Cargo.Wood = session.Cargo.Iron = session.Cargo.Cloth = session.Cargo.Powder = 0;
                        session.Cargo.Cannonballs = session.Cargo.Gold = session.Cargo.Fame = 0;
                    }
                    SetProperty(session, "Phase", CoreDemoPhase.Encounter);
                    SetProperty(session, "ActiveEncounter", encounter);
                    if (!encounter.Mandatory)
                    {
                        double hours = session.TotalHours;
                        Assert.That(session.SkipEncounter(), Is.True, encounter.Id);
                        Assert.That(session.TotalHours, Is.EqualTo(hours));
                        Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                        continue;
                    }
                    Assert.That(session.SkipEncounter(), Is.False, encounter.Id);
                    bool hasSafeEntry = Enumerable.Range(0, encounter.Choices.Count).Any(index =>
                        session.IsChoiceAvailable(index) && encounter.Choices[index].Effect.Hull > -session.Hull);
                    Assert.That(hasSafeEntry || session.CanRequestEmergencyTow(), Is.True,
                        encounter.Id + " must not trap a player without a crew or supplies.");
                }
            }
            Assert.That(variants.Select(item => item.Id).Distinct().Count(), Is.EqualTo(56));
            Assert.That(variants.Count, Is.EqualTo(93), "Both random-message variants and all three ranks of 18 treasure templates are checked.");
            Console.WriteLine("Authored encounter audit: " + variants.Count + " variants / 56 unique IDs / " +
                choiceCount + " choices / " + battleChoices + " battle entries.");
        }

        [TestCase("navigator", 0)] [TestCase("carpenter", 1)] [TestCase("doctor", 2)]
        [TestCase("gunner", 3)] [TestCase("cook", 4)] [TestCase("diver", 5)]
        public void EveryProfessionGateRequiresTheMatchingRecruitedCrew(string profession, int professionIndex)
        {
            for (int hired = -1; hired < 6; hired++)
            {
                var session = new CoreVoyageSession();
                session.ConfigureCrewCapabilities(hired == 0, hired == 1, hired == 2, hired == 3, hired == 4, hired == 5);
                session.StartVoyage();
                SetEncounter(session, new CoreEncounterChoice("专业选项", "奖励", 0, new VoyageEffect { Gold = 5 }, profession));
                int gold = session.Cargo.Gold;
                Assert.That(session.IsChoiceAvailable(0), Is.EqualTo(hired == professionIndex), profession + ": hired " + hired);
                Assert.That(session.ResolveChoice(0), Is.EqualTo(hired == professionIndex));
                Assert.That(session.Cargo.Gold, Is.EqualTo(gold + (hired == professionIndex ? 5 : 0)));
            }
        }

        [TestCase("witch")] [TestCase("captain")] [TestCase("unrecognized_profession")]
        public void UnknownProfessionDoesNotBecomeAnUnrestrictedReward(string profession)
        {
            var session = Voyage();
            SetEncounter(session, new CoreEncounterChoice("不可用职业", "奖励", 0, new VoyageEffect { Gold = 100 }, profession));
            int gold = session.Cargo.Gold;
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
        }

        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)] [TestCase(-1d)]
        public void InvalidEventDurationCannotAwardRewardsOrDamageVoyageClock(double hours)
        {
            var session = Voyage();
            SetEncounter(session, new CoreEncounterChoice("坏数据", "奖励", hours, new VoyageEffect { Gold = 100 }));
            int gold = session.Cargo.Gold, water = session.Cargo.Water;
            Assert.That(session.IsChoiceAvailable(0), Is.False);
            Assert.That(session.ResolveChoice(0), Is.False);
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.TotalHours, Is.Zero);
        }

        [Test]
        public void FatalEntryCostSettlesFailureBeforeCreatingBattleOrPayingPromisedReward()
        {
            var session = Voyage();
            SetEncounter(session, new CoreEncounterChoice("致命迎战", "船体损失与胜利收益", 0,
                new VoyageEffect { Hull = -session.MaxHull, Gold = 100 }, startsBattle: true));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(session.Battle, Is.Null);
            Assert.That(session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
            Assert.That(session.Settlement.BankedGold, Is.Zero);
            Assert.That(session.ResolveChoice(0), Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void VoyageChargesFirstFractionOnceAndFragmentedHoursCostTheSame(bool cook)
        {
            var whole = Voyage(cook); var split = Voyage(cook);
            int water = whole.Cargo.Water;
            Wait(whole, 24d);
            Wait(split, 0.5d);
            Assert.That(water - split.Cargo.Water, Is.EqualTo(1), "The first fractional ration is paid before sailing can earn experience.");
            for (int halfHour = 1; halfHour < 48; halfHour++) Wait(split, 0.5d);
            Assert.That(split.TotalHours, Is.EqualTo(whole.TotalHours));
            Assert.That(split.Cargo.Water, Is.EqualTo(whole.Cargo.Water));
            Assert.That(split.Cargo.Food, Is.EqualTo(whole.Cargo.Food));
            Assert.That(water - whole.Cargo.Water, Is.EqualTo((int)Math.Ceiling(whole.CrewCountValue *
                (cook ? GameplayBalance.CookRationMultiplier : 1d))));
        }

        [Test]
        public void NewVoyagesCannotRepeatedlyUseTheUnpaidRationFraction()
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            for (int trip = 0; trip < 4; trip++)
            {
                session.StartVoyage();
                int water = session.Cargo.Water, food = session.Cargo.Food;
                Wait(session, 0.5d);
                Assert.That(session.Cargo.Water, Is.EqualTo(water - 1));
                Assert.That(session.Cargo.Food, Is.EqualTo(food - 1));
                Assert.That(session.SafeReturn(), Is.True);
                session.ReturnToPortAfterSettlement();
                Assert.That(session.Cargo.Water, Is.EqualTo(water - 3));
                Assert.That(session.Cargo.Food, Is.EqualTo(food - 2));
            }
        }

        [Test]
        public void EncounterBeforeClippedBoundaryDoesNotTakeUnreachedBorderDamage()
        {
            var session = Voyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            SetProperty(session, "Position", new VoyagePoint(94, 33));
            var node = new CoreMapNode { Id = 0, Position = new VoyagePoint(95, 33),
                Encounter = new CoreEncounter("early-stop", "边界前事件", "事件", "", true, false, 0, 0,
                    new CoreEncounterChoice("通过", "无额外损失", 0, new VoyageEffect())) };
            session.Map.Nodes.Add(node);
            session.SetHeading(SailingDirection.East);
            session.SetDistance(180);
            Assert.That(session.GetForecast().Clipped, Is.True);
            int hull = session.Hull;
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.ActiveEncounter, Is.SameAs(node.Encounter));
            Assert.That(session.Hull, Is.EqualTo(hull), "The event interrupts travel before the planned border contact.");
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Sail(), Is.True);
            Assert.That(session.Hull, Is.EqualTo(hull - 2), "Actually reaching the clipped border still incurs its normal damage.");
        }

        [Test]
        public void MaximumMedicineCostNeverOverflowsIntoAPositiveReward()
        {
            var session = Voyage();
            MethodInfo adjusted = typeof(CoreVoyageSession).GetMethod("AdjustedMedicineEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(adjusted.Invoke(session, new object[] { int.MinValue }), Is.EqualTo(-1610612736));
        }

        [Test]
        public void DiverBonusUsesWideArithmeticBeforeAffordabilityValidation()
        {
            var session = Voyage();
            MethodInfo adjusted = typeof(CoreVoyageSession).GetMethod("AdjustedSalvageEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(Convert.ToInt64(adjusted.Invoke(session, new object[] { int.MaxValue })), Is.EqualTo(2684354558L));
        }

        [Test]
        public void ExtremeHealingDoesNotWrapIntoShipwreckOrLoseMorale()
        {
            var session = Voyage();
            SetProperty(session, "Hull", 10);
            SetEncounter(session, new CoreEncounterChoice("极值修理", "恢复", 0,
                new VoyageEffect { Hull = int.MaxValue, Morale = int.MaxValue }));
            Assert.That(session.ResolveChoice(0), Is.True);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            Assert.That(session.Morale, Is.EqualTo(CoreVoyageSession.MaxMorale));
        }

        private static CoreVoyageSession Voyage(bool cook = true, string requiredShip = "")
        {
            var session = new CoreVoyageSession();
            if (!string.IsNullOrWhiteSpace(requiredShip))
            {
                string id = requiredShip.Split('|', ',', ';')[0].Trim();
                int ship = Enumerable.Range(0, CoreVoyageSession.Ships.Count).Single(index => CoreVoyageSession.Ships[index].Id == id);
                Assert.That(session.SelectShip(ship), Is.True);
            }
            session.ConfigureCrewCapabilities(true, true, true, true, cook, true);
            session.StartVoyage();
            return session;
        }

        private static void Wait(CoreVoyageSession session, double hours)
        {
            SetEncounter(session, new CoreEncounterChoice("等待", "正常时间消耗", hours, new VoyageEffect()));
            Assert.That(session.ResolveChoice(0), Is.True);
        }

        private static void SetEncounter(CoreVoyageSession session, CoreEncounterChoice choice)
        {
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
            SetProperty(session, "ActiveEncounter", new CoreEncounter("safety-audit", "审计事件", "事件", "", true, false, 1, 70, choice));
        }

        private static void SetProperty(CoreVoyageSession session, string name, object value) =>
            typeof(CoreVoyageSession).GetProperty(name).SetValue(session, value);

        private static List<CoreEncounter> AuthoredEncounterVariants()
        {
            var authored = new Dictionary<string, CoreEncounter>(StringComparer.Ordinal);
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
            {
                var session = new CoreVoyageSession();
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[route].UnlockLevel),
                });
                Assert.That(session.CanSelectRoute(route, out string reason), Is.True, reason);
                session.SelectRoute(route); session.SetSeed("COMPLETE-EVENT-CATALOG-AUDIT"); session.StartVoyage();
                Assert.That(session.SelectedRouteIndex, Is.EqualTo(route), "The audit must sample every authored route.");
                // Generation places the complete authored pool, without replacement,
                // plus each route boss and all six optional third-chart bosses.
                foreach (CoreMapNode node in session.Map.Nodes) authored[node.Encounter.Id] = node.Encounter;
                MethodInfo ambush = typeof(CoreVoyageSession).GetMethod("CreateRandomAmbush", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (bool detected in new[] { false, true })
                {
                    var encounter = (CoreEncounter)ambush.Invoke(session, new object[] { detected });
                    authored[encounter.Id] = encounter;
                }
            }
            var variants = authored.Values.ToList();
            foreach (string factory in new[] { "FirstEvent", "OptionalEvent", "CrewRequirementEvent", "NavigatorEvent", "AmbushEvent", "BossEvent" })
                variants.Add((CoreEncounter)typeof(CoreGuidedTutorialContent).GetMethod(factory, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
            MethodInfo random = typeof(CoreGuidedTutorialContent).GetMethod("RandomEvent", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (bool lucky in new[] { false, true }) variants.Add((CoreEncounter)random.Invoke(null, new object[] { lucky }));
            foreach (CoreTreasureMapRank rank in CoreTreasureHuntCatalog.Ranks)
                variants.AddRange(CoreTreasureHuntCatalog.CreateEncounters(rank));
            return variants;
        }
    }
}
