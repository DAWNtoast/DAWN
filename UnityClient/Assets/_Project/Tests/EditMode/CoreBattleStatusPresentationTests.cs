using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleStatusPresentationTests
    {
        [TestCase(CoreNavalFieldKind.Fire, "火场伤害（不含燃烧）")]
        [TestCase(CoreNavalFieldKind.Curse, "禁域附加的咒印伤害（不含咒印本身）")]
        [TestCase(CoreNavalFieldKind.Tide, "水流推动")]
        [TestCase(CoreNavalFieldKind.Thunder, "雷栅伤害及停航")]
        public void ComponentFieldProtectionShowsItsScopeAndExpiresWithItsTurn(CoreNavalFieldKind kind, string scope)
        {
            var session = Battle(NavalBattleStage.Naval);
            CoreNavalFieldState fields = session.Battle.NavalFields;
            fields.Player.FieldImmunityMask = 1 << (int)kind;
            fields.Player.FieldImmunityExpires = fields.PlayerTurn + 1;
            string id = "field_protection_" + kind.ToString().ToLowerInvariant();
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == id);
            Assert.That(status.Value, Is.EqualTo("1"));
            Assert.That(status.Tooltip, Does.Contain(scope).And.Contain("下次回合开始"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == id), Is.False);
            session.NavalFieldsBeginTurn(true);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == id), Is.False);
        }

        [Test]
        public void NoBattle_HasNoStatusOrResourceSummary()
        {
            Assert.That(CoreBattleStatusPresentation.Build(null, true), Is.Empty);
            Assert.That(CoreBattleStatusPresentation.Build(new CoreVoyageSession(), false), Is.Empty);
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(null), Is.Empty);
        }

        [Test]
        public void NavalLoading_IncludesOverflow_AndDoesNotExposeResourceBalancesAsBuffs()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.PlayerLoadedShot = 7;
            SetSchool(session, "OverflowContribution", 3);
            SetSchool(session, "Mana", 2);
            SetSchool(session, "TemporaryMana", 1);
            SetSchool(session, "CurseMarks", 4);
            SetSchool(session, "TidePower", 5);
            SetSchool(session, "ThunderChain", 6);
            session.Battle.Energy = 20;
            session.Cargo.Powder = 30;
            var statuses = CoreBattleStatusPresentation.Build(session, true);
            Assert.That(statuses.Single(status => status.Id == "loaded_shot").Value, Is.EqualTo("10"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("mana"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("curse_marks"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("tide_power"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("thunder_chain"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("energy"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("powder"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Contain("法力 2/3 · 临时 +1"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("装填"));
        }

        [Test]
        public void BoardingBurn_ShowsCombinedStackCountAndDecay_WithoutMutatingLiveState()
        {
            var session = Battle(NavalBattleStage.Boarding);
            var b = session.Battle;
            b.Boarding.Initialized = true;
            b.EnemyBurnDamage = 99;
            b.EnemyBurnTurns = 9;
            b.Boarding.EnemyBurn.Add(new CoreBoardingTimedStacks(2, 1));
            b.Boarding.EnemyBurn.Add(new CoreBoardingTimedStacks(3, 1));
            b.Boarding.EnemyBurn.Add(new CoreBoardingTimedStacks(4, 2));
            b.Boarding.EnemyBurn.Add(new CoreBoardingTimedStacks(20, 0));
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn");
            Assert.That(status.Value, Is.EqualTo("9"));
            Assert.That(status.Tooltip, Does.Contain("敌方回合末，按当前层数"));
            Assert.That(status.Tooltip, Does.Contain("然后层数减少 1"));
            Assert.That(status.Tooltip, Does.Contain("新增层数相加"));
            Assert.That(status.Tooltip, Does.Not.Contain("各批"));
            Assert.That(status.Tooltip, Does.Not.Contain("剩余"));
            Assert.That(b.Boarding.EnemyBurn.Count, Is.EqualTo(4), "Reading the HUD must never consume or prune live effects.");
            Assert.That(b.EnemyBurnDamage, Is.EqualTo(99));
            b.Boarding.EnemyBurn.Clear();
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "burn"), Is.False);
        }

        [Test]
        public void NavalAndBoardingBurn_BothResolveAtEnemyTurnEnd()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.EnemyBurnDamage = 5;
            session.Battle.EnemyBurnTurns = 5;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Does.Contain("敌方回合末"));
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Boarding.Initialized = false;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Does.Contain("敌方回合末"));
        }

        [Test]
        public void DelayedDamageAndHealing_BelongToTheAffectedSide_AndDoNotMutateState()
        {
            var session = Battle(NavalBattleStage.Boarding);
            var b = session.Battle.Boarding;
            b.DelayedDamage.Add(new CoreBoardingDelayedDamage { Amount = 12, Remaining = 1, Label = "第一击" });
            b.DelayedDamage.Add(new CoreBoardingDelayedDamage { Amount = 8, Remaining = 2, Label = "穿盾击", IgnoreBlock = true });
            b.HealingTicks.Add(new CoreBoardingHealingTick { Amount = 7, Remaining = 7 });
            var player = CoreBattleStatusPresentation.Build(session, true);
            var enemy = CoreBattleStatusPresentation.Build(session, false);
            Assert.That(player.Single(item => item.Id == "healing").Value, Is.EqualTo("7"));
            Assert.That(player.Single(item => item.Id == "healing").Tooltip, Does.Contain("按当前层数恢复生命值"));
            Assert.That(player.Single(item => item.Id == "healing").Tooltip, Does.Contain("恢复后层数减少 1"));
            Assert.That(player.Single(item => item.Id == "healing").Tooltip, Does.Not.Contain("各批"));
            Assert.That(player.Any(item => item.Id == "delayed_damage"), Is.False);
            Assert.That(enemy.Single(item => item.Id == "delayed_damage").Value, Is.EqualTo("20"));
            Assert.That(enemy.Single(item => item.Id == "delayed_damage").Tooltip, Does.Contain("穿盾击：8 点无视格挡伤害，再过 2 次"));
            Assert.That(enemy.Any(item => item.Id == "healing"), Is.False);
            Assert.That(b.DelayedDamage[0].Remaining, Is.EqualTo(1));
            Assert.That(b.HealingTicks[0].Remaining, Is.EqualTo(7));
        }

        [Test]
        public void CurseStacksAndBonus_AreSeparateStatuses_AndBonusDoesNotChangeLifetimeDisplay()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "CurseMarks", 3);
            SetSchool(session, "CurseTurns", 3);
            SetSchool(session, "CurseNextTickBonus", 2);
            var statuses = CoreBattleStatusPresentation.Build(session, false);
            CoreBattleStatus curse = statuses.Single(item => item.Id == "curse_marks");
            CoreBattleStatus bonus = statuses.Single(item => item.Id == "curse_tick_bonus");
            Assert.That(curse.Value, Is.EqualTo("3"));
            Assert.That(curse.IsDebuff, Is.True);
            Assert.That(curse.Tooltip, Does.Contain("然后层数减少 1"));
            Assert.That(bonus.Value, Is.EqualTo("2"));
            Assert.That(bonus.Tooltip, Does.Contain("额外造成 2 点"));
            Assert.That(bonus.Tooltip, Does.Not.Contain("每层"));
            Assert.That(bonus.Tooltip, Does.Contain("不改变咒印层数与持续时间"));
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseTurns, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseNextTickBonus, Is.EqualTo(2));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Contain("对敌咒印 3"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("回合"));
        }

        [Test]
        public void PersistentAndNextCurseBonuses_ShowTheirOwnValuesAndLifetimes_WithoutMutatingStacks()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "CurseMarks", 3);
            SetSchool(session, "CurseTurns", 3);
            SetSchool(session, "CurseTickBonus", 2);
            SetSchool(session, "CurseNextTickBonus", 5);
            var statuses = CoreBattleStatusPresentation.Build(session, false);
            CoreBattleStatus curse = statuses.Single(item => item.Id == "curse_marks");
            CoreBattleStatus persistent = statuses.Single(item => item.Id == "curse_persistent_bonus");
            CoreBattleStatus next = statuses.Single(item => item.Id == "curse_tick_bonus");
            Assert.That(curse.Value, Is.EqualTo("3"));
            Assert.That(persistent.Value, Is.EqualTo("2"));
            Assert.That(next.Value, Is.EqualTo("5"));
            Assert.That(persistent.Tooltip, Does.Contain("每次结算咒印时，额外造成 2 点")
                .And.Contain("咒印消失时清除").And.Contain("不改变咒印层数与持续时间"));
            Assert.That(next.Tooltip, Does.Contain("下次结算咒印时，额外造成 5 点")
                .And.Contain("结算后清除").And.Contain("不改变咒印层数与持续时间"));
            Assert.That(persistent.Tooltip + next.Tooltip, Does.Not.Contain("每层"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Select(item => item.Tooltip),
                Is.EqualTo(statuses.Select(item => item.Tooltip)), "Repeated HUD reads are deterministic.");
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseTurns, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseTickBonus, Is.EqualTo(2));
            Assert.That(session.Battle.Schools.CurseNextTickBonus, Is.EqualTo(5));
        }

        [Test]
        public void BoardingCurse_IsShownOnItsTarget_WithNoIndependentDurationOrCap()
        {
            var session = Battle(NavalBattleStage.Boarding);
            var curseStacks = (System.Collections.Generic.List<int>)typeof(CoreBoardingEnemyState)
                .GetField("CurseDurations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(session.Battle.BoardingEnemy);
            curseStacks.AddRange(new[] { 1, 1, 1, 1 });
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "curse_marks");
            Assert.That(status.Value, Is.EqualTo("4"));
            Assert.That(status.Tooltip, Does.Contain("玩家回合末"));
            Assert.That(status.Tooltip, Does.Contain("新增层数相加"));
            Assert.That(status.Tooltip, Does.Not.Contain("剩余"));
            Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(4));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "curse_marks"), Is.False);
        }

        [Test]
        public void FirePots_ShowRepeatTriggerThroughBattleEnd_InsteadOfSentinelDuration()
        {
            var session = Battle(NavalBattleStage.Boarding);
            session.Battle.Boarding.FirePots.Add(new CoreBoardingTimedStacks(6, int.MaxValue));
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "fire_pots");
            Assert.That(status.Value, Is.EqualTo("6"));
            Assert.That(status.Tooltip, Does.Contain("每个己方回合开始时，给敌方施加 6 层灼烧"));
            Assert.That(status.Tooltip, Does.Contain("持续至战斗结束"));
            Assert.That(status.Tooltip, Does.Not.Contain(int.MaxValue.ToString()));
            Assert.That(status.Tooltip, Does.Not.Contain("剩余"));
        }

        [Test]
        public void ArcaneReserves_ExplainTheirNextTriggerWithoutDecay()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "ArcaneNextAttackDamage", 5);
            SetSchool(session, "ArcaneNextBlockBonus", 4);
            var statuses = CoreBattleStatusPresentation.Build(session, true);
            CoreBattleStatus attack = statuses.Single(item => item.Id == "arcane_attack");
            CoreBattleStatus guard = statuses.Single(item => item.Id == "arcane_guard");
            Assert.That(attack.Value, Is.EqualTo("5"));
            Assert.That(attack.Tooltip, Does.Contain("下次海战攻击"));
            Assert.That(guard.Value, Is.EqualTo("4"));
            Assert.That(guard.Tooltip, Does.Contain("下次获得格挡"));
            Assert.That(attack.Tooltip + guard.Tooltip, Does.Not.Contain("层数减少"));
        }

        [Test]
        public void NavalDebuffs_AppearOnEnemy_AndDoNotLeakIntoBoarding()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.NavalExpansion.RuneClamp = true;
            session.Battle.NavalExpansion.PulseReduction = 8;
            session.Battle.NavalExpansion.Precharge = true;
            session.Battle.NavalFields.Enemy.MovementSlow = 1;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Select(item => item.Id),
                Is.SupersetOf(new[] { "rune_clamp", "pulse_reduction", "movement_slow" }));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "rune_clamp"), Is.False);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "precharge").IsDebuff, Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "rune_clamp"), Is.False);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "precharge"), Is.False);
        }

        [Test]
        public void NavalStackedBonusesAndPersistentReduction_ShowLiveValuesAndCorrectExpiry()
        {
            var session = Battle(NavalBattleStage.Naval);
            CoreNavalExpansionBattleState expansion = session.Battle.NavalExpansion;
            expansion.Precharge = true; expansion.PrechargeBonus = 6;
            expansion.RuneClamp = true; expansion.RuneClampReduction = 14;
            expansion.PulseReduction = 12;
            session.Battle.NavalFields.Player.FireImmunityCharges = 4;
            session.Battle.NavalFields.Enemy.MovementSlow = 2;
            var player = CoreBattleStatusPresentation.Build(session, true);
            var enemy = CoreBattleStatusPresentation.Build(session, false);
            CoreBattleStatus precharge = player.Single(item => item.Id == "precharge");
            CoreBattleStatus rune = enemy.Single(item => item.Id == "rune_clamp");
            CoreBattleStatus pulse = enemy.Single(item => item.Id == "pulse_reduction");
            Assert.That(precharge.Value, Is.EqualTo("6"));
            Assert.That(precharge.Tooltip, Does.Contain("额外获得 6 层连锁").And.Contain("玩家回合末清除"));
            Assert.That(rune.Value, Is.EqualTo("14"));
            Assert.That(rune.Tooltip, Does.Contain("格挡减少 14 点").And.Contain("触发前保留").And.Not.Contain("回合末"));
            Assert.That(pulse.Value, Is.EqualTo("12"));
            Assert.That(pulse.Tooltip, Does.Contain("直到下个敌方回合结束，每次攻击").And.Contain("减少 12 点")
                .And.Not.Contain("50%").And.Not.Contain("触发后"));
            Assert.That(player.Single(item => item.Id == "fire_immunity").Tooltip, Does.Contain("接下来的 4 次"));
            Assert.That(enemy.Single(item => item.Id == "movement_slow").Tooltip,
                Does.Contain("减少 2 格").And.Contain("触发前保留").And.Not.Contain("到期"));
            Assert.That(expansion.PrechargeBonus, Is.EqualTo(6));
            Assert.That(expansion.RuneClampReduction, Is.EqualTo(14));
            Assert.That(expansion.PulseReduction, Is.EqualTo(12));
        }

        [Test]
        public void VoyageCurse_ReportsCappedDamagePenaltyOnPlayerInBothStages()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Cargo.Curse = 20;
            CoreBattleStatus curse = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "voyage_curse");
            Assert.That(curse.Value, Is.EqualTo("100%"));
            Assert.That(curse.IsDebuff, Is.True);
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "voyage_curse"), Is.False);
            session.Battle.Stage = NavalBattleStage.Boarding;
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "voyage_curse").Value, Is.EqualTo("100%"));
            session.Cargo.Curse = 0;
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "voyage_curse"), Is.False);
        }

        [Test]
        public void RealCurseCards_ProduceLiveWeaknessAndCounterTooltips()
        {
            var session = Battle(NavalBattleStage.Boarding);
            session.Battle.Hand.Clear();
            foreach (string key in new[] { "curse_02", "curse_07", "curse_09" })
                session.Battle.Hand.Add(Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                    .Single(index => CoreVoyageSession.CardLibrary[index].Id == "card_boarding_" + key));
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            Assert.That(session.PlayCard(0), Is.True);
            CoreBattleStatus weakness = CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "curse_weakness");
            CoreBattleStatus counter = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "curse_counter");
            Assert.That(weakness.Value, Is.EqualTo("6"));
            Assert.That(weakness.Tooltip, Does.Contain("持续期间每次攻击均生效"));
            Assert.That(weakness.Tooltip, Does.Not.Contain("50%"));
            Assert.That(counter.Tooltip, Does.Contain("当前伤害 6"));
            Assert.That(counter.Tooltip, Does.Contain("敌方主动攻击造成伤害前"));
            Assert.That(counter.Tooltip, Does.Contain("剩余 2 次敌方回合末"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Contain("对敌咒印 3"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("反击"));
        }

        private static CoreVoyageSession Battle(NavalBattleStage stage)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            session.Battle.Stage = stage;
            session.Battle.PlayerBoardingHealth = session.Battle.PlayerMaxBoardingHealth = 1000;
            session.Battle.EnemyBoardingHealth = session.Battle.EnemyMaxBoardingHealth = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.Energy = 100;
            return session;
        }

        private static void SetSchool(CoreVoyageSession session, string property, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(property).SetValue(session.Battle.Schools, value);
    }
}
