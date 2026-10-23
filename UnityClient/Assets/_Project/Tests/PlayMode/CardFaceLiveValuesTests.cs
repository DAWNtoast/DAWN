using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator CardFaceLiveValues_CannonRetainedRefreshChangesOnlyNumbers()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_chain_shot", "card_load_shot", "card_brace");
            SetResponsiveDrawPile(session);
            session.Battle.DistanceMeters = 100;
            session.Cargo.Powder = session.Cargo.Cannonballs = 20;
            RenderResponsiveBattle(controller);
            yield return null;
            Button card = RequiredResponsive<Button>("Battle Card 0");
            int retainedId = card.GetInstanceID();
            Text face = card.transform.Find("Label").GetComponent<Text>();
            CoreBattleCard definition = session.GetBattleCard(ResponsiveCardIndex("card_chain_shot"));
            int beforeDamage = session.GetBattleCardDamage(definition, NavalBattleStage.Naval);
            string before = ForgeVisibleText(face.text);
            Assert.That(before, Is.EqualTo("范围1-4格：造成" + beforeDamage + "点船体伤害"));
            AssertCannonFaceCostIcons(card);
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_01_Cannon_Before.png",
                "无装填增益时炮击卡面只有一个船体伤害值", "Battle Card");

            session.Battle.PlayerLoadedShot = 4;
            session.Battle.PlayerFireRounds = 2;
            MethodInfo refresh = typeof(CoreGameplayDemoController).GetMethod("TryRefreshRetainedBattleView", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            Assert.That((bool)refresh.Invoke(controller, null), Is.True);
            yield return null;
            card = RequiredResponsive<Button>("Battle Card 0");
            Assert.That(card.GetInstanceID(), Is.EqualTo(retainedId), "Live values update the existing card.");
            face = card.transform.Find("Label").GetComponent<Text>();
            int afterDamage = session.GetBattleCardDamage(definition, NavalBattleStage.Naval);
            Assert.That(afterDamage, Is.EqualTo(beforeDamage + 4));
            string after = ForgeVisibleText(face.text);
            Assert.That(after, Is.EqualTo("范围1-4格：造成" + afterDamage + "点船体伤害"));
            AssertCannonFaceCostIcons(card);
            Assert.That(Regex.Replace(after, @"\d+", "#"), Is.EqualTo(Regex.Replace(before, @"\d+", "#")));
            Assert.That(after, Does.Not.Contain("实际").And.Not.Contain("当前炮击").And.Not.Contain("命中点燃"));
            Assert.That(face.rectTransform.sizeDelta, Is.EqualTo(CardFaceLayout.EffectSize));
            Assert.That(face.preferredHeight, Is.LessThanOrEqualTo(face.rectTransform.rect.height + .5f));
            Assert.That(session.BattleCardDescription(definition, NavalBattleStage.Naval),
                Does.Contain("当前炮击 " + afterDamage).And.Contain("命中点燃敌舰"),
                "The separate detailed explanation still exposes conditional context.");
            NavalBattleVisualCapture.Capture(controller, "CardFaceLiveValues_02_Cannon_Loaded.png",
                "装填增益后同一炮击卡面仅增加主伤害数字，不追加当前炮击或点燃说明", "Battle Card");
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertCannonFaceCostIcons(Button card)
        {
            CardOrnamentGraphic[] ornaments = card.GetComponentsInChildren<CardOrnamentGraphic>();
            foreach (CardOrnamentKind kind in new[]
                { CardOrnamentKind.Energy, CardOrnamentKind.Gunpowder, CardOrnamentKind.Cannonball })
            {
                CardOrnamentGraphic[] costs = ornaments.Where(ornament => ornament.Kind == kind).ToArray();
                Assert.That(costs.Length, Is.EqualTo(1), "The chain shot keeps one visible " + kind + " cost icon.");
                Text amount = costs[0].GetComponentInChildren<Text>();
                Assert.That(amount, Is.Not.Null, kind + " has a visible cost amount.");
                Assert.That(amount.text, Is.EqualTo("1"), "Loading changes damage, not the chain shot's " + kind + " cost.");
            }
        }
    }
}
