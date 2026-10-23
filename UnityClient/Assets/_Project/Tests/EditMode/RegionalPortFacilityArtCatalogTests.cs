using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class RegionalPortFacilityArtCatalogTests
    {
        private static readonly string[] ExpectedPorts =
        {
            "断锚港",
            "白贝港",
            "铜钟港",
            "盐冠港",
            "暮潮港",
            "灰鸥港",
        };

        [Test]
        public void ResourceContract_AssignsUniqueCompositionPathToEveryPortAndFacility()
        {
            CollectionAssert.AreEqual(ExpectedPorts, PortFacilityArtCatalog.SupportedRegionalPorts);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    PortFacilityArtCatalog.WarehouseFacilityIndex,
                    PortFacilityArtCatalog.TavernFacilityIndex,
                    PortFacilityArtCatalog.ComponentShopFacilityIndex,
                    PortFacilityArtCatalog.MaintenanceFacilityIndex,
                    PortFacilityArtCatalog.VoyageFacilityIndex,
                },
                PortFacilityArtCatalog.SupportedRegionalFacilities);

            HashSet<string> resourcePaths = new HashSet<string>();
            foreach (int facilityIndex in PortFacilityArtCatalog.SupportedRegionalFacilities)
            {
                foreach (string portName in PortFacilityArtCatalog.SupportedRegionalPorts)
                {
                    string path = PortFacilityArtCatalog.RegionalBackgroundResourcePath(facilityIndex, portName);
                    Assert.That(path, Is.Not.Empty, facilityIndex + " / " + portName + " is missing its art path.");
                    Assert.That(resourcePaths.Add(path), Is.True, path + " aliases another port or facility.");
                }
            }

            Assert.That(resourcePaths.Count, Is.EqualTo(PortFacilityArtCatalog.ExpectedRegionalBackgroundCount));
            Assert.That(
                PortFacilityArtCatalog.RegionalBackgroundResourcePath(PortFacilityArtCatalog.TavernFacilityIndex, "未知港口"),
                Is.Empty,
                "An unknown port must not silently display another port's composition.");
            Assert.That(
                PortFacilityArtCatalog.RegionalBackgroundResourcePath(1, "断锚港"),
                Is.Empty,
                "Facilities outside the regional contract should retain their existing background lookup.");
            Assert.That(PortFacilityArtCatalog.RegionalBackgroundResourcePath(
                    PortFacilityArtCatalog.VoyageFacilityIndex, "断锚港"),
                Is.EqualTo("PortFacilities/Regional/Voyage/voyage_broken_anchor_crew_integrated_v1"),
                "The regional painting integrates the crew desk while leaving the active vessel as an independent layer.");
        }

        [Test]
        public void CommissionedRegionalArt_AllThirtyBackgroundsLoadAsDistinctTextures()
        {
            PortFacilityArtCatalog catalog = new PortFacilityArtCatalog();
            Assert.That(catalog.MissingRegionalBackgroundPaths(), Is.Empty,
                "A generic scene fallback is not completion of a regional art commission.");
            Assert.That(catalog.LoadedRegionalBackgroundCount, Is.EqualTo(30));
            Assert.That(catalog.AreAllRegionalBackgroundsAvailable, Is.True);
            HashSet<Texture2D> backgrounds = new HashSet<Texture2D>();
            foreach (int facility in PortFacilityArtCatalog.SupportedRegionalFacilities)
            {
                foreach (string port in PortFacilityArtCatalog.SupportedRegionalPorts)
                {
                    Texture2D background = catalog.RegionalBackground(facility, port);
                    Assert.That(backgrounds.Add(background), Is.True, port + " / " + facility + " must own a distinct image.");
                    Assert.That(background.width, Is.GreaterThanOrEqualTo(1024));
                    Assert.That(background.height, Is.GreaterThanOrEqualTo(512));
                    Assert.That(background.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                }
            }
        }

        [Test]
        public void TavernTitles_FollowPortRatherThanTutorialHarbour()
        {
            HashSet<string> titles = new HashSet<string>();
            foreach (string port in ExpectedPorts)
            {
                string title = PortFacilityArtCatalog.RegionalFacilityTitle(PortFacilityArtCatalog.TavernFacilityIndex, port, "灰鸥酒馆");
                Assert.That(title, Does.EndWith("酒馆"));
                Assert.That(titles.Add(title), Is.True, port + " reuses another harbour's tavern title.");
            }
            Assert.That(PortFacilityArtCatalog.RegionalFacilityTitle(PortFacilityArtCatalog.ComponentShopFacilityIndex, "铜钟港", "部件商店"), Is.EqualTo("部件商店"));
        }

        [Test]
        public void MissingRegionalArt_FallsBackToExistingFacilityWithoutCrossPortAliasing()
        {
            PortFacilityArtCatalog catalog = new PortFacilityArtCatalog();
            foreach (int facilityIndex in PortFacilityArtCatalog.SupportedRegionalFacilities)
            {
                Texture2D generic = catalog.Background(facilityIndex);
                Assert.That(generic, Is.Not.Null);
                Assert.That(catalog.Background(facilityIndex, "未知港口"), Is.SameAs(generic));

                foreach (string portName in PortFacilityArtCatalog.SupportedRegionalPorts)
                {
                    Texture2D regional = catalog.RegionalBackground(facilityIndex, portName);
                    Assert.That(
                        catalog.Background(facilityIndex, portName),
                        Is.SameAs(regional != null ? regional : generic));
                }
            }
        }

        private static IEnumerable<TestCaseData> RegionalPreviewCases()
        {
            for (int port = 0; port < ExpectedPorts.Length; port += 1)
            {
                yield return new TestCaseData(CoreGameplayPreviewScreen.Warehouse, (CoreGameplayPreviewPort)port,
                    ExpectedPorts[port], PortFacilityArtCatalog.WarehouseFacilityIndex, "Warehouse Background");
                yield return new TestCaseData(CoreGameplayPreviewScreen.Tavern, (CoreGameplayPreviewPort)port,
                    ExpectedPorts[port], PortFacilityArtCatalog.TavernFacilityIndex, "Tavern Background");
                yield return new TestCaseData(CoreGameplayPreviewScreen.ComponentShop, (CoreGameplayPreviewPort)port,
                    ExpectedPorts[port], PortFacilityArtCatalog.ComponentShopFacilityIndex, "ComponentShop Background");
                yield return new TestCaseData(CoreGameplayPreviewScreen.Maintenance, (CoreGameplayPreviewPort)port,
                    ExpectedPorts[port], PortFacilityArtCatalog.MaintenanceFacilityIndex, "Maintenance Background");
                yield return new TestCaseData(CoreGameplayPreviewScreen.VoyageDeparture, (CoreGameplayPreviewPort)port,
                    ExpectedPorts[port], PortFacilityArtCatalog.VoyageFacilityIndex, "Voyage Background");
            }
        }

        [TestCaseSource(nameof(RegionalPreviewCases))]
        public void EditorPreview_UsesSelectedPortsFacilityComposition(
            CoreGameplayPreviewScreen screen,
            CoreGameplayPreviewPort port,
            string portName,
            int facilityIndex,
            string backgroundObjectName)
        {
            GameObject previewHost = new GameObject("Regional Facility Preview Test Host");
            try
            {
                CoreGameplayDemoController controller = previewHost.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(screen, port, 0);

                Assert.That(controller.Session.CurrentPort, Is.EqualTo(portName));
                Transform preview = previewHost.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
                Assert.That(preview, Is.Not.Null);
                Transform backgroundObject = FindDescendant(preview, backgroundObjectName);
                Assert.That(backgroundObject, Is.Not.Null);

                PortFacilityArtCatalog catalog = new PortFacilityArtCatalog();
                Texture2D expected = screen == CoreGameplayPreviewScreen.Maintenance
                    ? catalog.Background(facilityIndex)
                    : catalog.RegionalBackground(facilityIndex, portName);
                if (expected == null) expected = catalog.Background(facilityIndex);
                if (screen == CoreGameplayPreviewScreen.ComponentShop)
                    expected = Resources.Load<Texture2D>("ComponentShopV3/shipyard_hall") ?? expected;

                RawImage background = backgroundObject.GetComponent<RawImage>();
                Assert.That(background.texture, Is.SameAs(expected));
                Assert.That(background.color, Is.EqualTo(Color.white),
                    "Regional compositions must be presented in authored colors rather than per-port tinting.");
                Image tint = FindDescendant(preview, "Facility Backdrop Tint").GetComponent<Image>();
                Assert.That(tint.color.a, Is.Zero,
                    "Regional facility scenes should not be recolored by the legacy dark overlay.");

                if (screen == CoreGameplayPreviewScreen.Warehouse || screen == CoreGameplayPreviewScreen.Maintenance ||
                    screen == CoreGameplayPreviewScreen.VoyageDeparture && PortFacilityArtCatalog.UsesPaintedDepartureScene(portName))
                {
                    Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
                    Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
                    Assert.That(background.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                        "Scene hit targets use the complete painting, without the legacy phase crop.");
                }

                if (screen == CoreGameplayPreviewScreen.Tavern)
                {
                    Assert.That(FindDescendant(preview, "Port Facility Title").GetComponent<Text>().text,
                        Is.EqualTo(PortFacilityArtCatalog.RegionalFacilityTitle(facilityIndex, portName, "酒馆")));
                }

                if (screen == CoreGameplayPreviewScreen.ComponentShop)
                {
                    Assert.That(FindDescendant(preview, "Component Shop Merchant"), Is.Null,
                        "The showroom now retains only components and their standing signs.");
                    Assert.That(preview.GetComponentsInChildren<Transform>(true)
                        .Any(item => item.name.StartsWith("Component Shop Mount")), Is.False);
                    Assert.That(FindDescendant(preview, "Component Shop Ship Preview"), Is.Null,
                        "The obsolete lower-right ship thumbnail must not return.");
                }

                if (screen == CoreGameplayPreviewScreen.Maintenance)
                {
                    Assert.That(FindDescendant(preview, "Port Investment Panel"), Is.Not.Null);
                    Assert.That(FindDescendant(preview, "Port Investment Summary"), Is.Not.Null);
                    Assert.That(FindDescendant(preview, "Port Investment Ledger"), Is.Not.Null);
                    Assert.That(FindDescendant(preview, "Confirm Port Investment"), Is.Not.Null);
                    Assert.That(FindDescendant(preview, "Open Port Market"), Is.Not.Null);
                    Assert.That(FindDescendant(preview, "Maintenance Supply Panel"), Is.Null);
                    Assert.That(FindDescendant(preview, "Maintenance Water Foreground"), Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(previewHost);
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == objectName);
        }
    }
}
