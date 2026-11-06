using System;
using System.Linq;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreBattleStatusPresentationTests
    {
        [TestCase(CoreNavalFieldKind.Fire, "火场")]
        [TestCase(CoreNavalFieldKind.Curse, "禁域")]
        [TestCase(CoreNavalFieldKind.Tide, "定向水流")]
        [TestCase(CoreNavalFieldKind.Thunder, "雷栅")]
        [TestCase(CoreNavalFieldKind.Blood, "血池")]
        public void ComponentFieldProtectionShowsItsScopeAndExpiresWithItsTurn(CoreNavalFieldKind kind, string scope)
        {
            var session = Battle(NavalBattleStage.Naval);
            CoreNavalFieldState fields = session.Battle.NavalFields;
            fields.Player.FieldImmunityMask = 1 << (int)kind;
            fields.Player.FieldImmunityExpires = fields.PlayerTurn + 1;
            if (kind == CoreNavalFieldKind.Fire) session.GrantNavalFireImmunity(true);
            string id = kind == CoreNavalFieldKind.Fire ? "burn_prevention" : "field_protection_" + kind.ToString().ToLowerInvariant();
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == id);
            Assert.That(status.Value, Is.EqualTo("1"));
            Assert.That(status.Tooltip, Is.EqualTo(status.Name + "\n直到下个我方回合开始时，不受到" + scope + "场地的负面效果"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == id), Is.False);
            session.NavalFieldsEndTurn(true);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == id), Is.True);
            session.NavalFieldsBeginTurn(false);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == id), Is.True);
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

        [TestCase(NavalBattleStage.Preparation, true)] [TestCase(NavalBattleStage.Preparation, false)]
        [TestCase(NavalBattleStage.Naval, true)] [TestCase(NavalBattleStage.Naval, false)]
        [TestCase(NavalBattleStage.Boarding, true)] [TestCase(NavalBattleStage.Boarding, false)]
        public void ChainAndTideCountersAppearOnlyOnTheirHolderAcrossStages(NavalBattleStage stage, bool player)
        {
            var session = Battle(stage);
            if (player)
            {
                if (stage == NavalBattleStage.Boarding)
                    typeof(CoreVoyageSession).GetMethod("CTBGainTide", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(session, new object[] { 23 });
                else SetSchool(session, "TidePower", 23);
                SetSchool(session, "ThunderChain", 17);
            }
            else if (stage == NavalBattleStage.Boarding)
            {
                typeof(CoreBoardingEnemyState).GetProperty("TidePower").SetValue(session.Battle.BoardingEnemy, 23);
                typeof(CoreBoardingEnemyState).GetProperty("ThunderChain").SetValue(session.Battle.BoardingEnemy, 17);
            }
            else
            {
                session.Battle.EnemyTidePower = 23;
                session.Battle.NavalFields.Enemy.ThunderChain = 17;
            }
            var statuses = CoreBattleStatusPresentation.Build(session, player);
            CoreBattleStatus chain = statuses.Single(item => item.Id == "thunder_chain");
            CoreBattleStatus tide = statuses.Single(item => item.Id == "tide_power");
            Assert.That(chain.Value, Is.EqualTo("17"));
            Assert.That(tide.Value, Is.EqualTo("23"));
            Assert.That(chain.Kind, Is.EqualTo(CoreBattleStatusKind.Thunder));
            Assert.That(tide.Kind, Is.EqualTo(CoreBattleStatusKind.Tide));
            Assert.That(chain.IsDebuff || tide.IsDebuff, Is.False);
            Assert.That(CoreBattleStatusPresentation.Build(session, !player).Any(item => item.Id == "thunder_chain" || item.Id == "tide_power"), Is.False);
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("连锁").And.Not.Contain("潮势"));
            Assert.That(CoreBattleStatusPresentation.Build(session, player).Select(item => item.Tooltip),
                Is.EqualTo(statuses.Select(item => item.Tooltip)), "HUD reads do not spend either counter.");
        }

        [TestCase(NavalBattleStage.Preparation, true)] [TestCase(NavalBattleStage.Preparation, false)]
        [TestCase(NavalBattleStage.Naval, true)] [TestCase(NavalBattleStage.Naval, false)]
        [TestCase(NavalBattleStage.Boarding, true)] [TestCase(NavalBattleStage.Boarding, false)]
        public void CurseMarkIconBelongsToItsHolderAndUsesTheReviewedTooltip(NavalBattleStage stage, bool player)
        {
            var session = Battle(stage);
            if (stage == NavalBattleStage.Boarding)
            {
                if (player)
                {
                    var marks = (System.Collections.Generic.List<int>)typeof(CoreBoardingEnemyState)
                        .GetField("CurseDurations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .GetValue(session.Battle.BoardingEnemy);
                    marks.AddRange(Enumerable.Repeat(1, 9));
                }
                else typeof(CoreVoyageSession).GetMethod("CTBAddMarks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(session, new object[] { 9 });
            }
            else if (player) session.Battle.PlayerCurseMarks = 9;
            else SetSchool(session, "CurseMarks", 9);
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == "curse_marks");
            Assert.That(status.Value, Is.EqualTo("9"));
            Assert.That(status.Kind, Is.EqualTo(CoreBattleStatusKind.Curse));
            Assert.That(status.IsDebuff, Is.True);
            Assert.That(status.Tooltip, Is.EqualTo("咒印\n死神，来找你了！"));
            Assert.That(CoreBattleStatusPresentation.Build(session, !player).Any(item => item.Id == "curse_marks"), Is.False);
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("咒印"));
        }

        [TestCase(NavalBattleStage.Preparation)] [TestCase(NavalBattleStage.Naval)] [TestCase(NavalBattleStage.Boarding)]
        public void ZeroCountersAndRemovedCrewAndCurseGuardStatesStayHidden(NavalBattleStage stage)
        {
            var session = Battle(stage);
            session.ConfigureDepartureCrew(0, 0);
            SetSchool(session, "CurseGuard", 12);
            foreach (bool player in new[] { true, false })
            {
                var ids = CoreBattleStatusPresentation.Build(session, player).Select(item => item.Id).ToArray();
                foreach (string id in new[] { "thunder_chain", "tide_power", "curse_marks", "guard", "crew_shortage", "curse_guard" })
                    Assert.That(ids, Has.None.EqualTo(id));
            }
            session.Battle.PlayerBlock = 12;
            CoreBattleStatus guard = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "guard");
            Assert.That(guard.Value, Is.EqualTo("12"));
            Assert.That(guard.Tooltip, Is.EqualTo("格挡\n下个己方回合开始时清空。"));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "curse_guard"), Is.False);
            Assert.That(session.EffectiveFirepower, Is.EqualTo(session.Firepower));
            Assert.That(session.EffectiveArmor, Is.EqualTo(session.Armor));
        }

        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void BloodHealingBanShowsWholePreventionForBothActors(NavalBattleStage stage)
        {
            var session = Battle(stage);
            session.ApplyBattleHealingBan(true, 2);
            session.ApplyBattleHealingBan(false, 1);
            session.Battle.NavalFields.Player.BloodAttackBonus = 4;
            foreach (bool player in new[] { true, false })
            {
                var statuses = CoreBattleStatusPresentation.Build(session, player);
                var ban = statuses.Single(x => x.Id == "healing_ban");
                Assert.That(ban.Value, Is.EqualTo(player ? "2" : "1"));
                Assert.That(ban.IsDebuff, Is.True);
                Assert.That(ban.Tooltip, Is.EqualTo("禁疗\n无法恢复船体或生命值。"));
                Assert.That(statuses.Any(x => x.Id == "blood_attack"), Is.False);
            }
        }

        [Test]
        public void ThunderResourcesAndStatusesUseCurrentRulesWithoutTheOldCap()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "ThunderChain", 72);
            session.Battle.NavalFields.Enemy.ThunderChain = 55;
            session.Battle.NavalExpansion.Precharge = true;
            session.Battle.NavalExpansion.GroundingReady = true;
            string summary = CoreBattleStatusPresentation.BuildResourceSummary(session);
            Assert.That(summary, Does.Not.Contain("连锁").And.Not.Contain("潮势").And.Not.Contain("咒印"));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Single(x => x.Id == "thunder_chain").Value, Is.EqualTo("72"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(x => x.Id == "thunder_chain").Value, Is.EqualTo("55"));
            var statuses = CoreBattleStatusPresentation.Build(session, true);
            Assert.That(statuses.Single(x => x.Id == "precharge").Name, Is.EqualTo("预充线圈"));
            Assert.That(statuses.Single(x => x.Id == "grounding").Tooltip,
                Does.Contain("首张").And.Not.Contain("雷栅"));

            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Boarding.ThunderWeakness = 10;
            session.Battle.Boarding.ThunderWeaknessTurns = 1;
            session.Battle.Boarding.ThunderResidualCharges = 3;
            session.Battle.Boarding.ThunderResidualDamage = 4;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(x => x.Id == "thunder_weakness").Tooltip,
                Does.Contain("下一次敌方攻击").And.Contain("10").And.Not.Contain("回合"));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Single(x => x.Id == "thunder_residual").Tooltip,
                Does.Contain("立即追加 4").And.Contain("延迟攻击牌也在出牌时追加"));
        }

        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void TideGuardDescriptionCoversAllAbsorbedDamageAndPersistentResource(NavalBattleStage stage)
        {
            var session = Battle(stage);
            session.Battle.PlayerBlock = 9;
            if (stage == NavalBattleStage.Boarding) session.Battle.Boarding.TideGuard = 7;
            else SetSchool(session, "TidePaidBlock", 7);
            CoreBattleStatus guard = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "tide_guard");
            Assert.That(guard.Name, Is.EqualTo("蓄潮格挡"));
            Assert.That(guard.Value, Is.EqualTo("7"));
            Assert.That(guard.Tooltip, Does.Contain("每实际吸收 1 点伤害，获得 1 点潮势")
                .And.Contain("保留至战斗结束").And.Not.Contain("地形伤害不会").And.Not.Contain("敌方主动攻击"));
        }

        [Test]
        public void RetainTideHasAnExplicitBattleLongStatusEvenWithoutCurrentGuard()
        {
            var session = Battle(NavalBattleStage.Boarding);
            typeof(CoreBoardingBattleState).GetProperty("TideGuardConversionActive").SetValue(session.Battle.Boarding, true);
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "tide_guard_conversion");
            Assert.That(status.Name, Is.EqualTo("留潮"));
            Assert.That(status.Value, Is.EqualTo("本场"));
            Assert.That(status.Tooltip, Does.Contain("当前全部格挡及后续获得的格挡"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "tide_guard_conversion"), Is.False);
        }

        [Test]
        public void TideAnchorAndRecedingWaveShowTheirActualExpiryAndMovementCap()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.GrantNavalTideImmunity(true);
            session.ApplyNavalMovementCap(false, 1);
            CoreBattleStatus protection = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "field_protection_tide");
            Assert.That(protection.Tooltip, Is.EqualTo("定向水流防护\n直到下个我方回合开始时，不受到定向水流场地的负面效果"));
            CoreBattleStatus movement = CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "movement_cap");
            Assert.That(movement.Value, Is.EqualTo("1"));
            Assert.That(movement.Tooltip, Does.Contain("至多移动 1 格").And.Not.Contain("距离减少"));
            session.NavalFieldsEndTurn(true);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "field_protection_tide"), Is.True);
            session.NavalFieldsBeginTurn(true);
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "field_protection_tide"), Is.False);
        }

        [Test]
        public void NavalLoadingIncludesOverflowWhileSchoolCountersAppearOnlyOnTheirHolders()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.PlayerLoadedShot = 7;
            SetSchool(session, "OverflowContribution", 3);
            SetSchool(session, "Mana", 5);
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
            Assert.That(statuses.Single(status => status.Id == "tide_power").Value, Is.EqualTo("5"));
            Assert.That(statuses.Single(status => status.Id == "thunder_chain").Value, Is.EqualTo("6"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(status => status.Id == "curse_marks").Value, Is.EqualTo("4"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("energy"));
            Assert.That(statuses.Select(status => status.Id), Has.None.EqualTo("powder"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Contain("法力 5/3").And.Not.Contain("临时"));
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
            Assert.That(status.Tooltip, Is.EqualTo("燃烧\n回合结束时，受到9点伤害。"));
            Assert.That(b.Boarding.EnemyBurn.Count, Is.EqualTo(4), "Reading the HUD must never consume or prune live effects.");
            Assert.That(b.EnemyBurnDamage, Is.EqualTo(99));
            b.Boarding.EnemyBurn.Clear();
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "burn"), Is.False);
        }

        [Test]
        public void NavalAndBoardingBurnUseTheReviewedCurrentDamageTooltip()
        {
            var session = Battle(NavalBattleStage.Naval);
            session.Battle.EnemyBurnDamage = 5;
            session.Battle.EnemyBurnTurns = 5;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Is.EqualTo("燃烧\n回合结束时，受到5点伤害。"));
            session.Battle.Stage = NavalBattleStage.Boarding;
            session.Battle.Boarding.Initialized = false;
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "burn").Tooltip,
                Is.EqualTo("燃烧\n回合结束时，受到5点伤害。"));
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
        public void CurseStacksShowReviewedTextAndIgnoreRetiredDamageBonus()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "CurseMarks", 3);
            SetSchool(session, "CurseTurns", 3);
            SetSchool(session, "CurseNextTickBonus", 2);
            var statuses = CoreBattleStatusPresentation.Build(session, false);
            CoreBattleStatus curse = statuses.Single(item => item.Id == "curse_marks");
            Assert.That(curse.Value, Is.EqualTo("3"));
            Assert.That(curse.IsDebuff, Is.True);
            Assert.That(curse.Tooltip, Is.EqualTo("咒印\n死神，来找你了！"));
            Assert.That(statuses.Any(item => item.Id == "curse_tick_bonus"), Is.False);
            Assert.That(session.Battle.Schools.CurseMarks, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseTurns, Is.EqualTo(3));
            Assert.That(session.Battle.Schools.CurseNextTickBonus, Is.EqualTo(2));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("咒印"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("回合"));
        }

        [TestCase(NavalBattleStage.Naval)]
        [TestCase(NavalBattleStage.Boarding)]
        public void CurseThresholdHudStartsHiddenAt100AndShowsTheFirstFivePointReduction(NavalBattleStage stage)
        {
            var session = Battle(stage);
            Assert.That(session.Battle.Schools.EnemyCurseExecutionThresholdPercent, Is.EqualTo(100));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "curse_threshold"), Is.False);
            SetSchool(session, "EnemyCurseExecutionThresholdPercent", 95);
            CoreBattleStatus entangle = CoreBattleStatusPresentation.Build(session, false)
                .Single(item => item.Id == "curse_threshold");
            Assert.That(entangle.Tooltip, Is.EqualTo("缠咒\n咒印判定百分比降低至 95% 。"));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "curse_threshold"), Is.False);
        }

        [Test]
        public void CurseHud_UsesCurrentThresholdAndDoesNotDisplayRetiredBonuses()
        {
            var session = Battle(NavalBattleStage.Naval);
            SetSchool(session, "CurseMarks", 3);
            SetSchool(session, "CurseTurns", 3);
            SetSchool(session, "CurseTickBonus", 2);
            SetSchool(session, "CurseNextTickBonus", 5);
            SetSchool(session, "EnemyCurseExecutionThresholdPercent", 30);
            var statuses = CoreBattleStatusPresentation.Build(session, false);
            CoreBattleStatus curse = statuses.Single(item => item.Id == "curse_marks");
            Assert.That(curse.Value, Is.EqualTo("3"));
            Assert.That(curse.Tooltip, Is.EqualTo("咒印\n死神，来找你了！"));
            Assert.That(statuses.Single(item => item.Id == "curse_threshold").Tooltip,
                Is.EqualTo("缠咒\n咒印判定百分比降低至 30% 。"));
            Assert.That(CoreBattleStatusPresentation.Build(session, true).Any(item => item.Id == "curse_threshold"), Is.False);
            Assert.That(statuses.Any(item => item.Id == "curse_persistent_bonus" || item.Id == "curse_tick_bonus"), Is.False);
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
            Assert.That(status.Tooltip, Is.EqualTo("咒印\n死神，来找你了！"));
            Assert.That(status.Tooltip, Does.Not.Contain("回合末"));
            Assert.That(session.Battle.BoardingEnemy.CurseMarks, Is.EqualTo(4));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Any(item => item.Id == "curse_marks"), Is.False);
        }

        [Test]
        public void FirePots_ShowOneTriggerAtTheNextPlayerTurn()
        {
            var session = Battle(NavalBattleStage.Boarding);
            session.Battle.Boarding.FirePots.Add(new CoreBoardingTimedStacks(6, 1));
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, true).Single(item => item.Id == "fire_pots");
            Assert.That(status.Value, Is.EqualTo("6"));
            Assert.That(status.Tooltip, Does.Contain("下个己方回合开始时，给敌方施加 6 层燃烧"));
            Assert.That(status.Tooltip, Does.Contain("随后清除本效果"));
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
            session.GrantNavalFireImmunity(true);
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
            Assert.That(player.Single(item => item.Id == "burn_prevention").Tooltip,
                Is.EqualTo("隔焰防护\n直到下个我方回合开始时，不受到火场场地的负面效果"));
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
            Assert.That(curse.Tooltip, Is.EqualTo("航行诅咒\n被诅咒的身躯，额外受到100%的伤害！"));
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
            Assert.That(weakness.Value, Is.EqualTo("12"));
            Assert.That(weakness.Tooltip, Does.Contain("持续期间每次攻击均生效"));
            Assert.That(weakness.Tooltip, Does.Not.Contain("50%"));
            Assert.That(counter.Tooltip, Does.Contain("当前伤害 12"));
            Assert.That(counter.Tooltip, Does.Contain("敌方主动攻击造成伤害前"));
            Assert.That(counter.Tooltip, Does.Contain("剩余 2 次敌方回合末"));
            Assert.That(CoreBattleStatusPresentation.Build(session, false).Single(item => item.Id == "curse_marks").Value, Is.EqualTo("6"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("咒印"));
            Assert.That(CoreBattleStatusPresentation.BuildResourceSummary(session), Does.Not.Contain("反击"));
        }

        [TestCase("fire_13", 0, "fire_retaliation", true, "每次伤害为当前燃烧层数；", 1)]
        [TestCase("fire_13", 2, "fire_retaliation", true, "每次伤害为当前燃烧层数；", 1)]
        [TestCase("curse_04", 0, "curse_contract", true, "施加 4 层咒印", 1)]
        [TestCase("curse_04", 0, "curse_contract", true, "施加 8 层咒印", 2)]
        [TestCase("curse_05", 0, "curse_link", true, "各给敌方施加 2 层咒印", 1)]
        [TestCase("curse_07", 0, "curse_weakness", false, "伤害减少 12", 1)]
        [TestCase("curse_07", 2, "curse_weakness", false, "伤害减少 20", 1)]
        [TestCase("curse_07", 0, "curse_weakness", false, "伤害减少 24", 2)]
        [TestCase("curse_08", 0, "curse_armor", false, "格挡减少 10", 1)]
        [TestCase("curse_08", 2, "curse_armor", false, "格挡减少 15", 1)]
        [TestCase("curse_08", 0, "curse_armor", false, "格挡减少 20", 2)]
        [TestCase("curse_09", 0, "curse_counter", true, "当前伤害 12", 1)]
        [TestCase("curse_09", 2, "curse_counter", true, "当前伤害 24", 1)]
        [TestCase("curse_09", 0, "curse_counter", true, "当前伤害 24", 2)]
        [TestCase("tide_09", 0, "tide_ambush", true, "反击 32 点伤害", 1)]
        [TestCase("tide_09", 2, "tide_ambush", true, "反击 56 点伤害", 1)]
        [TestCase("tide_09", 0, "tide_ambush", true, "反击 64 点伤害", 2)]
        [TestCase("tide_10", 0, "tide_weakness", false, "伤害减少 6", 1)]
        [TestCase("tide_10", 2, "tide_weakness", false, "伤害减少 10", 1)]
        [TestCase("tide_10", 0, "tide_weakness", false, "伤害减少 12", 2)]
        [TestCase("blood_08", 0, "blood_debt", true, "反击 16 点伤害", 1)]
        [TestCase("blood_08", 2, "blood_debt", true, "反击 28 点伤害", 1)]
        [TestCase("blood_15", 0, "blood_oath", true, "额外造成 16 点伤害", 1)]
        [TestCase("blood_15", 2, "blood_oath", true, "额外造成 28 点伤害", 1)]
        public void BoardingCardStatusesShowAuthoredRanksAndAllActiveCopies(
            string key, int rank, string statusId, bool player, string expected, int copies)
        {
            string id = "card_boarding_" + key;
            var session = Battle(NavalBattleStage.Boarding, id, rank);
            session.Battle.PlayerBoardingHealth = 200;
            session.Cargo.Powder = 100;
            typeof(CoreSchoolBattleState).GetProperty("TidePower").SetValue(session.Battle.Schools, 100);
            typeof(CoreVoyageSession).GetMethod("BInitialize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(session, null);
            typeof(CoreVoyageSession).GetMethod("CTBAddMarks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(session, new object[] { 6 });
            int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
            for (int copy = 0; copy < copies; copy++)
            {
                session.Battle.Hand.Clear(); session.Battle.Hand.Add(index);
                Assert.That(session.PlayCard(0), Is.True, session.CardUnavailableReason(0));
            }
            CoreBattleStatus status = CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == statusId);
            Assert.That(status.Tooltip, Does.Contain(expected));
            if (key == "fire_13") Assert.That(status.Tooltip, Does.Not.Contain("×2"));
            Assert.That(CoreBattleStatusPresentation.Build(session, player).Single(item => item.Id == statusId).Tooltip,
                Is.EqualTo(status.Tooltip), "Reading a tooltip never consumes a live effect.");
        }

        private static CoreVoyageSession Battle(NavalBattleStage stage, string upgradedCard = null, int rank = 0)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            if (upgradedCard != null) session.ConfigureCardUpgrades(new System.Collections.Generic.Dictionary<string, int> { { upgradedCard, rank } });
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
