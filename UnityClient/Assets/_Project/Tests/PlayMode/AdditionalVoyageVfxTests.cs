using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator AdditionalVfx_EncounterCommitsOnceShowsRealAmountsAndNeverReplaysOnRender()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = AdditionalVoyageFixture(controller);
            var encounter = new CoreEncounter("additional_outcome_fixture", "漂流补给", "海上发现", "检查真实入库反馈。", true, false, 0, 0,
                new CoreEncounterChoice("打捞物资", "获得金币和木板", 0, new VoyageEffect { Gold = 17, Wood = 2, Hull = -3 }));
            var node = new CoreMapNode { Id = 9917, Position = session.Position, Encounter = encounter };
            session.Map.Nodes.Add(node);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            typeof(CoreVoyageSession).GetProperty("ActiveEncounter").SetValue(session, encounter);
            typeof(CoreVoyageSession).GetProperty("Phase").SetValue(session, CoreDemoPhase.Encounter);
            RenderResponsiveBattle(controller); yield return new WaitForSecondsRealtime(.35f);
            Button choice = RequiredResponsive<Button>("Encounter Choice 0");
            int gold = session.Cargo.Gold, hull = session.Hull;
            choice.onClick.Invoke();
            int committedGold = session.Cargo.Gold, committedHull = session.Hull;
            choice.onClick.Invoke();
            Assert.That(session.Cargo.Gold, Is.EqualTo(committedGold));
            Assert.That(session.Hull, Is.EqualTo(committedHull));
            Assert.That(committedGold, Is.GreaterThan(gold));
            Assert.That(committedHull, Is.LessThan(hull));
            yield return null;
            var motion = controller.GetComponentInChildren<VoyageOutcomeMotion>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.GetComponentsInChildren<Button>(), Is.Empty, "Automatic feedback has no skip or other actions.");
            Assert.That(motion.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
            string receipt = string.Join(" ", motion.GetComponentsInChildren<Text>().Select(t => t.text));
            Assert.That(receipt, Does.Contain("+" + (committedGold - gold)));
            Assert.That(receipt, Does.Contain("−" + (hull - committedHull)));
            motion.SetElapsed(.48f); yield return null;
            var chart = motion.transform.parent as RectTransform;
            Assert.That(chart.name, Is.EqualTo("Navigation Chart"));
            Assert.That(chart.gameObject.activeInHierarchy, Is.True, "Feedback must belong to the current chart, never the retired view awaiting Destroy.");
            var corners = new Vector3[4];
            foreach (RectTransform row in motion.transform)
            {
                row.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    Assert.That(chart.rect.Contains(chart.InverseTransformPoint(corner)), Is.True, "Every receipt stays inside the chart and away from sailing controls.");
            }
            NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_Encounter_Result.png", "遭遇真实物资与受损反馈，无跳过按钮", "Navigation Chart");
            RenderResponsiveBattle(controller); yield return null;
            Assert.That(controller.GetComponentInChildren<VoyageOutcomeMotion>(), Is.Null, "Ordinary refresh must not replay the receipt.");
            Assert.That(session.Cargo.Gold, Is.EqualTo(committedGold));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalVfx_DiscoveryOnlyCoversNewCellsAndNaturallyFinishes()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = AdditionalVoyageFixture(controller);
            session.Map.InitializeChartExploration();
            int[] cells = Enumerable.Range(0, session.Map.ChartExploredCells.Length)
                .Where(i => !session.Map.ChartExploredCells[i]).Take(3).ToArray();
            Assert.That(cells.Length, Is.EqualTo(3));
            typeof(CoreGameplayDemoController).GetMethod("CaptureVoyageDiscovery", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            foreach (int cell in cells) session.Map.ChartExploredCells[cell] = true;
            RenderResponsiveBattle(controller); yield return null;
            VoyageDiscoveryMotion motion = controller.GetComponentInChildren<VoyageDiscoveryMotion>();
            Assert.That(motion, Is.Not.Null); Assert.That(motion.RevealedCellCount, Is.EqualTo(3));
            Assert.That(motion.raycastTarget, Is.False);
            double hours = session.TotalHours;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (motion.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(motion.IsPlaying, Is.False);
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(cells.All(i => session.Map.ChartExploredCells[i]), Is.True);
            RenderResponsiveBattle(controller); yield return null;
            Assert.That(controller.GetComponentInChildren<VoyageDiscoveryMotion>(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalVfx_ActualSailingRevealsAlongTheCommittedLegWithoutExtraCosts()
        {
            yield return LoadPersistentCourseFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            GameObject.Find("Distance Slider").GetComponent<Slider>().value = 60f;
            yield return null;
            int known = Enumerable.Range(0, session.Map.FineRows).Sum(row =>
                Enumerable.Range(0, session.Map.FineColumns).Count(column => session.Map.IsChartCellExplored(column, row)));
            ActiveButton("Execute Sail").onClick.Invoke();
            double hours = session.TotalHours;
            int water = session.Cargo.Water, food = session.Cargo.Food;
            yield return WaitForNavigationMotionCompletion(controller);
            VoyageDiscoveryMotion motion = controller.GetComponentInChildren<VoyageDiscoveryMotion>();
            int now = Enumerable.Range(0, session.Map.FineRows).Sum(row =>
                Enumerable.Range(0, session.Map.FineColumns).Count(column => session.Map.IsChartCellExplored(column, row)));
            Assert.That(now, Is.GreaterThan(known));
            Assert.That(motion, Is.Not.Null); Assert.That(motion.RevealedCellCount, Is.EqualTo(now - known));
            motion.SetElapsed(.38f); yield return null;
            NavalBattleVisualCapture.Capture(controller, "AdditionalVfx_Voyage_Discovery.png", "实际航行动作后新探索海域渐显，无额外时间或补给扣除", "Navigation Chart");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (motion.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(motion.IsPlaying, Is.False);
            Assert.That(session.TotalHours, Is.EqualTo(hours));
            Assert.That(session.Cargo.Water, Is.EqualTo(water)); Assert.That(session.Cargo.Food, Is.EqualTo(food));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdditionalVfx_OutcomeStopsWhenOwnerIsDisabled()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var host = new GameObject("Outcome Lifetime Fixture", typeof(RectTransform));
            host.transform.SetParent(controller.transform, false);
            VoyageOutcomeMotion motion = host.AddComponent<VoyageOutcomeMotion>();
            Font font = controller.GetComponentsInChildren<Text>().First(t => t.font != null).font;
            motion.Play(controller, font, new[] { new VoyageOutcomeEntry("wood", "木板", 2) });
            controller.enabled = false; yield return null; yield return null;
            Assert.That(motion == null || !motion.IsPlaying, Is.True);
            Assert.That(host == null || !host.activeSelf, Is.True);
            controller.enabled = true; yield return null;
            Assert.That(controller.GetComponentInChildren<VoyageOutcomeMotion>(), Is.Null);
        }

        private static CoreVoyageSession AdditionalVoyageFixture(CoreGameplayDemoController controller)
        {
            var session = new CoreVoyageSession();
            session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            session.SetSeed("ADDITIONAL-VFX-20261104"); session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            typeof(CoreGameplayDemoController).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, session);
            return session;
        }
    }
}
