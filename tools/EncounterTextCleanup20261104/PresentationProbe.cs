using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;

// Compile the two real presentation partials without the Unity scene/controller.
// These wrappers supply only their session dependency and expose their output.
namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private readonly CoreVoyageSession session;
        public CoreGameplayDemoController(CoreVoyageSession value) { session = value; }
        public static string ProbeExpeditionDetail(string detail, VoyageEffect effect) =>
            AppendMissingTreasureExpeditionEffects(detail, effect);
        public string ProbeChoiceDetail(int index) => ChartEncounterChoiceDetail(index);
        public List<string> ProbeExplorationDetail(VoyageEffect effect)
        {
            var values = new List<string>();
            AppendExplorationChoiceDetails(values, effect);
            return values;
        }
    }
}

namespace ShatteredSail.Core.Tests
{
    public sealed class EncounterTextPresentationProbeTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("获得吊装绞盘。", "weathered_winch")]
        [TestCase("取得吊装绞盘。", "weathered_winch")]
        [TestCase("铁矿 +8；建立潮工营地。", "weathered_camp")]
        [TestCase("获得潜水钟。", "weathered_bell")]
        public void AuthoredToolAndCampAliasesDoNotAppendTheSameReward(string authored, string mark)
        {
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureMark = mark }), Is.EqualTo(authored));
        }

        [TestCase("moonlit_dawn")]
        [TestCase("moonlit_dusk")]
        [TestCase("moonlit_midnight")]
        public void LightingAnAltarAndItsResonanceAreEachRecognized(string mark)
        {
            const string authored = "点亮此相、共鸣 +1、诅咒 +1。";
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureMark = mark, TreasureResonance = 1, Curse = 1 }), Is.EqualTo(authored));
        }

        [Test]
        public void WitnessAndCreditRemainSingleRewardsDespiteOtherEffectsAndDifferentOrder()
        {
            const string authored = "信用 +2、声望 +8；食物 -12；获得证人证词。";
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureMark = "gilded_witness", TreasureCredit = 2 }), Is.EqualTo(authored));
        }

        [Test]
        public void ReorderedNumericEffectsAndCappedHeatRemovalDoNotRepeat()
        {
            const string authored = "清除至多 1 点通缉；声望 +10、信用 +2。";
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureCredit = 2, TreasureHeat = -1 }), Is.EqualTo(authored));
        }

        [Test]
        public void PartialAuthoredEffectsOnlyAppendTheMissingReward()
        {
            const string authored = "获得证人证词；信用 +2。";
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureMark = "gilded_witness", TreasureCredit = 2, TreasureHeat = 1 }),
                Is.EqualTo(authored + "\n通缉 +1"));
        }

        [Test]
        public void NumericPrefixIsNotMistakenForTheSameAmount()
        {
            const string authored = "信用 +10、通缉 -10、共鸣 +10。";
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(authored,
                new VoyageEffect { TreasureCredit = 1, TreasureHeat = -1, TreasureResonance = 1 }),
                Is.EqualTo(authored + "\n信用 +1、通缉 -1、共鸣 +1"));
        }

        [Test]
        public void AbsentProseStillReceivesAllMissingExpeditionInformation()
        {
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail(null,
                new VoyageEffect { TreasureMark = "gilded_manifest", TreasureCredit = 3, TreasureHeat = 2 }),
                Is.EqualTo("取得公证货单、信用 +3、通缉 +2"));
            Assert.That(CoreGameplayDemoController.ProbeExpeditionDetail("金币 +20", null), Is.EqualTo("金币 +20"));
        }

        [Test]
        public void LearnedDiscoveryAndVisibleTargetDisappearWhileOtherEffectsRemain()
        {
            CoreVoyageSession session = Ready();
            CoreMapNode target = Target(session);
            target.Discovered = target.IntelRevealed = false;
            target.Visited = false;
            var controller = new CoreGameplayDemoController(session);
            var effect = new VoyageEffect { GrantDiscovery = "reef_dock_chart", RevealEncounterId = target.Encounter.Id, SailingBoostHours = 6 };
            CollectionAssert.AreEqual(new[] { "航速 +25% / 6 小时", "取得潮工船坞图（本航次）", "标出「潮痕秘藏」" },
                controller.ProbeExplorationDetail(effect));

            typeof(CoreVoyageSession).GetMethod("ApplyExplorationEffect", Hidden).Invoke(session, new object[] {
                new VoyageEffect { GrantDiscovery = "reef_dock_chart" } });
            target.IntelRevealed = true;
            CollectionAssert.AreEqual(new[] { "航速 +25% / 6 小时" }, controller.ProbeExplorationDetail(effect));
            target.Visited = true;
            CollectionAssert.AreEqual(new[] { "航速 +25% / 6 小时" }, controller.ProbeExplorationDetail(effect));
            session.Map.Nodes.Remove(target);
            CollectionAssert.AreEqual(new[] { "航速 +25% / 6 小时" }, controller.ProbeExplorationDetail(effect));
        }

        [Test]
        public void TargetPreviewUsesTheSameNearestUnvisitedLocationAsResolution()
        {
            CoreVoyageSession session = Ready();
            CoreMapNode near = Target(session);
            near.Position = session.Position;
            near.Visited = false;
            near.IntelRevealed = true;
            var far = new CoreMapNode { Id = 999, Encounter = near.Encounter,
                Position = new VoyagePoint(session.Position.X + 50f, session.Position.Y), Visited = false };
            session.Map.Nodes.Add(far);
            var controller = new CoreGameplayDemoController(session);
            var effect = new VoyageEffect { RevealEncounterId = near.Encounter.Id };
            Assert.That(controller.ProbeExplorationDetail(effect), Is.Empty,
                "A farther hidden duplicate must not be advertised when resolution selects the visible nearest site.");
            near.Visited = true;
            CollectionAssert.AreEqual(new[] { "标出「潮痕秘藏」" }, controller.ProbeExplorationDetail(effect));
        }

        [Test]
        public void TreasureClueRequirementIsKeptInConditionsInsteadOfBattleRewardProse()
        {
            CoreVoyageSession session = Ready();
            var choice = new CoreEncounterChoice("挑战", "金币 +20", 1, new VoyageEffect { Gold = 20 },
                startsBattle: true, requiredTreasureClues: 4);
            typeof(CoreVoyageSession).GetField("activeNode", Hidden).SetValue(session, null);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session,
                new CoreEncounter("text_probe", "测试", "测试", "测试", false, false, 1, 0, choice));
            Assert.That(session.EncounterChoiceConditionSummary(choice), Does.Contain("藏宝线索≥4"));
            Assert.That(new CoreGameplayDemoController(session).ProbeChoiceDetail(0), Does.Not.Contain("需要藏宝线索"));
        }

        private static CoreVoyageSession Ready()
        {
            var session = new CoreVoyageSession();
            session.SetSeed("ENCOUNTER-TEXT-PROBE-20261104");
            session.StartVoyage();
            return session;
        }

        private static CoreMapNode Target(CoreVoyageSession session) =>
            session.Map.Nodes.Single(node => node.Encounter.Id == "event_reef_hidden_cache");
    }
}
