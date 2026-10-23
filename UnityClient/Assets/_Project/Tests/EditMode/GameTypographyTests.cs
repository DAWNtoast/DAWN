using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using ShatteredSail.Core.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class GameTypographyTests
    {
        [Test]
        public void EmbeddedFont_LoadsFromTheAuthoredResourcesAsset_AndCoversCoreUiGlyphs()
        {
            Font embedded = Resources.Load<Font>(GameTypography.FontResourcePath);
            Assert.That(GameTypography.FontResourcePath, Is.EqualTo("Fonts/XiangcuiKesong"));
            Assert.That(embedded, Is.Not.Null, "Ship the font with the game; an OS-font fallback is not the authored typography.");
            Assert.That(GameTypography.InterfaceFont, Is.SameAs(embedded));
            Assert.That(AssetDatabase.GetAssetPath(embedded),
                Is.EqualTo("Assets/_Project/Resources/Fonts/XiangcuiKesong.ttf"));

            const string requiredGlyphs = "碎帆纪元立即使用返回港口当前功能名称时间总金币设置酒馆部件商店后勤断锚白贝铜钟盐冠暮潮灰鸥0123456789←→年月日:/+-%";
            embedded.RequestCharactersInTexture(requiredGlyphs, 24, FontStyle.Normal);
            string missing = new string(requiredGlyphs.Distinct().Where(character => !embedded.HasCharacter(character)).ToArray());
            Assert.That(missing, Is.Empty, "Missing authored glyphs: " + missing);
        }

        [Test]
        public void ReadingFont_IsEmbeddedAndCoversChineseNumbersAndNavigationSymbols()
        {
            Font body = Resources.Load<Font>(GameTypography.BodyFontResourcePath);
            Assert.That(body, Is.Not.Null);
            Assert.That(GameTypography.BodyFont, Is.SameAs(body));
            Assert.That(body, Is.Not.SameAs(GameTypography.InterfaceFont));
            const string glyphs = "第七港口船舱普通特殊物品生命格挡炮击火药船员治疗0123456789←→年月日:/+-%×";
            body.RequestCharactersInTexture(glyphs, 18, FontStyle.Normal);
            Assert.That(new string(glyphs.Distinct().Where(c => !body.HasCharacter(c)).ToArray()), Is.Empty);
        }

        [Test]
        public void NavigationCaptionsAndClock_FitTheirCompactReadingBoxes()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, CoreGameplayPreviewPort.GrayGull, 0);
                Canvas.ForceUpdateCanvases();
                string[] names = { "Map Title", "Navigation Title", "Lookout Heading", "Lookout Time Readout", "Lookout Time Legend" };
                foreach (string name in names)
                {
                    Text label = preview.Host.GetComponentsInChildren<Text>().Single(t => t.name == name);
                    Assert.That(label.text, Is.Not.Empty, name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), name + " must not disappear through vertical truncation");
                }
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void NavigationHeaderSkin_KeepsNativeDisplayWeightForTitleCargoAndSettings(CoreGameplayPreviewPort port)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Navigation, port, 0);
                Canvas.ForceUpdateCanvases();
                RectTransform header = preview.Host.GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Voyage Header");
                Text title = header.Find("Voyage Header Title").GetComponent<Text>();
                Text cargo = header.Find("Voyage Cargo/Label").GetComponent<Text>();
                Text settings = header.Find("Voyage Settings/Label").GetComponent<Text>();
                Assert.That(title.text, Is.Not.Empty);
                Assert.That(cargo.text, Is.EqualTo("船舱"));
                Assert.That(settings.text, Is.EqualTo("设置"));
                for (int pass = 0; pass < 2; pass++)
                {
                    if (pass > 0) NavigationHeaderSkin.Apply(header);
                    foreach (Text label in new[] { title, cargo, settings })
                    {
                        string context = port + " / " + label.transform.parent.name + " / " + label.name;
                        Assert.That(label.font, Is.SameAs(GameTypography.InterfaceFont), context + " must retain the selected display face.");
                        Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), context + " must not synthesize bold over the carved strokes.");
                        Assert.That(label.fontSize, Is.EqualTo(label == title ? 29 : 22), context);
                    }
                }
            }
        }

        [TestCase(FontStyle.Normal)]
        [TestCase(FontStyle.Bold)]
        [TestCase(FontStyle.Italic)]
        [TestCase(FontStyle.BoldAndItalic)]
        public void Apply_UsesNativeCarvedWeight_WithoutChangingSizeOrRichText(FontStyle requestedStyle)
        {
            GameObject host = new GameObject("Typography Apply Test", typeof(RectTransform), typeof(Text));
            try
            {
                Text label = host.GetComponent<Text>();
                label.text = "<size=13>总金币</size>\n1,234";
                label.supportRichText = true;
                label.lineSpacing = 1.7f;
                label.fontStyle = FontStyle.BoldAndItalic;
                GameTypography.Apply(label, 27, requestedStyle);

                Assert.That(label.font, Is.SameAs(requestedStyle == FontStyle.Bold ? GameTypography.InterfaceFont : GameTypography.BodyFont));
                Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), "Synthetic weight or slant obscures the carved face's small counters.");
                Assert.That(label.fontSize, Is.EqualTo(27), "Typography must preserve the layout's authored size.");
                Assert.That(label.lineSpacing, Is.EqualTo(1f));
                Assert.That(label.text, Is.EqualTo("<size=13>总金币</size>\n1,234"));
                Assert.That(label.supportRichText, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(typeof(CoreGameplayDemoController))]
        [TestCase(typeof(MainMenuController))]
        [TestCase(typeof(TutorialVoyageController))]
        public void EveryInterfaceFontFactory_BorrowsTheSameEmbeddedAsset(Type controllerType)
        {
            GameObject host = new GameObject("Inactive Typography Factory Test");
            host.SetActive(false);
            try
            {
                Component controller = host.AddComponent(controllerType);
                MethodInfo factory = controllerType.GetMethod("CreateInterfaceFont",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                Assert.That(factory, Is.Not.Null, controllerType.Name);
                Assert.That(factory.Invoke(factory.IsStatic ? null : controller, null), Is.SameAs(GameTypography.InterfaceFont), controllerType.Name);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void AllPreviewPages_UseEmbeddedFacesIncludingRichTextAndSeedInput(CoreGameplayPreviewPort port)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                int checkedInputs = 0;
                int checkedSeedInputs = 0;
                int checkedRichTextLabels = 0;
                foreach (CoreGameplayPreviewScreen screen in Enum.GetValues(typeof(CoreGameplayPreviewScreen)))
                {
                    preview.Controller.ConfigureEditorPreview(screen, port, 0);
                    string context = port + " / " + screen;
                    Assert.That(preview.Controller.HasCompleteEditorPreview, Is.True, context);
                    Text[] labels = preview.Host.GetComponentsInChildren<Text>(true);
                    Assert.That(labels.Length, Is.GreaterThan(0), context);
                    foreach (Text label in labels)
                    {
                        AssertAuthoredText(label, context);
                        if (label.supportRichText && label.text.Contains("<size=")) checkedRichTextLabels += 1;
                    }

                    foreach (InputField input in preview.Host.GetComponentsInChildren<InputField>(true))
                    {
                        Assert.That(input.textComponent, Is.Not.Null, context);
                        AssertAuthoredText(input.textComponent, context + " / input value");
                        Text placeholder = input.placeholder as Text;
                        // Quantity fields already display their numeric value and need
                        // no placeholder. The editable chart seed still needs its hint.
                        if (input.name == "Seed Input")
                        {
                            Assert.That(placeholder, Is.Not.Null, context + " / seed input placeholder");
                            Assert.That(placeholder.text, Is.Not.Empty, context + " / seed input hint");
                            checkedSeedInputs += 1;
                        }
                        if (placeholder != null) AssertAuthoredText(placeholder, context + " / input placeholder");
                        checkedInputs += 1;
                    }
                    if (screen == CoreGameplayPreviewScreen.TavernCrewDetail)
                        AssertReadableDossierPages(preview.Host, context);
                }
                Assert.That(checkedInputs, Is.GreaterThan(0), "Include editable values in every port's regression sweep.");
                Assert.That(checkedSeedInputs, Is.GreaterThan(0), "Include the departure seed field and its hint in every port's regression sweep.");
                Assert.That(checkedRichTextLabels, Is.GreaterThan(0), "Include mixed-size titlebar readouts and descriptions.");
            }
        }

        [Test]
        public void ComponentDossierHeadings_UseTheSameNativeDisplayWeightAsCrewHeadings()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.ComponentShopDetail, CoreGameplayPreviewPort.BrokenAnchor, 0);
                Text[] labels = preview.Host.GetComponentsInChildren<Text>(true);
                foreach (string name in new[] { "Component Detail Name", "Component Detail Meta" })
                {
                    Text label = labels.Single(value => value.name == name);
                    Assert.That(label.font, Is.SameAs(GameTypography.InterfaceFont), name);
                    Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), name);
                    Assert.That(label.fontSize, Is.EqualTo(name == "Component Detail Name" ? 36 : 22), name);
                }
            }
        }

        [Test]
        public void CardFaceEmphasis_OnlyAllowsSharedUsageConditionsAndKeepsOtherTypographyGuards()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, CoreGameplayPreviewPort.BronzeBell, 0);
                Text[] labels = preview.Host.GetComponentsInChildren<Text>(true);
                Text effect = labels.Single(label => label.name == "Tavern Special Card Effect");
                // The navigator now grants action points without a usage prerequisite.
                // Exercise this card-face typography with the live cannon card instead.
                effect.text = CardFaceLayout.FormatDescription(CoreVoyageSession.CardLibrary.Single(card => card.Id == "card_chain_shot").Description);
                string valid = effect.text;
                Assert.That(valid, Does.Contain("<b>"), "Exercise a real card with a usage condition.");
                AssertAuthoredText(effect, "Shared card condition");

                effect.text = "<b>格挡 +4</b>。";
                Assert.Throws<AssertionException>(() => AssertAuthoredText(effect, "Result must not receive condition emphasis"));
                effect.text = valid;
                effect.fontStyle = FontStyle.Bold;
                Assert.Throws<AssertionException>(() => AssertAuthoredText(effect, "Card face still forbids whole-label synthetic bold"));
                effect.fontStyle = FontStyle.Normal;
                effect.text = "<i>60米内</i>";
                Assert.Throws<AssertionException>(() => AssertAuthoredText(effect, "Card face still forbids synthetic italics"));

                Text biography = labels.Single(label => label.name == "Tavern Crew Biography");
                biography.text = "<b>60米内</b>";
                Assert.Throws<AssertionException>(() => AssertAuthoredText(biography, "Ordinary prose cannot borrow the card exception"));
            }
        }

        [Test]
        public void MainMenuAndHiddenDialogs_UseTheSameEmbeddedTypography()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject host = new GameObject("Main Menu Typography Preview");
            host.SetActive(false);
            SceneManager.MoveGameObjectToScene(host, scene);
            try
            {
                host.AddComponent<MainMenuController>();
                host.SetActive(true);
                Text[] labels = host.GetComponentsInChildren<Text>(true);
                Assert.That(labels.Length, Is.GreaterThan(10));
                Assert.That(labels.Any(label => !label.gameObject.activeInHierarchy), Is.True,
                    "Check settings/achievement/confirmation content even while its panel is hidden.");
                foreach (Text label in labels) AssertAuthoredText(label, "Main menu and dialogs");
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void RebuildingAndDestroyingPreviews_DoesNotDestroyTheBorrowedFont()
        {
            Font original = GameTypography.InterfaceFont;
            int instanceId = original.GetInstanceID();
            using (PreviewFixture preview = new PreviewFixture())
            {
                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    preview.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.TavernCrewDetail, port, 0);
                    Assert.That(original != null, Is.True, "Preview cleanup must never Destroy a shared Resources font.");
                    Assert.That(GameTypography.InterfaceFont.GetInstanceID(), Is.EqualTo(instanceId));
                }
                preview.Host.SetActive(false);
                Assert.That(original != null, Is.True, "Disabling the preview must preserve the borrowed asset.");
                preview.Host.SetActive(true);
                Assert.That(GameTypography.InterfaceFont.GetInstanceID(), Is.EqualTo(instanceId));
            }
            Assert.That(original != null, Is.True, "Destroying the preview host must preserve the borrowed asset.");
            Assert.That(Resources.Load<Font>(GameTypography.FontResourcePath), Is.SameAs(original));
            using (PreviewFixture second = new PreviewFixture())
            {
                second.Controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.GrayGull, 0);
                foreach (Text label in second.Host.GetComponentsInChildren<Text>(true))
                    Assert.That(label.font, Is.SameAs(original).Or.SameAs(GameTypography.BodyFont));
            }
        }

        private static void AssertReadableDossierPages(GameObject host, string context)
        {
            Text[] labels = host.GetComponentsInChildren<Text>(true);
            Assert.That(labels.Single(label => label.name == "Tavern Crew Detail Name").fontSize, Is.EqualTo(36), context);
            Assert.That(labels.Single(label => label.name == "Tavern Crew Detail Role").fontSize, Is.EqualTo(22), context);
            Assert.That(labels.Any(label => label.name == "Tavern Crew Appearance"), Is.False, context);
            Assert.That(labels.Any(label => label.name == "Tavern Crew Profile Heading"), Is.False, context);
            string[] bodyFields = { "Tavern Crew Biography", "Tavern Crew Story", "Tavern Crew Skill" };
            foreach (string field in bodyFields)
            {
                Text label = labels.Single(value => value.name == field);
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(26), context + " / " + field);
                Assert.That(label.resizeTextForBestFit, Is.False, context + " / " + field + " must keep its readable size.");
            }
            foreach (string page in new[] { "Background", "Story", "Skills", "SpecialCards" })
            {
                Button tab = host.GetComponentsInChildren<Button>(true).Single(button => button.name == "DossierPage" + page);
                tab.onClick.Invoke();
                Assert.That(tab.GetComponentInChildren<Text>().fontSize, Is.GreaterThanOrEqualTo(24), context);
                foreach (Text label in host.GetComponentsInChildren<Text>()) AssertAuthoredText(label, context + " / " + page);
            }
        }

        private static void AssertAuthoredText(Text label, string context)
        {
            Assert.That(label.font, Is.SameAs(GameTypography.InterfaceFont).Or.SameAs(GameTypography.BodyFont), context + " / " + label.name);
            Assert.That(label.fontStyle, Is.EqualTo(FontStyle.Normal), context + " / " + label.name);
            if (IsCardFaceEffect(label))
            {
                // Shared card faces deliberately emphasize usage conditions. Strip
                // every bold span and recreate it, so arbitrary emphasis cannot hide
                // behind the exception or the formatter's preservation of existing tags.
                Assert.That(label.font, Is.SameAs(GameTypography.BodyFont), context + " / " + label.name);
                Assert.That(label.supportRichText, Is.True, context + " / " + label.name);
                // The shared card renderer may insert additional line breaks
                // inside one prerequisite and close/reopen its bold span on
                // each line. Recover the complete clause before matching it.
                Assert.That(NormalizedCardEmphasis(label.text),
                    Is.EqualTo(ExpectedCardEmphasis(label.text)),
                    context + " / " + label.name + " must emphasize only shared card usage conditions.");
            }
            else Assert.That(label.text, Does.Not.Contain("<b>"), context + " / " + label.name + " must not reintroduce synthetic bold through rich text.");
            Assert.That(label.text, Does.Not.Contain("<i>"), context + " / " + label.name + " must not reintroduce synthetic italics through rich text.");
            Assert.That(label.lineSpacing, Is.EqualTo(ExpectedLineSpacing(label.name)), context + " / " + label.name);
            Assert.That(label.fontSize, Is.GreaterThan(0), context + " / " + label.name);
        }

        // Clause separators may be rendered as a line break or a semicolon.
        // This audit checks emphasis; CardDescriptionTextTests separately checks
        // every visible word and requires each original clause boundary to remain.
        private static string WithoutTypographyWhitespace(string text) =>
            new string(text.Where(letter => !char.IsWhiteSpace(letter) && letter != '；').ToArray());

        private static string NormalizedCardEmphasis(string text) =>
            WithoutTypographyWhitespace(text).Replace("</b><b>", string.Empty);

        private static string ExpectedCardEmphasis(string text)
        {
            string plain = WithoutTypographyWhitespace(text).Replace("<b>", string.Empty).Replace("</b>", string.Empty);
            return NormalizedCardEmphasis(CardFaceLayout.FormatDescription(plain));
        }

        [TestCase("靠近60米并转为平行\n<b>平行且距离不超过</b>\n<b>60\u00a0米</b>可接舷", true)]
        [TestCase("靠近60米并转为平行\n<b>平行且距离不超过60米</b>可接舷", true)]
        [TestCase("<b>靠近60米</b>并转为平行\n<b>平行且距离不超过60米</b>可接舷", false)]
        [TestCase("靠近60米并转为平行\n平行且<b>距离不超过60米</b>可接舷", false)]
        [TestCase("靠近60米并转为平行\n平行且距离不超过60米可接舷", false)]
        [TestCase("<b>格挡6</b>", false)]
        [TestCase("<b>范围1-4格</b>：造成28点船体伤害", true)]
        [TestCase("<b>范围1-4格</b>：<b>造成28点船体伤害</b>", false)]
        [TestCase("范围1-4格：造成28点船体伤害", false)]
        public void CardEmphasisAudit_AcceptsWrappedPrerequisitesButRejectsAddedOrMissingEmphasis(string text, bool valid)
        {
            Assert.That(NormalizedCardEmphasis(text) == ExpectedCardEmphasis(text), Is.EqualTo(valid));
        }

        private static float ExpectedLineSpacing(string name)
        {
            // Independent inventory of authored Runtime spacing overrides. These
            // paragraphs keep their accepted market, chart and dossier layouts;
            // every remaining label retains GameTypography's default spacing.
            switch (name)
            {
                case "Confirmation Body": return 1.35f;
                case "Component Detail Description":
                case "Component Detail Effect":
                case "Component Detail Story Body":
                case "Tavern Crew Biography": return 1.25f;
                // The market's two compact 48px inventory readouts use the
                // authored item-detail spacing, distinct from dossier prose.
                case "Market Detail Stock":
                case "Market Detail Cargo":
                case "Market Purchase Choice Quote":
                case "Component Special Card Growth":
                case "Tavern Special Card Growth": return 1.15f;
                case "Guided Tutorial Instruction":
                case "Boarding Clash Label":
                case "Component Detail Specialization":
                case "Component Detail Effect Heading":
                case "Component Detail Story Chapter":
                case "Tavern Crew Story Heading":
                case "Tavern Crew Skill Heading": return 1.1f;
                case "Forecast Detail Text":
                case "Tavern Crew Skill": return 1.3f;
                case "Component Special Card Calculation":
                case "Tavern Special Card Calculation": return 1.18f;
                case "Forecast": return 1.16f;
                case "Lookout Report": return 1.08f;
                case "Shipyard Dossier Story":
                case "Warehouse Treasure Story":
                case "Tavern Crew Story":
                case "Component Special Card Lore":
                case "Component Special Card Empty":
                case "Tavern Special Card Lore":
                case "Tavern Special Card Empty": return 1.22f;
                default: return 1f;
            }
        }

        private static bool IsCardFaceEffect(Text label)
        {
            RectTransform card = label.transform.parent as RectTransform;
            return card != null && card.sizeDelta == CardFaceLayout.CardSize &&
                label.rectTransform.sizeDelta == CardFaceLayout.EffectSize &&
                label.rectTransform.anchoredPosition == CardFaceLayout.EffectPosition &&
                (card.GetComponentsInChildren<BattleCardSurfaceGraphic>(true).Any(graphic => graphic.transform.parent == card) ||
                 card.GetComponentsInChildren<BattleCardFrameGraphic>(true).Any(graphic => graphic.transform.parent == card) ||
                 card.GetComponentsInChildren<CardSchoolFrameGraphic>(true).Any(graphic => graphic.transform.parent == card));
        }

        private sealed class PreviewFixture : IDisposable
        {
            private readonly Scene scene;
            public GameObject Host { get; }
            public CoreGameplayDemoController Controller { get; }

            public PreviewFixture()
            {
                Assert.That(Application.isPlaying, Is.False);
                scene = EditorSceneManager.NewPreviewScene();
                Host = new GameObject("Game Typography Preview") { hideFlags = HideFlags.HideAndDontSave };
                Host.SetActive(false);
                SceneManager.MoveGameObjectToScene(Host, scene);
                Controller = Host.AddComponent<CoreGameplayDemoController>();
                Host.SetActive(true);
            }

            public void Dispose()
            {
                if (Host != null) UnityEngine.Object.DestroyImmediate(Host);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
