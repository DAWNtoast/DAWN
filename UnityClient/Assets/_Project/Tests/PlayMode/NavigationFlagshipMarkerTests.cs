using System.Collections;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    // Reuse the shared fixture's save-key snapshot and restoration.
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavigationFlagship_MapMarkerMatchesEverySelectedVesselWithoutBlockingCourseInput()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                PlayerPrefs.SetInt(CoreGameplayDemoController.ShipOwnedKey(ship), 1);
            var atlas = new PortArtCatalog();

            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                PlayerPrefs.SetInt(CoreGameplayDemoController.SelectedShipKey, ship);
                SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
                yield return null;
                yield return null;
                CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
                Assert.That(controller.Session.SelectedShipIndex, Is.EqualTo(ship));
                yield return ClickShipyardControl("Port Hotspot Voyage");
                yield return ClickShipyardControl("Start Voyage");

                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
                RectTransform marker = GameObject.Find("Player Ship").GetComponent<RectTransform>();
                Assert.That(marker.sizeDelta, Is.EqualTo(new Vector2(98f, 64f)), "Vessel selection must not enlarge its map hit footprint.");
                Assert.That(GameObject.Find("Ship Label"), Is.Null);
                Assert.That(GameObject.Find("Departure Label"), Is.Null);
                Assert.That(GameObject.Find("Destination Label"), Is.Null);
                Assert.That(GameObject.Find("Course Target Label"), Is.Null);
                foreach (RawImage layer in marker.GetComponentsInChildren<RawImage>())
                    Assert.That(layer.raycastTarget, Is.False, "Ship artwork must not intercept free-course clicks.");

                if (ship == 0)
                {
                    Assert.That(marker.Find("Player Ship Base"), Is.Not.Null, "The starter keeps its equipped modular artwork.");
                    Assert.That(marker.GetComponentsInChildren<BattleShipCutout>(), Is.Empty,
                        "Transparent modular layers must not receive the navy atlas color key.");
                }
                else
                {
                    Assert.That(marker.Find("Player Ship Base"), Is.Null, "An owned nonstarter cannot silently display the starter hull.");
                    RawImage hull = marker.Find("Player Ship Hull").GetComponent<RawImage>();
                    Assert.That(hull.texture, Is.SameAs(atlas.ShipTexture(ship)));
                    Assert.That(hull.texture, Is.Not.Null, "Every owned hull needs its corresponding imported artwork.");
                    Assert.That(hull.uvRect, Is.EqualTo(UiImageAspect.InsetUv(hull.texture, atlas.ShipUv(ship), 10f)),
                        "Map art must use the selected vessel's atlas cell with its existing ten-texel gutter crop.");
                    BattleShipCutout cutout = hull.GetComponent<BattleShipCutout>();
                    Assert.That(cutout, Is.Not.Null, "The roster's navy backing and black cell separators must be removed.");
                    Assert.That(cutout.OwnedMaterial, Is.Not.Null);
                    Assert.That(hull.material, Is.SameAs(cutout.OwnedMaterial));
                }

                Image courseInput = GameObject.Find("Map Course Input").GetComponent<Image>();
                Assert.That(courseInput.raycastTarget, Is.True);
                Assert.That(courseInput.GetComponent<MapCourseInputSurface>(), Is.Not.Null);
                LogAssert.NoUnexpectedReceived();
            }
        }
    }
}
