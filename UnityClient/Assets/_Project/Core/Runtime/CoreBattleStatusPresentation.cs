using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreBattleStatusKind
    {
        Guard, Armor, Attack, Fire, Retaliation, Healing, Curse, Weakness, Tide,
        Arcane, Thunder, Blood, Delayed, Anchor, Slow, Immunity, Seal, Draw
    }

    /// <summary>A read-only combat effect. Resource balances belong to the resource HUD.</summary>
    public sealed class CoreBattleStatus
    {
        public CoreBattleStatus(string id, string name, string value, string tooltip, CoreBattleStatusKind kind, bool isDebuff = false)
        { Id = id; Name = name; Value = value; Tooltip = name + "\n" + tooltip; Kind = kind; IsDebuff = isDebuff; }
        public string Id { get; }
        public string Name { get; }
        public string Value { get; }
        public string Tooltip { get; }
        public CoreBattleStatusKind Kind { get; }
        public bool IsDebuff { get; }
    }

    /// <summary>
    /// Projects live rules without consuming effects or running previews. In particular, mana,
    /// blood loss, energy and cargo remain resources. Chains, tide power and curse marks
    /// are shown on their holder's status row.
    /// Decaying effects display one stack count for both their next base value and lifetime.
    /// Legacy mirrors are never counted a second time.
    /// </summary>
    public static class CoreBattleStatusPresentation
    {
        public static string CurseExecutionRule(int thresholdPercent = CoreCurseRules.DefaultExecutionThresholdPercent) =>
            thresholdPercent <= 0 ? "咒印消灭比例已降至 " + N(thresholdPercent) + "%，立即消灭该角色。" :
            "咒印层数大于或等于持有者剩余生命值的 " + N(thresholdPercent) + "% 时立即消灭该角色；咒印不会随回合减少。";

        public static string BuildResourceSummary(CoreVoyageSession session)
        {
            if (session?.Battle == null) return string.Empty;
            CoreBattleState battle = session.Battle;
            CoreSchoolBattleState s = battle.Schools;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            var text = new StringBuilder("己方体系资源");
            text.Append("\n法力 ").Append(s.Mana).Append('/').Append(s.ManaLimit);
            CoreBoardingEnemyState enemy = battle.BoardingEnemy;
            if (boarding)
            {
                switch (enemy.Deck?.SchoolId)
                {
                    case "arcane": text.Append("\n\n敌方法力 ").Append(enemy.Mana).Append('/').Append(CoreBoardingEnemyState.ManaLimit); break;
                    case "blood": text.Append("\n\n敌方失血 ").Append(enemy.BloodLoss).Append('/').Append(CoreBoardingEnemyState.BloodLossLimit); break;
                }
                if (enemy.Deck?.SchoolId != "arcane" && battle.BoardingFields.Contains(CoreNavalFieldKind.Arcane))
                    text.Append("\n敌方法力 ").Append(enemy.Mana).Append('/').Append(CoreBoardingEnemyState.ManaLimit);
            }
            else if (battle.Stage == NavalBattleStage.Naval)
            {
                text.Append("\n\n敌方法力 ").Append(battle.NavalFields.Enemy.Mana).Append('/').Append(battle.NavalFields.Enemy.ManaLimit);
            }
            return text.ToString();
        }

        public static IReadOnlyList<CoreBattleStatus> Build(CoreVoyageSession session, bool player)
        {
            if (session?.Battle == null) return Array.Empty<CoreBattleStatus>();
            var result = new List<CoreBattleStatus>();
            CoreBattleState battle = session.Battle;
            int guard = player ? battle.PlayerBlock : battle.EnemyBlock;
            Add(result, "guard", "格挡", guard, CoreBattleStatusKind.Guard,
                "下个己方回合开始时清空。");
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            int chain = player ? battle.Schools.ThunderChain :
                boarding ? battle.BoardingEnemy.ThunderChain : battle.NavalFields.Enemy.ThunderChain;
            int tide = player ? (boarding ? battle.Boarding.TidePower : battle.Schools.TidePower) :
                boarding ? battle.BoardingEnemy.TidePower : battle.EnemyTidePower;
            Add(result, "thunder_chain", "连锁", chain, CoreBattleStatusKind.Thunder,
                "当前 " + N(chain) + "\n使用雷霆攻击牌后增加 1；使用非雷霆牌后或己方回合开始时清零。");
            Add(result, "tide_power", "潮势", tide, CoreBattleStatusKind.Tide,
                "当前 " + N(tide) + "\n蓄潮格挡实际吸收的伤害转为等量潮势。");
            if (player)
                Flag(result, "voyage_curse", "航行诅咒", session.CurseDamageBonusPercent > 0, CoreBattleStatusKind.Curse,
                    "被诅咒的身躯，额外受到" + N(session.CurseDamageBonusPercent) + "%的伤害！",
                    true, N(session.CurseDamageBonusPercent) + "%");
            if (battle.Stage == NavalBattleStage.Boarding) AddBoarding(result, battle, player);
            else AddNaval(result, session, player);
            int healingBan = player ? battle.NavalExpansion.PlayerHealingBanTurns : battle.NavalExpansion.EnemyHealingBanTurns;
            Add(result, "healing_ban", "禁疗", healingBan, CoreBattleStatusKind.Healing,
                "无法恢复船体或生命值。", true);
            if (!player && battle.Schools.EnemyCurseExecutionThresholdPercent != CoreCurseRules.DefaultExecutionThresholdPercent)
                result.Add(new CoreBattleStatus("curse_threshold", "缠咒", N(battle.Schools.EnemyCurseExecutionThresholdPercent) + "%",
                    "咒印判定百分比降低至 " + N(battle.Schools.EnemyCurseExecutionThresholdPercent) + "% 。",
                    CoreBattleStatusKind.Curse, true));
            return result.AsReadOnly();
        }

        private static void AddNaval(List<CoreBattleStatus> result, CoreVoyageSession session, bool player)
        {
            CoreBattleState b = session.Battle;
            CoreSchoolBattleState s = b.Schools;
            if (player)
            {
                AddSpecialCardDoctrines(result, b);
                // Zero still has a meaningful multiplier, so these permanent stats stay visible.
                string cannonPercent = (CoreNavalStatRules.CannonDamageMultiplier(session.EffectiveFirepower) * 100m)
                    .ToString("0.#", CultureInfo.InvariantCulture);
                string armorPercent = (CoreNavalStatRules.ArmorDamageMultiplier(session.EffectiveArmor) * 100m)
                    .ToString("0.#", CultureInfo.InvariantCulture);
                result.Add(new CoreBattleStatus("firepower", "舰炮火力", N(session.EffectiveFirepower),
                    "当前海战炮击基础伤害倍率 " + cannonPercent + "%；火力 6 对应 100%，每点有效火力增加 8 个百分点。" +
                    "倍率不设上限。只缩放炮击基础伤害（含卡牌强化）；装填、流派等固定加伤另行相加。", CoreBattleStatusKind.Attack));
                result.Add(new CoreBattleStatus("armor", "船体装甲", N(session.EffectiveArmor),
                    "当前敌方炮击承伤比例 " + armorPercent + "%（装甲 2 对应 100%）；按比例计算后向上取整，最多减伤 " +
                    (100 - CoreNavalStatRules.MinimumArmorDamagePercent) + "%。" +
                    "先结算装甲，再结算航行诅咒与脉冲弱化，随后格挡可继续吸收。" +
                    "装甲不会因受击消耗，不减免冲撞、白刃或持续、场地伤害。", CoreBattleStatusKind.Armor));
                Add(result, "loaded_shot", "装填加伤", session.PlayerLoadedShotDamageBonus, CoreBattleStatusKind.Attack,
                    "下次炮击增加 " + N(session.PlayerLoadedShotDamageBonus) + " 点伤害；开炮时消耗。");
                Add(result, "deck_buff", "甲板强化", b.PlayerDeckBuff, CoreBattleStatusKind.Guard,
                    "本场海战中，船体防御牌额外获得 " + N(b.PlayerDeckBuff) + " 点格挡。");
                Add(result, "fire_rounds", "附火", b.PlayerFireRounds, CoreBattleStatusKind.Fire,
                    "下一次攻击给目标施加 " + N(b.PlayerFireRounds) + " 层燃烧，随后清除附火；攻击被格挡也会触发。");
                AddBurn(result, "burn", "燃烧", b.PlayerBurnStacks, b.PlayerBurnStacks, "玩家回合末");
                AddCurse(result, b.PlayerCurseMarks);
                Add(result, "prepare_fire_guard", "引火防护", s.PrepareFireGuard, CoreBattleStatusKind.Guard,
                    "下次成功施加燃烧时获得 " + N(s.PrepareFireGuard) + " 点格挡，随后消耗此效果。");
                if (s.NaturalFireGuardTurns > 0)
                    Add(result, "natural_fire_guard", "余焰护甲", s.NaturalFireGuard, CoreBattleStatusKind.Guard,
                        "敌方燃烧自然结算并造成伤害时，获得 " + N(s.NaturalFireGuard) + " 点格挡。" + EnemyEnds(s.NaturalFireGuardTurns));
                if (s.BoardingBurnTurns > 0)
                    Add(result, "boarding_burn", "接舷火种", s.BoardingBurn, CoreBattleStatusKind.Fire,
                        "进入接舷战时，给敌方船员施加 " + N(s.BoardingBurn) + " 层燃烧。");
                Add(result, "boarding_cover", "接舷掩护", s.BoardingCover, CoreBattleStatusKind.Guard,
                    "转入接舷战时提供 " + N(s.BoardingCover) + " 点掩护；敌方先手时先抵挡首击，余量转为船员格挡。");
                if (s.ScorchDamage > 0)
                    Add(result, "scorch", "灼烬追击", s.ScorchCharges, CoreBattleStatusKind.Fire,
                        "主动攻击已燃烧的敌方并造成实际伤害后，追加 " + N(s.ScorchDamage) + " 点直接伤害。剩余 " + N(s.ScorchCharges) + " 次。");
                Add(result, "thunder_ready", "雷击蓄势", s.ThunderReadyDamage, CoreBattleStatusKind.Thunder,
                    "下次雷霆攻击增加 " + N(s.ThunderReadyDamage) + " 点伤害，使用后消耗。");
                Add(result, "arcane_attack", "奥术蓄能", s.ArcaneNextAttackDamage, CoreBattleStatusKind.Arcane,
                    "下次海战攻击增加 " + N(s.ArcaneNextAttackDamage) + " 点伤害，使用后消耗。");
                Add(result, "arcane_guard", "奥术护盾", s.ArcaneNextBlockBonus, CoreBattleStatusKind.Arcane,
                    "下次获得格挡时，额外获得 " + N(s.ArcaneNextBlockBonus) + " 点格挡，触发后消耗。");
                Add(result, "tide_guard", "蓄潮格挡", Math.Min(b.PlayerBlock, s.TidePaidBlock), CoreBattleStatusKind.Tide,
                    "当前格挡中可积累潮势的部分。每实际吸收 1 点伤害，获得 1 点潮势；未花费的潮势保留至战斗结束。");
            }
            else
            {
                Add(result, "loaded_shot", "装填加伤", b.EnemyLoadedShot, CoreBattleStatusKind.Attack,
                    "敌舰下次炮击获得 " + N(b.EnemyLoadedShot) + " 点装填加伤，开炮后消耗。");
                AddBurn(result, "burn", "燃烧", b.EnemyBurnDamage, b.EnemyBurnTurns, "敌方回合末");
                AddCurse(result, s.CurseMarks, s.EnemyCurseExecutionThresholdPercent);
                Add(result, "tide_guard", "蓄潮格挡", Math.Min(b.EnemyBlock, b.EnemyTidalGuard), CoreBattleStatusKind.Tide,
                    "当前格挡中可积累潮势的部分。每实际吸收 1 点伤害，获得 1 点潮势；未花费的潮势保留至战斗结束。");
            }
            if (b.Stage != NavalBattleStage.Naval) return;
            CoreNavalFieldShipState field = player ? b.NavalFields.Player : b.NavalFields.Enemy;
            if (field != null)
            {
                if (!player && field.BossFieldImmunityMask != 0)
                    Flag(result, "boss_field_immunity", "场地防护", true, CoreBattleStatusKind.Immunity,
                        session.EnemyNavalBossFieldImmunityDescription);
                int ownerTurn = player ? b.NavalFields.PlayerTurn : b.NavalFields.EnemyTurn;
                bool preventsBurn = field.BurnPreventionExpiresAtOwnerTurn > ownerTurn ||
                    (field.BurnPreventionExpiresAtTurnEnd && field.BurnPreventionExpiresAtOwnerTurn == ownerTurn);
                Flag(result, "burn_prevention", "隔焰防护", preventsBurn, CoreBattleStatusKind.Immunity,
                    "直到下个我方回合开始时，不受到" + CoreComponentFieldCards.FieldName(CoreNavalFieldKind.Fire) + "场地的负面效果");
                if (field.FieldImmunityExpires > ownerTurn || field.TideImmunityExpiresAfterPlayerTurnEnd > 0)
                {
                    foreach (CoreNavalFieldKind kind in new[] { CoreNavalFieldKind.Curse,
                        CoreNavalFieldKind.Tide, CoreNavalFieldKind.Thunder, CoreNavalFieldKind.Blood })
                    {
                        bool tideTurnEnd = kind == CoreNavalFieldKind.Tide && field.TideImmunityExpiresAfterPlayerTurnEnd > 0;
                        if (!tideTurnEnd && (field.FieldImmunityExpires <= ownerTurn ||
                            (field.FieldImmunityMask & (1 << (int)kind)) == 0)) continue;
                        string name = CoreComponentFieldCards.FieldName(kind);
                        Flag(result, "field_protection_" + kind.ToString().ToLowerInvariant(), name + "防护", true,
                            CoreBattleStatusKind.Immunity, "直到下个我方回合开始时，不受到" + name + "场地的负面效果", value: "1");
                    }
                }
                Add(result, "anchor", "定锚", field.AnchorCharges, CoreBattleStatusKind.Anchor,
                    "可拒绝 " + N(field.AnchorCharges) + " 次有效强制位移；拒绝时消耗，到本方下次回合开始时到期。");
                Add(result, "movement_slow", "航速受阻", field.MovementSlow, CoreBattleStatusKind.Slow,
                    "下一次主动移动的可移动距离减少 " + N(field.MovementSlow) + " 格；实际主动移动后消耗，触发前保留。", true);
                Add(result, "movement_cap", "卸帆限速", field.NextMovementMaxCells, CoreBattleStatusKind.Slow,
                    "下一次主动移动至多移动 " + N(field.NextMovementMaxCells) + " 格；实际主动移动后消耗，触发前保留。", true);
            }
            CoreNavalExpansionBattleState e = b.NavalExpansion;
            if (e == null) return;
            if (player)
            {
                int precharge = e.PrechargeBonus > 0 ? e.PrechargeBonus : 2;
                Flag(result, "precharge", "预充线圈", e.Precharge, CoreBattleStatusKind.Thunder,
                    "本回合下次雷霆攻击完成后，额外获得 " + N(precharge) + " 层连锁；触发后或玩家回合末清除。", value: N(precharge));
                Flag(result, "grounding", "接地回流", e.GroundingReady && !e.GroundingUsed, CoreBattleStatusKind.Thunder,
                    "本回合首张对敌舰造成伤害的雷霆牌恢复 1 能量；格挡吸收的伤害也可触发。");
            }
            else
            {
                int runeClamp = e.RuneClampReduction > 0 ? e.RuneClampReduction : 6;
                Flag(result, "oil", "浸油", e.OilPlayerEndsRemaining > 0, CoreBattleStatusKind.Fire,
                    "下一次获得燃烧时，新增层数翻倍，随后移除浸油。", true);
                Flag(result, "rune_clamp", "符文钳制", e.RuneClamp, CoreBattleStatusKind.Seal,
                    "下次获得的格挡减少 " + N(runeClamp) + " 点，最低为 0；触发前保留，触发后清除。", true, N(runeClamp));
                int loadSealReduction = e.LoadSealReduction > 0 ? e.LoadSealReduction : 6;
                Flag(result, "load_seal", "蚀印封装", e.LoadSeal, CoreBattleStatusKind.Seal,
                    "下一次炮击伤害降低 " + N(loadSealReduction) + " 点，最低为 0；触发前保留，触发后清除。", true, N(loadSealReduction));
                Add(result, "pulse_reduction", "脉冲弱化", e.PulseReduction, CoreBattleStatusKind.Weakness,
                    "直到下个敌方回合结束，每次攻击造成的伤害减少 " + N(e.PulseReduction) + " 点，最低为 0。", true);
            }
        }

        private static void AddSpecialCardDoctrines(List<CoreBattleStatus> result, CoreBattleState battle)
        {
            if (battle.Stage != NavalBattleStage.Naval) return;
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            string[] names = { "炉心续燃", "星核回流", "咒印增殖", "潮汐蓄能", "连锁超载", "血契豁免" };
            string[] rules = {
                "每回合首张其他火焰牌结算后，给敌舰施加 3 层燃烧。",
                "每回合首张其他秘术牌结算后获得 1 点法力，可以超过法力上限。",
                "每回合首张其他诅咒牌结算后，给敌舰施加 1 层咒印，最多增至 6 层。",
                "每回合首张其他潮汐牌结算后获得 3 潮势，保留至战斗结束。",
                "每回合首张其他雷霆攻击结算后额外获得 1 连锁。",
                "每回合首张其他血契牌不支付生命成本；仍支付能量及其他耗材。"
            };
            CoreBattleStatusKind[] kinds = { CoreBattleStatusKind.Fire, CoreBattleStatusKind.Arcane,
                CoreBattleStatusKind.Curse, CoreBattleStatusKind.Tide, CoreBattleStatusKind.Thunder, CoreBattleStatusKind.Blood };
            for (int i = 0; i < schools.Length; i++)
                if (battle.SpecialCards.IsActive(schools[i]))
                    result.Add(new CoreBattleStatus("special_doctrine_" + schools[i], names[i],
                        battle.SpecialCards.HasPlayed(schools[i]) ? "已用" : "待触发",
                        rules[i] + "本场海战有效，同类不叠加，进入接舷或战斗结束时清除。激活牌本身不占额度；激活前本回合已打出的同系牌也计入首张。", kinds[i]));
        }

        private static void AddBoarding(List<CoreBattleStatus> result, CoreBattleState battle, bool player)
        {
            AddBoardingFields(result, battle.BoardingFields);
            CoreBoardingBattleState b = battle.Boarding;
            CoreBoardingEnemyState enemy = battle.BoardingEnemy;
            if (player)
            {
                AddDecayStacks(result, "burn", "燃烧", b.SelfBurn, "玩家回合末");
                Add(result, "fire_rounds", "附火", battle.PlayerFireRounds, CoreBattleStatusKind.Fire,
                    "下一次攻击给目标施加 " + N(battle.PlayerFireRounds) + " 层燃烧，随后清除附火；攻击被格挡也会触发。");
                AddCurse(result, enemy.CurseMarks);
                Add(result, "fire_pact", "共燃契约", b.FirePactTurns, CoreBattleStatusKind.Fire,
                    "每次主动对自身施加燃烧时，给敌方施加等量燃烧。剩余 " + N(b.FirePactTurns) + " 次玩家回合末。");
                Add(result, "fire_retaliation", "焰盾反击", b.FireRetaliations, CoreBattleStatusKind.Retaliation,
                    "每次用格挡抵挡敌方攻击后，进行 " + N(b.FireRetaliations) + " 次反击，每次伤害为当前燃烧层数；持续至战斗结束。");
                AddFirePots(result, b.FirePots);
                Add(result, "free_powder", "免耗火药", b.FreePowderUses, CoreBattleStatusKind.Fire,
                    "本回合接下来 " + N(b.FreePowderUses) + " 张需要火药的火焰牌，各减免 1 火药；玩家回合末清除。");
                AddRetainedGuard(result, battle);
                Add(result, "tide_guard", "蓄潮格挡", Math.Min(battle.PlayerBlock, b.TideGuard), CoreBattleStatusKind.Tide,
                    "当前格挡中可积累潮势的部分。每实际吸收 1 点伤害，获得 1 点潮势；未花费的潮势保留至战斗结束。");
                Flag(result, "tide_guard_conversion", "留潮", b.TideGuardConversionActive, CoreBattleStatusKind.Tide,
                    "本场战斗中，将当前全部格挡及后续获得的格挡转换为等量蓄潮格挡。", value: "本场");
                Flag(result, "arcane_echo", "回响刻印", b.ArcaneEchoReady, CoreBattleStatusKind.Arcane,
                    "下一张奥术攻击牌结算后，产生一次延迟攻击，伤害为本次原始攻击总伤害的一半；在下一次玩家回合开始结算。");
                Add(result, "thunder_ready", "电弧导流", b.ThunderReady, CoreBattleStatusKind.Thunder,
                    "本回合下一张雷霆攻击的首段伤害增加 " + N(b.ThunderReady) + "；延迟攻击也可获得加成。使用后或玩家回合末清除。");
                Add(result, "thunder_residual", "雷网残响", b.ThunderResidualCharges, CoreBattleStatusKind.Thunder,
                    "本回合接下来 " + N(b.ThunderResidualCharges) + " 张主动雷霆攻击各立即追加 " + N(b.ThunderResidualDamage) + " 点伤害；延迟攻击牌也在出牌时追加。玩家回合末清除。");
                Add(result, "curse_contract", "恶兆契约", b.CurseContractTurns, CoreBattleStatusKind.Curse,
                    "每次因敌方主动攻击失去生命值时，给敌方施加 " + N((int)Math.Min(int.MaxValue, (long)Math.Max(1, b.CurseContracts.Count) * 4)) + " 层咒印。" + EnemyEnds(b.CurseContractTurns));
                Add(result, "curse_link", "连环咒印", b.CurseLinkCharges, CoreBattleStatusKind.Curse,
                    "本回合接下来 " + N(b.CurseLinkCharges) + " 张主动攻击结算后，各给敌方施加 2 层咒印；玩家回合末清除。");
                Add(result, "curse_counter", "逆刃咒", b.CurseCounterTurns, CoreBattleStatusKind.Retaliation,
                    "每次敌方主动攻击造成伤害前，对其造成无视格挡的伤害。当前伤害 " + N(BoardingWindowAmount(b.CurseCounters, b.CurseCounterCard, Sum(b.CurseMarks, b.CurseMarks))) + "（随当前咒印层数变化）。" + EnemyEnds(b.CurseCounterTurns));
                Add(result, "tide_ambush", "潜流伏击", b.TideAmbushTurns, CoreBattleStatusKind.Retaliation,
                    "下次敌方主动攻击后，反击 " + N(BoardingWindowAmount(b.TideAmbushes, b.TideAmbushCard, 32)) + " 点伤害，不会暴击，触发后消耗。" + EnemyEnds(b.TideAmbushTurns));
                Add(result, "blood_debt", "血债追偿", b.BloodDebtTurns, CoreBattleStatusKind.Blood,
                    "敌方每段主动攻击造成伤害后，反击 " + N(Scale(b.BloodDebtCard, CoreBoardingCardBalance.Linear(8))) + " 点伤害；格挡吸收的伤害也触发，不会暴击。" + EnemyEnds(b.BloodDebtTurns));
                Add(result, "blood_oath", "绝境誓约", b.BloodOathPlayerEnds, CoreBattleStatusKind.Blood,
                    "每张主动攻击牌额外造成 " + N(Scale(b.BloodOathCard, CoreBoardingCardBalance.Linear(8))) + " 点伤害，延迟攻击牌也可获得加成；施放后恢复生命不会解除。剩余 " + N(b.BloodOathPlayerEnds) + " 次玩家回合末。");
                AddHealing(result, b);
                int retainedCards = 0;
                foreach (CoreBoardingHeldCard held in b.HeldCards) if (held.Retain && !held.RetainUsed) retainedCards++;
                Add(result, "retained_cards", "封存手牌", retainedCards, CoreBattleStatusKind.Draw,
                    "已封存 " + N(retainedCards) + " 张手牌，在本次回合结束时保留一次；各牌的费用变化仍在牌面显示。");
            }
            else
            {
                if (b.EnemyBurn.Count > 0)
                    AddDecayStacks(result, "burn", "燃烧", b.EnemyBurn, "敌方回合末");
                else if (!b.Initialized)
                    AddBurn(result, "burn", "燃烧", battle.EnemyBurnDamage, battle.EnemyBurnTurns, "敌方回合末");
                AddCurse(result, b.CurseMarks, battle.Schools.EnemyCurseExecutionThresholdPercent);
                Add(result, "retained_guard", "留潮盾", Math.Min(battle.EnemyBlock, enemy.RetainedGuard), CoreBattleStatusKind.Tide,
                    "当前格挡中的保留部分，可保留到下一次敌方行动开始一次；实际吸收攻击伤害时积累潮势。");
                if (b.ThunderWeaknessTurns > 0)
                    Add(result, "thunder_weakness", "雷霆弱化", b.ThunderWeakness, CoreBattleStatusKind.Weakness,
                        "下一次敌方攻击的伤害降低 " + N(b.ThunderWeakness) + " 点，最低为 0；触发后移除。", true);
                if (b.CurseWeakTurns > 0)
                    AddPersistentWeakness(result, "curse_weakness", "衰弱咒", BoardingWindowAmount(b.CurseWeaknesses, b.CurseWeakCard, Sum(6, b.CurseMarks)), b.CurseWeakTurns);
                if (b.TideWeakTurns > 0)
                    AddPersistentWeakness(result, "tide_weakness", "潮汐弱化", BoardingWindowAmount(b.TideWeaknesses, b.TideWeakCard, 6), b.TideWeakTurns);
                Add(result, "curse_armor", "蚀甲咒", b.CurseArmorTurns, CoreBattleStatusKind.Seal,
                    "每次获得的格挡减少 " + N(BoardingWindowAmount(b.CurseArmors, b.CurseArmorCard, Sum(4, b.CurseMarks))) + "（随当前咒印层数变化），最低为 0。" + EnemyEnds(b.CurseArmorTurns), true);
                AddDelayed(result, b);
                if (enemy.Deck?.SchoolId == "blood" && (long)battle.EnemyBoardingHealth * 100 <= (long)battle.EnemyMaxBoardingHealth * 40)
                    Flag(result, "desperate", "绝境", true, CoreBattleStatusKind.Blood,
                        "生命不高于上限 40%；血刃与收割类攻击额外增加 25% 基础攻击。恢复到阈值以上后不再生效。", false, "25%");
            }
        }

        private static void AddBoardingFields(List<CoreBattleStatus> result, CoreBoardingFieldState fields)
        {
            foreach (var field in fields.Fields)
            {
                if (field == null || field.RemainingTurns <= 0) continue;
                string key, name, meaning;
                CoreBattleStatusKind kind;
                switch (field.Kind)
                {
                    case CoreNavalFieldKind.Fire:
                        key = "fire"; name = "火场"; kind = CoreBattleStatusKind.Fire;
                        meaning = "主动攻击伤害提高 25%。本方回合结束时，先结算已有燃烧伤害，再获得等于火场剩余持续时间的燃烧。"; break;
                    case CoreNavalFieldKind.Arcane:
                        key = "arcane"; name = "折射阵"; kind = CoreBattleStatusKind.Arcane;
                        meaning = "本方回合开始时，获得等于折射阵剩余持续时间的法力；受到的主动攻击伤害提高 25%。"; break;
                    case CoreNavalFieldKind.Curse:
                        key = "curse"; name = "禁域"; kind = CoreBattleStatusKind.Curse;
                        meaning = "受到的主动攻击伤害降低 25%。本方回合结束时，获得等于禁域剩余持续时间的咒印，立即检查消灭条件。"; break;
                    case CoreNavalFieldKind.Thunder:
                        key = "thunder"; name = "雷栅"; kind = CoreBattleStatusKind.Thunder;
                        meaning = "连锁不会清空或消耗；每次增加至至少 50 时，受到 1000 点伤害，先扣格挡，连锁保留。"; break;
                    case CoreNavalFieldKind.Blood:
                        key = "blood"; name = "血池"; kind = CoreBattleStatusKind.Blood;
                        meaning = "主动支付生命值时，获得支付量两倍的格挡。本方回合结束时，流失等于血池剩余持续时间的生命，无视格挡、可致死，且不触发格挡返还。"; break;
                    default: continue;
                }
                result.Add(new CoreBattleStatus("boarding_field_" + key, name, N(field.RemainingTurns),
                    "由海战继承，作用于双方。" + meaning + "剩余 " + N(field.RemainingTurns) + " 回合；" +
                    (field.PlayerOwned ? "己方" : "敌方") + "场地，在" + (field.PlayerOwned ? "敌方" : "己方") +
                    "回合结束的场地效果结算后，持续时间减 1。", kind));
            }
        }

        private static void AddRetainedGuard(List<CoreBattleStatus> result, CoreBattleState battle)
        {
            CoreBoardingBattleState b = battle.Boarding;
            int amount = Math.Min(battle.PlayerBlock, b.ArcaneRetainedGuard);
            var detail = new StringBuilder("当前格挡中的奥术保留部分，受到攻击时仍会消耗；各批独立到期。");
            foreach (CoreBoardingRetainedGuard batch in b.RetainedGuardBatches)
                if (batch.Amount > 0 && batch.Expiry >= battle.Turn)
                    detail.Append("\n").Append(batch.Amount).Append(" 点：可保留至第 ").Append(batch.Expiry).Append(" 回合，下一回合开始时到期。");
            Add(result, "retained_guard", "奥术保留格挡", amount, CoreBattleStatusKind.Arcane, detail.ToString());
        }

        private static void AddHealing(List<CoreBattleStatus> result, CoreBoardingBattleState b)
        {
            int total = 0;
            foreach (CoreBoardingHealingTick heal in b.HealingTicks)
                if (heal.Amount > 0 && heal.Remaining > 0)
                    total = Sum(total, heal.Amount);
            Add(result, "healing", "持续恢复", total, CoreBattleStatusKind.Healing,
                "玩家回合末，先按当前层数恢复生命值，再结算燃烧；恢复后层数减少 1。新增层数相加，降至 0 层时结束；恢复不超过生命上限。");
        }

        private static void AddDelayed(List<CoreBattleStatus> result, CoreBoardingBattleState b)
        {
            int total = 0;
            var detail = new StringBuilder("待结算的攻击，不会暴击；在玩家回合开始时按出牌顺序结算。");
            foreach (CoreBoardingDelayedDamage delayed in b.DelayedDamage)
                if (delayed.Amount > 0 && delayed.Remaining > 0)
                {
                    total = Sum(total, delayed.Amount);
                    detail.Append("\n").Append(delayed.Label).Append("：").Append(delayed.Amount).Append(" 点")
                        .Append(delayed.IgnoreBlock ? "无视格挡伤害" : "可格挡伤害").Append("，再过 ").Append(delayed.Remaining).Append(" 次玩家回合开始结算。");
                }
            Add(result, "delayed_damage", "延迟攻击", total, CoreBattleStatusKind.Delayed, detail.ToString(), true);
        }

        private static void AddFirePots(List<CoreBattleStatus> result, List<CoreBoardingTimedStacks> pots)
        {
            int total = 0;
            foreach (CoreBoardingTimedStacks pot in pots)
                if (pot.Amount > 0 && pot.Remaining > 0) total = Sum(total, pot.Amount);
            Add(result, "fire_pots", "燃烧火壶", total, CoreBattleStatusKind.Fire,
                "下个己方回合开始时，给敌方施加 " + N(total) + " 层燃烧，随后清除本效果。");
        }

        private static void AddBurn(List<CoreBattleStatus> result, string id, string name, int damage, int turns, string timing)
        {
            if (turns <= 0) return;
            Add(result, id, name, damage, CoreBattleStatusKind.Fire,
                BurnTooltip(damage), true);
        }

        private static void AddDecayStacks(List<CoreBattleStatus> result, string id, string name,
            List<CoreBoardingTimedStacks> stacks, string timing)
        {
            int total = 0;
            foreach (CoreBoardingTimedStacks item in stacks)
                if (item.Amount > 0 && item.Remaining > 0) total = Sum(total, item.Amount);
            Add(result, id, name, total, CoreBattleStatusKind.Fire, BurnTooltip(total), true);
        }

        private static void AddCurse(List<CoreBattleStatus> result, int stacks,
            int thresholdPercent = CoreCurseRules.DefaultExecutionThresholdPercent) =>
            Add(result, "curse_marks", "咒印", stacks, CoreBattleStatusKind.Curse,
                "死神，来找你了！", true);

        private static string BurnTooltip(int stacks) =>
            "回合结束时，受到" + N(stacks) + "点伤害。";

        private static void AddWeakness(List<CoreBattleStatus> result, string id, string name, int amount, int turns) =>
            Add(result, id, name, amount, CoreBattleStatusKind.Weakness,
                "下一次主动攻击伤害减少 " + N(amount) + "，最多降低该击的 50%；与其它一次性弱化取最大值，不相加。触发后消耗。" + EnemyEnds(turns), true);

        private static void AddPersistentWeakness(List<CoreBattleStatus> result, string id, string name, int amount, int turns) =>
            Add(result, id, name, amount, CoreBattleStatusKind.Weakness,
                "每次主动攻击造成的伤害减少 " + N(amount) + "，最低为 0；持续期间每次攻击均生效。" + EnemyEnds(turns), true);

        private static void Add(List<CoreBattleStatus> result, string id, string name, int amount, CoreBattleStatusKind kind, string text, bool debuff = false)
        { if (amount > 0) result.Add(new CoreBattleStatus(id, name, N(amount), text, kind, debuff)); }
        private static void Flag(List<CoreBattleStatus> result, string id, string name, bool active, CoreBattleStatusKind kind, string text, bool debuff = false, string value = "1")
        { if (active) result.Add(new CoreBattleStatus(id, name, value, text, kind, debuff)); }
        private static int Sum(int a, int b) => (int)Math.Min(int.MaxValue, Math.Max(0L, a) + Math.Max(0L, b));
        private static int BoardingWindowAmount(List<CoreBoardingEffectWindow> windows, CoreBattleCard fallback, int basis)
        {
            if (windows.Count == 0) return Scale(fallback, basis);
            int amount = 0;
            foreach (CoreBoardingEffectWindow effect in windows) amount = Sum(amount, Scale(effect.Card, basis));
            return amount;
        }
        private static int Scale(CoreBattleCard card, int amount) => CoreCardUpgradeDesign.ScaleEffect(card, amount, card?.UpgradeLevel ?? 0);
        private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);
        private static string EnemyEnds(int turns) => "剩余 " + N(turns) + " 次敌方回合末，之后清除。";
        private static string Triggered(bool triggered) => triggered ? "本回合已触发。" : "本回合尚可触发。";
    }
}
