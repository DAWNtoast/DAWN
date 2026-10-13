using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class EditorPreviewRefreshTests
    {
        private Scene previewScene;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void RefreshExistingPreview_ReplacesCanvasWithoutDuplicatingItOrChangingConfiguration(int route)
        {
            CreatePreview(route);
            for (int refresh = 0; refresh < 3; refresh++)
            {
                Canvas previous = host.GetComponentInChildren<Canvas>(true);
                controller.RefreshEditorPreview();

                Assert.That(previous == null, Is.True, "The old generated hierarchy must be destroyed, not left hidden behind the new one.");
                AssertCompleteDeparture(route);
            }
        }

        [Test]
        public void RefreshAfterPreparingSceneSave_ResumesSuppressedPreviewWithItsOriginalConfiguration()
        {
            const int route = 1;
            CreatePreview(route);
            controller.PrepareEditorPreviewForSceneSave();
            Assert.That(controller.HasEditorPreview, Is.False);
            Assert.That(host.GetComponentsInChildren<Canvas>(true), Is.Empty);

            controller.RefreshEditorPreview();

            AssertCompleteDeparture(route);
            // A second refresh verifies the temporary save suppression was cleared,
            // rather than bypassed for one special rebuild only.
            controller.RefreshEditorPreview();
            AssertCompleteDeparture(route);
        }

        [Test]
        public void RefreshWhenGeneratedCanvasIsMissing_RebuildsTheSelectedPreview()
        {
            const int route = 2;
            CreatePreview(route);
            Transform previous = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Assert.That(previous, Is.Not.Null);
            Object.DestroyImmediate(previous.gameObject);
            Assert.That(controller.HasEditorPreview, Is.False);

            controller.RefreshEditorPreview();

            AssertCompleteDeparture(route);
        }

        private void CreatePreview(int route)
        {
            Assert.That(Application.isPlaying, Is.False);
            previewScene = EditorSceneManager.NewPreviewScene();
            host = new GameObject("Editor Preview Refresh Test") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, previewScene);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewPort.WhiteShell, route);
            host.SetActive(true);
            AssertCompleteDeparture(route);
        }

        private void AssertCompleteDeparture(int route)
        {
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas[] canvases = host.GetComponentsInChildren<Canvas>(true);
            Assert.That(canvases.Length, Is.EqualTo(1), "Refreshing should leave exactly one generated preview Canvas.");
            Assert.That(canvases[0].name, Is.EqualTo(CoreGameplayDemoController.EditorPreviewCanvasName));
            Assert.That(canvases[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(canvases[0].gameObject.hideFlags & HideFlags.DontSaveInEditor, Is.EqualTo(HideFlags.DontSaveInEditor));

            SerializedObject configuration = new SerializedObject(controller);
            Assert.That(configuration.FindProperty("editorPreviewScreen").intValue, Is.EqualTo((int)CoreGameplayPreviewScreen.VoyageDeparture));
            Assert.That(configuration.FindProperty("editorPreviewPort").intValue, Is.EqualTo((int)CoreGameplayPreviewPort.WhiteShell));
            Assert.That(configuration.FindProperty("editorPreviewRouteIndex").intValue, Is.EqualTo(route));
            RawImage[] backgrounds = canvases[0].GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name == "Voyage Background").ToArray();
            Assert.That(backgrounds.Length, Is.EqualTo(1));
            Assert.That(backgrounds[0].texture, Is.Not.Null);
            Assert.That(canvases[0].GetComponentsInChildren<Button>(true).Any(button => button.name == "Start Voyage"), Is.True);
        }
    }
}
