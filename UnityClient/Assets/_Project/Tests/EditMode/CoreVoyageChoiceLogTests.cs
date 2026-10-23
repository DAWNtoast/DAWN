using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreVoyageChoiceLogTests
    {
        [Test]
        public void RevisitedChoiceLogsActualDiscountedSalvageAndElapsedRations()
        {
            CoreVoyageSession session = Ready();
            session.ConfigureCrewCapabilities(false, false, false, false, false, true);
            string payload = "SS3.0.0.1.0.1." + SeedHash.Compute("PROGRESS-FIXTURE-0").ToString("X8") + ".00000000";
            Assert.That(session.TrySelectChartSeed(payload + "." + SeedHash.Compute(payload).ToString("X8"), out string reason), Is.True, reason);
            foreach (int expectedWood in new[] { 15, 7, 3, 0 })
            {
                Refill(session);
                session.StartVoyage();
                CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == "event_reef_sunken_boat");
                SetField(session, "activeNode", node);
                Enter(session, node.Encounter);
                int water = session.Cargo.Water, food = session.Cargo.Food;
                int rations = Rations(session, 3d);

                if (expectedWood == 0)
                {
                    double hours = session.TotalHours;
                    string[] logs = session.LogEntries.ToArray();
                    Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(3));
                    Assert.That(session.EffectiveChoiceEffect(0).Wood, Is.Zero);
                    Assert.That(session.ChoiceUnavailableReason(0), Does.Contain("奖励已耗尽"));
                    Assert.That(session.ResolveChoice(0), Is.False);
                    Assert.That(session.Cargo.Wood, Is.Zero);
                    Assert.That(session.Cargo.Water, Is.EqualTo(water));
                    Assert.That(session.Cargo.Food, Is.EqualTo(food));
                    Assert.That(session.TotalHours, Is.EqualTo(hours));
                    Assert.That(session.ActiveEncounterClaimCount, Is.EqualTo(3));
                    Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
                    CollectionAssert.AreEqual(logs, session.LogEntries,
                        "An exhausted choice must not log an action or supplies that were never consumed.");
                    Assert.That(session.SkipEncounter(), Is.True);
                    Assert.That(session.TotalHours, Is.EqualTo(hours));
                    Assert.That(session.Cargo.Water, Is.EqualTo(water));
                    Assert.That(session.Cargo.Food, Is.EqualTo(food));
                }
                else
                {
                    Assert.That(session.ResolveChoice(0), Is.True);
                    Assert.That(session.Cargo.Wood, Is.EqualTo(expectedWood));
                    Assert.That(session.Cargo.Water, Is.EqualTo(water - rations));
                    Assert.That(session.Cargo.Food, Is.EqualTo(food - rations));
                    string log = ChoiceLog(session);
                    Assert.That(log, Does.Contain("耗时 3 小时"));
                    Assert.That(log, Does.Contain("淡水 -" + rations).And.Contain("食物 -" + rations));
                    Assert.That(log, Does.Contain("木材 +" + expectedWood));
                    Assert.That(log, Does.Not.Contain("木材 +12"));
                }
                Assert.That(session.SafeReturn(), Is.True);
                session.ReturnToPortAfterSettlement();
            }
        }

        [TestCase(1)]
        [TestCase(0)]
        public void ChoiceLogsCrewAdjustedCostsAndGainsAndOnlyActualHullRepair(int missingHull)
        {
            CoreVoyageSession session = Ready();
            session.ConfigureCrewCapabilities(false, false, true, false, false, true);
            session.Cargo.Medicine = 4;
            session.StartVoyage();
            SetProperty(session, "Hull", session.MaxHull - missingHull);
            Enter(session, new CoreEncounter("choice_log_crew", "修船打捞", "检查", "", false, false, 1, 0,
                new CoreEncounterChoice("修补后回收", "旧描述：药品 -4、木材 +8、铁矿 +4、船体 +8", 2,
                    new VoyageEffect { Medicine = -4, Wood = 8, Iron = 4, Hull = 8, Water = 5, Food = -2 })));
            int water = session.Cargo.Water, food = session.Cargo.Food;
            int rations = Rations(session, 2d);

            Assert.That(session.ResolveChoice(0), Is.True);

            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            Assert.That(session.Cargo.Wood, Is.EqualTo(10));
            Assert.That(session.Cargo.Iron, Is.EqualTo(5));
            Assert.That(session.Hull, Is.EqualTo(session.MaxHull));
            Assert.That(session.Cargo.Water - water, Is.EqualTo(5 - rations));
            Assert.That(session.Cargo.Food - food, Is.EqualTo(-2 - rations));
            string log = ChoiceLog(session);
            Assert.That(log, Does.Contain("药品 -3").And.Contain("木材 +10").And.Contain("铁矿 +5"));
            Assert.That(log, Does.Contain("淡水 +" + (5 - rations)).And.Contain("食物 -" + (2 + rations)));
            if (missingHull == 0) Assert.That(log, Does.Not.Contain("船体 +"));
            else Assert.That(log, Does.Contain("船体 +1"));
            Assert.That(log, Does.Not.Contain("旧描述").And.Not.Contain("船体 +8").And.Not.Contain("药品 -4"));
        }

        [Test]
        public void BattleChoiceLogsOnlyEntryCostsAndDefersUnawardedRewards()
        {
            CoreVoyageSession session = Ready();
            session.ConfigureCrewCapabilities(false, false, true, false, false, false);
            session.Cargo.Medicine = 4;
            session.StartVoyage();
            Enter(session, new CoreEncounter("choice_log_battle", "海上迎战", "检查", "", true, false, 1, 8,
                new CoreEncounterChoice("护住货舱迎战", "金币 +85、宝石 +1、古代材料 +1；另获基础海战奖励", 1,
                    new VoyageEffect { Medicine = -4, Gold = 85, Gem = 1, AncientMaterial = 1 }, startsBattle: true)));
            int gold = session.Cargo.Gold, gems = session.Cargo.Gem, materials = session.Cargo.AncientMaterial;
            int rations = Rations(session, 1d);

            Assert.That(session.ResolveChoice(0), Is.True);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(session.Cargo.Gem, Is.EqualTo(gems));
            Assert.That(session.Cargo.AncientMaterial, Is.EqualTo(materials));
            Assert.That(session.Cargo.Medicine, Is.EqualTo(1));
            string log = ChoiceLog(session);
            Assert.That(log, Does.Contain("进入战斗，获胜后结算奖励"));
            Assert.That(log, Does.Contain("药品 -3").And.Contain("淡水 -" + rations).And.Contain("食物 -" + rations));
            Assert.That(log, Does.Not.Contain("金币 +").And.Not.Contain("宝石 +").And.Not.Contain("古代材料 +"));
        }

        [TestCase(1)]
        [TestCase(0)]
        public void RevealChoiceLogsOnlyTheNumberOfNewlyRevealedHiddenLocations(int unknownLocations)
        {
            CoreVoyageSession session = Ready();
            session.StartVoyage();
            CoreMapNode[] hidden = session.Map.Nodes.Where(node => node.Encounter.CanRevealByIntel).ToArray();
            Assert.That(hidden.Length, Is.GreaterThan(0));
            foreach (CoreMapNode node in hidden) node.IntelRevealed = true;
            if (unknownLocations > 0)
            {
                hidden[0].IntelRevealed = hidden[0].Discovered = false;
                hidden[0].Visited = false;
            }
            Enter(session, new CoreEncounter("choice_log_reveal", "核对海图", "检查", "", false, false, 1, 0,
                new CoreEncounterChoice("抄录隐藏地点坐标", "情报 +3", 0, new VoyageEffect { Reveal = 3 })));
            int before = hidden.Count(node => node.IntelRevealed);

            Assert.That(session.ResolveChoice(0), Is.True);

            Assert.That(hidden.Count(node => node.IntelRevealed) - before, Is.EqualTo(unknownLocations));
            string log = ChoiceLog(session);
            if (unknownLocations > 0) Assert.That(log, Does.Contain("揭示隐藏地点 +1"));
            else Assert.That(log, Does.Not.Contain("揭示隐藏地点"));
            Assert.That(log, Does.Not.Contain("情报 +3").And.Not.Contain("揭示隐藏地点 +3"));
        }

        private static CoreVoyageSession Ready()
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
            session.RestoreProfile(1000, "断锚港");
            Refill(session);
            return session;
        }

        private static void Refill(CoreVoyageSession session)
        {
            foreach (PropertyInfo property in typeof(VoyageCargo).GetProperties().Where(item => item.PropertyType == typeof(int)))
                property.SetValue(session.Cargo, 0);
            session.Cargo.Water = session.Cargo.Food = 100;
        }

        private static void Enter(CoreVoyageSession session, CoreEncounter encounter)
        {
            SetProperty(session, "ActiveEncounter", encounter);
            SetProperty(session, "Phase", CoreDemoPhase.Encounter);
        }

        private static int Rations(CoreVoyageSession session, double hours) => (int)typeof(CoreVoyageSession)
            .GetMethod("RationCostFor", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { hours });

        private static string ChoiceLog(CoreVoyageSession session) => session.LogEntries.Last(line => line.StartsWith("选择「", StringComparison.Ordinal));
        private static void SetField(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
        private static void SetProperty(CoreVoyageSession session, string name, object value) => typeof(CoreVoyageSession)
            .GetProperty(name).SetValue(session, value);
    }
}
