using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TavernRosterIntegrityTests
    {
        private GameObject host;
        private Scene previewScene;
        private bool legacyFlagExisted;
        private int legacyFlag;

        [SetUp]
        public void PreserveLegacyFlag()
        {
            legacyFlagExisted = PlayerPrefs.HasKey(CoreGameplayDemoController.LegacyWitchRecruitedKey);
            legacyFlag = PlayerPrefs.GetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, 1);
        }

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (legacyFlagExisted) PlayerPrefs.SetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey, legacyFlag);
            else PlayerPrefs.DeleteKey(CoreGameplayDemoController.LegacyWitchRecruitedKey);
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void RegionalTavernPreview_HasSixPaintedCandidates_NoUnillustratedRecruitmentPlaque(CoreGameplayPreviewPort port)
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            host = new GameObject("Tavern Roster Integrity") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, previewScene);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Tavern, port, 0);
            host.SetActive(true);

            for (int pass = 0; pass < 2; pass++)
            {
                Assert.That(controller.HasCompleteEditorPreview, Is.True);
                RawImage background = host.GetComponentsInChildren<RawImage>(true).Single(item => item.name == "Tavern Background");
                Assert.That(background.texture, Is.Not.Null, "The six crew members are painted into the regional tavern background.");
                Button[] buttons = host.GetComponentsInChildren<Button>(true);
                string[] expected = Enumerable.Range(0, 6).Select(index => "Tavern Crew " + index).ToArray();
                CollectionAssert.AreEquivalent(expected, buttons.Where(button => expected.Contains(button.name)).Select(button => button.name));
                Assert.That(buttons.Any(button => button.name == "Tavern Crew 6" || button.name.Contains("Witch")), Is.False);
                Type catalog = Type.GetType("ShatteredSail.Content.CrewNarrativeCatalog, ShatteredSail.Content", true);
                for (int profession = 0; profession < 6; profession++)
                {
                    object crew = catalog.GetMethod("ForPortAndProfession").Invoke(null, new object[] { controller.Session.CurrentPort, profession });
                    string name = (string)crew.GetType().GetProperty("DisplayName").GetValue(crew);
                    Assert.That(host.GetComponentsInChildren<Text>(true).Single(item => item.name == "Tavern Crew Name " + profession).text,
                        Is.EqualTo(name));
                }
                Assert.That(host.GetComponentsInChildren<Text>(true).Any(label => label.text.Contains("烬潮") || label.text.Contains("余烬回声")), Is.False);
                Assert.That(CoreVoyageSession.CardLibrary.Any(card => card.Id.Contains("witch") || card.RequiredCrew.Contains("witch")), Is.False);
                Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.LegacyWitchRecruitedKey), Is.EqualTo(1),
                    "Editor previews must neither restore retired crew nor migrate the user's real save.");
                controller.RefreshEditorPreview();
            }
        }
    }
}
