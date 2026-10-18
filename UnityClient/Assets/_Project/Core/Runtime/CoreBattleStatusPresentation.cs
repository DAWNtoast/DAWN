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
    /// curse marks, tide power, thunder chains, blood loss, energy and cargo are not statuses.
    /// Burn batches retain their individual durations instead of advertising the longest one
    /// as the duration of every stack. Legacy mirrors are never counted a second time.
    /// </summary>
    public static class CoreBattleStatusPresentation
    {
        public static string BuildResourceSummary(CoreVoyageSession session)
        {
            if (session?.Battle == null) return string.Empty;
            CoreBattleState battle = session.Battle;
            CoreSchoolBattleState s = battle.Schools;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            var text = new StringBuilder("己方体系资源");
            text.Append("\n法力 ").Append(s.Mana).Append('/').Append(s.ManaLimit);
            if (s.TemporaryMana > 0) text.Append(" · 临时 +").Append(s.TemporaryMana);
            text.Append("\n雷链 ").Append(s.ThunderChain).Append('/').Append(s.ThunderLimit);
            text.Append("\n潮势 ").Append(boarding ? battle.Boarding.TidePower : s.TidePower).Append('/').Append(boarding ? 12 : s.TideLimit);
            text.Append("\n对敌咒痕 ").Append(boarding ? battle.Boarding.CurseMarks : s.CurseMarks);
            if (boarding) text.Append("/6");
            else if (s.CurseMarks > 0) text.Append(" · ").Append(s.CurseTurns).Append(" 回合");
            CoreBoardingEnemyState enemy = battle.BoardingEnemy;
            if (boarding)
            {
                switch (enemy.Deck?.SchoolId)
                {
                    case "arcane": text.Append("\n\n敌方法力 ").Append(enemy.Mana).Append('/').Append(CoreBoardingEnemyState.ManaLimit); break;
                    case "thunder": text.Append("\n\n敌方雷链 ").Append(enemy.ThunderChain).Append('/').Append(CoreBoardingEnemyState.ThunderChainLimit); break;
                    case "curse": text.Append("\n\n敌方对我咒痕 ").Append(enemy.CurseMarks).Append('/').Append(CoreBoardingEnemyState.CurseMarkLimit); break;
                    case "tide": text.Append("\n\n敌方潮势 ").Append(enemy.TidePower).Append('/').Append(CoreBoardingEnemyState.TidePowerLimit); break;
                    case "blood": text.Append("\n\n敌方失血 ").Append(enemy.BloodLoss).Append('/').Append(CoreBoardingEnemyState.BloodLossLimit); break;
                }
            }
            else if (battle.NavalFields.Enemy.TemporaryMana > 0)
                text.Append("\n\n敌方临时法力 ").Append(battle.NavalFields.Enemy.TemporaryMana);
            return text.ToString();
        }

        public static IReadOnlyList<CoreBattleStatus> Build(CoreVoyageSession session, bool player)
        {
            if (session?.Battle == null) return Array.Empty<CoreBattleStatus>();
            var result = new List<CoreBattleStatus>();
            CoreBattleState battle = session.Battle;
            int guard = player ? battle.PlayerBlock : battle.EnemyBlock;
            Add(result, "guard", "格挡", guard, CoreBattleStatusKind.Guard,
                "优先吸收可格挡的伤害；每吸收 1 点伤害消耗 1 点格挡。普通格挡在持有者下次行动开始时清空，保留格挡按其独立时限结算。" +
                (battle.Stage == NavalBattleStage.Preparation && player ? "准备阶段的普通格挡保留至首轮炮战。" : string.Empty));
            if (player)
                Flag(result, "voyage_curse", "航行诅咒", session.CurseDamageBonusPercent > 0, CoreBattleStatusKind.Curse,
                    "承受经诅咒结算的战斗攻击伤害提高 " + N(session.CurseDamageBonusPercent) + "%；每层诅咒增加 10%，最多增加 100%。此状态来自航行诅咒，可在港口净化。",
                    true, N(session.CurseDamageBonusPercent) + "%");
            if (battle.Stage == NavalBattleStage.Boarding) AddBoarding(result, battle, player);
            else AddNaval(result, session, player);
            return result.AsReadOnly();
        }

        private static void AddNaval(List<CoreBattleStatus> result, CoreVoyageSession session, bool player)
        {
            CoreBattleState b = session.Battle;
            CoreSchoolBattleState s = b.Schools;
            if (player)
            {
                AddSpecialCardDoctrines(result, b);
                Add(result, "armor", "船体装甲", session.EffectiveArmor, CoreBattleStatusKind.Armor,
                    "船只的常驻装甲属性，不会因受击消耗。参与敌方船体攻击的减伤计算；不是额外生命或格挡。");
                Add(result, "crew_shortage", "水手不足", session.CrewCombatPenalty, CoreBattleStatusKind.Weakness,
                    "基础水手不足：火力降低 " + N(session.CrewCombatPenalty) + "，装甲降低 " + N(session.CrewCombatPenalty / 2) + "。补足基础水手后解除。", true);
                Add(result, "loaded_shot", "装填加伤", session.PlayerLoadedShotDamageBonus, CoreBattleStatusKind.Attack,
                    "下次炮击增加 " + N(session.PlayerLoadedShotDamageBonus) + " 点伤害，包含溢焰装药的加成；开炮时消耗。");
                Add(result, "deck_buff", "甲板强化", b.PlayerDeckBuff, CoreBattleStatusKind.Guard,
                    "本场海战中，船体防御牌额外获得 " + N(b.PlayerDeckBuff) + " 点格挡。");
                Add(result, "fire_rounds", "烈焰炮弹", b.PlayerFireRounds, CoreBattleStatusKind.Fire,
                    "剩余 " + N(b.PlayerFireRounds) + " 发。炮击消耗 1 发；造成实际伤害后点燃敌舰，每次燃烧造成 " + N(b.PlayerFireRoundDamage) + " 点伤害，持续 2 次结算。");
                Add(result, "prepare_fire_guard", "引火防护", s.PrepareFireGuard, CoreBattleStatusKind.Guard,
                    "下次成功施加燃烧时获得 " + N(s.PrepareFireGuard) + " 点格挡，随后消耗此效果。");
                if (s.NaturalFireGuardTurns > 0)
                    Add(result, "natural_fire_guard", "余焰护甲", s.NaturalFireGuard, CoreBattleStatusKind.Guard,
                        "敌方燃烧自然结算并造成伤害时，获得 " + N(s.NaturalFireGuard) + " 点格挡。" + EnemyEnds(s.NaturalFireGuardTurns));
                if (s.BoardingBurnTurns > 0)
                    Add(result, "boarding_burn", "接舷火种", s.BoardingBurn, CoreBattleStatusKind.Fire,
                        "进入接舷战时给敌方船员施加燃烧，每次 " + N(s.BoardingBurn) + " 点，持续 " + N(s.BoardingBurnTurns) + " 次结算。");
                Add(result, "boarding_cover", "接舷掩护", s.BoardingCover, CoreBattleStatusKind.Guard,
                    "转入接舷战时提供 " + N(s.BoardingCover) + " 点掩护；敌方先手时先抵挡首击，余量转为船员格挡。");
                if (s.ScorchDamage > 0)
                    Add(result, "scorch", "灼烬追击", s.ScorchCharges, CoreBattleStatusKind.Fire,
                        "主动攻击已燃烧的敌方并造成实际伤害后，追加 " + N(s.ScorchDamage) + " 点直接伤害。剩余 " + N(s.ScorchCharges) + " 次。");
                Add(result, "overflow", "溢焰装药", s.OverflowAmount, CoreBattleStatusKind.Fire,
                    "燃烧已达 3 次时，再延长燃烧可转为下次炮击加伤，每回合最多触发一次，每次 +" + N(s.OverflowAmount) +
                    "；本效果累计上限 " + N(s.OverflowOwnLimit) + "，总装填上限 " + N(s.OverflowTotalLimit) + "。" + (s.OverflowUsed ? "本回合已触发。" : "本回合尚可触发。"));
                Add(result, "thunder_ready", "雷击蓄势", s.ThunderReadyDamage, CoreBattleStatusKind.Thunder,
                    "下次雷霆攻击增加 " + N(s.ThunderReadyDamage) + " 点伤害，使用后消耗。");
                Add(result, "tide_guard", "潮汐格挡", Math.Min(b.PlayerBlock, s.TidePaidBlock), CoreBattleStatusKind.Tide,
                    "当前格挡中可积累潮势的部分。实际吸收敌方主动攻击伤害后转为潮势；地形伤害不会积累潮势。");
                Add(result, "curse_guard", "咒术护盾", Math.Min(b.PlayerBlock, s.CurseGuard), CoreBattleStatusKind.Curse,
                    "当前格挡中的临时咒术护盾；在下一次玩家回合开始时清空。准备阶段获得的咒术护盾在准备阶段结束时移除。");
            }
            else
            {
                Add(result, "loaded_shot", "装填加伤", b.EnemyLoadedShot, CoreBattleStatusKind.Attack,
                    "敌舰下次炮击获得 " + N(b.EnemyLoadedShot) + " 点装填加伤，开炮后消耗。");
                AddBurn(result, "burn", "燃烧", b.EnemyBurnDamage, b.EnemyBurnTurns, "敌方行动前");
                if (s.CurseMarks > 0 && s.CurseTurns > 0)
                    Add(result, "curse_tick_bonus", "咒印增伤", s.CurseNextTickBonus, CoreBattleStatusKind.Curse,
                        "下次敌方回合末，每层咒痕额外造成 " + N(s.CurseNextTickBonus) + " 点无视格挡伤害；结算后清除加成。", true);
            }
            if (b.Stage != NavalBattleStage.Naval) return;
            CoreNavalFieldShipState field = player ? b.NavalFields.Player : b.NavalFields.Enemy;
            if (field != null)
            {
                Add(result, "arcane_piercing", "奥术穿透", field.ArcanePiercing, CoreBattleStatusKind.Arcane,
                    "本回合下次奥术攻击中，最多 " + N(field.ArcanePiercing) + " 点伤害穿过格挡。使用后或本方回合末清除。");
                Add(result, "blood_attack", "血阵强化", field.BloodAttackBonus, CoreBattleStatusKind.Blood,
                    "本回合下次主动攻击增加 " + N(field.BloodAttackBonus) + " 点伤害。使用后或本方回合末清除。");
                Add(result, "fire_immunity", "火场免疫", field.FireImmunityCharges, CoreBattleStatusKind.Immunity,
                    "免疫接下来的 " + N(field.FireImmunityCharges) + " 次火场触发伤害；到本方下次回合开始时到期。只免疫火场，不免疫燃烧。");
                Add(result, "anchor", "定锚", field.AnchorCharges, CoreBattleStatusKind.Anchor,
                    "可拒绝 " + N(field.AnchorCharges) + " 次有效强制位移；拒绝时消耗，到本方下次回合开始时到期。");
                Add(result, "movement_slow", "航速受阻", field.MovementSlow, CoreBattleStatusKind.Slow,
                    "下一次主动移动的可移动距离减少 " + N(field.MovementSlow) + " 格；移动后消耗，未使用则按回合时限到期。", true);
            }
            CoreNavalExpansionBattleState e = b.NavalExpansion;
            if (e == null) return;
            if (player)
            {
                Flag(result, "precharge", "预充电", e.Precharge, CoreBattleStatusKind.Thunder,
                    "本回合下次雷霆攻击完成后额外获得 2 连锁；触发后或玩家回合末清除。");
                Flag(result, "grounding", "接地回流", e.GroundingReady && !e.GroundingUsed, CoreBattleStatusKind.Thunder,
                    "本回合雷霆攻击命中雷栅上的敌舰并造成实际伤害时，恢复 1 能量；本回合限触发一次。");
            }
            else
            {
                Add(result, "oil", "油浸标记", e.OilPlayerEndsRemaining, CoreBattleStatusKind.Fire,
                    "下次受到烈焰主动攻击并实际受伤时，额外承受 4 点可格挡伤害并获得每次 3 点、持续 2 次的燃烧，然后清除标记。剩余 " + N(e.OilPlayerEndsRemaining) + " 次玩家回合末。", true);
                Flag(result, "rune_clamp", "符文钳制", e.RuneClamp, CoreBattleStatusKind.Seal,
                    "下次获得的格挡减少 6，最低为 0；触发后或敌方回合末清除。", true);
                Flag(result, "load_seal", "装填封印", e.LoadSeal, CoreBattleStatusKind.Seal,
                    "下次获得的装填加伤减少 6，最低为 0；触发后或敌方回合末清除。", true);
                Add(result, "pulse_reduction", "脉冲弱化", e.PulseReduction, CoreBattleStatusKind.Weakness,
                    "下一次攻击伤害减少 " + N(e.PulseReduction) + "，最多降低该次攻击的 50%；触发后或敌方回合末清除。", true);
                Flag(result, "healing_halved", "禁疗", e.EnemyHealingHalved, CoreBattleStatusKind.Healing,
                    "本轮实际治疗量减半，向下取整；敌方回合末清除。", true, "50%");
            }
        }

        private static void AddSpecialCardDoctrines(List<CoreBattleStatus> result, CoreBattleState battle)
        {
            if (battle.Stage != NavalBattleStage.Naval) return;
            string[] schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };
            string[] names = { "炉心续燃", "星核回流", "咒印增殖", "潮汐蓄能", "雷链超载", "血契豁免" };
            string[] rules = {
                "每回合首张其他火焰牌结算后点燃敌舰：3 伤害，持续 2 回合。",
                "每回合首张其他秘术牌结算后恢复 1 法力，不超过法力上限。",
                "每回合首张其他诅咒牌结算后追加 1 层咒印，最多增至 6 层，持续至少 2 回合，每层至少 2 伤害。",
                "每回合首张其他潮汐牌结算后获得 3 潮势，不超过潮势上限。",
                "每回合首张其他雷霆攻击结算后额外获得 1 连锁，不超过连锁上限。",
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
            CoreBoardingBattleState b = battle.Boarding;
            CoreBoardingEnemyState enemy = battle.BoardingEnemy;
            if (player)
            {
                AddBatches(result, "self_burn", "自燃", b.SelfBurn, CoreBattleStatusKind.Fire, "玩家回合末", "直接失去", true);
                AddBurn(result, "enemy_burn", "敌方点燃", enemy.PlayerBurnDamage, enemy.PlayerBurnTurns, "玩家回合末");
                Add(result, "fire_pact", "共燃契约", b.FirePactTurns, CoreBattleStatusKind.Fire,
                    "每回合首次获得自燃时，将本次新增自燃的最多 3 层同时施加给敌方。剩余 " + N(b.FirePactTurns) + " 次玩家回合末。" + (b.FirePactUsed ? "本回合已触发。" : "本回合尚可触发。"));
                Add(result, "fire_retaliation", "焰盾反击", b.FireRetaliations, CoreBattleStatusKind.Retaliation,
                    "格挡实际吸收敌方攻击后，消耗 1 次并反击：伤害为自身燃烧层数 ×2，最多 8。剩余 " + N(b.FireRetaliations) + " 次；敌方回合末清除。");
                AddBatches(result, "fire_pots", "燃烧火壶", b.FirePots, CoreBattleStatusKind.Fire, "玩家回合开始", "给敌方施加", false, " 层燃烧（每批持续 2 次敌方回合末）");
                Add(result, "free_powder", "免耗火药", b.FreePowderUses, CoreBattleStatusKind.Fire,
                    "本回合接下来 " + N(b.FreePowderUses) + " 次需要火药的出牌免付火药；玩家回合末清除。");
                AddRetainedGuard(result, battle);
                Add(result, "tide_guard", "潮汐格挡", Math.Min(battle.PlayerBlock, b.TideGuard), CoreBattleStatusKind.Tide,
                    "当前格挡中的潮汐部分。每实际吸收 1 点敌方攻击伤害，获得 1 点潮势，最多 12；普通格挡与保留格挡先消耗。");
                Flag(result, "arcane_echo", "回响刻印", b.ArcaneEchoReady, CoreBattleStatusKind.Arcane,
                    "本回合下一张非延迟奥术攻击结算后，产生一次延迟攻击，伤害为本次原始攻击总伤害的一半，最多 " + N(Scale(b.ArcaneEchoCard, 18)) + "；在下一次玩家回合开始结算。玩家回合末未用则清除。");
                Add(result, "thunder_ready", "电弧导流", b.ThunderReady, CoreBattleStatusKind.Thunder,
                    "本回合下一张雷霆攻击的首段伤害增加 " + N(b.ThunderReady) + "；延迟攻击也可获得加成。使用后或玩家回合末清除。");
                Add(result, "thunder_residual", "雷网残响", b.ThunderResidualCharges, CoreBattleStatusKind.Thunder,
                    "本回合接下来 " + N(b.ThunderResidualCharges) + " 张雷霆攻击各追加 " + N(b.ThunderResidualDamage) + " 点伤害；延迟攻击的追加伤害也延迟结算。玩家回合末清除。");
                Add(result, "curse_contract", "恶兆契约", b.CurseContractTurns, CoreBattleStatusKind.Curse,
                    "每回合首次被敌方主动攻击造成生命损失时，给敌方增加 2 层咒痕。" + EnemyEnds(b.CurseContractTurns) + Triggered(b.CurseContractTriggered));
                Add(result, "curse_link", "连环咒印", b.CurseLinkCharges, CoreBattleStatusKind.Curse,
                    "本回合接下来 " + N(b.CurseLinkCharges) + " 张主动攻击结算后各给敌方增加 1 层咒痕；玩家回合末清除。");
                Add(result, "curse_counter", "逆刃咒", b.CurseCounterTurns, CoreBattleStatusKind.Retaliation,
                    "每回合首次受到敌方主动攻击后，造成无视格挡的反击伤害。当前伤害 " + N(Scale(b.CurseCounterCard, Math.Min(2 * b.CurseMarks, 10))) + "（随当前咒痕变化）。" + EnemyEnds(b.CurseCounterTurns) + Triggered(b.CurseCounterTriggered));
                Add(result, "tide_ambush", "潜流伏击", b.TideAmbushTurns, CoreBattleStatusKind.Retaliation,
                    "下次敌方主动攻击后，反击 " + N(Scale(b.TideAmbushCard, 16)) + " 点伤害，不会暴击，触发后消耗。" + EnemyEnds(b.TideAmbushTurns));
                Add(result, "blood_debt", "血债追偿", b.BloodDebtTurns, CoreBattleStatusKind.Blood,
                    "每回合首次被敌方主动攻击造成生命损失后，反击 " + N(Scale(b.BloodDebtCard, 8)) + " 点伤害，不会暴击。" + EnemyEnds(b.BloodDebtTurns) + Triggered(b.BloodDebtTriggered));
                Add(result, "blood_oath", "绝境誓约", b.BloodOathPlayerEnds, CoreBattleStatusKind.Blood,
                    "生命不高于上限 40% 时，每回合首次非延迟主动攻击增加 " + N(Scale(b.BloodOathCard, 8)) + " 点伤害。剩余 " + N(b.BloodOathPlayerEnds) + " 次玩家回合末。" + Triggered(b.BloodOathTriggered));
                AddHealing(result, b);
                int retainedCards = 0;
                foreach (CoreBoardingHeldCard held in b.HeldCards) if (held.Retain && !held.RetainUsed) retainedCards++;
                Add(result, "retained_cards", "封存手牌", retainedCards, CoreBattleStatusKind.Draw,
                    "已封存 " + N(retainedCards) + " 张手牌，在本次回合结束时保留一次；各牌的费用变化仍在牌面显示。");
            }
            else
            {
                if (b.EnemyBurn.Count > 0)
                    AddBatches(result, "burn", "燃烧", b.EnemyBurn, CoreBattleStatusKind.Fire, "敌方回合末", "直接失去", true);
                else if (!b.Initialized)
                    AddBurn(result, "burn", "燃烧", battle.EnemyBurnDamage, battle.EnemyBurnTurns, "敌方回合末");
                AddBurn(result, "self_burn", "自燃", enemy.SelfBurnDamage, enemy.SelfBurnTurns, "敌方回合末");
                Add(result, "retained_guard", "留潮盾", Math.Min(battle.EnemyBlock, enemy.RetainedGuard), CoreBattleStatusKind.Tide,
                    "当前格挡中的保留部分，可保留到下一次敌方行动开始一次；实际吸收攻击伤害时积累潮势。");
                if (b.ThunderWeaknessTurns > 0)
                    AddWeakness(result, "thunder_weakness", "雷霆弱化", b.ThunderWeakness, b.ThunderWeaknessTurns);
                if (b.CurseWeakTurns > 0)
                    AddWeakness(result, "curse_weakness", "衰弱咒", Scale(b.CurseWeakCard, 3 + b.CurseMarks), b.CurseWeakTurns);
                if (b.TideWeakTurns > 0)
                    AddWeakness(result, "tide_weakness", "潮汐弱化", Scale(b.TideWeakCard, 3), b.TideWeakTurns);
                Add(result, "curse_armor", "蚀甲咒", b.CurseArmorTurns, CoreBattleStatusKind.Seal,
                    "下次获得格挡时减少 " + N(Scale(b.CurseArmorCard, 2 + b.CurseMarks)) + "（随当前咒痕变化），最低为 0；触发后消耗。" + EnemyEnds(b.CurseArmorTurns), true);
                AddDelayed(result, b);
                if (enemy.Deck?.SchoolId == "blood" && (long)battle.EnemyBoardingHealth * 100 <= (long)battle.EnemyMaxBoardingHealth * 40)
                    Flag(result, "desperate", "绝境", true, CoreBattleStatusKind.Blood,
                        "生命不高于上限 40%；血刃与收割类攻击额外增加 25% 基础攻击。恢复到阈值以上后不再生效。", false, "25%");
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
            var detail = new StringBuilder("在玩家回合末先恢复生命，再结算自燃；各批独立结算，恢复不超过生命上限。");
            foreach (CoreBoardingHealingTick heal in b.HealingTicks)
                if (heal.Amount > 0 && heal.Remaining > 0)
                {
                    total = Sum(total, heal.Amount);
                    detail.Append("\n每次恢复 ").Append(heal.Amount).Append("，剩余 ").Append(heal.Remaining).Append(" 次。");
                }
            Add(result, "healing", "持续治疗", total, CoreBattleStatusKind.Healing, detail.ToString());
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

        private static void AddBatches(List<CoreBattleStatus> result, string id, string name, List<CoreBoardingTimedStacks> batches,
            CoreBattleStatusKind kind, string timing, string action, bool debuff, string units = " 点生命（无视格挡）")
        {
            var durations = new SortedDictionary<int, int>();
            int total = 0;
            foreach (CoreBoardingTimedStacks batch in batches)
                if (batch.Amount > 0 && batch.Remaining > 0)
                {
                    total = Sum(total, batch.Amount);
                    durations.TryGetValue(batch.Remaining, out int previous);
                    durations[batch.Remaining] = Sum(previous, batch.Amount);
                }
            var text = new StringBuilder("各批独立计时，新增不会刷新已有持续时间。");
            foreach (KeyValuePair<int, int> batch in durations)
                text.Append("\n").Append(timing).Append(action).Append(' ').Append(batch.Value).Append(units)
                    .Append("；剩余 ").Append(batch.Key).Append(" 次。");
            Add(result, id, name, total, kind, text.ToString(), debuff);
        }

        private static void AddBurn(List<CoreBattleStatus> result, string id, string name, int damage, int turns, string timing)
        {
            if (turns <= 0) return;
            Add(result, id, name, damage, CoreBattleStatusKind.Fire,
                timing + "直接失去 " + N(damage) + " 点生命，无视格挡；剩余 " + N(turns) + " 次。", true);
        }

        private static void AddWeakness(List<CoreBattleStatus> result, string id, string name, int amount, int turns) =>
            Add(result, id, name, amount, CoreBattleStatusKind.Weakness,
                "下一次主动攻击伤害减少 " + N(amount) + "，最多降低该击的 50%；与其它一次性弱化取最大值，不相加。触发后消耗。" + EnemyEnds(turns), true);

        private static void Add(List<CoreBattleStatus> result, string id, string name, int amount, CoreBattleStatusKind kind, string text, bool debuff = false)
        { if (amount > 0) result.Add(new CoreBattleStatus(id, name, N(amount), text, kind, debuff)); }
        private static void Flag(List<CoreBattleStatus> result, string id, string name, bool active, CoreBattleStatusKind kind, string text, bool debuff = false, string value = "1")
        { if (active) result.Add(new CoreBattleStatus(id, name, value, text, kind, debuff)); }
        private static int Sum(int a, int b) => (int)Math.Min(int.MaxValue, Math.Max(0L, a) + Math.Max(0L, b));
        private static int Scale(CoreBattleCard card, int amount) => CoreCardUpgradeDesign.ScaleEffect(card, amount, card?.UpgradeLevel ?? 0);
        private static string N(int number) => number.ToString(CultureInfo.InvariantCulture);
        private static string EnemyEnds(int turns) => "剩余 " + N(turns) + " 次敌方回合末；未触发则到期清除。";
        private static string Triggered(bool triggered) => triggered ? "本回合已触发。" : "本回合尚可触发。";
    }
}
