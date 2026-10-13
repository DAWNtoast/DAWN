using System.Collections;
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
        public IEnumerator RuntimeMemory_PortChromeReusesObjectsWithoutRetainingInactiveRegionalArt()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            yield return WaitForPortArtCollection(controller);
            PortHeaderView hub = PortArtTestField<PortHeaderView>(controller, "cachedHubHeader");
            RawImage backdrop = PortArtTestField<RawImage>(controller, "cachedPortBackdrop");
            Assert.That(hub.Frame.sprite != null && backdrop.texture != null, Is.True);
            int originalFrame = hub.Frame.sprite.GetInstanceID();
            ActiveButton("Port Hotspot Market").onClick.Invoke();
            yield return WaitForPortArtCollection(controller);
            PortHeaderView facility = PortArtTestField<PortHeaderView>(controller, "cachedFacilityHeader");
            Assert.That(hub.gameObject.activeInHierarchy, Is.False);
            Assert.That(hub.Frame.sprite, Is.Null, "An inactive retained header owns no regional art.");
            Assert.That(facility.Frame.sprite.GetInstanceID(), Is.EqualTo(originalFrame), "Current-port art is reused across facilities.");
            ActiveButton("Return To Port Hub").onClick.Invoke();
            yield return WaitForPortArtCollection(controller);
            Assert.That(PortArtTestField<PortHeaderView>(controller, "cachedHubHeader"), Is.SameAs(hub));
            Assert.That(hub.Frame.sprite.GetInstanceID(), Is.EqualTo(originalFrame));
            Assert.That(facility.Frame.sprite, Is.Null);

            // Set up a new region independently of voyage mechanics. This test
            // exercises the same binding/render path used after actual arrival.
            typeof(CoreVoyageSession).GetProperty("CurrentPort").SetValue(controller.Session, "白贝港");
            RenderPortArtTest(controller);
            yield return WaitForPortArtCollection(controller);
            Assert.That(hub.Frame.sprite.texture.name, Is.EqualTo("white_shell_header_v1"));
            Assert.That(backdrop.texture.name, Is.EqualTo("port_hub_white_shell_v3"));
            Assert.That(LoadedSpriteExists(originalFrame), Is.False, "The departed region's generated frame is no longer pinned.");
            Assert.That(PortArtTestField<PortHeaderView>(controller, "cachedHubHeader"), Is.SameAs(hub));

            int secondFrame = hub.Frame.sprite.GetInstanceID();
            controller.Session.Cargo.Water = controller.Session.Cargo.Food = 200;
            controller.Session.StartVoyage();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            RenderPortArtTest(controller);
            yield return WaitForPortArtCollection(controller);
            Assert.That(hub.Frame.sprite, Is.Null);
            Assert.That(facility.Frame.sprite, Is.Null);
            Assert.That(backdrop.texture, Is.Null, "The retained background shell must release its port painting at sea.");
            Assert.That(LoadedSpriteExists(secondFrame), Is.False);
            Assert.That(GameObject.Find("Voyage Header Frame").GetComponent<Image>().sprite.texture.name,
                Is.EqualTo("navigation_header_v3"), "The navigation header keeps its existing art.");

            Assert.That(controller.Session.SafeReturn(), Is.True);
            controller.Session.ReturnToPortAfterSettlement();
            RenderPortArtTest(controller);
            yield return WaitForPortArtCollection(controller);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(PortArtTestField<PortHeaderView>(controller, "cachedHubHeader"), Is.SameAs(hub));
            Assert.That(hub.Frame.sprite.texture.name, Is.EqualTo("white_shell_header_v1"));
            Assert.That(backdrop.texture.name, Is.EqualTo("port_hub_white_shell_v3"));
            Assert.That(hub.Settings.IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuntimeMemory_ReleasingHeaderCatalogNeverDestroysAFrameStillOwnedByAnImage()
        {
            yield return LoadProvisioningFixture();
            var catalog = new PortHeaderArtCatalog();
            var host = new GameObject("Borrowed Regional Header", typeof(RectTransform), typeof(Image));
            Image image = host.GetComponent<Image>();
            image.sprite = catalog.ForPort("暮潮港").Frame;
            int id = image.sprite.GetInstanceID();
            try
            {
                catalog.ReleaseUnusedThemes(null);
                yield return Resources.UnloadUnusedAssets();
                Assert.That(image.sprite != null && image.sprite.texture != null, Is.True);
                Assert.That(image.sprite.GetInstanceID(), Is.EqualTo(id));
                Object.Destroy(host);
                host = null;
                image = null;
                yield return null;
                yield return Resources.UnloadUnusedAssets();
                Assert.That(LoadedSpriteExists(id), Is.False);
                Assert.That(catalog.ForPort("暮潮港").Frame.texture != null, Is.True);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (host != null) Object.Destroy(host);
                catalog.Dispose();
            }
        }

        private static T PortArtTestField<T>(CoreGameplayDemoController controller, string name) =>
            (T)typeof(CoreGameplayDemoController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        private static void RenderPortArtTest(CoreGameplayDemoController controller) =>
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

        private static IEnumerator WaitForPortArtCollection(CoreGameplayDemoController controller)
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (PortArtTestField<Coroutine>(controller, "runtimeArtCollection") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(PortArtTestField<Coroutine>(controller, "runtimeArtCollection"), Is.Null, "The deferred resource collection must complete.");
        }
    }
}
