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
        public void RefreshExistingPreview_ReplacesCanvasWithoutDuplicatingItOrChangingConfiguration(int mission)
        {
            CreatePreview(mission);
            for (int refresh = 0; refresh < 3; refresh++)
            {
                Canvas previous = host.GetComponentInChildren<Canvas>(true);
                controller.RefreshEditorPreview();

                Assert.That(previous == null, Is.True, "The old generated hierarchy must be destroyed, not left hidden behind the new one.");
                AssertCompleteDeployment(mission);
            }
        }

        [Test]
        public void RefreshAfterPreparingSceneSave_ResumesSuppressedPreviewWithItsOriginalConfiguration()
        {
            const int mission = 1;
            CreatePreview(mission);
            controller.PrepareEditorPreviewForSceneSave();
            Assert.That(controller.HasEditorPreview, Is.False);
            Assert.That(host.GetComponentsInChildren<Canvas>(true), Is.Empty);

            controller.RefreshEditorPreview();

            AssertCompleteDeployment(mission);
            // A second refresh verifies the temporary save suppression was cleared,
            // rather than bypassed for one special rebuild only.
            controller.RefreshEditorPreview();
            AssertCompleteDeployment(mission);
        }

        [Test]
        public void RefreshWhenGeneratedCanvasIsMissing_RebuildsTheSelectedPreview()
        {
            const int mission = 2;
            CreatePreview(mission);
            Transform previous = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Assert.That(previous, Is.Not.Null);
            Object.DestroyImmediate(previous.gameObject);
            Assert.That(controller.HasEditorPreview, Is.False);

            controller.RefreshEditorPreview();

            AssertCompleteDeployment(mission);
        }

        private void CreatePreview(int mission)
        {
            Assert.That(Application.isPlaying, Is.False);
            previewScene = EditorSceneManager.NewPreviewScene();
            host = new GameObject("Editor Preview Refresh Test") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, previewScene);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DispatchDeployment, CoreGameplayPreviewPort.WhiteShell, mission);
            host.SetActive(true);
            AssertCompleteDeployment(mission);
        }

        private void AssertCompleteDeployment(int mission)
        {
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas[] canvases = host.GetComponentsInChildren<Canvas>(true);
            Assert.That(canvases.Length, Is.EqualTo(1), "Refreshing should leave exactly one generated preview Canvas.");
            Assert.That(canvases[0].name, Is.EqualTo(CoreGameplayDemoController.EditorPreviewCanvasName));
            Assert.That(canvases[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(canvases[0].gameObject.hideFlags & HideFlags.DontSaveInEditor, Is.EqualTo(HideFlags.DontSaveInEditor));

            SerializedObject configuration = new SerializedObject(controller);
            Assert.That(configuration.FindProperty("editorPreviewScreen").intValue, Is.EqualTo((int)CoreGameplayPreviewScreen.DispatchDeployment));
            Assert.That(configuration.FindProperty("editorPreviewPort").intValue, Is.EqualTo((int)CoreGameplayPreviewPort.WhiteShell));
            Assert.That(configuration.FindProperty("editorPreviewRouteIndex").intValue, Is.EqualTo(mission));
            RawImage[] frames = canvases[0].GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Dispatch Deployment Frame ")).ToArray();
            Assert.That(frames.Length, Is.EqualTo(1));
            Assert.That(frames[0].name, Is.EqualTo("Dispatch Deployment Frame " + mission));
            Assert.That(frames[0].texture, Is.Not.Null);
            string[] frameNames = { "dispatch_frame_salvage_v1", "dispatch_frame_escort_v1", "dispatch_frame_survey_v1" };
            Assert.That(frames[0].texture.name, Is.EqualTo(frameNames[mission]), "Refreshing must retain the selected mission's themed parchment frame.");
        }
    }
}
