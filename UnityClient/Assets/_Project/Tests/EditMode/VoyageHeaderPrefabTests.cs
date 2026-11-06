using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class VoyageHeaderPrefabTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void HeaderSurface_RebindsSixDistinctPortsOnTheSameHierarchy(bool voyage)
        {
            GameObject prefab = Resources.Load<GameObject>(voyage ? "UI/VoyageHeader" : "UI/PortHubHeader");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            instance.name = voyage ? "Voyage Header" : "Port Hub";
            try
            {
                RectTransform header = instance.GetComponent<RectTransform>();
                Button[] controls = instance.GetComponentsInChildren<Button>(true);
                var textures = new HashSet<Texture>();
                NauticalHeaderGraphic retainedSurface = null;
                Texture firstTexture = null;
                foreach (string port in new[] { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" })
                {
                    if (voyage) NavigationHeaderSkin.Apply(header, port);
                    else NauticalHeaderSkin.ApplyPort(header, Color.white, true, port);
                    var surface = header.Find("Header Brass Rules").GetComponent<NauticalHeaderGraphic>();
                    if (retainedSurface == null) retainedSurface = surface;
                    Assert.That(surface, Is.SameAs(retainedSurface), "Rebind the retained header instead of stacking surfaces.");
                    Assert.That(surface.raycastTarget, Is.False);
                    Texture texture = surface.mainTexture;
                    Assert.That(AssetDatabase.GetAssetPath(texture), Is.Not.Empty, "Every port must resolve an authored timber asset: " + port);
                    textures.Add(texture);
                    if (firstTexture == null) firstTexture = texture;
                    CollectionAssert.AreEqual(controls, instance.GetComponentsInChildren<Button>(true));
                }
                Assert.That(textures.Count, Is.EqualTo(6), "Regional headers must not collapse to one common timber texture.");
                Texture lastTexture = retainedSurface.mainTexture;
                if (voyage) NavigationHeaderSkin.Apply(header);
                else NauticalHeaderSkin.ApplyPort(header, Color.white, true);
                Assert.That(retainedSurface.mainTexture, Is.SameAs(lastTexture), "A layout-only skin pass preserves the selected port.");
                if (voyage) NavigationHeaderSkin.Apply(header, "断锚港");
                else NauticalHeaderSkin.ApplyPort(header, Color.white, true, "断锚港");
                Assert.That(retainedSurface.mainTexture, Is.SameAs(firstTexture), "Returning to the original port restores its timber.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VoyageHeader_HasSerializedTimelineAndControlsWithoutRetainingSceneArt()
        {
            VoyageHeaderView prefab = Resources.Load<VoyageHeaderView>("UI/VoyageHeader");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.IsComplete, Is.True);
            Assert.That(prefab.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
            Assert.That(prefab.GetComponentsInChildren<VoyageTimeRibbonGraphic>(true).Length, Is.EqualTo(1));
            Assert.That(prefab.Frame.sprite, Is.Null);
            Assert.That(prefab.Compass.sprite, Is.Null);
            Assert.That(prefab.Timeline.ElapsedHours, Is.Zero, "Templates contain no captured voyage state.");
            Assert.That(prefab.Cargo.onClick.GetPersistentEventCount(), Is.Zero);
            Assert.That(prefab.Settings.onClick.GetPersistentEventCount(), Is.Zero);
            string[] dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true);
            Assert.That(dependencies.Any(path => path.Contains("navigation_header_v3") ||
                path.Contains("navigation_compass_v3") || path.Contains("broken_anchor_header_v1")), Is.False,
                "The prefab template must not keep inactive navigation or regional artwork in memory.");
        }
    }
}
