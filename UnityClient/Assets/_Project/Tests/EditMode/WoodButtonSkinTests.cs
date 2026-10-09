using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WoodButtonSkinTests
    {
        private GameObject host;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void NativeWoodSourceHasUsableCornersAndMinificationLevels()
        {
            Sprite face = WoodButtonSkin.Face;
            Assert.That(face, Is.Not.Null);
            Assert.That(face.texture.width, Is.EqualTo(1774));
            Assert.That(face.texture.height, Is.EqualTo(887));
            Assert.That(face.border, Is.EqualTo(Vector4.one * 112f));
            Assert.That(face.texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(face.texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
        }

        [TestCase(30, 30)]
        [TestCase(108, 26)]
        [TestCase(230, 52)]
        [TestCase(740, 94)]
        public void ControlsKeepHitBoundsAndFitSmallLabels(float width, float height)
        {
            host = new GameObject("Confirm Test", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(13, 17);
            GameObject labelHost = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelHost.transform.SetParent(rect, false);
            Text label = labelHost.GetComponent<Text>();
            label.fontSize = 16;
            label.color = Color.black;
            Button button = host.GetComponent<Button>();
            Assert.That(WoodButtonSkin.Apply(button, Color.blue), Is.True);
            Image image = host.GetComponent<Image>();
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(width, height)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(13, 17)));
            Assert.That(image.sprite.border.x / image.pixelsPerUnitMultiplier, Is.InRange(3f, 9f));
            Assert.That(label.rectTransform.rect.width, Is.InRange(20f, width));
            Assert.That(label.rectTransform.rect.height, Is.LessThan(height));
            Assert.That(label.color, Is.EqualTo(WoodButtonSkin.Ink));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(button.colors.highlightedColor, Is.Not.EqualTo(button.colors.normalColor));
            Assert.That(button.colors.pressedColor, Is.Not.EqualTo(button.colors.normalColor));
            Assert.That(button.colors.disabledColor, Is.Not.EqualTo(button.colors.normalColor));
        }

        [TestCase("Battle Card 0", false)]
        [TestCase("Add Deck Card 0", false)]
        [TestCase("Dispatch Crew Toggle 0", false)]
        [TestCase("Event Node wreck", false)]
        [TestCase("Confirm Dispatch Deployment", true)]
        [TestCase("Supply Increase Water", true)]
        public void OnlyActionControlsReceiveBacking(string name, bool expected)
        {
            Assert.That(WoodButtonSkin.IsControl(name, Color.white), Is.EqualTo(expected));
            Assert.That(WoodButtonSkin.IsControl(name, Color.clear), Is.False, "Never materialize transparent scene hotspots.");
        }

        [TestCase("Execute Sail", WoodButtonRole.Primary)]
        [TestCase("End Battle Turn", WoodButtonRole.Primary)]
        [TestCase("Return To Port", WoodButtonRole.Primary)]
        [TestCase("Confirm Warehouse Cargo Adjustment", WoodButtonRole.Primary)]
        [TestCase("Recruit Tavern Crew Navigator", WoodButtonRole.Primary)]
        [TestCase("Back From Achievements", WoodButtonRole.Secondary)]
        [TestCase("Cancel Warehouse Cargo Sale", WoodButtonRole.Secondary)]
        [TestCase("Next Tavern Dossier Page", WoodButtonRole.Secondary)]
        [TestCase("Voyage Settings", WoodButtonRole.Secondary)]
        [TestCase("Safe Return", WoodButtonRole.Secondary)]
        [TestCase("Confirm New Game", WoodButtonRole.Danger)]
        [TestCase("Recall Dispatch Job", WoodButtonRole.Danger)]
        public void SemanticRolesDoNotTreatGoldNavigationOrRoutineTurnsAsDanger(string name, WoodButtonRole expected)
        {
            Assert.That(WoodButtonSkin.DefaultRoleFor(name), Is.EqualTo(expected));
        }

        [Test]
        public void ExplicitRoleAndSelectedTabSurviveReskinWithoutChangingActionsOrSecondaryLabels()
        {
            host = new GameObject("Back From Achievements", typeof(RectTransform), typeof(Image), typeof(Button));
            var button = host.GetComponent<Button>();
            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320, 50);
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(host.transform, false);
            var label = labelObject.GetComponent<Text>();
            label.text = "返回";
            label.fontSize = 22;
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            WoodButtonSkin.Apply(button, new Color(.95f, .72f, .22f));
            var state = host.GetComponent<WoodButtonRoleState>();
            Assert.That(state.Role, Is.EqualTo(WoodButtonRole.Secondary), "Gold navigation must remain secondary.");
            Assert.That(state.enabled, Is.False);
            Color secondary = button.colors.normalColor;
            var statusObject = new GameObject("Price Status", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(host.transform, false);
            var status = statusObject.GetComponent<Text>();
            status.color = Color.red;
            WoodButtonSkin.SetSelected(button, true);
            Assert.That(state.enabled && state.IsSelected, Is.True);
            WoodButtonSkin.SetRole(button, WoodButtonRole.Primary);
            Assert.That(state.IsSelected && state.HasExplicitRole, Is.True);
            Assert.That(status.color, Is.EqualTo(Color.red), "Selection and role changes preserve semantic status colors.");
            WoodButtonSkin.Apply(button, Color.blue);
            Assert.That(state.Role, Is.EqualTo(WoodButtonRole.Primary));
            Assert.That(state.IsSelected, Is.True);
            WoodButtonSkin.SetSelected(button, false);
            Assert.That(state.Role, Is.EqualTo(WoodButtonRole.Primary));
            Assert.That(state.enabled, Is.True, "A primary retains its mesh edge after deselection.");
            Assert.That(button.colors.normalColor.r, Is.GreaterThan(secondary.r * 1.5f), "The copper primary is visibly brighter than quiet wood.");
            Assert.That(host.GetComponents<WoodButtonRoleState>().Length, Is.EqualTo(1), "Restyling is idempotent.");
            Assert.That(host.GetComponentsInChildren<Graphic>().Length, Is.EqualTo(3), "The border adds no graphic or pointer surface.");
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(320, 50)));
            Assert.That(button.targetGraphic, Is.SameAs(host.GetComponent<Image>()));
            button.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
            WoodButtonSkin.SetRole(button, WoodButtonRole.Secondary);
            Assert.That(state.enabled, Is.False);
            Assert.That(button.colors.normalColor, Is.EqualTo(secondary));
        }

        [TestCase("Battle Card 0")]
        [TestCase("Add Deck Card 0")]
        [TestCase("Event Node wreck")]
        public void SemanticRoleCannotRepaintExcludedControls(string name)
        {
            host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var button = host.GetComponent<Button>();
            button.targetGraphic = host.GetComponent<Image>();
            ColorBlock before = button.colors;
            Assert.That(WoodButtonSkin.Apply(button, Color.white), Is.False);
            WoodButtonSkin.SetRole(button, WoodButtonRole.Primary);
            WoodButtonSkin.SetSelected(button, true);
            Assert.That(host.GetComponent<WoodButtonRoleState>(), Is.Null);
            Assert.That(button.colors, Is.EqualTo(before));
        }

        [TestCase(CoreGameplayPreviewScreen.ComponentShopDetail, "Back To Component Shop")]
        [TestCase(CoreGameplayPreviewScreen.DispatchDeployment, "Confirm Dispatch Deployment")]
        [TestCase(CoreGameplayPreviewScreen.Battle, "End Battle Turn")]
        [TestCase(CoreGameplayPreviewScreen.Maintenance, "Confirm Port Investment")]
        [TestCase(CoreGameplayPreviewScreen.Navigation, "Execute Sail")]
        [TestCase(CoreGameplayPreviewScreen.TavernCrewDetail, "Next Tavern Dossier Page")]
        [TestCase(CoreGameplayPreviewScreen.DeckBuilder, "Back To Card Workshop Chests")]
        [TestCase(CoreGameplayPreviewScreen.Shipyard, "Shipyard Next Vessel")]
        public void RuntimeFactoryAndEditorPreviewsUseTheSameSkin(CoreGameplayPreviewScreen screen, string name)
        {
            host = new GameObject("Wood Controls Preview Test");
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
            Button button = host.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            Assert.That(button.targetGraphic, Is.TypeOf<Image>());
            Assert.That(((Image)button.targetGraphic).sprite, Is.SameAs(WoodButtonSkin.Face));
            foreach (Button other in host.GetComponentsInChildren<Button>(true).Where(item =>
                item.name.StartsWith("Battle Card ") || item.name.StartsWith("Tavern Crew ") || item.name.StartsWith("Component Shop Offer ")))
                Assert.That(other.GetComponent<Image>().sprite, Is.Not.SameAs(WoodButtonSkin.Face), other.name);
        }

        [Test]
        public void MainMenuAndItsInactiveDialogsAreSkinned()
        {
            host = new GameObject("Wood Main Menu Test");
            host.AddComponent<MainMenuController>();
            Button[] buttons = host.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.GreaterThan(8));
            foreach (Button button in buttons)
                Assert.That(button.GetComponent<Image>().sprite, Is.SameAs(WoodButtonSkin.Face), button.name);
        }

        [Test]
        public void TutorialFactoryUsesWoodWithoutStartingTutorial()
        {
            host = new GameObject("Wood Tutorial Test");
            host.SetActive(false);
            var controller = host.AddComponent<TutorialVoyageController>();
            MethodInfo factory = typeof(TutorialVoyageController).GetMethod("CreateButton", BindingFlags.Instance | BindingFlags.NonPublic);
            Button button = (Button)factory.Invoke(controller, new object[] { "Primary Action", host.transform,
                new Vector2(520, 58), Vector2.zero, Color.blue, Color.white, 21 });
            Assert.That(button.GetComponent<Image>().sprite, Is.SameAs(WoodButtonSkin.Face));
        }
    }
}
