using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class SettlementPresentationTests
    {
        private GameObject host;
        private CoreGameplayDemoController controller;
        [TearDown] public void Cleanup() { if (host != null) Object.DestroyImmediate(host); }
        private void Create()
        {
            host = new GameObject("Settlement Presentation Test"); host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Settlement, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true); Canvas.ForceUpdateCanvases();
        }
        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(x => x.name == name);
        private void Render(VoyageSettlement result)
        {
            typeof(CoreVoyageSession).GetProperty("Settlement").GetSetMethod(true).Invoke(controller.Session, new object[] { result });
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
        }

        [Test]
        public void Settlement_LoadsNewArt_AndShowsClearlyMarkedSampleWithoutChangingRealReward()
        {
            Create();
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_hold_v1"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_chest_v1"), Is.Not.Null);
            Assert.That(Resources.Load<Texture2D>("SettlementReveal/settlement_journal_v1"), Is.Not.Null);
            Assert.That(Find<Text>("Settlement Badge").text, Does.Contain("结算预览").And.Contain("不写入存档"));
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("+1,268"));
            Assert.That(controller.Session.Settlement.BankedGold, Is.Zero);
            Assert.That(Find<Text>("Settlement Title").fontSize, Is.GreaterThanOrEqualTo(64));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Header"), Is.False,
                "No generic Header should remain above the cinematic results page.");
            Assert.That(Find<SettlementRevealController>("Settlement Card").IsComplete, Is.True);
            RectTransform backdrop = Find<RawImage>("Settlement Hold").rectTransform;
            RectTransform card = Find<RectTransform>("Settlement Card");
            Assert.That(card.anchoredPosition + backdrop.anchoredPosition, Is.EqualTo(Vector2.zero),
                "The raised results card must not shift its full-screen backdrop off the canvas.");
            Assert.That(backdrop.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [TestCase(0, "凯旋归港", true)]
        [TestCase(1, "战利归仓", true)]
        [TestCase(2, "整备，再启航", false)]
        [TestCase(3, "平安归航", false)]
        public void OutcomeHasAppropriateTitleAndCelebration(int scenario, string title, bool celebration)
        {
            Create(); Render(SettlementPreviewSamples.Create(scenario));
            Assert.That(Find<Text>("Settlement Title").text, Is.EqualTo(title));
            Assert.That(Find<SettlementCelebrationGraphic>("Settlement Gold Burst").gameObject.activeSelf, Is.EqualTo(celebration));
            Assert.That(Find<Button>("Return To Port").interactable, Is.True);
            if (!celebration) Assert.That(host.GetComponentsInChildren<RawImage>().Any(x => x.name == "Settlement Treasure Chest"), Is.False);
        }

        [Test]
        public void NonGoldRewardBecomesHeadline_AndZeroDoesNotPretendToBePositive()
        {
            Create(); Render(SettlementPreviewSamples.Create(4));
            Assert.That(Find<Text>("Settlement Featured Name").text, Is.EqualTo("木材"));
            Assert.That(Find<Text>("Settlement Reward Wood").text, Is.EqualTo("+38"));
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("0"));
        }

        [Test]
        public void TimelineIsExactMonotonicAndSkippable_WithoutRewardOrSaveMutation()
        {
            Create(); VoyageSettlement result = SettlementPreviewSamples.Create(0); Render(result);
            decimal before = controller.Session.ProfileGold;
            var reveal = Find<SettlementRevealController>("Settlement Card");
            reveal.SetElapsed(0); Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("0"));
            Assert.That(Find<Button>("Return To Port").interactable, Is.True, "Never force a player to wait for the reveal.");
            int previous = 0;
            for (float time = 0.6f; time < 2.8f; time += 0.11f)
            {
                reveal.SetElapsed(time);
                int current = int.Parse(Find<Text>("Settlement Reward Gold").text.Replace(",", "").TrimStart('+'));
                Assert.That(current, Is.InRange(previous, 1268)); previous = current;
            }
            reveal.CompleteImmediately(); reveal.CompleteImmediately();
            Assert.That(reveal.IsComplete, Is.True);
            Assert.That(Find<Text>("Settlement Reward Gold").text, Is.EqualTo("+1,268"));
            Assert.That(Find<Text>("Settlement Reward Fame").text, Is.EqualTo("+47"));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(before));
            Assert.That(result.BankedGold, Is.EqualTo(1268));
        }

        [Test]
        public void SafeReturnDoesNotPlaySinkingSound()
        {
            // Settlement uses its dedicated world cues, separate from battle
            // victory/discovery/sinking sounds. A failed voyage is a salvage
            // outcome here, even when the preceding battle already sank a ship.
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(0)), Is.EqualTo("harbor_arrive"));
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(1)), Is.EqualTo("settlement_reward"));
            Assert.That(SettlementRevealAudio.OutcomeCue(SettlementPreviewSamples.Create(2)), Is.EqualTo("settlement_failure"));
            VoyageSettlement treasure = SettlementPreviewSamples.Create(1);
            treasure.TreasureCompleted = true;
            Assert.That(SettlementRevealAudio.OutcomeCue(treasure), Is.EqualTo("settlement_treasure"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EarnedFragmentsAreVisibleInOrdinaryAndTutorialSettlement(bool tutorial)
        {
            Create();
            typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, tutorial);
            if (tutorial)
            {
                typeof(CoreVoyageSession).GetProperty("GuidedTutorialLeg").GetSetMethod(true)
                    .Invoke(controller.Session, new object[] { CoreGuidedTutorialContent.BossVoyageLeg });
                typeof(CoreVoyageSession).GetProperty("GuidedTutorialBossDefeated").GetSetMethod(true)
                    .Invoke(controller.Session, new object[] { true });
            }
            VoyageSettlement result = SettlementPreviewSamples.Create(1);
            result.BankedFragments = 1;
            result.BankedPirateExperience = 15;
            Render(result);
            Text rewards = Find<Text>("Settlement Treasure Rewards");
            Assert.That(rewards.text, Does.Contain("藏宝图碎片 +1"));
            Assert.That(rewards.text, Does.Contain("海盗经验 +15"));
            Assert.That(rewards.preferredHeight, Is.LessThanOrEqualTo(rewards.rectTransform.rect.height + 1f));
            if (tutorial)
            {
                RectTransform component = Find<RectTransform>("Guided Tutorial Component Reward");
                RectTransform manifest = Find<RectTransform>("Settlement Treasure Manifest");
                Assert.That(component.parent, Is.SameAs(manifest.parent));
                Rect cardBounds = new Rect(component.anchoredPosition - component.sizeDelta * .5f, component.sizeDelta);
                Rect fragmentBounds = new Rect(manifest.anchoredPosition - manifest.sizeDelta * .5f, manifest.sizeDelta);
                Assert.That(cardBounds.Overlaps(fragmentBounds), Is.False,
                    "The actual tutorial component/valuable card must not obscure fragments or pirate XP.");
                Assert.That(Find<Text>("Guided Tutorial Reward Text").text, Does.Contain("破浪者的罗盘"));
            }
            Assert.That(controller.Session.TreasureFragments, Is.Zero,
                "Displaying the result must not award or bank the fragment a second time.");
        }

        [Test]
        public void ExperienceOnlySettlementDoesNotAnnounceAFragmentDrop()
        {
            Create();
            VoyageSettlement result = SettlementPreviewSamples.Create(1);
            result.BankedPirateExperience = 15;
            Render(result);
            Assert.That(Find<Text>("Settlement Treasure Rewards").text, Is.EqualTo("海盗经验 +15"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void FinalTextAndActionsFitTheirAuthoredBounds(int scenario)
        {
            Create();
            VoyageSettlement result = SettlementPreviewSamples.Create(scenario, "盐冠港");
            Render(result);
            var card = Find<RectTransform>("Settlement Card");
            // Validate authored logical geometry independently of the batch
            // Editor's Game View size. Production scaler behavior is covered by
            // the separate renders with real target resolutions and scaling.
            Canvas canvas = card.GetComponentInParent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.scaleFactor = 1f;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            canvasRect.localScale = Vector3.one;
            Find<SettlementRevealController>("Settlement Card").CompleteImmediately();
            Text[] labels = card.GetComponentsInChildren<Text>()
                .Where(label => label.isActiveAndEnabled && !string.IsNullOrEmpty(label.text)).ToArray();
            // Font atlas rebuilds can dirty another label during the same pass.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (Text label in labels) label.SetAllDirty();
                Canvas.ForceUpdateCanvases();
            }
            foreach (Text label in labels)
            {
                Vector3[] rendered = RenderedTextVertices(label);
                int renderedGlyphs = VisibleGlyphBounds(rendered, out Rect visible);
                Assert.That(renderedGlyphs, Is.GreaterThan(0), label.name + " renders no visible text");
                Rect rect = label.rectTransform.rect;
                Assert.That(visible.xMin, Is.GreaterThanOrEqualTo(rect.xMin - 1f), label.name + " clips on the left");
                Assert.That(visible.xMax, Is.LessThanOrEqualTo(rect.xMax + 1f), label.name + " clips on the right");
                Assert.That(visible.yMin, Is.GreaterThanOrEqualTo(rect.yMin - 1f), label.name + " clips below its rectangle");
                Assert.That(visible.yMax, Is.LessThanOrEqualTo(rect.yMax + 1f), label.name + " clips above its rectangle");
                if (label.alignment == TextAnchor.MiddleLeft || label.alignment == TextAnchor.MiddleCenter ||
                    label.alignment == TextAnchor.MiddleRight)
                    Assert.That(Mathf.Abs(visible.center.y - rect.center.y), Is.LessThan(1f), label.name + " is not vertically centered");
                if (label.alignment == TextAnchor.MiddleCenter)
                    Assert.That(Mathf.Abs(visible.center.x - rect.center.x), Is.LessThan(1f), label.name + " is not horizontally centered");
                Transform row = label.transform.parent;
                if (row.name == "Settlement Featured Reward" || row.name.StartsWith("Settlement Reward Tile "))
                {
                    Rect textInRow = BoundsRelativeTo(visible, label.transform, row);
                    foreach (RawImage icon in row.GetComponentsInChildren<RawImage>()
                        .Where(icon => icon.isActiveAndEnabled && icon.transform.parent == row))
                    {
                        Rect iconInRow = BoundsRelativeTo(icon.rectTransform.rect, icon.transform, row);
                        Assert.That(textInRow.Overlaps(iconInRow), Is.False, label.name + " overlaps " + icon.name);
                    }
                }

                // A truncated mesh can fit perfectly while silently dropping a line.
                // Compare against unrestricted height at the same width and final
                // best-fit font size, rather than comparing the font's line box.
                var settings = label.GetGenerationSettings(rect.size);
                if (label.resizeTextForBestFit)
                    settings.fontSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                settings.resizeTextForBestFit = false;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                using (var complete = new TextGenerator())
                {
                    Assert.That(complete.Populate(label.text, settings), Is.True, label.name + " cannot generate complete text");
                    int completeGlyphs = VisibleGlyphBounds(complete.verts.Select(vertex => vertex.position).ToArray(), out _);
                    Assert.That(renderedGlyphs, Is.EqualTo(completeGlyphs), label.name + " loses glyphs through truncation");
                    Assert.That(label.cachedTextGenerator.characterCountVisible, Is.EqualTo(complete.characterCountVisible),
                        label.name + " loses characters or a line through truncation");
                }
            }
            Assert.That(labels.Any(label => label.text == "船体修缮" || label.text == "铸造储备" || label.text == "远航声名"),
                Is.False, "Removed material/reputation hints must be absent, including when a material is the featured reward.");
            Assert.That(card.GetComponentsInChildren<Transform>(true).Any(item =>
                item.name == "Settlement Featured Detail" || item.name == "Settlement Journal Caption"), Is.False,
                "Removed explanatory captions must not return.");
            string[] rewardIds = { "Gold", "Wood", "Iron", "Fame" };
            string[] rewardNames = { "金币", "木材", "铁矿", "声望" };
            int[] rewardAmounts = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
            for (int i = 0; i < rewardIds.Length; i++)
            {
                Assert.That(labels.Count(label => label.text == rewardNames[i]), Is.EqualTo(1),
                    rewardIds[i] + " keeps its resource name after explanatory captions are removed.");
                Assert.That(Find<Text>("Settlement Reward " + rewardIds[i]).text,
                    Is.EqualTo((rewardAmounts[i] > 0 ? "+" : "") + rewardAmounts[i].ToString("N0")),
                    rewardIds[i] + " keeps the complete, exact settled amount.");
            }
            foreach (Button button in card.GetComponentsInChildren<Button>())
            {
                Vector3[] corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = card.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(-930f, 930f), button.name);
                    Assert.That(local.y, Is.InRange(-470f, 470f), button.name);
                }
            }
        }

        private static Vector3[] RenderedTextVertices(Text label)
        {
            Mesh rendered = label.canvasRenderer.GetMesh();
            if (rendered != null && rendered.vertexCount > 0) return rendered.vertices;
            // A headless EditMode canvas may not reach PreRender. In that case
            // exercise Text's real generation and attached mesh effects directly.
            using (var vertices = new VertexHelper())
            {
                typeof(Text).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(label, new object[] { vertices });
                foreach (IMeshModifier modifier in label.GetComponents<Component>().OfType<IMeshModifier>())
                    modifier.ModifyMesh(vertices);
                var mesh = new Mesh();
                try
                {
                    vertices.FillMesh(mesh);
                    return mesh.vertices;
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        private static int VisibleGlyphBounds(Vector3[] vertices, out Rect bounds)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            int glyphs = 0;
            for (int start = 0; start + 3 < vertices.Length; start += 4)
            {
                Vector2 quadMin = vertices[start], quadMax = vertices[start];
                for (int i = start + 1; i < start + 4; i++)
                {
                    quadMin = Vector2.Min(quadMin, vertices[i]);
                    quadMax = Vector2.Max(quadMax, vertices[i]);
                }
                // Empty space/newline quads do not describe visible glyph bounds.
                if (quadMax.x - quadMin.x <= .001f || quadMax.y - quadMin.y <= .001f) continue;
                min = Vector2.Min(min, quadMin); max = Vector2.Max(max, quadMax); glyphs++;
            }
            bounds = glyphs == 0 ? default : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return glyphs;
        }

        private static Rect BoundsRelativeTo(Rect bounds, Transform item, Transform relativeTo)
        {
            var corners = new[]
            {
                new Vector3(bounds.xMin, bounds.yMin), new Vector3(bounds.xMin, bounds.yMax),
                new Vector3(bounds.xMax, bounds.yMin), new Vector3(bounds.xMax, bounds.yMax)
            };
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = relativeTo.InverseTransformPoint(item.TransformPoint(corner));
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

