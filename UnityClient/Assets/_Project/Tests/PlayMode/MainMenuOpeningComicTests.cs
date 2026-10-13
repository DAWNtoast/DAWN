using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest]
        public IEnumerator NewGame_OpeningComicAutoAndClickFinishOnceAndRestorePortHubGuide()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            ActiveButton("New Game").onClick.Invoke();
            yield return null;
            yield return null;

            var game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            AssertOpeningComicPage(game, 0);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False,
                "Submit and movement cannot reach the tutorial underneath the opening.");
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            Button voyageHotspot = ActiveButton("Port Hotspot Voyage");
            Assert.That(voyageHotspot, Is.Not.Null);
            Assert.That(voyageHotspot.IsInteractable(), Is.False);
            double beforeHours = game.Session.ElapsedGameHours;
            decimal beforeGold = game.Session.ProfileGold;
            Button.ButtonClickedEvent retiredAdvance = ActiveButton("Opening Comic Advance").onClick;
            OpeningTimer().RestoreRemainingSeconds(.08f);
            yield return WaitForOpeningPage(game, 1);
            AssertOpeningComicPage(game, 1);
            Assert.That(OpeningTimer().RemainingSeconds, Is.GreaterThan(5f));

            // Expiry and repeated pointer events in the same frame must still
            // expose the next image for its full reading interval.
            OpeningTimer().RestoreRemainingSeconds(0f);
            ClickOpeningComic(new Vector2(.99f, .99f));
            ClickOpeningComic(new Vector2(.01f, .01f));
            AssertOpeningComicPage(game, 2);
            yield return null;
            AssertOpeningComicPage(game, 2);
            Assert.That(OpeningTimer().RemainingSeconds, Is.GreaterThan(5f));
            OpeningTimer().RestoreRemainingSeconds(.06f);
            yield return WaitForOpeningPage(game, 3);
            AssertOpeningComicPage(game, 3);
            OpeningTimer().RestoreRemainingSeconds(.06f);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (game.IsOpeningComicPlaying && Time.realtimeSinceStartup < deadline) yield return null;

            AssertOpeningFinished(game);
            Assert.That(ActiveButton("Port Hotspot Voyage").IsInteractable(), Is.True);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
            Assert.That(game.Session.ElapsedGameHours, Is.EqualTo(beforeHours));
            Assert.That(game.Session.ProfileGold, Is.EqualTo(beforeGold));
            retiredAdvance.Invoke();
            AssertOpeningFinished(game);
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, null);
            AssertOpeningFinished(game);

            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            game = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(game.IsOpeningComicPlaying, Is.False,
                "An ordinary scene reload cannot replay the consumed new-game opening.");
            AssertOpeningFinished(game);
            ActiveButton("Port Hotspot Voyage").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Voyage Setup"), Is.Not.Null);
            Assert.That(ActiveButton("Start Voyage").IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        internal static IEnumerator ClickThroughOpeningComic(CoreGameplayDemoController game)
        {
            Assert.That(OpeningComicCatalog.ConsumePlaybackRequest(), Is.False,
                "The new-game request is consumed as the scene opens.");
            Vector2[] corners = { new Vector2(.01f, .01f), new Vector2(.99f, .01f),
                new Vector2(.01f, .99f), new Vector2(.99f, .99f) };
            for (int page = 0; page < 4; page++)
            {
                AssertOpeningComicPage(game, page);
                Assert.That(OpeningTimer().RemainingSeconds, Is.InRange(4f, 6f));
                ClickOpeningComic(corners[page]);
                yield return null;
            }
            AssertOpeningFinished(game);
        }

        private static void AssertOpeningComicPage(CoreGameplayDemoController game, int index)
        {
            Assert.That(game, Is.Not.Null);
            Assert.That(game.IsOpeningComicPlaying, Is.True);
            Assert.That(OpeningPageIndex(game), Is.EqualTo(index));
            GameObject overlay = GameObject.Find("Opening Comic Overlay");
            Assert.That(overlay, Is.Not.Null);
            RawImage page = GameObject.Find("Opening Comic Page").GetComponent<RawImage>();
            Text narration = GameObject.Find("Opening Comic Narration").GetComponent<Text>();
            Assert.That(page.texture, Is.SameAs(Resources.Load<Texture2D>("OpeningComics/opening_page0" + (index + 1))));
            Assert.That(page.texture, Is.Not.Null, "Every opening page must ship as a real Resources asset.");
            Assert.That(page.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(narration.text, Is.EqualTo(OpeningComicCatalog.Pages[index].Narration));
            Assert.That(string.IsNullOrWhiteSpace(narration.text), Is.False);
            Assert.That(narration.font, Is.SameAs(GameTypography.InterfaceFont));
            Assert.That(narration.raycastTarget, Is.False);
            Assert.That(overlay.GetComponentsInChildren<Text>(true), Has.Length.EqualTo(1),
                "The only text is narration within the artwork's authored box.");
            Assert.That(overlay.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in new[] { overlay.GetComponent<RectTransform>(), page.rectTransform,
                ActiveButton("Opening Comic Advance").GetComponent<RectTransform>() })
            {
                Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector2 minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                FixedGameViewport.Bounds(canvas).GetWorldCorners(corners);
                Vector2 viewportMinimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 viewportMaximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Assert.That(minimum.x, Is.EqualTo(viewportMinimum.x).Within(1f), rect.name);
                Assert.That(minimum.y, Is.EqualTo(viewportMinimum.y).Within(1f), rect.name);
                Assert.That(maximum.x, Is.EqualTo(viewportMaximum.x).Within(1f), rect.name);
                Assert.That(maximum.y, Is.EqualTo(viewportMaximum.y).Within(1f), rect.name);
            }
        }

        private static void AssertOpeningFinished(CoreGameplayDemoController game)
        {
            Assert.That(game.IsOpeningComicPlaying, Is.False);
            Assert.That(GameObject.Find("Opening Comic Overlay"), Is.Null);
            Assert.That(game.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(game.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstDeparture));
            Assert.That(GameObject.Find("Port Hub"), Is.Not.Null);
            Assert.That(GameObject.Find("Voyage Setup"), Is.Null);
            Assert.That(ActiveButton("Port Hotspot Voyage").IsInteractable(), Is.True);
            GuidedTutorialFocusGraphic focus = Object.FindObjectsByType<GuidedTutorialFocusGraphic>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(graphic =>
                    graphic.Target != null && graphic.Target.name == "Port Hotspot Voyage");
            Assert.That(focus, Is.Not.Null, "Completing the comic restores the dock-entry guide on the harbor.");
            Assert.That(focus.HasVisibleTarget, Is.True);
            Assert.That(focus.raycastTarget, Is.False);
        }

        private static ComicAutoAdvance OpeningTimer() =>
            GameObject.Find("Opening Comic Overlay").GetComponent<ComicAutoAdvance>();

        private static int OpeningPageIndex(CoreGameplayDemoController game) => (int)
            typeof(CoreGameplayDemoController).GetField("openingComicPageIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(game);

        private static IEnumerator WaitForOpeningPage(CoreGameplayDemoController game, int expected)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (OpeningPageIndex(game) != expected && game.IsOpeningComicPlaying && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(OpeningPageIndex(game), Is.EqualTo(expected), "The opening advances automatically through real frames.");
        }

        private static void ClickOpeningComic(Vector2 normalized)
        {
            Button advance = ActiveButton("Opening Comic Advance");
            Assert.That(advance, Is.Not.Null);
            Assert.That(advance.IsInteractable(), Is.True);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = advance.GetComponentInParent<Canvas>().rootCanvas;
            RectTransform viewport = FixedGameViewport.Bounds(canvas);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 localPoint = viewport.rect.min + Vector2.Scale(viewport.rect.size, normalized);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(localPoint)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(advance),
                "The entire game viewport must advance the opening while protecting the tutorial controls.");
            ExecuteEvents.Execute(advance.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
