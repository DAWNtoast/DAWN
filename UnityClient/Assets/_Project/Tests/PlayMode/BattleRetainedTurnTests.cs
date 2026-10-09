using System.Collections;
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
        public IEnumerator BattleResponsive_SameStageEnemyTurnRetainsSceneAndRefreshesAllLiveHudValues()
        {
            yield return LoadResponsiveBattleScene();
            CoreGameplayDemoController controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval);
            SetResponsiveHand(session, "card_brace", "card_chain_shot");
            SetResponsiveDrawPile(session, "card_brace", "card_chain_shot", "card_emergency_repair", "card_brace", "card_full_sail_turn");
            CoreBattleState battle = session.Battle;
            battle.Energy = 0;
            battle.PlayerBlock = 7;
            battle.EnemyBlock = 1;
            battle.PlayerDeckBuff = 2;
            battle.PlayerLoadedShot = 3;
            battle.PlayerFireRounds = 2;
            battle.EnemyBurnTurns = 2;
            battle.EnemyBurnDamage = 3;
            battle.DistanceMeters = 200f;
            battle.Alignment = NavalBattleAlignment.Parallel;
            battle.PhaseTurn = 2;
            int originalTurn = battle.Turn;
            int originalEnemyHull = battle.EnemyHull;
            RenderResponsiveBattle(controller);
            yield return null;

            RectTransform originalScene = RequiredResponsive<RectTransform>("Battle Card");
            RawImage originalBackdrop = RequiredResponsive<RawImage>("Battle Scene Art");
            RectTransform originalPlayer = RequiredResponsive<RectTransform>("Player Ship Art");
            RectTransform originalEnemy = RequiredResponsive<RectTransform>("Enemy Ship Art");
            BattleDefenseHud originalPlayerHud = RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track");
            BattleDefenseHud originalEnemyHud = RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track");
            int[] originalIds = { originalScene.GetInstanceID(), originalBackdrop.GetInstanceID(),
                originalPlayer.GetInstanceID(), originalEnemy.GetInstanceID(), originalPlayerHud.GetInstanceID(), originalEnemyHud.GetInstanceID() };
            Assert.That(RequiredResponsive<Text>("Resource 能 Value").text, Is.EqualTo("0/" + battle.MaxEnergy));

            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);

            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Battle));
            Assert.That(session.Battle, Is.SameAs(battle));
            Assert.That(battle.Stage, Is.EqualTo(NavalBattleStage.Naval));
            Assert.That(battle.Turn, Is.EqualTo(originalTurn + 1));
            CollectionAssert.AreEqual(originalIds, new[] {
                RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(),
                RequiredResponsive<RawImage>("Battle Scene Art").GetInstanceID(),
                RequiredResponsive<RectTransform>("Player Ship Art").GetInstanceID(),
                RequiredResponsive<RectTransform>("Enemy Ship Art").GetInstanceID(),
                RequiredResponsive<BattleDefenseHud>("Player Battle Hull Bar Track").GetInstanceID(),
                RequiredResponsive<BattleDefenseHud>("Enemy Battle Hull Bar Track").GetInstanceID()
            }, "The same-stage enemy turn must not reconstruct the ocean, ships or defense HUDs.");
            Assert.That(RequiredResponsive<Text>("Battle Badge").text, Does.EndWith("回合 " + battle.Turn));
            Assert.That(RequiredResponsive<Text>("Battle Status").text, Is.EqualTo(battle.LastAction));
            Assert.That(RequiredResponsive<Text>("Battle Range").text, Is.EqualTo(battle.DistanceMeters.ToString("0") + " 米"));
            Assert.That(RequiredResponsive<Text>("Enemy Intent Text").text, Is.EqualTo("敌方意图\n" + session.GetEnemyIntent()));
            Assert.That(RequiredResponsive<Text>("Player Resource Title").text, Is.EqualTo("旗舰资源 · 格挡 " + battle.PlayerBlock));
            Assert.That(RequiredResponsive<Text>("Resource 能 Value").text, Is.EqualTo(battle.Energy + "/" + battle.MaxEnergy));
            Assert.That(RequiredResponsive<Text>("Resource 回 Value").text, Is.EqualTo(battle.Turn.ToString()));
            Assert.That(RequiredResponsive<Text>("Resource 药 Value").text, Is.EqualTo(session.Cargo.Powder.ToString()));
            Assert.That(RequiredResponsive<Text>("Resource 盾 Value").text, Is.EqualTo(battle.EnemyBlock.ToString()));
            Assert.That(RequiredResponsive<Text>("Player Ammunition").text, Is.EqualTo("炮弹 " + session.Cargo.Cannonballs + " · 装填 +" + battle.PlayerLoadedShot));
            Assert.That(RequiredResponsive<Text>("Enemy Burning").text, Is.EqualTo("燃烧 " + battle.EnemyBurnTurns + " 回合"));
            Assert.That(RequiredResponsive<Text>("Battle Active Buffs").text, Does.Contain("甲板强化 +" + battle.PlayerDeckBuff)
                .And.Contain("装填加伤 +" + battle.PlayerLoadedShot)
                .And.Contain("牌堆 " + battle.DrawPile.Count + " · 弃牌 " + battle.DiscardPile.Count));
            Assert.That(originalPlayerHud.DisplayedHull, Is.EqualTo(session.Hull));
            Assert.That(originalPlayerHud.DisplayedGuard, Is.EqualTo(battle.PlayerBlock));
            Assert.That(battle.PlayerBlock, Is.Zero);
            Assert.That(originalEnemyHud.DisplayedHull, Is.EqualTo(battle.EnemyHull));
            Assert.That(originalEnemyHud.DisplayedGuard, Is.EqualTo(battle.EnemyBlock));
            Assert.That(battle.EnemyHull, Is.EqualTo(originalEnemyHull - 3));
            Assert.That(battle.EnemyBurnTurns, Is.EqualTo(1));
            Assert.That(RequiredResponsive<Button>("Battle Ram").interactable, Is.EqualTo(session.CanRam(out _)));
            Assert.That(RequiredResponsive<Button>("Battle Board").interactable, Is.EqualTo(session.CanBoard(out _)));
            Assert.That(RequiredResponsive<Button>("End Battle Turn").interactable, Is.True);
            AssertResponsiveHandMatchesCore(session);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
