using System.IO;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class EditablePreviewScenePersistenceTests
    {
        private const string BattlePath = "Assets/_Project/Scenes/UIPreviewsForMe/UI_Battle.unity";

        [Test]
        public void EditableDrawChartPreview_ShowsSettlementDrawingActionWithoutTreasureRoom()
        {
            const string path = "Assets/_Project/Scenes/UIPreviewsForMe/TutorialVoyage/28_DrawChart.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CoreGameplayDemoController>()).FirstOrDefault(item => item != null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Settlement));
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                Transform[] objects = controller.GetComponentsInChildren<Transform>(true);
                Assert.That(objects.Any(item => item.name == "Treasure Panel"), Is.False,
                    "Drawing occurs during the actual voyage settlement, before entering the port treasure room.");
                RectTransform card = objects.Single(item => item.name == "Settlement Card") as RectTransform;
                Button draw = card.Find("Draw Sea Chart")?.GetComponent<Button>();
                Button finish = card.Find("Return To Port")?.GetComponent<Button>();
                Assert.That(draw, Is.Not.Null);
                Assert.That(finish, Is.Not.Null);
                Assert.That(draw.GetComponentInChildren<Text>().text, Is.EqualTo("航海士 · 绘制海图"));
                Assert.That(draw.GetComponent<Image>().sprite, Is.Not.Null);
                Assert.That(draw.GetComponent<Image>().sprite, Is.EqualTo(finish.GetComponent<Image>().sprite));
                RectTransform drawing = draw.GetComponent<RectTransform>();
                RectTransform returning = finish.GetComponent<RectTransform>();
                RectTransform note = objects.Single(item => item.name == "Settlement Note") as RectTransform;
                Rect drawingBounds = new Rect(drawing.anchoredPosition - drawing.sizeDelta * 0.5f, drawing.sizeDelta);
                Assert.That(drawingBounds.Overlaps(new Rect(returning.anchoredPosition - returning.sizeDelta * 0.5f, returning.sizeDelta)), Is.False);
                Assert.That(drawingBounds.Overlaps(new Rect(note.anchoredPosition - note.sizeDelta * 0.5f, note.sizeDelta)), Is.False,
                    "The drawing control must not cover the settlement note.");
                Assert.That(drawingBounds.yMin, Is.GreaterThanOrEqualTo(card.rect.yMin));
                Assert.That(draw.onClick.GetPersistentEventCount(), Is.Zero, "A layout preview must not write a chart or player save.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EditableBattlePreview_SerializesCanvasAndKeepsInspectorLayoutAfterReopen()
        {
            Assert.That(File.Exists(Path.Combine(Application.dataPath, "_Project/Scenes/UIPreviewsForMe/UI_Battle.unity")), Is.True);
            // Save/reopen only a disposable copy: the authored scene can contain the
            // developer's unsaved-to-Git layout edits, including when assertions fail.
            string isolatedPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/__UIRefactorBattlePersistence_" + System.Guid.NewGuid().ToString("N") + ".unity");
            Scene scene = default;
            try
            {
                Assert.That(AssetDatabase.CopyAsset(BattlePath, isolatedPath), Is.True);
                scene = EditorSceneManager.OpenScene(isolatedPath, OpenSceneMode.Additive);
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CoreGameplayDemoController>())
                    .FirstOrDefault(item => item != null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                Canvas canvas = controller.GetComponentInChildren<Canvas>(true);
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.transform.localScale.x, Is.GreaterThan(0.001f), "Saved preview Canvas must remain visible.");
                Assert.That(canvas.transform.localScale.y, Is.EqualTo(canvas.transform.localScale.x).Within(0.001f));
                Assert.That(canvas.gameObject.hideFlags & HideFlags.DontSaveInEditor, Is.EqualTo(HideFlags.None));
                RectTransform art = canvas.transform.Find("Battle Overlay/Battle Card/Battle Scene Art") as RectTransform;
                if (art == null) art = canvas.GetComponentsInChildren<RectTransform>(true).First(item => item.name == "Battle Scene Art");
                Vector2 original = art.anchoredPosition;
                Vector2 edited = original + new Vector2(17f, -9f);
                art.anchoredPosition = edited;
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                EditorSceneManager.CloseScene(scene, true);
                scene = EditorSceneManager.OpenScene(isolatedPath, OpenSceneMode.Additive);
                CoreGameplayDemoController reopened = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CoreGameplayDemoController>())
                    .FirstOrDefault(item => item != null);
                RectTransform reopenedArt = reopened.GetComponentInChildren<Canvas>(true)
                    .GetComponentsInChildren<RectTransform>(true).First(item => item.name == "Battle Scene Art");
                Assert.That(reopenedArt.anchoredPosition, Is.EqualTo(edited));
            }
            finally
            {
                try
                {
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
                finally
                {
                    if (File.Exists(isolatedPath))
                        Assert.That(AssetDatabase.DeleteAsset(isolatedPath), Is.True,
                            "Remove only the disposable persistence-test scene.");
                }
            }
        }
        [Test]
        public void EditableBattlePreview_KeepsBattleReadoutsAboveVoyageHeaderAndUsesWoodBoards()
        {
            Scene scene = EditorSceneManager.OpenScene(BattlePath, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = scene.GetRootGameObjects()
                    .Select(root => root.GetComponent<CoreGameplayDemoController>())
                    .FirstOrDefault(item => item != null);
                Assert.That(controller, Is.Not.Null);

                RectTransform canvas = controller.GetComponentInChildren<Canvas>(true).transform as RectTransform;
                Assert.That(canvas, Is.Not.Null);
                RectTransform phaseContent = canvas.Find("Phase Content") as RectTransform;
                Assert.That(phaseContent, Is.Not.Null);
                RectTransform voyageHeader = phaseContent.Find("Voyage Header") as RectTransform;
                RectTransform readouts = phaseContent.Find("Battle Readouts Overlay") as RectTransform;
                Assert.That(voyageHeader, Is.Not.Null);
                Assert.That(readouts, Is.Not.Null);
                Assert.That(voyageHeader.Find("Voyage Header Title").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(-734f, 0f)).Within(0.1f));
                Assert.That(voyageHeader.Find("Voyage Header Status").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(511f, 0f)).Within(0.1f));
                Assert.That(readouts.GetSiblingIndex(), Is.GreaterThan(voyageHeader.GetSiblingIndex()));

                Assert.That(readouts.Find("Battle Range"), Is.Not.Null);
                Assert.That(readouts.Find("Battle Alignment"), Is.Not.Null);

                RectTransform battleCard = phaseContent.Find("Battle Overlay/Battle Card") as RectTransform;
                Assert.That(battleCard, Is.Not.Null);
                Image intentImage = battleCard.Find("Enemy Intent HUD")?.GetComponent<Image>();
                Image playerBoard = battleCard.Find("Player Battle Name Board")?.GetComponent<Image>();
                Image enemyBoard = battleCard.Find("Enemy Battle Name Board")?.GetComponent<Image>();
                Assert.That(intentImage, Is.Not.Null);
                Assert.That(playerBoard, Is.Not.Null);
                Assert.That(enemyBoard, Is.Not.Null);
                Assert.That(playerBoard.sprite, Is.EqualTo(intentImage.sprite));
                Assert.That(enemyBoard.sprite, Is.EqualTo(intentImage.sprite));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}

