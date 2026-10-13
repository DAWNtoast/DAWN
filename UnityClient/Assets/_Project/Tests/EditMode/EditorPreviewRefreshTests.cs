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
                Canvas[] previous = host.GetComponentsInChildren<Canvas>(true);
                controller.RefreshEditorPreview();

                Assert.That(previous.All(canvas => canvas == null), Is.True,
                    "The old root and letterbox Canvas must both be destroyed, not hidden behind the replacement.");
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
            Canvas[] roots = canvases.Where(canvas => canvas.isRootCanvas).ToArray();
            Assert.That(roots.Length, Is.EqualTo(1), "Refreshing should leave exactly one generated preview root Canvas.");
            Canvas root = roots[0];
            Assert.That(root.name, Is.EqualTo(CoreGameplayDemoController.EditorPreviewCanvasName));
            Assert.That(root.transform.parent, Is.SameAs(host.transform));
            Assert.That(root.gameObject.activeInHierarchy, Is.True);
            Assert.That(root.gameObject.hideFlags & HideFlags.DontSaveInEditor, Is.EqualTo(HideFlags.DontSaveInEditor));
            // The fixed viewport intentionally owns a sorted child Canvas to
            // cover hovered controls outside its 16:9 surface.
            Assert.That(canvases.Length, Is.EqualTo(2), "Only the root and its letterbox overlay should remain after refresh.");
            Canvas overlay = canvases.Single(canvas => !canvas.isRootCanvas);
            Assert.That(overlay.name, Is.EqualTo("Game Letterbox Overlay"));
            Assert.That(overlay.transform.parent, Is.SameAs(root.transform));
            Assert.That(overlay.rootCanvas, Is.SameAs(root));
            Assert.That(root.transform.Find(FixedGameViewport.Name), Is.Not.Null);

            SerializedObject configuration = new SerializedObject(controller);
            Assert.That(configuration.FindProperty("editorPreviewScreen").intValue, Is.EqualTo((int)CoreGameplayPreviewScreen.VoyageDeparture));
            Assert.That(configuration.FindProperty("editorPreviewPort").intValue, Is.EqualTo((int)CoreGameplayPreviewPort.WhiteShell));
            Assert.That(configuration.FindProperty("editorPreviewRouteIndex").intValue, Is.EqualTo(route));
            RawImage[] backgrounds = root.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name == "Voyage Background").ToArray();
            Assert.That(backgrounds.Length, Is.EqualTo(1));
            Assert.That(backgrounds[0].texture, Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(button => button.name == "Start Voyage"), Is.True);
        }
    }
}
