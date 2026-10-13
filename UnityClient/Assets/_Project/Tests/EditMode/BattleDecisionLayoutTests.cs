using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleDecisionLayoutTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase(NavalBattleStage.Preparation, 600f, NavalBattleAlignment.HeadOn)]
        [TestCase(NavalBattleStage.Naval, 600f, NavalBattleAlignment.HeadOn)]
        [TestCase(NavalBattleStage.Naval, 80f, NavalBattleAlignment.HeadOn)]
        [TestCase(NavalBattleStage.Naval, 50f, NavalBattleAlignment.Parallel)]
        [TestCase(NavalBattleStage.Boarding, 0f, NavalBattleAlignment.HeadOn)]
        public void DecisionPanels_KeepReadableControlsSeparateFromTheHandAndCombatants(NavalBattleStage stage, float distance, NavalBattleAlignment alignment)
        {
            CreatePreview(stage, distance, alignment);
            CoreBattleState battle = controller.Session.Battle;
            AssertLayout(stage);
            Assert.That(Find<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + controller.Session.GetEnemyIntent()));
            if (stage == NavalBattleStage.Naval && distance < 120f && alignment == NavalBattleAlignment.HeadOn)
            {
                Assert.That(controller.Session.EnemyWillRam, Is.True, "The near fixture must include the complete ram warning.");
                Assert.That(Find<Text>("Enemy Intent Text").text, Does.Contain("灼烧").And.Contain("先手"));
            }
            if (stage == NavalBattleStage.Naval && alignment == NavalBattleAlignment.Parallel)
            {
                Assert.That(controller.Session.CanBoard(out _), Is.True);
                Assert.That(Find<Text>("Battle Board Condition").text,
                    Is.EqualTo("普通水手数决定接舷生命上限；海战船体损伤按一半带入"));
            }
            foreach (string side in new[] { "Player", "Enemy" })
            {
                RectTransform track = Find<RectTransform>(side + " Battle Hull Bar Track");
                Image fill = Find<Image>(side + " Battle Hull Bar");
                Assert.That(fill.transform.parent, Is.SameAs(track));
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                BattleHealthSegmentsGraphic[] grids = fill.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true);
                Assert.That(grids, Has.Length.EqualTo(1));
                int expectedMaximum = side == "Player"
                    ? (stage == NavalBattleStage.Boarding ? battle.PlayerMaxBoardingHealth : controller.Session.MaxHull)
                    : (stage == NavalBattleStage.Boarding ? battle.EnemyMaxBoardingHealth : battle.EnemyMaxHull);
                Assert.That(grids[0].MaximumHealth, Is.EqualTo(expectedMaximum));
                Assert.That(grids[0].raycastTarget, Is.False);
                foreach (string part in new[] { "Armor Bar", "Armor Bar Lower", "Armor Frame 0", "Armor Frame 1", "Armor Frame 2", "Armor Frame 3" })
                {
                    Image shell = Find<Image>(side + " " + part);
                    Assert.That(shell.transform.parent, Is.SameAs(track), shell.name);
                    Assert.That(shell.raycastTarget, Is.False, shell.name);
                    Assert.That(shell.color.r, Is.GreaterThan(.9f), shell.name);
                    Assert.That(shell.color.g, Is.GreaterThan(.9f), shell.name);
                    Assert.That(shell.color.b, Is.GreaterThan(.9f), shell.name);
                }
                Assert.That(track.GetComponent<BattleDefenseHud>().DisplayedGuard,
                    Is.EqualTo(side == "Player" ? battle.PlayerBlock : battle.EnemyBlock));
            }
        }

        [Test]
        public void RetainedRefresh_ReusesDecisionObjectsAndRestoresLayoutWithCurrentResourceValues()
        {
            const NavalBattleStage stage = NavalBattleStage.Naval;
            CreatePreview(stage, 600f);
            CoreBattleState battle = controller.Session.Battle;
            string[] retainedNames = { "Battle Card", "Battle Hand", "Player Resource HUD", "Enemy Resource HUD",
                "Battle Buff Panel", "Retreat Battle", "End Battle Turn", "Enemy Intent HUD",
                "Battle Ram", "Battle Board", "Battle Ram Condition", "Battle Board Condition",
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" };
            RectTransform[] retained = retainedNames.Select(Find<RectTransform>).ToArray();
            BattleHealthSegmentsGraphic[] grids = host.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true);
            int childCount = host.GetComponentsInChildren<Transform>(true).Length;
            foreach (string name in new[] { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Intent HUD", "Enemy Resource HUD", "End Battle Turn",
                "Battle Ram", "Battle Board", "Battle Ram Condition", "Battle Board Condition" })
                Find<RectTransform>(name).anchoredPosition = Vector2.zero;
            battle.Energy = 1;
            battle.PlayerBlock = 5;
            battle.EnemyBlock = 7;
            controller.Session.Cargo.Powder = 23;
            battle.DistanceMeters = 80f;
            battle.EnemyBurnTurns = 2;
            MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView", PrivateInstance);
            Assert.That(refresh, Is.Not.Null);
            Assert.That(refresh.Invoke(controller, null), Is.EqualTo(true), "Exercise the existing retained refresh instead of rebuilding the view.");
            Canvas.ForceUpdateCanvases();

            Assert.That(controller.Session.Battle, Is.SameAs(battle));
            for (int i = 0; i < retainedNames.Length; i++) Assert.That(Find<RectTransform>(retainedNames[i]), Is.SameAs(retained[i]), retainedNames[i]);
            Assert.That(host.GetComponentsInChildren<Transform>(true), Has.Length.EqualTo(childCount));
            Assert.That(host.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true), Is.EqualTo(grids));
            Assert.That(Find<Text>("Resource 能 Value").text, Is.EqualTo("1/" + battle.MaxEnergy));
            Assert.That(Find<Text>("Resource 药 Value").text, Is.EqualTo("23"));
            CardOrnamentGraphic powder = Find<CardOrnamentGraphic>("Resource 药 Icon");
            Assert.That(powder.Kind, Is.EqualTo(CardOrnamentKind.Gunpowder));
            Assert.That(powder.rectTransform.rect.width, Is.EqualTo(powder.rectTransform.rect.height));
            Assert.That(powder.GetComponentInChildren<Text>(), Is.Null, "A red crystal replaces the circular fire glyph.");
            Assert.That(Find<Text>("Resource 盾 Value").text, Is.EqualTo("7"));
            Assert.That(Find<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + controller.Session.GetEnemyIntent()));
            AssertLayout(stage);
        }

        private void CreatePreview(NavalBattleStage stage, float distance, NavalBattleAlignment alignment = NavalBattleAlignment.HeadOn)
        {
            host = new GameObject("Battle Decision Layout Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            Assert.That(controller.HasCompleteEditorPreview, Is.True);
            CoreBattleState battle = controller.Session.Battle;
            // Change only this disposable preview's in-memory combat fixture.
            // No gameplay action or persistence callback participates in layout setup.
            battle.Stage = stage;
            battle.DistanceMeters = distance;
            battle.Alignment = alignment;
            battle.Profile = new CoreBattleProfile(CoreBossKind.None, CoreBattleArchetype.Raider,
                battle.EnemyMaxHull, 8, 11, 0, 0, battle.EnemyOrdinaryCrew, 1f, 1f, 1d, 120f);
            battle.PlayerBlock = 12;
            battle.EnemyBlock = 5;
            battle.EnemyBurnTurns = stage == NavalBattleStage.Naval && distance < 120f ? 2 : 0;
            MethodInfo render = typeof(CoreGameplayDemoController).GetMethod("Render", PrivateInstance);
            Assert.That(render, Is.Not.Null);
            render.Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
            Assert.That(controller.Session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(controller.Session.Battle, Is.SameAs(battle));
        }

        private void AssertLayout(NavalBattleStage stage)
        {
            RectTransform root = Find<RectTransform>("Battle Card");
            RectTransform surface = Find<RectTransform>("Battle Overlay");
            Rect hand = In(root, Find<RectTransform>("Battle Hand"));
            Rect card = In(root, Find<RectTransform>("Battle Card 0"));
            Rect player = In(root, Find<RectTransform>("Player Resource HUD"));
            Rect buffs = In(root, Find<RectTransform>("Battle Buff Panel"));
            Rect retreat = In(root, Find<RectTransform>("Retreat Battle"));
            Assert.That(hand.width, Is.GreaterThan(1000f), "No collapsed preview geometry.");
            Assert.That(Find<RectTransform>("Battle Hand").sizeDelta, Is.EqualTo(CardFaceLayout.HandSize));
            Assert.That(Find<RectTransform>("Battle Hand").anchoredPosition, Is.EqualTo(CardFaceLayout.HandPosition));
            foreach (string name in new[] { "Battle Hand", "Player Resource HUD", "Enemy Resource HUD", "Battle Buff Panel",
                "Retreat Battle", "End Battle Turn", "Enemy Intent HUD", "Player Ship Art", "Enemy Ship Art",
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" })
                Assert.That(Find<RectTransform>(name).parent, Is.SameAs(root), name + " keeps its runtime lookup path");
            foreach (Rect panel in new[] { player, buffs, retreat }) AssertSeparate(panel, hand, "Left decision column and hand");
            Assert.That(player.yMin - buffs.yMax, Is.EqualTo(8f).Within(.01f));
            Assert.That(buffs.yMin - retreat.yMax, Is.EqualTo(8f).Within(.01f));
            Assert.That(Vector2.Distance(player.size, new Vector2(230f, 120f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(buffs.size, player.size), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(player.center, new Vector2(-735f, -196f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(buffs.center, new Vector2(-735f, -324f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(retreat.size, new Vector2(230f, 44f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(retreat.center, new Vector2(-735f, -414f)), Is.LessThanOrEqualTo(.01f));
            foreach (CardSchoolFrameGraphic frame in Find<RectTransform>("Battle Hand").GetComponentsInChildren<CardSchoolFrameGraphic>())
            {
                Rect fullFrame = In(root, frame.rectTransform);
                AssertContains(hand, fullFrame, "Complete frame including all cost corners stays in the hand");
                foreach (Rect panel in new[] { player, buffs, retreat })
                    AssertSeparate(panel, fullFrame, "Fixed decision column does not cover a complete card frame");
            }
            AssertContains(hand, card, "The scaled card body stays inside its owning hand");
            foreach (string name in new[] { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Resource HUD", "End Battle Turn" })
            {
                RectTransform panel = Find<RectTransform>(name);
                foreach (Text text in panel.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.fontSize, Is.EqualTo(18), text.name + " uses the common side-panel font size");
                    Assert.That(text.resizeTextForBestFit, Is.False, text.name + " keeps the common font size");
                    AssertContains(panel.rect, In(panel, text.rectTransform), text.name + " stays inside its panel");
                    AssertTextFits(text, 18);
                }
            }
            RectTransform resourceRoot = Find<RectTransform>("Player Resource HUD");
            foreach (string glyph in new[] { "能", "药" })
            {
                Rect icon = In(resourceRoot, resourceRoot.Find("Resource " + glyph + " Icon") as RectTransform);
                Rect value = In(resourceRoot, resourceRoot.Find("Resource " + glyph + " Value") as RectTransform);
                Assert.That(value.xMin - icon.xMax, Is.GreaterThanOrEqualTo(4f),
                    "Readable resource numbers need a clear gap after their circular icon.");
                AssertContains(resourceRoot.rect, value, glyph + " value stays inside its panel");
            }

            Rect intent = In(root, Find<RectTransform>("Enemy Intent HUD"));
            Rect enemy = In(root, Find<RectTransform>("Enemy Resource HUD"));
            Rect endTurn = In(root, Find<RectTransform>("End Battle Turn"));
            foreach (var pair in new[] { new[] { player, intent }, new[] { buffs, enemy }, new[] { retreat, endTurn } })
            {
                Assert.That(Vector2.Distance(pair[0].size, pair[1].size), Is.LessThanOrEqualTo(.01f), "Opposite rows have equal sizes.");
                Assert.That(pair[0].center.y, Is.EqualTo(pair[1].center.y).Within(.01f), "Opposite rows share their vertical position.");
                Assert.That(pair[0].center.x, Is.EqualTo(-pair[1].center.x).Within(.01f), "Opposite rows mirror about the cards.");
                AssertSeparate(pair[1], hand, "Right decision column and hand");
            }
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "Player Battle Hull Bar Track",
                "Enemy Battle Hull Bar Track", "Player Battle Name Board", "Enemy Battle Name Board", "Battle Hand",
                "End Battle Turn", "Enemy Resource HUD" })
                AssertSeparate(intent, In(root, Find<RectTransform>(name)), "Enemy intent and " + name);
            foreach (string name in new[] { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Intent HUD", "End Battle Turn", "Enemy Resource HUD" })
                AssertContains(surface.rect, In(surface, Find<RectTransform>(name)), name + " fits the actual scaled battle surface");
            if (stage == NavalBattleStage.Naval)
            {
                Rect ram = In(root, Find<RectTransform>("Battle Ram"));
                Rect board = In(root, Find<RectTransform>("Battle Board"));
                Assert.That(ram.center.x, Is.EqualTo(board.center.x).Within(.01f), "The actions share the right-side column.");
                Assert.That(ram.width, Is.EqualTo(board.width).Within(.01f));
                Assert.That(ram.yMin, Is.GreaterThan(board.yMax), "Ram precedes boarding in the right-side action stack.");
                Assert.That(board.yMin, Is.GreaterThan(intent.yMax), "Both actions remain above the aligned information panels.");
                AssertSeparate(ram, board, "Ram and boarding remain separate actions");
                foreach (Rect action in new[] { ram, board })
                {
                    Assert.That(action.xMin, Is.GreaterThanOrEqualTo(intent.xMin - .01f), "Actions fit inside the information-column width.");
                    Assert.That(action.xMax, Is.LessThanOrEqualTo(intent.xMax + .01f));
                    Assert.That(action.center.x, Is.GreaterThan(0f), "Actions remain on the right.");
                    Assert.That(action.yMin, Is.GreaterThan(hand.yMax), "Actions stay above the hand.");
                    foreach (Rect panel in new[] { intent, enemy, endTurn, hand })
                        AssertSeparate(action, panel, "Naval actions and decision column");
                }
                Rect ramCondition = In(root, Find<RectTransform>("Battle Ram Condition"));
                Rect boardCondition = In(root, Find<RectTransform>("Battle Board Condition"));
                Assert.That(ramCondition.height, Is.EqualTo(28f).Within(.01f));
                Assert.That(boardCondition.height, Is.EqualTo(28f).Within(.01f));
                Assert.That(ramCondition.yMax, Is.LessThan(ram.yMin));
                Assert.That(ramCondition.yMin, Is.GreaterThan(board.yMax));
                Assert.That(boardCondition.yMax, Is.LessThan(board.yMin));
                Assert.That(boardCondition.yMin, Is.GreaterThan(intent.yMax));
                foreach (string name in new[] { "Battle Ram", "Battle Board", "Battle Ram Condition", "Battle Board Condition" })
                {
                    Rect control = In(root, Find<RectTransform>(name));
                    foreach (string side in new[] { "Player", "Enemy" })
                        foreach (string part in new[] { " Battle Hull Bar Track", " Armor Frame 3" })
                            AssertSeparate(control, In(root, Find<RectTransform>(side + part)), name + " clears " + side + part);
                    Text text = Find<RectTransform>(name).GetComponent<Text>() ?? Find<RectTransform>(name).GetComponentInChildren<Text>();
                    AssertTextFits(text, text.fontSize);
                }
            }
            foreach (string name in new[] { "Battle Card Cost Rule Panel", "Battle Card Cost Rule", "Battle Status", "Battle Collision Quality" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False,
                    name + " is removed from the battle view");
            Text label = Find<Text>("Enemy Intent Text");
            Assert.That(label.resizeTextMinSize, Is.EqualTo(14));
            Assert.That(label.resizeTextMaxSize, Is.EqualTo(18));
            AssertContains(intent, In(root, label.rectTransform), "Intent text stays in its wood panel");
            AssertTextFits(label, 14);
            foreach (string name in new[] { "Player Resource HUD", "Enemy Resource HUD", "Battle Buff Panel", "Enemy Intent HUD" })
                foreach (Graphic decoration in Find<RectTransform>(name).GetComponentsInChildren<Graphic>(true))
                    Assert.That(decoration.raycastTarget, Is.False, decoration.name + " cannot intercept combat input");
        }

        private static void AssertTextFits(Text label, int fontSize)
        {
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = fontSize;
            float fullHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            Assert.That(fullHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f),
                label.name + " must show its complete text at " + fontSize + "px.");
        }

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(item => item.name == name);

        private static Rect In(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            for (int i = 0; i < corners.Length; i++) corners[i] = parent.InverseTransformPoint(corners[i]);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
        }

        private static void AssertSeparate(Rect first, Rect second, string context)
        {
            float overlapX = Mathf.Min(first.xMax, second.xMax) - Mathf.Max(first.xMin, second.xMin);
            float overlapY = Mathf.Min(first.yMax, second.yMax) - Mathf.Max(first.yMin, second.yMin);
            Assert.That(overlapX <= .1f || overlapY <= .1f, Is.True, context + ": " + first + " / " + second);
        }

        private static void AssertContains(Rect outer, Rect inner, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .1f), context);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .1f), context);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .1f), context);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .1f), context);
        }
    }
}
