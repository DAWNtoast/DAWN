using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BoardingEnemyPresentationTests
    {
        private GameObject host;

        [TearDown]
        public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
        }

        [TestCase("fire", "自燃")]
        [TestCase("arcane", "魔力")]
        [TestCase("thunder", "连锁")]
        [TestCase("curse", "咒痕")]
        [TestCase("tide", "潮势")]
        [TestCase("blood", "失血")]
        public void SchoolIntentOpensReadableReadonlyDetailsAndRestoresBattleInput(string school, string resource)
        {
            host = new GameObject("Boarding Enemy Presentation Test");
            host.SetActive(false);
            var controller = host.AddComponent<CoreGameplayDemoController>();
            controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
            host.SetActive(true);
            CoreVoyageSession session = controller.Session;
            CoreBattleState battle = session.Battle;
            battle.Stage = NavalBattleStage.Boarding;
            battle.Profile = new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Balanced,
                100, 10, 10, 5, 0, 20, 1, 1, boardingGuard: 5);
            typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool(school));
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Canvas.ForceUpdateCanvases();

            RectTransform icons = Find<RectTransform>("Enemy Intent Icons");
            Button intent = icons.GetComponentsInChildren<Button>(false).First();
            Assert.That(intent, Is.Not.Null, "Every school plan must be discoverable from the visible intent.");
            Assert.That(intent.interactable, Is.True);
            Assert.That(icons.gameObject.activeInHierarchy, Is.True);
            foreach (CoreBattleIntent action in CoreBattleIntentPresentation.Build(session))
                Assert.That(icons.Find("Intent " + action.Id + "/Amount").GetComponent<Text>().text, Is.EqualTo(action.Value));
            string before = Snapshot(session);
            intent.onClick.Invoke();
            Canvas.ForceUpdateCanvases();

            Text details = Find<Text>("Boarding Enemy Plan Details");
            Assert.That(details.text, Does.Contain("本轮行动预告").And.Contain("体系规则").And.Contain("固定循环"));
            Assert.That(details.text, Does.Contain(resource), "School resource details remain discoverable from the compact intent icon.");
            foreach (CoreBoardingEnemyTurn turn in battle.BoardingEnemy.Deck.Turns)
                foreach (CoreBoardingEnemyCard card in turn.Cards)
                    Assert.That(details.text, Does.Contain(card.Name));
            Assert.That(details.rectTransform.rect.height, Is.GreaterThanOrEqualTo(details.preferredHeight - 1f),
                "The scroll content must contain the complete plan without clipping or shrinking the body text.");
            Assert.That(details.fontSize, Is.GreaterThanOrEqualTo(20));
            Assert.That(details.resizeTextForBestFit, Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
            Assert.That((bool)typeof(CoreGameplayDemoController).GetProperty("BattleInspectionBlocksInput", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.True);

            Find<Button>("Close Battle Deck").onClick.Invoke();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Battle Deck Overlay" && t.gameObject.activeSelf), Is.False);
            Assert.That((bool)typeof(CoreGameplayDemoController).GetProperty("BattleInspectionBlocksInput", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.False);
            Assert.That(Snapshot(session), Is.EqualTo(before));
        }

        private T Find<T>(string name) where T : Component => host.GetComponentsInChildren<T>(true).Single(c => c.name == name);

        private static string Snapshot(CoreVoyageSession session)
        {
            CoreBattleState battle = session.Battle;
            return string.Join("|", battle.PhaseTurn, battle.Turn, battle.Energy, battle.PlayerBoardingHealth,
                battle.EnemyBoardingHealth, battle.PlayerBlock, battle.EnemyBlock, session.GetBoardingEnemyResourceSummary(),
                string.Join(",", battle.Hand), string.Join(",", battle.DrawPile), string.Join(",", battle.DiscardPile), session.GetEnemyIntent());
        }
    }
}
