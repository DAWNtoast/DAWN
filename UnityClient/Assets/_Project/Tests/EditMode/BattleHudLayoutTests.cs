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
            RectTransform art = Find<RectTransform>("Shared tactical sea");
            RectTransform board = Find<RectTransform>("Naval Tactical Board");
            Assert.That(board.GetComponent<RectMask2D>(), Is.Not.Null, "The rotating sea is clipped to the battlefield.");
            RectTransform battleCard = Find<RectTransform>("Battle Card");
            RectTransform overlay = Find<RectTransform>("Battle Overlay");

            Rect artBounds = WorldRect(art);
            AssertContains(artBounds, WorldRect(battleCard), 1.5f,
                "The scene art must cover the full battle card after the authored scale is applied.");
            AssertContains(artBounds, WorldRect(board), 1.5f, "The sea also covers the full tactical grid.");
            Assert.That(board.sizeDelta.x, Is.EqualTo(1860f).Within(0.1f));
            Assert.That(board.sizeDelta.y, Is.EqualTo(900f).Within(0.1f));
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
            RawImage backdrop = Find<RawImage>("Shared tactical sea");
            Assert.That(backdrop.texture, Is.SameAs(NavalTacticalArtCatalog.ForSea(host.GetComponent<CoreGameplayDemoController>().Session.BattleTerrain)));
            Assert.That(backdrop.texture, Is.Not.Null);
            Assert.That(backdrop.uvRect.width, Is.GreaterThan(0f));
            Assert.That(backdrop.uvRect.width, Is.EqualTo(backdrop.uvRect.height));
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
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            if (!boarding)
                foreach (string name in new[] { "Battle Range", "Battle Alignment" })
                    Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False, name);
            string[] readoutNames = boarding
                ? new[] { "Battle Range", "Battle Alignment", "Voyage Header Status" }
                : new[] { "Voyage Header Status" };
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
                Image paper = Find<Image>("Guided Tutorial Banner");
                Rect paperBounds = WorldRect(paper.rectTransform);
                Assert.That(paper.transform.parent, Is.SameAs(header.parent), "The lesson owns an independent reading surface.");
                Assert.That(NauticalSurfaceSkin.IsPaperSurface(paper.sprite), Is.True);
                Assert.That(paper.sprite, Is.SameAs(MarketShopSkin.Paper));
                Assert.That(paper.color.a, Is.EqualTo(1f), "The battle artwork must not bleed through the reading area.");
                Assert.That(paper.raycastTarget, Is.False);
                AssertContains(WorldRect((RectTransform)header.parent), paperBounds, 0.1f, "The full paper must stay on screen.");
                // This disposable EditMode hierarchy has not been rendered, so
                // CanvasRenderer.absoluteDepth is -1. These peers share a canvas.
                Assert.That(paper.transform.GetSiblingIndex(), Is.GreaterThan(header.GetSiblingIndex()),
                    "Raising the regional header must not cover the paper.");
                Assert.That(paper.transform.GetSiblingIndex(), Is.GreaterThan(readouts.GetSiblingIndex()),
                    "Teaching paper floats above the ordinary battle information.");
                Rect? precedingBand = null;
                foreach (string name in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                {
                    Text label = Find<Text>(name);
                    Assert.That(label.isActiveAndEnabled, Is.True, name);
                    Assert.That(label.text, Is.Not.Empty, name);
                    Rect band = WorldRect(label.rectTransform);
                    AssertContains(paperBounds, band, 0.1f, name + " must stay on its paper");
                    Assert.That(label.resizeTextForBestFit, Is.False, name + " must keep its enlarged font.");
                    Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(24), name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                        name + " must show the complete lesson without truncation.");
                    Assert.That(label.raycastTarget, Is.False);
                    if (precedingBand.HasValue)
                        Assert.That(band.Overlaps(precedingBand.Value), Is.False, "Teaching objective and instruction must remain separate.");
                    precedingBand = band;
                }
            }
            else
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Guided Tutorial Banner"), Is.False);
        }

        [Test]
        public void BattleResources_UseThreeIconRowsWithoutTheRetiredTextPanels()
        {
            CreateBattlePreview();
            Image retreat = Find<Button>("Retreat Battle").GetComponent<Image>();
            Assert.That(retreat.sprite, Is.Not.Null, "Retreat Battle needs its wood board face.");
            RectTransform resources = Find<RectTransform>("Battle Compact Resources");
            for (int line = 1; line <= 3; line++)
            {
                RectTransform row = Find<RectTransform>("Battle Resource Line " + line);
                Assert.That(row.parent, Is.SameAs(resources));
                Assert.That(row.rect.width, Is.EqualTo(resources.rect.width).Within(.1f));
                Assert.That(row.GetComponentsInChildren<BattleCommandIconGraphic>(false).Length, Is.EqualTo(line == 1 ? 2 : 4));
            }
            foreach (string name in new[] { "Player Resource HUD", "Enemy Resource HUD", "Enemy Intent HUD", "Battle School Status Panel" })
                Assert.That(Find<RectTransform>(name).gameObject.activeInHierarchy, Is.False, name + " was replaced by compact icons.");
            Assert.That(host.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "Battle Buff Panel" && item.gameObject.activeInHierarchy), Is.False,
                "Combat effects now belong to the icon rows under the health bars.");
            Assert.That(host.GetComponentsInChildren<BattleStatusRow>(true), Has.Length.EqualTo(2),
                "Both combatants need a retained status row.");
        }

        [Test]
        public void BattleSidePanels_KeepDecisionColumnSeparateFromTheHand()
        {
            CreateBattlePreview();
            Rect hand = WorldRect(Find<RectTransform>("Battle Hand"));
            RectTransform player = Find<RectTransform>("Battle Compact Resources");
            RectTransform enemy = Find<RectTransform>("Battle Discard Pile");
            RectTransform retreat = Find<RectTransform>("Retreat Battle");
            foreach (RectTransform panel in new[] { player, enemy, retreat })
                Assert.That(WorldRect(panel).Overlaps(hand), Is.False, panel.name + " must not cover the hand.");
            Assert.That(WorldRect(player).yMin, Is.GreaterThan(WorldRect(Find<RectTransform>("Battle Draw Pile")).yMax),
                "The three resource rows remain above the clickable draw pile.");
            foreach (string side in new[] { "Player", "Enemy" })
            {
                BattleStatusRow row = Find<BattleStatusRow>(side + " Battle Status Row");
                PopulateStatusRow(row, side == "Player");
                row.FollowHealthBar(Find<RectTransform>(side + " Battle Hull Bar Track"));
                row.RefreshVisibility();
                Assert.That(WorldRect((RectTransform)row.transform).Overlaps(hand), Is.False,
                    row.name + " must keep the hand readable and clickable.");
            }
        }

        [Test]
        public void BattleHullBars_FollowTheirShipsWithinTheStatusLanesAndCenterTheirHealthValues()
        {
            CreateBattlePreview();
            foreach (string side in new[] { "Player", "Enemy" })
            {
                RectTransform shipRoot = Find<RectTransform>(side + " Ship Art");
                Rect ship = WorldRect(shipRoot);
                Image hull = shipRoot.GetComponent<Image>();
                Assert.That(hull, Is.Not.Null, side + " uses the top-down tactical ship.");
                CoreVoyageSession artSession = host.GetComponent<CoreGameplayDemoController>().Session;
                Assert.That(hull.sprite, Is.SameAs(side == "Player" ? NavalTacticalArtCatalog.ForPlayer(artSession.SelectedShip.Id) :
                    NavalTacticalArtCatalog.ForEnemy(artSession.Battle, artSession.ActiveEncounter, artSession.SelectedRouteIndex)));
                Assert.That(hull.sprite, Is.Not.Null);
                float hullContactY = ship.yMin;
                RectTransform barRoot = Find<RectTransform>(side + " Battle Hull Bar Track");
                Rect bar = WorldRect(barRoot);
                Vector3 localShipCenter = barRoot.parent.InverseTransformPoint(shipRoot.position);
                float expectedX = side == "Player" ? Mathf.Clamp(localShipCenter.x, -430f, -185f) :
                    Mathf.Clamp(localShipCenter.x, 185f, 430f);
                Assert.That(barRoot.localPosition.x, Is.EqualTo(expectedX).Within(3f),
                    side + " health follows the ship while keeping both status lanes apart.");
                Assert.That(barRoot.rect.width, Is.EqualTo(310f).Within(.1f), "The naval track matches the borderless health fill.");
                BattleDefenseHud hud = barRoot.GetComponent<BattleDefenseHud>();
                Assert.That(hud, Is.Not.Null);
                Assert.That(hud.HullBar.rectTransform.rect.width, Is.EqualTo(310f).Within(.1f));
                Assert.That(bar.center.y, Is.LessThan(ship.center.y), side + " hull bar should be below the ship.");
                Assert.That(bar.yMax, Is.LessThanOrEqualTo(hullContactY), side + " hull bar must not cover the hull artwork.");
                Assert.That(bar.yMin, Is.GreaterThan(hullContactY - 85f), side + " hull bar should remain close to the hull.");
                Text numeric = Find<Text>(side + " Hull Numeric");
                Assert.That(numeric.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
                Assert.That(WorldRect(numeric.rectTransform).center, Is.EqualTo(bar.center).Within(.1f),
                    "Current / maximum health stays centered on the continuous bar.");
                BattleStatusRow status = Find<BattleStatusRow>(side + " Battle Status Row");
                PopulateStatusRow(status, side == "Player");
                status.FollowHealthBar(barRoot);
                Rect statusBounds = WorldRect((RectTransform)status.transform);
                Assert.That(statusBounds.yMax, Is.LessThan(bar.yMin), "Status icons belong below health.");
                Assert.That(statusBounds.center.x, Is.EqualTo(bar.center.x).Within(.1f));
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
            Assert.That(voyageTitle.anchoredPosition, Is.EqualTo(new Vector2(-734f, 0f)).Within(0.1f));
            Assert.That(voyageTitle.sizeDelta, Is.EqualTo(new Vector2(234.8733f, 60f)).Within(0.1f));
            Assert.That(voyageStatus.anchoredPosition, Is.EqualTo(new Vector2(600f, 0f)).Within(0.1f));
            Assert.That(voyageStatus.sizeDelta, Is.EqualTo(new Vector2(316f, 60f)).Within(0.1f));

            Image retreat = Find<Button>("Retreat Battle").GetComponent<Image>();
            RectTransform intent = Find<RectTransform>("Enemy Intent Icons");
            typeof(BattleIntentAnchor).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(intent.GetComponent<BattleIntentAnchor>(), null);
            Assert.That(intent.GetComponentsInChildren<BattleCommandIconGraphic>(false), Is.Not.Empty);
            Assert.That(WorldRect(intent).yMin, Is.GreaterThan(WorldRect(Find<RectTransform>("Enemy Ship Art")).yMax),
                "The compact enemy intents remain above the enemy ship.");
            Assert.That(Find<RectTransform>("Player Battle Name").anchoredPosition.x, Is.EqualTo(-735f).Within(.1f));
            Assert.That(Find<RectTransform>("Enemy Battle Name").anchoredPosition.x, Is.EqualTo(735f).Within(.1f));
            Assert.That(Find<Image>("Player Battle Name Board").sprite, Is.SameAs(retreat.sprite));
            Assert.That(Find<Image>("Enemy Battle Name Board").sprite, Is.SameAs(retreat.sprite));
            Assert.That(Find<RectTransform>("Retreat Battle").anchoredPosition.y,
                Is.GreaterThan(0f), "Retreat stays above the lower resource and deck controls.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Header"), Is.False,
                "Battle previews should not keep the obsolete generic Header.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Ocean Backdrop"), Is.False,
                "Battle previews should not keep the obsolete Ocean Backdrop.");
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Stage Progress"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Stage Instructions"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Cost Rule Panel"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Card Cost Rule"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Status"), Is.False);
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Battle Collision Quality"), Is.False);
        }
        private void PopulateStatusRow(BattleStatusRow row, bool player)
        {
            var statuses = CoreBattleStatusPresentation.Build(host.GetComponent<CoreGameplayDemoController>().Session, player).ToList();
            if (!statuses.Any(status => status.Id == "guard"))
                statuses.Add(new CoreBattleStatus("guard", "格挡", "12", "吸收伤害。", CoreBattleStatusKind.Guard));
            row.SetStatuses(statuses);
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










