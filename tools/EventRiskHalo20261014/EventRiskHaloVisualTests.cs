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
    // Validation-only live scene fixture. Named catalog cases express the user
    // visible expectations; this does not reproduce the risk classification rule.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator EventRiskHalo_CatalogColorsVisibilityCompletionAndPointerSelection()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            Assert.That(session.TrySelectChartSeed("SS3.0.0.1.0.0.6286796D.78C3DA98.0D9FC4B6", out string reason), Is.True, reason);
            session.StartVoyage();
            CoreMapNode supply = session.Map.Nodes.Single(n => n.Encounter.Id == "event_reef_fisher_cache");
            CoreMapNode salvage = session.Map.Nodes.Single(n => n.Encounter.Id == "event_reef_sunken_boat");
            CoreMapNode battle = session.Map.Nodes.Single(n => n.Encounter.Id == "event_reef_coral_ambush");
            CoreMapNode boss = session.Map.Nodes.Single(n => n.IsBoss);
            CoreMapNode hidden = session.Map.Nodes.Single(n => n.Encounter.Id == "common_whale_song");
            CoreMapNode completed = session.Map.Nodes.Single(n => n.Encounter.Id == "event_reef_broken_mast");
            session.Map.Nodes.Clear();
            session.Map.Nodes.AddRange(new[] { supply, salvage, battle, boss, hidden, completed });
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.ExploredCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.VoyageExploredCells, 0, session.Map.CellCount);
            Array.Clear(session.Map.ChartExploredCells, 0, session.Map.ChartExploredCells.Length);
            supply.Position = new VoyagePoint(28f, 40f);
            salvage.Position = new VoyagePoint(43f, 40f);
            battle.Position = new VoyagePoint(58f, 40f);
            boss.Position = new VoyagePoint(73f, 40f);
            hidden.Position = new VoyagePoint(20f, 57f);
            completed.Position = new VoyagePoint(43f, 24f);
            foreach (CoreMapNode node in session.Map.Nodes) { node.Discovered = true; node.IntelRevealed = false; node.Visited = false; }
            // Actual port sight activates the existing stronger gold boss cue.
            ObserveHaloPoint(session, session.Map.Destination);
            foreach (CoreMapNode node in new[] { supply, salvage, battle, boss, completed }) ObserveHaloPoint(session, node.Position);
            hidden.Discovered = hidden.IntelRevealed = false;
            completed.Visited = true;
            SetDestinationTestPosition(session, new VoyagePoint(58f, 24f));
            session.SetHeading(SailingDirection.East);
            RenderDestinationBossFixture(controller);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            Graphic green = HaloFor(supply);
            Graphic red = HaloFor(salvage);
            Graphic combat = HaloFor(battle);
            Assert.That(green, Is.Not.Null, "The known supply stop needs a visible cue.");
            Assert.That(red, Is.Not.Null, "Risk 1 salvage with possible hull loss still needs a warning.");
            Assert.That(combat, Is.Not.Null);
            Assert.That(green.color.g, Is.GreaterThan(green.color.r));
            Assert.That(red.color.r, Is.GreaterThan(red.color.g));
            Assert.That(combat.color.r, Is.GreaterThan(combat.color.g));
            Assert.That(GameObject.Find("Event Node " + hidden.Id), Is.Null, "Unseen fog must not reveal an event or its glow.");
            Assert.That(HaloFor(completed), Is.Null);
            Assert.That(HaloFor(boss), Is.Null, "The active gold boss guidance must not stack a second risk halo.");
            Assert.That(GameObject.Find("Destination Boss Guidance Halo"), Is.Not.Null);
            Assert.That(GameObject.Find("Destination Boss Guidance Ring"), Is.Not.Null);
            NavalBattleVisualCapture.Capture(controller, "Event_Risk_Halo_Comparison.png",
                "左至右：海民储藏箱弱绿、半沉小船弱红、珊瑚伏击弱红、首领强金；下方完成点无光圈，未发现点不显示", "Navigation Chart");

            var costs = new NavigationArrivalCosts(session);
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + supply.Id).gameObject, Vector2.zero);
            costs.AssertUnchanged(session);
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertDestinationTestPoint(session.DirectCourseTarget, supply.Position);
            yield return ClickDestinationTestGraphic(ActiveButton("Event Node " + salvage.Id).gameObject, Vector2.zero);
            costs.AssertUnchanged(session);
            AssertDestinationTestPoint(session.DirectCourseTarget, salvage.Position);
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.ActiveEncounter, Is.Null);
            supply.Visited = true;
            RenderDestinationBossFixture(controller);
            yield return null;
            Assert.That(HaloFor(supply), Is.Null, "Completion removes the existing green cue on refresh.");
            Assert.That(HaloFor(completed), Is.Null);
            Assert.That(GameObject.Find("Event Node " + hidden.Id), Is.Null);
            Assert.That(HaloFor(salvage), Is.Not.Null);
            costs.AssertUnchanged(session);
            NavalBattleVisualCapture.Capture(controller, "Event_Risk_Halo_Completed_And_Locked.png",
                "供给点完成后绿光圈消失；真实点击仍锁定半沉小船，没有自动航行或额外物资消耗", "Navigation Chart");
            LogAssert.NoUnexpectedReceived();
        }

        private static Graphic HaloFor(CoreMapNode node) =>
            GameObject.Find("Event Node " + node.Id)?.transform.Find("Event Risk Halo")?.GetComponent<Graphic>();
        private static void ObserveHaloPoint(CoreVoyageSession session, VoyagePoint point) =>
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { point });
    }
}
