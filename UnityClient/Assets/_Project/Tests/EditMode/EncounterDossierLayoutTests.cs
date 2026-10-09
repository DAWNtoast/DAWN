using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class EncounterDossierLayoutTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        private int? previousReadingLevel;

        [SetUp]
        public void PrepareReadingSize()
        {
            previousReadingLevel = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = 0;
        }

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            GameReadability.OverrideLevel = previousReadingLevel;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RevealInEditMode_RestoresVisibleFinalPoseAndReacquiresDestroyedGroup(bool replaceDestroyedGroup)
        {
            Assert.That(Application.isPlaying, Is.False);
            host = new GameObject("Encounter Reveal Test", typeof(RectTransform));
            RectTransform card = host.GetComponent<RectTransform>();
            Vector2 position = new Vector2(24f, -16f);
            Vector3 scale = new Vector3(0.85f, 0.85f, 1f);
            card.anchoredPosition = position;
            card.localScale = scale;
            EncounterDossierRevealMotion reveal = host.AddComponent<EncounterDossierRevealMotion>();
            reveal.Play(card);
            CanvasGroup first = card.GetComponent<CanvasGroup>();
            Assert.That(first != null, Is.True);
            Assert.That(first.alpha, Is.EqualTo(1f));
            if (replaceDestroyedGroup) Object.DestroyImmediate(first);
            else first.alpha = 0f;

            reveal.Play(card);

            CanvasGroup current = card.GetComponent<CanvasGroup>();
            Assert.That(current != null, Is.True, "A destroyed Unity component must be reacquired, not used through CLR null semantics.");
            Assert.That(card.GetComponents<CanvasGroup>().Length, Is.EqualTo(1));
            Assert.That(current.alpha, Is.EqualTo(1f), "Editor preview must show the completed entrance immediately.");
            Assert.That(card.anchoredPosition, Is.EqualTo(position));
            Assert.That(card.localScale, Is.EqualTo(scale), "Refreshing a preview must not accumulate its entrance scale/offset.");
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(0, 2)]
        [TestCase(1, 2)]
        [TestCase(2, 2)]
        public void ThreeChoicesAndSkip_StayInsidePaperAndClearOfNarrative(int routeIndex, int level)
        {
            GameReadability.OverrideLevel = level;
            CreatePreview(routeIndex);
            CoreEncounter encounter = ThreeChoiceEncounter(false);
            ShowCoreEncounter(encounter);

            AssertDossierLayout(false, 3, true);
            for (int index = 0; index < encounter.Choices.Count; index++)
                AssertChoiceText(Find<Button>(host.transform, "Encounter Choice " + index),
                    encounter.Choices[index].Label, encounter.Choices[index].Detail);
            Assert.That(Find<Text>(host.transform, "Encounter Description").text,
                Is.EqualTo(encounter.Description));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EmbeddedEncounter_FinalDisplayScaleKeepsEveryGlyphAndFixedNotesAboveTheFrame(int level)
        {
            GameReadability.OverrideLevel = level;
            CreatePreview(0);
            using (var viewport = new NavigationDashboardTests.DashboardViewport(host))
            {
                // Moving an ExecuteAlways preview into its render scene can
                // rebuild the stock event. Install the custom four-action case
                // after the fixture owns that scene/canvas.
                ShowCoreEncounter(ThreeChoiceEncounter(false));
                foreach (Transform child in viewport.Canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    viewport.ResizeAndRender(size.x, size.y);
                    RectTransform card = Find<RectTransform>(host.transform, "Encounter Card");
                    RectTransform paper = Find<RectTransform>(card, "Encounter Paper Content");
                    ScrollRect scroll = Find<ScrollRect>(card, "Encounter Decision Viewport");
                    scroll.verticalNormalizedPosition = 1;
                    Canvas.ForceUpdateCanvases();
                    foreach (Text label in card.GetComponentsInChildren<Text>()) AssertEveryGlyphFits(label);
                    AssertDossierLayout(false, 3, true);
                    foreach (string name in new[] { "Encounter Terrain", "Encounter Decision Footer" })
                    {
                        Text note = Find<Text>(card, name);
                        Assert.That(BoundsIn(paper, note.rectTransform).yMin, Is.GreaterThanOrEqualTo(-184.1f),
                            name + " must stay above the bottom rope, medallion and chest at " + size);
                    }
                    Assert.That(BoundsIn(paper, Find<RectTransform>(card, "Encounter Title")).yMax,
                        Is.LessThanOrEqualTo(216.1f), "Title must clear the top compass and rope artwork.");
                    Assert.That(Find<Text>(card, "Encounter Decision Footer").alignment, Is.EqualTo(TextAnchor.MiddleLeft),
                        "The complete reminder starts on clean paper instead of ending over the right-hand chest.");
                    AssertClearGap(BoundsIn(paper, Find<RectTransform>(card, "Encounter Illustration Mount")),
                        BoundsIn(paper, Find<RectTransform>(card, "Encounter Location")), "Art / location");
                    AssertClearGap(BoundsIn(paper, Find<RectTransform>(card, "Encounter Location")),
                        BoundsIn(paper, Find<RectTransform>(card, "Encounter Terrain")), "Location / pause note");
                    NavigationDashboardLayout layout = Find<NavigationDashboardLayout>(host.transform, "Navigation Dashboard Layout");
                    layout.Apply();
                    layout.Apply();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f),
                        "Final-scale reflow preserves the last choice being read.");
                    Assert.That(card.GetComponentsInChildren<ScrollRect>().Length, Is.EqualTo(1));
                    foreach (Text label in card.GetComponentsInChildren<Text>()) AssertEveryGlyphFits(label);
                }
            }
        }

        [Test]
        public void MandatoryEncounter_HasEveryChoiceAndNoSkipAction()
        {
            CreatePreview(0);
            ShowCoreEncounter(ThreeChoiceEncounter(true));

            AssertDossierLayout(false, 3, false);
            Assert.That(controller.Session.SkipEncounter(), Is.False);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        [TestCase(true, 1)]
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        [TestCase(false, 2)]
        public void Tutorial_LongNarrativeAndCrewChoiceFitWithCorrectSkipVisibility(bool optional, int level)
        {
            GameReadability.OverrideLevel = level;
            CreatePreview(0);
            controller.TutorialSession.BeginFirstLeg();
            TutorialEncounter encounter = new TutorialEncounter(
                "tutorial_crew_requirement_event", "陌生海图暗语", "特殊船员需求",
                "瞭望手发现海盗正借礁影包抄。首次航行不适合接战，只能立刻转向并返回断锚港。",
                optional, false,
                new TutorialChoice("航海士校准星位", "显示隐藏地点，后续 2 航段航速 +1", 2,
                    new TutorialEffect { RevealHidden = true, SpeedBoost = 1f, SpeedBoostLegs = 2 }, "navigator"),
                new TutorialChoice("让普通水手逐行核对", "金币 +12", 4, new TutorialEffect { Gold = 12 }));
            SetProperty(controller.TutorialSession, "CurrentEncounter", encounter);
            SetProperty(controller.TutorialSession, "Phase", TutorialVoyagePhase.ResolvingEncounter);
            ClearPhaseContent();
            InvokeController("BuildTutorialEncounterOverlay");

            AssertDossierLayout(true, 2, optional);
            Assert.That(Find<Text>(host.transform, "Tutorial Encounter Body").text,
                Is.EqualTo(encounter.Description));
            for (int index = 0; index < encounter.Choices.Count; index++)
            {
                Button choice = Find<Button>(host.transform, "Tutorial Choice " + index);
                AssertChoiceText(choice, encounter.Choices[index].Label, encounter.Choices[index].Detail);
                Assert.That(choice.interactable, Is.EqualTo(controller.TutorialSession.IsChoiceAvailable(index)));
            }
            Assert.That(Find<Button>(host.transform, "Tutorial Choice 0").interactable, Is.False);
            Assert.That(Find<Button>(host.transform, "Tutorial Choice 1").interactable, Is.True);
        }

        [Test]
        public void EveryRoute_UsesDistinctFrameArtworkAndNonBlockingIllustrations()
        {
            CreatePreview(0);
            HashSet<Texture> frames = new HashSet<Texture>();
            for (int routeIndex = 0; routeIndex < 3; routeIndex++)
            {
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter,
                    CoreGameplayPreviewPort.GrayGull, routeIndex);
                ShowCoreEncounter(ThreeChoiceEncounter(false));
                RawImage frame = Find<RawImage>(host.transform, "Encounter Themed Frame");
                RawImage parchment = Find<RawImage>(host.transform, "Encounter Parchment");
                Assert.That(frame.texture, Is.Not.Null, "Route " + routeIndex);
                Assert.That(parchment.texture, Is.Not.Null, "Route " + routeIndex);
                Assert.That(frames.Add(frame.texture), Is.True, "Each route should have its own themed frame.");
                AssertDecorationsDoNotInterceptInput(Find<RectTransform>(host.transform, "Encounter Card"));
            }
        }

        [TestCase("navigator")]
        [TestCase("carpenter")]
        [TestCase("cook")]
        [TestCase("doctor")]
        [TestCase("gunner")]
        [TestCase("diver")]
        [TestCase("unknown_profession")]
        [TestCase("resources")]
        [TestCase("capacity")]
        public void LockedChoice_PreservesSessionAvailabilityAndRemainsVisible(string restriction)
        {
            CreatePreview(0);
            SetProperty(controller.Session, "Phase", CoreDemoPhase.Port);
            controller.Session.ConfigureCrewCapabilities(false, false, false, false, false, false);
            VoyageCargo cargo = new VoyageCargo { Water = 10, Food = 10 };
            SetProperty(controller.Session, "Cargo", cargo);
            VoyageEffect effect = new VoyageEffect();
            string requiredCrew = string.Empty;
            if (restriction != "resources" && restriction != "capacity") requiredCrew = restriction;
            if (restriction == "resources") effect.Gold = -1;
            if (restriction == "capacity")
            {
                cargo.Wood = Mathf.CeilToInt(controller.Session.CargoCapacityValue);
                effect.Wood = 1;
            }
            ShowCoreEncounter(new CoreEncounter("encounter_locked_test", "受限选择", "事件",
                "船员、资源和货舱条件由航海规则判定。", false, false, 1, 0,
                new CoreEncounterChoice("检查特殊条件", "满足条件后可以执行", 1, effect, requiredCrew)));

            Button choice = Find<Button>(host.transform, "Encounter Choice 0");
            Assert.That(choice.gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.Session.IsChoiceAvailable(0), Is.False, restriction);
            Assert.That(choice.interactable, Is.False, restriction);
            if (requiredCrew.Length > 0)
            {
                string[] professions = { "navigator", "carpenter", "cook", "doctor", "gunner", "diver" };
                string[] names = { "航海士", "木匠", "厨师", "船医", "炮手", "潜水员" };
                int profession = System.Array.IndexOf(professions, requiredCrew);
                string expectedName = profession >= 0 ? names[profession] : "指定职业船员";
                string expectedReason = "缺少所需船员：" + expectedName + "。";
                Assert.That(controller.Session.ChoiceUnavailableReason(0), Is.EqualTo(expectedReason));
                string hours = choice.GetComponentsInChildren<Text>().Single(text => text.name == "Hours").text;
                Assert.That(hours, Does.Contain(expectedReason), "UI must show the core rule's localized explanation.");
                Assert.That(hours, Does.Not.Contain(requiredCrew), "Internal profession IDs are not player-facing copy.");
                Assert.That(choice.GetComponentsInChildren<Text>().Single(text => text.name == "Detail").text,
                    Does.Contain("需要" + expectedName));
            }
            Assert.That(controller.Session.ResolveChoice(0), Is.False, restriction);
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Encounter));
            Assert.That(Find<Button>(host.transform, "Skip Encounter").interactable, Is.True);
        }

        [Test]
        public void ChoiceButton_ResolvesItsOwnIndexAndClosesEncounter()
        {
            CreatePreview(0);
            // This test verifies the choice callback, not ration availability after the preview voyage.
            SetProperty(controller.Session, "Cargo", new VoyageCargo { Gold = 100, Water = 10, Food = 10 });
            ShowCoreEncounter(ThreeChoiceEncounter(false));
            double hours = controller.Session.TotalHours;

            Assert.That(controller.Session.IsChoiceAvailable(2), Is.True);
            Find<Button>(host.transform, "Encounter Choice 2").onClick.Invoke();

            Assert.That(controller.Session.Cargo.Gold, Is.EqualTo(130));
            Assert.That(controller.Session.TotalHours, Is.EqualTo(hours + 1).Within(0.0001));
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.ActiveEncounter, Is.Null);
            Assert.That(FindOptional<RectTransform>(host.transform, "Encounter Card"), Is.Null);
        }

        [Test]
        public void SkipButton_ReturnsToSailingWithoutSpendingEventTimeOrRations()
        {
            CreatePreview(0);
            ShowCoreEncounter(ThreeChoiceEncounter(false));
            double hours = controller.Session.TotalHours;
            int water = controller.Session.Cargo.Water;
            int food = controller.Session.Cargo.Food;
            int gold = controller.Session.Cargo.Gold;

            Find<Button>(host.transform, "Skip Encounter").onClick.Invoke();

            Assert.That(controller.Session.TotalHours, Is.EqualTo(hours));
            Assert.That(controller.Session.Cargo.Water, Is.EqualTo(water));
            Assert.That(controller.Session.Cargo.Food, Is.EqualTo(food));
            Assert.That(controller.Session.Cargo.Gold, Is.EqualTo(gold));
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(controller.Session.ActiveEncounter, Is.Null);
        }

        private void CreatePreview(int routeIndex)
        {
            host = new GameObject("Encounter Dossier Layout Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter,
                CoreGameplayPreviewPort.GrayGull, routeIndex);
            host.SetActive(true);
            Assert.That(controller.Session, Is.Not.Null);
            Assert.That(controller.Session.Map, Is.Not.Null);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TreasureStory_ReadingSizesKeepDialogueComicAndActionsSeparated(int level)
        {
            GameReadability.OverrideLevel = level;
            CreatePreview(0);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo build = typeof(CoreGameplayDemoController).GetMethod("BuildTreasureStoryOverlay", flags);
            FieldInfo page = typeof(CoreGameplayDemoController).GetField("treasureStoryPage", flags);
            foreach (TreasureStoryChapter chapter in CoreTreasureStoryCatalog.Chapters)
            {
                var stories = new List<TreasureBattleStory> { null };
                stories.AddRange(chapter.LegendaryIds.Select(CoreTreasureStoryCatalog.FindBattle));
                foreach (TreasureBattleStory battle in stories)
                for (int index = 0; index < 2; index++)
                {
                    ClearPhaseContent();
                    page.SetValue(controller, index);
                    build.Invoke(controller, new object[] { chapter, battle, "reading-test" });
                    Canvas.ForceUpdateCanvases();
                    RectTransform card = Find<RectTransform>(host.transform, "Treasure Story Card");
                    Text dialogue = Find<Text>(card, "Treasure Story Dialogue");
                    Assert.That(dialogue.text, Is.EqualTo(battle == null ? chapter.Pages[index] : battle.Pages[index]));
                    Assert.That(dialogue.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
                    AssertTextFits(dialogue);
                    Text heading = Find<Text>(card, "Treasure Story Heading");
                    Assert.That(heading.fontSize, Is.EqualTo(GameReadability.FontSize(32)));
                    Assert.That(heading.font, Is.SameAs(GameTypography.InterfaceFont));
                    AssertTextFits(heading);
                    string[] regions = { "Treasure Story Eyebrow", "Treasure Story Heading", "Treasure Story Skip",
                        "Treasure Story Comic Frame", "Treasure Story Dialogue Rail", "Treasure Story Previous", "Treasure Story Next" };
                    var bounds = new List<Rect>();
                    foreach (string name in regions)
                    {
                        Rect rect = BoundsIn(card, Find<RectTransform>(card, name));
                        AssertContained(card.rect, rect, name);
                        foreach (Rect other in bounds) AssertClearGap(rect, other, name);
                        bounds.Add(rect);
                    }
                    RawImage comic = Find<RawImage>(card, "Treasure Story Comic Page");
                    RectTransform art = Find<RectTransform>(card, "Treasure Story Comic Frame");
                    AssertContained(art.rect, BoundsIn(art, comic.rectTransform), "Complete comic image");
                    Assert.That(comic.rectTransform.rect.width / comic.rectTransform.rect.height,
                        Is.EqualTo((float)comic.texture.width / comic.texture.height).Within(.001f));
                    ScrollRect scroll = dialogue.GetComponentInParent<ScrollRect>();
                    scroll.verticalNormalizedPosition = 0;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(BoundsIn(scroll.viewport, dialogue.rectTransform).yMin,
                        Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - .1f), "The last dialogue line remains reachable.");
                }
            }
        }

        private void ShowCoreEncounter(CoreEncounter encounter)
        {
            SetProperty(controller.Session, "ActiveEncounter", encounter);
            SetProperty(controller.Session, "Phase", CoreDemoPhase.Encounter);
            ClearPhaseContent();
            InvokeController("BuildEncounterOverlay");
        }

        private void ClearPhaseContent()
        {
            Transform content = Find<RectTransform>(host.transform, "Phase Content");
            for (int index = content.childCount - 1; index >= 0; index--)
                Object.DestroyImmediate(content.GetChild(index).gameObject);
        }

        private static CoreEncounter ThreeChoiceEncounter(bool mandatory)
        {
            return new CoreEncounter("event_reef_broken_mast", "断桅求援", "航海事件",
                "远海首领舰盘踞在十日航程之外，控制着通往新港的关键海域。",
                mandatory, false, 3, 0,
                new CoreEncounterChoice("记录星位", "声望 +8、显示 2 个隐藏点", 0.5, new VoyageEffect { Gold = 10 }),
                new CoreEncounterChoice("改变航向", "安全通过，主要成本来自时间", 0, new VoyageEffect { Gold = 20 }),
                new CoreEncounterChoice("木匠加固后打捞", "啤酒 -1、金币 +80、士气 -1", 1, new VoyageEffect { Gold = 30 }));
        }

        private void AssertDossierLayout(bool tutorial, int choiceCount, bool optional)
        {
            string prefix = tutorial ? "Tutorial Encounter" : "Encounter";
            string choicePrefix = tutorial ? "Tutorial Choice " : "Encounter Choice ";
            string skipName = tutorial ? "Tutorial Skip Encounter" : "Skip Encounter";
            RectTransform card = Find<RectTransform>(host.transform, prefix + " Card");
            RectTransform paper = Find<RectTransform>(card, prefix + " Paper Content");
            RectTransform choices = Find<RectTransform>(card, tutorial ? "Tutorial Choices" : "Encounter Choices");
            ScrollRect scroll = choices.GetComponentInParent<ScrollRect>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(choices);
            Canvas.ForceUpdateCanvases();

            Text title = Find<Text>(card, prefix + " Title");
            Text description = Find<Text>(card, tutorial ? "Tutorial Encounter Body" : "Encounter Description");
            Rect titleBounds = BoundsIn(paper, title.rectTransform);
            Rect descriptionBounds = BoundsIn(paper, description.rectTransform);
            AssertContained(paper.rect, titleBounds, title.name);
            AssertContained(choices.rect, BoundsIn(choices, description.rectTransform), description.name);
            AssertContained(paper.rect, BoundsIn(paper, scroll.viewport), "Reading viewport / writable paper");
            AssertContained(scroll.viewport.rect, BoundsIn(scroll.viewport, description.rectTransform),
                "A fresh encounter starts with the whole narrative visible");
            AssertClearGap(titleBounds, descriptionBounds, "Title and narrative");
            AssertTextFits(title);
            AssertTextFits(description);
            Assert.That(title.fontSize, Is.EqualTo(GameReadability.FontSize(32)));
            Assert.That(title.font, Is.SameAs(GameTypography.InterfaceFont));
            Assert.That(description.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            // The viewport stays within the writable paper; long text and all
            // decision rows must be reachable through its real scroll range.
            Text badge = Find<Text>(card, prefix + " Badge");
            Text footer = Find<Text>(card, prefix + " Decision Footer");
            Rect badgeBounds = BoundsIn(paper, badge.rectTransform);
            AssertContained(paper.rect, badgeBounds, "Badge / writable paper");
            AssertClearGap(badgeBounds, titleBounds, "Badge and title");
            AssertContained(paper.rect, BoundsIn(paper, footer.rectTransform), "Footer / writable paper");
            AssertClearGap(BoundsIn(paper, scroll.viewport), badgeBounds, "Badge above reading viewport");
            AssertClearGap(BoundsIn(paper, scroll.viewport), BoundsIn(paper, footer.rectTransform), "Footer below reading viewport");
            Assert.That(scroll.viewport.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);

            Button[] buttons = choices.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(choiceCount + (optional ? 1 : 0)));
            Assert.That(FindOptional<Button>(card, skipName) != null, Is.EqualTo(optional));
            List<Rect> actionBounds = new List<Rect>();
            for (int index = 0; index < choiceCount; index++)
            {
                Button choice = Find<Button>(choices, choicePrefix + index);
                foreach (string field in new[] { "Label", "Detail", "Hours" })
                {
                    Text text = Find<Text>(choice.transform, field);
                    AssertTextFits(text);
                    AssertContained(choice.GetComponent<RectTransform>().rect,
                        BoundsIn(choice.transform, text.rectTransform), choice.name + " / " + field);
                }
                Text label = Find<Text>(choice.transform, "Label");
                Text detail = Find<Text>(choice.transform, "Detail");
                Text hours = Find<Text>(choice.transform, "Hours");
                AssertClearGap(BoundsIn(paper, label.rectTransform), BoundsIn(paper, detail.rectTransform), choice.name);
                AssertClearGap(BoundsIn(paper, label.rectTransform), BoundsIn(paper, hours.rectTransform), choice.name);
                AssertClearGap(BoundsIn(paper, detail.rectTransform), BoundsIn(paper, hours.rectTransform), choice.name);
            }
            foreach (Button button in buttons)
            {
                Rect bounds = BoundsIn(paper, button.GetComponent<RectTransform>());
                AssertContained(choices.rect, BoundsIn(choices, button.GetComponent<RectTransform>()), button.name);
                AssertVisibleClearGap(titleBounds, bounds, BoundsIn(paper, scroll.viewport), button.name + " / title");
                AssertClearGap(descriptionBounds, bounds, button.name + " / narrative");
                Assert.That(button.targetGraphic, Is.Not.Null, button.name);
                Assert.That(button.targetGraphic.raycastTarget, Is.True, button.name);
                foreach (Rect other in actionBounds) AssertClearGap(other, bounds, "Action buttons");
                actionBounds.Add(bounds);
            }
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            Button last = buttons.Last();
            AssertContained(scroll.viewport.rect, BoundsIn(scroll.viewport, last.GetComponent<RectTransform>()), "Last decision after scrolling");
            foreach (Button button in buttons)
                AssertVisibleClearGap(titleBounds, BoundsIn(paper, button.GetComponent<RectTransform>()),
                    BoundsIn(paper, scroll.viewport), button.name + " / title after scrolling");
            AssertDecorationsDoNotInterceptInput(card);
        }

        private static void AssertChoiceText(Button button, string label, string detail)
        {
            Assert.That(Find<Text>(button.transform, "Label").text, Does.Contain(label));
            Assert.That(Find<Text>(button.transform, "Detail").text, Does.Contain(detail));
            Assert.That(Find<Text>(button.transform, "Hours").text, Is.Not.Empty);
        }

        private static void AssertDecorationsDoNotInterceptInput(Transform card)
        {
            foreach (RawImage image in card.GetComponentsInChildren<RawImage>(true))
            {
                Button button = image.GetComponentInParent<Button>();
                bool isButtonSurface = button != null && button.targetGraphic == image;
                Assert.That(image.raycastTarget, Is.EqualTo(isButtonSurface), image.name);
            }
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
                Assert.That(text.raycastTarget, Is.False, text.name);
        }

        private static void AssertTextFits(Text text)
        {
            Assert.That(text.text, Is.Not.Empty, text.name);
            Assert.That(text.resizeTextForBestFit, Is.False, text.name + " must keep the selected reading size");
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(18)), text.name);
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f),
                text.name + " must display all lines without vertical clipping.");
            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 0.5f),
                    text.name + " must fit within its horizontal field.");
        }

        private static void AssertEveryGlyphFits(Text text)
        {
            AssertTextFits(text);
            TextGenerationSettings settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            using (var displayed = new TextGenerator())
            using (var complete = new TextGenerator())
            {
                displayed.Populate(text.text, settings);
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                complete.Populate(text.text, settings);
                Assert.That(displayed.characterCount, Is.EqualTo(complete.characterCount),
                    text.name + " must generate every glyph at the final display scale: " + text.text);
            }
        }

        private static void AssertClearGap(Rect first, Rect second, string context)
        {
            float gap = Mathf.Max(first.xMin - second.xMax, second.xMin - first.xMax,
                first.yMin - second.yMax, second.yMin - first.yMax);
            Assert.That(gap, Is.GreaterThan(0.1f), context + " should have visible separation.");
        }

        private static void AssertVisibleClearGap(Rect fixedRegion, Rect scrollingRegion, Rect viewport, string context)
        {
            float left = Mathf.Max(scrollingRegion.xMin, viewport.xMin);
            float right = Mathf.Min(scrollingRegion.xMax, viewport.xMax);
            float bottom = Mathf.Max(scrollingRegion.yMin, viewport.yMin);
            float top = Mathf.Min(scrollingRegion.yMax, viewport.yMax);
            // Hidden rows can pass behind fixed headings while scrolling, but
            // every pixel inside the real mask must remain separated from them.
            if (right > left && top > bottom)
                AssertClearGap(fixedRegion, Rect.MinMaxRect(left, bottom, right, top), context);
        }

        private static void AssertContained(Rect outer, Rect inner, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 0.1f), context);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 0.1f), context);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 0.1f), context);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 0.1f), context);
        }

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

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, name);
            setter.Invoke(target, new[] { value });
        }

        private void InvokeController(string name)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            method.Invoke(controller, null);
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = FindOptional<T>(root, name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static T FindOptional<T>(Transform root, string name) where T : Component
        {
            return root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
        }
    }
}
