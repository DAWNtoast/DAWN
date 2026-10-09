using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    /// <summary>
    /// Layout contracts for the authored battle card.  These checks intentionally
    /// use rendered world bounds rather than only anchored positions so a future
    /// change to the battle-card scale cannot reintroduce a one-pixel seam or a
    /// panel which floats over the hand.
    /// </summary>
    public sealed class BattleHudLayoutTests
    {
        private GameObject host;

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
        public void BattlePreview_EveryRouteBuildsDeterministicallyWithoutSailing(CoreGameplayPreviewPort port)
        {
            CreateBattlePreview(port);
            CoreGameplayDemoController controller = host.GetComponent<CoreGameplayDemoController>();
            for (int route = 0; route < CoreVoyageSession.Routes.Count; route++)
            {
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, port, route);
                string context = port + " / route " + route;
                Assert.That(controller.HasCompleteEditorPreview, Is.True, context);
                Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle), context);
                Assert.That(controller.Session.Battle, Is.Not.Null, context);
                Assert.That(controller.Session.TotalDistanceNm, Is.Zero, "No random sailing may be required to construct " + context);
                Assert.That(controller.Session.TotalHours, Is.Zero, "Preview preparation may not consume voyage/event time: " + context);
                Assert.That(controller.Session.Battle.Hand.Count, Is.GreaterThan(0), context);
                int[] hand = controller.Session.Battle.Hand.ToArray();
                var decisionSeed = controller.Session.Battle.DecisionSeed;
                string encounterId = controller.Session.ActiveEncounter.Id;

                controller.RefreshEditorPreview();

                Assert.That(controller.HasCompleteEditorPreview, Is.True, context + " / rebuild");
                Assert.That(controller.Session.ActiveEncounter.Id, Is.EqualTo(encounterId), context);
                Assert.That(controller.Session.Battle.DecisionSeed, Is.EqualTo(decisionSeed), context);
                Assert.That(controller.Session.Battle.Hand, Is.EqualTo(hand), context + " / opening hand");
            }
        }

        [Test]
        public void BattleSceneArt_CoversTheBattleSurface()
        {
            CreateBattlePreview();
            RectTransform art = Find<RectTransform>("Battle Scene Art");
            RectTransform battleCard = Find<RectTransform>("Battle Card");
            RectTransform overlay = Find<RectTransform>("Battle Overlay");

            Rect artBounds = WorldRect(art);
            AssertContains(artBounds, WorldRect(battleCard), 1.5f,
                "The scene art must cover the full battle card after the authored scale is applied.");
            Assert.That(art.sizeDelta.x, Is.EqualTo(1860f).Within(0.1f));
            Assert.That(art.sizeDelta.y, Is.GreaterThanOrEqualTo(900f));
            Assert.That(battleCard.sizeDelta.x, Is.EqualTo(1740f).Within(0.1f));
            Assert.That(battleCard.sizeDelta.y, Is.EqualTo(900f).Within(0.1f));
            Assert.That(overlay.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.BackgroundSize).Within(0.1f));
            Assert.That(overlay.anchoredPosition, Is.EqualTo(PhaseSurfaceLayout.BackgroundPosition).Within(0.1f));
            float expectedScale = Mathf.Min(PhaseSurfaceLayout.BackgroundSize.x / 1740f,
                PhaseSurfaceLayout.BackgroundSize.y / 900f);
            Assert.That(battleCard.localScale.x, Is.EqualTo(expectedScale).Within(0.001f));
            Assert.That(battleCard.localScale.y, Is.EqualTo(expectedScale).Within(0.001f));
            Image battleCardSurface = battleCard.GetComponent<Image>();
            Assert.That(battleCardSurface.sprite, Is.Null, "The authored battle card surface keeps the parchment image cleared.");
            Assert.That(battleCardSurface.type, Is.EqualTo(Image.Type.Tiled));
            RawImage backdrop = Find<RawImage>("Battle Scene Art");
            Assert.That(backdrop.uvRect.y, Is.EqualTo(0.07012101f).Within(0.0001f));
            Assert.That(backdrop.uvRect.height, Is.EqualTo(0.85975796f).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BattleHeader_ReservesItsActualHeightAndSeparatesReadoutsFromStatusAndTeaching(bool tutorial)
        {
            CreateBattlePreview();
            CoreGameplayDemoController controller = host.GetComponent<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            CoreBattleState battle = session.Battle;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo tutorialActive = typeof(CoreGameplayDemoController).GetField("tutorialActive", flags);
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", flags);
            Assert.That(tutorialActive, Is.Not.Null);
            Assert.That(render, Is.Not.Null);
            // Rebuild this disposable preview's real hierarchy. Do not initialize
            // tutorial persistence or invoke any battle/gameplay action callback.
            tutorialActive.SetValue(controller, tutorial);
            render.Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.Session, Is.SameAs(session));
            Assert.That(controller.Session.Battle, Is.SameAs(battle));
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));

            float expectedHeaderHeight = tutorial ? 148f : 108f;
            RectTransform header = Find<RectTransform>("Voyage Header");
            RectTransform overlay = Find<RectTransform>("Battle Overlay");
            RectTransform card = Find<RectTransform>("Battle Card");
            RectTransform readouts = Find<RectTransform>("Battle Readouts Overlay");
            Assert.That(header.rect.height, Is.EqualTo(expectedHeaderHeight).Within(0.01f));
            Assert.That(header.anchoredPosition.y, Is.EqualTo(-expectedHeaderHeight * .5f).Within(0.01f));
            Assert.That(overlay.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f - expectedHeaderHeight)).Within(0.01f));
            Assert.That(overlay.anchoredPosition, Is.EqualTo(new Vector2(0f, -expectedHeaderHeight * .5f)).Within(0.01f));
            float expectedScale = Mathf.Min(1920f / 1740f, (1080f - expectedHeaderHeight) / 900f);
            Assert.That(card.localScale.x, Is.EqualTo(expectedScale).Within(0.001f));
            Assert.That(card.localScale.y, Is.EqualTo(expectedScale).Within(0.001f));
            Assert.That(readouts.parent, Is.SameAs(header.parent));
            Assert.That(readouts.GetSiblingIndex(), Is.GreaterThan(header.GetSiblingIndex()), "Readouts remain visible above the decorative header.");

            Rect headerBounds = WorldRect(header);
            Assert.That(headerBounds.height, Is.GreaterThan(1f), "Collapsed canvas geometry must not make containment checks vacuous.");
            Assert.That(WorldRect(overlay).yMax, Is.EqualTo(headerBounds.yMin).Within(0.1f), "Battle surface meets the actual header without a seam or overlap.");
            string[] readoutNames = { "Battle Range", "Battle Alignment", "Voyage Header Status" };
            Rect[] bounds = new Rect[readoutNames.Length];
            for (int i = 0; i < readoutNames.Length; i++)
            {
                Text label = Find<Text>(readoutNames[i]);
                Assert.That(label.isActiveAndEnabled, Is.True, readoutNames[i]);
                Assert.That(label.text, Is.Not.Empty, readoutNames[i]);
                bounds[i] = WorldRect(label.rectTransform);
                AssertContains(headerBounds, bounds[i], 0.1f, readoutNames[i] + " must fit inside the actual header");
                for (int preceding = 0; preceding < i; preceding++)
                    Assert.That(bounds[i].Overlaps(bounds[preceding]), Is.False, readoutNames[i] + " overlaps " + readoutNames[preceding]);
            }

            if (tutorial)
            {
                Rect? precedingBand = null;
                foreach (string name in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                {
                    Text label = Find<Text>(name);
                    Assert.That(label.isActiveAndEnabled, Is.True, name);
                    Assert.That(label.text, Is.Not.Empty, name);
                    Rect band = WorldRect(label.rectTransform);
                    AssertContains(headerBounds, band, 0.1f, name + " must stay in its reserved teaching band");
                    for (int i = 0; i < bounds.Length; i++)
                        Assert.That(band.Overlaps(bounds[i]), Is.False, name + " overlaps " + readoutNames[i]);
                    if (precedingBand.HasValue)
                        Assert.That(band.Overlaps(precedingBand.Value), Is.False, "Teaching objective and instruction must remain separate.");
                    precedingBand = band;
                }
            }
            else
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Guided Tutorial Banner"), Is.False);
        }

        [Test]
        public void BattleSidePanels_UseRetreatWoodAndShareACommonWidth()
        {
            CreateBattlePreview();
            Image retreat = Find<Button>("Retreat Battle").GetComponent<Image>();
            Image player = Find<Image>("Player Resource HUD");
            Image buff = Find<Image>("Battle Buff Panel");
            Image enemy = Find<Image>("Enemy Resource HUD");

            Assert.That(retreat.sprite, Is.Not.Null, "Retreat Battle needs its wood board face.");
            Assert.That(player.sprite, Is.SameAs(retreat.sprite));
            Assert.That(buff.sprite, Is.SameAs(retreat.sprite));
            Assert.That(enemy.sprite, Is.SameAs(retreat.sprite));
            Assert.That(player.rectTransform.sizeDelta.x, Is.EqualTo(buff.rectTransform.sizeDelta.x).Within(0.1f));
            Assert.That(player.rectTransform.sizeDelta.x, Is.EqualTo(enemy.rectTransform.sizeDelta.x).Within(0.1f));
            Assert.That(player.rectTransform.sizeDelta.x, Is.EqualTo(retreat.rectTransform.sizeDelta.x).Within(0.1f));
        }

        [Test]
        public void BattleSidePanels_KeepDecisionColumnSeparateFromTheHand()
        {
            CreateBattlePreview();
            Rect hand = WorldRect(Find<RectTransform>("Battle Hand"));
            RectTransform player = Find<RectTransform>("Player Resource HUD");
            RectTransform enemy = Find<RectTransform>("Enemy Resource HUD");
            RectTransform buff = Find<RectTransform>("Battle Buff Panel");
            RectTransform retreat = Find<RectTransform>("Retreat Battle");
            foreach (RectTransform panel in new[] { player, enemy, buff, retreat })
                Assert.That(WorldRect(panel).Overlaps(hand), Is.False, panel.name + " must not cover the hand.");
            Assert.That(WorldRect(player).yMin, Is.GreaterThan(WorldRect(buff).yMax),
                "Current resources precede the distinct buff panel.");
            Assert.That(WorldRect(buff).yMin, Is.GreaterThan(WorldRect(retreat).yMax),
                "Retreat keeps its own clickable row below buffs.");
        }

        [Test]
        public void BattleHullBars_AreCenteredDirectlyUnderTheirShips()
        {
            CreateBattlePreview();
            foreach (string side in new[] { "Player", "Enemy" })
            {
                Rect ship = WorldRect(Find<RectTransform>(side + " Ship Art"));
                Rect bar = WorldRect(Find<RectTransform>(side + " Battle Hull Bar Track"));
                Assert.That(bar.center.x, Is.EqualTo(ship.center.x).Within(3f), side + " hull bar should follow the ship center.");
                Assert.That(bar.center.y, Is.LessThan(ship.center.y), side + " hull bar should be below the ship.");
                Assert.That(bar.yMax, Is.LessThanOrEqualTo(ship.yMin + 3f), side + " hull bar must not cover the hull artwork.");
                Assert.That(bar.yMin, Is.GreaterThan(ship.yMin - 85f), side + " hull bar should remain close to the hull.");
            }
        }

        [Test]
        public void BattleCallouts_UseRegionalHeaderAndRemoveStageBanners()
        {
            CreateBattlePreview();
            RectTransform header = Find<RectTransform>("Voyage Header");
            RectTransform previewCanvas = host.transform.Find(CoreGameplayDemoController.EditorPreviewCanvasName) as RectTransform;
            Assert.That(previewCanvas, Is.Not.Null);
            Assert.That(previewCanvas.localScale.x, Is.GreaterThan(0.001f), "Editor preview Canvas must remain visible.");
            Assert.That(previewCanvas.localScale.y, Is.EqualTo(previewCanvas.localScale.x).Within(0.001f));
            Image frame = Find<Image>("Voyage Header Frame");
            Assert.That(header, Is.Not.Null);
            Assert.That(frame.sprite, Is.Not.Null);
            Assert.That(frame.sprite.texture.name, Does.Contain("gray_gull"));
            RectTransform voyageTitle = Find<RectTransform>("Voyage Header Title");
            RectTransform voyageStatus = Find<RectTransform>("Voyage Header Status");
            Assert.That(voyageTitle.anchoredPosition, Is.EqualTo(new Vector2(-710f, 0f)).Within(0.1f));
            Assert.That(voyageTitle.sizeDelta, Is.EqualTo(new Vector2(300f, 60f)).Within(0.1f));
            Assert.That(voyageTitle.GetComponent<Text>().fontSize, Is.EqualTo(GameReadability.FontSize(32)));
            Assert.That(voyageTitle.GetComponent<Text>().resizeTextForBestFit, Is.False);
            Assert.That(voyageStatus.anchoredPosition, Is.EqualTo(new Vector2(660f, 0f)).Within(0.1f));
            Assert.That(voyageStatus.sizeDelta, Is.EqualTo(new Vector2(420f, 60f)).Within(0.1f));

            Image retreat = Find<Button>("Retreat Battle").GetComponent<Image>();
            Image intent = Find<Image>("Enemy Intent HUD");
            Assert.That(intent.sprite, Is.SameAs(retreat.sprite));
            Assert.That(Find<RectTransform>("Player Battle Name").anchoredPosition,
                Is.EqualTo(new Vector2(-620f, 396f)).Within(0.1f));
            Assert.That(Find<RectTransform>("Enemy Battle Name").anchoredPosition,
                Is.EqualTo(new Vector2(620f, 396f)).Within(0.1f));
            Assert.That(intent.rectTransform.anchoredPosition.x, Is.EqualTo(780f).Within(0.1f));
            Assert.That(intent.rectTransform.anchoredPosition.y - intent.rectTransform.rect.height * .5f,
                Is.EqualTo(-82f).Within(0.1f), "The intent grows upward from its reserved action boundary.");
            Assert.That(Find<Image>("Player Battle Name Board").sprite, Is.SameAs(retreat.sprite));
            Assert.That(Find<Image>("Enemy Battle Name Board").sprite, Is.SameAs(retreat.sprite));
            Assert.That(Find<RectTransform>("Retreat Battle").anchoredPosition.y,
                Is.EqualTo(-417f).Within(0.1f));
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Header"), Is.False,
                "Battle previews should not keep the obsolete generic Header.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Ocean Backdrop"), Is.False,
                "Battle previews should not keep the obsolete Ocean Backdrop.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Stage Progress"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Stage Instructions"), Is.False);
        }
        private void CreateBattlePreview(CoreGameplayPreviewPort port = CoreGameplayPreviewPort.GrayGull)
        {
            host = new GameObject("Battle HUD Layout Test");
            host.SetActive(false);
            CoreGameplayDemoController controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, port, 0);
            host.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        private T Find<T>(string name) where T : Component
        {
            return host.GetComponentsInChildren<T>(true).Single(item => item.name == name);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners.Min(point => point.x),
                corners.Min(point => point.y),
                corners.Max(point => point.x),
                corners.Max(point => point.y));
        }

        private static void AssertContains(Rect outer, Rect inner, float tolerance, string message)
        {
            Assert.That(outer.xMin, Is.LessThanOrEqualTo(inner.xMin + tolerance), message + " (left)");
            Assert.That(outer.xMax, Is.GreaterThanOrEqualTo(inner.xMax - tolerance), message + " (right)");
            Assert.That(outer.yMin, Is.LessThanOrEqualTo(inner.yMin + tolerance), message + " (bottom)");
            Assert.That(outer.yMax, Is.GreaterThanOrEqualTo(inner.yMax - tolerance), message + " (top)");
        }
    }
}










