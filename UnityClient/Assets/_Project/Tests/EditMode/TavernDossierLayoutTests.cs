using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class TavernDossierLayoutTests
    {
        private static readonly string[] PageKeys = { "Background", "Story", "Skills", "SpecialCards" };
        private static readonly string[][] PageFields =
        {
            new[] { "Tavern Crew Biography" },
            new[] { "Tavern Crew Story Heading", "Tavern Crew Story" },
            new[] { "Tavern Crew Skill Heading", "Tavern Crew Skill" },
            new[] { "Tavern Special Card Name" },
        };

        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(800, 1000)]
        [TestCase(1000, 800)]
        [TestCase(512, 1024)]
        public void BackgroundPortraitPreservesItsColorsAndAspectWhileCoveringWindow(int width, int height)
        {
            Texture2D portraitTexture = new Texture2D(width, height);
            Texture2D frameTexture = new Texture2D(2, 2);
            try
            {
                host = new GameObject("Portrait layout test");
                controller = host.AddComponent<CoreGameplayDemoController>();
                System.Func<string, Texture2D> loader = path => path.EndsWith("_portrait_v2") ? portraitTexture : frameTexture;
                PortArtCatalog art = (PortArtCatalog)typeof(PortArtCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(System.Func<string, Texture2D>) }, null).Invoke(new object[] { loader });
                typeof(CoreGameplayDemoController).GetField("portArt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, art);
                RectTransform detail = new GameObject("Detail", typeof(RectTransform)).GetComponent<RectTransform>();
                detail.SetParent(host.transform, false);
                bool built = (bool)typeof(CoreGameplayDemoController).GetMethod("TryBuildTavernDossierPortrait",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { detail, "test_crew" });
                Assert.That(built, Is.True);
                RawImage portrait = Find<RawImage>(detail, "Tavern Dossier Background Portrait");
                RectTransform viewport = Find<RectTransform>(detail, "Tavern Dossier Portrait Viewport");
                Assert.That(portrait.texture, Is.SameAs(portraitTexture));
                Assert.That(portrait.GetComponent<CrewArtChromaKey>(), Is.Null);
                Assert.That(portrait.GetComponent<TavernPortraitPaperBlend>(), Is.Null);
                Assert.That(portrait.raycastTarget, Is.False);
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
                Rect bounds = BoundsIn(viewport, portrait.rectTransform);
                AssertContained(bounds, viewport.rect, "The portrait fills every edge of the window.");
                Assert.That(bounds.yMax, Is.EqualTo(viewport.rect.yMax).Within(.1f));
                Assert.That(portrait.rectTransform.rect.width / portrait.rectTransform.rect.height,
                    Is.EqualTo((float)width / height).Within(.001f));
                RawImage frame = Find<RawImage>(detail, "Tavern Dossier Parchment");
                Assert.That(frame.texture, Is.SameAs(frameTexture));
                Assert.That(frame.GetComponent<CrewArtChromaKey>(), Is.Not.Null);
                Assert.That(frame.transform.GetSiblingIndex(), Is.GreaterThan(viewport.GetSiblingIndex()));
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                Object.DestroyImmediate(portraitTexture);
                Object.DestroyImmediate(frameTexture);
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void EveryDossierPageKeepsControlsAndLargeTextInsideMeasuredPaper(CoreGameplayPreviewPort port)
        {
            Transform root = CreatePreview(port);
            RectTransform detail = Find<RectTransform>(root, "Tavern Crew Detail");
            Find<RawImage>(root, "Tavern Dossier Parchment");

            // The approved integrated painting retains the name location while
            // moving the writing and footer away from the right-hand wax seal.
            Rect portrait = Rect.MinMaxRect(-780f, -235f, -393f, 153f);
            Rect profilePaper = Rect.MinMaxRect(-253f, -216f, 765f, 237f);
            Rect footerPaper = Rect.MinMaxRect(-288f, -264f, 600f, -220f);
            RectTransform header = Find<RectTransform>(root, "Port Facility Header");
            Button back = Find<Button>(root, "Return To Port Hub");
            Rect recruit = BoundsIn(detail, Find<Button>(root, "Recruit Tavern Crew 0").GetComponent<RectTransform>());

            Assert.That(back.transform.parent, Is.SameAs(header), "The dossier uses the existing header return control.");
            AssertContained(header.rect, BoundsIn(header, back.GetComponent<RectTransform>()), "Return button / facility header");
            Assert.That(BoundsIn(detail, back.GetComponent<RectTransform>()).yMin, Is.GreaterThan(detail.rect.yMax),
                "The return control stays above the dossier body.");
            Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
            Assert.That(root.GetComponentsInChildren<Button>(true).Count(button =>
                button.GetComponentInChildren<Text>(true)?.text.Contains("返回酒馆") == true), Is.EqualTo(1));
            Assert.That(detail.GetComponentsInChildren<Button>(true).Any(button =>
                button.name == "Back To Tavern Crew Lineup" ||
                button.GetComponentInChildren<Text>(true)?.text.Contains("返回酒馆") == true), Is.False,
                "The dossier body must not contain a second return button.");
            Assert.That(detail.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name == "Tavern Crew Detail Full Body" || item.name == "Tavern Crew Detail Portrait Frame"), Is.False,
                "The dossier must not restore the old full-body cutout or portrait frame.");
            Rect nameBounds = BoundsIn(detail, Find<Text>(root, "Tavern Crew Detail Name").rectTransform);
            Rect roleBounds = BoundsIn(detail, Find<Text>(root, "Tavern Crew Detail Role").rectTransform);
            Assert.That(nameBounds.xMin, Is.EqualTo(-165f), "The name retains its original visible left edge.");
            Assert.That(nameBounds.center.y, Is.EqualTo(210f), "The name retains its original baseline.");
            Assert.That(roleBounds.center.y, Is.EqualTo(nameBounds.center.y), "The red role summary shares the name row.");
            Assert.That(roleBounds.xMin - nameBounds.xMax, Is.GreaterThanOrEqualTo(16.9f));
            Assert.That(Find<Button>(root, "DossierPageBackground").GetComponent<RectTransform>().anchoredPosition.y,
                Is.EqualTo(160f), "Tabs rise one text line after the heading becomes a single row.");
            AssertContained(footerPaper, recruit, "Recruit button / bottom-right wax seal");
            Assert.That(recruit.height, Is.GreaterThanOrEqualTo(43.9f));
            Assert.That(Find<Button>(root, "Recruit Tavern Crew 0").GetComponentInChildren<Text>().fontSize, Is.EqualTo(22));
            Assert.That(back.GetComponentInChildren<Text>().fontSize, Is.EqualTo(20));

            for (int page = 0; page < PageKeys.Length; page++)
            {
                Find<Button>(root, "DossierPage" + PageKeys[page]).onClick.Invoke();
                AssertPage(root, page);
                string[] fields = new[] { "Tavern Crew Detail Name", "Tavern Crew Detail Role", "DossierPageBackground" }
                    .Concat(page == 3 ? new string[0] : PageFields[page]).ToArray();
                Rect previous = default;
                for (int field = 0; field < fields.Length; field++)
                {
                    RectTransform fieldRect = Find<RectTransform>(root, fields[field]);
                    ScrollRect textScroll = fieldRect.GetComponentInParent<ScrollRect>();
                    Rect current = BoundsIn(detail, textScroll != null ? textScroll.viewport : fieldRect);
                    AssertContained(profilePaper, current, fields[field]);
                    Assert.That(current.xMin - portrait.xMax, Is.GreaterThanOrEqualTo(130f), fields[field]);
                    if (field > 0 && fields[field] != "Tavern Crew Detail Role")
                        Assert.That(previous.yMin - current.yMax, Is.GreaterThanOrEqualTo(0.9f), fields[field]);
                    previous = current;
                }
                Assert.That(previous.yMin - recruit.yMax, Is.GreaterThanOrEqualTo(4.9f));
                if (page == 3)
                {
                    Rect face = BoundsIn(detail, Find<RectTransform>(root, "Tavern Special Card Face")
                        .GetComponentInChildren<CardSchoolFrameGraphic>().rectTransform);
                    Rect stage = BoundsIn(detail, Find<Text>(root, "Tavern Special Card Stage").rectTransform);
                    Rect details = BoundsIn(detail, Find<RectTransform>(root, "Tavern Special Card Details Viewport"));
                    AssertContained(profilePaper, face, "Complete special card face / dossier bay");
                    Assert.That(face.xMin - portrait.xMax, Is.GreaterThanOrEqualTo(130f));
                    Assert.That(face.yMin - recruit.yMax, Is.GreaterThanOrEqualTo(3.9f));
                    foreach (Rect region in new[] { stage, details })
                    {
                        AssertContained(profilePaper, region, "Special card details / right column");
                        Assert.That(region.xMin, Is.GreaterThan(face.xMax), "The details column stays beside the card.");
                        Assert.That(region.Overlaps(face), Is.False);
                    }
                    Assert.That(stage.yMin - details.yMax, Is.GreaterThanOrEqualTo(.9f));
                    Assert.That(details.yMin - recruit.yMax, Is.GreaterThanOrEqualTo(5.9f));
                }
                foreach (Button tab in root.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("DossierPage")))
                    AssertContained(profilePaper, BoundsIn(detail, tab.GetComponent<RectTransform>()), tab.name);
                Assert.That(back.gameObject.activeInHierarchy, Is.True);
                Assert.That(back.IsInteractable(), Is.True);
                Assert.That(back.GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
                Assert.That(Find<Button>(root, "Recruit Tavern Crew 0").gameObject.activeInHierarchy, Is.True);
            }

            foreach (Graphic graphic in detail.GetComponentsInChildren<Graphic>(true))
            {
                ScrollRect scroll = graphic.GetComponent<ScrollRect>();
                Scrollbar scrollbar = graphic.GetComponentInParent<Scrollbar>(true);
                bool scrollHandle = scrollbar != null && scrollbar.targetGraphic == graphic;
                bool inputSurface = graphic.GetComponent<Selectable>() != null || scroll != null || scrollHandle;
                Assert.That(graphic.raycastTarget, Is.EqualTo(inputSurface), graphic.name + " must only receive intended control input.");
                if (!graphic.raycastTarget || graphic.GetComponent<Button>() != null) continue;
                // Long effects now scroll. Their input surfaces are necessary;
                // check real overlap rather than forbidding every non-button hit.
                Rect hitBounds = BoundsIn(detail, graphic.rectTransform);
                AssertContained(profilePaper, hitBounds, graphic.name);
                foreach (Button button in detail.GetComponentsInChildren<Button>(true))
                    Assert.That(hitBounds.Overlaps(BoundsIn(detail, button.GetComponent<RectTransform>())), Is.False,
                        graphic.name + " must not obstruct " + button.name);
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void AllThirtySixIdentitiesRetainCompleteTextAtReadableSizesAcrossFourPages(CoreGameplayPreviewPort port)
        {
            Transform root = CreatePreview(port);
            // Offer previews isolate the displayed quote from the developer's recruited roster.
            controller.ConfigureEditorFlowState(GameplayFlowPreviewState.RecruitmentOffer);
            root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            System.Type catalog = NarrativeCatalog();
            int checkedCrew = 0;
            foreach (object crew in AllCrew())
            {
                if (Read(crew, "PortName") != controller.Session.CurrentPort) continue;
                string crewId = Read(crew, "Id");
                OpenCrew(int.Parse(Read(crew, "ProfessionIndex")));
                RawImage parchment = Find<RawImage>(root, "Tavern Dossier Parchment");
                Assert.That(parchment.texture, Is.Not.Null, crewId);
                Assert.That(parchment.texture.name, Is.EqualTo("tavern_crew_dossier_frame_4k_layers_v1"), crewId);
                RawImage portrait = Find<RawImage>(root, "Tavern Dossier Background Portrait");
                Assert.That(portrait.texture.name, Is.EqualTo(crewId + "_portrait_v2"),
                    "Every crew member must resolve to their own background portrait.");
                Assert.That(portrait.GetComponent<CrewArtChromaKey>(), Is.Null, "Authored blue colors must survive.");
                RectTransform viewport = Find<RectTransform>(root, "Tavern Dossier Portrait Viewport");
                Rect portraitBounds = BoundsIn(viewport, portrait.rectTransform);
                AssertContained(portraitBounds, viewport.rect, crewId + " / portrait covers the window");
                Assert.That(portraitBounds.yMax, Is.EqualTo(viewport.rect.yMax).Within(.1f), "Keep the head aligned to the top.");
                Assert.That(portrait.rectTransform.rect.width / portrait.rectTransform.rect.height,
                    Is.EqualTo((float)portrait.texture.width / portrait.texture.height).Within(.001f), "Do not stretch the face.");
                Assert.That(parchment.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), crewId);
                Assert.That(parchment.GetComponent<Mask>(), Is.Null, crewId);
                Assert.That(Find<RectTransform>(root, "Tavern Crew Detail").GetComponentsInChildren<TavernPortraitPaperBlend>(true), Is.Empty, crewId);
                object story = catalog.GetMethod("StoryFor").Invoke(null, new[] { crew });
                AssertText(root, "Tavern Crew Detail Name", Read(crew, "DisplayName"), 36, crewId);
                AssertText(root, "Tavern Crew Detail Role", Read(crew, "PortName") + "　·　" + Read(crew, "RoleSummary"), 22, crewId);
                foreach (string heading in new[] { "Tavern Crew Detail Name", "Tavern Crew Detail Role" })
                {
                    Text label = Find<Text>(root, heading);
                    Assert.That(label.preferredWidth, Is.LessThanOrEqualTo(label.rectTransform.rect.width),
                        crewId + " / the longest names and roles must each fit on one header line.");
                }
                Assert.That(Find<RectTransform>(root, "Tavern Dossier Background Page").rect.height, Is.EqualTo(326f));
                Assert.That(Find<RectTransform>(root, "Tavern Dossier Story Page").rect.height, Is.EqualTo(326f));
                Assert.That(Find<RectTransform>(root, "Tavern Dossier SpecialCards Page").rect.height, Is.EqualTo(368f));
                Assert.That(Find<Button>(root, "Recruit Tavern Crew " + Read(crew, "ProfessionIndex"))
                    .GetComponentInChildren<Text>().text,
                    Is.EqualTo("签署契约 · " + Read(crew, "ContractCost") + " 金币"), crewId + " / cost stays with confirmation");
                AssertText(root, "Tavern Crew Biography", Read(crew, "Biography"), 26, crewId);
                AssertBackgroundContainsOnlyBiography(root, crewId);
                AssertText(root, "Tavern Crew Story Heading", Read(story, "ChapterLabel"), 25, crewId);
                AssertText(root, "Tavern Crew Story", Read(story, "Body"), 26, crewId);
                AssertText(root, "Tavern Crew Skill Heading", "专长 · " + Read(crew, "Specialty") + "　｜　职业能力", 28, crewId);
                AssertText(root, "Tavern Crew Skill", Read(crew, "SkillDescription"), 26, crewId);
                for (int page = 0; page < PageKeys.Length; page++)
                {
                    Find<Button>(root, "DossierPage" + PageKeys[page]).onClick.Invoke();
                    AssertPage(root, page);
                    foreach (Text label in Find<RectTransform>(root, "Tavern Crew Detail").GetComponentsInChildren<Text>())
                        AssertTextFits(label, crewId);
                }
                checkedCrew++;
            }
            Assert.That(checkedCrew, Is.EqualTo(6));
        }

        [Test]
        public void LongAccountsRemainCompleteAndScrollableWithoutShrinkingTheAuthoredType()
        {
            Transform root = CreatePreview(CoreGameplayPreviewPort.BrokenAnchor);
            RectTransform page = Find<RectTransform>(root, "Tavern Dossier Background Page");
            string value = string.Concat(Enumerable.Repeat("船员沿着旧航路航行，记下每一处潮汐与靠岸的故事。\n\n", 24));
            Text label = (Text)typeof(CoreGameplayDemoController)
                .GetMethod("CreateTavernReadingText", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { "Long Account Audit", page, new Vector2(20f, 8f),
                    new Vector2(820f, 264f), 27, 1.25f, Color.black, value });
            Canvas.ForceUpdateCanvases();
            Assert.That(WithoutLineBreaks(label.text), Is.EqualTo(WithoutLineBreaks(value)));
            Assert.That(label.fontSize, Is.EqualTo(27));
            Assert.That(label.resizeTextForBestFit, Is.False);
            ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.viewport.rect.size, Is.EqualTo(new Vector2(820f, 264f)));
            AssertTextFits(label, "long account");
            AssertContained(scroll.content.rect, BoundsIn(scroll.content, label.rectTransform), "Complete account / scroll content");
            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Assert.That(BoundsIn(scroll.viewport, label.rectTransform).yMin,
                Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin), "The last line must be reachable at the end of the scroll.");
        }

        [Test]
        public void MissingPersonalCardKeepsItsExplanationAndPageControls()
        {
            Transform root = CreatePreview(CoreGameplayPreviewPort.BrokenAnchor);
            RectTransform page = Find<RectTransform>(root, "Tavern Dossier SpecialCards Page");
            foreach (Transform child in page.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            const string explanation = "这位船员不提供战斗专属卡牌。职业专长仍然生效。";
            typeof(CoreGameplayDemoController).GetMethod("BuildTavernDossierSpecialCard", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { page, null, explanation, Color.black, Color.black });
            Find<Button>(root, "DossierPageSpecialCards").onClick.Invoke();
            AssertPage(root, 3);
            Assert.That(WithoutLineBreaks(Find<Text>(root, "Tavern Special Card Empty").text), Is.EqualTo(explanation));
            foreach (Text label in page.GetComponentsInChildren<Text>()) AssertTextFits(label, "no personal card");
            Assert.That(page.GetComponentsInChildren<RawImage>(), Is.Empty);
            Assert.That(Find<Button>(root, "Recruit Tavern Crew 0").gameObject.activeInHierarchy, Is.True);
        }

        [Test]
        public void PageTabsOnlyChangeVisiblePageWithoutSpendingRecruitingOrSavingCrew()
        {
            Transform root = CreatePreview(CoreGameplayPreviewPort.BrokenAnchor);
            var session = controller.Session;
            decimal gold = session.ProfileGold;
            string savedCrew = CrewPreferenceSnapshot();
            RectTransform originalDetail = Find<RectTransform>(root, "Tavern Crew Detail");
            AssertPage(root, 0);
            Find<Button>(root, "DossierPageBackground").onClick.Invoke();
            AssertPage(root, 0);
            Find<Button>(root, "DossierPageStory").onClick.Invoke();
            AssertPage(root, 1);
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            AssertPage(root, 2);
            Find<Button>(root, "DossierPageSpecialCards").onClick.Invoke();
            AssertPage(root, 3);
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            AssertPage(root, 2);
            Find<Button>(root, "DossierPageBackground").onClick.Invoke();
            AssertPage(root, 0);
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            AssertPage(root, 2);
            Assert.That(Find<RectTransform>(root, "Tavern Crew Detail"), Is.SameAs(originalDetail), "Turning a page must not rebuild the game UI.");
            Assert.That(controller.Session, Is.SameAs(session));
            Assert.That(session.ProfileGold, Is.EqualTo(gold));
            Assert.That(CrewPreferenceSnapshot(), Is.EqualTo(savedCrew), "Page navigation must not recruit, choose a representative, or create save keys.");
        }

        [Test]
        public void CurrentPageSurvivesRenderAndResetsForAnotherCrewLineupOrPort()
        {
            Transform root = CreatePreview(CoreGameplayPreviewPort.GrayGull);
            Find<Button>(root, "DossierPageStory").onClick.Invoke();
            Render();
            AssertPage(root, 1);
            OpenCrew(1);
            AssertPage(root, 0);
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            Assert.That(Find<Button>(root, "Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回酒馆"));
            Find<Button>(root, "Return To Port Hub").onClick.Invoke();
            Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name == "Tavern Crew Stage"), Is.True);
            Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name == "Tavern Crew Detail"), Is.False);
            Assert.That(Find<Button>(root, "Return To Port Hub").GetComponentInChildren<Text>().text, Is.EqualTo("返回港口"));
            Find<Button>(root, "Return To Port Hub").onClick.Invoke();
            Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name == "Port Hub"), Is.True);
            Find<Button>(root, "Port Hotspot Tavern").onClick.Invoke();
            Find<Button>(root, "Tavern Crew 1").onClick.Invoke();
            AssertPage(root, 0);
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.WhiteShell, 0);
            root = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
            AssertPage(root, 0);
            Assert.That(Find<Text>(root, "Tavern Crew Detail Role").text, Does.StartWith("白贝港"));
            Find<Button>(root, "DossierPageSkills").onClick.Invoke();
            Find<Button>(root, "Next Port Facility").onClick.Invoke();
            Find<Button>(root, "Previous Port Facility").onClick.Invoke();
            Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name == "Tavern Crew Stage"), Is.True);
        }

        private Transform CreatePreview(CoreGameplayPreviewPort port)
        {
            host = new GameObject("Tavern Dossier Paper Layout Test");
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, port, 0);
            return host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName);
        }

        private void OpenCrew(int index)
        {
            typeof(CoreGameplayDemoController).GetField("selectedTavernCandidate", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, index);
            Render();
        }

        private void Render()
        {
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
        }

        private static void AssertPage(Transform root, int selectedPage)
        {
            for (int page = 0; page < PageKeys.Length; page++)
            {
                Assert.That(Find<RectTransform>(root, "Tavern Dossier " + PageKeys[page] + " Page").gameObject.activeSelf, Is.EqualTo(page == selectedPage), PageKeys[page]);
                foreach (string field in PageFields[page])
                    Assert.That(Find<Text>(root, field).gameObject.activeInHierarchy, Is.EqualTo(page == selectedPage), field);
            }
            // A missing card definition keeps an explanation instead of
            // offering acquisition or leaving an empty fourth tab.
            Transform[] specialCardFields = Find<RectTransform>(root, "Tavern Dossier SpecialCards Page")
                .GetComponentsInChildren<Transform>(true);
            Transform face = specialCardFields.SingleOrDefault(field => field.name == "Tavern Special Card Face");
            Transform empty = specialCardFields.SingleOrDefault(field => field.name == "Tavern Special Card Empty");
            Assert.That((face != null) != (empty != null), Is.True,
                "The special-card page must show either its card face or the no-card explanation.");
            Transform status = face != null ? face : empty;
            Assert.That(status.gameObject.activeInHierarchy, Is.EqualTo(selectedPage == 3), status.name);
            if (empty != null) Assert.That(empty.GetComponent<Text>().text, Does.Contain("职业专长"));
            foreach (string removed in new[] { "Tavern Special Card Growth", "Tavern Dossier Page Counter",
                "Previous Tavern Dossier Page", "Next Tavern Dossier Page" })
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name == removed), Is.False, removed);
        }

        private static void AssertText(Transform root, string name, string expected, int minimumSize, string crewId)
        {
            Text label = Find<Text>(root, name);
            string actual = WithoutLineBreaks(label.text);
            expected = WithoutLineBreaks(expected);
            Assert.That(actual, Is.EqualTo(expected), crewId + " / " + name);
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(minimumSize), crewId + " / " + name);
            Assert.That(label.resizeTextForBestFit, Is.False, name + " must not shrink to fit.");
        }

        private static void AssertBackgroundContainsOnlyBiography(Transform root, string crewId)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            Assert.That(descendants.Any(item => item.name == "Tavern Crew Appearance"), Is.False, crewId);
            Assert.That(descendants.Any(item => item.name == "Tavern Crew Profile Heading"), Is.False, crewId);
            Text[] background = Find<RectTransform>(root, "Tavern Dossier Background Page").GetComponentsInChildren<Text>(true);
            Assert.That(background.Length, Is.EqualTo(1), crewId + " / the background body must not repeat headings or appearance notes.");
            Assert.That(background[0].name, Is.EqualTo("Tavern Crew Biography"), crewId);
            Assert.That(Find<Button>(root, "DossierPageBackground").GetComponentInChildren<Text>().text, Is.EqualTo("人物背景"), crewId);
        }

        private static string WithoutLineBreaks(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty);

        private static void AssertTextFits(Text label, string crewId)
        {
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.5f), crewId + " / " + label.transform.parent.name + " / " + label.name);
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            label.cachedTextGenerator.Populate(label.text, settings);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            using (TextGenerator complete = new TextGenerator())
            {
                complete.Populate(label.text, settings);
                Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                    crewId + " / " + label.name + " must retain the final glyphs.");
            }
            ScrollRect scroll = label.GetComponentInParent<ScrollRect>();
            if (scroll == null) return;
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height),
                label.name + " must only scroll when its complete text exceeds the reading area.");
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
        }

        private static System.Type NarrativeCatalog()
        {
            // Read the loaded catalogue without broadening this test assembly's dependencies.
            return System.Type.GetType("ShatteredSail.Content.CrewNarrativeCatalog, ShatteredSail.Content", true);
        }

        private static IEnumerable AllCrew()
        {
            return (IEnumerable)NarrativeCatalog().GetProperty("AllCrew").GetValue(null);
        }

        private static string CrewPreferenceSnapshot()
        {
            StringBuilder snapshot = new StringBuilder();
            foreach (object crew in AllCrew())
            {
                string key = CoreGameplayDemoController.CrewVariantRecruitmentKey(Read(crew, "Id"));
                snapshot.Append(key).Append(':').Append(PlayerPrefs.HasKey(key)).Append(':').Append(PlayerPrefs.GetInt(key, -1)).Append(';');
            }
            for (int profession = 0; profession < 6; profession++)
            {
                string recruitedKey = CoreGameplayDemoController.CrewRecruitmentKey(profession);
                string activeKey = CoreGameplayDemoController.ActiveCrewVariantKey(profession);
                snapshot.Append(PlayerPrefs.HasKey(recruitedKey)).Append(':').Append(PlayerPrefs.GetInt(recruitedKey, -1)).Append(';');
                snapshot.Append(PlayerPrefs.HasKey(activeKey)).Append(':').Append(PlayerPrefs.GetString(activeKey, "<missing>")).Append(';');
            }
            return snapshot.ToString();
        }

        private static string Read(object value, string property)
        {
            return value.GetType().GetProperty(property).GetValue(value).ToString();
        }

        private static void AssertContained(Rect paper, Rect content, string context)
        {
            Assert.That(content.xMin, Is.GreaterThanOrEqualTo(paper.xMin - 0.1f), context);
            Assert.That(content.xMax, Is.LessThanOrEqualTo(paper.xMax + 0.1f), context);
            Assert.That(content.yMin, Is.GreaterThanOrEqualTo(paper.yMin - 0.1f), context);
            Assert.That(content.yMax, Is.LessThanOrEqualTo(paper.yMax + 0.1f), context);
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

        private static T Find<T>(Transform root, string name) where T : Component
        {
            T result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }
    }
}
