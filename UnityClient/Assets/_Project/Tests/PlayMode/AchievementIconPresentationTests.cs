using ShatteredSail.Core.Gameplay;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class MainMenuSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator AchievementIcons_RealMenuPagesReachEveryBadgeWithoutChangingProgress()
        {
            // The shared fixture preserves and restores all achievement keys.
            // One recorded milestone makes both visual states visible together.
            AchievementCatalog.RecordDeckEdited();
            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null; yield return null;
            var controller = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item => item.name == "Achievements Panel"), Is.False);
            Assert.That(controller.GetComponentsInChildren<Transform>(true).Any(item => item.name.StartsWith("Achievement Row")), Is.False);
            Assert.That(controller.GetComponentsInChildren<RawImage>(true).Any(item => item.name.StartsWith("Achievement Icon")), Is.False);
            var progress = AchievementCatalog.PersistenceKeys.ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null);
            var displayedStates = AchievementCatalog.All.ToDictionary(item => item.Id, AchievementCatalog.IsUnlocked);
            yield return ClickAchievementIconControl("Achievements");
            AssertAchievementProgressUnchanged(progress);
            var visited = new HashSet<string>();
            RawImage[] allIcons = controller.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Achievement Icon ")).ToArray();
            Assert.That(allIcons.Length, Is.EqualTo(8));
            Transform[] builtObjects = controller.GetComponentsInChildren<Transform>(true);
            Assert.That(GameObject.Find("Achievement Icon tutorial_deck").GetComponent<RawImage>().color, Is.EqualTo(Color.white));
            Assert.That(GameObject.Find("Achievement Row tutorial_deck").transform.Find("State").GetComponent<Text>().text, Is.EqualTo("已解锁"));
            Assert.That(GameObject.Find("Achievement Icon tutorial_departure").GetComponent<RawImage>().color.a, Is.LessThan(1f));
            // Simulate a milestone recorded elsewhere while the menu is open.
            // Paging keeps the opening snapshot; reopening must refresh it.
            Assert.That(displayedStates["tutorial_recovery"], Is.False);
            AchievementCatalog.RecordDamagedReturn();
            progress = AchievementCatalog.PersistenceKeys.ToDictionary(key => key,
                key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null);

            for (int page = 0; page < 7; page++)
            {
                Canvas.ForceUpdateCanvases();
                RawImage[] visible = allIcons.Where(image => image.gameObject.activeInHierarchy).ToArray();
                Assert.That(visible.Length, Is.EqualTo(page == 6 ? 2 : 8));
                Assert.That(GameObject.Find("Achievements Page Indicator").GetComponent<Text>().text,
                    Is.EqualTo((page + 1) + " / 7"));
                foreach (RawImage icon in visible)
                {
                    string id = icon.name.Substring("Achievement Icon ".Length);
                    ItemIconArt art = AchievementIconCatalog.ForId(id);
                    Assert.That(visited.Add(id), Is.True, id + " appears on only one page.");
                    Assert.That(art.IsAvailable, Is.True, id);
                    Assert.That(icon.enabled, Is.True, id);
                    Assert.That(icon.texture, Is.SameAs(art.Texture), id);
                    Assert.That(icon.uvRect, Is.EqualTo(art.Uv), id);
                    Assert.That(icon.raycastTarget, Is.False, id);
                    Transform row = icon.transform.parent;
                    CoreAchievementDefinition definition = AchievementCatalog.All.Single(item => item.Id == id);
                    Assert.That(row.Find("Title").GetComponent<Text>().text, Is.EqualTo(definition.Title));
                    Assert.That(row.Find("Description").GetComponent<Text>().text, Is.EqualTo(definition.Description));
                    Assert.That(row.Find("State").GetComponent<Text>().text, Is.EqualTo(displayedStates[id] ? "已解锁" : "未解锁"),
                        id + " must use the snapshot from opening, without querying progress again while paging.");
                    Rect picture = AchievementIconBounds(row, icon.rectTransform);
                    foreach (Text label in row.GetComponentsInChildren<Text>())
                        Assert.That(picture.Overlaps(AchievementIconBounds(row, label.rectTransform)), Is.False,
                            id + " covers " + label.name);
                }
                CollectionAssert.AreEquivalent(builtObjects, controller.GetComponentsInChildren<Transform>(true),
                    "Paging must not create or destroy any UI objects.");
                NavalBattleVisualCapture.Capture(null, "AchievementIcons_" + (page + 1).ToString("00") + "_Page" + (page + 1).ToString("00") + ".png",
                    "实际主菜单成就页 " + (page + 1) + "/7；隔离成就状态夹具，图标保留所有说明与解锁文字", "Achievements Card");
                if (page < 6) yield return ClickAchievementIconControl("Achievements Next Page");
            }
            CollectionAssert.AreEquivalent(AchievementCatalog.All.Select(definition => definition.Id), visited);
            Assert.That(ActiveButton("Achievements Next Page").interactable, Is.False);
            yield return ClickAchievementIconControl("Back From Achievements");
            Assert.That(GameObject.Find("Achievements Panel"), Is.Null);
            yield return ClickAchievementIconControl("Achievements");
            Assert.That(GameObject.Find("Achievements Page Indicator").GetComponent<Text>().text, Is.EqualTo("7 / 7"),
                "Reopening preserves the page the player was reading.");
            string refreshedSummary = "已解锁 " + (displayedStates.Count(item => item.Value) + 1) + " / 50";
            Assert.That(GameObject.Find("Achievements Card").transform.Find("Modal Subtitle").GetComponent<Text>().text, Is.EqualTo(refreshedSummary));
            Assert.That(ActiveButton("Achievements").transform.Find("Detail"), Is.Null);
            int milestonePage = AchievementCatalog.All.ToList().FindIndex(item => item.Id == "tutorial_recovery") / 8;
            for (int page = 6; page > milestonePage; page--) yield return ClickAchievementIconControl("Achievements Previous Page");
            RawImage refreshedIcon = GameObject.Find("Achievement Icon tutorial_recovery").GetComponent<RawImage>();
            Assert.That(refreshedIcon.color, Is.EqualTo(Color.white));
            Assert.That(refreshedIcon.texture, Is.SameAs(AchievementIconCatalog.ForId("tutorial_recovery").Texture));
            Assert.That(refreshedIcon.uvRect, Is.EqualTo(AchievementIconCatalog.ForId("tutorial_recovery").Uv));
            Assert.That(refreshedIcon.transform.parent.Find("State").GetComponent<Text>().text, Is.EqualTo("已解锁"));
            for (int page = milestonePage; page > 0; page--) yield return ClickAchievementIconControl("Achievements Previous Page");
            Assert.That(ActiveButton("Achievements Previous Page").interactable, Is.False);
            CollectionAssert.AreEquivalent(allIcons, controller.GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith("Achievement Icon ")).ToArray(), "Paging and reopening reuse the existing badge graphics.");
            CollectionAssert.AreEquivalent(builtObjects, controller.GetComponentsInChildren<Transform>(true));
            AssertAchievementProgressUnchanged(progress);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertAchievementProgressUnchanged(Dictionary<string, int?> progress)
        {
            foreach (KeyValuePair<string, int?> saved in progress)
            {
                Assert.That(PlayerPrefs.HasKey(saved.Key), Is.EqualTo(saved.Value.HasValue), saved.Key);
                if (saved.Value.HasValue) Assert.That(PlayerPrefs.GetInt(saved.Key), Is.EqualTo(saved.Value.Value), saved.Key);
            }
        }

        private static IEnumerator ClickAchievementIconControl(string name)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                name + " blocked by " + hits[0].gameObject.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static Rect AchievementIconBounds(Transform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            Vector3[] points = corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
