using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class VoyageHeaderPrefabTests
    {
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
