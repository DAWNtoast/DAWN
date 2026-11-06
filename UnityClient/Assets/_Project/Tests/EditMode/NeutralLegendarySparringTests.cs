using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

namespace ShatteredSail.Core.Tests
{
    public sealed class NeutralLegendarySparringTests
    {
        private const string Tide = "legendary_pirate_tide_requiem";
        private const string Horizon = "legendary_pirate_last_horizon";

        [TestCase(Tide, true, "继承者舰队")]
        [TestCase(Horizon, false, "中立探索舰队")]
        public void FriendlyLegends_OfferOptionalSparringWithoutConflatingTheirMotives(
            string id, bool stillSearching, string category)
        {
            CoreLegendaryWarship ship = CoreLegendaryWarships.Find(id);
            SeventhPortFormerLieutenant lore = SeventhPortLoreCatalog.FormerLieutenantForFleet(id);
            Assert.That(ship.IsFormerCaptainCrew, Is.True);
            Assert.That(ship.IsSparring, Is.True);
            Assert.That(ship.StillSearching, Is.EqualTo(stillSearching));
            Assert.That(lore, Is.Not.Null);
            Assert.That(lore.Id, Is.EqualTo(ship.FormerLieutenantId));
            Assert.That(lore.Name, Is.EqualTo(ship.FormerLieutenantName));
            Assert.That(lore.IsSparring, Is.True);
            Assert.That(lore.StillSearching, Is.EqualTo(stillSearching));

            CoreVoyageSession session = Voyage("SPAR-ENTRY-" + id);
            CoreEncounter[] entries = {
                ship.CreateEncounter(),
                session.Map.Nodes.Single(node => node.Encounter.Id == id).Encounter,
                CoreLegendaryWarships.CreateTreasureEncounters("moonlit").Single(encounter => encounter.Id == id)
            };
            foreach (CoreEncounter encounter in entries)
            {
                Assert.That(encounter.Category, Does.Contain(category).And.Contain("切磋").And.Not.Contain("堕落"));
                Assert.That(encounter.Mandatory, Is.False);
                Assert.That(encounter.Choices[0].Label, Is.EqualTo("与传说舰队切磋"));
                Assert.That(encounter.Choices[0].StartsBattle, Is.True);
                Assert.That(encounter.Choices[0].Detail,
                    Does.Contain("海战切磋").And.Contain("接舷切磋").And.Not.Contain("击沉").And.Not.Contain("夺船"));
                Assert.That(encounter.Choices[1].StartsBattle, Is.False);
                Assert.That(encounter.Choices[1].Hours, Is.Zero);
            }
        }

        [TestCase("legendary_pirate_iron_oath")]
        [TestCase("legendary_pirate_thunder_crown")]
        [TestCase("legendary_pirate_red_widow")]
        [TestCase("legendary_pirate_ember_ark")]
        public void FallenLegends_KeepHostileChoicesAndVictoryQuotes(string id)
        {
            CoreLegendaryWarship ship = CoreLegendaryWarships.Find(id);
            CoreEncounter encounter = ship.CreateEncounter();
            Assert.That(ship.IsSparring, Is.False);
            Assert.That(ship.StillSearching, Is.False);
            Assert.That(SeventhPortLoreCatalog.FormerLieutenantForFleet(id).IsSparring, Is.False);
            Assert.That(encounter.Category, Does.Contain("堕落旧部").And.Not.Contain("切磋"));
            Assert.That(encounter.Choices[0].Label, Is.EqualTo("挑战堕落旧部"));
            Assert.That(encounter.Choices[0].Detail, Does.Contain("击沉").And.Contain("夺船"));
        }

        [TestCase(Tide, false)]
        [TestCase(Tide, true)]
        [TestCase(Horizon, false)]
        [TestCase(Horizon, true)]
        public void SparringVictory_PreservesRewardsAndProgressWithoutClaimingCaptureOrSinking(string id, bool boarding)
        {
            CoreVoyageSession session = Voyage("SPAR-WIN-" + id + boarding);
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
            EnterEncounter(session, node);
            int goldBefore = session.Cargo.Gold, fameBefore = session.Cargo.Fame;
            Assert.That(session.ResolveChoice(0), Is.True);
            CoreBattleState battle = session.Battle;
            battle.Stage = boarding ? NavalBattleStage.Boarding : NavalBattleStage.Naval;
            battle.PlayerBoardingHealth = Math.Max(1, battle.PlayerBoardingHealth);
            if (boarding) battle.EnemyBoardingHealth = 0;
            else battle.EnemyHull = 0;
            int logStart = session.LogEntries.Count;
            Invoke(session, "ResolveBattleOutcome");

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.Battle, Is.Null);
            Assert.That(node.Visited, Is.True);
            Assert.That(session.BossDefeated, Is.False);
            Assert.That(session.LastBattleWasCaptured, Is.EqualTo(boarding), "Keep the existing stage/save flag and reward semantics.");
            Assert.That(session.Cargo.Gold - goldBefore,
                Is.EqualTo(CoreBattleRewardBalance.GoldReward(true, node.Encounter.Choices[0].Effect, boarding,
                    doctor: session.HasDoctor, diver: session.HasDiver)));
            Assert.That(session.Cargo.Fame - fameBefore,
                Is.EqualTo(CoreBattleRewardBalance.FameReward(true, node.Encounter.Choices[0].Effect, boarding)));
            string expected = boarding ? "接舷切磋获胜" : "海战切磋获胜";
            Assert.That(battle.LastAction, Does.Contain(expected).And.Not.Contain("沉没").And.Not.Contain("夺船"));
            string log = string.Join("\n", session.LogEntries.Skip(logStart));
            Assert.That(log, Does.Contain(expected).And.Not.Contain("击沉").And.Not.Contain("夺船"));
        }

