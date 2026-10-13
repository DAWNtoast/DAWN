using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class MainMenuPrefabTests
    {
        [TestCase(MainMenuPrefabKind.Surface)]
        [TestCase(MainMenuPrefabKind.Settings)]
        [TestCase(MainMenuPrefabKind.Achievements)]
        [TestCase(MainMenuPrefabKind.NewGameConfirmation)]
        [TestCase(MainMenuPrefabKind.SaveNotice)]
        public void Prefab_ContainsCompleteAuthoredHierarchyAndPersistentArt(MainMenuPrefabKind kind)
        {
            MainMenuPrefabView prefab = Resources.Load<MainMenuPrefabView>("UI/MainMenu" + kind);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.Kind, Is.EqualTo(kind));
            Assert.That(prefab.IsComplete, Is.True);
            Assert.That(prefab.GetComponentsInChildren<RectTransform>(true).Length, Is.GreaterThan(5),
                "The prefab must contain the existing UI hierarchy, not an empty runtime builder.");
            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                Assert.That(EditorUtility.IsPersistent(image.sprite), Is.True, image.name);
                Assert.That(EditorUtility.IsPersistent(image.sprite.texture), Is.True, image.name);
            }
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true))
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                    button.name + " must be bound to the current controller, not the export host.");
            Assert.That(prefab.GetComponentInChildren<MainMenuController>(true), Is.Null);
        }

        [Test]
        public void HomePrefab_DoesNotPreloadOptionalDialogOrAchievementArt()
        {
            MainMenuPrefabView prefab = Resources.Load<MainMenuPrefabView>("UI/MainMenuSurface");
            Assert.That(prefab, Is.Not.Null);
            string[] dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true);
            Assert.That(dependencies.Any(path => path.Contains("aged_reading_parchment") ||
                path.Contains("AchievementIcons") || path.Contains("wood_button_face")), Is.False,
                "Home must retain lazy dialog loading; a root prefab may not pull all optional surfaces into memory.");
            Assert.That(prefab.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(5));
            Assert.That(prefab.GetComponentsInChildren<Slider>(true), Is.Empty);
        }

        [Test]
        public void AchievementPrefab_ContainsOnlyEightReusableEmptyRows()
        {
            MainMenuPrefabView prefab = Resources.Load<MainMenuPrefabView>("UI/MainMenuAchievements");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.Rows.Count, Is.EqualTo(8));
            foreach (MainMenuAchievementRowView row in prefab.Rows)
            {
                Assert.That(row.Icon.texture, Is.Null, "Achievement artwork is loaded for the selected page only.");
                Assert.That(row.Title.text, Is.Empty);
                Assert.That(row.Description.text, Is.Empty);
            }
        }
    }
}
