using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
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
        public IEnumerator DeckBuilderCards_TenDifferentRowsFillBothColumns()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[] deck = controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval)
                .Where(index => !CoreVoyageSession.IsSpecialBattleCard(controller.Session.GetBattleCard(index).Id))
                .OrderByDescending(index => CardFaceLayout.DisplayName(controller.Session.GetBattleCard(index)).Length).Take(10).ToArray();
            Assert.That(controller.Session.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            CoreSpecialCardDefinition[] sources = CoreSpecialCardCatalog.All.Where(card => card.Card.IsAllowedInStage(NavalBattleStage.Naval))
                .OrderByDescending(card => CardFaceLayout.DisplayName(controller.Session.GetBattleCard(DeckBuilderIndex(card.Card.Id))).Length).Take(10).ToArray();
            ClickDeckBuilderHeader("Battle Deck Stage Tab Naval");
            yield return null;
            // Render refreshes production crew/equipment sources. Inject this visual fixture only after
            // that refresh, then rebuild its special list with the same production list builder.
            controller.Session.ConfigureSpecialCardSources(
                sources.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Crew).Select(card => card.SourceId).ToArray(),
                sources.Where(card => card.SourceKind == CoreSpecialCardSourceKind.Component).Select(card => card.SourceId).ToArray());
            Assert.That(controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), Has.Count.EqualTo(10));
            GameObject oldColumn = GameObject.Find("Deck Builder Special Column");
            int siblingIndex = oldColumn.transform.GetSiblingIndex();
            oldColumn.SetActive(false);
            Object.Destroy(oldColumn);
            yield return null;
            MethodInfo buildColumn = typeof(CoreGameplayDemoController).GetMethod("BuildDeckBuilderColumn", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildColumn, Is.Not.Null);
            ScrollRect rebuilt = (ScrollRect)buildColumn.Invoke(controller, new object[]
            {
                GameObject.Find("Deck Builder Panel").GetComponent<RectTransform>(),
                GameObject.Find("Deck Builder Deck Rail").GetComponent<RectTransform>(),
                null, controller.Session.GetBonusBattleCards(NavalBattleStage.Naval), true,
            });
            rebuilt.transform.parent.SetSiblingIndex(siblingIndex);
            FieldInfo specialScroll = typeof(CoreGameplayDemoController).GetField("deckBuilderSpecialScrollRect", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(specialScroll, Is.Not.Null);
            specialScroll.SetValue(controller, rebuilt);
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertDeckBuilderColumnCounts(controller);
            foreach (bool special in new[] { false, true })
            {
                ScrollRect scroll = DeckBuilderScroll(special);
                Button[] rows = special ? DeckBuilderSpecialCards() : DeckBuilderBaseCards();
                Assert.That(rows, Has.Length.EqualTo(10));
                foreach (Button row in rows) AssertDeckBuilderCompactCard(controller, row, 1);
                Assert.That(scroll.content.rect.height, Is.EqualTo(scroll.viewport.rect.height).Within(.1f));
                Rect first = ForgeBoundsIn(scroll.viewport, rows[0].GetComponent<RectTransform>());
                Rect last = ForgeBoundsIn(scroll.viewport, rows.Last().GetComponent<RectTransform>());
                Assert.That(first.yMax, Is.EqualTo(scroll.viewport.rect.yMax).Within(.1f));
                Assert.That(last.yMin, Is.EqualTo(scroll.viewport.rect.yMin).Within(.1f));
                DeckListDiamondScrollbar bar = scroll.transform.parent.GetComponentInChildren<DeckListDiamondScrollbar>(true);
                Assert.That(bar.HasOverflow, Is.False);
                Assert.That(bar.Handle.gameObject.activeSelf, Is.False);
            }
            yield return new WaitForSecondsRealtime(.7f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_00_TenDifferentRows.png",
                "两列包含当前炮击阶段最长基础与专属卡名，各10种铺满，验证缩字与数量图标互不遮挡", "Deck Builder Panel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderCards_ParallelColumnsAndWheelPreserveCardsAndProgress()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[] deck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            int[] sources = controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).ToArray();
            Dictionary<string, string> saved = DeckBuilderSavedProgress();
            decimal gold = controller.Session.ProfileGold;
            Button[] cards = DeckBuilderBaseCards();
            Assert.That(cards.Length, Is.EqualTo(deck.Distinct().Count()));
            CollectionAssert.AreEqual(deck.Distinct(), cards.Select(DeckBuilderCardIndex));
            foreach (Button card in cards) AssertDeckBuilderCompactCard(controller, card, deck.Count(index => index == DeckBuilderCardIndex(card)));
            Assert.That(GameObject.Find("Deck Builder Base Filter"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Special Filter"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null);
            AssertDeckBuilderColumnCounts(controller);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_01_Base.png", "基础卡与专属牌并排常驻，同名卡聚合为紧凑横条并分别计数", "Deck Builder Panel");

            ScrollRect scroll = DeckBuilderScroll();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Vector2 beforeWheel = scroll.content.anchoredPosition;
            Vector2 specialBeforeWheel = DeckBuilderScroll(true).content.anchoredPosition;
            PointerEventData pointer = DeckBuilderPointer(cards[0]);
            pointer.scrollDelta = new Vector2(0f, -3f);
            ExecuteEvents.ExecuteHierarchy(pointer.pointerCurrentRaycast.gameObject, pointer, ExecuteEvents.scrollHandler);
            yield return null;
            Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(beforeWheel.y), "Wheel input over a compact row scrolls only its owning column.");
            Assert.That(DeckBuilderScroll(true).content.anchoredPosition, Is.EqualTo(specialBeforeWheel));
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.02f), "Start the diamond gesture at the opposite end from its destination.");
            yield return DragDeckBuilderDiamondToBottom();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(.02f), "The border diamond can reveal the final compact row.");
            Assert.That(DeckBuilderScroll(true).content.anchoredPosition, Is.EqualTo(specialBeforeWheel), "Dragging one column's diamond cannot move the other column.");
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = .35f;
            yield return null;
            float savedBaseScroll = scroll.verticalNormalizedPosition;
            Button[] special = DeckBuilderSpecialCards();
            CollectionAssert.AreEqual(sources.Distinct(), special.Select(DeckBuilderCardIndex));
            Assert.That(special, Is.Not.Empty);
            foreach (Button card in special)
            {
                Assert.That(card.interactable, Is.True);
                Assert.That(card.GetComponent<DeckBuilderCardDrag>(), Is.Null);
                AssertDeckBuilderCompactCard(controller, card, sources.Count(index => index == DeckBuilderCardIndex(card)));
                ExecuteEvents.Execute(card.gameObject, DeckBuilderPointer(card), ExecuteEvents.pointerClickHandler);
                Assert.That(GameObject.Find("Deck Builder Status").GetComponent<Text>().text, Does.Contain("不能手动移除"));
            }
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_02_Special.png", "专属牌常驻独立列，点击只说明自动来源，不会修改牌组", "Deck Builder Panel");
            ClickDeckBuilderHeader("Next Battle Card Collection Page");
            yield return null;
            Assert.That(DeckBuilderScroll().verticalNormalizedPosition, Is.EqualTo(savedBaseScroll).Within(.02f));
            Assert.That(DeckBuilderScroll(true).content.anchoredPosition, Is.EqualTo(specialBeforeWheel));
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(sources, controller.Session.GetBonusBattleCards(NavalBattleStage.Naval));
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress(), "Browsing either column does not persist edits or spend gold.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderCards_HoverShowsCenteredFullCardsWithoutBlockingDeckGestures()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[] deck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            int[] sources = controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).ToArray();
            Dictionary<string, string> saved = DeckBuilderSavedProgress();
            Button[] rows = DeckBuilderBaseCards();
            Assert.That(GameObject.Find("Deck Builder Heading"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Interaction Hint"), Is.Null);
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null);

            PointerEventData firstPointer = DeckBuilderPointer(rows[0]);
            ExecuteEvents.Execute(rows[0].gameObject, firstPointer, ExecuteEvents.pointerEnterHandler);
            Vector2 baseCenter = AssertDeckBuilderHoverCard(controller, rows[0], 12f);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderHover_01_Base.png",
                "基础卡悬停在左侧蓝框位置显示完整卡面，卡面垂直居中且不拦截鼠标", "Deck Builder Panel");

            PointerEventData nextPointer = DeckBuilderPointer(rows[1]);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(rows[0].gameObject, firstPointer, ExecuteEvents.pointerExitHandler);
            Vector2 nextCenter = AssertDeckBuilderHoverCard(controller, rows[1], 12f);
            Assert.That(Vector2.Distance(nextCenter, baseCenter), Is.LessThan(.1f),
                "Changing the hovered row changes the full card without moving its fixed preview center.");
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerExitHandler);
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null, "Leaving the row hides its full card.");

            Button special = DeckBuilderSpecialCards().First();
            PointerEventData specialPointer = DeckBuilderPointer(special);
            ExecuteEvents.Execute(special.gameObject, specialPointer, ExecuteEvents.pointerEnterHandler);
            Vector2 specialCenter = AssertDeckBuilderHoverCard(controller, special, 402f);
            Assert.That(specialCenter.x, Is.GreaterThan(baseCenter.x + 300f),
                "Special cards preview over the base-card column, to the right of the base-card preview.");
            Assert.That(specialCenter.y, Is.EqualTo(baseCenter.y).Within(.1f));
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderHover_02_Special.png",
                "专属牌悬停在基础卡列红框位置显示完整卡面，与基础卡预览保持相同垂直中心", "Deck Builder Panel");
            ExecuteEvents.Execute(special.gameObject, specialPointer, ExecuteEvents.pointerExitHandler);
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null);

            nextPointer = DeckBuilderPointer(rows[1]);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerEnterHandler);
            AssertDeckBuilderHoverCard(controller, rows[1], 12f);
            float beforeScroll = DeckBuilderScroll().content.anchoredPosition.y;
            nextPointer.scrollDelta = new Vector2(0f, -1f);
            ExecuteEvents.ExecuteHierarchy(rows[1].gameObject, nextPointer, ExecuteEvents.scrollHandler);
            yield return null;
            Assert.That(DeckBuilderScroll().content.anchoredPosition.y, Is.GreaterThan(beforeScroll));
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null,
                "Scrolling clears the old card while the row beneath the pointer changes.");
            nextPointer = DeckBuilderPointer(rows[1]);
            ExecuteEvents.Execute<IPointerMoveHandler>(rows[1].gameObject, nextPointer,
                (handler, data) => handler.OnPointerMove((PointerEventData)data));
            AssertDeckBuilderHoverCard(controller, rows[1], 12f);

            nextPointer.pointerDrag = rows[1].gameObject;
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.beginDragHandler);
            Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Not.Null);
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null,
                "Starting a card drag replaces the fixed hover preview with the drag ghost.");
            nextPointer.dragging = true;
            ExecuteEvents.Execute<IPointerMoveHandler>(rows[1].gameObject, nextPointer,
                (handler, data) => handler.OnPointerMove((PointerEventData)data));
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null, "A drag cannot reopen hover preview.");
            ExecuteEvents.Execute(rows[1].gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Null);

            nextPointer = DeckBuilderPointer(rows[1]);
            ExecuteEvents.Execute(rows[1].gameObject, nextPointer, ExecuteEvents.pointerEnterHandler);
            AssertDeckBuilderHoverCard(controller, rows[1], 12f);
            ClickDeckBuilderHeader("Next Battle Card Collection Page");
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Hover Preview"), Is.Null,
                "Rebuilding the workshop for a different collection page clears the outgoing preview.");
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(sources, controller.Session.GetBonusBattleCards(NavalBattleStage.Naval));
            CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress(), "Hover, scroll and canceled drag never edit saved cards.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderCards_AggregatedClickLimitsAndReloadPersistExactlyOneChange()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            decimal gold = controller.Session.ProfileGold;
            int[] preparation = controller.Session.GetBattleDeck(NavalBattleStage.Preparation).ToArray();
            int[] boarding = controller.Session.GetBattleDeck(NavalBattleStage.Boarding).ToArray();
            int chain = DeckBuilderIndex("card_chain_shot");
            for (int expected = 11; expected >= 10; expected--)
            {
                Button remove = DeckBuilderBaseCards().First(card => DeckBuilderCardIndex(card) == chain);
                yield return RevealDeckBuilderCard(remove);
                ClickDeckBuilderHeader(remove.name);
                yield return null;
                AssertDeckBuilderSaved(controller, expected);
                Button remaining = DeckBuilderBaseCards().FirstOrDefault(card => DeckBuilderCardIndex(card) == chain);
                if (expected == 11)
                {
                    Assert.That(remaining, Is.Not.Null, "Clicking an x2 row leaves the x1 row.");
                    AssertDeckBuilderCompactCard(controller, remaining, 1);
                }
                else Assert.That(remaining, Is.Null, "Only removing the final copy removes its row.");
            }
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(index => index == chain), Is.Zero);
            for (int expected = 11; expected <= 12; expected++)
            {
                ClickDeckBuilderHeader("Add Deck Card " + chain);
                yield return null;
                AssertDeckBuilderSaved(controller, expected);
            }
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(index => index == chain), Is.EqualTo(2));
            AssertDeckBuilderSaved(controller, 12);
            int[] beforeBlocked = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            ClickDeckBuilderHeader("Add Deck Card " + chain, false);
            yield return null;
            CollectionAssert.AreEqual(beforeBlocked, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            AssertDeckBuilderSaved(controller, 12);
            Assert.That(GameObject.Find("Deck Builder Status").GetComponent<Text>().text, Does.Contain("2"));
            int addIndex = DeckBuilderIndex("card_full_sail_turn");
            ClickDeckBuilderHeader("Add Deck Card " + addIndex);
            yield return null;
            AssertDeckBuilderSaved(controller, 13);
            Assert.That(DeckBuilderSpecialCards(), Is.Not.Empty, "Adding a base card leaves the special column visible.");
            AssertDeckBuilderCompactCard(controller, DeckBuilderBaseCards().Single(card => DeckBuilderCardIndex(card) == addIndex), 2);
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Last(), Is.EqualTo(addIndex));
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_03_AggregatedAddition.png", "同名卡再次加入只增加横条数量，基础与专属两列保持可见", "Deck Builder Panel");
            int blockedAtMaximum = DeckBuilderIndex("card_circle_enemy");
            while (controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count < 20)
            {
                int next = controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval)
                    .First(index => index != blockedAtMaximum && controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(card => card == index) < 2);
                int expected = controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count + 1;
                yield return RevealDeckBuilderLibraryCard(next);
                ClickDeckBuilderHeader("Add Deck Card " + next);
                yield return null;
                AssertDeckBuilderSaved(controller, expected);
            }
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_05_TwentyCardMaximum.png", "基础牌组已加入 20 张，每种最多 2 张；专属牌仍独立追加", "Deck Builder Panel");
            yield return RevealDeckBuilderLibraryCard(blockedAtMaximum);
            ClickDeckBuilderHeader("Add Deck Card " + blockedAtMaximum, false);
            yield return null;
            AssertDeckBuilderSaved(controller, 20);
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count, Is.EqualTo(20));
            Assert.That(GameObject.Find("Deck Builder Status").GetComponent<Text>().text, Does.Contain("满"));
            while (controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count > 10)
            {
                Button remove = DeckBuilderBaseCards().First();
                yield return RevealDeckBuilderCard(remove);
                ClickDeckBuilderHeader(remove.name);
                yield return null;
            }
            Button minimum = DeckBuilderBaseCards().First();
            yield return RevealDeckBuilderCard(minimum);
            int removedAtMinimum = DeckBuilderCardIndex(minimum);
            int[] finalDeck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Dictionary<string, string> savedAtMinimum = DeckBuilderSavedProgress();
            ClickDeckBuilderHeader(minimum.name);
            yield return null;
            AssertDeckBuilderDraftCount(9);
            CollectionAssert.AreEqual(finalDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval),
                "Removing the tenth card edits the on-screen draft while preserving the last valid gameplay deck.");
            CollectionAssert.AreEquivalent(savedAtMinimum, DeckBuilderSavedProgress());
            AssertDeckBuilderMinimumLock(true);
            yield return RevealDeckBuilderLibraryCard(removedAtMinimum);
            ClickDeckBuilderHeader("Add Deck Card " + removedAtMinimum);
            yield return null;
            finalDeck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            AssertDeckBuilderSaved(controller, 10);
            AssertDeckBuilderMinimumLock(false);
            NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_06_TenCardMinimum.png", "不足10张时留在构筑界面，补足10张后保存并恢复导航", "Deck Builder Panel");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            CollectionAssert.AreEqual(preparation, controller.Session.GetBattleDeck(NavalBattleStage.Preparation));
            CollectionAssert.AreEqual(boarding, controller.Session.GetBattleDeck(NavalBattleStage.Boarding));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(finalDeck, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Every successful click is committed without an extra save button.");
            Assert.That(controller.Session.ProfileGold, Is.EqualTo(gold));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderCards_DragChangesOnceAndCancellationNeverChangesTheDeck()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int chain = DeckBuilderIndex("card_chain_shot");
            for (int n = 0; n < 2; n++)
            {
                Button remove = DeckBuilderBaseCards().First(card => DeckBuilderCardIndex(card) == chain);
                yield return RevealDeckBuilderCard(remove);
                ClickDeckBuilderHeader(remove.name);
                yield return null;
            }
            Button source = ActiveButton("Add Deck Card " + chain);
            yield return CancelDeckBuilderPressBeforeDrag(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), false);
            AssertDeckBuilderSaved(controller, 10);
            source = ActiveButton("Add Deck Card " + chain);
            yield return CancelDeckBuilderPressBeforeDrag(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), true);
            AssertDeckBuilderSaved(controller, 10);
            source = ActiveButton("Add Deck Card " + chain);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), false, false, "DeckBuilderCards_04_Drag.png");
            AssertDeckBuilderSaved(controller, 11);
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(index => index == chain), Is.EqualTo(1), "Drag and subsequent click cannot add twice.");
            ClickDeckBuilderHeader("Add Deck Card " + chain);
            yield return null;
            source = DeckBuilderBaseCards().First(card => DeckBuilderCardIndex(card) == chain);
            yield return RevealDeckBuilderCard(source);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Parchment"), false);
            AssertDeckBuilderSaved(controller, 11);
            Assert.That(controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count(index => index == chain), Is.EqualTo(1), "Drag and subsequent click cannot remove twice.");

            int[] original = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Dictionary<string, string> saved = DeckBuilderSavedProgress();
            source = ActiveButton("Add Deck Card " + chain);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Parchment"), false);
            CollectionAssert.AreEqual(original, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Dropping back on the library is a no-op.");
            source = DeckBuilderBaseCards().First(card => DeckBuilderCardIndex(card) == chain);
            yield return RevealDeckBuilderCard(source);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), false);
            CollectionAssert.AreEqual(original, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Dropping back inside the deck is a no-op.");
            source = ActiveButton("Add Deck Card " + chain);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), true);
            CollectionAssert.AreEqual(original, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Cancel leaves the deck untouched.");
            source = ActiveButton("Add Deck Card " + chain);
            yield return DragDeckBuilderCard(source, DeckBuilderNamedPoint("Deck Builder Deck Rail"), false, true);
            CollectionAssert.AreEqual(original, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Losing focus cancels a pending edit.");
            source = DeckBuilderBaseCards().First(card => DeckBuilderCardIndex(card) == chain);
            yield return RevealDeckBuilderCard(source);
            yield return DragDeckBuilderCard(source, new Vector2(-50f, -50f), false);
            CollectionAssert.AreEqual(original, controller.Session.GetBattleDeck(NavalBattleStage.Naval), "Dropping outside the screen does not remove a card.");
            CollectionAssert.AreEquivalent(saved, DeckBuilderSavedProgress(), "Canceled or invalid drags never save.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderHeader_AllFourButtonsStayOnPaperAndFilterReadOnlySpecialCardsByStage()
        {
            yield return LoadDeckBuilderCardFixture();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            NavalBattleStage[] stages = { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding };
            string[] labels = { "备战策略", "炮击机动", "接舷近战" };
            controller.Session.AcquireSpecialCards(stages.SelectMany(stage => CoreSpecialCardCatalog.All
                .Where(source => source.Card.IsAllowedInStage(stage)).Take(9)).Select(source => source.Card.Id).Distinct().ToArray());
            Dictionary<NavalBattleStage, int[]> decks = stages.ToDictionary(stage => stage, stage => controller.Session.GetBattleDeck(stage).ToArray());
            Dictionary<string, string> saves = DeckBuilderSavedProgress();
            for (int index = 0; index < stages.Length; index++)
            {
                ClickDeckBuilderHeader("Battle Deck Stage Tab " + stages[index]);
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain(labels[index]));
                Button[] tabs = GameObject.Find("Deck Builder Header").GetComponentsInChildren<Button>();
                Assert.That(tabs, Has.Length.EqualTo(4));
                RectTransform paper = GameObject.Find("Deck Builder Parchment").GetComponent<RectTransform>();
                foreach (Button button in tabs)
                {
                    Rect bounds = ForgeBoundsIn(paper, button.GetComponent<RectTransform>());
                    Assert.That(bounds.xMin, Is.GreaterThan(paper.rect.xMin));
                    Assert.That(bounds.xMax, Is.LessThan(paper.rect.xMax));
                    Assert.That(bounds.yMax, Is.LessThan(paper.rect.yMax));
                }
                Button[] available = GameObject.Find("Deck Builder Collection").GetComponentsInChildren<Button>()
                    .Where(button => button.name.StartsWith("Add Deck Card ")).ToArray();
                Assert.That(available.Length, Is.InRange(1, 8));
                foreach (Button button in available)
                    Assert.That(controller.Session.GetBattleCard(DeckBuilderCardIndex(button)).IsAllowedInStage(stages[index]), Is.True);
                NavalBattleVisualCapture.Capture(controller, "DeckBuilderCards_Stage_" + stages[index] + ".png", "真实卡面编组 · " + labels[index], "Deck Builder Panel");
                Assert.That(ActiveButton("Open Battle Card Upgrades"), Is.Null);
                Assert.That(ActiveButton("Open Special Card Collection").GetComponentInChildren<Text>().text, Is.EqualTo("特殊卡牌"));
                ClickDeckBuilderHeader("Open Special Card Collection");
                yield return null;
                Assert.That(GameObject.Find("Battle Card Upgrade Panel"), Is.Null);
                Assert.That(GameObject.Find("Deck Builder Panel"), Is.Null);
                Assert.That(GameObject.Find("Card Workshop Preview Panel"), Is.Not.Null);
                HashSet<int> visible = new HashSet<int>();
                int[] expected = controller.Session.AcquiredSpecialCardIds.Select(DeckBuilderIndex)
                    .Where(card => controller.Session.GetBattleCard(card).IsAllowedInStage(stages[index])).ToArray();
                for (int page = 0; page < Mathf.CeilToInt(expected.Length / 8f); page++)
                {
                    Button[] specialCards = GameObject.Find("Workshop Preview Collection").GetComponentsInChildren<Button>()
                        .Where(button => button.name.StartsWith("Select Special Card ")).ToArray();
                    foreach (Button card in specialCards)
                    {
                        int cardIndex = int.Parse(card.name.Substring("Select Special Card ".Length));
                        Assert.That(visible.Add(cardIndex), Is.True, "Each matching special appears only once across pages.");
                        Assert.That(controller.Session.GetBattleCard(cardIndex).IsAllowedInStage(stages[index]), Is.True);
                        Assert.That(card.GetComponent<DeckBuilderCardDrag>(), Is.Null);
                        AssertSpecialCardMembership(cardIndex, controller.Session.GetBonusBattleCards(stages[index]).Contains(cardIndex));
                    }
                    int[] bonusBeforeSelection = controller.Session.GetBonusBattleCards(stages[index]).ToArray();
                    ClickDeckBuilderHeader(specialCards.Last().name);
                    yield return null;
                    CollectionAssert.AreEqual(decks[stages[index]], controller.Session.GetBattleDeck(stages[index]),
                        "Clicking a special card only selects its preview and cannot add it to the base deck.");
                    CollectionAssert.AreEqual(bonusBeforeSelection, controller.Session.GetBonusBattleCards(stages[index]),
                        "Clicking a source-provided card cannot remove it from the special deck.");
                    if (page == 0)
                        NavalBattleVisualCapture.Capture(controller, "DeckBuilderSpecial_Stage_" + stages[index] + ".png",
                            labels[index] + "只展示本阶段特殊卡牌，卡牌下方按实际来源显示已加入或配置入口", "Card Workshop Preview Panel");
                    if (page + 1 < Mathf.CeilToInt(expected.Length / 8f))
                    {
                        ClickDeckBuilderHeader("Next Upgrade Cards");
                        yield return null;
                    }
                }
                CollectionAssert.AreEquivalent(expected, visible, "The builder special collection includes only acquired cards for its current stage.");
                ClickDeckBuilderHeader("Workshop Preview Stage " + stages[index]);
                yield return null;
                Assert.That(GameObject.Find("Deck Builder Deck Title").GetComponent<Text>().text, Does.Contain(labels[index]));
                CollectionAssert.AreEqual(decks[stages[index]], controller.Session.GetBattleDeck(stages[index]));
            }
            CollectionAssert.AreEquivalent(saves, DeckBuilderSavedProgress());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeckBuilderLegacySave_TrimsThirdCopyFillsTenAndPersistsMigration()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval), "0,0,0,1,2,5,6,7");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            int[] migrated = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Assert.That(migrated.Length, Is.EqualTo(10));
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 2, 5, 6, 7 }, migrated.Take(7));
            Assert.That(migrated.GroupBy(index => index).All(group => group.Count() <= 2), Is.True);
            Assert.That(migrated.All(index => CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Naval)), Is.True);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval)), Is.EqualTo(string.Join(",", migrated)));
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CollectionAssert.AreEqual(migrated, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadDeckBuilderCardFixture()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.TutorialCompletedKey, 1);
            CrewVariantDefinition navigator = CrewNarrativeCatalog.ForPortAndProfession("灰鸥港", 0);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(navigator.Id), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(navigator.ProfessionIndex), navigator.Id);
            PlayerPrefs.SetString(CoreGameplayDemoController.BattleCardUpgradeKey, "card_cannon_barrage=3");
            PlayerPrefs.SetString(CoreGameplayDemoController.SpecialCardExperienceKey, CoreSpecialCardCatalog.ForCrew(navigator.Id).Card.Id + "=840");
            PlayerPrefs.SetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval), "0,0,6,1,2,5,7,8,9,10,11,13");
            SceneManager.LoadScene("CoreGameplayDemo", LoadSceneMode.Single);
            yield return null; yield return null;
            ClickDeckBuilderHeader("Port Hotspot CardWorkshop");
            yield return null;
            ClickDeckBuilderHeader("Edit Stage Deck Naval");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<CoreGameplayDemoController>().Session.GetBattleDeck(NavalBattleStage.Naval).Count, Is.EqualTo(12));
            Assert.That(DeckBuilderBaseCards(), Has.Length.EqualTo(11), "The pair of chain shots shares one summary row.");
            Canvas.ForceUpdateCanvases();
        }

        private static ScrollRect DeckBuilderScroll(bool special = false) => GameObject.Find("Deck Builder " + (special ? "Special" : "Base") + " Scroll Viewport").GetComponent<ScrollRect>();
        private static Button[] DeckBuilderBaseCards() => DeckBuilderScroll().content.GetComponentsInChildren<Button>()
            .Where(button => button.name.StartsWith("Remove Deck Card ")).ToArray();
        private static Button[] DeckBuilderSpecialCards() => DeckBuilderScroll(true).content.GetComponentsInChildren<Button>()
            .Where(button => button.name.StartsWith("Crew Bonus Deck Card ")).ToArray();
        private static int DeckBuilderIndex(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static int DeckBuilderCardIndex(Button button)
        {
            string prefix = button.name.StartsWith("Crew Bonus Deck Card ") ? "Crew Bonus Deck Card " :
                button.name.StartsWith("Remove Deck Card ") ? "Remove Deck Card " : "Add Deck Card ";
            return int.Parse(button.name.Substring(prefix.Length).Split(' ')[0]);
        }

        private static void AssertDeckBuilderSaved(CoreGameplayDemoController controller, int total)
        {
            IReadOnlyList<int> deck = controller.Session.GetBattleDeck(NavalBattleStage.Naval);
            Assert.That(deck.Count, Is.EqualTo(total));
            Assert.That(DeckBuilderBaseCards().Length, Is.EqualTo(deck.Distinct().Count()));
            CollectionAssert.AreEqual(deck.Distinct(), DeckBuilderBaseCards().Select(DeckBuilderCardIndex));
            foreach (Button row in DeckBuilderBaseCards())
                AssertDeckBuilderCompactCard(controller, row, deck.Count(index => index == DeckBuilderCardIndex(row)));
            AssertDeckBuilderColumnCounts(controller);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval)), Is.EqualTo(string.Join(",", deck)));
        }

        private static void AssertDeckBuilderCompactCard(CoreGameplayDemoController controller, Button button, int copies)
        {
            CoreBattleCard card = controller.Session.GetBattleCard(DeckBuilderCardIndex(button));
            Assert.That(button.GetComponentInChildren<CardSchoolFrameGraphic>(), Is.Null);
            DeckListRowGraphic frame = button.GetComponentInChildren<DeckListRowGraphic>();
            Assert.That(frame, Is.Not.Null);
            Assert.That(frame.HasAuthoredTexture, Is.True);
            Assert.That(frame.SchoolId, Is.EqualTo(CardSchoolArtCatalog.NormalizeSchool(card.SchoolId)));
            Assert.That(button.GetComponentsInChildren<CardOrnamentGraphic>(true), Is.Empty,
                "Only the compact strip's energy icon appears; full-card resource badges do not leak into the row.");
            Text[] labels = button.GetComponentsInChildren<Text>(true).Where(label => !string.IsNullOrEmpty(label.text)).ToArray();
            Assert.That(labels, Has.Length.EqualTo(3), "Only energy, name and aggregated quantity are shown.");
            Text title = labels.Single(label => label.name == button.name + " Name");
            string fullName = CardFaceLayout.DisplayName(card);
            if (title.text != fullName)
            {
                Assert.That(title.text, Does.EndWith("…"));
                Assert.That(title.text.Length, Is.GreaterThan(1));
                Assert.That(fullName, Does.StartWith(title.text.Substring(0, title.text.Length - 1)), "Any shortened name retains its true prefix.");
            }
            Assert.That(title.fontSize, Is.GreaterThanOrEqualTo(12));
            Assert.That(title.preferredWidth, Is.LessThanOrEqualTo(title.rectTransform.rect.width + .5f), "The actual text width at its final font size fits the name area: " + fullName);
            Assert.That(title.preferredHeight, Is.LessThanOrEqualTo(title.rectTransform.rect.height + .5f), fullName);
            Text quantity = labels.Single(label => label.name == button.name + " Quantity");
            Assert.That(quantity.text, Is.EqualTo(copies.ToString()));
            Assert.That(quantity.preferredWidth, Is.LessThanOrEqualTo(quantity.rectTransform.rect.width + .5f), "The entire quantity including its numeral must fit on one line.");
            Assert.That(quantity.preferredHeight, Is.LessThanOrEqualTo(quantity.rectTransform.rect.height + .5f), "The quantity must not wrap and lose its final digit.");
            Assert.That(ForgeBoundsIn(button.transform, title.rectTransform).xMax,
                Is.LessThan(ForgeBoundsIn(button.transform, quantity.rectTransform).xMin), "The fitted title cannot cover the count.");
            Assert.That(button.GetComponentsInChildren<RawImage>(true).Any(image => image.texture != null), Is.True);
            Rect bounds = button.GetComponent<RectTransform>().rect;
            Assert.That(bounds.width, Is.GreaterThan(bounds.height * 3f), "The right side renders horizontal summaries.");
            Text energy = labels.Single(label => label.name == button.name + " Energy");
            Assert.That(energy.text, Is.EqualTo(card.Energy.ToString()));
            Rect energyBounds = ForgeBoundsIn(button.transform, energy.rectTransform);
            Assert.That(energyBounds.center.x, Is.InRange(bounds.xMin, bounds.xMin + 54f), "Energy remains inside the left icon.");
        }

        private static Vector2 AssertDeckBuilderHoverCard(CoreGameplayDemoController controller, Button row, float expectedX)
        {
            Canvas.ForceUpdateCanvases();
            GameObject preview = GameObject.Find("Deck Builder Hover Preview");
            Assert.That(preview, Is.Not.Null, row.name);
            RectTransform panel = GameObject.Find("Deck Builder Panel").GetComponent<RectTransform>();
            Assert.That(preview.transform.parent, Is.SameAs(panel), "The preview is outside both masked scroll viewports.");
            CoreBattleCard card = controller.Session.GetBattleCard(DeckBuilderCardIndex(row));
            CardSchoolFrameGraphic frame = preview.GetComponentInChildren<CardSchoolFrameGraphic>();
            Assert.That(frame, Is.Not.Null, "Hover restores the complete card frame, not another compact summary.");
            Assert.That(frame.HasAuthoredTexture, Is.True);
            Assert.That(frame.mainTexture, Is.SameAs(CardSchoolArtCatalog.Frame(card)));
            Rect bounds = ForgeBoundsIn(panel, frame.rectTransform);
            Rect authored = CardSchoolArtCatalog.FrameBounds(card);
            Assert.That(bounds.width / bounds.height, Is.EqualTo(authored.width / authored.height).Within(.001f));
            Assert.That(bounds.width, Is.InRange(180f, 260.1f));
            Assert.That(bounds.height, Is.InRange(300f, 390.1f));
            Assert.That(bounds.center.x, Is.EqualTo(expectedX).Within(.1f));
            Rect screenBounds = ForgeBoundsIn(GameObject.Find("Phase Content").transform, frame.rectTransform);
            Assert.That(screenBounds.center.y, Is.EqualTo(0f).Within(.1f),
                "The complete card frame is vertically centered on the screen, independently of its hovered row.");
            Text[] text = preview.GetComponentsInChildren<Text>();
            Assert.That(text.Any(label => label.text == CardFaceLayout.DisplayName(card)), Is.True, "The full title is present.");
            Assert.That(text.Any(label => label.name.Contains("Effect") && !string.IsNullOrWhiteSpace(label.text)), Is.True,
                "The card effect accompanies its artwork and title.");
            Assert.That(preview.GetComponentsInChildren<RawImage>().Any(image => image.texture != null), Is.True);
            Assert.That(preview.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True,
                "The large preview cannot steal pointer hits from a row or a collection card beneath it.");
            CanvasGroup group = preview.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            DeckBuilderPointer(row);
            return bounds.center;
        }

        private static void AssertDeckBuilderColumnCounts(CoreGameplayDemoController controller)
        {
            Assert.That(GameObject.Find("Deck Builder Base Column Count").GetComponent<Text>().text,
                Is.EqualTo("基础卡 " + controller.Session.GetBattleDeck(NavalBattleStage.Naval).Count + " 张"));
            Assert.That(GameObject.Find("Deck Builder Special Column Count").GetComponent<Text>().text,
                Is.EqualTo("专属牌 " + controller.Session.GetBonusBattleCards(NavalBattleStage.Naval).Count + " 张"));
        }

        private static IEnumerator RevealDeckBuilderCard(Button card)
        {
            ScrollRect scroll = DeckBuilderScroll();
            if (!card.transform.IsChildOf(scroll.content)) yield break;
            scroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            Rect bounds = ForgeBoundsIn(scroll.content, card.GetComponent<RectTransform>());
            float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            if (overflow > 0f)
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((scroll.content.rect.yMax - bounds.yMax) / overflow);
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private static IEnumerator RevealDeckBuilderLibraryCard(int index)
        {
            if (ActiveButton("Add Deck Card " + index) != null) yield break;
            while (ActiveButton("Previous Battle Card Collection Page").interactable)
            {
                ClickDeckBuilderHeader("Previous Battle Card Collection Page");
                yield return null;
            }
            while (ActiveButton("Add Deck Card " + index) == null)
            {
                Assert.That(ActiveButton("Next Battle Card Collection Page").interactable, Is.True, "Requested base card must be reachable in the real library.");
                ClickDeckBuilderHeader("Next Battle Card Collection Page");
                yield return null;
            }
        }

        private static Vector2 DeckBuilderScreenPoint(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
        }

        private static Vector2 DeckBuilderNamedPoint(string name) => DeckBuilderScreenPoint(GameObject.Find(name).GetComponent<RectTransform>());

        private static IEnumerator DragDeckBuilderDiamondToBottom()
        {
            DeckListDiamondScrollbar scrollbar = GameObject.Find("Deck Builder Base Scrollbar").GetComponent<DeckListDiamondScrollbar>();
            Assert.That(scrollbar.HasOverflow, Is.True);
            Canvas.ForceUpdateCanvases();
            scrollbar.Refresh();
            Vector2 handleSize = scrollbar.Handle.rect.size;
            Vector2 start = DeckBuilderScreenPoint(scrollbar.Handle);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = start,
                pressPosition = start,
                pointerDrag = scrollbar.gameObject,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<DeckListDiamondScrollbar>(), Is.SameAs(scrollbar), "The diamond is a real visible drag target.");
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(scrollbar.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(scrollbar.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(scrollbar.gameObject, pointer, ExecuteEvents.beginDragHandler);
            RectTransform track = scrollbar.GetComponent<RectTransform>();
            Canvas canvas = track.GetComponentInParent<Canvas>().rootCanvas;
            pointer.position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                track.TransformPoint(new Vector2(track.rect.center.x, track.rect.yMin + handleSize.y * .5f)));
            pointer.dragging = true;
            ExecuteEvents.Execute(scrollbar.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(scrollbar.gameObject, pointer, ExecuteEvents.endDragHandler);
            yield return null;
            Assert.That(scrollbar.Handle.rect.size, Is.EqualTo(handleSize), "The diamond retains its proportions throughout scrolling.");
        }

        private static PointerEventData DeckBuilderPointer(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                eligibleForClick = true,
                position = DeckBuilderScreenPoint(button.GetComponent<RectTransform>()),
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), button.name + " receives the real visible hit target");
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            pointer.pressPosition = pointer.position;
            pointer.pointerPress = button.gameObject;
            pointer.rawPointerPress = button.gameObject;
            return pointer;
        }

        private static void ClickDeckBuilderHeader(string name, bool requireEnabled = true)
        {
            Button button = ActiveButton(name);
            Assert.That(button, Is.Not.Null, name);
            if (requireEnabled) Assert.That(button.interactable, Is.True, name);
            PointerEventData pointer = DeckBuilderPointer(button);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator CancelDeckBuilderPressBeforeDrag(Button button, Vector2 destination, bool scroll)
        {
            PointerEventData pointer = DeckBuilderPointer(button);
            pointer.pointerDrag = button.gameObject;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            if (scroll)
            {
                pointer.scrollDelta = new Vector2(0f, -1f);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.scrollHandler);
            }
            else ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            pointer.delta = destination - pointer.position;
            pointer.position = destination;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.dragging = true;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.dragHandler);
            Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Null, "A canceled press cannot restart dragging while the same pointer is held.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.endDragHandler);
            pointer.dragging = false;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator DragDeckBuilderCard(Button button, Vector2 destination, bool cancel, bool loseFocus = false, string captureName = null)
        {
            DeckBuilderCardDrag drag = button.GetComponent<DeckBuilderCardDrag>();
            Assert.That(drag, Is.Not.Null, button.name);
            PointerEventData pointer = DeckBuilderPointer(button);
            pointer.pointerDrag = button.gameObject;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.beginDragHandler);
            GameObject ghost = GameObject.Find("Deck Builder Drag Ghost");
            Assert.That(ghost, Is.Not.Null, "Dragging has a visible card preview.");
            Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(ghost.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
            pointer.dragging = true;
            pointer.delta = destination - pointer.position;
            pointer.position = destination;
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.dragHandler);
            yield return null;
            if (captureName != null)
                NavalBattleVisualCapture.Capture(Object.FindFirstObjectByType<CoreGameplayDemoController>(), captureName,
                    "真实拖拽卡面进入基础卡组，目标高亮，释放只加入一张", "Deck Builder Panel");
            if (cancel)
            {
                Assert.That(drag, Is.InstanceOf<ICancelHandler>(), "The same cancel path supports Escape and UI cancellation.");
                ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            }
            if (loseFocus) button.gameObject.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(drag.SuppressClick, Is.True, "Releasing the pointer before EndDrag keeps its follow-up click suppressed.");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(drag.SuppressClick, Is.True, "A completed or canceled drag must suppress the follow-up click.");
            pointer.dragging = false;
            if (button != null)
            {
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }
            yield return null;
            Assert.That(GameObject.Find("Deck Builder Drag Ghost"), Is.Null, "Dropping or canceling removes the drag preview.");
        }

        private static Dictionary<string, string> DeckBuilderSavedProgress()
        {
            string[] keys =
            {
                CoreGameplayDemoController.BattleDeckKey, CoreGameplayDemoController.BattleCardUpgradeKey,
                CoreGameplayDemoController.SpecialCardExperienceKey, CoreGameplayDemoController.AcquiredSpecialCardsKey,
                CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation),
                CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval),
                CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Boarding), ProfileGoldPersistence.DecimalKey,
            };
            Dictionary<string, string> result = keys.ToDictionary(key => key, key => PlayerPrefs.HasKey(key) ? "set:" + PlayerPrefs.GetString(key) : "absent");
            string goldKey = CoreGameplayDemoController.ProfileGoldKey;
            result.Add(goldKey, PlayerPrefs.HasKey(goldKey) ? "set:" + PlayerPrefs.GetInt(goldKey) : "absent");
            return result;
        }
    }
}
