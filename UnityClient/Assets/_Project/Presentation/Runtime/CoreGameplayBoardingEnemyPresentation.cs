using System.Collections;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool HasBoardingEnemySchool(CoreBattleState battle) => battle != null &&
            battle.Stage == NavalBattleStage.Boarding &&
            battle.Profile?.TrainingTarget == null && battle.BoardingEnemy.Deck != null &&
            battle.BoardingEnemy.Deck.SchoolId != "neutral";

        private void RefreshBoardingEnemyHud(RectTransform root, CoreBattleState battle)
        {
            if (!HasBoardingEnemySchool(battle)) return;
            var deck = battle.BoardingEnemy.Deck;
            var preview = session.GetBoardingEnemyPreview();
            Transform intent = root.Find("Enemy Intent HUD");
            Text intentLabel = intent?.Find("Enemy Intent Text")?.GetComponent<Text>();
            if (intentLabel != null)
            {
                intentLabel.text = CoreWarshipDesignCatalog.SchoolName(deck.SchoolId) + " · " +
                    ((System.Math.Max(1, battle.PhaseTurn) - 1) % deck.Turns.Count + 1) + "/" + deck.Turns.Count +
                    "\n" + deck.TurnAt(battle.PhaseTurn).Name +
                    "\n攻击 " + preview.AttackDamage + " · 穿盾 " + preview.PiercingDamage +
                    "\n失血 " + preview.PlayerHealthLoss + " · 灼烧 " + preview.PlayerBurnDamage +
                    " · 咒印 " + preview.PlayerCurseDamage + "\n点击查看行动";
                intentLabel.raycastTarget = false;
                intentLabel.resizeTextMinSize = 14;
            }
            if (intent != null && intent.GetComponent<Button>() == null)
            {
                Button inspect = intent.gameObject.AddComponent<Button>();
                inspect.targetGraphic = intent.GetComponent<Image>();
                inspect.onClick.AddListener(OpenBoardingEnemyOverview);
            }

            Transform resources = root.Find("Enemy Resource HUD");
            if (resources == null) return;
            foreach (string name in new[] { "Resource 回 Icon", "Resource 回 Value", "Resource 盾 Icon", "Resource 盾 Value" })
            {
                Transform item = resources.Find(name);
                if (item != null) item.gameObject.SetActive(false);
            }
            Text title = resources.Find("Enemy Resource Title")?.GetComponent<Text>();
            if (title != null) title.text = "敌方资源 · 格挡 " + battle.EnemyBlock;
            Text status = resources.Find("Enemy Burning")?.GetComponent<Text>();
            if (status != null)
            {
                status.text = session.GetBoardingEnemyResourceSummary();
                status.rectTransform.sizeDelta = new Vector2(212f, 74f);
                status.rectTransform.anchoredPosition = new Vector2(0f, -12f);
                status.fontSize = 16;
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 14;
                status.resizeTextMaxSize = 16;
            }
        }

        private void RefreshBoardingEnemyOverview()
        {
            CoreBattleState battle = inspectedBattle;
            var deck = battle.BoardingEnemy.Deck;
            battleDeckSummary.text = string.Empty;
            var body = new StringBuilder();
            body.Append("当前资源\n").Append(session.GetBoardingEnemyResourceSummary())
                .Append("\n\n本轮行动预告\n").Append(session.GetEnemyIntent())
                .Append("\n\n体系规则\n").Append(session.GetBoardingEnemyMechanicDescription())
                .Append("\n\n固定循环\n");
            for (int index = 0; index < deck.Turns.Count; index++)
            {
                CoreBoardingEnemyTurn turn = deck.Turns[index];
                body.Append(index + 1).Append(". ").Append(turn.Name).Append('\n');
                for (int card = 0; card < turn.Cards.Count; card++)
                {
                    CoreBoardingEnemyCard action = turn.Cards[card];
                    body.Append("  ").Append(card + 1).Append(") ").Append(action.Name)
                        .Append("：").Append(session.GetBoardingEnemyCardDescription(action)).Append('\n');
                }
                body.Append('\n');
            }
            body.Append("\n预告基于当前状态。格挡、减伤、反击或生命变化都可能改变后续动作；任一方倒下立即停止。\n")
                .Append("查看战术不会推进回合、消耗资源或改变出牌顺序。");
            Text text = BattleDeckLabel("Boarding Enemy Plan Details", battleDeckContent,
                new Vector2(1188f, 612f), new Vector2(0f, -16f), 23, body.ToString(),
                MarketShopSkin.Ink, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = false;
            float height = Mathf.Max(612f, text.preferredHeight + 32f);
            text.rectTransform.sizeDelta = new Vector2(1188f, height - 32f);
            battleDeckContent.sizeDelta = new Vector2(1276f, height);
            battleDeckScroll.StopMovement();
            battleDeckContent.anchoredPosition = Vector2.zero;
            battleDeckScroll.verticalNormalizedPosition = 1f;
        }

        private IEnumerator AnimateBoardingEnemyActions(RectTransform stage, RectTransform player, RectTransform enemy,
            Image playerBar, Image enemyBar, BattleAnimationSnapshot before, BattleAnimationSnapshot after,
            CoreBoardingEnemyState results, CoreBoardingBattleState resolvedState = null, long resolvedAfterSequence = long.MaxValue)
        {
            int playerHealth = results.LastCurseHealthLost > 0
                ? results.LastCursePlayerHealth - results.LastCurseHealthLost
                : results.LastBurnHealthLost > 0 ? results.LastBurnPlayerHealth - results.LastBurnHealthLost : before.PlayerCrew;
            int enemyHealth = before.EnemyCrew;
            int playerGuard = before.PlayerBlock, enemyGuard = before.EnemyBlock;
            foreach (CoreBoardingEnemyActionResult action in results.LastActions)
            {
                if (playerHealth != action.PlayerHealthBefore)
                    yield return AnimateHealthBar(playerBar, playerHealth, action.PlayerHealthBefore, before.PlayerMaxCrew,
                        action.PlayerHealthBefore > playerHealth ? Green : Coral);
                if (enemyHealth != action.EnemyHealthBefore)
                    yield return AnimateHealthBar(enemyBar, enemyHealth, action.EnemyHealthBefore, before.EnemyMaxCrew,
                        action.EnemyHealthBefore > enemyHealth ? Green : Coral);
                yield return AnimateFloatingText(stage, BattleEffectPosition(stage, enemy) + Vector2.up * 90f,
                    action.Name + (string.IsNullOrEmpty(action.Detail) ? string.Empty : "\n" + action.Detail), Gold, "Enemy School Action");
                if (action.RawAttack > 0)
                {
                    bool schoolAttack = BattleSchoolAttackPresentation.TryEnemy(results.Deck?.SchoolId, action,
                        out BattleSchoolAttackKind school, out BattleSchoolAttackOutcome outcome);
                    if (!schoolAttack) yield return AnimateShipWindup(enemy, DirectionToward(enemy, player), false);
                    Coroutine damage = StartCoroutine(schoolAttack
                        ? AnimateSchoolContactDamage(playerBar, action.PlayerHealthBefore, action.PlayerHealthAfter,
                            before.PlayerMaxCrew, action.PlayerGuardBefore, action.PlayerGuardAfter, stage)
                        : AnimateBattleDamage(playerBar, action.PlayerHealthBefore, action.PlayerHealthAfter,
                            before.PlayerMaxCrew, action.PlayerGuardBefore, action.PlayerGuardAfter, stage));
                    string result = action.HealthLost > 0 ? "-" + action.HealthLost : action.AbsorbedDamage > 0 ? "格挡" : "未命中";
                    if (schoolAttack) yield return AnimateSchoolAttack(stage, enemy, player, school, outcome, true, result);
                    else yield return AnimateImpact(stage, player, Coral, result, "Player School Hit");
                    yield return damage;
                }
                else if (action.PlayerHealthBefore != action.PlayerHealthAfter)
                    yield return AnimateHealthBar(playerBar, action.PlayerHealthBefore, action.PlayerHealthAfter, before.PlayerMaxCrew, Coral);
                if (action.SelfDamage > 0)
                    yield return AnimateAura(stage, enemy, Coral, "自损 -" + action.SelfDamage, "Enemy School Life Cost");
                if (action.Healed > 0)
                    yield return AnimateAura(stage, enemy, Green, "恢复 +" + action.Healed, "Enemy School Healing");
                if (action.EnemyHealthBefore != action.EnemyHealthAfter)
                    yield return AnimateHealthBar(enemyBar, action.EnemyHealthBefore, action.EnemyHealthAfter, before.EnemyMaxCrew,
                        action.EnemyHealthAfter > action.EnemyHealthBefore ? Green : Coral);
                if (enemyGuard != action.EnemyGuardAfter)
                    yield return AnimateBattleGuard(enemyBar, enemyGuard, action.EnemyGuardAfter, stage, false);
                playerHealth = action.PlayerHealthAfter; enemyHealth = action.EnemyHealthAfter;
                playerGuard = action.PlayerGuardAfter; enemyGuard = action.EnemyGuardAfter;
            }
            if (playerHealth != after.PlayerCrew)
                yield return AnimateHealthBar(playerBar, playerHealth, after.PlayerCrew, before.PlayerMaxCrew,
                    after.PlayerCrew > playerHealth ? Green : Coral);
            int enemyAfter = after.EnemyCrew, enemyGuardAfter = after.EnemyBlock;
            if (resolvedState != null)
                foreach (CoreBoardingResolvedAttack attack in resolvedState.ResolvedAttacks)
                    if (attack.Sequence > resolvedAfterSequence)
                    {
                        enemyAfter = attack.HealthBefore;
                        enemyGuardAfter = attack.GuardBefore;
                        break;
                    }
            if (enemyHealth != enemyAfter)
            {
                if (enemyHealth > enemyAfter)
                    yield return AnimateAura(stage, enemy, Coral, "回合结算 -" + (enemyHealth - enemyAfter), "Enemy School Turn End");
                yield return AnimateHealthBar(enemyBar, enemyHealth, enemyAfter, before.EnemyMaxCrew,
                    enemyAfter > enemyHealth ? Green : Coral);
            }
            if (playerGuard != after.PlayerBlock)
                yield return AnimateBattleGuard(playerBar, playerGuard, after.PlayerBlock, stage, false);
            if (enemyGuard != enemyGuardAfter)
                yield return AnimateBattleGuard(enemyBar, enemyGuard, enemyGuardAfter, stage, false);
        }
    }
}
