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

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
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

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ThreeChoicesAndSkip_StayInsidePaperAndClearOfNarrative(int routeIndex)
        {
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

        [Test]
        public void ExplorationChoices_KeepLongRewardsAndClueConditionsReadableInsideTheScroll()
        {
            CreatePreview(0);
            string detail = "木材 -3、古代材料 +2、船体 +8；航速 +25% / 16 小时、瞭望范围 +35% / 18 小时。" +
                "取得潮工船坞图（本航次），标出潮痕秘藏；调查后可解锁深处的机关，已有线索仍会保留。";
            ShowCoreEncounter(new CoreEncounter("exploration_layout_test", "潮工留下的航海记录", "线索调查",
                "船员在旧灯室找到一份需要仔细解读的航海记录。", false, false, 1, 0,
                new CoreEncounterChoice("逐项核对船坞图并准备深入秘藏的补给", detail, 2, new VoyageEffect()),
                new CoreEncounterChoice("根据线索打开秘藏深处的机关", detail, 3, new VoyageEffect(),
                    requiredDiscovery: "reef_dock_chart"),
                new CoreEncounterChoice("抄录完整调查记录并整理下一次探索的准备事项", detail, 2, new VoyageEffect())));

            AssertDossierLayout(false, 3, true);
            Button first = Find<Button>(host.transform, "Encounter Choice 0");
            Text description = Find<Text>(first.transform, "Detail");
            Assert.That(description.text, Is.EqualTo(detail));
            Assert.That(description.resizeTextForBestFit, Is.False);
            Assert.That(description.fontSize, Is.GreaterThanOrEqualTo(20));
            Assert.That(first.GetComponent<RectTransform>().rect.height, Is.GreaterThan(96f));
            Assert.That(Find<ScrollRect>(host.transform, "Encounter Decision Viewport").vertical, Is.True);
            Button locked = Find<Button>(host.transform, "Encounter Choice 1");
            Assert.That(locked.interactable, Is.False);
            Assert.That(Find<Text>(locked.transform, "Requirement").text, Does.Contain("潮工船坞图"));
            Assert.That(Find<Text>(locked.transform, "Hours").text, Does.Not.Contain("潮工船坞图"));
            Assert.That(Find<Text>(locked.transform, "Detail").text, Is.EqualTo(detail));
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

        [Test]
        public void LongNarrative_ScrollsIndependentlyWithoutMovingChoicesOrFixedActions()
        {
            CreatePreview(0);
            string description = string.Concat(Enumerable.Repeat(
                "船员将残缺的记录逐行展开，海风掀起纸角，远处的灯塔仍在指引航路。\n", 16));
            ShowCoreEncounter(new CoreEncounter("long_record", "海风中的航海记录", "线索调查", description,
                false, false, 1, 0, new CoreEncounterChoice("收好记录", "保留线索并继续航行", 0, new VoyageEffect())));
            AssertJournalLayout(1, true);
            ScrollRect narrative = Find<ScrollRect>(host.transform, "Encounter Description Viewport");
            ScrollRect decisions = Find<ScrollRect>(host.transform, "Encounter Decision Viewport");
            RectTransform paper = Find<RectTransform>(host.transform, "Encounter Paper Content");
            RectTransform secondary = Find<RectTransform>(host.transform, "Encounter Secondary Actions");
            Vector2 choicePosition = decisions.content.anchoredPosition;
            Rect fixedBounds = BoundsIn(paper, secondary);
            Text body = Find<Text>(host.transform, "Encounter Description");
            Assert.That(body.text, Is.EqualTo(description));
            Assert.That(narrative.vertical, Is.True);
            Assert.That(narrative.content.rect.height, Is.GreaterThan(narrative.viewport.rect.height));
            narrative.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(BoundsIn(narrative.viewport, body.rectTransform).yMin,
                Is.EqualTo(narrative.viewport.rect.yMin).Within(.5f), "The final narrative line must be reachable.");
            Assert.That(decisions.content.anchoredPosition, Is.EqualTo(choicePosition));
            Assert.That(BoundsIn(paper, secondary), Is.EqualTo(fixedBounds));
        }

        [Test]
        public void EmergencyTow_StaysOutsideChoiceScrollAndResolvesToFailureSettlement()
        {
            CreatePreview(0);
            SetProperty(controller.Session, "Cargo", new VoyageCargo { Water = 10, Food = 10 });
            ShowCoreEncounter(new CoreEncounter("blocked_mandatory", "补给耗尽", "强制事件", "船只只能请求拖带。",
                true, false, 1, 0,
                new CoreEncounterChoice("支付拖航费", "金币 -100", 0, new VoyageEffect { Gold = -100 })));
            Assert.That(controller.Session.CanRequestEmergencyTow(), Is.True);
            AssertJournalLayout(1, false);
            Button rescue = Find<Button>(host.transform, "Emergency Encounter Return");
            Assert.That(rescue.transform.parent.name, Is.EqualTo("Encounter Secondary Actions"));
            Assert.That(Find<Text>(rescue.transform, "Hours").text, Is.EqualTo("返回港口"));

            rescue.onClick.Invoke();

            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Settlement));
            Assert.That(controller.Session.Settlement.Outcome, Is.EqualTo(VoyageOutcome.Failure));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Tutorial_LongNarrativeAndCrewChoiceFitWithCorrectSkipVisibility(bool optional)
        {
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
        public void EventIllustration_SharesTheHeaderWithReadableProseWithoutStretchingSubjects()
        {
            CreatePreview(0);
            ShowCoreEncounter(ThreeChoiceEncounter(false));
            RawImage art = Find<RawImage>(host.transform, "Demo Event Art");
            RectTransform narrative = Find<RectTransform>(host.transform, "Encounter Description Viewport");
            Assert.That(art.rectTransform.rect.width, Is.GreaterThanOrEqualTo(240f),
                "Keep the painting readable while making room for the prose and decisions.");
            Assert.That(narrative.rect.width, Is.GreaterThanOrEqualTo(art.rectTransform.rect.width),
                "The event description must no longer be squeezed beside an oversized illustration.");
            float sourceAspect = art.texture.width * art.uvRect.width / (art.texture.height * art.uvRect.height);
            float displayAspect = art.rectTransform.rect.width / art.rectTransform.rect.height;
            Assert.That(displayAspect, Is.EqualTo(4f / 3f).Within(.0001f),
                "Reflowing the painting must preserve the existing 4:3 composition.");
            Assert.That(sourceAspect, Is.EqualTo(displayAspect).Within(0.0001f),
                "The event crop must preserve painted faces, vessels and objects in the current illustration box.");
            Assert.That(((Texture2D)art.texture).mipmapCount, Is.GreaterThan(1),
                "High-resolution event paintings must minify cleanly in the expanded event panel.");
            Assert.That(art.texture, Is.SameAs(Resources.Load<Texture2D>(
                EncounterIllustrationCatalog.ReviewedResourceRoot + "reef_broken_mast")));
            Assert.That(EncounterIllustrationCatalog.TryGetLocation("event_reef_broken_mast", out var location), Is.True);
            Assert.That(location.Uv, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "The reviewed scene starts from the standalone painting, not a legacy atlas cell.");
            // The landscape painting fills the 4:3 window after an eight-pixel mip guard.
            // Preserve all safe vertical pixels and crop equal amounts from the left/right.
            const float guardPixels = 8f;
            float cropHeight = art.texture.height - 2f * guardPixels;
            float cropWidth = cropHeight * 4f / 3f;
            Assert.That(cropWidth, Is.LessThan(art.texture.width - 2f * guardPixels));
            Assert.That(art.uvRect.xMin * art.texture.width,
                Is.EqualTo((art.texture.width - cropWidth) * .5f).Within(.001f));
            Assert.That(art.uvRect.xMax * art.texture.width,
                Is.EqualTo((art.texture.width + cropWidth) * .5f).Within(.001f));
            Assert.That(art.uvRect.yMin * art.texture.height, Is.EqualTo(guardPixels).Within(.001f));
            Assert.That(art.uvRect.yMax * art.texture.height,
                Is.EqualTo(art.texture.height - guardPixels).Within(.001f));
        }

        [Test]
        public void LegacyAtlasIllustration_KeepsItsMipSamplingInsideTheAuthoredCell()
        {
            CreatePreview(0);
            const string encounterId = "event_reef_shortcut";
            ShowCoreEncounter(new CoreEncounter(encounterId, "捷径", "航海事件", "海流引向一条捷径。", false, false, 0, 0));
            Assert.That(EncounterIllustrationCatalog.TryGetLocation(encounterId, out var location), Is.True);
            RawImage art = Find<RawImage>(host.transform, "Demo Event Art");
            Assert.That(art.texture, Is.SameAs(Resources.Load<Texture2D>(location.ResourcePath)));
            Assert.That(((Texture2D)art.texture).mipmapCount, Is.GreaterThan(1));
            float guardX = 8f / art.texture.width, guardY = 8f / art.texture.height;
            Assert.That(art.uvRect.xMin, Is.GreaterThanOrEqualTo(location.Uv.xMin + guardX - .00001f));
            Assert.That(art.uvRect.yMin, Is.GreaterThanOrEqualTo(location.Uv.yMin + guardY - .00001f));
            Assert.That(art.uvRect.xMax, Is.LessThanOrEqualTo(location.Uv.xMax - guardX + .00001f));
            Assert.That(art.uvRect.yMax, Is.LessThanOrEqualTo(location.Uv.yMax - guardY + .00001f));
        }

        [Test]
        public void SpecialShipIllustration_RetainsTheCompletePaintingInTheCompactHeader()
        {
            CreatePreview(0);
            ShowCoreEncounter(ThreeChoiceEncounter(false));
            Assert.That(SpecialWarshipArtCatalog.TryLoad(CoreRouteBossCatalog.All.First().Id,
                out Texture2D texture, out Rect uv), Is.True);
            RawImage art = Find<RawImage>(host.transform, "Demo Event Art");
            art.texture = texture;
            art.uvRect = uv;
            EncounterJournalLayout.Apply(Find<RectTransform>(host.transform, "Encounter Card"));
            Canvas.ForceUpdateCanvases();

            Assert.That(art.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(art.rectTransform.rect.width / art.rectTransform.rect.height,
                Is.EqualTo(texture.width / (float)texture.height).Within(.0001f));
            RectTransform mount = Find<RectTransform>(host.transform, "Encounter Illustration Mount");
            AssertContained(mount.rect, BoundsIn(mount, art.rectTransform), "Complete special ship painting");
        }

        [Test]
        public void Choices_ReflowScrollbarGutterAndKeepCompactFooterFixedAsTextChanges()
        {
            CreatePreview(0);
            ShowCoreEncounter(ThreeChoiceEncounter(false));
            RectTransform card = Find<RectTransform>(host.transform, "Encounter Card");
            RectTransform paper = Find<RectTransform>(card, "Encounter Paper Content");
            RectTransform secondary = Find<RectTransform>(card, "Encounter Secondary Actions");
            ScrollRect decisions = Find<ScrollRect>(card, "Encounter Decision Viewport");
            Scrollbar bar = Find<Scrollbar>(card, "Encounter Decision Scrollbar");
            Rect footer = BoundsIn(paper, secondary);
            Text detail = Find<Text>(Find<Button>(card, "Encounter Choice 0").transform, "Detail");
            string original = detail.text;

            Assert.That(decisions.vertical, Is.False, "Three ordinary choices should fit without scrolling.");
            Assert.That(decisions.viewport.rect.width, Is.EqualTo(paper.rect.width).Within(.01f));
            Assert.That(BoundsIn(paper, decisions.viewport).xMin, Is.EqualTo(footer.xMin).Within(.01f));

            detail.text = string.Concat(Enumerable.Repeat("整理完整的航海记录与物资清单。\n", 18));
            EncounterJournalLayout.Apply(card);
            Canvas.ForceUpdateCanvases();
            Assert.That(decisions.vertical, Is.True);
            Assert.That(bar.gameObject.activeSelf, Is.True);
            Assert.That(decisions.viewport.rect.width, Is.LessThan(paper.rect.width));
            Assert.That(BoundsIn(paper, decisions.viewport).xMax,
                Is.LessThan(BoundsIn(paper, bar.GetComponent<RectTransform>()).xMin));
            decisions.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            RectTransform last = Find<Button>(card, "Encounter Choice 2").GetComponent<RectTransform>();
            Assert.That(BoundsIn(decisions.viewport, last).yMin,
                Is.EqualTo(decisions.viewport.rect.yMin).Within(.5f), "The final choice remains reachable.");
            Assert.That(BoundsIn(paper, secondary), Is.EqualTo(footer));

            detail.text = original;
            EncounterJournalLayout.Apply(card);
            Canvas.ForceUpdateCanvases();
            Assert.That(decisions.vertical, Is.False);
            Assert.That(bar.gameObject.activeSelf, Is.False);
            Assert.That(decisions.viewport.rect.width, Is.EqualTo(paper.rect.width).Within(.01f));
            Assert.That(decisions.content.anchoredPosition.y, Is.EqualTo(0f).Within(.5f));
            Assert.That(BoundsIn(paper, secondary), Is.EqualTo(footer));
            AssertJournalLayout(3, true);

            // Also exercise actions with no description: an absent field must
            // not leave the old blank second line in a single-line button.
            Find<Text>(Find<Button>(card, "Skip Encounter").transform, "Detail").text = string.Empty;
            EncounterJournalLayout.Apply(card);
            Canvas.ForceUpdateCanvases();
            Assert.That(secondary.rect.height, Is.LessThan(footer.height - 10f),
                "Removing the description reclaims its line without shrinking the action label.");
            AssertJournalLayout(3, true);
        }

        [Test]
        public void EveryRoute_UsesMarketPaperAndNonBlockingIllustrations()
        {
            CreatePreview(0);
            for (int routeIndex = 0; routeIndex < 3; routeIndex++)
            {
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Encounter,
                    CoreGameplayPreviewPort.GrayGull, routeIndex);
                ShowCoreEncounter(ThreeChoiceEncounter(false));
                Image parchment = Find<Image>(host.transform, "Encounter Parchment");
                Assert.That(FindOptional<Transform>(host.transform, "Encounter Themed Frame"), Is.Null,
                    "Embedded encounters must leave the parchment visible without the decorative route frame.");
                Assert.That(parchment.sprite, Is.Not.Null, "Route " + routeIndex);
                Assert.That(parchment.sprite, Is.SameAs(MarketShopSkin.Paper), "Route " + routeIndex);
                Assert.That(parchment.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(Find<RawImage>(host.transform, "Demo Event Art").texture, Is.Not.Null,
                    "Removing the border must preserve the encounter illustration.");
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
            Assert.That(Find<Text>(choice.transform, "Requirement").text,
                Does.Contain(controller.Session.ChoiceUnavailableReason(0)), restriction);
            if (requiredCrew.Length > 0)
            {
                string[] professions = { "navigator", "carpenter", "cook", "doctor", "gunner", "diver" };
                string[] names = { "航海士", "木匠", "厨师", "船医", "炮手", "潜水员" };
                int profession = System.Array.IndexOf(professions, requiredCrew);
                string expectedName = profession >= 0 ? names[profession] : "指定职业船员";
                string expectedReason = "缺少所需船员：" + expectedName + "。";
                Assert.That(controller.Session.ChoiceUnavailableReason(0), Is.EqualTo(expectedReason));
                string requirement = Find<Text>(choice.transform, "Requirement").text;
                Assert.That(requirement, Does.Contain(expectedReason), "UI must show the core rule's localized explanation.");
                Assert.That(requirement, Does.Not.Contain(requiredCrew), "Internal profession IDs are not player-facing copy.");
                Assert.That(Find<Text>(choice.transform, "Hours").text, Does.Not.Contain(expectedReason));
                Assert.That(choice.GetComponentsInChildren<Text>().Single(text => text.name == "Detail").text,
                    Is.EqualTo("满足条件后可以执行"));
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
            if (!tutorial)
            {
                AssertJournalLayout(choiceCount, optional);
                return;
            }
            string prefix = tutorial ? "Tutorial Encounter" : "Encounter";
            string choicePrefix = tutorial ? "Tutorial Choice " : "Encounter Choice ";
            string skipName = tutorial ? "Tutorial Skip Encounter" : "Skip Encounter";
            RectTransform card = Find<RectTransform>(host.transform, prefix + " Card");
            RectTransform paper = Find<RectTransform>(card, prefix + " Paper Content");
            Image surface = Find<Image>(card, prefix + " Parchment");
            Assert.That(surface.sprite, Is.SameAs(MarketShopSkin.Paper));
            Assert.That(surface.type, Is.EqualTo(Image.Type.Sliced));
            RectTransform choices = Find<RectTransform>(card, tutorial ? "Tutorial Choices" : "Encounter Choices");
            LayoutRebuilder.ForceRebuildLayoutImmediate(choices);
            Canvas.ForceUpdateCanvases();

            Text title = Find<Text>(card, prefix + " Title");
            Text description = Find<Text>(card, tutorial ? "Tutorial Encounter Body" : "Encounter Description");
            Rect titleBounds = BoundsIn(paper, title.rectTransform);
            Rect descriptionBounds = BoundsIn(paper, description.rectTransform);
            AssertContained(paper.rect, titleBounds, title.name);
            AssertContained(paper.rect, descriptionBounds, description.name);
            AssertClearGap(titleBounds, descriptionBounds, "Title and narrative");
            AssertTextFits(title);
            AssertTextFits(description);
            Text badge = Find<Text>(card, prefix + " Badge");
            Text footer = Find<Text>(card, prefix + " Decision Footer");
            Rect badgeBounds = BoundsIn(paper, badge.rectTransform);
            Rect footerBounds = BoundsIn(paper, footer.rectTransform);
            if (tutorial)
            {
                // Keep the tutorial's existing reading and footer geometry.
                AssertContained(Rect.MinMaxRect(160f, 122f, 700f, 153f), badgeBounds, "Badge / hanging jewels");
                AssertContained(Rect.MinMaxRect(180f, -316f, 650f, -280f), footerBounds, "Footer / lower medallion");
            }
            else
            {
                AssertContained(paper.rect, badgeBounds, badge.name);
                AssertContained(paper.rect, footerBounds, footer.name);
                RectTransform art = Find<RectTransform>(card, "Encounter Illustration Mount");
                Rect artBounds = BoundsIn(paper, art);
                AssertContained(paper.rect, artBounds, art.name);
                AssertContained(art.rect, BoundsIn(art, Find<RawImage>(art, "Demo Event Art").rectTransform), "Encounter illustration");
                AssertClearGap(artBounds, titleBounds, "Illustration and title");
                AssertClearGap(artBounds, descriptionBounds, "Illustration and narrative");
                AssertClearGap(artBounds, badgeBounds, "Illustration and badge");
                AssertClearGap(descriptionBounds, badgeBounds, "Narrative and badge");
                AssertClearGap(badgeBounds, footerBounds, "Badge and footer");
                AssertTextFits(badge);
                AssertTextFits(footer);
            }
            AssertClearGap(badgeBounds, titleBounds, "Badge and title");

            Button[] buttons = choices.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
                Assert.That(button.GetComponent<Image>().sprite.name, Is.EqualTo("Market Light Control"));
            Assert.That(buttons.Length, Is.EqualTo(choiceCount + (optional ? 1 : 0)));
            Assert.That(FindOptional<Button>(card, skipName) != null, Is.EqualTo(optional));
            ScrollRect decisionScroll = tutorial ? null : Find<ScrollRect>(card, "Encounter Decision Viewport");
            if (decisionScroll != null)
            {
                Assert.That(decisionScroll.content, Is.SameAs(choices));
                Assert.That(choices.parent, Is.SameAs(decisionScroll.viewport));
                Assert.That(decisionScroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Assert.That(decisionScroll.horizontal, Is.False);
                Assert.That(decisionScroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(decisionScroll.vertical, Is.EqualTo(choices.rect.height > decisionScroll.viewport.rect.height + 0.1f));
                AssertContained(paper.rect, BoundsIn(paper, decisionScroll.viewport), "Visible decision area");
            }
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
                if (tutorial) AssertContained(paper.rect, bounds, button.name);
                else AssertContained(choices.rect, BoundsIn(choices, button.GetComponent<RectTransform>()), button.name);
                AssertClearGap(titleBounds, bounds, button.name + " / title");
                AssertClearGap(descriptionBounds, bounds, button.name + " / narrative");
                if (!tutorial)
                {
                    AssertClearGap(badgeBounds, bounds, button.name + " / badge");
                    AssertClearGap(footerBounds, bounds, button.name + " / footer");
                    AssertClearGap(BoundsIn(paper, Find<RectTransform>(card, "Encounter Illustration Mount")),
                        bounds, button.name + " / illustration");
                }
                Assert.That(button.targetGraphic, Is.Not.Null, button.name);
                Assert.That(button.targetGraphic.raycastTarget, Is.True, button.name);
                foreach (Rect other in actionBounds) AssertClearGap(other, bounds, "Action buttons");
                actionBounds.Add(bounds);
            }
            if (decisionScroll != null)
            {
                decisionScroll.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                AssertContained(decisionScroll.viewport.rect,
                    BoundsIn(decisionScroll.viewport, buttons.First().GetComponent<RectTransform>()), "First action is visible at the top");
                decisionScroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                AssertContained(decisionScroll.viewport.rect,
                    BoundsIn(decisionScroll.viewport, buttons.Last().GetComponent<RectTransform>()), "Last action is reachable by scrolling");
            }
            AssertDecorationsDoNotInterceptInput(card);
        }

        private void AssertJournalLayout(int choiceCount, bool optional)
        {
            RectTransform card = Find<RectTransform>(host.transform, "Encounter Card");
            RectTransform paper = Find<RectTransform>(card, "Encounter Paper Content");
            RectTransform choices = Find<RectTransform>(card, "Encounter Choices");
            RectTransform secondary = Find<RectTransform>(card, "Encounter Secondary Actions");
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Encounter Journal Heading", "Encounter Badge", "Encounter Location",
                "Encounter Decision Heading", "Encounter Decision Footer", "Encounter Footer Note" })
                Assert.That(FindOptional<Transform>(card, name), Is.Null, "Removed explanation " + name);
            string[] regions = { "Encounter Illustration Mount", "Encounter Title",
                "Encounter Description Viewport", "Encounter Decision Viewport" };
            Rect[] bounds = regions.Select(name => BoundsIn(paper, Find<RectTransform>(card, name))).ToArray();
            for (int index = 0; index < regions.Length; index++)
            {
                AssertContained(paper.rect, bounds[index], regions[index]);
                for (int other = index + 1; other < regions.Length; other++)
                    AssertClearGap(bounds[index], bounds[other], regions[index] + " / " + regions[other]);
            }
            AssertTextFits(Find<Text>(card, "Encounter Title"));
            RectTransform art = Find<RectTransform>(card, "Encounter Illustration Mount");
            AssertContained(art.rect, BoundsIn(art, Find<RawImage>(art, "Demo Event Art").rectTransform), "Encounter illustration");

            ScrollRect narrative = Find<ScrollRect>(card, "Encounter Description Viewport");
            Text description = Find<Text>(card, "Encounter Description");
            Assert.That(narrative.content.name, Is.EqualTo("Encounter Description Content"));
            Assert.That(description.transform.parent, Is.SameAs(narrative.content));
            Assert.That(narrative.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(narrative.horizontal, Is.False);
            AssertContained(narrative.content.rect, BoundsIn(narrative.content, description.rectTransform), description.name);
            AssertTextFits(description);

            ScrollRect decisions = Find<ScrollRect>(card, "Encounter Decision Viewport");
            Assert.That(decisions.content, Is.SameAs(choices));
            Assert.That(choices.parent, Is.SameAs(decisions.viewport));
            Assert.That(decisions.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(decisions.horizontal, Is.False);
            Assert.That(decisions.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(decisions.vertical, Is.EqualTo(choices.rect.height > decisions.viewport.rect.height + .5f));
            Scrollbar scrollbar = Find<Scrollbar>(card, "Encounter Decision Scrollbar");
            Assert.That(decisions.verticalScrollbar, Is.SameAs(scrollbar));
            Assert.That(scrollbar.gameObject.activeSelf, Is.EqualTo(decisions.vertical));
            Assert.That(secondary.parent, Is.SameAs(paper), "Fixed actions must not scroll with the choices.");
            Assert.That(secondary.IsChildOf(choices), Is.False);
            if (secondary.rect.height > 0f)
            {
                AssertContained(paper.rect, BoundsIn(paper, secondary), secondary.name);
                AssertClearGap(BoundsIn(paper, decisions.viewport), BoundsIn(paper, secondary), "Choices and fixed actions");
            }

            Button[] buttons = choices.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(choiceCount));
            Button skip = FindOptional<Button>(card, "Skip Encounter");
            Assert.That(skip != null, Is.EqualTo(optional));
            if (skip != null) Assert.That(skip.transform.parent, Is.SameAs(secondary));
            var actionBounds = new List<Rect>();
            foreach (Button button in buttons.Concat(secondary.GetComponentsInChildren<Button>(true)))
            {
                RectTransform row = button.GetComponent<RectTransform>();
                AssertContained(((RectTransform)row.parent).rect, BoundsIn(row.parent, row), button.name);
                var fields = new List<Rect>();
                foreach (string field in new[] { "Label", "Detail", "Hours", "Requirement" })
                {
                    Text text = FindOptional<Text>(row, field);
                    if (text == null || string.IsNullOrEmpty(text.text)) continue;
                    AssertTextFits(text);
                    Rect fieldBounds = BoundsIn(row, text.rectTransform);
                    AssertContained(row.rect, fieldBounds, button.name + " / " + field);
                    foreach (Rect previous in fields) AssertClearGap(previous, fieldBounds, button.name + " text fields");
                    fields.Add(fieldBounds);
                }
                Assert.That(button.targetGraphic, Is.Not.Null, button.name);
                Assert.That(button.targetGraphic.raycastTarget, Is.True, button.name);
                Image face = button.GetComponent<Image>();
                Assert.That(face.sprite, Is.Not.Null, button.name);
                Assert.That(face.sprite.name, Is.EqualTo("Market Light Control"), button.name);
                Assert.That(face.type, Is.EqualTo(Image.Type.Sliced), button.name);
                if (button.transform.parent == choices)
                {
                    Rect rowBounds = BoundsIn(choices, row);
                    foreach (Rect previous in actionBounds) AssertClearGap(previous, rowBounds, "Choices");
                    actionBounds.Add(rowBounds);
                }
            }
            Rect fixedBounds = BoundsIn(paper, secondary);
            decisions.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            Assert.That(BoundsIn(decisions.viewport, buttons.First().GetComponent<RectTransform>()).yMax,
                Is.EqualTo(decisions.viewport.rect.yMax).Within(.5f), "The first choice starts at the top.");
            decisions.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Rect last = BoundsIn(decisions.viewport, buttons.Last().GetComponent<RectTransform>());
            Assert.That(last.yMin, Is.GreaterThanOrEqualTo(decisions.viewport.rect.yMin - .5f), "The last choice is reachable.");
            Assert.That(BoundsIn(paper, secondary), Is.EqualTo(fixedBounds), "Scrolling must leave secondary actions fixed.");
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
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f),
                text.name + " must display all lines without vertical clipping.");
            if (text.horizontalOverflow == HorizontalWrapMode.Overflow)
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 0.5f),
                    text.name + " must fit within its horizontal field.");
        }

        private static void AssertClearGap(Rect first, Rect second, string context)
        {
            float gap = Mathf.Max(first.xMin - second.xMax, second.xMin - first.xMax,
                first.yMin - second.yMax, second.yMin - first.yMax);
            Assert.That(gap, Is.GreaterThan(0.1f), context + " should have visible separation.");
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
