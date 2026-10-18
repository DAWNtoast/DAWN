using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TavernDossierEditorPagingTests
    {
        private static readonly string[] PageNames = { "Background", "Story", "Skills", "SpecialCards" };
        private static readonly string[] PortNames = { "断锚港", "白贝港", "铜钟港", "盐冠港", "暮潮港", "灰鸥港" };
        private Scene previewScene;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor, 0, 0)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell, 0, 1)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell, 0, 2)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown, 5, 0)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide, 5, 1)]
        [TestCase(CoreGameplayPreviewPort.GrayGull, 5, 2)]
        [TestCase(CoreGameplayPreviewPort.GrayGull, 0, 3)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell, 5, 3)]
        public void ConfigureCrewDossier_PreservesPortAndSelectionOnRefresh_WhileGeneralConfigurationResetsSelection(
            CoreGameplayPreviewPort port, int profession, int page)
        {
            CreateTavernPreview(port);

            controller.ConfigureEditorCrewDossier(profession, page);
            AssertDossier(port, profession, page);

            Transform previousCanvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Canvas[] previousCanvases = host.GetComponentsInChildren<Canvas>(true);
            controller.RefreshEditorPreview();
            Assert.That(previousCanvas == null, Is.True, "Refresh must rebuild the generated preview hierarchy.");
            Assert.That(previousCanvases.All(canvas => canvas == null), Is.True,
                "Refresh must also destroy the previous viewport's letterbox Canvas.");
            AssertDossier(port, profession, page);

            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, port, 1);
            AssertDossier(port, 0, 0);
        }

        private void CreateTavernPreview(CoreGameplayPreviewPort port)
        {
            Assert.That(Application.isPlaying, Is.False);
            previewScene = EditorSceneManager.NewPreviewScene();
            host = new GameObject("Tavern Dossier Editor Paging Test") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, previewScene);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, port, 1);
            host.SetActive(true);
            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Tavern));
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
        }

        private void AssertDossier(CoreGameplayPreviewPort port, int profession, int page)
        {
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.TavernCrewDetail));
            Assert.That(controller.Session.CurrentPort, Is.EqualTo(PortNames[(int)port]), "Selecting or refreshing a dossier must keep the configured port.");
            Canvas[] canvases = host.GetComponentsInChildren<Canvas>(true);
            Canvas[] roots = canvases.Where(canvas => canvas.isRootCanvas).ToArray();
            Assert.That(roots.Length, Is.EqualTo(1), "The dossier must have one generated preview root.");
            Assert.That(canvases.Length, Is.EqualTo(2), "The only child Canvas is the fixed viewport's letterbox overlay.");
            Canvas overlay = canvases.Single(canvas => !canvas.isRootCanvas);
            Assert.That(roots[0].name, Is.EqualTo(CoreGameplayDemoController.EditorPreviewCanvasName));
            Assert.That(roots[0].transform.parent, Is.SameAs(host.transform));
            Assert.That(overlay.name, Is.EqualTo("Game Letterbox Overlay"));
            Assert.That(overlay.transform.parent, Is.SameAs(roots[0].transform));
            Assert.That(overlay.rootCanvas, Is.SameAs(roots[0]));
            Assert.That(roots[0].transform.Find(FixedGameViewport.Name), Is.Not.Null);

            SerializedObject configuration = new SerializedObject(controller);
            Assert.That(configuration.FindProperty("editorPreviewPort").intValue, Is.EqualTo((int)port));
            Assert.That(configuration.FindProperty("editorPreviewRouteIndex").intValue, Is.EqualTo(1));
            Assert.That(configuration.FindProperty("editorPreviewCrewIndex").intValue, Is.EqualTo(profession));
            Assert.That(configuration.FindProperty("editorPreviewCrewPage").intValue, Is.EqualTo(page));

            Transform root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.gameObject.activeInHierarchy, Is.True);
            object crew = ExpectedCrew(PortNames[(int)port], profession);
            Assert.That(Find<Text>(root, "Tavern Crew Detail Name").text, Is.EqualTo(ReadString(crew, "DisplayName")));
            Text biography = Find<Text>(root, "Tavern Crew Biography");
            string expectedBiography = WithoutLineBreaks(ReadString(crew, "Biography"));
            Assert.That(WithoutLineBreaks(biography.text), Is.EqualTo(expectedBiography));
            Assert.That(biography.fontSize, Is.GreaterThanOrEqualTo(26));
            Assert.That(biography.resizeTextForBestFit, Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Tavern Crew Appearance"), Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Tavern Crew Profile Heading"), Is.False);
            Assert.That(Find<Button>(root, "DossierPageBackground").GetComponentInChildren<Text>().text, Is.EqualTo("人物背景"));
            Assert.That(Find<Text>(root, "Tavern Crew Detail Role").text, Does.StartWith(PortNames[(int)port]));
            Assert.That(Find<Button>(root, "Recruit Tavern Crew " + profession).gameObject.activeInHierarchy, Is.True);

            for (int index = 0; index < PageNames.Length; index++)
            {
                RectTransform pageRoot = Find<RectTransform>(root, "Tavern Dossier " + PageNames[index] + " Page");
                Assert.That(pageRoot.gameObject.activeSelf, Is.EqualTo(index == page), PageNames[index] + " activeSelf");
                Assert.That(pageRoot.gameObject.activeInHierarchy, Is.EqualTo(index == page), PageNames[index] + " activeInHierarchy");
            }
            foreach (string removed in new[] { "Tavern Special Card Growth", "Tavern Dossier Page Counter",
                "Previous Tavern Dossier Page", "Next Tavern Dossier Page" })
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name == removed), Is.False, removed);
        }

        private static object ExpectedCrew(string portName, int profession)
        {
            // Keep the EditMode assembly's existing Core/Presentation references.
            // The production preview has already loaded its Content catalogue.
            Type catalog = Type.GetType("ShatteredSail.Content.CrewNarrativeCatalog, ShatteredSail.Content", true);
            return catalog.GetMethod("ForPortAndProfession").Invoke(null, new object[] { portName, profession });
        }

        private static string ReadString(object instance, string property)
        {
            return (string)instance.GetType().GetProperty(property).GetValue(instance);
        }

        private static string WithoutLineBreaks(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty);

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = root.GetComponentsInChildren<T>(true).FirstOrDefault(component => component.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
