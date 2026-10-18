using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class AchievementCatalogTests
    {
        [Test]
        public void Catalog_ContainsFiftyDistinctProgressAchievements()
        {
            Assert.That(AchievementCatalog.All.Count, Is.EqualTo(AchievementCatalog.TotalCount));
            Assert.That(AchievementCatalog.All.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(AchievementCatalog.TotalCount));
            Assert.That(AchievementCatalog.All.All(item => !string.IsNullOrEmpty(item.Title) &&
                !string.IsNullOrEmpty(item.Description) && !string.IsNullOrEmpty(item.CategoryLabel)), Is.True);
        }

        [Test]
        public void MainMenuAchievements_PaginatesEightReadableRowsWithinTheCard()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = new GameObject("Achievement Pagination Fixture");
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scene);
            try
            {
                host.AddComponent<MainMenuController>();
                host.SetActive(true);
                host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();
                Button next = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements Next Page");
                Button previous = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements Previous Page");
                Text page = host.GetComponentsInChildren<Text>(true).Single(text => text.name == "Achievements Page Indicator");
                RectTransform card = host.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Achievements Card");
                Canvas.ForceUpdateCanvases();
                Assert.That(page.text, Is.EqualTo("1 / 7"));
                Assert.That(previous.interactable, Is.False);
                Assert.That(next.interactable, Is.True);
                AssertAchievementRowsFit(card, next, previous);

                next.onClick.Invoke();
                Assert.That(page.text, Is.EqualTo("2 / 7"));
                Assert.That(previous.interactable, Is.True);
                AssertAchievementRowsFit(card, next, previous);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AssertAchievementRowsFit(RectTransform card, Button next, Button previous)
        {
            Rect[] rows = card.GetComponentsInChildren<RectTransform>()
                .Where(rect => rect.name.StartsWith("Achievement Row ", StringComparison.Ordinal))
                .Select(rect => BoundsIn(card, rect)).OrderByDescending(rect => rect.yMax).ToArray();
            Assert.That(rows.Length, Is.EqualTo(8));
            float navigationTop = Mathf.Max(BoundsIn(card, next.GetComponent<RectTransform>()).yMax,
                BoundsIn(card, previous.GetComponent<RectTransform>()).yMax);
            for (int index = 0; index < rows.Length; index++)
            {
                Rect row = rows[index];
                Assert.That(row.width, Is.GreaterThan(0f));
                Assert.That(row.height, Is.GreaterThanOrEqualTo(60f), "Larger text needs a readable row height.");
                Assert.That(row.xMin, Is.GreaterThanOrEqualTo(card.rect.xMin));
                Assert.That(row.xMax, Is.LessThanOrEqualTo(card.rect.xMax));
                Assert.That(row.yMax, Is.LessThanOrEqualTo(card.rect.yMax));
                Assert.That(row.yMin, Is.GreaterThanOrEqualTo(card.rect.yMin));
                Assert.That(row.yMin - navigationTop, Is.GreaterThanOrEqualTo(12f),
                    "Achievement rows must leave a clear gap above the pagination buttons.");
                if (index > 0)
                    Assert.That(rows[index - 1].yMin - row.yMax, Is.GreaterThanOrEqualTo(8f - .01f),
                        "Adjacent rows need separate reading areas.");
            }
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 minimum = parent.InverseTransformPoint(corners[0]);
            Vector2 maximum = minimum;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = parent.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
