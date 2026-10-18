using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_SixRealPlacementsCancelReplaceAndExpireWithoutDuplicatePayment()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetNavalFieldTestPose(session, false, 4, 0);
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            CoreNavalFieldKind[] kinds = { CoreNavalFieldKind.Fire, CoreNavalFieldKind.Arcane,
                CoreNavalFieldKind.Curse, CoreNavalFieldKind.Tide, CoreNavalFieldKind.Thunder, CoreNavalFieldKind.Blood };
            SetResponsiveHand(session, schools.Select(school => "card_school_" + school + "_n01").Concat(new[] { "card_brace" }).ToArray());
            SetResponsiveDrawPile(session, "card_brace", "card_brace", "card_brace");
            session.Battle.Energy = session.Battle.MaxEnergy = 30;
            RenderResponsiveBattle(controller);
            yield return null;
            NavalFieldMotion motion = RequiredResponsive<NavalFieldMotion>("Persistent Naval Fields");
            Assert.That(motion.ActiveCues, Is.Empty);
            int stageId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();
            int energyBeforeCancel = session.Battle.Energy, powderBeforeCancel = session.Cargo.Powder;
            long sequenceBeforeCancel = motion.LastConsumedSequence;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return null;
            Object.FindFirstObjectByType<NavalCardChoiceOverlay>().SelectCell(3, 0);
            Button invalid = RequiredResponsive<Button>("Confirm Naval Card Choice");
            Assert.That(invalid.interactable, Is.False);
            invalid.onClick.Invoke();
            Assert.That(controller.IsNavalCardChoiceOpen, Is.True);
            RequiredResponsive<Button>("Cancel Naval Card Choice").onClick.Invoke();
            yield return null;
            Assert.That(session.Battle.Energy, Is.EqualTo(energyBeforeCancel));
            Assert.That(session.Cargo.Powder, Is.EqualTo(powderBeforeCancel));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.Zero);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(motion.LastConsumedSequence, Is.EqualTo(sequenceBeforeCancel));
            Assert.That(motion.TotalPlacedCues + motion.TotalTriggeredCues + motion.TotalRetiredCues, Is.Zero,
                "Cancelled placement has no committed lifecycle cues.");

            for (int index = 0; index < schools.Length; index++)
            {
                int previousPlaced = motion.TotalPlacedCues, previousRetired = motion.TotalRetiredCues;
                int overlap = session.Battle.NavalFields.Cells.Count(cell =>
                    session.GetNavalFieldShape(kinds[index], 0, 1, false).Any(point =>
                        Mathf.Approximately(point.X, cell.X * 100f) && Mathf.Approximately(point.Y, cell.Y * 100f)));
                int energy = session.Battle.Energy, powder = session.Cargo.Powder, hull = session.Hull;
                int cardsPlayed = session.Battle.CardsPlayedThisTurn;
                int cost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                yield return BeginFieldStatusPlacement(kinds[index], 0, 1);
                UnityEvent confirm = RequiredResponsive<Button>("Confirm Naval Card Choice").onClick;
                confirm.Invoke(); confirm.Invoke();
                CoreNavalFieldKind kind = kinds[index];
                yield return ObserveFieldStatusCue(controller, motion,
                    cue => cue.Event.Change == CoreNavalFieldChangeKind.Placed && cue.Event.Kind == kind,
                    "FieldStatus_Field_" + index.ToString("00") + "_" + schools[index] + ".png",
                    "真实" + schools[index] + "场地铺设：新格展开，被覆盖旧格消退，放置本身不触发");
                Assert.That(motion.TotalPlacedCues - previousPlaced, Is.EqualTo(kind == CoreNavalFieldKind.Thunder ? 3 : 4));
                Assert.That(motion.TotalRetiredCues - previousRetired, Is.EqualTo(overlap));
                Assert.That(motion.TotalTriggeredCues, Is.Zero, "Placement alone must never be portrayed as a field hit.");
                Assert.That(session.Battle.Energy, Is.EqualTo(energy - cost));
                Assert.That(session.Cargo.Powder, Is.EqualTo(powder - (kind == CoreNavalFieldKind.Fire ? 1 : 0)));
                Assert.That(session.Hull, Is.EqualTo(hull - (kind == CoreNavalFieldKind.Blood ? 4 : 0)));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(cardsPlayed + 1));
                Assert.That(session.GetNavalFieldAt(0, 1).Kind, Is.EqualTo(kind));
                Assert.That(session.Battle.NavalFields.Cells.Select(cell => cell.X + ":" + cell.Y).Distinct().Count(),
                    Is.EqualTo(session.Battle.NavalFields.Cells.Count));
                if (kind == CoreNavalFieldKind.Tide)
                    Assert.That(session.Battle.NavalFields.Cells.Where(cell => cell.Kind == kind).All(cell => cell.DirectionX == 0 && cell.DirectionY == 1), Is.True);
                long sequence = motion.LastConsumedSequence;
                confirm.Invoke();
                yield return null;
                Assert.That(motion.LastConsumedSequence, Is.EqualTo(sequence));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(cardsPlayed + 1));
                Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(stageId));
            }

            int ownerTurn = session.Battle.NavalFields.PlayerTurn;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle.NavalFields.PlayerTurn, Is.EqualTo(ownerTurn + 1));
            Assert.That(session.Battle.NavalFields.Cells.Count, Is.GreaterThan(0));
            int remaining = session.Battle.NavalFields.Cells.Count, retiredBeforeExpiry = motion.TotalRetiredCues;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return ObserveFieldStatusCue(controller, motion,
                cue => cue.Event.Change == CoreNavalFieldChangeKind.Expired,
                "FieldStatus_Field_06_Expiry.png", "第二次己方回合开始：规则已清除场地，旧格执行有限消散");
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
            Assert.That(motion.TotalRetiredCues - retiredBeforeExpiry, Is.EqualTo(remaining));
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(motion.ActiveCues, Is.Empty);
            Assert.That(RequiredResponsive<NavalFieldGraphic>("Persistent Naval Fields").Cells, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_RealMovementDefersArcaneStatusUntilArrivalHeadingIsChosen()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetNavalFieldTestPose(session, false, 4, 0);
            SetResponsiveHand(session, "card_school_arcane_n01", "card_brace");
            SetResponsiveDrawPile(session);
            session.Battle.Energy = session.Battle.MaxEnergy = 20;
            RenderResponsiveBattle(controller);
            yield return null;
            NavalFieldMotion fields = RequiredResponsive<NavalFieldMotion>("Persistent Naval Fields");
            yield return BeginFieldStatusPlacement(CoreNavalFieldKind.Arcane, 1, 0);
            RequiredResponsive<Button>("Confirm Naval Card Choice").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(fields.TotalTriggeredCues, Is.Zero);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.Zero);
            BattleStatusRow row = RequiredResponsive<BattleStatusRow>("Player Battle Status Row");
            BattleResourceChangeTracker mana = FieldStatusResource("Mana");
            int manaPlays = mana.Motion.PlayCount;
            yield return DragTacticalRoute(controller, new[] { new VoyagePoint(100f, 0f) });
            Assert.That(session.NavalMovementPlannedSteps, Is.EqualTo(1));
            UnityEvent commit = RequiredResponsive<Button>("Commit Naval Movement").onClick;
            commit.Invoke(); commit.Invoke();
            Assert.That(fields.TotalTriggeredCues, Is.Zero,
                "Committing the complete route must not flash a field before the displayed ship enters it.");
            Assert.That(fields.ActiveCues.Any(cue => cue.Event.Change == CoreNavalFieldChangeKind.Triggered), Is.False);
            yield return ObserveFieldStatusCue(controller, fields,
                cue => cue.Event.Change == CoreNavalFieldChangeKind.Triggered && cue.Event.Kind == CoreNavalFieldKind.Arcane && cue.Event.TargetPlayer,
                "FieldStatus_Field_07_ArcaneEntry.png", "真实路线进入折射阵：场地触发，临时法力即时增加；隐藏状态行等待朝向选择",
                () => Assert.That(RequiredResponsive<NavalTacticalBoardView>("Naval Tactical Board").DisplayedPlayerPositionMeters.X,
                    Is.GreaterThanOrEqualTo(45f), "The displayed ship must approach the next cell before its field trigger begins."));
            Assert.That(session.Battle.Schools.TemporaryMana, Is.EqualTo(1));
            Assert.That(mana.LastTemporaryDelta, Is.EqualTo(1));
            Assert.That(mana.Motion.PlayCount, Is.EqualTo(manaPlays + 1));
            Assert.That(FieldStatusResourceValue("Mana"), Is.EqualTo(session.Battle.Schools.Mana + "+1"));
            Assert.That(session.Battle.NavalAwaitingHeadingSelection, Is.True);
            Assert.That(row.IsSuppressed, Is.True);
            BattleHudChangeMotion piercing = FieldStatusSlotMotion(row, "arcane_piercing");
            Assert.That(piercing, Is.Not.Null);
            Assert.That(piercing.PlayCount, Is.Zero, "Do not spend a status cue while its entire row is hidden.");
            int energy = session.Battle.Energy, cards = session.Battle.CardsPlayedThisTurn;
            yield return WaitTacticalHeadingPointerReady("Naval Arrival Heading 0");
            RequiredResponsive<Button>("Naval Arrival Heading 0").onClick.Invoke();
            yield return ObserveFieldStatusHudMotion(controller, () => piercing, 0, BattleHudChangeKind.Added,
                "FieldStatus_07_DeferredArcaneStatus.png", "选择到达朝向后状态行恢复：奥术穿透只提示一次，法力不重复增加");
            Assert.That(row.IsSuppressed, Is.False);
            Assert.That(session.Battle.Schools.TemporaryMana, Is.EqualTo(1));
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(cards));
            Assert.That(fields.TotalTriggeredCues, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_RealEndTurnKeepsFireTriggerAfterTheRuleLedgerResets()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetNavalFieldTestPose(session, false, 4, 0);
            SetResponsiveHand(session, "card_school_fire_n01", "card_brace");
            SetResponsiveDrawPile(session, "card_brace");
            RenderResponsiveBattle(controller);
            yield return null;
            NavalFieldMotion fields = RequiredResponsive<NavalFieldMotion>("Persistent Naval Fields");
            yield return BeginFieldStatusPlacement(CoreNavalFieldKind.Fire, 0, 0);
            RequiredResponsive<Button>("Confirm Naval Card Choice").onClick.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(fields.TotalTriggeredCues, Is.Zero);
            int hull = session.Hull, turn = session.Battle.NavalFields.PlayerTurn;
            RequiredResponsive<Button>("End Battle Turn").onClick.Invoke();
            yield return ObserveFieldStatusCue(controller, fields,
                cue => cue.Event.Change == CoreNavalFieldChangeKind.Triggered && cue.Event.Kind == CoreNavalFieldKind.Fire && cue.Event.TargetPlayer,
                "FieldStatus_Field_08_FireTurnTick.png", "真实回合末火场伤害：新回合已重置规则去重账本，表现事件仍按序播放");
            Assert.That(session.Hull, Is.LessThanOrEqualTo(hull - 2));
            Assert.That(session.Battle.NavalFields.PlayerTurn, Is.EqualTo(turn + 1));
            Assert.That(session.Battle.NavalFields.TriggerLedger.Contains("P:F:0:0"), Is.False);
            int triggerCount = fields.TotalTriggeredCues;
            RequiredResponsive<NavalFieldBoardOverlay>("Naval Tactical Board").RefreshNow();
            yield return null;
            Assert.That(fields.TotalTriggeredCues, Is.EqualTo(triggerCount), "A refresh cannot replay consumed field events.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_RealManaGainCapAndSpendUseActualDeltasWithoutReplay()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetResponsiveHand(session, "card_school_arcane_b06", "card_school_arcane_b06",
                "card_school_arcane_b06", "card_school_arcane_b03", "card_brace");
            SetResponsiveDrawPile(session, "card_brace", "card_brace", "card_brace");
            session.Battle.Energy = session.Battle.MaxEnergy = 30;
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 0);
            RenderResponsiveBattle(controller);
            yield return null;
            BattleResourceChangeTracker mana = FieldStatusResource("Mana");
            Assert.That(mana.Motion.PlayCount, Is.Zero, "An initial balance is a quiet baseline.");
            BattleResourceChangeTracker energyFeedback = FieldStatusResource("Energy");
            int stageId = RequiredResponsive<RectTransform>("Battle Card").GetInstanceID();

            for (int index = 0; index < 3; index++)
            {
                int before = session.Battle.Schools.Mana, previousPlays = mana.Motion.PlayCount;
                int previousEnergyPlays = energyFeedback.Motion.PlayCount;
                int expectedGain = Math.Min(2, session.Battle.Schools.ManaLimit - before);
                Button card = RequiredResponsive<Button>("Battle Card 0");
                int cardsPlayed = session.Battle.CardsPlayedThisTurn, energy = session.Battle.Energy;
                int cost = session.GetBattleCardEnergyCost(session.Battle.Hand[0]);
                UnityEvent stale = card.onClick;
                stale.Invoke(); stale.Invoke();
                if (expectedGain > 0)
                {
                    yield return ObserveFieldStatusHudMotion(controller, () => mana.Motion,
                        previousPlays, BattleHudChangeKind.Gained, index == 0 ? "FieldStatus_01_ManaGain.png" : null,
                        "真实余辉回收：法力只显示实际增加量，能量同步支付");
                }
                else yield return WaitResponsiveBattle(controller);
                Assert.That(session.Battle.Schools.Mana, Is.EqualTo(before + expectedGain));
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(cardsPlayed + 1));
                Assert.That(session.Battle.Energy, Is.EqualTo(energy - cost));
                Assert.That(energyFeedback.Motion.PlayCount, Is.EqualTo(previousEnergyPlays + 1));
                Assert.That(energyFeedback.LastDelta, Is.EqualTo(-cost));
                Assert.That(mana.Motion.PlayCount, Is.EqualTo(previousPlays + (expectedGain > 0 ? 1 : 0)),
                    "Reaching the cap must not invent gains or replay feedback during the retained refresh.");
                if (expectedGain > 0) Assert.That(mana.LastDelta, Is.EqualTo(expectedGain));
                Assert.That(FieldStatusResourceValue("Mana"), Is.EqualTo(session.Battle.Schools.Mana + "/" + session.Battle.Schools.ManaLimit));
                stale.Invoke();
                yield return null;
                Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(cardsPlayed + 1), "A retired card callback cannot play the next hand entry.");
            }

            int manaBeforeSpend = session.Battle.Schools.Mana, countBeforeSpend = mana.Motion.PlayCount;
            int threshold = session.GetBattleCard(session.Battle.Hand[0]).SchoolEffect.Threshold;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveFieldStatusHudMotion(controller, () => mana.Motion, countBeforeSpend,
                BattleHudChangeKind.Spent, "FieldStatus_02_ManaSpend.png", "真实奥术飞矢：法力消费与权威余额一致");
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(manaBeforeSpend - threshold));
            Assert.That(mana.LastDelta, Is.EqualTo(-threshold));
            Assert.That(mana.Motion.PlayCount, Is.EqualTo(countBeforeSpend + 1));
            Assert.That(RequiredResponsive<RectTransform>("Battle Card").GetInstanceID(), Is.EqualTo(stageId));
            Assert.That(FieldStatusResourceValue("Mana"), Is.EqualTo(session.Battle.Schools.Mana + "/" + session.Battle.Schools.ManaLimit));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_RealGuardAndFireRoundChangesCloseRetiredTooltipAndCleanOnExit()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetResponsiveHand(session, "card_brace", "card_brace", "card_chain_shot", "card_chain_shot", "card_chain_shot");
            SetResponsiveDrawPile(session);
            session.Battle.Energy = session.Battle.MaxEnergy = 30;
            session.Battle.PlayerFireRounds = 2;
            RenderResponsiveBattle(controller);
            yield return null;
            BattleStatusRow row = RequiredResponsive<BattleStatusRow>("Player Battle Status Row");
            BattleHudChangeMotion fire = FieldStatusSlotMotion(row, "fire_rounds");
            Assert.That(fire.PlayCount, Is.Zero, "Fixture statuses must not be advertised as newly gained.");

            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveFieldStatusHudMotion(controller, () => FieldStatusSlotMotion(row, "guard"), 0,
                BattleHudChangeKind.Added, "FieldStatus_03_GuardAdded.png", "真实防御牌：新增格挡状态，原始数值立即显示");
            BattleHudChangeMotion guard = FieldStatusSlotMotion(row, "guard");
            int guardCount = guard.PlayCount, block = session.Battle.PlayerBlock;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveFieldStatusHudMotion(controller, () => guard, guardCount,
                BattleHudChangeKind.Increased, "FieldStatus_04_GuardIncreased.png", "重复防御牌：已有格挡槽保留并显示增量");
            Assert.That(session.Battle.PlayerBlock, Is.GreaterThan(block));
            Assert.That(FieldStatusSlotMotion(row, "guard"), Is.SameAs(guard));

            int fireCount = fire.PlayCount;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            yield return ObserveFieldStatusHudMotion(controller, () => fire, fireCount,
                BattleHudChangeKind.Decreased, "FieldStatus_05_FireRoundSpent.png", "真实炮击：附火从两发降为一发，消耗反馈不改写状态值");
            Assert.That(session.Battle.PlayerFireRounds, Is.EqualTo(1));
            Assert.That(row.transform.Find("Status fire_rounds/Amount").GetComponent<Text>().text, Is.EqualTo("1"));

            ExecuteEvents.Execute(fire.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(GameObject.Find("Battle Status Tooltip"), Is.Not.Null);
            fireCount = fire.PlayCount;
            RequiredResponsive<Button>("Battle Card 0").onClick.Invoke();
            bool sawRemoval = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
            {
                if (fire != null && fire.IsPlaying && fire.PlayCount > fireCount && fire.LastKind == BattleHudChangeKind.Removed)
                {
                    Assert.That(session.Battle.PlayerFireRounds, Is.Zero);
                    Assert.That(row.transform.Find("Status fire_rounds"), Is.Null);
                    Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "fire_rounds"), Is.False);
                    Assert.That(GameObject.Find("Battle Status Tooltip"), Is.Null, "A removed status closes its existing tooltip immediately.");
                    Assert.That(fire.GetComponentsInChildren<Graphic>().All(item => !item.raycastTarget), Is.True,
                        "The fading remnant must not intercept input or reopen its tooltip.");
                    fire.SetElapsed(.12f);
                    NavalBattleVisualCapture.Capture(controller, "FieldStatus_06_StatusRemoved.png", "最后一发附火已消耗：失效图标退出，悬浮说明立即关闭");
                    sawRemoval = true;
                    break;
                }
                yield return null;
            }
            Assert.That(sawRemoval, Is.True, "Observe retirement during the real attack, before the short effect expires.");
            yield return WaitResponsiveBattle(controller);
            Assert.That(row.RetiringStatusCount, Is.Zero);
            Assert.That(row.transform.Find("Retiring Status fire_rounds"), Is.Null);

            // A disposable one-hull target makes the next real shot exercise battle exit.
            session.Battle.EnemyHull = 1;
            Button lethal = RequiredResponsive<Button>("Battle Card 0");
            UnityEvent retiredShot = lethal.onClick;
            retiredShot.Invoke();
            yield return WaitResponsiveBattle(controller);
            Assert.That(session.Battle, Is.Null);
            Assert.That(GameObject.Find("Battle Card"), Is.Null);
            Assert.That(Object.FindObjectsByType<BattleHudChangeMotion>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<NavalFieldBoardOverlay>(FindObjectsSortMode.None), Is.Empty);
            retiredShot.Invoke();
            yield return null;
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_ControllerDisableCancelsCommittedFeedbackWithoutReplaying()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            SetResponsiveHand(session, "card_school_arcane_b06", "card_brace");
            SetResponsiveDrawPile(session);
            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 0);
            RenderResponsiveBattle(controller);
            yield return null;
            BattleResourceChangeTracker mana = FieldStatusResource("Mana");
            UnityEvent stale = RequiredResponsive<Button>("Battle Card 0").onClick;
            stale.Invoke();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!mana.Motion.IsPlaying && controller.IsBattleAnimationPlaying && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(mana.Motion.IsPlaying, Is.True, "Disable during a committed, still-visible change.");
            int energy = session.Battle.Energy, balance = session.Battle.Schools.Mana;
            int played = session.Battle.CardsPlayedThisTurn;
            int[] hand = session.Battle.Hand.ToArray();
            controller.enabled = false;
            yield return null;
            Assert.That(mana == null || !mana.Motion.IsPlaying, Is.True);
            stale.Invoke();
            controller.enabled = true;
            yield return null; yield return null;
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Battle.Schools.Mana, Is.EqualTo(balance));
            Assert.That(session.Battle.CardsPlayedThisTurn, Is.EqualTo(played));
            CollectionAssert.AreEqual(hand, session.Battle.Hand);
            Assert.That(controller.IsBattleAnimationPlaying, Is.False);
            Assert.That(controller.GetComponentsInChildren<BattleHudChangeMotion>().All(item => !item.IsPlaying), Is.True,
                "Reactivation presents the committed baseline, without replaying the old gain or cost.");
            Assert.That(FieldStatusResourceValue("Mana"), Is.EqualTo(balance + "/" + session.Battle.Schools.ManaLimit));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NavalFieldStatusMotion_FieldRootReactivationUsesSilentBaselineThenAnimatesNewCoverage()
        {
            yield return LoadResponsiveBattleScene();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            CoreVoyageSession session = CreateFieldStatusMotionBattle(controller);
            RenderResponsiveBattle(controller);
            yield return null;
            NavalFieldMotion motion = RequiredResponsive<NavalFieldMotion>("Persistent Naval Fields");
            NavalFieldBoardOverlay overlay = RequiredResponsive<NavalFieldBoardOverlay>("Naval Tactical Board");
            CoreNavalFieldState fields = session.Battle.NavalFields;
            int energy = session.Battle.Energy, hull = session.Hull;
            long beforeHiddenPlacement = fields.PresentationSequence;
            motion.gameObject.SetActive(false);
            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Fire, true, 0, 1, false, 0, 0), Is.True);
            long hiddenSequence = fields.PresentationSequence;
            Assert.That(hiddenSequence, Is.GreaterThan(beforeHiddenPlacement));
            Assert.That(fields.Cells.Count, Is.EqualTo(4));
            // No yield while inactive: OnEnable must baseline facts created after OnDisable.
            motion.gameObject.SetActive(true);
            Assert.That(motion.LastConsumedSequence, Is.EqualTo(hiddenSequence));
            yield return null;
            Assert.That(motion.ActiveCues, Is.Empty, "Hidden placements must not replay on reactivation.");
            Assert.That(motion.PendingTriggerCount, Is.Zero);
            Assert.That(motion.TotalPlacedCues, Is.Zero);
            Assert.That(motion.TotalRetiredCues, Is.Zero);
            Assert.That(motion.TotalTriggeredCues, Is.Zero);
            Assert.That(motion.LastConsumedSequence, Is.EqualTo(hiddenSequence));
            Assert.That(motion.GetComponent<NavalFieldGraphic>().Cells.Count, Is.EqualTo(4));
            Assert.That(fields.Cells.All(cell => cell.Kind == CoreNavalFieldKind.Fire), Is.True);

            Assert.That(session.PlaceNavalField(CoreNavalFieldKind.Arcane, true, 0, 1, false, 0, 0), Is.True);
            long coverageSequence = fields.PresentationSequence;
            yield return null;
            Assert.That(motion.ActiveCues.Count(cue => cue.Event.Change == CoreNavalFieldChangeKind.Placed &&
                cue.Event.Kind == CoreNavalFieldKind.Arcane), Is.EqualTo(4));
            Assert.That(motion.ActiveCues.Count(cue => cue.Event.Change == CoreNavalFieldChangeKind.Overwritten &&
                cue.Event.Kind == CoreNavalFieldKind.Fire), Is.EqualTo(4));
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(4));
            Assert.That(motion.TotalRetiredCues, Is.EqualTo(4));
            Assert.That(motion.TotalTriggeredCues, Is.Zero, "Coverage alone is not a field trigger.");
            Assert.That(motion.LastConsumedSequence, Is.EqualTo(coverageSequence));
            overlay.RefreshNow();
            yield return null;
            Assert.That(motion.TotalPlacedCues, Is.EqualTo(4), "A refresh must not replay new coverage.");
            Assert.That(motion.TotalRetiredCues, Is.EqualTo(4));
            Assert.That(fields.PresentationSequence, Is.EqualTo(coverageSequence));
            Assert.That(fields.Cells.Count, Is.EqualTo(4));
            Assert.That(fields.Cells.All(cell => cell.Kind == CoreNavalFieldKind.Arcane), Is.True);
            Assert.That(session.Battle.Energy, Is.EqualTo(energy));
            Assert.That(session.Hull, Is.EqualTo(hull));
            LogAssert.NoUnexpectedReceived();
        }

        private static CoreVoyageSession CreateFieldStatusMotionBattle(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = CreateResponsiveBattle(controller, NavalBattleStage.Naval, pirateLevel: 10);
            SetNavalFieldTestPose(session, true, 0, 0);
            SetNavalFieldTestPose(session, false, 2, 0);
            session.Cargo.Powder = session.Cargo.Cannonballs = session.Cargo.Wood = 30;
            return session;
        }

        private static IEnumerator BeginFieldStatusPlacement(CoreNavalFieldKind kind, int x, int y)
        {
            Button card = RequiredResponsive<Button>("Battle Card 0");
            Assert.That(card.interactable, Is.True, "The next field card must be available through the real hand.");
            card.onClick.Invoke();
            yield return null;
            var choice = Object.FindFirstObjectByType<NavalCardChoiceOverlay>();
            Assert.That(choice, Is.Not.Null);
            if (kind == CoreNavalFieldKind.Tide) RequiredResponsive<Button>("Naval field direction 1").onClick.Invoke();
            choice.SelectCell(x, y);
            Assert.That(RequiredResponsive<Button>("Confirm Naval Card Choice").interactable, Is.True);
        }

        private static IEnumerator ObserveFieldStatusCue(CoreGameplayDemoController controller, NavalFieldMotion motion,
            Func<NavalFieldMotionCue, bool> predicate, string file, string caption, Action onObserved = null)
        {
            bool observed = false;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (motion.ActiveCues.Any(predicate))
                {
                    onObserved?.Invoke();
                    Assert.That(motion.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True,
                        "Field lifecycle effects must not intercept route or card targeting input.");
                    // Freeze a readable phase after a real action; this advances presentation only.
                    motion.AdvancePresentation(.12f);
                    NavalBattleVisualCapture.Capture(controller, file, caption);
                    observed = true;
                    break;
                }
                if (!controller.IsBattleAnimationPlaying) break;
                yield return null;
            }
            Assert.That(observed, Is.True, "The real model action must expose the expected field lifecycle cue.");
            yield return WaitResponsiveBattle(controller);
        }

        private static BattleResourceChangeTracker FieldStatusResource(string id) =>
            RequiredResponsive<BattleResourceChangeTracker>("Resource " + id);

        private static string FieldStatusResourceValue(string id) =>
            FieldStatusResource(id).transform.Find("Amount").GetComponent<Text>().text;

        private static BattleHudChangeMotion FieldStatusSlotMotion(BattleStatusRow row, string id)
        {
            Transform slot = row == null ? null : row.transform.Find("Status " + id);
            return slot == null ? null : slot.GetComponent<BattleHudChangeMotion>();
        }

        private static IEnumerator ObserveFieldStatusHudMotion(CoreGameplayDemoController controller,
            Func<BattleHudChangeMotion> find, int previousCount, BattleHudChangeKind kind, string file, string caption)
        {
            BattleHudChangeMotion observed = null;
            float deadline = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < deadline)
            {
                BattleHudChangeMotion motion = find();
                if (motion != null && motion.IsPlaying && motion.PlayCount > previousCount && motion.LastKind == kind)
                {
                    observed = motion;
                    Assert.That(motion.PlayCount, Is.EqualTo(previousCount + 1));
                    if (file != null)
                    {
                        motion.SetElapsed(.18f);
                        NavalBattleVisualCapture.Capture(controller, file, caption);
                    }
                    break;
                }
                if (!controller.IsBattleAnimationPlaying) break;
                yield return null;
            }
            Assert.That(observed, Is.Not.Null, "A real committed action must expose " + kind + " feedback before completion.");
            yield return WaitResponsiveBattle(controller);
            Assert.That(observed.PlayCount, Is.EqualTo(previousCount + 1), "Completion refresh must not replay an already observed delta.");
        }
    }
}
