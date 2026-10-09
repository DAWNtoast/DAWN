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
            new[] { "Tavern Crew Skill Heading", "Tavern Crew Skill", "Tavern Crew Stats", "Tavern Crew Contract Note" },
            new[] { "Tavern Special Card Name", "Tavern Special Card Growth" },
        };

        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
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

            // Measured from the painted inner rule and wax seals in
            // tavern_crew_dossier_parchment_v1, not the rectangular PNG bounds.
            Rect navigationPaper = Rect.MinMaxRect(-775f, 301f, -545f, 351f);
            Rect portraitPaper = Rect.MinMaxRect(-780f, -235f, -393f, 153f);
            Rect profilePaper = Rect.MinMaxRect(-165f, -181f, 765f, 237f);
            Rect footerPaper = Rect.MinMaxRect(-200f, -237f, 688f, -181f);
            Rect back = BoundsIn(detail, Find<Button>(root, "Back To Tavern Crew Lineup").GetComponent<RectTransform>());
            Rect portrait = BoundsIn(detail, Find<RawImage>(root, "Tavern Crew Detail Full Body").rectTransform);
            Rect recruit = BoundsIn(detail, Find<Button>(root, "Recruit Tavern Crew 0").GetComponent<RectTransform>());

            AssertContained(navigationPaper, back, "Return button / top seal");
            AssertContained(portraitPaper, portrait, "Portrait / carved portrait bay");
            AssertContained(footerPaper, recruit, "Recruit button / bottom-right wax seal");
            Assert.That(back.yMin - portrait.yMax, Is.GreaterThanOrEqualTo(20f));
            Assert.That(recruit.height, Is.GreaterThanOrEqualTo(43.9f));
            Assert.That(Find<Button>(root, "Recruit Tavern Crew 0").GetComponentInChildren<Text>().fontSize, Is.EqualTo(22));
            Assert.That(Find<Button>(root, "Back To Tavern Crew Lineup").GetComponentInChildren<Text>().fontSize, Is.EqualTo(22));

            string[] footerNames = { "Previous Tavern Dossier Page", "Tavern Dossier Page Counter", "Next Tavern Dossier Page", "Recruit Tavern Crew 0" };
            Rect previousFooter = default;
            for (int index = 0; index < footerNames.Length; index++)
            {
                Rect current = BoundsIn(detail, Find<RectTransform>(root, footerNames[index]));
                AssertContained(footerPaper, current, footerNames[index]);
                if (index > 0) Assert.That(current.xMin - previousFooter.xMax, Is.GreaterThanOrEqualTo(19.9f), footerNames[index]);
                previousFooter = current;
            }

            for (int page = 0; page < PageKeys.Length; page++)
            {
                Find<Button>(root, "DossierPage" + PageKeys[page]).onClick.Invoke();
                AssertPage(root, page);
                string[] fields = new[] { "Tavern Crew Detail Name", "Tavern Crew Detail Role", "DossierPageBackground" }.Concat(PageFields[page]).ToArray();
                Rect previous = default;
                for (int field = 0; field < fields.Length; field++)
                {
                    Rect current = BoundsIn(detail, Find<RectTransform>(root, fields[field]));
                    AssertContained(profilePaper, current, fields[field]);
                    Assert.That(current.xMin - portrait.xMax, Is.GreaterThanOrEqualTo(200f), fields[field]);
                    if (field > 0) Assert.That(previous.yMin - current.yMax, Is.GreaterThanOrEqualTo(0.9f), fields[field]);
                    previous = current;
                }
                Assert.That(previous.yMin - recruit.yMax, Is.GreaterThanOrEqualTo(9.9f));
                foreach (Button tab in root.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("DossierPage")))
                    AssertContained(profilePaper, BoundsIn(detail, tab.GetComponent<RectTransform>()), tab.name);
                Assert.That(Find<Button>(root, "Back To Tavern Crew Lineup").gameObject.activeInHierarchy, Is.True);
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
                object story = catalog.GetMethod("StoryFor").Invoke(null, new[] { crew });
                AssertText(root, "Tavern Crew Detail Name", Read(crew, "DisplayName"), 36, crewId);
                AssertText(root, "Tavern Crew Detail Role", Read(crew, "PortName") + "　·　" + Read(crew, "RoleSummary"), 22, crewId);
                Assert.That(Find<Button>(root, "Recruit Tavern Crew " + Read(crew, "ProfessionIndex"))
                    .GetComponentInChildren<Text>().text,
                    Is.EqualTo("签署契约 · " + Read(crew, "ContractCost") + " 金币"), crewId + " / cost stays with confirmation");
                AssertText(root, "Tavern Crew Biography", Read(crew, "Biography"), 26, crewId);
                AssertBackgroundContainsOnlyBiography(root, crewId);
                AssertText(root, "Tavern Crew Story Heading", Read(story, "ChapterLabel"), 28, crewId);
                AssertText(root, "Tavern Crew Story", Read(story, "Body"), 26, crewId);
                AssertText(root, "Tavern Crew Skill Heading", "专长 · " + Read(crew, "Specialty") + "　｜　职业能力", 28, crewId);
                AssertText(root, "Tavern Crew Skill", Read(crew, "SkillDescription"), 26, crewId);
                AssertText(root, "Tavern Crew Stats", Read(crew, "Stats"), 26, crewId);
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

        [TestCase(0, 26)]
        [TestCase(1, 33)]
        [TestCase(2, 39)]
        public void ReadabilityModesKeepDossierBodyCompleteAndControlsReachable(int level, int expectedBodySize)
        {
            int? previousLevel = GameReadability.OverrideLevel;
            try
            {
                GameReadability.OverrideLevel = level;
                Transform root = CreatePreview(CoreGameplayPreviewPort.WhiteShell);
                Transform detail = root.Find("Tavern Crew Detail");
                for (int page = 0; page < PageKeys.Length; page++)
                {
                    Find<Button>(root, "DossierPage" + PageKeys[page]).onClick.Invoke();
                    RectTransform pageRect = Find<RectTransform>(root, "Tavern Dossier " + PageKeys[page] + " Page");
                    ScrollRect scroll = pageRect.GetComponent<ScrollRect>();
                    Assert.That(scroll != null, Is.EqualTo(level > 0), "Large text uses a real clipped reading viewport.");
                    if (level > 0)
                    {
                        Canvas.ForceUpdateCanvases();
                        Assert.That(scroll.content, Is.Not.Null);
                        Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(scroll.viewport.rect.height));
                        scroll.verticalNormalizedPosition = 0f;
                        Canvas.ForceUpdateCanvases();
                        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.01f));
                    }
                    Text[] bodies = pageRect.GetComponentsInChildren<Text>(true).Where(text =>
                        text.name.Contains("Biography") || text.name.Contains("Story") || text.name.Contains("Skill") || text.name.Contains("Stats") || text.name.Contains("Contract")).ToArray();
                    foreach (Text body in bodies)
                    {
                        Assert.That(body.font, Is.SameAs(GameTypography.BodyFont), body.name);
                        Assert.That(body.fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(body.name.Contains("Contract") ? 22 : 26)), body.name);
                        Assert.That(body.resizeTextForBestFit, Is.False, body.name);
                        Assert.That(body.text, Is.Not.Empty, body.name);
                        if (level > 0) Assert.That(body.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow), body.name);
                    }
                    Assert.That(Find<Button>(root, "Back To Tavern Crew Lineup").interactable, Is.True);
                    Assert.That(Find<Button>(root, "Recruit Tavern Crew 0").gameObject.activeInHierarchy, Is.True);
                }
            }
            finally { GameReadability.OverrideLevel = previousLevel; }
        }

        [Test]
        public void PageTabsAndArrowsOnlyChangeVisiblePageWithoutSpendingRecruitingOrSavingCrew()
        {
            Transform root = CreatePreview(CoreGameplayPreviewPort.BrokenAnchor);
            var session = controller.Session;
            decimal gold = session.ProfileGold;
            string savedCrew = CrewPreferenceSnapshot();
            RectTransform originalDetail = Find<RectTransform>(root, "Tavern Crew Detail");
            AssertPage(root, 0);
            Find<Button>(root, "Previous Tavern Dossier Page").onClick.Invoke();
            AssertPage(root, 0);
            Find<Button>(root, "Next Tavern Dossier Page").onClick.Invoke();
            AssertPage(root, 1);
            Find<Button>(root, "Next Tavern Dossier Page").onClick.Invoke();
            AssertPage(root, 2);
            Find<Button>(root, "Next Tavern Dossier Page").onClick.Invoke();
            AssertPage(root, 3);
            Find<Button>(root, "Previous Tavern Dossier Page").onClick.Invoke();
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
            Find<Button>(root, "Back To Tavern Crew Lineup").onClick.Invoke();
            Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name == "Tavern Crew Stage"), Is.True);
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
            Assert.That(Find<Text>(root, "Tavern Dossier Page Counter").text, Is.EqualTo("第 " + (selectedPage + 1) + " / 4 页"));
            Assert.That(Find<Button>(root, "Previous Tavern Dossier Page").interactable, Is.EqualTo(selectedPage > 0));
            Assert.That(Find<Button>(root, "Next Tavern Dossier Page").interactable, Is.EqualTo(selectedPage < 3));
        }

        private static void AssertText(Transform root, string name, string expected, int minimumSize, string crewId)
        {
            Text label = Find<Text>(root, name);
            string actual = label.text;
            if (name == "Tavern Crew Biography")
            {
                actual = WithoutLineBreaks(actual);
                expected = WithoutLineBreaks(expected);
            }
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
