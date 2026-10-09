using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Core.Tests
{
    public sealed class PortFacilityHeaderTests
    {
        private static readonly CoreGameplayPreviewScreen[] Screens =
        {
            CoreGameplayPreviewScreen.Warehouse, CoreGameplayPreviewScreen.Shipyard,
            CoreGameplayPreviewScreen.Tavern, CoreGameplayPreviewScreen.ComponentShop,
            CoreGameplayPreviewScreen.CardWorkshop, CoreGameplayPreviewScreen.Dispatch,
            CoreGameplayPreviewScreen.TreasureMap, CoreGameplayPreviewScreen.Maintenance,
            CoreGameplayPreviewScreen.VoyageDeparture, CoreGameplayPreviewScreen.Market,
        };

        private static readonly string[] Titles =
        {
            "船长收藏室", "皇家船坞", "灰鸥酒馆", "部件商店", "卡组工坊",
            "远航派遣台", "藏宝图桌", "港口投资建设", "出航码头", "港口市场",
        };

        private static readonly string[] BackgroundNames =
        {
            "Warehouse Background", "Shipyard Background", "Tavern Background", "ComponentShop Background",
            "CardWorkshop Background", "Dispatch Background", "Treasure Background", "Maintenance Background",
            "Voyage Background", "Market Background",
        };

        private static readonly string[] HeaderOrder =
        {
            "Return To Port Hub", "Previous Port Facility", "Port Facility Title", "Next Port Facility",
            "Port Facility Time", "Port Facility Port", "Port Facility Gold", "Port Facility Settings",
        };

        private static readonly string[] HubHeaderOrder =
        {
            "Port Hub Time", "Port Hub Port", "Port Hub Gold", "Port Hub Settings",
        };

        private static readonly string[] FacilityKeys =
        {
            "Warehouse", "Shipyard", "Tavern", "ComponentShop", "CardWorkshop",
            "Dispatch", "Treasure", "Maintenance", "Voyage", "Market",
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TutorialHeader_LeavesBothInstructionBandsClearAtEveryReadingSize(int level)
        {
            int? previous = GameReadability.OverrideLevel;
            GameReadability.OverrideLevel = level;
            try
            {
                using (var preview = new PreviewFixture())
                foreach (CoreGameplayPreviewScreen screen in new[] { CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewScreen.VoyageDeparture })
                {
                    preview.Configure(screen, CoreGameplayPreviewPort.GrayGull);
                    typeof(CoreGameplayDemoController).GetField("tutorialActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(preview.Controller, true);
                    typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(preview.Controller, null);
                    Canvas.ForceUpdateCanvases();
                    bool hub = screen == CoreGameplayPreviewScreen.PortHub;
                    RectTransform header = preview.Require<RectTransform>(hub ? "Port Hub" : "Port Facility Header");
                    foreach (string name in hub ? HubHeaderOrder : HeaderOrder)
                    {
                        RectTransform control = preview.Require<RectTransform>(name);
                        Rect bounds = BoundsIn(control, header);
                        foreach (string lesson in new[] { "Guided Tutorial Objective", "Guided Tutorial Instruction" })
                            Assert.That(bounds.Overlaps(BoundsIn(preview.Require<RectTransform>(lesson), header)), Is.False, level + " " + name + " / " + lesson);
                        foreach (Text label in control.GetComponentsInChildren<Text>())
                            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), level + " " + name);
                    }
                }
            }
            finally { GameReadability.OverrideLevel = previous; }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void PortHub_HasFourOrderedReadoutsInTheSameRegionalFrame(CoreGameplayPreviewPort port)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.Tavern, port);
                Image facilityFrame = preview.Require<Image>("Port Facility Frame");
                Texture regionalTexture = facilityFrame.sprite.texture;
                Vector4 regionalBorder = facilityFrame.sprite.border;
                float regionalSliceScale = facilityFrame.pixelsPerUnitMultiplier;
                DateTime before = preview.Controller.Session?.GameDateTime ?? ShatteredSail.Core.Gameplay.CoreVoyageSession.GameClockOrigin;
                preview.Configure(CoreGameplayPreviewScreen.PortHub, port);
                DateTime after = preview.Controller.Session.GameDateTime;
                Assert.That(preview.Controller.HasCompleteEditorPreview, Is.True, port.ToString());
                Canvas.ForceUpdateCanvases();

                AssertHubHeader(preview);
                RectTransform header = preview.Require<RectTransform>("Port Hub");
                RectTransform canvas = preview.Canvas.GetComponent<RectTransform>();
                Rect bounds = BoundsIn(header, canvas);
                Assert.That(bounds.width, Is.EqualTo(1920f).Within(0.01f));
                Assert.That(bounds.height, Is.EqualTo(108f).Within(0.01f));
                Assert.That(bounds.xMin, Is.EqualTo(canvas.rect.xMin).Within(0.01f));
                Assert.That(bounds.xMax, Is.EqualTo(canvas.rect.xMax).Within(0.01f));
                Assert.That(bounds.yMax, Is.EqualTo(canvas.rect.yMax).Within(0.01f));
                AssertHubSceneUnmoved(preview, bounds);

                Image frame = preview.Require<Image>("Port Hub Frame");
                Assert.That(frame.transform.parent, Is.SameAs(header));
                Rect frameBounds = BoundsIn(frame.rectTransform, header);
                Assert.That(frameBounds.xMin, Is.EqualTo(header.rect.xMin).Within(0.01f));
                Assert.That(frameBounds.xMax, Is.EqualTo(header.rect.xMax).Within(0.01f));
                Assert.That(frameBounds.yMin, Is.EqualTo(header.rect.yMin).Within(0.01f));
                Assert.That(frameBounds.yMax, Is.EqualTo(header.rect.yMax).Within(0.01f));
                Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(frame.sprite.texture, Is.SameAs(regionalTexture), "The hub must share its port's authored facility art.");
                Assert.That(frame.sprite.border, Is.EqualTo(regionalBorder));
                Assert.That(frame.pixelsPerUnitMultiplier, Is.EqualTo(regionalSliceScale));
                Assert.That(frame.color, Is.EqualTo(Color.white));
                Assert.That(frame.raycastTarget, Is.False);
                Assert.That(header.GetComponent<Image>().color.a, Is.EqualTo(1f), "Seal transparent frame corners above the scene.");
                Assert.That(header.GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(preview.Require<Text>("Port Hub Port").text, Is.EqualTo(preview.Controller.Session.CurrentPort));
                AssertGold(preview, "Port Hub Gold");
                AssertGameMinute(preview.Require<Text>("Port Hub Time").text, before, after);
            }
        }

        [Test]
        public void WideCanvas_PortHubHeaderRemainsAlignedWithItsUnmovedScene()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull);
                RectTransform canvas = preview.Canvas.GetComponent<RectTransform>();
                canvas.sizeDelta = new Vector2(2560f, 1080f);
                Canvas.ForceUpdateCanvases();
                Rect bounds = BoundsIn(preview.Require<RectTransform>("Port Hub"), canvas);
                Assert.That(bounds.width, Is.EqualTo(1920f).Within(0.01f));
                Assert.That(bounds.height, Is.EqualTo(108f).Within(0.01f));
                Assert.That(bounds.xMin, Is.GreaterThan(canvas.rect.xMin));
                Assert.That(bounds.xMax, Is.LessThan(canvas.rect.xMax));
                Assert.That(bounds.yMax, Is.EqualTo(canvas.rect.yMax).Within(0.01f));
                AssertHubSceneUnmoved(preview, bounds);
                AssertHubHeader(preview);
            }
        }

        [Test]
        public void PortHub_LiveReadoutsRefreshGoldAndMinuteWithoutRebuildingScene()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.BronzeBell);
                RectTransform header = preview.Require<RectTransform>("Port Hub");
                RawImage background = preview.Require<RawImage>("Port Hub Background");
                Text time = preview.Require<Text>("Port Hub Time");
                Text gold = preview.Require<Text>("Port Hub Gold");
                preview.Controller.Session.GrantProfileGold(123456, "hub header regression credit");
                TickReadouts(preview.Controller);
                AssertGold(preview, "Port Hub Gold");
                Assert.That(preview.Controller.Session.TrySpendProfileGold(45, "hub header regression debit"), Is.True);
                TickReadouts(preview.Controller);
                AssertGold(preview, "Port Hub Gold");

                FieldInfo cachedMinute = typeof(CoreGameplayDemoController).GetField("displayedPortMinute", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(cachedMinute, Is.Not.Null);
                cachedMinute.SetValue(preview.Controller, -1L);
                time.text = "stale minute";
                DateTime before = preview.Controller.Session?.GameDateTime ?? ShatteredSail.Core.Gameplay.CoreVoyageSession.GameClockOrigin;
                TickReadouts(preview.Controller);
                AssertGameMinute(time.text, before, preview.Controller.Session.GameDateTime);
                Assert.That(preview.Require<RectTransform>("Port Hub"), Is.SameAs(header));
                Assert.That(preview.Require<RawImage>("Port Hub Background"), Is.SameAs(background));
                Assert.That(preview.Require<Text>("Port Hub Gold"), Is.SameAs(gold));
                Assert.That(preview.Require<Text>("Port Hub Time"), Is.SameAs(time));
            }
        }

        [Test]
        public void PortHub_HotspotsEnterAllTenFacilityHeadersAndReturnToFourControls()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.PortHub, CoreGameplayPreviewPort.GrayGull);
                var session = preview.Controller.Session;
                decimal gold = session.ProfileGold;
                Texture regionalTexture = preview.Require<Image>("Port Hub Frame").sprite.texture;
                for (int index = 0; index < FacilityKeys.Length; index += 1)
                {
                    preview.Require<Button>("Port Hotspot " + FacilityKeys[index]).onClick.Invoke();
                    RectTransform facilityHeader = preview.Require<RectTransform>("Port Facility Header");
                    Assert.That(preview.Find("Port Hub"), Is.Null);
                    foreach (string control in HeaderOrder)
                    {
                        Assert.That(preview.All(control).Count(), Is.EqualTo(1));
                        Assert.That(preview.Find(control).parent, Is.SameAs(facilityHeader));
                    }
                    Assert.That(facilityHeader.Cast<Transform>().Count(item => item.GetComponent<Text>() != null || item.GetComponent<Button>() != null),
                        Is.EqualTo(8), "Facility navigation must keep all eight controls.");
                    AssertTitle(preview, index);
                    Assert.That(preview.Require<Image>("Port Facility Frame").sprite.texture, Is.SameAs(regionalTexture));
                    preview.Require<Button>("Return To Port Hub").onClick.Invoke();
                    AssertHubHeader(preview);
                    Assert.That(preview.Require<Image>("Port Hub Frame").sprite.texture, Is.SameAs(regionalTexture));
                    Assert.That(preview.Controller.Session, Is.SameAs(session));
                    Assert.That(session.ProfileGold, Is.EqualTo(gold));
                    Assert.That(session.CurrentPort, Is.EqualTo("灰鸥港"));
                }
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void EveryFacility_HasOneOrderedRegionalHeaderWithoutMovingSceneContent(CoreGameplayPreviewPort port)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                Texture regionalTexture = null;
                for (int index = 0; index < Screens.Length; index += 1)
                {
                    DateTime before = preview.Controller.Session?.GameDateTime ?? ShatteredSail.Core.Gameplay.CoreVoyageSession.GameClockOrigin;
                    preview.Configure(Screens[index], port);
                    DateTime after = preview.Controller.Session.GameDateTime;
                    string context = port + " / " + Screens[index];
                    Assert.That(preview.Controller.HasCompleteEditorPreview, Is.True, context);
                    Canvas.ForceUpdateCanvases();
                    RectTransform header = preview.Require<RectTransform>("Port Facility Header");
                    Assert.That(preview.All("Port Facility Header").Count(), Is.EqualTo(1), context);
                    Assert.That(preview.Find("Facility Profile Status"), Is.Null, "Remove the legacy merged status label: " + context);
                    Assert.That(preview.Find("Header"), Is.Null,
                        "The obsolete generic Header must be removed from every facility preview: " + context);

                    RectTransform content = preview.Require<RectTransform>("Phase Content");
                    Assert.That(content.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize), context);
                    Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero),
                        "Phase Content must use the full 1920 x 1080 reference surface: " + context);

                    Rect? preceding = null;
                    foreach (string objectName in HeaderOrder)
                    {
                        RectTransform control = preview.Require<RectTransform>(objectName);
                        Assert.That(control.parent, Is.SameAs(header), objectName + " must belong to the facility bar.");
                        Rect bounds = BoundsIn(control, header);
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(header.rect.xMin), context + " / " + objectName);
                        Assert.That(bounds.xMax, Is.LessThanOrEqualTo(header.rect.xMax), context + " / " + objectName);
                        if (preceding.HasValue)
                            Assert.That(bounds.xMin, Is.GreaterThan(preceding.Value.xMax),
                                "Header controls must be left-to-right with no overlap: " + context + " / " + objectName);
                        preceding = bounds;
                    }

                    Rect canvasHeader = BoundsIn(header, preview.Canvas.transform);
                    Rect canvasBounds = preview.Canvas.GetComponent<RectTransform>().rect;
                    Assert.That(canvasHeader.xMin, Is.EqualTo(canvasBounds.xMin).Within(0.01f),
                        "The header must reach the left edge of the reference scene: " + context);
                    Assert.That(canvasHeader.xMax, Is.EqualTo(canvasBounds.xMax).Within(0.01f),
                        "The header must reach the right edge of the reference scene: " + context);
                    Assert.That(canvasHeader.yMax, Is.EqualTo(canvasBounds.yMax).Within(0.01f),
                        "The header must reach the top of the 1920 x 1080 reference canvas: " + context);
                    Assert.That(canvasHeader.height, Is.EqualTo(108f).Within(0.01f), context);
                    AssertHeaderMeetsBackground(preview, index, canvasHeader, context);
                    Image frame = preview.Require<Image>("Port Facility Frame");
                    Rect frameBounds = BoundsIn(frame.rectTransform, header);
                    Assert.That(frameBounds.xMin, Is.EqualTo(header.rect.xMin).Within(0.01f), context + " / frame left");
                    Assert.That(frameBounds.xMax, Is.EqualTo(header.rect.xMax).Within(0.01f), context + " / frame right");
                    Assert.That(frameBounds.yMin, Is.EqualTo(header.rect.yMin).Within(0.01f), context + " / frame bottom");
                    Assert.That(frameBounds.yMax, Is.EqualTo(header.rect.yMax).Within(0.01f), context + " / frame top");
                    Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced), context);
                    Assert.That(frame.sprite, Is.Not.Null, context);
                    Assert.That(frame.sprite.texture, Is.Not.Null, context);
                    Assert.That(frame.sprite.border.x + frame.sprite.border.y + frame.sprite.border.z + frame.sprite.border.w,
                        Is.GreaterThan(0f), "The authored corner border must be preserved during ten-facility nine-slice scaling: " + context);
                    Assert.That(frame.color, Is.EqualTo(Color.white), "Use the authored regional art rather than a tint variant: " + context);
                    Assert.That(frame.raycastTarget, Is.False, "Decorative frame must never intercept navigation clicks: " + context);
                    Image backing = header.GetComponent<Image>();
                    Assert.That(backing.color.a, Is.EqualTo(1f),
                        "Transparent authored frame corners need an opaque backing to conceal the old HUD: " + context);
                    Assert.That(backing.raycastTarget, Is.False, context);
                    if (regionalTexture == null) regionalTexture = frame.sprite.texture;
                    else Assert.That(frame.sprite.texture, Is.SameAs(regionalTexture), "One port should keep its frame across all ten facilities.");

                    foreach (string buttonName in new[] { HeaderOrder[0], HeaderOrder[1], HeaderOrder[3], HeaderOrder[7] })
                    {
                        Button button = preview.Require<Button>(buttonName);
                        Assert.That(button.IsInteractable(), Is.True, context + " / " + buttonName);
                        Assert.That(button.targetGraphic, Is.Not.Null, context + " / " + buttonName);
                        Assert.That(button.targetGraphic.raycastTarget, Is.True, context + " / " + buttonName);
                        Assert.That(button.transform.GetSiblingIndex(), Is.GreaterThan(frame.transform.GetSiblingIndex()),
                            "Button targets should be above the decorative frame: " + context + " / " + buttonName);
                        foreach (Text label in button.GetComponentsInChildren<Text>())
                            Assert.That(label.raycastTarget, Is.False, "Button text must not become a separate obstruction.");
                    }
                    foreach (string readout in new[] { HeaderOrder[2], HeaderOrder[4], HeaderOrder[5], HeaderOrder[6] })
                        Assert.That(preview.Require<Text>(readout).raycastTarget, Is.False, context + " / " + readout);

                    AssertButtonText(preview, HeaderOrder[0], "返回港口");
                    AssertNavigationLabels(preview, index);
                    AssertButtonText(preview, HeaderOrder[7], "设置");
                    AssertTitle(preview, index);
                    Assert.That(preview.Require<Text>("Port Facility Port").text, Is.EqualTo(preview.Controller.Session.CurrentPort), context);
                    AssertGold(preview);
                    AssertGameMinute(preview.Require<Text>("Port Facility Time").text, before, after);
                }
            }
        }

        [Test]
        public void WideCanvas_HeaderStaysAlignedWithFixedWidthFacilityScene()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                for (int index = 0; index < Screens.Length; index += 1)
                {
                    preview.Configure(Screens[index], CoreGameplayPreviewPort.GrayGull);
                    RectTransform canvasRect = preview.Canvas.GetComponent<RectTransform>();
                    canvasRect.sizeDelta = new Vector2(2560f, 1080f);
                    Canvas.ForceUpdateCanvases();

                    string context = "Wide canvas / " + Screens[index];
                    Rect header = BoundsIn(preview.Require<RectTransform>("Port Facility Header"), canvasRect);
                    Assert.That(header.width, Is.EqualTo(1920f).Within(0.01f),
                        "A wider viewport must retain the established scene width: " + context);
                    Assert.That(header.xMin, Is.GreaterThan(canvasRect.rect.xMin), context);
                    Assert.That(header.xMax, Is.LessThan(canvasRect.rect.xMax), context);
                    Assert.That(header.yMax, Is.EqualTo(canvasRect.rect.yMax).Within(0.01f), context);
                    AssertHeaderMeetsBackground(preview, index, header, context);
                }
            }
        }

        [Test]
        public void SixPorts_UseSixDistinctFrameTexturesRatherThanRecoloringOneFrame()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                HashSet<Texture> textures = new HashSet<Texture>();
                foreach (CoreGameplayPreviewPort port in Enum.GetValues(typeof(CoreGameplayPreviewPort)))
                {
                    preview.Configure(CoreGameplayPreviewScreen.Tavern, port);
                    Image frame = preview.Require<Image>("Port Facility Frame");
                    Assert.That(frame.sprite, Is.Not.Null, port.ToString());
                    Assert.That(textures.Add(frame.sprite.texture), Is.True, port + " aliases another port's frame texture.");
                }
                Assert.That(textures.Count, Is.EqualTo(6));
            }
        }

        [TestCase(CoreGameplayPreviewPort.BrokenAnchor)]
        [TestCase(CoreGameplayPreviewPort.WhiteShell)]
        [TestCase(CoreGameplayPreviewPort.BronzeBell)]
        [TestCase(CoreGameplayPreviewPort.SaltCrown)]
        [TestCase(CoreGameplayPreviewPort.TwilightTide)]
        [TestCase(CoreGameplayPreviewPort.GrayGull)]
        public void NavigationLabels_MatchAllTenDestinationsInBothDirections_AndReturnRestoresRegionalHubHeader(CoreGameplayPreviewPort port)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.Warehouse, port);
                var session = preview.Controller.Session;
                decimal gold = session.ProfileGold;
                string currentPort = session.CurrentPort;
                AssertNavigationLabels(preview, 0);
                // Start with the boundary transition: warehouse's previous page is the market.
                NavigateToLabel(preview, "Previous Port Facility", Screens.Length - 1);
                NavigateToLabel(preview, "Next Port Facility", 0);
                for (int step = 1; step <= Screens.Length; step += 1)
                {
                    NavigateToLabel(preview, "Next Port Facility", step % Screens.Length);
                }
                for (int step = 1; step <= Screens.Length; step += 1)
                {
                    NavigateToLabel(preview, "Previous Port Facility", (Screens.Length - step) % Screens.Length);
                }
                preview.Require<Button>("Return To Port Hub").onClick.Invoke();
                AssertHubHeader(preview);
                Assert.That(preview.Controller.Session, Is.SameAs(session), "Navigation must not recreate voyage/profile state.");
                Assert.That(session.ProfileGold, Is.EqualTo(gold));
                Assert.That(session.CurrentPort, Is.EqualTo(currentPort));
            }
        }

        [Test]
        public void LiveReadouts_ReflectCreditDebitAndMinuteRefreshWithoutRebuildingPage()
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(CoreGameplayPreviewScreen.Maintenance, CoreGameplayPreviewPort.SaltCrown);
                RectTransform page = preview.Require<RectTransform>("Port Investment Panel");
                Text time = preview.Require<Text>("Port Facility Time");
                Text gold = preview.Require<Text>("Port Facility Gold");
                preview.Controller.Session.GrantProfileGold(123456, "header regression credit");
                TickReadouts(preview.Controller);
                AssertGold(preview);
                Assert.That(preview.Controller.Session.TrySpendProfileGold(45, "header regression debit"), Is.True);
                TickReadouts(preview.Controller);
                AssertGold(preview);

                // Exercise the same minute-change branch without sleeping or altering the OS clock.
                FieldInfo cachedMinute = typeof(CoreGameplayDemoController).GetField("displayedPortMinute", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(cachedMinute, Is.Not.Null);
                cachedMinute.SetValue(preview.Controller, -1L);
                time.text = "stale minute";
                DateTime before = preview.Controller.Session?.GameDateTime ?? ShatteredSail.Core.Gameplay.CoreVoyageSession.GameClockOrigin;
                TickReadouts(preview.Controller);
                AssertGameMinute(time.text, before, preview.Controller.Session.GameDateTime);
                Assert.That(preview.Require<RectTransform>("Port Investment Panel"), Is.SameAs(page));
                Assert.That(preview.Require<Text>("Port Facility Gold"), Is.SameAs(gold));
                Assert.That(preview.Require<Text>("Port Facility Time"), Is.SameAs(time));
            }
        }

        [TestCase(CoreGameplayPreviewScreen.Navigation)]
        public void NonFacilityPages_RetainTheirOriginalHud(CoreGameplayPreviewScreen screen)
        {
            using (PreviewFixture preview = new PreviewFixture())
            {
                preview.Configure(screen, CoreGameplayPreviewPort.BrokenAnchor);
                Assert.That(preview.Controller.HasCompleteEditorPreview, Is.True);
                Assert.That(preview.Find("Header"), Is.Null,
                    "The obsolete generic Header must not be recreated on navigation previews.");
                Assert.That(preview.Require<RectTransform>("Voyage Header"), Is.Not.Null);
                Image frame = preview.Require<Image>("Voyage Header Frame");
                Assert.That(frame.sprite, Is.Not.Null);
                Assert.That(frame.sprite.texture.name, Does.Contain("broken_anchor"));
                Assert.That(preview.Find("Port Facility Header"), Is.Null);
            }
        }

        private static void AssertHubHeader(PreviewFixture preview)
        {
            RectTransform header = preview.Require<RectTransform>("Port Hub");
            Assert.That(preview.All("Port Hub").Count(), Is.EqualTo(1));
            Assert.That(preview.All("Port Hub Frame").Count(), Is.EqualTo(1));
            Assert.That(preview.Find("Header"), Is.Null,
                "The obsolete generic Header must not remain above the regional hub header.");
            foreach (string oldName in new[] { "Port Hub Title", "Port Hub Eyebrow", "Port Hub Status", "Port Facility Header" })
                Assert.That(preview.Find(oldName), Is.Null, "Unexpected legacy or facility UI in the hub: " + oldName);
            foreach (string facilityControl in HeaderOrder)
                Assert.That(preview.Find(facilityControl), Is.Null, "Facility navigation is not part of the four-item hub header.");
            Assert.That(header.Cast<Transform>().Count(item => item.GetComponent<Text>() != null || item.GetComponent<Button>() != null),
                Is.EqualTo(4), "The hub header must contain exactly time, port, gold and settings.");

            Rect? preceding = null;
            foreach (string name in HubHeaderOrder)
            {
                Assert.That(preview.All(name).Count(), Is.EqualTo(1), name);
                RectTransform control = preview.Require<RectTransform>(name);
                Assert.That(control.parent, Is.SameAs(header), name);
                Rect bounds = BoundsIn(control, header);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(header.rect.xMin), name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(header.rect.xMax), name);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(header.rect.yMin), name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(header.rect.yMax), name);
                if (preceding.HasValue)
                    Assert.That(bounds.xMin, Is.GreaterThan(preceding.Value.xMax), "Four hub controls must be ordered left to right without overlap.");
                preceding = bounds;
            }
            foreach (string readout in HubHeaderOrder.Take(3))
                Assert.That(preview.Require<Text>(readout).raycastTarget, Is.False, readout);
            Button settings = preview.Require<Button>("Port Hub Settings");
            Assert.That(settings.IsInteractable(), Is.True);
            Assert.That(settings.targetGraphic.raycastTarget, Is.True);
            Assert.That(settings.transform.GetSiblingIndex(), Is.GreaterThan(preview.Find("Port Hub Frame").GetSiblingIndex()));
            foreach (Text label in settings.GetComponentsInChildren<Text>())
                Assert.That(label.raycastTarget, Is.False);
            AssertButtonText(preview, "Port Hub Settings", "设置");
        }

        private static void AssertHubSceneUnmoved(PreviewFixture preview, Rect header)
        {
            RectTransform content = preview.Require<RectTransform>("Phase Content");
            Assert.That(content.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.ContentSize));
            Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
            RawImage background = preview.Require<RawImage>("Port Hub Background");
            Assert.That(background.rectTransform.sizeDelta, Is.EqualTo(PhaseSurfaceLayout.BackgroundSize));
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(PhaseSurfaceLayout.BackgroundPosition));
            Assert.That(background.uvRect, Is.EqualTo(PhaseSurfaceLayout.BackgroundUv(background.texture)));
            Assert.That(background.texture, Is.Not.Null);
            Rect bounds = BoundsIn(background.rectTransform, preview.Canvas.transform);
            Assert.That(header.xMin, Is.EqualTo(bounds.xMin).Within(0.01f));
            Assert.That(header.xMax, Is.EqualTo(bounds.xMax).Within(0.01f));
            Assert.That(header.yMin, Is.EqualTo(bounds.yMax).Within(0.01f),
                "The header bottom must meet the cropped harbor background top without a seam.");
            Assert.That(bounds.yMin, Is.EqualTo(preview.Canvas.GetComponent<RectTransform>().rect.yMin).Within(0.01f));

            Vector2[] positions =
            {
                new Vector2(-805f, 215f), new Vector2(660f, 115f), new Vector2(-815f, -90f),
                new Vector2(-355f, -200f), new Vector2(-365f, 135f), new Vector2(-30f, -30f),
                new Vector2(220f, 55f), new Vector2(635f, -275f), new Vector2(75f, -250f),
                new Vector2(-560f, -340f),
            };
            Vector2[] sizes =
            {
                new Vector2(220f, 168f), new Vector2(260f, 188f), new Vector2(220f, 158f),
                new Vector2(230f, 170f), new Vector2(240f, 178f), new Vector2(230f, 174f),
                new Vector2(250f, 182f), new Vector2(250f, 176f), new Vector2(260f, 190f),
                new Vector2(260f, 176f),
            };
            for (int index = 0; index < FacilityKeys.Length; index += 1)
            {
                RectTransform hotspot = preview.Require<RectTransform>("Port Hotspot " + FacilityKeys[index]);
                Assert.That(hotspot.parent, Is.SameAs(content));
                Assert.That(hotspot.anchoredPosition, Is.EqualTo(positions[index]), FacilityKeys[index]);
                Assert.That(hotspot.sizeDelta, Is.EqualTo(sizes[index]), FacilityKeys[index]);
            }
        }

        private static void AssertTitle(PreviewFixture preview, int index)
        {
            string expected = PortFacilityArtCatalog.RegionalFacilityTitle(index, preview.Controller.Session.CurrentPort, Titles[index]);
            Assert.That(preview.Require<Text>("Port Facility Title").text, Is.EqualTo(expected));
        }

        private static void AssertNavigationLabels(PreviewFixture preview, int index)
        {
            int previousIndex = (index + Screens.Length - 1) % Screens.Length;
            int nextIndex = (index + 1) % Screens.Length;
            string port = preview.Controller.Session.CurrentPort;
            string previousTitle = PortFacilityArtCatalog.RegionalFacilityTitle(previousIndex, port, Titles[previousIndex]);
            string nextTitle = PortFacilityArtCatalog.RegionalFacilityTitle(nextIndex, port, Titles[nextIndex]);
            AssertButtonText(preview, "Previous Port Facility", "← " + previousTitle);
            AssertButtonText(preview, "Next Port Facility", nextTitle + " →");
        }

        private static void NavigateToLabel(PreviewFixture preview, string buttonName, int destinationIndex)
        {
            Button button = preview.Require<Button>(buttonName);
            string label = button.GetComponentInChildren<Text>().text;
            string destinationTitle = buttonName == "Previous Port Facility"
                ? label.Substring(2)
                : label.Substring(0, label.Length - 2);
            button.onClick.Invoke();
            Assert.That(preview.Require<Text>("Port Facility Title").text, Is.EqualTo(destinationTitle),
                "Navigation must open the facility named on the button: " + preview.Controller.Session.CurrentPort + " / " + buttonName);
            AssertTitle(preview, destinationIndex);
            AssertNavigationLabels(preview, destinationIndex);
        }

        private static void AssertHeaderMeetsBackground(PreviewFixture preview, int index, Rect header, string context)
        {
            RawImage background = preview.Require<RawImage>(BackgroundNames[index]);
            Assert.That(background.rectTransform.rect.size, Is.EqualTo(PhaseSurfaceLayout.BackgroundSize),
                "The room art must fill the 1920 x 972 surface below the compact header: " + context);
            Assert.That(background.rectTransform.anchoredPosition, Is.EqualTo(PhaseSurfaceLayout.BackgroundPosition), context);
            Assert.That(background.uvRect, Is.EqualTo(PhaseSurfaceLayout.BackgroundUv(background.texture)),
                "The room art must use the shared aspect-preserving crop: " + context);
            Rect bounds = BoundsIn(background.rectTransform, preview.Canvas.transform);
            Assert.That(header.xMin, Is.EqualTo(bounds.xMin).Within(0.01f), context + " / scene left");
            Assert.That(header.xMax, Is.EqualTo(bounds.xMax).Within(0.01f), context + " / scene right");
            Assert.That(header.yMin, Is.EqualTo(bounds.yMax).Within(0.01f),
                "The header and room background must meet exactly, leaving no visible seam: " + context);
            Assert.That(bounds.yMin, Is.EqualTo(preview.Canvas.GetComponent<RectTransform>().rect.yMin).Within(0.01f), context + " / scene bottom");
        }

        private static void AssertButtonText(PreviewFixture preview, string name, string expected)
        {
            Assert.That(preview.Require<Button>(name).GetComponentInChildren<Text>().text, Is.EqualTo(expected));
        }

        private static void AssertGold(PreviewFixture preview, string name = "Port Facility Gold")
        {
            string label = Regex.Replace(preview.Require<Text>(name).text, "<[^>]*>", string.Empty);
            Assert.That(label, Does.Contain("总金币"));
            Assert.That(label.Split('\n').Last().Trim(), Is.EqualTo(PortMarketCatalog.FormatPrice(preview.Controller.Session.ProfileGold)),
                "The bar must show total profile gold, not voyage pocket gold or a cached preview constant.");
        }

        private static void AssertGameMinute(string label, DateTime before, DateTime after)
        {
            Match match = Regex.Match(label, @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}");
            Assert.That(match.Success, Is.True, "Show the game calendar as yyyy-MM-dd HH:mm.");
            DateTime parsed = DateTime.ParseExact(match.Value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            long minute = parsed.Ticks / TimeSpan.TicksPerMinute;
            Assert.That(minute, Is.InRange(before.Ticks / TimeSpan.TicksPerMinute, after.Ticks / TimeSpan.TicksPerMinute),
                "Timestamp must use the shared saved game calendar.");
        }

        private static void TickReadouts(CoreGameplayDemoController controller)
        {
            MethodInfo update = typeof(CoreGameplayDemoController).GetMethod("UpdatePortHeaderReadouts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null);
            update.Invoke(controller, new object[] { false });
        }

        private static Rect BoundsIn(RectTransform rect, Transform relativeTo)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = relativeTo.InverseTransformPoint(corners[0]);
            Vector3 max = relativeTo.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private sealed class PreviewFixture : IDisposable
        {
            private readonly Scene scene;
            private readonly GameObject host;
            public CoreGameplayDemoController Controller { get; }
            public Canvas Canvas => host.GetComponentInChildren<Canvas>();

            public PreviewFixture()
            {
                Assert.That(Application.isPlaying, Is.False);
                scene = EditorSceneManager.NewPreviewScene();
                host = new GameObject("Port Header Regression Preview") { hideFlags = HideFlags.HideAndDontSave };
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                Controller = host.AddComponent<CoreGameplayDemoController>();
                host.SetActive(true);
            }

            public void Configure(CoreGameplayPreviewScreen screen, CoreGameplayPreviewPort port)
            {
                Controller.ConfigureEditorPreview(screen, port, 0);
                Canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRect = Canvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);
                canvasRect.localScale = Vector3.one;
            }

            public IEnumerable<Transform> All(string name) => host.GetComponentsInChildren<Transform>(true).Where(t => t.name == name);
            public Transform Find(string name) => All(name).FirstOrDefault();
            public T Require<T>(string name) where T : Component
            {
                Transform item = Find(name);
                Assert.That(item, Is.Not.Null, "Missing UI object: " + name);
                T component = item.GetComponent<T>();
                Assert.That(component, Is.Not.Null, name + " is missing " + typeof(T).Name);
                return component;
            }

            public void Dispose()
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}







