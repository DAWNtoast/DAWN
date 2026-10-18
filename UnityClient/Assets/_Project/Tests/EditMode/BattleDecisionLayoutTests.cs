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
        [TestCase(NavalBattleStage.Naval, 100f, NavalBattleAlignment.EnemyBroadside)]
        [TestCase(NavalBattleStage.Naval, 100f, NavalBattleAlignment.Parallel)]
        [TestCase(NavalBattleStage.Boarding, 0f, NavalBattleAlignment.HeadOn)]
        public void DecisionPanels_KeepReadableControlsSeparateFromTheHandAndCombatants(NavalBattleStage stage, float distance, NavalBattleAlignment alignment)
        {
            CreatePreview(stage, distance, alignment);
            CoreBattleState battle = controller.Session.Battle;
            AssertLayout(stage);
            Assert.That(Find<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + controller.Session.GetEnemyIntent()));
            if (stage == NavalBattleStage.Naval && distance == 100f && alignment == NavalBattleAlignment.EnemyBroadside)
            {
                Assert.That(controller.Session.EnemyWillRam, Is.True, "The near fixture must include the complete ram warning.");
                Assert.That(Find<Text>("Enemy Intent Text").text, Does.Contain("灼烧").And.Contain("先手"));
            }
            if (stage == NavalBattleStage.Naval && alignment == NavalBattleAlignment.Parallel)
            {
                Assert.That(controller.Session.CanBoard(out _), Is.True);
                Assert.That(Find<Text>("Battle Board Condition").text,
                    Does.Contain("上下左右相邻").And.Contain("船头同向").And.Contain("逆向不可").And.Contain("不耗能量"));
            }
            foreach (string side in new[] { "Player", "Enemy" })
            {
                RectTransform track = Find<RectTransform>(side + " Battle Hull Bar Track");
                Image fill = Find<Image>(side + " Battle Hull Bar");
                Assert.That(fill.transform.parent, Is.SameAs(track));
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                BattleHealthSegmentsGraphic[] grids = fill.GetComponentsInChildren<BattleHealthSegmentsGraphic>(true);
                Assert.That(grids, Is.Empty, "The current health bar is continuous, without retired ten-health dividers.");
                int expectedMaximum = side == "Player"
                    ? (stage == NavalBattleStage.Boarding ? battle.PlayerMaxBoardingHealth : controller.Session.MaxHull)
                    : (stage == NavalBattleStage.Boarding ? battle.EnemyMaxBoardingHealth : battle.EnemyMaxHull);
                Assert.That(track.GetComponent<BattleDefenseHud>().MaximumHull, Is.EqualTo(expectedMaximum));
                Assert.That(fill.raycastTarget, Is.False);
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
        public void EnemyIntent_LeavesTheCompleteShipPortraitClearAtEveryHeading()
        {
            CreatePreview(NavalBattleStage.Naval, 600f);
            RectTransform root = Find<RectTransform>("Battle Card");
            RectTransform ship = Find<RectTransform>("Enemy Ship Art");
            RectTransform intent = Find<RectTransform>("Enemy Intent Icons");
            Rect intentBounds = In(root, intent);
            for (int angle = 0; angle < 360; angle += 15)
            {
                ship.localRotation = Quaternion.Euler(0f, 0f, angle);
                AssertSeparate(intentBounds, In(root, ship),
                    "The complete intention row must leave the rotating portrait clear at " + angle + " degrees.");
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
                "Player Battle Hull Bar Track", "Enemy Battle Hull Bar Track", "Battle Compact Resources",
                "Battle Draw Pile", "Battle Discard Pile", "Enemy Intent Icons" };
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
            SetDecisionTacticalPose(battle, 100f, NavalBattleAlignment.EnemyBroadside);
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
            Assert.That(Find<RectTransform>("Resource Energy").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo("1/" + battle.MaxEnergy), "The visible compact energy badge also refreshes.");
            Assert.That(Find<RectTransform>("Resource Powder").Find("Amount").GetComponent<Text>().text,
                Is.EqualTo("23"), "The visible compact supply badge also refreshes.");
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
            if (stage == NavalBattleStage.Boarding)
            {
                battle.PlayerBoardingHealth = battle.PlayerMaxBoardingHealth = 200;
                battle.EnemyBoardingHealth = battle.EnemyMaxBoardingHealth = 200;
            }
            SetDecisionTacticalPose(battle, distance, alignment);
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

        private static void SetDecisionTacticalPose(CoreBattleState battle, float distance, NavalBattleAlignment alignment)
        {
            MethodInfo pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", PrivateInstance);
            Assert.That(pose, Is.Not.Null);
            // Near enemy intent uses a bow-to-side ram; parallel neighbors use
            // the same heading so the boarding control is actually available.
            pose.Invoke(battle, new object[] { true, new VoyagePoint(-distance, 0f),
                alignment == NavalBattleAlignment.EnemyBroadside ? 90f : 0f });
            pose.Invoke(battle, new object[] { false, new VoyagePoint(0f, 0f),
                alignment == NavalBattleAlignment.Parallel ? 0f : 180f });
            typeof(CoreBattleState).GetProperty("NavalActionPoints").SetValue(battle, 1);
            battle.Alignment = alignment;
        }

        private void AssertLayout(NavalBattleStage stage)
        {
            bool boarding = stage == NavalBattleStage.Boarding;
            RectTransform root = Find<RectTransform>("Battle Card");
            RectTransform surface = Find<RectTransform>("Battle Overlay");
            RectTransform handRoot = Find<RectTransform>("Battle Hand");
            Rect hand = In(root, handRoot);
            Assert.That(handRoot.sizeDelta, Is.EqualTo(new Vector2(1120f, 200f)));
            Assert.That(handRoot.anchoredPosition, Is.EqualTo(new Vector2(0f, -350f)));
            Assert.That(handRoot.parent, Is.SameAs(root));

            // These retained bindings still receive current data, but their old
            // panel geometry must not constrain the visible compact controls.
            foreach (string name in new[] { "Player Resource HUD", "Enemy Resource HUD",
                "Battle Buff Panel", "Enemy Intent HUD" })
            {
                RectTransform retired = Find<RectTransform>(name);
                Assert.That(retired.parent, Is.SameAs(root));
                Assert.That(retired.gameObject.activeInHierarchy, Is.False, name + " is replaced by compact icons.");
            }

            RectTransform resources = Find<RectTransform>("Battle Compact Resources");
            RectTransform draw = Find<RectTransform>("Battle Draw Pile");
            RectTransform discard = Find<RectTransform>("Battle Discard Pile");
            RectTransform endTurn = Find<RectTransform>("End Battle Turn");
            var controls = new System.Collections.Generic.List<RectTransform> { resources, draw, discard, endTurn };
            Assert.That(In(root, resources).xMax, Is.LessThan(hand.xMin), "Resources stay left of the hand.");
            Assert.That(In(root, draw).xMax, Is.LessThan(hand.xMin));
            Assert.That(In(root, discard).xMin, Is.GreaterThan(hand.xMax));
            Assert.That(In(root, endTurn).xMin, Is.GreaterThan(hand.xMax));
            Assert.That(In(root, resources).yMin, Is.GreaterThan(In(root, draw).yMax),
                "Resource rows remain above the clickable draw pile.");
            Assert.That(In(root, endTurn).yMin, Is.GreaterThan(In(root, discard).yMax),
                "Ending the turn must not obscure the discard-pile button.");

            RectTransform retreat = Find<RectTransform>("Retreat Battle");
            Assert.That(retreat.gameObject.activeInHierarchy, Is.EqualTo(!boarding),
                "Retreat is hidden during boarding.");
            if (!boarding)
            {
                controls.Add(retreat);
                Assert.That(In(root, retreat).yMin, Is.GreaterThan(0f), "Retreat occupies the upper-left corner.");
            }

            if (stage == NavalBattleStage.Naval)
            {
                RectTransform ram = Find<RectTransform>("Battle Ram");
                RectTransform board = Find<RectTransform>("Battle Board");
                RectTransform movement = Find<RectTransform>("Naval movement controls");
                controls.AddRange(new[] { ram, board, movement });
                Rect ramBounds = In(root, ram), boardBounds = In(root, board), movementBounds = In(root, movement);
                Assert.That(ramBounds.center.y, Is.EqualTo(boardBounds.center.y).Within(.1f),
                    "The two close-contact actions share one compact row.");
                Assert.That(ramBounds.xMax, Is.LessThan(boardBounds.xMin));
                Assert.That(ramBounds.yMin, Is.GreaterThan(movementBounds.yMax));
                Assert.That(movementBounds.yMin, Is.GreaterThan(In(root, endTurn).yMax));
                foreach (string name in new[] { "Battle Ram Condition", "Battle Board Condition" })
                    Assert.That(Find<RectTransform>(name).gameObject.activeInHierarchy, Is.False,
                        "Action details are available from hover without restoring the retired text panels.");
            }

            for (int index = 0; index < controls.Count; index++)
            {
                RectTransform control = controls[index];
                Assert.That(control.parent, Is.SameAs(root), control.name + " keeps its runtime lookup path.");
                Assert.That(control.gameObject.activeInHierarchy, Is.True, control.name);
                Rect bounds = In(root, control);
                AssertSeparate(bounds, hand, control.name + " leaves the hand clickable.");
                AssertContains(surface.rect, In(surface, control), control.name + " fits the battle surface.");
                for (int previous = 0; previous < index; previous++)
                    AssertSeparate(bounds, In(root, controls[previous]), control.name + " and " + controls[previous].name);
            }
            foreach (CardSchoolFrameGraphic frame in handRoot.GetComponentsInChildren<CardSchoolFrameGraphic>())
            {
                Rect bounds = In(root, frame.rectTransform);
                AssertRestingFanCardBounds(hand, bounds);
                foreach (RectTransform control in controls)
                    AssertSeparate(In(root, control), bounds, control.name + " does not cover a resting card.");
            }
            AssertRestingFanCardBounds(hand, In(root, Find<RectTransform>("Battle Card 0")));

            Rect? previousRow = null;
            for (int line = 1; line <= 3; line++)
            {
                RectTransform row = Find<RectTransform>("Battle Resource Line " + line);
                Assert.That(row.parent, Is.SameAs(resources));
                Rect rowBounds = In(resources, row);
                AssertContains(resources.rect, rowBounds, row.name + " stays in the resource area.");
                if (previousRow.HasValue) AssertSeparate(previousRow.Value, rowBounds, "Resource rows remain distinct.");
                previousRow = rowBounds;
                Assert.That(row.childCount, Is.EqualTo(line == 1 ? 2 : 4));
                for (int slot = 0; slot < row.childCount; slot++)
                {
                    RectTransform badge = (RectTransform)row.GetChild(slot);
                    AssertContains(row.rect, In(row, badge), badge.name + " stays in its resource row.");
                    if (slot > 0) AssertSeparate(In(row, (RectTransform)row.GetChild(slot - 1)), In(row, badge),
                        "Adjacent resource hit areas must not overlap.");
                    Text amount = badge.Find("Amount").GetComponent<Text>();
                    Assert.That(amount.fontSize, Is.GreaterThanOrEqualTo(19));
                    Assert.That(amount.raycastTarget, Is.False);
                    AssertTextFits(amount, amount.fontSize);
                }
            }

            RectTransform intent = Find<RectTransform>("Enemy Intent Icons");
            Assert.That(intent.parent, Is.SameAs(root));
            Assert.That(intent.GetComponentsInChildren<BattleCommandIconGraphic>(false), Is.Not.Empty);
            AssertContains(surface.rect, In(surface, intent), "Enemy intentions remain on the battle surface.");
            foreach (string side in new[] { "Player", "Enemy" })
            {
                RectTransform actor = Find<RectTransform>(side + " Ship Art");
                RectTransform health = Find<RectTransform>(side + " Battle Hull Bar Track");
                Assert.That(actor.parent, Is.SameAs(root));
                Assert.That(health.parent, Is.SameAs(root));
                foreach (BattleCommandIconGraphic icon in intent.GetComponentsInChildren<BattleCommandIconGraphic>(false))
                {
                    // The row reserves room for future intentions; only its
                    // populated badge hit areas occupy the visible scene.
                    Rect badge = In(root, (RectTransform)icon.transform.parent);
                    AssertSeparate(badge, In(root, actor), "Intent badges keep " + side + " combatant visible.");
                    AssertSeparate(badge, In(root, health), "Intent badges keep " + side + " health visible.");
                    AssertSeparate(badge, hand, "Intent badges keep cards readable.");
                }
            }
            AssertSeparate(In(root, intent), hand, "Intent icons keep cards readable.");

            foreach (RectTransform control in controls)
                foreach (Text text in control.GetComponentsInChildren<Text>(false))
                {
                    Assert.That(text.raycastTarget, Is.False, text.name + " does not intercept button input.");
                    AssertTextFits(text, text.fontSize);
                }
            foreach (string name in new[] { "Battle Card Cost Rule Panel", "Battle Card Cost Rule",
                "Battle Status", "Battle Collision Quality" })
                Assert.That(host.GetComponentsInChildren<Transform>(true).Any(item => item.name == name), Is.False,
                    name + " is removed from the battle view.");
        }

        private static void AssertRestingFanCardBounds(Rect hand, Rect card)
        {
            // All battle cards tuck below the viewport at rest; their full
            // readable inspection pose is covered by the fan PlayMode tests.
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(hand.xMin - .1f), "The complete resting card stays within the hand's width.");
            Assert.That(card.xMax, Is.LessThanOrEqualTo(hand.xMax + .1f), "The complete resting card stays within the hand's width.");
            // The hand is a layout anchor, not a clipping rectangle. Rotation
            // may lift a card above it; actual controls are checked separately.
            Assert.That(card.yMax - Mathf.Max(card.yMin, hand.yMin), Is.GreaterThanOrEqualTo(card.height * .5f - .1f),
                "At least half of each resting card remains visible for selection.");
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
