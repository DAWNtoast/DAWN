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
        [UnityTest, Timeout(120000)]
        public IEnumerator PortChrome_RetainsHeaderBackdropAndSingleNavigationActionAcrossAllFacilities()
        {
            RequirePortChromeImplementation();
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            GameObject hub = GameObject.Find("Port Hub");
            RawImage backdrop = FindPortChromeBackdrop(controller);
            Assert.That(hub, Is.Not.Null);
            Assert.That(backdrop, Is.Not.Null);
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            GameObject header = GameObject.Find("Port Facility Header");
            Button next = ActiveButton("Next Port Facility");
            Assert.That(header, Is.Not.Null);
            Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
            string[] expectedSections = { "船长收藏室", "皇家船坞", "灰鸥酒馆", "部件商店", "卡组工坊", "藏宝图桌", "港口投资建设", "出航码头", "港口市场" };
            string[] expectedBackdropNames = { "Warehouse", "Shipyard", "Tavern", "ComponentShop", "CardWorkshop", "Treasure", "Maintenance", "Voyage", "Market" };
            for (int index = 0; index < expectedSections.Length; index++)
            {
                Assert.That(ActiveButton("Next Port Facility"), Is.SameAs(next));
                ClickAcceptanceButton("Next Port Facility"); yield return null;
                Assert.That(controller.ActivePortSection, Is.EqualTo(expectedSections[index]),
                    "One click must advance exactly one facility after repeatedly rebinding the retained header.");
                Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
                Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
                Assert.That(backdrop.name, Is.EqualTo(expectedBackdropNames[index] + " Background"));
                Assert.That(header.activeInHierarchy, Is.True);
                Assert.That(backdrop.raycastTarget, Is.False);
                Assert.That(controller.GetComponentsInChildren<Transform>(true).Count(item => item.name == "Port Facility Header"), Is.EqualTo(1));
            }
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.SameAs(hub));
            Assert.That(header.activeInHierarchy, Is.False);
            Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
            Assert.That(backdrop.name, Is.EqualTo("Port Hub Background"));
            ClickAcceptanceButton("Port Hotspot Warehouse"); yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(ActiveButton("Next Port Facility"), Is.SameAs(next));
            Assert.That(controller.ActivePortSection, Is.EqualTo("船长收藏室"));
            ClickAcceptanceButton("Previous Port Facility"); yield return null;
            Assert.That(controller.ActivePortSection, Is.EqualTo("港口市场"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortChrome_RefreshesRetainedFacilityAndHubReadoutsAfterPortChange()
        {
            RequirePortChromeImplementation();
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            GameObject hub = GameObject.Find("Port Hub");
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            GameObject header = GameObject.Find("Port Facility Header");
            RawImage backdrop = FindPortChromeBackdrop(controller);
            Text facilityPort = GameObject.Find("Port Facility Port").GetComponent<Text>();
            Text facilityGold = GameObject.Find("Port Facility Gold").GetComponent<Text>();
            Assert.That(facilityPort.text, Is.EqualTo("断锚港"));
            // Explicit port-arrival fixture; only the following navigation uses UI.
            controller.Session.RestoreProfile(4321.25m, "白贝港");
            ClickAcceptanceButton("Next Port Facility"); yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
            Assert.That(GameObject.Find("Port Facility Port").GetComponent<Text>(), Is.SameAs(facilityPort));
            Assert.That(GameObject.Find("Port Facility Gold").GetComponent<Text>(), Is.SameAs(facilityGold));
            Assert.That(facilityPort.text, Is.EqualTo("白贝港"));
            Assert.That(facilityGold.text, Does.Contain(PortMarketCatalog.FormatPrice(controller.Session.ProfileGold)));
            Assert.That(GameObject.Find("Port Facility Title").GetComponent<Text>().text,
                Is.EqualTo(PortFacilityArtCatalog.RegionalFacilityTitle(0, "白贝港", "船长收藏室")));
            ClickAcceptanceButton("Return To Port Hub"); yield return null;
            Assert.That(GameObject.Find("Port Hub"), Is.SameAs(hub));
            Assert.That(GameObject.Find("Port Hub Port").GetComponent<Text>().text, Is.EqualTo("白贝港"));
            Assert.That(GameObject.Find("Port Hub Gold").GetComponent<Text>().text,
                Does.Contain(PortMarketCatalog.FormatPrice(controller.Session.ProfileGold)));
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(facilityPort.text, Is.EqualTo("白贝港"));
            Assert.That(GameObject.Find("Market Panel"), Is.Not.Null);
            Assert.That(GameObject.Find("Market Footer"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortChrome_TutorialSettingsSurviveRealStepChangesAndRepeatedRenders()
        {
            RequirePortChromeImplementation();
            // Saved-checkpoint fixture: enter the real second departure lesson,
            // then advance its steps only with the normal permitted UI actions.
            PlayerPrefs.SetInt(CoreGameplayDemoController.GuidedTutorialCheckpointKey, 2);
            yield return LoadGuidedTutorialScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SecondPier));
            Assert.That(controller.IsTutorialActive, Is.True);
            ClickGuidedButton("Port Hub Settings"); yield return null;
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null);
            ClickAcceptanceButton("Close Port Settings"); yield return null;
            ClickGuidedButton("Port Hotspot Voyage"); yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SecondRoute));
            GameObject header = GameObject.Find("Port Facility Header");
            Button settings = ActiveButton("Port Facility Settings");
            Assert.That(settings, Is.Not.Null);
            Button.ButtonClickedEvent oldStepClick = settings.onClick;
            ClickGuidedButton("Port Facility Settings"); yield return null;
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null);
            ClickAcceptanceButton("Close Port Settings"); yield return null;
            ClickGuidedButton("Route 0"); yield return null;
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SecondDeparture));
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(ActiveButton("Port Facility Settings"), Is.SameAs(settings));
            oldStepClick.Invoke(); yield return null;
            Assert.That(GameObject.Find("Port Settings Overlay"), Is.Null,
                "Rebinding the current header must not revive an event retained from an earlier tutorial step.");
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int refresh = 0; refresh < 3; refresh++)
            {
                render.Invoke(controller, null); yield return null;
                Assert.That(controller.IsTutorialActive, Is.True);
                Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.SecondDeparture));
                Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
                Assert.That(ActiveButton("Port Facility Settings"), Is.SameAs(settings));
                ClickGuidedButton("Port Facility Settings"); yield return null;
                Assert.That(GameObject.Find("Port Settings Overlay"), Is.Not.Null,
                    "Current-step Settings must not be trapped behind a nested callback from the retired step.");
                Assert.That(controller.GetComponentsInChildren<Transform>().Count(item => item.name == "Port Settings Overlay"), Is.EqualTo(1));
                ClickAcceptanceButton("Close Port Settings"); yield return null;
            }
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey, 0), Is.Zero);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator PortChrome_ParksWhileSailingAndRestoresTheSameViewsAfterSafeReturn()
        {
            RequirePortChromeImplementation();
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            GameObject hub = GameObject.Find("Port Hub");
            RawImage backdrop = FindPortChromeBackdrop(controller);
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 100;
            controller.Session.Cargo.Wood = 10;
            controller.Session.Cargo.Powder = 10;
            controller.Session.Cargo.Cannonballs = 20;
            ClickAcceptanceButton("Port Hotspot Voyage"); yield return null;
            GameObject header = GameObject.Find("Port Facility Header");
            Assert.That(header, Is.Not.Null);
            ClickAcceptanceButton("Start Voyage"); yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(header, Is.Not.Null, "The port header must be parked, not destroyed at departure.");
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(header.activeInHierarchy, Is.False);
            Assert.That(hub.activeInHierarchy, Is.False);
            Assert.That(backdrop.gameObject.activeInHierarchy, Is.False);
            Assert.That(FindPortChromeBackdrop(controller), Is.Null);
            Assert.That(GameObject.Find("Voyage Header"), Is.Not.Null);
            ClickAcceptanceButton("Safe Return"); yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(backdrop.gameObject.activeInHierarchy, Is.False);
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
            { ClickAcceptanceButton("Skip Settlement Reveal"); yield return null; }
            ClickAcceptanceButton("Return To Port"); yield return null;
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(GameObject.Find("Port Hub"), Is.SameAs(hub));
            Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
            Assert.That(backdrop.name, Is.EqualTo("Port Hub Background"));
            Assert.That(backdrop.texture, Is.Not.Null);
            Assert.That(backdrop.color.a, Is.EqualTo(1f));
            Assert.That(backdrop.gameObject.activeInHierarchy, Is.True);
            Assert.That(GameObject.Find("Voyage Header"), Is.Null);
            ClickAcceptanceButton("Port Hotspot Market"); yield return null;
            Assert.That(GameObject.Find("Port Facility Header"), Is.SameAs(header));
            Assert.That(FindPortChromeBackdrop(controller), Is.SameAs(backdrop));
            yield return OpenMarketItem("wood");
            Assert.That(ActiveButton("Market Item Icon wood"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private static void RequirePortChromeImplementation()
        {
            // Reflection keeps this source compilable when copied beside the baseline.
            if (typeof(CoreGameplayDemoController).GetField("cachedFacilityHeader", BindingFlags.Instance | BindingFlags.NonPublic) == null)
                Assert.Ignore("The baseline has no retained port header; run this behavior assertion after optimization.");
        }

        private static RawImage FindPortChromeBackdrop(CoreGameplayDemoController controller) =>
            controller.GetComponentsInChildren<RawImage>().SingleOrDefault(image =>
                image.name.EndsWith(" Background", StringComparison.Ordinal) && image.transform.parent != null &&
                image.transform.parent.name == "Phase Content");
    }
}
