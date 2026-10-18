using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrew_MinimumOrdinaryCrewBlocksNormalVoyageBelowFortyPercent() =>
            CheckMinimumDepartureOrdinaryCrew("ordinary");

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrew_MinimumOrdinaryCrewBlocksRecordedChartBelowFortyPercent() =>
            CheckMinimumDepartureOrdinaryCrew("chart");

        [UnityTest, Timeout(120000)]
        public IEnumerator DepartureCrew_MinimumOrdinaryCrewBlocksTreasureWithoutConsumingMapBelowFortyPercent() =>
            CheckMinimumDepartureOrdinaryCrew("treasure");

        private static IEnumerator CheckMinimumDepartureOrdinaryCrew(string mode)
        {
            CrewVariantDefinition[] local = CrewNarrativeCatalog.AllCrew.Where(item => item.PortName == "断锚港").ToArray();
            yield return LoadDepartureCrewOverlayFixture(local);
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            if (mode == "treasure")
                session.RestoreTreasureProgress(new CoreTreasureProgress
                {
                    pirateExperience = 1500,
                    maps = new[] { new CoreTreasureMapSave { id = "crew-minimum-map", rankId = "weathered", seed = "crew-minimum-seed" } },
                });
            yield return ClickShipyardControl("Port Hotspot Voyage");
            string seed = null;
            if (mode == "chart")
            {
                seed = CoreVoyageSession.CreateChartSeed(0, "断锚港", "白贝港");
                yield return EnterDepartureChartSeed(seed);
            }
            else if (mode == "treasure")
            {
                yield return ClickShipyardControl("Departure Treasure Seed Picker");
                yield return SelectDepartureTreasureOption(0);
                Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("crew-minimum-map"));
            }
            Assert.That(session.CrewCountValue, Is.EqualTo(10));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(6));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True, "Exactly 40% ordinary crew permits departure.");
            Button.ButtonClickedEvent previousStart = ActiveButton("Start Voyage").onClick;
            yield return ClickShipyardControl("DepartureOrdinaryCrewDecreaseButton");
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(3));
            Assert.That(ActiveButton("Start Voyage").GetComponentInChildren<Text>().text, Is.EqualTo("需4名普通船员"));
            Assert.That(session.EffectiveCrewCount, Is.EqualTo(9), "Six specialists do not satisfy the ordinary crew minimum.");
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False);
            InvestmentResourceSnapshot resources = SnapshotInvestmentResources(session);
            int water = session.Cargo.Water, food = session.Cargo.Food;
            string atSea = PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey, string.Empty);
            string[] maps = session.TreasureMaps.Select(map => map.Id).ToArray();
            previousStart.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port), "A retained callback from an enabled Start control must recheck current staffing.");
            AssertInvestmentResourcesEqual(session, resources);
            ActiveButton("Start Voyage").onClick.Invoke(); yield return null;
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Port), "Invoking a disabled Start control directly cannot bypass the core departure gate.");
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(3));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.TreasureMaps.Select(map => map.Id), Is.EqualTo(maps));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CrewAtSeaKey, string.Empty), Is.EqualTo(atSea));
            AssertInvestmentResourcesEqual(session, resources);
            yield return SetOrdinaryDepartureCount(controller, 0);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.False, "The dock still permits choosing zero ordinary crew without permitting departure.");
            yield return SetOrdinaryDepartureCount(controller, 4);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True, "Restoring exactly 40% re-enables the same route.");
            yield return ClickShipyardControl("Start Voyage");
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SelectedOrdinaryCrew, Is.EqualTo(4));
            Assert.That(session.SelectedSpecialCrewCount, Is.EqualTo(6));
            if (mode == "chart") Assert.That(session.CurrentChartSeed, Is.EqualTo(seed));
            if (mode == "treasure")
            {
                Assert.That(session.IsTreasureVoyage, Is.True);
                Assert.That(session.ActiveTreasureMap.Id, Is.EqualTo("crew-minimum-map"));
                Assert.That(session.TreasureMaps, Is.Empty, "The map is consumed only after staffing permits actual departure.");
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
