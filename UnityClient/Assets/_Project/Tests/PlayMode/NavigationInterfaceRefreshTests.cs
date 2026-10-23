using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavigationInterfaceRefresh_RepeatedMapClicksKeepCurrentDashboardSkin()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            AssertNavigationInterfaceRefreshSkin("before map clicks");

            foreach (VoyagePoint target in new[] { new VoyagePoint(35f, 34f), new VoyagePoint(26f, 36f) })
            {
                GameObject oldChart = GameObject.Find("Navigation Chart");
                Vector2 seaSize = GameObject.Find("Ocean Tile Layer").GetComponent<RectTransform>().rect.size;
                Vector2 local = new Vector2((target.X / session.Map.WorldWidth - .5f) * seaSize.x,
                    (target.Y / session.Map.WorldHeight - .5f) * seaSize.y);
                ClickNavigationInterfaceRefreshGraphic(GameObject.Find("Map Course Input"), local);

                Assert.That(session.HasDirectCourseTarget, Is.True);
                Assert.That(session.DirectCourseTarget.X, Is.EqualTo(target.X).Within(.001f));
                Assert.That(session.DirectCourseTarget.Y, Is.EqualTo(target.Y).Within(.001f));
                Assert.That(GameObject.Find("Navigation Chart"), Is.Not.SameAs(oldChart),
                    "The pointer must exercise the production Render path, not only edit the course model.");
                // Do not yield or force another layout before inspecting the replacement:
                // its inactive predecessor still exists until Unity ends this frame.
                AssertNavigationInterfaceRefreshSkin("same frame after map click");
                yield return null;
                AssertNavigationInterfaceRefreshSkin("next frame after map click");
            }

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.TotalDistanceNm, Is.Zero, "Plotting a course must not execute it.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavigationInterfaceRefresh_CompassPointerClickKeepsSkinAndChangesHeading()
        {
            yield return LoadNavigationInterfaceRefreshFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetCourseTo(new VoyagePoint(35f, 34f));
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null;
            Assert.That(session.HasDirectCourseTarget, Is.True);
            AssertNavigationInterfaceRefreshSkin("before compass click");

            GameObject chart = GameObject.Find("Navigation Chart"), controls = GameObject.Find("Navigation Controls"),
                map = GameObject.Find("Map Layer"), ocean = GameObject.Find("Lookout Ocean");
            int water = session.Cargo.Water, food = session.Cargo.Food, requested = session.RequestedDistanceNm;
            double elapsed = session.ElapsedGameHours;

            Button south = ActiveButton("Heading South");
            Assert.That(south, Is.Not.Null);
            Assert.That(south.IsInteractable(), Is.True);
            RectTransform bounds = south.GetComponent<RectTransform>();
            ClickNavigationInterfaceRefreshGraphic(south.gameObject, Vector2.down * (bounds.rect.width * .335f));

            Assert.That(session.Heading, Is.EqualTo(SailingDirection.South));
            Assert.That(session.HasDirectCourseTarget, Is.False);
            Assert.That(GameObject.Find("Navigation Chart"), Is.SameAs(chart));
            Assert.That(GameObject.Find("Navigation Controls"), Is.SameAs(controls));
            Assert.That(GameObject.Find("Map Layer"), Is.SameAs(map));
            Assert.That(GameObject.Find("Lookout Ocean"), Is.SameAs(ocean));
            Assert.That(GameObject.Find("Direct Course Target Halo"), Is.Null);
            Assert.That(GameObject.Find("Distance Label").GetComponent<Text>().text, Does.Not.Contain("锁定"));
            Assert.That(GameObject.Find("Forecast").GetComponent<Text>().text, Does.Not.Contain("取消锁定"));
            Assert.That(GameObject.Find("Distance Slider").GetComponent<Slider>().maxValue,
                Is.EqualTo(Mathf.Max(session.MaximumDistanceNm, requested)));
            Assert.That(GameObject.Find("Lookout Heading").GetComponent<Text>().text,
                Does.Contain(session.DirectionLabel(SailingDirection.South)));
            Assert.That(session.RequestedDistanceNm, Is.EqualTo(requested));
            Assert.That(session.Cargo.Water, Is.EqualTo(water));
            Assert.That(session.Cargo.Food, Is.EqualTo(food));
            Assert.That(session.ElapsedGameHours, Is.EqualTo(elapsed));
            AssertNavigationInterfaceRefreshSkin("same frame after compass click");
            yield return null;
            AssertNavigationInterfaceRefreshSkin("next frame after compass click");
            Assert.That(ActiveButton("Heading South").GetComponent<WoodButtonRoleState>().IsSelected, Is.True);
            Assert.That(ActiveButton("Heading East").GetComponent<WoodButtonRoleState>().IsSelected, Is.False);
            Assert.That(session.TotalDistanceNm, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator LoadNavigationInterfaceRefreshFixture()
        {
            // Reuse the shared fixture's save preservation/restoration and the
            // isolated validation project's persistence identity.
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = controller.Session;
            session.SetSeed("NAVIGATION-INTERFACE-REFRESH");
            session.Cargo.Water = session.Cargo.Food = 200;
            session.StartVoyage();
            session.Map.Nodes.Clear();
            Array.Clear(session.Map.TerrainCells, 0, session.Map.CellCount);
            typeof(CoreVoyageSession).GetProperty("Position").SetValue(session, new VoyagePoint(30f, 32f));
            session.SetHeading(SailingDirection.East);
            session.SetDistance(120);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            yield return null;
            yield return null;
        }

        private static void ClickNavigationInterfaceRefreshGraphic(GameObject target, Vector2 localOffset)
        {
            Assert.That(target, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = target.GetComponent<RectTransform>();
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center + localOffset)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, target.name + " must accept pointer input.");
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(target), target.name + " was blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void AssertNavigationInterfaceRefreshSkin(string context)
        {
            Image chart = NavigationInterfaceRefreshImage("Navigation Chart", context);
            Image mapBacking = NavigationInterfaceRefreshImage("Procedural Sea Chart", context);
            foreach (Image surface in new[] { chart, mapBacking })
            {
                Assert.That(surface.sprite, Is.Null, context + ": " + surface.name + " must remain a solid sea surface.");
                Assert.That(surface.type, Is.EqualTo(Image.Type.Simple), context);
                Assert.That(surface.color.r, Is.EqualTo(6f / 255f).Within(.0001f), context + ": sea red");
                Assert.That(surface.color.g, Is.EqualTo(18f / 255f).Within(.0001f), context + ": sea green");
                Assert.That(surface.color.b, Is.EqualTo(24f / 255f).Within(.0001f), context + ": sea blue");
                Assert.That(surface.color.a, Is.EqualTo(1f), context + ": the sea backing must remain opaque.");
            }

            Image strip = NavigationInterfaceRefreshImage(NavigationInterfaceSkin.TitleStripName, context);
            Assert.That(strip.sprite, Is.SameAs(NauticalSurfaceSkin.Paper), context + ": map title strip");
            Assert.That(strip.rectTransform.rect.height, Is.InRange(40f, 80f), context + ": compact title strip");
            Image controls = NavigationInterfaceRefreshImage("Navigation Controls", context);
            Assert.That(controls.sprite, Is.SameAs(NauticalSurfaceSkin.Paper), context + ": controls paper");
            Assert.That(controls.type, Is.EqualTo(Image.Type.Tiled), context + ": controls must not revert to the old framed parchment.");

            Texture2D frameTexture = Resources.Load<Texture2D>(NavigationInterfaceSkin.PanelFrameResourcePath);
            Assert.That(frameTexture, Is.Not.Null, context);
            foreach (string panelName in new[] { "Navigation Chart", "Navigation Controls", "Forward View Panel" })
            {
                Image frame = NavigationInterfaceRefreshImage(panelName + " Timber Frame", context);
                Assert.That(frame.sprite, Is.Not.Null, context + ": " + panelName + " frame");
                Assert.That(frame.sprite.texture, Is.SameAs(frameTexture), context + ": new fine panel frame");
                Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced), context);
                Assert.That(frame.fillCenter, Is.False, context);
                Assert.That(frame.raycastTarget, Is.False, context);
            }
            CoreVoyageMap map = Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.Map;
            Vector2 seaSize = new Vector2(map.Columns, map.Rows) * (1160f / 48f);
            Assert.That(GameObject.Find("Ocean Tile Layer").GetComponent<RectTransform>().rect.size,
                Is.EqualTo(seaSize), context + ": chart cells must remain square.");
            Assert.That(GameObject.Find("Map Layer").GetComponent<RectTransform>().sizeDelta,
                Is.EqualTo(seaSize + new Vector2(40f, 45f)), context + ": retain chart margins around the square grid.");
        }

        private static Image NavigationInterfaceRefreshImage(string name, string context)
        {
            // GameObject.Find excludes the inactive objects awaiting destruction.
            GameObject active = GameObject.Find(name);
            Assert.That(active, Is.Not.Null, context + ": missing active " + name);
            Image image = active.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, context + ": missing Image on " + name);
            return image;
        }
    }
}
