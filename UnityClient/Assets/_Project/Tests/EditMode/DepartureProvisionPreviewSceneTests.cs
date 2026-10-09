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
    public sealed class DepartureProvisionPreviewSceneTests
    {
        private const string Root = "Assets/_Project/Scenes/UIPreviewsForMe/";

        [TestCase("UI_VoyageDeparture.unity", false)]
        [TestCase("UIVoyageDeparture/UIVoyageDeparture_RouteSelect.unity", false)]
        [TestCase("UIVoyageDeparture/UIVoyageDeparture_ShipSelect.unity", false)]
        [TestCase("UIVoyageDeparture/UIVoyageDeparture_ReadyToDepart.unity", false)]
        [TestCase("UIVoyageDeparture/UIVoyageDeparture_TreasureReady.unity", true)]
        public void SavedDeparture_ProvisionControlsFitAndPreserveVoyageKind(string relative, bool treasure)
        {
            Scene scene = EditorSceneManager.OpenScene(Root + relative, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = Controller(scene);
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.VoyageDeparture));
                Assert.That(controller.EditorPreviewPersistsGeneratedUi, Is.True);
                var settings = new SerializedObject(controller);
                Assert.That(settings.FindProperty("editorPreviewFlowState").enumValueIndex,
                    Is.EqualTo((int)(treasure ? GameplayFlowPreviewState.TreasureDeparture : GameplayFlowPreviewState.None)));
                Transform[] all = controller.GetComponentsInChildren<Transform>(true);
                RectTransform panel = (RectTransform)all.Single(item => item.name == "Voyage Setup");
                Button purchase = panel.Find("Departure Buy Provisions")?.GetComponent<Button>();
                Text quote = panel.Find("Departure Provision Quote")?.GetComponent<Text>();
                Assert.That(purchase, Is.Not.Null, relative);
                Assert.That(quote, Is.Not.Null, relative);
                Assert.That(purchase.GetComponentInChildren<Text>().text, Does.Contain("水粮"));
                Assert.That(quote.text, Is.Not.Empty);
                TextGenerationSettings quoteSettings = quote.GetGenerationSettings(new Vector2(quote.rectTransform.rect.width, 0f));
                quoteSettings.resizeTextForBestFit = false;
                quoteSettings.fontSize = quote.resizeTextForBestFit ? quote.resizeTextMinSize : quote.fontSize;
                Assert.That(quoteSettings.fontSize, Is.GreaterThanOrEqualTo(14), "The quote must remain readable.");
                using (var generator = new TextGenerator())
                    Assert.That(generator.GetPreferredHeight(quote.text, quoteSettings) / quote.pixelsPerUnit,
                        Is.LessThanOrEqualTo(quote.rectTransform.rect.height + 1f),
                        relative + " must show the entire trip/quantity/price quote.");
                string departureLabel = panel.Find("Start Voyage").GetComponentInChildren<Text>().text;
                if (treasure) Assert.That(departureLabel, Does.Contain("消耗藏宝图"));
                else Assert.That(departureLabel, Does.Not.Contain("消耗藏宝图"));
                Assert.That(all.Any(item => item.name == "Treasure Departure Detail"), Is.EqualTo(treasure));
                Assert.That(panel.Find("Departure Ship Schematic Caption"), Is.Not.Null,
                    "Synchronizing supplies must retain the authored ship selector.");
                InputField seedInput = panel.Find("Seed Input")?.GetComponent<InputField>();
                if (seedInput != null && seedInput.transform.Find("Seed Input Dropdown") != null)
                {
                    Assert.That(seedInput.enabled, Is.True, "Saved departure previews retain the visible input component.");
                    Assert.That(seedInput.textComponent.gameObject.activeSelf, Is.True);
                    Toggle mode = panel.Find("Use Chart Seed")?.GetComponent<Toggle>();
                    Assert.That(mode, Is.Not.Null);
                    Assert.That(mode.isOn, Is.False, "Existing departure previews start in random mode.");
                    Assert.That(seedInput.interactable, Is.False);
                }

                string[] names = { "Cargo", "Departure Provision Quote", "Departure Buy Provisions",
                    "Adjust Voyage Supplies", "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage" };
                Rect[] bounds = names.Select(name => Bounds((RectTransform)panel.Find(name))).ToArray();
                for (int index = 0; index < bounds.Length; index++)
                {
                    Rect item = bounds[index];
                    Assert.That(item.xMin, Is.GreaterThanOrEqualTo(panel.rect.xMin), names[index]);
                    Assert.That(item.xMax, Is.LessThanOrEqualTo(panel.rect.xMax), names[index]);
                    Assert.That(item.yMin, Is.GreaterThanOrEqualTo(panel.rect.yMin), names[index]);
                    Assert.That(item.yMax, Is.LessThanOrEqualTo(panel.rect.yMax), names[index]);
                    for (int other = index + 1; other < bounds.Length; other++)
                        Assert.That(item.Overlaps(bounds[other]), Is.False, names[index] + " overlaps " + names[other]);
                }
                Rect start = Bounds((RectTransform)panel.Find("Start Voyage"));
                Assert.That(start.yMin - panel.rect.yMin, Is.GreaterThanOrEqualTo(7.9f),
                    "The primary action needs padding above the frame edge.");
                foreach (string name in new[] { "Cargo", "Departure Provision Quote" })
                {
                    Rect textBounds = Bounds((RectTransform)panel.Find(name));
                    Assert.That(textBounds.xMin, Is.EqualTo(start.xMin).Within(.1f), name);
                    Assert.That(textBounds.xMax, Is.EqualTo(start.xMax).Within(.1f), name);
                }
                foreach (string[] row in new[] {
                    new[] { "Departure Buy Provisions", "Adjust Voyage Supplies" },
                    new[] { "Configure Voyage Battle Decks", "Configure Voyage Crew" }
                })
                {
                    Rect left = Bounds((RectTransform)panel.Find(row[0]));
                    Rect right = Bounds((RectTransform)panel.Find(row[1]));
                    Assert.That(left.yMin, Is.EqualTo(right.yMin).Within(.1f), string.Join(" / ", row));
                    Assert.That(left.yMax, Is.EqualTo(right.yMax).Within(.1f), string.Join(" / ", row));
                    Assert.That(left.xMin, Is.EqualTo(start.xMin).Within(.1f), row[0]);
                    Assert.That(right.xMax, Is.EqualTo(start.xMax).Within(.1f), row[1]);
                    Assert.That(right.xMin - left.xMax, Is.GreaterThanOrEqualTo(7.9f));
                }
                Assert.That(purchase.onClick.GetPersistentEventCount(), Is.Zero,
                    "A saved art preview must not execute purchases or change player saves.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("UI_Market.unity")]
        [TestCase("UIMarket/UIMarket_Buy.unity")]
        [TestCase("UIMarket/UIMarket_Sell.unity")]
        [TestCase("UIMarket/UIMarket_TransactionComplete.unity")]
        public void SavedMarket_ExplainsMonthlyGameInventoryRefresh(string relative)
        {
            Scene scene = EditorSceneManager.OpenScene(Root + relative, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = Controller(scene);
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Market));
                Text summary = controller.GetComponentsInChildren<Text>(true).Single(item => item.name == "Market Summary");
                Assert.That(summary.text, Does.Contain("下次补货"));
                Assert.That(summary.text, Does.Contain("00:00"));
                Assert.That(summary.text, Does.Not.Contain("每日"));
                Assert.That(summary.preferredHeight, Is.LessThanOrEqualTo(summary.rectTransform.rect.height + 1f));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static CoreGameplayDemoController Controller(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(item => item.GetComponentsInChildren<CoreGameplayDemoController>(true)).Single();

        private static Rect Bounds(RectTransform item) => new Rect(
            item.anchoredPosition - Vector2.Scale(item.sizeDelta, item.pivot), item.sizeDelta);
    }
}