        [TestCase(Tide)]
        [TestCase(Horizon)]
        public void SparringLoss_UsesYieldingLanguageAndKeepsFailureSettlement(string id)
        {
            CoreVoyageSession session = Voyage("SPAR-LOSS-" + id);
            CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id == id);
            EnterEncounter(session, node);
            Assert.That(session.ResolveChoice(0), Is.True);
            int logStart = session.LogEntries.Count;
            Set(session, "Hull", 0);
            Invoke(session, "ResolveBattleOutcome");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(node.Visited, Is.False);
            Assert.That(session.BossDefeated, Is.False);
            string log = string.Join("\n", session.LogEntries.Skip(logStart));
            Assert.That(log, Does.Contain("切磋中认输").And.Not.Contain("旗舰沉没"));
        }

        [TestCase(Tide, "接舷切磋额外奖励")]
        [TestCase(Horizon, "接舷切磋额外奖励")]
        [TestCase("legendary_pirate_iron_oath", "夺船额外战利品")]
        [TestCase(null, "夺船额外战利品")]
        public void ComponentRewardLabels_UseTheRewardSourceRatherThanCurrentEncounter(string sourceId, string label)
        {
            var session = new CoreVoyageSession();
            session.RecordBoardingComponentDrop("试验部件", sourceId);
            Assert.That(session.LogEntries.Last(), Is.EqualTo(label + "：获得「试验部件」。"));
        }

        [Test]
        public void MoonlitRoster_KeepsBothSparringLegendsAndLegacyOneShipLayout()
        {
            CollectionAssert.AreEquivalent(new[] { Tide, Horizon },
                CoreLegendaryWarships.CreateTreasureEncounters("moonlit").Select(encounter => encounter.Id));
            CollectionAssert.AreEqual(new[] { Tide },
                CoreLegendaryWarships.CreateTreasureEncounters("moonlit", false).Select(encounter => encounter.Id));
            Assert.That(CoreLegendaryWarships.All.Count(ship => ship.IsSparring), Is.EqualTo(2));
            Assert.That(CoreLegendaryWarships.All.Count(ship => ship.StillSearching), Is.EqualTo(1));
            Assert.That(CoreLegendaryWarships.IsSparringEncounter(null), Is.False);
            Assert.That(CoreLegendaryWarships.IsSparringEncounter("missing"), Is.False);
        }

        [Test]
        public void StoryCatalog_PreservesSixShipsAndCallsBothFriendlyEncountersSparring()
        {
            Assert.That(CoreTreasureStoryCatalog.Battles.Count, Is.EqualTo(6));
            foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
            {
                TreasureBattleStory story = CoreTreasureStoryCatalog.FindBattle(ship.Id);
                Assert.That(story, Is.Not.Null, ship.Id);
                Assert.That(story.ShipName, Is.EqualTo(ship.Name));
                Assert.That(story.Pages.Count, Is.EqualTo(2));
                if (ship.IsSparring) Assert.That(string.Join("\n", story.Pages), Does.Contain("切磋"));
            }
            string horizonStory = string.Join("\n", CoreTreasureStoryCatalog.FindBattle(Horizon).Pages);
            Assert.That(horizonStory, Does.Contain("不理解").And.Contain("从未反叛").And.Contain("离开后")
                .And.Contain("探险").And.Contain("中立").And.Contain("切磋"));
        }

        private static CoreVoyageSession Voyage(string seed)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress
                { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(CoreVoyageSession.Routes[2].UnlockLevel) });
            session.SelectRoute(2);
            session.SetSeed(seed);
            session.StartVoyage();
            return session;
        }

        private static void EnterEncounter(CoreVoyageSession session, CoreMapNode node)
        {
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, node);
            Set(session, "ActiveEncounter", node.Encounter);
            Set(session, "Phase", CoreDemoPhase.Encounter);
        }

        private static void Set(CoreVoyageSession session, string property, object value) =>
            typeof(CoreVoyageSession).GetProperty(property).SetValue(session, value);

        private static void Invoke(CoreVoyageSession session, string method) =>
            typeof(CoreVoyageSession).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, null);
    }
}
