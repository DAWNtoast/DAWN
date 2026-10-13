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
    [Ignore("UIPreviewsForMe was retired and cleared on 2026-10-13; these historical saved-scene fixtures are no longer maintained.")]
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
                Assert.That(purchase, Is.Not.Null, relative);
                Assert.That(purchase.GetComponentInChildren<Text>().text, Is.EqualTo("购买基础物资"));
                Button market = panel.Find("Adjust Voyage Supplies")?.GetComponent<Button>();
                Assert.That(market, Is.Not.Null, relative);
                Assert.That(market.GetComponentInChildren<Text>().text, Is.EqualTo("前往市场"));
                Assert.That(purchase.GetComponent<RectTransform>().rect.width,
                    Is.EqualTo(market.GetComponent<RectTransform>().rect.width).Within(.1f),
                    "The two short supply actions share a balanced row.");
                string[] removed = { "Ship Stats", "Departure Ship Schematic Caption", "Departure Vessel Hint",
                    "Destination Summary", "Cargo", "Departure Provision Quote",
                    "Departure Provision Water Icon", "Departure Provision Food Icon" };
                removed = removed.Concat(Enumerable.Range(0, ComponentArtCatalog.SlotCount)
                    .SelectMany(slot => new[] { "Upgrade Part " + slot, "Upgrade Name " + slot })).ToArray();
                foreach (string name in removed)
                    Assert.That(all.Any(item => item.name == name), Is.False, relative + " / removed " + name);
                string departureLabel = panel.Find("Start Voyage").GetComponentInChildren<Text>().text;
                if (treasure) Assert.That(departureLabel, Does.Contain("消耗藏宝图"));
                else Assert.That(departureLabel, Does.Not.Contain("消耗藏宝图"));
                Assert.That(all.Any(item => item.name == "Treasure Departure Detail"), Is.EqualTo(treasure));
                RectTransform ship = panel.Find("Inspect Departure Vessel") as RectTransform;
                Assert.That(ship, Is.Not.Null, "Synchronizing supplies must retain the authored ship selector.");
                Assert.That(ship.sizeDelta.x, Is.GreaterThanOrEqualTo(367.9f));
                Assert.That(ship.sizeDelta.y, Is.GreaterThanOrEqualTo(207.9f));
                Assert.That(ship.anchoredPosition.x, Is.EqualTo(0f).Within(.1f));
                Assert.That(ship.GetComponentsInChildren<RawImage>().Any(image => image.rectTransform.rect.width > 240f),
                    Is.True, "The saved ship artwork must grow with the click area.");
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

                string[] names = { "Setup Title", "Departure Previous Vessel", "Inspect Departure Vessel", "Departure Next Vessel",
                    "Departure Buy Provisions", "Adjust Voyage Supplies",
                    "Configure Voyage Battle Decks", "Configure Voyage Crew", "Start Voyage" };
                // Older authored previews do not contain the runtime crew controls.
                // Keep validating their geometry when present without requiring new objects.
                names = names.Concat(new[] { "DepartureOrdinaryCrewDecreaseButton", "DepartureOrdinaryCrewCount",
                    "DepartureOrdinaryCrewIncreaseButton", "DepartureCrewSpeedPreview" }
                    .Where(name => panel.Find(name) != null)).ToArray();
                Rect[] bounds = names.Select(name =>
                {
                    RectTransform item = panel.Find(name) as RectTransform;
                    Assert.That(item, Is.Not.Null, relative + " / " + name);
                    return Bounds(item);
                }).ToArray();
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
        public void SavedMarket_ShowsFreeCargoAndOnePurchaseQuotePerItem(string relative)
        {
            Scene scene = EditorSceneManager.OpenScene(Root + relative, OpenSceneMode.Additive);
            try
            {
                CoreGameplayDemoController controller = Controller(scene);
                Assert.That(controller.EditorPreviewScreen, Is.EqualTo(CoreGameplayPreviewScreen.Market));
                Text[] labels = controller.GetComponentsInChildren<Text>(true);
                Text summary = labels.Single(item => item.name == "Market Summary");
                // Inspect serialized labels without rebuilding the authored scene.
                // Exact live quotes and earned discounts are covered by PortMarketPreviewTests.
                Assert.That(summary.text, Does.Match(@"^货舱余量\s+\d+\.\d(　·　采购优惠 [1-9]\d?%)?$"));
                Assert.That(summary.text, Does.Not.Contain("月行情").And.Not.Contain("投资")
                    .And.Not.Contain("下次补货").And.Not.Contain("\n"));
                Assert.That(summary.fontSize, Is.EqualTo(20));
                Assert.That(summary.font, Is.SameAs(GameTypography.BodyFont));
                Assert.That(summary.fontStyle, Is.EqualTo(FontStyle.Normal));
                Assert.That(summary.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(labels.Any(label => label.name == "Market Storage Mode Hint"), Is.False);
                Assert.That(summary.preferredHeight, Is.LessThanOrEqualTo(summary.rectTransform.rect.height + 1f));
                foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                {
                    string key = PortMarketCatalog.Key(item);
                    int buyLot = System.Math.Max(PortMarketCatalog.PurchaseStep(item), PortMarketCatalog.SaleStep(item));
                    Text purchase = labels.Single(label => label.name == "Market Buy Price " + key);
                    Text unit = labels.Single(label => label.name == "Market Buy Unit " + key);
                    Assert.That(purchase.text, Does.Match(@"^\d+(\.\d{1,3})? 金$"), key);
                    Assert.That(unit.text, Is.EqualTo("每" + buyLot + "单位"), key);
                    Assert.That(purchase.fontSize, Is.EqualTo(26), key);
                    Assert.That(purchase.font, Is.SameAs(GameTypography.InterfaceFont), key);
                    Assert.That(unit.fontSize, Is.EqualTo(18), key);
                    Assert.That(unit.font, Is.SameAs(GameTypography.BodyFont), key);
                    Assert.That(labels.Any(label => label.name == "Market Buy Title " + key ||
                        label.name == "Market Sell Title " + key || label.name == "Market Sell Price " + key), Is.False, key);
                    foreach (Text label in new[] { purchase, unit })
                    {
                        Assert.That(label.text, Does.Not.Contain("\n"), label.name);
                        Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), label.name);
                        Assert.That(label.alignment, Is.EqualTo(TextAnchor.MiddleCenter), label.name);
                        Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), label.name);
                    }
                }
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

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            Vector3[] corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
