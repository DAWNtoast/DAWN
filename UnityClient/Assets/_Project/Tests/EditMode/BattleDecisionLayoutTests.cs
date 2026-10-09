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

        [TestCase(NavalBattleStage.Preparation, 600f)]
        [TestCase(NavalBattleStage.Naval, 600f)]
        [TestCase(NavalBattleStage.Naval, 80f)]
        [TestCase(NavalBattleStage.Boarding, 0f)]
        public void DecisionPanels_KeepReadableControlsSeparateFromTheHandAndCombatants(NavalBattleStage stage, float distance)
        {
            CreatePreview(stage, distance);
            CoreBattleState battle = controller.Session.Battle;
            AssertLayout(stage);
            Assert.That(Find<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + controller.Session.GetEnemyIntent()));
            if (stage == NavalBattleStage.Naval && distance < 120f)
            {
                Assert.That(controller.Session.EnemyWillRam, Is.True, "The near fixture must include the complete ram warning.");
                Assert.That(Find<Text>("Enemy Intent Text").text, Does.Contain("灼烧").And.Contain("先手"));
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
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" };
            RectTransform[] retained = retainedNames.Select(Find<RectTransform>).ToArray();
            BattleHealthSegmentsGraphic[] grids = host.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true);
            int childCount = host.GetComponentsInChildren<Transform>(true).Length;
            foreach (string name in new[] { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Intent HUD" })
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
            Assert.That(Find<Text>("Resource 药 Glyph").text, Is.EqualTo("火"));
            Assert.That(Find<Text>("Resource 盾 Value").text, Is.EqualTo("7"));
            Assert.That(Find<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + controller.Session.GetEnemyIntent()));
            AssertLayout(stage);
        }

        [TestCase(0, NavalBattleStage.Preparation)] [TestCase(1, NavalBattleStage.Preparation)] [TestCase(2, NavalBattleStage.Preparation)]
        [TestCase(0, NavalBattleStage.Naval)] [TestCase(1, NavalBattleStage.Naval)] [TestCase(2, NavalBattleStage.Naval)]
        [TestCase(0, NavalBattleStage.Boarding)] [TestCase(1, NavalBattleStage.Boarding)] [TestCase(2, NavalBattleStage.Boarding)]
        public void ReadingLevelsKeepIntentScrollableAndResourcesOutsideTheHand(int level, NavalBattleStage stage)
        {
            int? previous = GameReadability.OverrideLevel;
            try
            {
                GameReadability.OverrideLevel = level;
                CreatePreview(stage, stage == NavalBattleStage.Boarding ? 0f : 80f);
                RectTransform root = Find<RectTransform>("Battle Card");
                RectTransform surface = Find<RectTransform>("Battle Overlay");
                foreach (string side in new[] { "Player", "Enemy" })
                {
                    Text name = Find<Text>(side + " Battle Name");
                    RectTransform board = Find<RectTransform>(side + " Battle Name Board");
                    AssertContains(surface.rect, In(surface, board), side + " crew callout remains on screen after battle scaling");
                    AssertContains(board.rect, In(board, name.rectTransform), side + " crew label stays inside its board");
                    Assert.That(name.preferredHeight, Is.LessThanOrEqualTo(name.rectTransform.rect.height + .5f), name.name);
                    Assert.That(name.resizeTextForBestFit, Is.False, name.name);
                }
                Rect hand = In(root, Find<RectTransform>("Battle Hand"));
                string[] names = { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Intent HUD", "End Battle Turn", "Enemy Resource HUD" };
                for (int index = 0; index < names.Length; index++)
                {
                    Rect box = In(root, Find<RectTransform>(names[index]));
                    AssertSeparate(box, hand, names[index]);
                    for (int other = index + 1; other < names.Length; other++)
                        AssertSeparate(box, In(root, Find<RectTransform>(names[other])), names[index] + " and " + names[other]);
                }
                foreach (string name in new[] { "Resource 能 Value", "Resource 药 Value", "Resource 回 Value", "Resource 盾 Value", "Enemy Intent Text" })
                {
                    Text label = Find<Text>(name);
                    Assert.That(label.fontSize, Is.EqualTo(GameReadability.FontSize(22)), name);
                    Assert.That(label.resizeTextForBestFit, Is.False, name);
                    Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), name);
                }
                ScrollRect intent = Find<Text>("Enemy Intent Text").GetComponentInParent<ScrollRect>();
                Assert.That(intent.viewport.GetComponent<Image>().raycastTarget, Is.True);
                intent.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases();
                Assert.That(In(intent.viewport, intent.content).yMin, Is.EqualTo(intent.viewport.rect.yMin).Within(.5f));
            }
            finally { GameReadability.OverrideLevel = previous; }
        }

        private void CreatePreview(NavalBattleStage stage, float distance)
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
            battle.Alignment = NavalBattleAlignment.HeadOn;
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
            Rect player = In(root, Find<RectTransform>("Player Resource HUD"));
            Rect buffs = In(root, Find<RectTransform>("Battle Buff Panel"));
            Rect retreat = In(root, Find<RectTransform>("Retreat Battle"));
            Assert.That(hand.width, Is.GreaterThan(1000f), "No collapsed preview geometry.");
            foreach (string name in new[] { "Battle Hand", "Player Resource HUD", "Enemy Resource HUD", "Battle Buff Panel",
                "Retreat Battle", "End Battle Turn", "Enemy Intent HUD", "Player Ship Art", "Enemy Ship Art",
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track" })
                Assert.That(Find<RectTransform>(name).parent, Is.SameAs(root), name + " keeps its runtime lookup path");
            foreach (Rect panel in new[] { player, buffs, retreat }) AssertSeparate(panel, hand, "Left decision column and hand");
            Assert.That(player.yMin - buffs.yMax, Is.GreaterThanOrEqualTo(8f));
            Assert.That(buffs.yMin - retreat.yMax, Is.GreaterThanOrEqualTo(20f));
            Assert.That(Vector2.Distance(player.size, new Vector2(230f, 132f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Vector2.Distance(player.center, new Vector2(-735f, -200f)), Is.LessThanOrEqualTo(.01f));
            Assert.That(Find<Text>("Resource 能 Value").fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(22)));
            Assert.That(Find<Text>("Resource 药 Value").fontSize, Is.GreaterThanOrEqualTo(GameReadability.FontSize(22)));
            RectTransform resourceRoot = Find<RectTransform>("Player Resource HUD");
            foreach (string glyph in new[] { "能", "药" })
            {
                Rect icon = In(resourceRoot, resourceRoot.Find("Resource " + glyph + " Icon") as RectTransform);
                Rect value = In(resourceRoot, resourceRoot.Find("Resource " + glyph + " Value") as RectTransform);
                // Canvas/world round trips can lose a fraction of a pixel.
                Assert.That(value.xMin - icon.xMax, Is.GreaterThanOrEqualTo(4f - 0.01f),
                    "Readable resource numbers need a clear gap after their circular icon.");
                AssertContains(resourceRoot.rect, value, glyph + " value stays inside its panel");
            }

            Rect intent = In(root, Find<RectTransform>("Enemy Intent HUD"));
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art", "Player Battle Hull Bar Track",
                "Enemy Battle Hull Bar Track", "Player Battle Name Board", "Enemy Battle Name Board", "Battle Hand",
                "End Battle Turn", "Enemy Resource HUD" })
                AssertSeparate(intent, In(root, Find<RectTransform>(name)), "Enemy intent and " + name);
            foreach (string name in new[] { "Player Resource HUD", "Battle Buff Panel", "Retreat Battle", "Enemy Intent HUD", "End Battle Turn", "Enemy Resource HUD" })
                AssertContains(surface.rect, In(surface, Find<RectTransform>(name)), name + " fits the actual scaled battle surface");
            if (stage == NavalBattleStage.Boarding)
            {
                Assert.That(Vector2.Distance(intent.size, new Vector2(230f, 104f)), Is.LessThanOrEqualTo(.01f));
                Assert.That(Vector2.Distance(intent.center, new Vector2(735f, -186f)), Is.LessThanOrEqualTo(.01f));
                Assert.That(Find<RectTransform>("End Battle Turn").anchoredPosition.y, Is.EqualTo(-278f).Within(.01f));
            }
            else
            {
                Assert.That(intent.width, Is.EqualTo(210f).Within(.01f));
                Assert.That(intent.height, Is.GreaterThanOrEqualTo(96f - .01f));
                Assert.That(intent.center.x, Is.EqualTo(780f).Within(.01f));
                Assert.That(intent.yMin, Is.EqualTo(-82f).Within(.01f));
            }
            Text label = Find<Text>("Enemy Intent Text");
            Assert.That(label.resizeTextMinSize, Is.GreaterThanOrEqualTo(14));
            Assert.That(label.resizeTextMaxSize, Is.GreaterThanOrEqualTo(15));
            Assert.That(label.fontSize, Is.EqualTo(GameReadability.FontSize(22)));
            Assert.That(label.resizeTextForBestFit, Is.False);
            AssertContains(intent, In(root, label.GetComponentInParent<ScrollRect>().viewport), "Intent viewport stays in its wood panel");
            TextGenerationSettings settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = 14;
            float fullHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
            Assert.That(fullHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .5f), "Complete intent must remain readable.");
            foreach (string name in new[] { "Player Resource HUD", "Enemy Resource HUD", "Battle Buff Panel", "Enemy Intent HUD" })
                foreach (Graphic decoration in Find<RectTransform>(name).GetComponentsInChildren<Graphic>(true))
                    Assert.That(decoration.raycastTarget, Is.EqualTo(decoration.GetComponent<ScrollRect>() != null || decoration.GetComponentInParent<Scrollbar>() != null), decoration.name + " only the reading viewport may receive wheel input");
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
