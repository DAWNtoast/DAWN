using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator DeckBuilderHeader_AllFourButtonsStayOnPaperAndKeepStageUpgradeNavigation()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null;
            yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            Assert.That(controller, Is.Not.Null);
            ActiveButton("Port Hotspot CardWorkshop").onClick.Invoke();
            yield return null;
            ActiveButton("Open Deck Chest 0").onClick.Invoke();
            yield return null;
            ActiveButton("Edit Deck From Card Upgrades").onClick.Invoke();
            yield return null;

            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            string[] stageLabels = { "备战策略", "炮击机动", "接舷近战" };
            string[] captures = { "92_DeckBuilder_Header.png", "93_DeckBuilder_Header_Naval.png", "94_DeckBuilder_Header_Boarding.png" };
            Dictionary<NavalBattleStage, int[]> decks = stages.ToDictionary(stage => stage, stage => controller.Session.GetBattleDeck(stage).ToArray());
            Dictionary<NavalBattleStage, string> saves = stages.ToDictionary(stage => stage, stage => PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty));
            decimal gold = controller.Session.ProfileGold;

            for (int index = 0; index < stages.Length; index++)
            {
                ClickDeckBuilderHeader("Battle Deck Stage Tab " + stages[index]);
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain(stageLabels[index]));
                RectTransform paper = GameObject.Find("Deck Builder Parchment").GetComponent<RectTransform>();
                Button[] headerButtons = GameObject.Find("Deck Builder Header").GetComponentsInChildren<Button>();
                Assert.That(headerButtons.Length, Is.EqualTo(4));
                foreach (Button button in headerButtons)
                {
                    Vector3[] corners = new Vector3[4];
                    button.GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (Vector3 corner in corners)
                        Assert.That(paper.rect.Contains(paper.InverseTransformPoint(corner)), Is.True, button.name + " must stay on the parchment.");
                }
                NavalBattleVisualCapture.Capture(controller, captures[index], "牌组编组页签与卡牌强化统一位于羊皮纸内 · " + stageLabels[index], "Deck Builder Panel");

                ClickDeckBuilderHeader("Open Battle Card Upgrades");
                yield return null;
                GameObject forge = GameObject.Find("Battle Card Upgrade Panel");
                Assert.That(forge, Is.Not.Null);
                Button[] cards = forge.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Select Upgrade Card ")).ToArray();
                Assert.That(cards, Is.Not.Empty);
                foreach (Button card in cards)
                {
                    int cardIndex = int.Parse(card.name.Substring("Select Upgrade Card ".Length));
                    Assert.That(controller.Session.GetBattleCard(cardIndex).IsAllowedInStage(stages[index]), Is.True, card.name);
                }
                ActiveButton("Edit Deck From Card Upgrades").onClick.Invoke();
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain(stageLabels[index]));
            }

            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            foreach (NavalBattleStage stage in stages)
            {
                CollectionAssert.AreEqual(decks[stage], controller.Session.GetBattleDeck(stage), stage.ToString());
                Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(stage), string.Empty), Is.EqualTo(saves[stage]));
            }
        }

        private static void ClickDeckBuilderHeader(string name)
        {
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.interactable, Is.True, name);
            Assert.That(EventSystem.current, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Canvas canvas = button.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
            };
            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), name + " must receive the visible hit target.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
