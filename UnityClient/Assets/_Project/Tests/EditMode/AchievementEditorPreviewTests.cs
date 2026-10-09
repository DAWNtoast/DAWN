using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class AchievementEditorPreviewTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void PreviewBuildsEightRowsOnDemandAndReachesAllFiftyAchievements()
        {
            host = new GameObject("Achievement Preview Test");
            host.AddComponent<MainMenuController>();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Achievements Panel"), Is.False);
            host.GetComponentsInChildren<Button>(true).Single(button => button.name == "Achievements").onClick.Invoke();

            RectTransform[] rows = host.GetComponentsInChildren<RectTransform>(true)
                .Where(item => item.name.StartsWith("Achievement Row "))
                .ToArray();
            Assert.That(rows.Length, Is.EqualTo(8));

            Button next = host.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Achievements Next Page");
            Text page = host.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "Achievements Page Indicator");
            Assert.That(page.text, Is.EqualTo("1 / 7"));
            Assert.That(next.interactable, Is.True);
            var visited = new System.Collections.Generic.HashSet<string>(rows.Where(row => row.gameObject.activeSelf).Select(row => row.name));

            next.onClick.Invoke();
            Assert.That(page.text, Is.EqualTo("2 / 7"));
            Assert.That(rows.Count(row => row.gameObject.activeSelf), Is.EqualTo(8));

            while (true)
            {
                foreach (RectTransform row in rows.Where(row => row.gameObject.activeSelf)) visited.Add(row.name);
                if (!next.interactable) break;
                next.onClick.Invoke();
            }
            Assert.That(page.text, Is.EqualTo("7 / 7"));
            Assert.That(rows.Count(row => row.gameObject.activeSelf), Is.EqualTo(2));
            Assert.That(visited.Count, Is.EqualTo(50), "Larger rows must not make any achievement unreachable.");
            CollectionAssert.AreEquivalent(rows, host.GetComponentsInChildren<RectTransform>(true)
                .Where(item => item.name.StartsWith("Achievement Row ")).ToArray(), "All pages reuse the same eight row instances.");
        }
    }
}
