using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Core.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool battleAnimationPlaying;
        private bool battlePresentationNeedsRecovery;

        public bool IsBattleAnimationPlaying => battleAnimationPlaying || IsBattleHandFlying;

        private void SuspendBattlePresentation()
        {
            CancelBattleSchoolAttackPresentation();
            CancelBattleResolutionPresentation();
            CloseBattleDeckOverview(false);
            CloseBoardingCardChoice();
            CloseNavalCardChoice();
            CloseRamComic();
            CloseBoardingTransition();
            if (!Application.isPlaying || session == null) return;
            if (!battleAnimationPlaying && session.Phase != CoreDemoPhase.Battle &&
                (retainedBattleRoot == null || !retainedBattleRoot.gameObject.activeSelf)) return;
            // Disabling a MonoBehaviour alone does NOT stop Unity coroutines.
            // Cancel the presentation before it can commit a pending card; any
            // already committed result remains authoritative in the session.
            battlePresentationNeedsRecovery = true;
            CompleteBattleHandFlight();
            // Application pause can reach this path without OnDisable. Clear
            // the collection handle before stopping it so Resume/Render can
            // schedule cleanup again instead of retaining a stopped coroutine.
            SuspendUnusedArtCollection();
            StopAllCoroutines();
            battleAnimationPlaying = false;
            if (retainedBattleRoot != null) LockBattleInput(retainedBattleRoot);
        }

        private void ResumeBattlePresentation()
        {
            if (!Application.isPlaying || !battlePresentationNeedsRecovery || session == null || contentRoot == null) return;
            battlePresentationNeedsRecovery = false;
            battleAnimationPlaying = false;
            // A cancelled tween may leave a reparented card, partial pose or FX.
            // This rare lifecycle path must rebuild, not retain those objects.
            Render();
            // Recovery restores the authoritative hand immediately; these are
            // existing cards, not another draw from the deck.
            CompleteBattleHandFlight();
        }

        private void ConfigureBattleCardPresentation(Button button, Color accent, RectTransform keywordTooltip = null)
        {
            // A hand card has its own scale and full-size inspection animation.
            // The general button tween otherwise pulls every small card back to 1x.
            NauticalButtonMotion ordinaryMotion = button.GetComponent<NauticalButtonMotion>();
            if (ordinaryMotion != null) ordinaryMotion.enabled = false;
            BattleCardHoverFeedback feedback = button.gameObject.AddComponent<BattleCardHoverFeedback>();
            feedback.Configure(accent, keywordTooltip);
        }

        private static void ConfigureBattleShipIdle(RectTransform ship, float phase, float lift, float roll)
        {
            BattleShipIdleMotion motion = ship.gameObject.AddComponent<BattleShipIdleMotion>();
            motion.Configure(phase, lift, roll);
        }

        private Image CreateBattleHullBar(string name, Transform parent, Vector2 size, Vector2 position, int current, int maximum, Color color)
        {
            size.y = Mathf.Max(24f, size.y);
            // Health is a solid meter, so its empty track must bypass panel paper skins.
            Image backing = CreateImage(name + " Track", parent, size + new Vector2(8f, 8f), position, new Color(0.015f, 0.025f, 0.03f, 0.92f));
            backing.raycastTarget = false;
            RectTransform track = backing.rectTransform;
            Outline outline = track.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.8f, .12f, .10f, .8f);
            outline.effectDistance = new Vector2(2f, -2f);
            Image fill = CreateImage(name, track, size, Vector2.zero, color);
            fill.sprite = BattleDefenseHud.FillSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = Mathf.Clamp01(current / (float)Mathf.Max(1, maximum));
            fill.raycastTarget = false;
            return fill;
        }

        private void BeginBattleCardAnimation(Button source, int handIndex, CoreBattleCard battleCard)
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen || source == null || !source.gameObject.activeInHierarchy || !source.interactable ||
                session.Phase != CoreDemoPhase.Battle || battleCard == null || session.Battle == null)
            {
                return;
            }
            CompleteBattleHandFlight();
            if (TryHandleGuidedBossCardAttempt(source, handIndex, battleCard)) return;
            if (!session.CanPlayCard(handIndex)) return;
            if (TryBeginBoardingCardChoice(source, handIndex, battleCard)) return;
            if (TryBeginNavalCardChoice(source, handIndex, battleCard)) return;
            StartCoroutine(AnimateBattleCard(source, handIndex, battleCard));
        }

        private IEnumerator AnimateBattleCard(Button source, int handIndex, CoreBattleCard battleCard, CoreBoardingPlayOptions options = null,
            CoreNavalCardPlayOptions navalOptions = null)
        {
            battleAnimationPlaying = true;
            try
            {
                CoreBattleState battle = session.Battle;
                if (battle == null) yield break;
                long resolvedBefore = battle.Boarding.ResolvedAttackSequence;
                BattleAnimationSnapshot before = new BattleAnimationSnapshot(session, battle);
                RectTransform stage = GameObject.Find("Battle Card")?.GetComponent<RectTransform>();
                RectTransform playerShip = GameObject.Find("Player Ship Art")?.GetComponent<RectTransform>();
                RectTransform enemyShip = GameObject.Find("Enemy Ship Art")?.GetComponent<RectTransform>();
                Image playerHullBar = GameObject.Find("Player Battle Hull Bar")?.GetComponent<Image>();
                Image enemyHullBar = GameObject.Find("Enemy Battle Hull Bar")?.GetComponent<Image>();
                if (stage == null || playerShip == null || enemyShip == null)
                {
                    if (navalOptions != null) session.PlayNavalCard(handIndex, navalOptions);
                    else session.PlayCard(handIndex, options);
                    yield break;
                }

                LockBattleInput(stage);
                RectTransform playedCard = source.GetComponent<RectTransform>();
                PlaySfx("battle_card_play", 0.78f);
                yield return RaiseCardToCenter(playedCard, stage, CardEffectColor(battleCard));
                if (!(navalOptions != null ? session.PlayNavalCard(handIndex, navalOptions) : session.PlayCard(handIndex, options))) yield break;
                // Core resolves an outcome by clearing session.Battle. The captured
                // object still contains final hull/crew values, including a loss.
                BattleAnimationSnapshot after = new BattleAnimationSnapshot(session, session.Battle ?? battle);
                RefreshBattleStateChangePresentation(stage, battle);
                bool boarding = before.Stage == NavalBattleStage.Boarding;
                int playerBefore = boarding ? before.PlayerCrew : before.PlayerHull;
                int playerAfter = boarding ? after.PlayerCrew : after.PlayerHull;
                int block = Mathf.Max(0, after.PlayerBlock - before.PlayerBlock);
                int repair = Mathf.Max(0, playerAfter - playerBefore);
                bool targetedAttack = battleCard.Damage > 0 || battle.Boarding.ResolvedAttackSequence > resolvedBefore ||
                    BattleSchoolAttackPresentation.TryPlayer(battleCard, boarding,
                        Mathf.Max(0, (boarding ? before.EnemyCrew - after.EnemyCrew : before.EnemyHull - after.EnemyHull)),
                        Mathf.Max(0, before.EnemyBlock - after.EnemyBlock), out _, out _);
                // Commit once, then overlap independent feedback instead of making
                // buff/draw/guard notifications each block another full animation.
                // Physical cannon -> impact and impact -> maneuver order stays intact.
                List<IEnumerator> feedback = new List<IEnumerator>
                {
                    ReleaseBattleCard(playedCard, stage, targetedAttack ? enemyShip : playerShip, CardEffectColor(battleCard)),
                    AnimateCardCastStamp(stage, battleCard.Type, CardEffectColor(battleCard)),
                    AnimateBattleCardPhysicalFeedback(stage, playerShip, enemyShip, enemyHullBar, battleCard, before, after),
                };
                if (boarding && battle.Boarding.ResolvedAttackSequence > resolvedBefore)
                    feedback.Add(AnimateResolvedSchoolAttacks(stage, playerShip, enemyShip, enemyHullBar,
                        battle.Boarding, resolvedBefore, before.EnemyMaxCrew));
                if (block > 0)
                {
                    feedback.Add(AnimateBattleGuard(playerHullBar, before.PlayerBlock, after.PlayerBlock, stage));
                    feedback.Add(AnimateAura(stage, playerShip, Teal, "格挡 +" + block, "Player Shield"));
                }
                if (repair > 0)
                {
                    feedback.Add(AnimateAura(stage, playerShip, Green, (boarding ? "船员 +" : "船体 +") + repair, "Player Repair", block > 0 ? 50f : 105f));
                    feedback.Add(AnimateHealthBar(playerHullBar, playerBefore, playerAfter, boarding ? before.PlayerMaxCrew : before.PlayerMaxHull, Green));
                }
                if (boarding && playerAfter < playerBefore)
                {
                    feedback.Add(AnimateFloatingText(stage, BattleEffectPosition(stage, playerShip) + Vector2.up * 140f,
                        "生命 -" + (playerBefore - playerAfter), Coral, "Boarding Life Payment Float"));
                    feedback.Add(AnimateHealthBar(playerHullBar, playerBefore, playerAfter, before.PlayerMaxCrew, Coral));
                }
                string buffs = BattleCardBuffChange(before, after);
                if (buffs.Length > 0)
                    feedback.Add(AnimateFloatingText(stage, BattleEffectPosition(stage, playerShip) + Vector2.up * 170f, buffs, Gold, "Battle Strategy Float"));
                int drawn = Mathf.Max(0, after.HandCount - before.HandCount + 1);
                if (drawn > 0)
                    feedback.Add(AnimateFloatingText(stage, new Vector2(0f, -115f), "抽牌 +" + drawn, Gold, "Battle Draw Float"));
                if (after.EnemyBurnTurns > before.EnemyBurnTurns)
                    feedback.Add(AnimateFloatingText(stage, BattleEffectPosition(stage, enemyShip) + Vector2.up * 170f,
                        (boarding ? "敌方燃烧 · " : "敌舰燃烧 · ") + after.EnemyBurnTurns + " 回合", Coral, "Enemy Burning Float"));
                yield return PlayBattleFeedbackTogether(feedback);

                if (session.Phase != CoreDemoPhase.Battle)
                    yield return AnimateStagedBattleResolution(stage, playerShip, enemyShip, before, after);
                else if (playedCard != null && playedCard.gameObject.activeSelf)
                    yield return FadePlayedCard(playedCard);
            }
            finally
            {
                // The rest of the hand is retained; remove only this spent card.
                if (source != null && source.transform.parent != null && source.transform.parent.name == "Battle Card")
                {
                    source.gameObject.SetActive(false);
                    Destroy(source.gameObject);
                }
                CompleteStagedBattleAnimation();
            }
        }

        private IEnumerator PlayBattleFeedbackTogether(List<IEnumerator> feedback)
        {
            Coroutine[] running = new Coroutine[feedback.Count];
            for (int index = 0; index < feedback.Count; index++) running[index] = StartCoroutine(feedback[index]);
            foreach (Coroutine effect in running)
                if (effect != null) yield return effect;
        }

        private IEnumerator AnimateBattleCardPhysicalFeedback(RectTransform stage, RectTransform playerShip, RectTransform enemyShip,
            Image enemyHullBar, CoreBattleCard card, BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            bool boarding = before.Stage == NavalBattleStage.Boarding;
            int from = boarding ? before.EnemyCrew : before.EnemyHull;
            int to = boarding ? after.EnemyCrew : after.EnemyHull;
            bool schoolAttack = BattleSchoolAttackPresentation.TryPlayer(card, boarding,
                Mathf.Max(0, from - to), Mathf.Max(0, before.EnemyBlock - after.EnemyBlock),
                out BattleSchoolAttackKind school, out BattleSchoolAttackOutcome outcome);
            bool directAttack = card.Damage > 0 && !(boarding && card.BoardingSpec != null && card.BoardingSpec.IsDelayed);
            if (before.Stage != NavalBattleStage.Preparation && (directAttack || schoolAttack))
            {
                int damage = Mathf.Max(0, from - to);
                string result = damage > 0 ? "-" + damage : before.EnemyBlock > after.EnemyBlock ? "格挡" : "未命中";
                if (schoolAttack)
                {
                    Coroutine numbers = StartCoroutine(AnimateSchoolContactDamage(enemyHullBar, from, to,
                        boarding ? before.EnemyMaxCrew : before.EnemyMaxHull, before.EnemyBlock, after.EnemyBlock, stage));
                    yield return AnimateSchoolAttack(stage, playerShip, enemyShip, school, outcome, boarding, result);
                    if (numbers != null) yield return numbers;
                }
                else
                {
                    if (boarding) yield return AnimateShipWindup(playerShip, DirectionToward(playerShip, enemyShip), false);
                    else if (card.Action == NavalBattleCardAction.Cannon)
                        yield return AnimateCannonVolley(stage, playerShip, enemyShip, "Player Cannon Projectile", before.PlayerFireRounds > 0);
                    Coroutine numbers = StartCoroutine(AnimateBattleDamage(enemyHullBar, from, to,
                        boarding ? before.EnemyMaxCrew : before.EnemyMaxHull, before.EnemyBlock, after.EnemyBlock, stage));
                    if (boarding || card.Action == NavalBattleCardAction.Cannon)
                        yield return AnimateImpact(stage, enemyShip, Coral, result, boarding ? "Enemy Melee Hit" : "Enemy Hit");
                    else yield return AnimateAura(stage, enemyShip, Coral, "水下破坏 " + result, "Enemy Sabotage");
                    if (numbers != null) yield return numbers;
                }
            }
            if (!boarding && (before.TacticalRevision != after.TacticalRevision || !Mathf.Approximately(before.Distance, after.Distance) || before.Alignment != after.Alignment))
                yield return AnimateBattleManeuver(stage, playerShip, enemyShip, before, after, false);
        }

        private void BeginEnemyTurnAnimation()
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || BattleInspectionBlocksInput || IsBoardingCardChoiceOpen || IsNavalCardChoiceOpen || session.Battle == null || session.Phase != CoreDemoPhase.Battle) return;
            if (session.NavalActionMovementActive && session.Battle.NavalAwaitingHeadingSelection) return;
            if (session.IsGuidedBossBattle && !session.GuidedBossAllowsEndTurn) return;
            CompleteBattleHandFlight();
            StartCoroutine(AnimateEnemyTurn());
        }

        private IEnumerator AnimateEnemyTurn()
        {
            battleAnimationPlaying = true;
            try
            {
                CoreBattleState battle = session.Battle;
                if (battle == null) yield break;
                long resolvedBefore = battle.Boarding.ResolvedAttackSequence;
                BattleAnimationSnapshot before = new BattleAnimationSnapshot(session, battle);
                string intent = session.GetEnemyIntent();
                bool announcedRam = session.EnemyWillRam;
                RectTransform stage = GameObject.Find("Battle Card")?.GetComponent<RectTransform>();
                RectTransform playerShip = GameObject.Find("Player Ship Art")?.GetComponent<RectTransform>();
                RectTransform enemyShip = GameObject.Find("Enemy Ship Art")?.GetComponent<RectTransform>();
                Image playerHullBar = GameObject.Find("Player Battle Hull Bar")?.GetComponent<Image>();
                Image enemyHullBar = GameObject.Find("Enemy Battle Hull Bar")?.GetComponent<Image>();
                if (stage == null || playerShip == null || enemyShip == null)
                {
                    session.EndBattleTurn();
                    yield break;
                }

                LockBattleInput(stage);
                PlaySfx("battle_enemy_intent", 0.52f);
                yield return DiscardBattleHandAtTurnEnd();
                yield return AnimateFloatingText(stage, new Vector2(0f, 150f),
                    before.Stage == NavalBattleStage.Preparation ? "双方完成备战" : "敌方行动", Coral, "Enemy Intent Banner");
                // Commit once. Boarding hit feedback uses actual attack results;
                // turn-end fire can kill a combatant before that attack happens.
                if (!session.EndBattleTurn()) yield break;
                BattleAnimationSnapshot after = new BattleAnimationSnapshot(session, session.Battle ?? battle);
                RefreshBattleStateChangePresentation(stage, battle);
                bool boarding = before.Stage == NavalBattleStage.Boarding;
                bool ram = before.Stage == NavalBattleStage.Naval && announcedRam;
                bool cannon = before.Stage == NavalBattleStage.Naval && intent.Contains("炮击：");
                CoreBoardingEnemyState boardingResult = battle.BoardingEnemy;
                bool melee = boarding && boardingResult.LastAttackExecuted;
                int playerBefore = boarding ? before.PlayerCrew : before.PlayerHull;
                int playerAfter = boarding ? after.PlayerCrew : after.PlayerHull;
                if (melee)
                {
                    playerBefore = boardingResult.LastAttackPlayerHealth;
                    playerAfter = playerBefore - boardingResult.LastAttackHealthLost;
                }
                int playerDamage = Mathf.Max(0, playerBefore - playerAfter);
                int enemyDamage = Mathf.Max(0, before.EnemyHull - after.EnemyHull);
                int burned = before.Stage == NavalBattleStage.Naval && before.EnemyBurnTurns > after.EnemyBurnTurns
                    ? Mathf.Min(enemyDamage, Mathf.Min(before.EnemyBurnDamage, before.EnemyHull)) : 0;
                if (burned > 0)
                {
                    yield return AnimateAura(stage, enemyShip, Coral, "灼烧 -" + burned, "Enemy Burn Damage");
                    yield return AnimateHealthBar(enemyHullBar, before.EnemyHull, before.EnemyHull - burned, before.EnemyMaxHull, Coral);
                }
                if (boarding && boardingResult.LastBurnHealthLost > 0)
                {
                    yield return AnimateAura(stage, playerShip, Coral, "灼烧 -" + boardingResult.LastBurnHealthLost, "Player Boarding Burn Damage");
                    yield return AnimateHealthBar(playerHullBar, boardingResult.LastBurnPlayerHealth,
                        boardingResult.LastBurnPlayerHealth - boardingResult.LastBurnHealthLost, before.PlayerMaxCrew, Coral);
                }

                if (boarding && HasBoardingEnemySchool(battle))
                {
                    // A school turn may contain several real actions. Play only
                    // committed steps, so counters and deaths stop the sequence.
                    yield return AnimateBoardingEnemyActions(stage, playerShip, enemyShip, playerHullBar,
                        enemyHullBar, before, after, boardingResult, battle.Boarding, resolvedBefore);
                    yield return AnimateResolvedSchoolAttacks(stage, playerShip, enemyShip, enemyHullBar,
                        battle.Boarding, resolvedBefore, before.EnemyMaxCrew);
                    if (session.Phase != CoreDemoPhase.Battle)
                        yield return AnimateStagedBattleResolution(stage, playerShip, enemyShip, before, after);
                    yield break;
                }

                Texture2D ramComic = ram ? RamComicTexture(true, NavalBattleAlignment.EnemyBroadside) : null;
                if (ramComic != null)
                {
                    yield return AnimateRamComic(ramComic, true, before, after, burned);
                    // The committed result remains authoritative: a lethal ram
                    // (including the boarding first strike) never forces boarding.
                    if (session.Phase != CoreDemoPhase.Battle)
                        yield return AnimateStagedBattleResolution(stage, playerShip, enemyShip, before, after);
                    yield break;
                }

                if (!boarding && (before.TacticalRevision != after.TacticalRevision || !Mathf.Approximately(before.Distance, after.Distance) || before.Alignment != after.Alignment))
                    yield return AnimateBattleManeuver(stage, playerShip, enemyShip, before, after, true);
                if (cannon || melee || ram)
                {
                    if (cannon)
                        yield return AnimateCannonVolley(stage, enemyShip, playerShip, "Enemy Cannon Projectile");
                    else
                        yield return AnimateShipWindup(enemyShip, DirectionToward(enemyShip, playerShip), ram);
                    string result = playerDamage > 0 ? "-" + playerDamage : before.PlayerBlock > 0 && !ram ? "格挡" : "未命中";
                    int remainingGuard = Mathf.Max(0, before.PlayerBlock - after.EnemyGuardAbsorbed);
                    Coroutine hitNumbers = StartCoroutine(AnimateBattleDamage(playerHullBar, playerBefore, playerAfter,
                        boarding ? before.PlayerMaxCrew : before.PlayerMaxHull, before.PlayerBlock, remainingGuard, stage));
                    yield return AnimateImpact(stage, playerShip, Coral, result, melee ? "Player Melee Hit" : ram ? "Player Ram Hit" : "Player Hit");
                    yield return hitNumbers;
                    if (remainingGuard != after.PlayerBlock)
                        yield return AnimateBattleGuard(playerHullBar, remainingGuard, after.PlayerBlock, stage, false);
                }
                if (melee && playerAfter != after.PlayerCrew)
                    yield return AnimateHealthBar(playerHullBar, playerAfter, after.PlayerCrew, before.PlayerMaxCrew,
                        after.PlayerCrew > playerAfter ? Green : Coral);
                int recoil = Mathf.Max(0, enemyDamage - burned);
                if (recoil > 0)
                {
                    yield return AnimateImpact(stage, enemyShip, Coral, "反伤 -" + recoil, "Enemy Ram Recoil");
                    yield return AnimateHealthBar(enemyHullBar, before.EnemyHull - burned, after.EnemyHull, before.EnemyMaxHull, Coral);
                }
                int enemyGuardBeforeDelayed = after.EnemyBlock;
                if (boarding)
                    foreach (CoreBoardingResolvedAttack attack in battle.Boarding.ResolvedAttacks)
                        if (attack.Sequence > resolvedBefore) { enemyGuardBeforeDelayed = attack.GuardBefore; break; }
                if (session.Phase == CoreDemoPhase.Battle && !cannon && !melee && !ram && enemyGuardBeforeDelayed > 0)
                {
                    yield return AnimateBattleGuard(enemyHullBar, before.EnemyBlock, enemyGuardBeforeDelayed, stage, false);
                    yield return AnimateAura(stage, enemyShip, Gold, "敌方格挡 " + enemyGuardBeforeDelayed, "Enemy Shield");
                }
                if (after.EnemyLoadedShot > before.EnemyLoadedShot)
                    yield return AnimateFloatingText(stage, BattleEffectPosition(stage, enemyShip) + Vector2.up * 110f,
                        "敌方装填 +" + (after.EnemyLoadedShot - before.EnemyLoadedShot), Gold, "Enemy Loading Float");
                if (ram && after.Stage == NavalBattleStage.Boarding)
                    yield return AnimateFloatingText(stage, new Vector2(0f, 145f),
                        "敌方撞击先手 · 船员生命 -" + Mathf.Max(0, before.PlayerCrew - after.PlayerCrew), Coral, "Enemy Boarding First Strike");
                if (boarding)
                    yield return AnimateResolvedSchoolAttacks(stage, playerShip, enemyShip, enemyHullBar,
                        battle.Boarding, resolvedBefore, before.EnemyMaxCrew);
                if (session.Phase != CoreDemoPhase.Battle)
                    yield return AnimateStagedBattleResolution(stage, playerShip, enemyShip, before, after);
                else if (before.Stage != after.Stage)
                {
                    yield return AnimateFloatingText(stage, new Vector2(0f, 110f),
                        after.Stage == NavalBattleStage.Naval ? "进入炮击机动" : "进入接舷近战", Gold, "Battle Stage Transition");
                    if (after.Stage == NavalBattleStage.Boarding) yield return AnimateBoardingSceneTransition();
                }
            }
            finally
            {
                CompleteStagedBattleAnimation();
            }
        }

        private void CompleteStagedBattleAnimation()
        {
            try
            {
                CloseRamComic();
                CloseBoardingTransition();
                if (this != null && isActiveAndEnabled)
                {
                    // Unwrap old tutorial callbacks before rebinding retained cards;
                    // reapply the current red-frame guidance after ordinary UI state.
                    RestoreGuidedTutorialControls(false);
                    UpdateGuidedTutorialProgress();
                    if (!TryRefreshRetainedBattleView()) Render();
                    else
                    {
                        BuildGuidedTutorialActions();
                        BuildGuidedTutorialOverlay();
                    }
                }
            }
            finally
            {
                // Keep callbacks blocked until the retained controls match the model.
                battleAnimationPlaying = false;
            }
        }

        private IEnumerator AnimateStagedBattleResolution(RectTransform stage, RectTransform playerShip, RectTransform enemyShip,
            BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            bool boardingRules = before.Stage == NavalBattleStage.Boarding || after.Stage == NavalBattleStage.Boarding;
            bool lost = after.PlayerHull <= 0 || (boardingRules && after.PlayerCrew <= 0) ||
                (session.Settlement != null && session.Settlement.Outcome == VoyageOutcome.Failure);
            RectTransform defeated = lost ? playerShip : enemyShip;
            bool boarding = BattleResolutionMotion.DisplayedKind(defeated) == BattleResolutionKind.BoardingDefeat;
            string message = lost
                ? boarding ? "船员战败 · 本次远航失利" : "旗舰失去航行能力"
                : boarding ? "敌方投降 · 夺船胜利" : "敌舰沉没 · 海战胜利";
            int fragments = after.TreasureFragments - before.TreasureFragments;
            if (!lost && fragments > 0) message += "\n藏宝图碎片 +" + fragments;
            yield return AnimateDefeat(stage, defeated, message);
        }

        private IEnumerator AnimateSchoolContactDamage(Image hull, int before, int after, int maximum,
            int guardBefore, int guardAfter, RectTransform stage)
        {
            // Authoritative values have already committed. The meter's visual drain
            // starts when the school trajectory reaches its target.
            yield return new WaitForSecondsRealtime(BattleSchoolAttackGraphic.Duration * BattleSchoolAttackGraphic.ContactProgress);
            if (stage == null || hull == null) yield break;
            yield return AnimateBattleDamage(hull, before, after, maximum, guardBefore, guardAfter, stage);
        }

        private IEnumerator AnimateBattleManeuver(RectTransform stage, RectTransform playerShip, RectTransform enemyShip,
            BattleAnimationSnapshot before, BattleAnimationSnapshot after, bool enemyActing)
        {
            if (stage != null && stage.GetComponentInChildren<NavalTacticalBoardView>() != null)
            {
                yield return AnimateTacticalNavalMovement(stage, before, after);
                yield break;
            }
            PlaySfx("battle_maneuver", enemyActing ? 0.72f : 0.78f);
            foreach (RectTransform ship in new[] { playerShip, enemyShip })
            {
                BattleShipIdleMotion idle = ship.GetComponent<BattleShipIdleMotion>();
                if (idle != null) idle.enabled = false;
            }
            float delta = before.Distance - after.Distance;
            float fromYaw = BattleViewLayout.EnemyYaw(before.Alignment);
            float toYaw = BattleViewLayout.EnemyYaw(after.Alignment);
            Text range = FindBattleReadout(stage, "Battle Range");
            float duration = before.Alignment != after.Alignment ? 0.44f : 0.36f;
            BattleFxGraphic playerWake = CreateManeuverWake(stage, "Player", new Color(0.35f, 0.86f, 0.88f, 0.72f));
            BattleFxGraphic enemyWake = CreateManeuverWake(stage, "Enemy", new Color(0.90f, 0.66f, 0.40f, 0.62f));
            string message = Mathf.Approximately(before.Distance, after.Distance) ? "调整舰位" :
                (delta > 0f ? "靠近 " : "拉开 ") + Mathf.Abs(delta).ToString("0") + " 米";
            Coroutine caption = StartCoroutine(AnimateFloatingText(stage, new Vector2(0f, 95f), message, Teal, "Battle Maneuver Float"));
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (stage == null || playerShip == null || enemyShip == null) yield break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                float distance = Mathf.Lerp(before.Distance, after.Distance, t);
                ApplyBattleViewPose(stage, distance, before.Stage, Mathf.Lerp(fromYaw, toYaw, t));
                if (range != null) range.text = "舰距 " + distance.ToString("0") + " 米";
                if (playerWake != null)
                {
                    playerWake.Progress = t;
                    RectTransform water = stage.Find("Player Ship Water Contact") as RectTransform;
                    if (water != null) playerWake.rectTransform.anchoredPosition = BattleEffectPosition(stage, water) + Vector2.down * 6f;
                }
                if (enemyWake != null)
                {
                    enemyWake.Progress = t;
                    RectTransform water = stage.Find("Enemy Ship Water Contact") as RectTransform;
                    if (water != null) enemyWake.rectTransform.anchoredPosition = BattleEffectPosition(stage, water) + Vector2.down * 6f;
                }
                yield return null;
            }
            ApplyBattleViewPose(stage, after.Distance, after.Stage, after.Alignment);
            if (playerWake != null) Destroy(playerWake.gameObject);
            if (enemyWake != null) Destroy(enemyWake.gameObject);
            if (caption != null) yield return caption;
            // Final pose equals the subsequent rebuild: no end-of-animation snap.
        }

        private Text FindBattleReadout(RectTransform stage, string name)
        {
            Text local = stage == null ? null : stage.Find(name)?.GetComponent<Text>();
            if (local != null) return local;
            return contentRoot == null ? null : contentRoot.Find("Battle Readouts Overlay/" + name)?.GetComponent<Text>();
        }

        private BattleFxGraphic CreateManeuverWake(RectTransform stage, string side, Color tint)
        {
            RectTransform water = stage == null ? null : stage.Find(side + " Ship Water Contact") as RectTransform;
            if (water == null) water = stage == null ? null : stage.Find(side + " Ship Water Shadow") as RectTransform;
            if (water == null) return null;
            Vector2 position = BattleEffectPosition(stage, water) + Vector2.down * 6f;
            BattleFxGraphic wake = CreateBattleFx(side + " Maneuver Wake", stage, BattleFxKind.Splash,
                new Vector2(300f, 92f), position, tint, side == "Enemy" ? 17 : 11);
            wake.Progress = 0.04f;
            return wake;
        }
        private string BattleCardBuffChange(BattleAnimationSnapshot before, BattleAnimationSnapshot after)
        {
            string result = string.Empty;
            if (after.PlayerLoadedShotDamageBonus > before.PlayerLoadedShotDamageBonus)
                result = "装填加伤 +" + ((long)after.PlayerLoadedShotDamageBonus - before.PlayerLoadedShotDamageBonus);
            if (after.PlayerDeckBuff > before.PlayerDeckBuff)
                result += (result.Length == 0 ? string.Empty : "\n") + "甲板强化 +" + (after.PlayerDeckBuff - before.PlayerDeckBuff);
            if (after.PlayerFireRounds > before.PlayerFireRounds)
                result += (result.Length == 0 ? string.Empty : "\n") + "烈焰强化 +" + (after.PlayerFireRounds - before.PlayerFireRounds) + " 发";
            return result;
        }

        private static int StableBattleStringHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int index = 0; index < value.Length; index++) hash = hash * 31 + value[index];
                return hash;
            }
        }

        private static Vector2 BattleEffectPosition(RectTransform stage, RectTransform ship) => stage.InverseTransformPoint(ship.position);

        private static float DirectionToward(RectTransform source, RectTransform target) => target.position.x < source.position.x ? -1f : 1f;

        private sealed class BattleAnimationSnapshot
        {
            public readonly NavalBattleStage Stage;
            public readonly NavalBattleAlignment Alignment;
            public readonly float Distance;
            public readonly int TacticalRevision;
            public readonly CoreNavalTacticalMovement TacticalMovement;
            public readonly int PlayerHull, PlayerMaxHull, EnemyHull, EnemyMaxHull;
            public readonly int PlayerCrew, PlayerMaxCrew, EnemyCrew, EnemyMaxCrew;
            public readonly int PlayerBlock, EnemyBlock, PlayerLoadedShotDamageBonus, EnemyLoadedShot, PlayerDeckBuff, PlayerFireRounds, EnemyBurnTurns, EnemyBurnDamage, HandCount;
            public readonly int EnemyGuardAbsorbed;
            public readonly int TreasureFragments;

            public BattleAnimationSnapshot(CoreVoyageSession current, CoreBattleState battle)
            {
                Stage = battle.Stage;
                Alignment = battle.Alignment;
                Distance = battle.DistanceMeters;
                TacticalRevision = battle.TacticalRevision;
                TacticalMovement = battle.LastTacticalMovement;
                PlayerHull = current.Hull;
                PlayerMaxHull = current.MaxHull;
                EnemyHull = battle.EnemyHull;
                EnemyMaxHull = battle.EnemyMaxHull;
                PlayerCrew = battle.PlayerBoardingHealth;
                PlayerMaxCrew = battle.PlayerMaxBoardingHealth;
                EnemyCrew = battle.EnemyBoardingHealth;
                EnemyMaxCrew = battle.EnemyMaxBoardingHealth;
                PlayerBlock = battle.PlayerBlock;
                EnemyGuardAbsorbed = battle.LastEnemyGuardAbsorbed;
                EnemyBlock = battle.EnemyBlock;
                PlayerLoadedShotDamageBonus = current.PlayerLoadedShotDamageBonus;
                EnemyLoadedShot = battle.EnemyLoadedShot;
                PlayerDeckBuff = battle.PlayerDeckBuff;
                PlayerFireRounds = battle.PlayerFireRounds;
                EnemyBurnTurns = battle.EnemyBurnTurns;
                EnemyBurnDamage = battle.EnemyBurnDamage;
                HandCount = battle.Hand.Count;
                TreasureFragments = current.VoyageTreasureFragments;
            }
        }

        private void BeginTutorialBattleAnimation(Button source, TutorialBattleAction action)
        {
            if (battleAnimationPlaying || source == null || tutorial.Phase != TutorialVoyagePhase.Battle)
            {
                return;
            }

            StartCoroutine(AnimateTutorialBattleAction(source, action));
        }

        private IEnumerator AnimateTutorialBattleAction(Button source, TutorialBattleAction action)
        {
            battleAnimationPlaying = true;
            RectTransform stage = GameObject.Find("Tutorial Battle Card")?.GetComponent<RectTransform>();
            RectTransform playerShip = GameObject.Find("Tutorial Player Battle Ship")?.GetComponent<RectTransform>();
            RectTransform enemyShip = GameObject.Find("Tutorial Enemy Battle Ship")?.GetComponent<RectTransform>();
            Image playerHullBar = GameObject.Find("Tutorial Player Hull Bar")?.GetComponent<Image>();
            Image enemyHullBar = GameObject.Find("Tutorial Enemy Hull Bar")?.GetComponent<Image>();
            if (stage == null || playerShip == null || enemyShip == null)
            {
                tutorial.PerformBattleAction(action);
                battleAnimationPlaying = false;
                Render();
                yield break;
            }

            LockBattleInput(stage);
            Color effectColor = action == TutorialBattleAction.FireCannons ? Coral : action == TutorialBattleAction.Brace ? Teal : Green;
            RectTransform playedCard = source.GetComponent<RectTransform>();
            PlaySfx("battle_card_play", 0.78f);
            yield return RaiseCardToCenter(playedCard, stage, effectColor);

            int enemyHullBefore = tutorial.BattleEnemyHull;
            int playerHullBefore = tutorial.Hull;
            int playerBlockBefore = tutorial.BattleBlock;
            int playerHullBeforeCounter = action == TutorialBattleAction.EmergencyRepair
                ? Mathf.Min(TutorialVoyageSession.MaxHull, playerHullBefore + 6)
                : playerHullBefore;

            if (action == TutorialBattleAction.FireCannons)
            {
                yield return AnimateProjectile(stage, playerShip.anchoredPosition, enemyShip.anchoredPosition, Coral, "Tutorial Player Projectile");
            }

            tutorial.PerformBattleAction(action);
            int enemyDamage = Mathf.Max(0, enemyHullBefore - tutorial.BattleEnemyHull);
            int playerDamage = Mathf.Max(0, playerHullBeforeCounter - tutorial.Hull);

            if (enemyDamage > 0)
            {
                yield return AnimateImpact(stage, enemyShip, Coral, "-" + enemyDamage, "Tutorial Enemy Hit");
                yield return AnimateHealthBar(enemyHullBar, enemyHullBefore, tutorial.BattleEnemyHull, tutorial.BattleEnemyMaxHull, Coral);
            }
            else if (action == TutorialBattleAction.Brace)
            {
                yield return AnimateAura(stage, playerShip, Teal, "格挡 +8", "Tutorial Player Shield");
            }
            else if (action == TutorialBattleAction.EmergencyRepair)
            {
                int repaired = Mathf.Max(0, playerHullBeforeCounter - playerHullBefore);
                yield return AnimateAura(stage, playerShip, Green, "船体 +" + repaired, "Tutorial Player Repair");
                yield return AnimateHealthBar(playerHullBar, playerHullBefore, playerHullBeforeCounter, TutorialVoyageSession.MaxHull, Green);
            }

            if (tutorial.Phase == TutorialVoyagePhase.Battle)
            {
                yield return AnimateProjectile(stage, enemyShip.anchoredPosition, playerShip.anchoredPosition, Coral, "Tutorial Enemy Projectile");
                string counterResult = playerDamage > 0 ? "-" + playerDamage : playerBlockBefore > 0 || action == TutorialBattleAction.Brace ? "格挡" : "未命中";
                yield return AnimateImpact(stage, playerShip, Coral, counterResult, "Tutorial Player Hit");
                if (playerDamage > 0)
                {
                    yield return AnimateHealthBar(playerHullBar, playerHullBeforeCounter, tutorial.Hull, TutorialVoyageSession.MaxHull, Coral);
                }
                yield return FadePlayedCard(playedCard);
            }
            else
            {
                yield return AnimateDefeat(stage, enemyShip, "教学海战胜利");
            }

            battleAnimationPlaying = false;
            Render();
        }

        private IEnumerator RaiseCardToCenter(RectTransform card, RectTransform stage, Color color)
        {
            Vector3 worldStart = card.position;
            Vector3 worldScale = card.lossyScale;
            BattleCardHoverFeedback hover = card.GetComponent<BattleCardHoverFeedback>();
            if (hover != null) hover.enabled = false;
            card.SetParent(stage, true);
            float startScale = worldScale.x / stage.lossyScale.x;
            card.anchorMin = card.anchorMax = Vector2.one * 0.5f;
            card.pivot = Vector2.one * 0.5f;
            card.SetAsLastSibling();
            Vector2 start = stage.InverseTransformPoint(worldStart);
            Vector2 focus = new Vector2(0f, 30f);
            CanvasGroup group = GetOrAddCanvasGroup(card.gameObject);
            group.blocksRaycasts = false;
            Canvas cardCanvas = card.GetComponent<Canvas>();
            if (cardCanvas != null) cardCanvas.overrideSorting = false;
            BattleFxGraphic ring = CreateBattleFx("Battle Card Activation Glow", stage, BattleFxKind.RuneRing,
                new Vector2(350f, 350f), focus, color);
            ring.transform.SetSiblingIndex(card.GetSiblingIndex());
            BattleFxGraphic shine = CreateBattleFx("Battle Card Foil Sweep", card, BattleFxKind.CardShine,
                card.rect.size - Vector2.one * 8f, Vector2.zero, Color.white);
            // A short press, then an arced reveal with a spring settle. The card
            // remains face-up: this is a played card, not a random prize reveal.
            const float duration = 0.22f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (card == null || ring == null || shine == null) yield break;
                float t = elapsed / duration;
                if (t < 0.16f)
                {
                    float press = t / 0.16f;
                    card.anchoredPosition = start + Vector2.down * (9f * press);
                    card.localScale = Vector3.one * Mathf.Lerp(startScale, startScale * 0.94f, press);
                }
                else
                {
                    float u = (t - 0.16f) / 0.84f;
                    float travel = EaseOutCubic(u);
                    card.anchoredPosition = Vector2.Lerp(start, focus, travel) + Vector2.up * (35f * Mathf.Sin(u * Mathf.PI));
                    float spring = Mathf.Sin(u * Mathf.PI) * 0.11f;
                    card.localScale = Vector3.one * (Mathf.Lerp(startScale * 0.94f, 1.1f, travel) + spring);
                    card.localRotation = Quaternion.Euler(0f, Mathf.Sin(u * Mathf.PI) * -24f,
                        Mathf.Lerp(start.x < 0f ? 8f : -8f, -3f, travel));
                }
                ring.Progress = Mathf.Clamp01(t * 0.8f);
                shine.Progress = Mathf.Clamp01((t - 0.25f) / 0.75f);
                yield return null;
            }
            card.anchoredPosition = focus;
            card.localScale = Vector3.one * 1.1f;
            card.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Destroy(ring.gameObject);
            Destroy(shine.gameObject);
        }

        private IEnumerator AnimateCardCastStamp(RectTransform stage, string cardType, Color color)
        {
            if (stage == null || string.IsNullOrEmpty(cardType)) yield break;
            const float duration = 0.34f;
            Vector2 position = new Vector2(0f, -22f);
            BattleFxGraphic ring = CreateBattleFx("Battle Card Cast Stamp Ring", stage, BattleFxKind.RuneRing,
                new Vector2(245f, 130f), position, color, StableBattleStringHash(cardType));
            Text stamp = CreateText("Battle Card Cast Stamp", stage, new Vector2(360f, 44f), position,
                20, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            stamp.text = "出牌 · " + cardType;
            AddSceneLabelOutline(stamp);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (ring == null || stamp == null) yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = t < 0.62f ? 1f : 1f - Mathf.InverseLerp(0.62f, 1f, t);
                float lift = Mathf.Lerp(0f, 22f, EaseOutCubic(t));
                ring.Progress = Mathf.Clamp01(t * 1.25f);
                ring.rectTransform.anchoredPosition = position + Vector2.up * lift;
                stamp.rectTransform.anchoredPosition = position + Vector2.up * lift;
                stamp.rectTransform.localScale = Vector3.one * (0.86f + 0.14f * EaseOutCubic(Mathf.Clamp01(t * 2.2f)));
                stamp.color = new Color(color.r, color.g, color.b, fade);
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
            if (stamp != null) Destroy(stamp.gameObject);
        }
        private IEnumerator AnimateProjectile(RectTransform stage, Vector2 from, Vector2 to, Color color, string name, bool playSound = true)
        {
            if (playSound) PlaySfx("battle_cannon_fire", 0.92f);
            Vector2 direction = (to - from).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            BattleFxGraphic muzzle = CreateBattleFx(name + " Muzzle Flash", stage, BattleFxKind.MuzzleFlash,
                new Vector2(175f, 106f), from + direction * 40f, Color.white);
            muzzle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            BattleFxGraphic smoke = CreateBattleFx(name + " Smoke", stage, BattleFxKind.Smoke,
                new Vector2(230f, 150f), from, Color.white);
            BattleFxGraphic trail = CreateBattleFx(name + " Trail", stage, BattleFxKind.Trail,
                new Vector2(165f, 34f), from, color);
            Image shot = CreateImage(name, stage, new Vector2(12f, 12f), from, new Color(0.16f, 0.14f, 0.1f), circleSprite);
            shot.raycastTarget = false;
            Vector2 midpoint = (from + to) * 0.5f + Vector2.up * 34f;
            float duration = Mathf.Lerp(0.30f, 0.46f, Mathf.InverseLerp(350f, 1100f, Vector2.Distance(from, to)));
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (stage == null || shot == null) yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector2 first = Vector2.Lerp(from, midpoint, t);
                Vector2 second = Vector2.Lerp(midpoint, to, t);
                Vector2 position = Vector2.Lerp(first, second, t);
                Vector2 heading = (second - first).normalized;
                shot.rectTransform.anchoredPosition = position;
                trail.rectTransform.anchoredPosition = position - heading * 68f;
                trail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                trail.Progress = t * 0.8f;
                muzzle.Progress = Mathf.Clamp01(elapsed / 0.19f);
                smoke.Progress = t * 0.7f;
                smoke.rectTransform.anchoredPosition = from - direction * (t * 28f) + Vector2.up * (t * 25f);
                yield return null;
            }
            Destroy(muzzle.gameObject); Destroy(trail.gameObject); Destroy(shot.gameObject);
            // Smoke disperses while the impact starts, not after the entire hit.
            StartCoroutine(DisperseBattleSmoke(smoke));
        }

        private IEnumerator DisperseBattleSmoke(BattleFxGraphic smoke)
        {
            for (float elapsed = 0f; elapsed < 0.26f; elapsed += Time.unscaledDeltaTime)
            {
                if (smoke == null) yield break;
                smoke.Progress = Mathf.Lerp(0.7f, 1f, elapsed / 0.26f);
                yield return null;
            }
            if (smoke != null) Destroy(smoke.gameObject);
        }

        private IEnumerator AnimateImpact(RectTransform stage, RectTransform target, Color color, string label, string name)
        {
            if (name.Contains("Melee") && IsBoardingVisualTarget(target))
            {
                yield return AnimateBoardingHit(stage, target, color, label, name);
                yield break;
            }
            string impactSound = name.Contains("Melee") ? "battle_melee_impact" :
                name.Contains("Ram") ? "battle_ram_impact" : "battle_impact";
            PlaySfx(impactSound, 0.88f);
            BattleShipIdleMotion idle = target.GetComponent<BattleShipIdleMotion>();
            bool idleWasEnabled = idle != null && idle.enabled;
            if (idle != null) idle.enabled = false;
            Vector2 targetOrigin = target.anchoredPosition;
            bool melee = name.Contains("Melee");
            Vector2 origin = BattleEffectPosition(stage, target) + Vector2.down * (melee ? 10f : target.rect.height * target.localScale.y * 0.23f);
            if (stage.GetComponentInChildren<NavalTacticalBoardView>() != null) origin = BattleEffectPosition(stage, target);
            BattleFxGraphic flash = CreateBattleFx(name + " Flash", stage, melee ? BattleFxKind.Slash : BattleFxKind.Impact,
                new Vector2(255f, 195f), origin, Color.white);
            BattleFxGraphic wave = CreateBattleFx(name + " Shockwave", stage, BattleFxKind.Shockwave,
                new Vector2(245f, 140f), origin, color);
            BattleFxGraphic splash = melee ? null : CreateBattleFx(name + " Water Splash", stage, BattleFxKind.Splash,
                new Vector2(235f, 105f), origin + Vector2.down * 50f, Color.white);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage, origin + Vector2.up * 95f, label, color, name + " Float"));
            const float duration = 0.26f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (target == null || flash == null) yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                // A brief impact hold followed by damped horizontal recoil. The
                // waterline stays anchored instead of lifting the ship into air.
                float shake = (1f - t) * (1f - t) * 9f;
                target.anchoredPosition = targetOrigin + Vector2.right * (Mathf.Sin(t * 48f) * shake);
                FollowBattleShipWaterHorizontal(target);
                flash.Progress = t; wave.Progress = t;
                if (splash != null) splash.Progress = t;
                yield return null;
            }
            target.anchoredPosition = targetOrigin;
            FollowBattleShipWaterHorizontal(target);
            if (idle != null) idle.enabled = idleWasEnabled;
            Destroy(flash.gameObject); Destroy(wave.gameObject);
            if (splash != null) Destroy(splash.gameObject);
            yield return number;
        }

        private IEnumerator AnimateAura(RectTransform stage, RectTransform target, Color color, string label, string name, float labelHeight = 105f)
        {
            if (IsBoardingVisualTarget(target) && (name.Contains("Shield") || name.Contains("Repair")))
            {
                yield return AnimateBoardingAura(stage, target, color, label, name, labelHeight);
                yield break;
            }
            PlaySfx(name.Contains("Repair") ? "battle_repair" : "battle_guard", 0.78f);
            Vector2 origin = BattleEffectPosition(stage, target);
            BattleFxGraphic rune = CreateBattleFx(name + " Rune", stage, BattleFxKind.RuneRing,
                new Vector2(280f, 260f), origin, color);
            BattleFxGraphic ring = CreateBattleFx(name + " Ring", stage, BattleFxKind.Shockwave,
                new Vector2(330f, 200f), origin + Vector2.down * 45f, color);
            Coroutine number = StartCoroutine(AnimateFloatingText(stage, origin + new Vector2(0f, labelHeight), label, color, name + " Float"));
            Coroutine outer = StartCoroutine(PlayBattleFx(ring, 0.36f));
            yield return PlayBattleFx(rune, 0.36f);
            yield return outer;
            yield return number;
        }

        private static IEnumerator AnimateHealthBar(Image bar, int before, int after, int maximum, Color flashColor)
        {
            if (bar == null) yield break;
            BattleDefenseHud hud = bar.GetComponentInParent<BattleDefenseHud>();
            float from = Mathf.Clamp01(before / (float)Mathf.Max(1, maximum));
            float to = Mathf.Clamp01(after / (float)Mathf.Max(1, maximum));
            Color baseColor = bar.color;
            bar.fillAmount = from;
            const float duration = 0.28f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (bar == null) yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                float front = EaseOutCubic(Mathf.Clamp01(t / 0.62f));
                float chip = EaseOutCubic(Mathf.Clamp01((t - 0.28f) / 0.72f));
                if (hud != null)
                    hud.SetHull(Mathf.Lerp(before, after, front), Mathf.Lerp(before, after, before > after ? chip : front));
                else bar.fillAmount = Mathf.Lerp(from, to, front);
                bar.color = Color.Lerp(baseColor, Color.white, Mathf.Max(0f, 1f - t * 4f) * 0.65f);
                yield return null;
            }
            bar.fillAmount = to;
            bar.color = baseColor;
            if (hud != null) hud.SetHull(after, after);
        }

        private IEnumerator AnimateFloatingText(RectTransform stage, Vector2 position, string value, Color color, string name)
        {
            // Lift teal numbers above the sea's similar colors without a heavy outline.
            if (color == Teal) color = Color.Lerp(color, Color.white, 0.6f);
            Text text = CreateText(name, stage, new Vector2(320f, 62f), position, 30, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            text.text = value;
            // The display face already has heavy strokes; a wide opaque outline
            // reads as offset copies while the label moves and scales.
            AddSceneLabelOutline(text);
            float duration = 0.42f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (text == null)
                {
                    yield break;
                }
                float t = Mathf.Clamp01(elapsed / duration);
                text.rectTransform.anchoredPosition = position + Vector2.up * (50f * EaseOutCubic(t));
                text.rectTransform.localScale = Vector3.one * (0.8f + Mathf.Sin(Mathf.Min(1f, t * 2f) * Mathf.PI * 0.5f) * 0.25f);
                text.color = new Color(color.r, color.g, color.b, 1f - Mathf.Max(0f, (t - 0.55f) / 0.45f));
                yield return null;
            }
            if (text != null)
            {
                Destroy(text.gameObject);
            }
        }

        private IEnumerator AnimateShipWindup(RectTransform ship, float direction, bool ram)
        {
            if (!ram && IsBoardingVisualTarget(ship))
            {
                yield return AnimateBoardingWindup(ship, direction);
                yield break;
            }
            PlaySfx(ram ? "battle_ram_charge" : "battle_melee", ram ? 0.9f : 0.82f);
            BattleShipIdleMotion idle = ship.GetComponent<BattleShipIdleMotion>();
            bool idleWasEnabled = idle != null && idle.enabled;
            if (idle != null) idle.enabled = false;
            Vector2 origin = ship.anchoredPosition;
            Vector3 scale = ship.localScale;
            Quaternion rotation = ship.localRotation;
            float duration = 0.28f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                ship.anchoredPosition = origin + Vector2.right * direction * pulse * 32f;
                ship.localScale = scale * (1f + pulse * 0.08f);
                // Preserve the current facing and add a small bow-down lurch so a
                // melee/ram action reads as forward motion rather than a pure pulse.
                ship.localRotation = rotation * Quaternion.Euler(0f, 0f, -direction * 4f * pulse);
                FollowBattleShipWaterHorizontal(ship);
                yield return null;
            }
            ship.anchoredPosition = origin;
            ship.localScale = scale;
            ship.localRotation = rotation;
            FollowBattleShipWaterHorizontal(ship);
            if (idle != null) idle.enabled = idleWasEnabled;
        }

        private IEnumerator AnimateDefeat(RectTransform stage, RectTransform ship, string message)
        {
            yield return AnimateBattleResolutionVisual(stage, ship, message);
        }

        private IEnumerator FadePlayedCard(RectTransform card)
        {
            CanvasGroup group = GetOrAddCanvasGroup(card.gameObject);
            float duration = 0.18f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = 1f - t;
                card.localScale = Vector3.one * Mathf.Lerp(1.22f, 0.72f, t);
                yield return null;
            }
        }

        private static void LockBattleInput(RectTransform stage)
        {
            Button[] buttons = stage.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index += 1)
            {
                buttons[index].interactable = false;
            }
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject target)
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private static Color CardEffectColor(CoreBattleCard card)
        {
            return BattleCardStylePalette.ForType(card.Type).Accent;
        }

        private static float EaseOutCubic(float value)
        {
            float t = Mathf.Clamp01(value);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }

    internal sealed class BattleCardHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rect;
        private Vector3 targetScale = Vector3.one;
        private float restingScale = 1f;
        private float inspectionScale = 1f;
        private Quaternion targetRotation = Quaternion.identity;
        private Quaternion restingRotation = Quaternion.identity;
        private bool fanHand;
        private Outline fanHighlight;
        private Vector2 restingPosition;
        private Vector2 targetPosition;
        private bool positionCaptured;
        private bool hovering;
        private bool pressed;
        private Color accent = Color.white;
        private Image background;
        private Color restingColor;
        private RectTransform keywordTooltip;
        private CanvasGroup keywordTooltipGroup;
        private Canvas hoverCanvas;
        private Vector4 restingRaycastPadding;
        private Rect frameHitBounds;

        public void Configure(Color effectColor, RectTransform tooltip = null)
        {
            rect = GetComponent<RectTransform>();
            background = GetComponent<Image>();
            hoverCanvas = gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();
            accent = effectColor;
            if (background != null)
            {
                restingColor = background.color;
                RefreshFrameHitArea();
            }
            SetKeywordTooltip(tooltip);
        }

        public void RefreshFrameHitArea()
        {
            if (rect == null || background == null) return;
            restingRaycastPadding = background.raycastPadding;
            Rect body = rect.rect;
            frameHitBounds = Rect.MinMaxRect(body.xMin + restingRaycastPadding.x,
                body.yMin + restingRaycastPadding.y, body.xMax - restingRaycastPadding.z,
                body.yMax - restingRaycastPadding.w);
        }

        public void SetKeywordTooltip(RectTransform tooltip)
        {
            if (keywordTooltip == tooltip) return;
            if (keywordTooltipGroup != null) keywordTooltipGroup.alpha = 0f;
            if (keywordTooltip != null) keywordTooltip.gameObject.SetActive(false);
            keywordTooltip = tooltip;
            keywordTooltipGroup = null;
            if (keywordTooltip != null)
            {
                keywordTooltipGroup = keywordTooltip.GetComponent<CanvasGroup>();
                if (keywordTooltipGroup == null) keywordTooltipGroup = keywordTooltip.gameObject.AddComponent<CanvasGroup>();
                keywordTooltipGroup.alpha = 0f;
                keywordTooltipGroup.interactable = false;
                keywordTooltipGroup.blocksRaycasts = false;
                keywordTooltip.gameObject.SetActive(hovering);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverCanvas != null) { hoverCanvas.overrideSorting = true; hoverCanvas.sortingOrder = 30; }
            if (rect != null)
            {
                if (!positionCaptured) restingPosition = rect.anchoredPosition;
                positionCaptured = true;
            }
            hovering = true;
            pressed = false;
            targetRotation = fanHand ? Quaternion.identity : Quaternion.Euler(0f, 0f, -0.7f);
            RefreshInspectionTarget();
            if (fanHighlight != null) fanHighlight.enabled = fanHand;
            if (keywordTooltip != null) keywordTooltip.gameObject.SetActive(true);
        }

        private void RefreshInspectionTarget()
        {
            if (rect == null || !positionCaptured) return;
            targetPosition = restingPosition + Vector2.up * (46f +
                (1f - restingScale) * CardFaceLayout.FrameSize.y * .5f);
            inspectionScale = 1f;
            if (rect.parent is RectTransform hand && hand.name == "Battle Hand")
            {
                if (fanHand) FitFanInspection(hand);
                else FitInspectionToClearSpace(hand);
            }
            targetScale = Vector3.one * inspectionScale * (pressed ? .98f : 1f);
        }

        private void FitFanInspection(RectTransform hand)
        {
            Canvas canvas = hand.GetComponentInParent<Canvas>();
            RectTransform screen = canvas != null ? FixedGameViewport.Bounds(canvas) : hand.parent as RectTransform;
            Rect screenBounds = BoundsRelativeTo(screen, hand);
            Rect frame = RotatedInspectionBounds();
            // Reserve the upper two thirds for the chart or boarding crew.
            // Bottom alignment also brings every corner cost fully on screen.
            inspectionScale = Mathf.Min(1f, (screenBounds.height * .32f - 16f) / frame.height);
            inspectionScale = Mathf.Max(restingScale, inspectionScale);
            targetPosition.x = Mathf.Clamp(restingPosition.x,
                Mathf.Max(hand.rect.xMin, screenBounds.xMin) - frame.xMin * inspectionScale + 4f,
                Mathf.Min(hand.rect.xMax, screenBounds.xMax) - frame.xMax * inspectionScale - 4f);
            targetPosition.y = screenBounds.yMin + 8f - frame.yMin * inspectionScale;
        }

        public void RefreshInspectionLayoutImmediately()
        {
            if (!hovering || rect == null) return;
            RefreshInspectionTarget();
            rect.anchoredPosition = targetPosition;
            rect.localScale = targetScale;
            rect.localRotation = targetRotation;
            UpdatePointerFootprint();
        }

        private void FitInspectionToClearSpace(RectTransform hand)
        {
            // Preserve a full-size inspection wherever possible. A taller card
            // beside a boarding party needs the largest scale that fits both
            // the visible screen and the clear space below that party.
            Canvas canvas = hand.GetComponentInParent<Canvas>();
            RectTransform screen = canvas != null ? FixedGameViewport.Bounds(canvas) : hand;
            Rect screenBounds = BoundsRelativeTo(screen, hand);
            Rect frame = RotatedInspectionBounds();
            float lower = screenBounds.yMin + 6f;
            float upper = screenBounds.yMax - 6f;
            targetPosition.x = Mathf.Clamp(targetPosition.x,
                Mathf.Max(hand.rect.xMin, screenBounds.xMin) - frame.xMin + 4f,
                Mathf.Min(hand.rect.xMax, screenBounds.xMax) - frame.xMax - 4f);
            foreach (string name in new[] { "Player Ship Art", "Enemy Ship Art" })
            {
                RectTransform actor = hand.parent.Find(name) as RectTransform;
                if (actor == null) continue;
                Rect actorBounds = BoundsRelativeTo(actor, hand);
                if (targetPosition.x + frame.xMax + 3f > actorBounds.xMin &&
                    targetPosition.x + frame.xMin - 3f < actorBounds.xMax)
                    upper = Mathf.Min(upper, actorBounds.yMin - 6f);
            }
            inspectionScale = Mathf.Min(1f, (upper - lower) / frame.height);
            inspectionScale = Mathf.Max(.01f, inspectionScale);
            targetPosition.y = Mathf.Clamp(targetPosition.y,
                lower - frame.yMin * inspectionScale, upper - frame.yMax * inspectionScale);
        }

        private Rect RotatedInspectionBounds()
        {
            Vector2 minimum = frameHitBounds.min;
            Vector2 maximum = frameHitBounds.max;
            Quaternion rotation = fanHand ? Quaternion.identity : Quaternion.Euler(0f, 0f, -.7f);
            for (int index = 0; index < 4; index++)
            {
                Vector2 corner = new Vector2((index & 1) == 0 ? frameHitBounds.xMin : frameHitBounds.xMax,
                    (index & 2) == 0 ? frameHitBounds.yMin : frameHitBounds.yMax);
                Vector2 rotated = rotation * corner;
                minimum = Vector2.Min(minimum, rotated);
                maximum = Vector2.Max(maximum, rotated);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static Rect BoundsRelativeTo(RectTransform target, RectTransform parent)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 minimum = parent.InverseTransformPoint(corners[0]);
            Vector2 maximum = minimum;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 point = parent.InverseTransformPoint(corners[index]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        public void ResetAfterHandLayout()
        {
            if (rect == null) return;
            restingPosition = targetPosition = rect.anchoredPosition;
            positionCaptured = false;
            hovering = false;
            pressed = false;
            targetScale = Vector3.one * restingScale;
            targetRotation = restingRotation;
            rect.localScale = Vector3.one * restingScale;
            rect.localRotation = restingRotation;
            if (fanHighlight != null) fanHighlight.enabled = false;
            if (hoverCanvas != null) hoverCanvas.overrideSorting = false;
            if (background != null) background.color = restingColor;
            if (background != null) background.raycastPadding = restingRaycastPadding;
            if (keywordTooltipGroup != null) keywordTooltipGroup.alpha = 0f;
            if (keywordTooltip != null) keywordTooltip.gameObject.SetActive(false);
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; }
            enabled = true;
        }

        public void SetRestingScale(float value)
        {
            SetRestingPose(value, Quaternion.identity, false);
        }

        public void SetRestingPose(float value, Quaternion rotation, bool isFanHand)
        {
            restingScale = value;
            restingRotation = rotation;
            fanHand = isFanHand;
            if (fanHand && fanHighlight == null)
            {
                CardSchoolFrameGraphic frame = GetComponentInChildren<CardSchoolFrameGraphic>();
                if (frame != null)
                {
                    fanHighlight = frame.gameObject.AddComponent<Outline>();
                    fanHighlight.effectColor = new Color(.30f, .94f, 1f, .92f);
                    fanHighlight.effectDistance = new Vector2(3f, -3f);
                    fanHighlight.useGraphicAlpha = true;
                }
            }
            ResetAfterHandLayout();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (hoverCanvas != null) hoverCanvas.overrideSorting = false;
            hovering = false;
            targetScale = Vector3.one * restingScale;
            targetRotation = restingRotation;
            if (fanHighlight != null) fanHighlight.enabled = false;
            if (positionCaptured) targetPosition = restingPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            targetScale = Vector3.one * ((hovering ? inspectionScale : restingScale) * 0.98f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            targetScale = Vector3.one * (hovering ? inspectionScale : restingScale);
        }

        private void Update()
        {
            if (rect == null) return;
            if (hovering) RefreshInspectionTarget();
            float blend = 1f - Mathf.Exp(-15f * Time.unscaledDeltaTime);
            rect.localScale = Vector3.Lerp(rect.localScale, targetScale, blend);
            rect.localRotation = Quaternion.Slerp(rect.localRotation, targetRotation, blend);
            if (positionCaptured) rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPosition, blend);
            if (hovering && rect.parent is RectTransform hand && hand.name == "Battle Hand")
            {
                Vector2 bounded = rect.anchoredPosition;
                float inset = CardFaceLayout.FrameSize.x * (fanHand ? inspectionScale : 1f) * .5f + 4f;
                bounded.x = Mathf.Clamp(bounded.x, hand.rect.xMin + inset, hand.rect.xMax - inset);
                rect.anchoredPosition = bounded;
            }
            UpdatePointerFootprint();
            if (background != null)
            {
                float highlight = hovering ? 1f : 0f;
                background.color = Color.Lerp(restingColor, Color.Lerp(restingColor, accent, 0.28f), highlight);
                Color fill = background.color;
                fill.a = restingColor.a;
                background.color = fill;
            }
            if (keywordTooltipGroup != null && keywordTooltip != null)
            {
                keywordTooltipGroup.alpha = Mathf.Lerp(keywordTooltipGroup.alpha, hovering ? 1f : 0f, blend);
                if (!hovering && keywordTooltipGroup.alpha < 0.02f && keywordTooltip.gameObject.activeSelf)
                {
                    keywordTooltipGroup.alpha = 0f;
                    keywordTooltip.gameObject.SetActive(false);
                }
            }
        }

        private void UpdatePointerFootprint()
        {
            if (background == null || !positionCaptured || rect.parent == null) return;
            // Raising a card must not move its hit area away from a stationary
            // pointer at the original bottom corner and repeatedly cancel hover.
            Vector2 minimum = frameHitBounds.min;
            Vector2 maximum = frameHitBounds.max;
            for (int index = 0; index < 4; index++)
            {
                Vector2 corner = new Vector2((index & 1) == 0 ? frameHitBounds.xMin : frameHitBounds.xMax,
                    (index & 2) == 0 ? frameHitBounds.yMin : frameHitBounds.yMax);
                Vector3 world = rect.parent.TransformPoint((Vector3)restingPosition + restingRotation * (Vector3)(corner * restingScale));
                Vector2 point = rect.InverseTransformPoint(world);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            Rect body = rect.rect;
            background.raycastPadding = new Vector4(minimum.x - body.xMin, minimum.y - body.yMin,
                body.xMax - maximum.x, body.yMax - maximum.y);
        }

        private void OnDisable()
        {
            if (rect != null)
            {
                rect.localScale = Vector3.one * restingScale;
                rect.localRotation = restingRotation;
                if (positionCaptured) rect.anchoredPosition = restingPosition;
            }
            if (background != null) background.color = restingColor;
            if (background != null) background.raycastPadding = restingRaycastPadding;
            hovering = false;
            if (fanHighlight != null) fanHighlight.enabled = false;
            if (keywordTooltipGroup != null) keywordTooltipGroup.alpha = 0f;
            if (hoverCanvas != null) hoverCanvas.overrideSorting = false;
            if (keywordTooltip != null) keywordTooltip.gameObject.SetActive(false);
        }
    }

    internal sealed class BattleShipIdleMotion : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 origin;
        private float phase;
        private float lift;
        private float roll;

        public void Configure(float motionPhase, float verticalLift, float rollDegrees)
        {
            rect = GetComponent<RectTransform>();
            origin = rect.anchoredPosition;
            phase = motionPhase;
            lift = verticalLift;
            roll = rollDegrees;
        }

        private void Update()
        {
            if (rect == null) return;
            float wave = Mathf.Sin(Time.unscaledTime * 1.45f + phase);
            float secondary = Mathf.Sin(Time.unscaledTime * 0.73f + phase * 1.7f);
            rect.anchoredPosition = origin + new Vector2(secondary * 1.8f, wave * lift);
            rect.localRotation = Quaternion.Euler(0f, 0f, wave * roll);
        }

        private void OnDestroy()
        {
            if (rect == null) return;
            rect.anchoredPosition = origin;
            rect.localRotation = Quaternion.identity;
        }
    }
}
