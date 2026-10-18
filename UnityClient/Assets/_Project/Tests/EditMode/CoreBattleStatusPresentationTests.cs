using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleStatusPresentationTests
    {
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
        public void BoardingBurn_UsesIndependentBatches_NotLegacyMirrorOrExpiredLayers()
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
            Assert.That(status.Tooltip, Does.Contain("敌方回合末直接失去 5 点生命（无视格挡）；剩余 1 次"));
            Assert.That(status.Tooltip, Does.Contain("敌方回合末直接失去 4 点生命（无视格挡）；剩余 2 次"));
            Assert.That(b.Boarding.EnemyBurn.Count, Is.EqualTo(4), "Reading the HUD must never consume or prune live effects.");
            Assert.That(b.EnemyBurnDamage, Is.EqualTo(99));
            b.Boarding.EnemyBurn.Clear();
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "burn"), Is.False);
        }

        [Test]
        public void NavalAndBoardingBurn_ExplainTheirDifferentResolutionTimes()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.EnemyBurnDamage = 5;
            session.Battle.EnemyBurnTurns = 2;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Does.Contain("敌方行动前"));
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Boarding.Initialized = false;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Does.Contain("敌方回合末"));
        }

        [Test]
        public void DelayedDamageAndHealing_BelongToTheAffectedSide_AndPreserveDurations()
        {
            var session = Battle(NavalBattleStage.Boarding);
            var b = session.Battle.Boarding;
            b.DelayedDamage.Add(new CoreBoardingDelayedDamage { Amount = 12, Remaining = 1, Label = "第一击" });
            b.DelayedDamage.Add(new CoreBoardingDelayedDamage { Amount = 8, Remaining = 2, Label = "穿盾击", IgnoreBlock = true });
            b.HealingTicks.Add(new CoreBoardingHealingTick { Amount = 5, Remaining = 2 });
            b.HealingTicks.Add(new CoreBoardingHealingTick { Amount = 2, Remaining = 1 });
            var player = CoreBattleStatusPresentation.Build(session, true);
            var enemy = CoreBattleStatusPresentation.Build(session, false);
            Assert.That(player.Single(item => item.Id == "healing").Value, Is.EqualTo("7"));
            Assert.That(player.Any(item => item.Id == "delayed_damage"), Is.False);
            Assert.That(enemy.Single(item => item.Id == "delayed_damage").Value, Is.EqualTo("20"));
            Assert.That(enemy.Single(item => item.Id == "delayed_damage").Tooltip, Does.Contain("穿盾击：8 点无视格挡伤害，再过 2 次"));
            Assert.That(enemy.Any(item => item.Id == "healing"), Is.False);
            Assert.That(b.DelayedDamage[0].Remaining, Is.EqualTo(1));
            Assert.That(b.HealingTicks[0].Remaining, Is.EqualTo(2));
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
            Assert.That(weakness.Tooltip, Does.Contain("取最大值，不相加"));
            Assert.That(counter.Tooltip, Does.Contain("当前伤害 6"));
            Assert.That(counter.Tooltip, Does.Contain("剩余 2 次敌方回合末"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Contain("对敌咒痕 3/6"));
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
