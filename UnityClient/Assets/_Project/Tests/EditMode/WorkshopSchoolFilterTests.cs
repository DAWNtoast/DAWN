using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WorkshopSchoolFilterTests
    {
        private static readonly string[] Schools = { "neutral", "fire", "arcane", "curse", "tide", "thunder", "blood" };
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }

        [Test]
        public void SevenSchoolButtonsFilterBaseCardsAndResetPagingBelowTheStageTabs()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardWorkshop);
            Assert.That(Field<string>("selectedWorkshopSchool"), Is.EqualTo("neutral"));
            OwnOrdinaryCardsForFilter();
            Click("Workshop Preview Stage Naval");
            string[] expectedLabels = { "无体系", "火焰", "秘术", "诅咒", "潮汐", "雷霆", "血契" };
            RectTransform paper = Find<RectTransform>("Workshop Preview Parchment");
            RectTransform filters = Find<RectTransform>("Workshop School Filters");
            Button[] buttons = filters.GetComponentsInChildren<Button>();
            CollectionAssert.AreEqual(Schools.Select(id => "Workshop School Filter " + id), buttons.Select(button => button.name));
            CollectionAssert.AreEqual(expectedLabels, buttons.Select(button => button.GetComponentInChildren<Text>().text));
            Rect filterBounds = BoundsIn(paper, filters);
            Assert.That(filterBounds.yMax, Is.LessThan(BoundsIn(paper, Find<RectTransform>("Workshop Preview Stage Naval")).yMin));
            foreach (Button button in buttons)
            {
                Rect bounds = BoundsIn(paper, button.GetComponent<RectTransform>());
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.rect.xMin));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(paper.rect.xMax));
                Text label = button.GetComponentInChildren<Text>();
                Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .5f));
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f));
            }
            for (int i = 1; i < buttons.Length; i++)
                Assert.That(BoundsIn(paper, buttons[i].GetComponent<RectTransform>()).yMax,
                    Is.LessThanOrEqualTo(BoundsIn(paper, buttons[i - 1].GetComponent<RectTransform>()).yMin - 15f));

            bool exercisedSecondPage = false;
            foreach (string school in Schools)
            {
                Click("Workshop School Filter " + school);
                int[] expected = BaseCards(school);
                Assert.That(expected, Is.Not.Empty, school + " has explicitly owned naval cards in this inventory fixture.");
                CollectionAssert.AreEqual(expected.Take(6), VisibleCards("Select Upgrade Card "));
                Assert.That(Field<string>("selectedWorkshopSchool"), Is.EqualTo(school));
                Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.False, "A different school starts on page one.");
                Assert.That(Find<Button>("Next Upgrade Cards").interactable, Is.EqualTo(expected.Length > 6));
                paper = Find<RectTransform>("Workshop Preview Parchment");
                filterBounds = BoundsIn(paper, Find<RectTransform>("Workshop School Filters"));
                foreach (CardSchoolFrameGraphic frame in Find<RectTransform>("Workshop Preview Collection").GetComponentsInChildren<CardSchoolFrameGraphic>())
                {
                    Rect bounds = BoundsIn(paper, frame.rectTransform);
                    Assert.That(bounds.xMin, Is.GreaterThan(filterBounds.xMax), "Cards leave the vertical school index unobstructed.");
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(paper.rect.yMin));
                }
                if (expected.Length > 6)
                {
                    Click("Next Upgrade Cards");
                    CollectionAssert.AreEqual(expected.Skip(6).Take(6), VisibleCards("Select Upgrade Card "));
                    Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.True);
                    exercisedSecondPage = true;
                }
            }
            Assert.That(exercisedSecondPage, Is.True);
            Assert.That(FindOptional<Transform>("Upgrade Collection Page"), Is.Null);
        }

        [Test]
        public void SpecialCollectionFiltersOwnedCardsBySchoolAndResetsItsOwnPage()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardWorkshop);
            controller.Session.AcquireSpecialCards(CoreVoyageSession.CardLibrary
                .Where(card => CoreVoyageSession.IsSpecialBattleCard(card.Id)).Select(card => card.Id).ToArray());
            Click("Open Special Card Collection");
            foreach (string school in new[] { "neutral", "tide", "fire" })
            {
                Click("Workshop School Filter " + school);
                int[] expected = Ordered(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Where(index =>
                    controller.Session.AcquiredSpecialCardIds.Contains(CoreVoyageSession.CardLibrary[index].Id) &&
                    CoreVoyageSession.IsSpecialBattleCard(CoreVoyageSession.CardLibrary[index].Id) &&
                    CardSchoolArtCatalog.NormalizeSchool(controller.Session.GetBattleCard(index, NavalBattleStage.Naval).SchoolId) == school));
                Assert.That(expected, Is.Not.Empty, school);
                CollectionAssert.AreEqual(expected.Take(6), VisibleCards("Select Special Card "));
                Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.False);
                Assert.That(Find<Button>("Next Upgrade Cards").interactable, Is.EqualTo(expected.Length > 6));
                Assert.That(FindOptional<Transform>("Confirm Battle Card Upgrade"), Is.Null);
                if (expected.Length > 6)
                {
                    Click("Next Upgrade Cards");
                    CollectionAssert.AreEqual(expected.Skip(6).Take(6), VisibleCards("Select Special Card "));
                    Assert.That(Find<Button>("Previous Upgrade Cards").interactable, Is.True);
                }
            }
        }

        [Test]
        public void PreviewAndEditorKeepTheSchoolWhileIncompleteDraftCanStillChangeFilters()
        {
            CreatePreview(CoreGameplayPreviewScreen.CardWorkshop);
            OwnOrdinaryCardsForFilter();
            Click("Workshop Preview Stage Naval");
            Click("Workshop School Filter fire");
            int[] previewCards = VisibleCards("Select Upgrade Card ");
            Click("Workshop Page DeckBuilder"); Click("Battle Deck Stage Tab Naval");
            CollectionAssert.AreEqual(previewCards, VisibleCards("Add Deck Card "));
            Assert.That(Field<string>("selectedWorkshopSchool"), Is.EqualTo("fire"));
            Assert.That(FindOptional<Transform>("Battle Card Collection Page"), Is.Null);
            Click("Back To Card Workshop Chests");
            CollectionAssert.AreEqual(previewCards, VisibleCards("Select Upgrade Card "));
            Assert.That(Field<string>("selectedWorkshopSchool"), Is.EqualTo("fire"));
            Click("Workshop Page DeckBuilder"); Click("Battle Deck Stage Tab Naval");

            int[] validDeck = BaseCards("neutral").Take(CoreVoyageSession.MinimumBaseBattleDeckSize).ToArray();
            Assert.That(validDeck, Has.Length.EqualTo(CoreVoyageSession.MinimumBaseBattleDeckSize));
            Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Naval, validDeck), Is.True);
            Click("Battle Deck Stage Tab Naval");
            Button remove = host.GetComponentsInChildren<Button>().First(button => button.name.StartsWith("Remove Deck Card "));
            remove.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            List<int> draft = Field<List<int>>("deckBuilderDraft");
            Assert.That(draft, Has.Count.EqualTo(CoreVoyageSession.MinimumBaseBattleDeckSize - 1));
            Click("Workshop School Filter tide");
            Assert.That(Field<string>("selectedWorkshopSchool"), Is.EqualTo("tide"), "Finding replacement cards must remain available with an incomplete deck.");
            Assert.That(Field<List<int>>("deckBuilderDraft"), Is.SameAs(draft), "Filtering does not discard the unsaved deck.");
            CollectionAssert.AreEqual(BaseCards("tide").Take(6), VisibleCards("Add Deck Card "));
            CollectionAssert.AreEqual(validDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Only the saved valid deck supplies gameplay until the draft is complete.");
            Assert.That(Find<Text>("Deck Builder Base Column Count").text, Is.EqualTo("基础卡 9 张"));
            Assert.That(Find<Button>("Previous Battle Card Collection Page").interactable, Is.False);
        }

        private void CreatePreview(CoreGameplayPreviewScreen screen)
        {
            host = new GameObject("Workshop School Filters Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 1);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            Canvas.ForceUpdateCanvases();
            if (screen == CoreGameplayPreviewScreen.CardWorkshop) Click("Workshop Entrance Collection");
        }

        private void OwnOrdinaryCardsForFilter()
        {
            Assert.That(controller.Session.RestoreCardPackProgress(new CoreCardPackProgress {
                randomState = 1234567u,
                cards = controller.Session.GetCardPackPool("neutral").Select(index => new CoreCardCopySave {
                    id = CoreVoyageSession.CardLibrary[index].Id, count = 2
                }).ToArray()
            }), Is.True, "School filtering needs owned cards; pirate levels no longer grant card collections.");
        }

        private int[] BaseCards(string school) => Ordered(controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval).Where(index =>
        {
            CoreBattleCard card = controller.Session.GetBattleCard(index, NavalBattleStage.Naval);
            return !CoreVoyageSession.IsSpecialBattleCard(card.Id) && string.IsNullOrEmpty(card.RequiredCrew) &&
                string.IsNullOrEmpty(card.RequiredComponent) && CardSchoolArtCatalog.NormalizeSchool(card.SchoolId) == school;
        }));

        private static int[] Ordered(IEnumerable<int> indices) => indices.Distinct()
            .OrderBy(index => CoreBattleCardNumbering.Number(CoreVoyageSession.CardLibrary[index])).ThenBy(index => index).ToArray();

        private int[] VisibleCards(string prefix) => host.GetComponentsInChildren<Button>()
            .Where(button => button.name.StartsWith(prefix)).Select(button => int.Parse(button.name.Substring(prefix.Length))).ToArray();

        private void Click(string name)
        {
            Button button = Find<Button>(name);
            Assert.That(button.interactable, Is.True, name);
            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }

        private T Field<T>(string name) => (T)typeof(CoreGameplayDemoController).GetField(name, PrivateInstance).GetValue(controller);
        private T FindOptional<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
        private T Find<T>(string name) where T : Component
        {
            T value = FindOptional<T>(name);
            Assert.That(value, Is.Not.Null, name);
            return value;
        }

        private static Rect BoundsIn(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector3[] local = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(point => point.x), local.Min(point => point.y), local.Max(point => point.x), local.Max(point => point.y));
        }
    }
}
