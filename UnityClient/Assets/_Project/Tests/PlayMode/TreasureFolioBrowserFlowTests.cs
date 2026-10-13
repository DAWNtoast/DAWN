using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator TreasureFolio_PointerBrowsingPagesAndHistoryPreservesTheSelectedSeedAndInventory()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 900, fragments = 8, selectedMapId = "folio-unused-0",
                maps = Enumerable.Range(0, 3).Select(index => new CoreTreasureMapSave
                {
                    id = "folio-unused-" + index, seed = "POINTER-UNUSED-" + index,
                    rankId = CoreTreasureHuntCatalog.Ranks[index].Id,
                }).ToArray(),
                usedMaps = Enumerable.Range(0, 3).Select(index => new CoreUsedTreasureMapSave
                {
                    id = "folio-used-" + index, seed = "POINTER-USED-" + index,
                    rankId = CoreTreasureHuntCatalog.Ranks[index].Id, departurePort = "断锚港",
                    hasExplorationCoverage = true, explorationCoverage = 37.5f + index,
                }).ToArray(),
            });
            string progress = JsonUtility.ToJson(session.CaptureTreasureProgress());
            int route = session.SelectedRouteIndex;
            string destination = session.SelectedDestinationPort;
            decimal gold = session.ProfileGold;
            bool saved = PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey);
            string savedProgress = PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey);

            yield return ClickTreasureFolioPointer(controller, "Port Hotspot Treasure");
            yield return ClickTreasureFolioPointer(controller, "Treasure Open Folio");
            Assert.That(GameObject.Find("Treasure Folio").GetComponent<Image>().sprite,
                Is.SameAs(NauticalSurfaceSkin.ReadingPaper));
            Assert.That(GameObject.Find("Treasure Inventory Summary").GetComponent<Text>().text,
                Is.EqualTo("未使用的藏宝图 · 3 张"));

            yield return ClickTreasureFolioPointer(controller, "Treasure Browse Unused");
            AssertTreasureFolioPointerPage(session, false, 0);
            yield return ClickTreasureFolioPointer(controller, "Treasure Folio Next Page");
            AssertTreasureFolioPointerPage(session, false, 1);
            yield return ClickTreasureFolioPointer(controller, "Treasure Folio Previous Page");
            AssertTreasureFolioPointerPage(session, false, 0);
            yield return ClickTreasureFolioPointer(controller, "Treasure Folio Next Page");

            // Switching lists from page two must start the other list on page
            // one, and its sea-chart geometry must match that record's seed.
            yield return ClickTreasureFolioPointer(controller, "Treasure Browse Used");
            AssertTreasureFolioPointerPage(session, true, 0);
            yield return ClickTreasureFolioPointer(controller, "Treasure Folio Next Page");
            AssertTreasureFolioPointerPage(session, true, 1);
            yield return ClickTreasureFolioPointer(controller, "Treasure Folio Back");
            Assert.That(GameObject.Find("Treasure Folio Title").GetComponent<Text>().text, Is.EqualTo("藏图册"));
            Assert.That(GameObject.Find("Treasure Folio Card 0"), Is.Null);
            yield return ClickTreasureFolioPointer(controller, "Treasure Close Folio");
            Assert.That(GameObject.Find("Treasure Folio"), Is.Null);

            yield return ClickTreasureFolioPointer(controller, "Treasure Open Folio");
            yield return ClickTreasureFolioPointer(controller, "Treasure Browse Unused");
            AssertTreasureFolioPointerPage(session, false, 0);
            yield return ClickTreasureFolioPointer(controller, "Treasure Close Folio");
            Assert.That(JsonUtility.ToJson(session.CaptureTreasureProgress()), Is.EqualTo(progress));
            Assert.That(session.SelectedTreasureMap.Id, Is.EqualTo("folio-unused-0"));
            Assert.That(session.SelectedRouteIndex, Is.EqualTo(route));
            Assert.That(session.SelectedDestinationPort, Is.EqualTo(destination));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(saved));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(savedProgress));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator ClickTreasureFolioPointer(CoreGameplayDemoController controller, string name)
        {
            // This helper performs the existing real EventSystem raycast and
            // pointer-click dispatch; it never invokes Button.onClick directly.
            ClickAcceptanceButton(name);
            yield return WaitForItemIconView(controller);
        }

        private static void AssertTreasureFolioPointerPage(CoreVoyageSession session, bool used, int page)
        {
            Assert.That(GameObject.Find("Treasure Folio Page").GetComponent<Text>().text,
                Is.EqualTo((page + 1) + " / 2"));
            Assert.That(ActiveButton("Treasure Folio Previous Page").IsInteractable(), Is.EqualTo(page > 0));
            Assert.That(ActiveButton("Treasure Folio Next Page").IsInteractable(), Is.EqualTo(page == 0));
            int count = page == 0 ? 2 : 1;
            Assert.That(Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Count(item => item.name.StartsWith("Treasure Folio Card ")), Is.EqualTo(count));
            for (int slot = 0; slot < count; slot++)
            {
                int index = page * 2 + slot;
                Text details = GameObject.Find("Treasure Map Details " + slot).GetComponent<Text>();
                Assert.That(details.text, Does.Contain(CoreTreasureHuntCatalog.Ranks[index].Name)
                    .And.Contain((used ? "POINTER-USED-" : "POINTER-UNUSED-") + index));
                RawImage image = GameObject.Find("Treasure Map Preview " + slot).GetComponent<RawImage>();
                Assert.That(image.texture, Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False, "Map art must leave page controls reachable.");
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                if (used)
                {
                    VoyageChartSurfaceGraphic surface = image.GetComponent<VoyageChartSurfaceGraphic>();
                    Assert.That(surface, Is.Not.Null);
                    Assert.That(surface.Field, Is.Not.Null);
                    CoreVoyageMap map = session.PreviewUsedTreasureMap(session.UsedTreasureMaps[index]);
                    Assert.That(surface.Field.GeometryFingerprint, Is.EqualTo(VoyageChartCoastField.Fingerprint(map)));
                    Assert.That(surface.Field.Width, Is.EqualTo(CoreVoyageMap.GridColumns * VoyageChartCoastField.SamplesPerCell + 1));
                    Assert.That(surface.Field.Height, Is.EqualTo(CoreVoyageMap.GridRows * VoyageChartCoastField.SamplesPerCell + 1));
                    Assert.That(details.text, Does.Contain((37.5f + index).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%"));
                }
                else Assert.That(image.texture.name, Is.EqualTo("treasure_chart_" + CoreTreasureHuntCatalog.Ranks[index].Id));
            }
        }
    }
}
