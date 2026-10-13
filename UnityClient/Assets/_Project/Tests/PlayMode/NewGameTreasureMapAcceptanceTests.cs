using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        // This uses the shared profile-preserving fixture. All progression after
        // New Game comes from the existing guided UI driver and ordinary cards.
        // Add -navalVisualCapturePath <new Artifacts directory> for screenshots
        // and matching .ui.json hierarchy snapshots, including the main menu.
        [UnityTest]
        [Timeout(360000)]
        public IEnumerator NewGame_FirstTreasureMap_TraversesRealTutorialAndCapturesScenes()
        {
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.HasSaveData, Is.False, "The preserved profile fixture must start a genuinely fresh game.");
            Assert.That(menu.ContinueButton.interactable, Is.False);
            var capture = new NewGameSceneCapture();
            capture.Capture(null, "MainMenu", "Main Menu Background", "UIMainMenu/UIMainMenu_Home");
            ClickAcceptanceButton("New Game");
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("CoreGameplayDemo"));
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsTutorialActive, Is.True);
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.FirstDeparture));
            yield return MainMenuSceneTests.ClickThroughOpeningComic(controller);
            AssertGuidedDefaultDecks(controller);
            var seen = new HashSet<string>();
            capture.Observe(controller);

            // Flatten nested coroutines only to observe their actual rendered
            // frames. The driver remains responsible for every progression action.
            var routines = new Stack<IEnumerator>();
            routines.Push(DriveGuidedTutorial(controller, "Complete", seen, false));
            while (routines.Count > 0)
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext())
                {
                    (routine as IDisposable)?.Dispose();
                    routines.Pop();
                    continue;
                }
                if (routine.Current is IEnumerator nested)
                {
                    routines.Push(nested);
                    continue;
                }
                yield return routine.Current;
                // This is the normal on-screen skip-reveal action, so the
                // reference comparison sees the full settlement rather than a fade.
                Button reveal = ActiveButton("Skip Settlement Reveal");
                if (controller.Session.Phase == CoreDemoPhase.Settlement && reveal != null && reveal.IsInteractable())
                {
                    reveal.onClick.Invoke();
                    yield return null;
                }
                capture.Observe(controller);
            }

            Assert.That(controller.IsTutorialActive, Is.False);
            Assert.That(controller.CurrentGuidedTutorialStep, Is.EqualTo(GuidedTutorialStep.Complete));
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
            Assert.That(seen, Does.Contain("TutorialBoss"));
            Assert.That(seen, Does.Contain("DisplayTreasure"));
            Assert.That(seen, Does.Contain("EquipRewardComponent"));
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.TutorialCompletedKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.DrawnSeaChartKey), Is.Not.Empty);
            Assert.That(controller.WarehouseCollection.IsDisplayed(CoreWarehouseCollection.TutorialTreasureId), Is.True);
            Assert.That(controller.EquippedComponent(ComponentSlot.Bow).Id,
                Is.EqualTo(CoreGameplayDemoController.GuidedTutorialRewardComponentId));
            AssertGuidedDefaultDecks(controller);

            // Leave the actual equipment dossier and use the unlocked port map
            // entrance. The settlement's Draw Sea Chart button is a different UI.
            if (ActiveButton("Back To Shipyard Gallery") != null)
            {
                ClickAcceptanceButton("Back To Shipyard Gallery");
                yield return null;
                yield return null;
                capture.Capture(controller, "Completed_Shipyard", "Shipyard Panel", "UI_Shipyard");
            }
            ClickAcceptanceButton("Return To Port Hub");
            yield return null;
            yield return null;
            capture.Capture(controller, "Completed_PortHub", "Port Hotspot Treasure", "UI_PortHub");
            Assert.That(GameObject.Find("Treasure Panel"), Is.Null, "The first treasure-table visit must happen through its port entrance.");
            ClickAcceptanceButton("Port Hotspot Treasure");
            yield return null;
            yield return null;

            Assert.That(GameObject.Find("Treasure Panel"), Is.Not.Null);
            int ordinaryRoute = controller.Session.SelectedRouteIndex;
            for (int route = 0; route < 3; route++)
            {
                Button button = RequireGuidedButton("Treasure Route " + route);
                Assert.That(button.IsInteractable(), Is.True);
                RawImage chart = button.GetComponentsInChildren<RawImage>()
                    .Single(item => item.name == "Treasure Route Art " + route);
                Assert.That(chart.texture, Is.Not.Null, "Each treasure puzzle must display its own chart artwork.");
                Assert.That(GameObject.Find("Treasure Route Name " + route).GetComponent<Text>().text,
                    Is.EqualTo(CoreTreasureHuntCatalog.Ranks[route].Name + "藏宝图"));
                Assert.That(GameObject.Find("Treasure Route Progress " + route).GetComponent<Text>().text, Does.Contain("0/4"));
                for (int piece = 0; piece < 4; piece++)
                    Assert.That(GameObject.Find("Treasure Fragment " + route + " " + piece).GetComponent<RawImage>().color.a, Is.Zero);
            }
            Assert.That(RequireGuidedButton("Treasure Identify Fragment").IsInteractable(), Is.EqualTo(controller.Session.CanIdentifyTreasureFragment(out _)));
            AssertTreasurePuzzlePresentation(0);
            capture.Capture(controller, "First_TreasureMap", "Treasure Panel", "UI_TreasureMap");
            foreach (int route in new[] { 1, 2, 0 })
            {
                ClickAcceptanceButton("Treasure Route " + route);
                yield return null;
                yield return null;
                Assert.That(controller.Session.SelectedRouteIndex, Is.EqualTo(ordinaryRoute), "Inspecting a treasure puzzle must preserve ordinary departure choices.");
                Assert.That(controller.Session.TreasureMaps, Is.Empty, "An incomplete puzzle cannot create a seed.");
                AssertTreasurePuzzlePresentation(route);
                capture.Capture(controller, "TreasureMap_SelectedPuzzle_" + route, "Treasure Panel", "UI_TreasureMap");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertTreasurePuzzlePresentation(int rankIndex)
        {
            CoreTreasureMapRank rank = CoreTreasureHuntCatalog.Ranks[rankIndex];
            Text caption = GameObject.Find("Treasure Route Name " + rankIndex).GetComponent<Text>();
            Assert.That(caption.text, Is.EqualTo(rank.Name + "藏宝图"));
            Text progress = GameObject.Find("Treasure Route Progress " + rankIndex).GetComponent<Text>();
            Assert.That(progress.text, Does.Contain("0/4"));
            Assert.That(caption.text, Does.Not.Contain("已绘海图：").And.Not.Contain("近港教学水域").And.Not.Contain("选择展板切换航线"));
            Assert.That(GameObject.Find("Treasure Route Reading Desk"), Is.Null);
            Assert.That(GameObject.Find("Treasure Selected Route"), Is.Null);
            Assert.That(GameObject.Find("Treasure Route Detail"), Is.Null);
            Text status = GameObject.Find("Treasure Workbench Status").GetComponent<Text>();
            Assert.That(status.text, Does.Contain("海盗等级").And.Contain("未鉴定碎片").And.Contain("藏宝种子").And.Not.Contain("月蚀"));
            Assert.That(RequireGuidedButton("Treasure Identify Fragment").GetComponentInChildren<Text>().text, Does.Contain("鉴定碎片"));
            foreach (string name in new[] { "Treasure Route Name " + rankIndex, "Treasure Route Progress " + rankIndex, "Treasure Workbench Status", "Treasure Workbench Notice" })
            {
                Text label = GameObject.Find(name).GetComponent<Text>();
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f),
                    "The remaining workbench information must fit after reflow: " + name);
            }
        }

        private static int AcceptanceReadyTreasureBoard(CoreVoyageSession session) =>
            Enumerable.Range(0, CoreTreasureHuntCatalog.Ranks.Count)
                .Where(index => session.CanCraftTreasureMap(CoreTreasureHuntCatalog.Ranks[index].Id, out _)).DefaultIfEmpty(-1).First();

        private static int AcceptanceIdentifiedFragments(CoreVoyageSession session) =>
            CoreTreasureHuntCatalog.Ranks.Sum(rank => session.GetIdentifiedTreasureFragments(rank.Id));

        private static void ClickAcceptanceButton(string name)
        {
            Button button = RequireGuidedButton(name);
            Assert.That(button.IsInteractable(), Is.True, "The real route requires an enabled control: " + name);
            Canvas.ForceUpdateCanvases();
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform rect = (RectTransform)button.transform;
            if (button.targetGraphic is NavigationCompassSectorGraphic)
                rect = (RectTransform)button.transform.Find("Label");
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, "The visible action must be reachable with the pointer: " + name +
                "; pointer=" + pointer.position + "; screen=" + Screen.width + "x" + Screen.height +
                "; canvas=" + canvas.renderMode + "; rect=" + rect.rect + "; world=" + rect.position +
                "; culled=" + button.targetGraphic.canvasRenderer.cull + "; raycast=" + button.targetGraphic.raycastTarget);
            string hitPath = hits[0].gameObject.name;
            for (Transform parent = hits[0].gameObject.transform.parent; parent != null; parent = parent.parent)
                hitPath = parent.name + "/" + hitPath;
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                "Another graphic intercepts the visible action: " + name + "; hit=" + hitPath);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private sealed class NewGameSceneCapture
        {
            private readonly HashSet<string> captured = new HashSet<string>();
            private int sequence;

            public void Observe(CoreGameplayDemoController controller)
            {
                if (controller.IsBattleAnimationPlaying) return;
                string root = null;
                string reference = null;
                foreach (string[] candidate in new[]
                {
                    new[] { "Shipyard Equipment Page", "UI_ShipyardDetail" },
                    new[] { "Shipyard Detail", "UI_ShipyardDetail" },
                    new[] { "Shipyard Panel", "UI_Shipyard" },
                    new[] { "Warehouse Panel", "UI_Warehouse" },
                    new[] { "Tavern Crew Detail", "UI_TavernCrewDetail" },
                    new[] { "Tavern Crew Stage", "UI_Tavern" },
                    new[] { "Settlement Card", "UI_Settlement" },
                    new[] { "Battle Card", "UI_Battle" },
                    new[] { "Encounter Card", "UI_Encounter" },
                    new[] { "Navigation Chart", "UI_Navigation" },
                    new[] { "Voyage Setup", "UI_VoyageDeparture" },
                    new[] { "Port Hotspot Voyage", "UI_PortHub" },
                })
                {
                    if (GameObject.Find(candidate[0]) == null) continue;
                    root = candidate[0];
                    reference = candidate[1];
                    break;
                }
                if (root == null) return;
                string key = controller.CurrentGuidedTutorialStep + "_" + controller.Session.Phase + "_" + root.Replace(' ', '_');
                if (controller.Session.Phase == CoreDemoPhase.Sailing)
                    key += "_" + controller.CurrentGuidedTutorialNavigationInputStep;
                if (controller.Session.Phase == CoreDemoPhase.Battle && controller.Session.Battle != null)
                    key += "_" + controller.Session.Battle.Stage;
                Capture(controller, key, root, reference);
            }

            public void Capture(CoreGameplayDemoController controller, string key, string rootName, string reference)
            {
                if (!captured.Add(key)) return;
                string file = (sequence++).ToString("00") + "_" + key + ".png";
                NavalBattleVisualCapture.Capture(controller, file, "新游戏实玩 · " + key, rootName);
                string[] args = Environment.GetCommandLineArgs();
                int flag = Array.IndexOf(args, "-navalVisualCapturePath");
                if (flag < 0) return;
                Canvas canvas = GameObject.Find(rootName).GetComponentInParent<Canvas>().rootCanvas;
                Canvas.ForceUpdateCanvases();
                var snapshot = new AcceptanceUiSnapshot
                {
                    previewScene = reference,
                    tutorialStep = controller == null ? string.Empty : controller.CurrentGuidedTutorialStep.ToString(),
                    phase = controller == null ? "MainMenu" : controller.Session.Phase.ToString(),
                };
                foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rect == canvas.transform) continue;
                    var segments = new Stack<string>();
                    for (Transform item = rect; item != canvas.transform; item = item.parent) segments.Push(item.name);
                    Text label = rect.GetComponent<Text>();
                    Image image = rect.GetComponent<Image>();
                    RawImage raw = rect.GetComponent<RawImage>();
                    Graphic graphic = rect.GetComponent<Graphic>();
                    snapshot.objects.Add(new AcceptanceUiObject
                    {
                        path = string.Join("/", segments),
                        name = rect.name,
                        active = rect.gameObject.activeInHierarchy,
                        anchorMin = rect.anchorMin,
                        anchorMax = rect.anchorMax,
                        pivot = rect.pivot,
                        anchoredPosition = rect.anchoredPosition,
                        sizeDelta = rect.sizeDelta,
                        localScale = rect.localScale,
                        fontSize = label == null ? 0 : label.fontSize,
                        text = label == null ? string.Empty : label.text,
                        imageName = image == null || image.sprite == null ? string.Empty : image.sprite.name,
                        textureName = raw == null || raw.texture == null ? string.Empty : raw.texture.name,
                        color = graphic == null ? Color.clear : graphic.color,
                        uvRect = raw == null ? default : raw.uvRect,
                    });
                }
                string path = Path.Combine(Path.GetFullPath(args[flag + 1]), Path.GetFileNameWithoutExtension(file) + ".ui.json");
                File.WriteAllText(path, JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
            }
        }

        [Serializable]
        private sealed class AcceptanceUiSnapshot
        {
            public string previewScene;
            public string tutorialStep;
            public string phase;
            public List<AcceptanceUiObject> objects = new List<AcceptanceUiObject>();
        }

        [Serializable]
        private sealed class AcceptanceUiObject
        {
            public string path;
            public string name;
            public bool active;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector3 localScale;
            public int fontSize;
            public string text;
            public string imageName;
            public string textureName;
            public Color color;
            public Rect uvRect;
        }
    }
}
