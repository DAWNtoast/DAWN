using ShatteredSail.Core.Gameplay;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class AchievementIconPresentationTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void AchievementIcons_AllFiftyEntriesReuseEightRowsAndFitDedicatedPictureSpace()
        {
            host = new GameObject("Achievement Icon Presentation Fixture");
            host.AddComponent<MainMenuController>();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Achievements Panel"), Is.False);
            Assert.That(host.GetComponentsInChildren<RawImage>(true).Any(item => item.name.StartsWith("Achievement Icon")), Is.False);
            Button open = host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Achievements");
            open.onClick.Invoke();
            RectTransform[] rows = host.GetComponentsInChildren<RectTransform>(true)
                .Where(item => item.name.StartsWith("Achievement Row ")).ToArray();
            Assert.That(rows.Length, Is.EqualTo(8));
            Transform[] builtObjects = host.GetComponentsInChildren<Transform>(true);
            Button next = host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Achievements Next Page");
            var visited = new HashSet<string>();
            for (int page = 0; page < 7; page++)
            {
                Assert.That(rows.Count(item => item.gameObject.activeSelf), Is.EqualTo(page == 6 ? 2 : 8));
                foreach (CoreAchievementDefinition definition in AchievementCatalog.All.Skip(page * 8).Take(8))
                {
                Assert.That(visited.Add(definition.Id), Is.True);
                RectTransform row = rows.Single(item => item.name == "Achievement Row " + definition.Id);
                RawImage icon = row.Find("Achievement Icon " + definition.Id)?.GetComponent<RawImage>();
                ItemIconArt art = AchievementIconCatalog.ForId(definition.Id);
                Assert.That(art.IsAvailable, Is.True, definition.Id);
                Assert.That(icon, Is.Not.Null, definition.Id);
                Assert.That(icon.enabled, Is.True, definition.Id);
                Assert.That(icon.texture, Is.SameAs(art.Texture), definition.Id);
                Assert.That(icon.uvRect, Is.EqualTo(art.Uv), definition.Id);
                Assert.That(icon.raycastTarget, Is.False, definition.Id);
                Rect picture = BoundsIn(row, icon.rectTransform);
                Assert.That(picture.width, Is.GreaterThan(0f).And.LessThanOrEqualTo(48.01f), definition.Id);
                Assert.That(picture.height, Is.GreaterThan(0f).And.LessThanOrEqualTo(48.01f), definition.Id);
                Assert.That(picture.xMin, Is.GreaterThanOrEqualTo(row.rect.xMin), definition.Id);
                Assert.That(picture.yMin, Is.GreaterThanOrEqualTo(row.rect.yMin), definition.Id);
                Assert.That(picture.xMax, Is.LessThanOrEqualTo(row.rect.xMax), definition.Id);
                Assert.That(picture.yMax, Is.LessThanOrEqualTo(row.rect.yMax), definition.Id);
                float aspect = art.Texture.width * art.Uv.width / (art.Texture.height * art.Uv.height);
                Assert.That(picture.width / picture.height, Is.EqualTo(aspect).Within(.001f), definition.Id);
                foreach (Text text in row.GetComponentsInChildren<Text>(true))
                    Assert.That(picture.Overlaps(BoundsIn(row, text.rectTransform)), Is.False, definition.Id + " / " + text.name);
                Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo(definition.Title));
                Assert.That(row.Find("Description").GetComponent<Text>().text, Is.EqualTo(definition.Description));
                Assert.That(row.Find("State").GetComponent<Text>().text,
                    Is.EqualTo(AchievementCatalog.IsUnlocked(definition) ? "已解锁" : "未解锁"));
                Assert.That(row.Find("Symbol"), Is.Null);
                if (AchievementCatalog.IsUnlocked(definition)) Assert.That(icon.color, Is.EqualTo(Color.white));
                else
                {
                    Assert.That(icon.color.maxColorComponent, Is.LessThan(1f));
                    Assert.That(icon.color.a, Is.GreaterThanOrEqualTo(.7f), "Locked badges remain identifiable.");
                }
                }
                CollectionAssert.AreEquivalent(builtObjects, host.GetComponentsInChildren<Transform>(true), "Paging must reuse every UI object.");
                if (page < 6) next.onClick.Invoke();
            }
            Assert.That(visited.Count, Is.EqualTo(50));
        }

        [Test]
        public void AchievementIcons_EditorPreviewRebuildClearsLazyRowsAndTransientFlags()
        {
            host = new GameObject("Achievement Preview Lifetime Fixture");
            host.AddComponent<MainMenuController>();
            host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Achievements").onClick.Invoke();
            Transform panel = host.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Achievements Panel");
            foreach (Transform child in panel.GetComponentsInChildren<Transform>(true))
                Assert.That(child.gameObject.hideFlags, Is.EqualTo(HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild), child.name);
            host.SetActive(false);
            Assert.That(panel == null, Is.True, "Disabling an editor preview destroys its generated panel.");
            host.SetActive(true);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Achievements Panel"), Is.False);
            host.GetComponentsInChildren<Button>(true).Single(item => item.name == "Achievements").onClick.Invoke();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Count(item => item.name.StartsWith("Achievement Row ")), Is.EqualTo(8));
            Assert.That(host.GetComponentsInChildren<Text>(true).Single(item => item.name == "Achievements Page Indicator").text, Is.EqualTo("1 / 7"));
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
